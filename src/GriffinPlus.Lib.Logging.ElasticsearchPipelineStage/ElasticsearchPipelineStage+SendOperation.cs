﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using GriffinPlus.Lib.Collections;
using GriffinPlus.Lib.Io;
using GriffinPlus.Lib.Threading;

// ReSharper disable ForCanBeConvertedToForeach

namespace GriffinPlus.Lib.Logging.Elasticsearch;

partial class ElasticsearchPipelineStage
{
	/// <summary>
	/// A send operation in the pipeline stage.
	/// </summary>
	private sealed class SendOperation
	{
		private readonly ElasticsearchPipelineStage mStage;
		private readonly JsonWriterOptions          mJsonWriterOptions      = new() { SkipValidation = true };
		private readonly Deque<LocalLogMessage>     mMessagesPreparedToSend = [];
		private          MemoryBlockStream          mContentStream;
		private          Utf8JsonWriter             mRequestContentWriter;
		private          HttpRequestMessage         mSendBulkRequestMessage;
		private          Task<HttpResponseMessage>  mSendBulkRequestTask;
		private          CancellationToken          mCancellationToken;

		/// <summary>
		/// Initializes a new instance of the <see cref="SendOperation"/> class.
		/// </summary>
		/// <param name="stage">The pipeline stage.</param>
		public SendOperation(ElasticsearchPipelineStage stage)
		{
			mStage = stage;
			mContentStream = new MemoryBlockStream(ArrayPool<byte>.Shared);
			mRequestContentWriter = new Utf8JsonWriter(mContentStream, mJsonWriterOptions);
			IsFull = false;
		}

		/// <summary>
		/// Gets a value indicating whether the request is full.
		/// </summary>
		public bool IsFull { get; private set; }

		/// <summary>
		/// Gets the number of messages that have been prepared to send.
		/// </summary>
		public int MessageCount => mMessagesPreparedToSend.Count;

		/// <summary>
		/// Gets a value indicating whether sending has completed (can be successful or error).
		/// </summary>
		public bool HasCompletedSending => mSendBulkRequestTask is { IsCompleted: true };

		/// <summary>
		/// Gets the bulk request endpoint of the Elasticsearch cluster the request was sent to.
		/// </summary>
		public EndpointInfo Endpoint { get; private set; }

		#region Reset

		/// <summary>
		/// Resets the send operation, so it can be re-used.
		/// </summary>
		public void Reset()
		{
			// discard messages that are still in the request preparation buffer
			for (int i = 0; i < mMessagesPreparedToSend.Count; i++) mMessagesPreparedToSend[i].Release();
			mMessagesPreparedToSend.Clear();

			// release the send task
			if (mSendBulkRequestTask != null)
			{
				// wait for the task to complete
				// (usually the task has completed before Reset() is called, but when the send operation is cancelled
				// the task might not be cancelled, yet... just wait until the task completes before proceeding, otherwise
				// disposing the task will throw an exception)
				if (!mSendBulkRequestTask.IsCompleted)
					mSendBulkRequestTask.WaitWithoutException(CancellationToken.None);

				mSendBulkRequestTask.Dispose();
				mSendBulkRequestTask = null;
			}

			// release the request message
			mSendBulkRequestMessage?.Dispose();
			mSendBulkRequestMessage = null;

			// reset the content stream
			ResetContentStream();
		}

		/// <summary>
		/// Resets the content stream and the <see cref="IsFull"/> property.
		/// </summary>
		private void ResetContentStream()
		{
			mContentStream?.Dispose(); // returns buffers back to the pool
			mContentStream = new MemoryBlockStream(ArrayPool<byte>.Shared);
			mRequestContentWriter = new Utf8JsonWriter(mContentStream, mJsonWriterOptions);
			IsFull = false;
		}

		#endregion

		#region Request Preparation

		/// <summary>
		/// Adds a message to the request.
		/// </summary>
		/// <param name="message">
		/// Message to add to the request
		/// (<see cref="LocalLogMessage.Release"/> is called automatically when sending completes).
		/// </param>
		/// <returns>
		/// <c>true</c> if the request was successfully added to the request;<br/>
		/// <c>false</c> if the request is full.
		/// </returns>
		public bool AddMessage(LocalLogMessage message)
		{
			// abort, if the maximum number of messages per request is reached
			if (mMessagesPreparedToSend.Count >= mStage.mBulkRequestMaxMessageCount)
				return false;

			// store current stream position to be able to truncate the stream in case of an error
			long lastStreamPosition = mContentStream.Position;

			// serialize indexing request for the message into the stream
			SerializeIndexingRequest(message);

			if (mContentStream.Position > mStage.mBulkRequestMaxSize)
			{
				// the maximum request size has been exceeded
				if (lastStreamPosition < 0)
				{
					// the current message is so big that it does not fit into a bulk request
					// => discard it
					mStage.WritePipelineError(
						$"Message is too large ({mContentStream.Position} bytes) to fit into a bulk request (max. {mStage.mBulkRequestMaxMessageCount} bytes). Discarding message...",
						null);
					message.Release();
					mContentStream.SetLength(0);
					return true; // pretend to have added it to the request...
				}

				// truncate the stream to contain only the last messages to avoid exceeding the maximum request size
				mContentStream.SetLength(lastStreamPosition);
				IsFull = true;
				return false;
			}

			// the message was added to the request successfully
			mMessagesPreparedToSend.AddToBack(message);
			return true;
		}

		/// <summary>
		/// Rebuilds the content stream, so it reflects changes in <see cref="mMessagesPreparedToSend"/>.
		/// </summary>
		private void RebuildContentStream()
		{
			// reset the content stream, before filling it afresh
			ResetContentStream();

			long lastStreamPosition = -1;
			for (int i = 0; i < mMessagesPreparedToSend.Count; i++)
			{
				// serialize indexing request
				LocalLogMessage message = mMessagesPreparedToSend[i];
				SerializeIndexingRequest(message);

				// determine whether the maximum request size is exceeded
				if (mContentStream.Position > mStage.mBulkRequestMaxSize)
				{
					if (lastStreamPosition < 0)
					{
						// the current message is so big that it does not fit into a bulk request
						// => discard it
						mStage.WritePipelineError(
							$"Message is too large ({mContentStream.Position} bytes) to fit into a bulk request (max. {mStage.mBulkRequestMaxMessageCount} bytes). Discarding message...",
							null);
						message.Release();
						mMessagesPreparedToSend.RemoveAt(i--);
						mContentStream.SetLength(0);
						continue;
					}

					// the maximum request size has been exceeded
					mContentStream.SetLength(lastStreamPosition);
					IsFull = true;
					break;
				}

				// the message was prepared to the request successfully
				lastStreamPosition = mContentStream.Position;
			}
		}

		/// <summary>
		/// Serializes an indexing operation in a bulk request
		/// </summary>
		/// <param name="message">Message to serialize.</param>
		private void SerializeIndexingRequest(LocalLogMessage message)
		{
			// serialize bulk request item object for indexing
			mRequestContentWriter.Reset();
			mStage.WriteBulkRequestIndexAction_ECS110(mRequestContentWriter);

			// finalize line with a newline character
			mContentStream.WriteByte((byte)'\n');

			// serialize the actual log message
			mRequestContentWriter.Reset();
			mStage.WriteJsonMessage_ECS110(mRequestContentWriter, message);

			// finalize line with a newline character
			mContentStream.WriteByte((byte)'\n');
		}

		#endregion

		#region Sending

		/// <summary>
		/// Starts sending the prepared request.
		/// </summary>
		/// <param name="endpoint">Endpoint the request is sent to.</param>
		/// <param name="cancellationToken">Cancellation token that can be signaled to abort the send operation.</param>
		/// <returns>
		/// <c>true</c> if sending was started successfully;<br/>
		/// otherwise <c>false</c>.
		/// </returns>
		public bool StartSending(EndpointInfo endpoint, CancellationToken cancellationToken)
		{
			Debug.Assert(mSendBulkRequestMessage == null, "A send operation is already pending.");
			Debug.Assert(MessageCount > 0);

			Endpoint = endpoint;
			mCancellationToken = cancellationToken;

			try
			{
				// create http request telling Elasticsearch to index the messages
				mContentStream.Position = 0;
				var content = new StreamContent(mContentStream);
				content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-ndjson");
				mSendBulkRequestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint.BulkRequestApiUrl) { Content = content };

				// send request asynchronously
				mSendBulkRequestTask = mStage.mHttpClient.SendAsync(
					mSendBulkRequestMessage,
					HttpCompletionOption.ResponseContentRead,
					cancellationToken);

				// trigger processing when the request is done
				mSendBulkRequestTask.ContinueWith(
					(_, state) => ((ElasticsearchPipelineStage)state).TriggerProcessing(),
					mStage,
					TaskContinuationOptions.ExecuteSynchronously);

				// sending was started successfully
				return true;
			}
			catch (OperationCanceledException)
			{
				// the cancellation token was signaled
				throw;
			}
			catch (Exception ex)
			{
				// some unexpected error occurred
				mStage.WritePipelineError($"An unexpected exception occurred sending a HTTP request to Elasticsearch endpoint ({mSendBulkRequestMessage?.RequestUri}) failed.", ex);
				return false;
			}
		}

		/// <summary>
		/// Processes a completed send operation (may have succeeded or failed).
		/// </summary>
		/// <returns>
		/// <c>true</c> if the endpoint is considered operational;<br/>
		/// otherwise <c>false</c>.
		/// </returns>
		public bool ProcessSendCompleted()
		{
			Debug.Assert(mSendBulkRequestTask != null);
			Debug.Assert(mSendBulkRequestTask.IsCompleted);

			BulkResponse bulkResponse = null;
			try
			{
				// get http response out of the task
				HttpResponseMessage response = mSendBulkRequestTask.WaitAndUnwrapException(); // should return immediately

				if (response.StatusCode == HttpStatusCode.OK)
				{
					// the bulk request was processed by the server
					// => deserialize the response
					byte[] responseData = response.Content.ReadAsByteArrayAsync().WaitAndUnwrapException(); // should return immediately
					bulkResponse = mStage.mBulkResponsePool.GetBulkResponse();
					bulkResponse.InitFromJson(responseData);

					// check response for elasticsearch errors
					if (bulkResponse.Errors)
					{
						// there are messages that were not indexed successfully
						// => evaluate the response
						List<BulkResponse.Item> bulkResponseItems = bulkResponse.Items;
						for (int i = bulkResponseItems.Count - 1; i >= 0; i--)
						{
							int status = bulkResponseItems[i].Create.Status;
							if (status is >= 200 and <= 299) // usually 201 (created)
							{
								// message was indexed successfully
								mMessagesPreparedToSend[i].Release();
								mMessagesPreparedToSend.RemoveAt(i);
							}
							else if (status == 429) // rate limit
							{
								// the server is under heavy load
								// => keep message and try again later
								mStage.WritePipelineWarning("Elasticsearch server seems to be overloaded (responded with 429). Trying again later...");
							}
							else
							{
								// indexing failed (no chance to solve this issue automatically)
								// => log error and discard message
								mStage.WritePipelineError(
									$"Elasticsearch endpoint ({mSendBulkRequestMessage?.RequestUri}) responded with {status}. Discarding message to index..." +
									Environment.NewLine +
									$"Error: {bulkResponseItems[i].Create.Error}",
									null);

								mMessagesPreparedToSend[i].Release();
								mMessagesPreparedToSend.RemoveAt(i);
							}
						}
					}
					else
					{
						// all messages were indexed successfully
						int count = bulkResponse.Items.Count;
						for (int i = 0; i < count; i++) mMessagesPreparedToSend[i].Release();
						mMessagesPreparedToSend.RemoveRange(0, count);
					}

					// rebuild the content stream, if there are messages left,
					// otherwise prepare for re-use
					if (mMessagesPreparedToSend.Count > 0) RebuildContentStream();
					else Reset();

					// request was send to the Elasticsearch cluster
					// => the endpoint can be considered operational...
					return true;
				}

				// log incident...
				mStage.WritePipelineError(
					$"The request to endpoint {mSendBulkRequestMessage?.RequestUri} failed with error code {(int)response.StatusCode} ({response.ReasonPhrase}).",
					null);

				// the bulk request failed entirely which is a severe condition
				// => the endpoint is not operational...
				// => rebuild the content stream for the next attempt (the HttpClient has disposed the stream)
				RebuildContentStream();
				return false;
			}
			catch (HttpRequestException ex)
			{
				// an error that occurred transporting the request to the elasticsearch server -or-
				// a timeout occurred (only .NET Framework)
				// => log and keep messages
				mStage.WritePipelineError($"Sending HTTP request to Elasticsearch endpoint ({mSendBulkRequestMessage?.RequestUri}) failed.", ex);
				RebuildContentStream();
				return false;
			}
			catch (OperationCanceledException ex)
			{
				// the cancellation token was signaled -or-
				// a timeout occurred (only .NET Core, .NET 5.0 or higher)
				if (ex.CancellationToken == mCancellationToken) throw;
				mStage.WritePipelineError($"Sending HTTP request to Elasticsearch endpoint ({mSendBulkRequestMessage?.RequestUri}) failed.", ex);
				RebuildContentStream();
				return false;
			}
			catch (Exception ex)
			{
				// an unexpected exception occurred
				mStage.WritePipelineError($"Sending HTTP request to Elasticsearch endpoint ({mSendBulkRequestMessage?.RequestUri}) failed.", ex);
				RebuildContentStream();
				return false;
			}
			finally
			{
				bulkResponse?.ReturnToPool();
				mSendBulkRequestMessage?.Dispose();
				mSendBulkRequestMessage = null;
				mSendBulkRequestTask?.Dispose();
				mSendBulkRequestTask = null;
			}
		}

		#endregion
	}
}
