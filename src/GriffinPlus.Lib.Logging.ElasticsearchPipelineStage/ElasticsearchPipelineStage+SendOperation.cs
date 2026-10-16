///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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
	internal sealed class SendOperation
	{
		private readonly ElasticsearchPipelineStage mStage;
		private readonly JsonWriterOptions          mJsonWriterOptions      = new() { SkipValidation = true };
		private readonly Deque<LocalLogMessage>     mMessagesPreparedToSend = [];
		private          MemoryBlockStream          mContentStream;
		private          Utf8JsonWriter             mRequestContentWriter;
		private          HttpRequestMessage         mSendBulkRequestMessage;
		private          Task<HttpResponseMessage>  mSendBulkRequestTask;
		private          CancellationToken          mCancellationToken;
		private          long                       mSentRequestSize;
		private          int                        mRequestMessageCount;
		private          int                        mSentMessageCount;

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
			mRequestMessageCount = 0;
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
		/// <see langword="true"/> if the request was successfully added to the request;<br/>
		/// <see langword="false"/> if the request is full.
		/// </returns>
		public bool AddMessage(LocalLogMessage message)
		{
			// abort, if the maximum number of messages per request is reached
			if (mMessagesPreparedToSend.Count >= mStage.mBulkRequestMaxMessageCount)
				return false;

			// store current stream position to be able to truncate the stream in case of an error
			long lastStreamPosition = mContentStream.Position;

			// serialize indexing request for the message into the stream
			if (!TrySerializeIndexingRequest(message, lastStreamPosition))
			{
				// the message could not be serialized
				// => discard it to avoid blocking the processing queue
				message.Release();
				return true; // pretend to have added it to the request...
			}

			if (mContentStream.Position > mStage.mEffectiveBulkRequestMaxSize)
			{
				// the maximum request size has been exceeded
				if (mMessagesPreparedToSend.Count == 0)
				{
					// the current message is so big that it does not fit into a bulk request
					// => discard it
					mStage.WritePipelineError(
						$"Message is too large ({mContentStream.Position - lastStreamPosition} bytes) to fit into a bulk request (max. {mStage.mEffectiveBulkRequestMaxSize} bytes). Discarding message...",
						null);
					message.Release();
					mContentStream.SetLength(lastStreamPosition);
					return true; // pretend to have added it to the request...
				}

				// truncate the stream to contain only the last messages to avoid exceeding the maximum request size
				mContentStream.SetLength(lastStreamPosition);
				IsFull = true;
				return false;
			}

			// the message was added to the request successfully
			mMessagesPreparedToSend.AddToBack(message);
			mRequestMessageCount++;
			return true;
		}

		/// <summary>
		/// Rebuilds the content stream, so it reflects changes in <see cref="mMessagesPreparedToSend"/>.
		/// Messages that cannot be serialized or that are too large to fit into a bulk request are released
		/// and removed from <see cref="mMessagesPreparedToSend"/>, so the remaining messages can still be sent.
		/// </summary>
		internal void RebuildContentStream()
		{
			// reset the content stream, before filling it afresh
			ResetContentStream();

			long lastStreamPosition = -1;
			for (int i = 0; i < mMessagesPreparedToSend.Count; i++)
			{
				// serialize indexing request
				LocalLogMessage message = mMessagesPreparedToSend[i];
				if (!TrySerializeIndexingRequest(message, Math.Max(lastStreamPosition, 0)))
				{
					// the message could not be serialized
					// => discard it, but keep the other messages
					message.Release();
					mMessagesPreparedToSend.RemoveAt(i--);
					continue;
				}

				// determine whether the maximum request size is exceeded
				if (mContentStream.Position > mStage.mEffectiveBulkRequestMaxSize)
				{
					if (lastStreamPosition < 0)
					{
						// the current message is so big that it does not fit into a bulk request
						// => discard it
						mStage.WritePipelineError(
							$"Message is too large ({mContentStream.Position} bytes) to fit into a bulk request (max. {mStage.mEffectiveBulkRequestMaxSize} bytes). Discarding message...",
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
				mRequestMessageCount++;
			}
		}

		/// <summary>
		/// Gets or sets a callback that is invoked while serializing an indexing request, after the bulk request action
		/// has been written and before the message itself is written (for testing purposes only, <see langword="null"/> by default).
		/// Throwing an exception in the callback simulates a serialization failure leaving a partially written request.
		/// </summary>
		internal Action<LocalLogMessage> SerializationTestHook { get; set; }

		/// <summary>
		/// Gets the stream containing the prepared request content (for testing purposes only).
		/// </summary>
		internal MemoryBlockStream ContentStream => mContentStream;

		/// <summary>
		/// Serializes an indexing operation in a bulk request and truncates the content stream to the specified position,
		/// if serialization fails (the failure is logged as a pipeline error).
		/// </summary>
		/// <param name="message">Message to serialize.</param>
		/// <param name="streamPositionBefore">Position of the content stream before serializing the message.</param>
		/// <returns>
		/// <see langword="true"/> if the message was serialized successfully;<br/>
		/// <see langword="false"/> if serialization failed and the content stream was restored.
		/// </returns>
		private bool TrySerializeIndexingRequest(LocalLogMessage message, long streamPositionBefore)
		{
			try
			{
				SerializeIndexingRequest(message);
				return true;
			}
			catch (Exception ex)
			{
				mStage.WritePipelineError("Serializing message for a bulk request failed. Discarding message...", ex);
				mRequestContentWriter.Reset(); // discards data that has not been flushed, yet
				mContentStream.SetLength(streamPositionBefore);
				return false;
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

			SerializationTestHook?.Invoke(message);

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
		/// <see langword="true"/> if sending was started successfully;<br/>
		/// otherwise, <see langword="false"/>.
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
				mSentRequestSize = mContentStream.Length;
				mSentMessageCount = mRequestMessageCount;
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
		/// <see langword="true"/> if the endpoint is considered operational;<br/>
		/// otherwise, <see langword="false"/>.
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

					// ensure that the response matches the request
					// (the response items are assigned to the sent messages by position)
					if (!IsBulkResponseMatchingRequest(bulkResponse))
					{
						mStage.WritePipelineError(
							$"The response of endpoint {mSendBulkRequestMessage?.RequestUri} does not match the request " +
							$"(expected {mSentMessageCount} items with a 'create' result, received {bulkResponse.Items?.Count.ToString() ?? "no"} items).",
							null);

						// keep all messages and send the request once again
						// => the endpoint is not considered operational
						RebuildContentStream();
						return false;
					}

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

				if ((int)response.StatusCode == 413)
				{
					// the server rejected the request as too large
					// => the endpoint is operational, but the request must be smaller
					HandleRequestEntityTooLarge();
					return true;
				}

				// log incident including the error details provided by the server (if any)...
				string errorDescription = GetErrorResponseDescription(response);
				mStage.WritePipelineError(
					$"The request to endpoint {mSendBulkRequestMessage?.RequestUri} failed with error code {(int)response.StatusCode} ({response.ReasonPhrase})." +
					(errorDescription != null ? Environment.NewLine + $"Error: {errorDescription}" : ""),
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

		/// <summary>
		/// Checks whether the specified bulk response contains exactly one item with a <c>create</c> result for each
		/// message in the sent request, so the items can be assigned to the sent messages by position.
		/// </summary>
		/// <param name="bulkResponse">Bulk response to check.</param>
		/// <returns>
		/// <see langword="true"/> if the response matches the sent request;<br/>
		/// otherwise, <see langword="false"/>.
		/// </returns>
		private bool IsBulkResponseMatchingRequest(BulkResponse bulkResponse)
		{
			List<BulkResponse.Item> items = bulkResponse.Items;
			if (items == null || items.Count != mSentMessageCount) return false;
			for (int i = 0; i < items.Count; i++)
			{
				if (items[i].Create == null) return false;
			}

			return true;
		}

		/// <summary>
		/// Gets a description of the error reported in the body of an error response.
		/// </summary>
		/// <param name="response">Error response to evaluate.</param>
		/// <returns>
		/// The error description (see <see cref="GetErrorResponseDescription(byte[])"/>);<br/>
		/// <see langword="null"/> if the response does not provide any error details or reading the body failed.
		/// </returns>
		private static string GetErrorResponseDescription(HttpResponseMessage response)
		{
			try
			{
				if (response.Content == null) return null;
				byte[] responseData = response.Content.ReadAsByteArrayAsync().WaitAndUnwrapException(); // should return immediately
				return GetErrorResponseDescription(responseData);
			}
			catch (Exception)
			{
				// reading the body failed
				// => the error is logged without details
				return null;
			}
		}

		/// <summary>
		/// Gets a description of the error reported in the body of an Elasticsearch error response, e.g.
		/// <c>{"error":{"type":"...","reason":"..."},"status":400}</c>.
		/// </summary>
		/// <param name="responseData">Body of the error response.</param>
		/// <returns>
		/// <c>"&lt;type&gt;: &lt;reason&gt;"</c> if the <c>error</c> property is an object (the type or the reason
		/// alone, if only one of them is present), the value of the <c>error</c> property if it is a string;<br/>
		/// <see langword="null"/> if the body is not a JSON object with an <c>error</c> property providing any of this information.
		/// </returns>
		internal static string GetErrorResponseDescription(byte[] responseData)
		{
			if (responseData == null || responseData.Length == 0)
				return null;

			try
			{
				using JsonDocument document = JsonDocument.Parse(responseData);
				if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("error", out JsonElement error))
					return null;

				if (error.ValueKind == JsonValueKind.String)
					return error.GetString();

				if (error.ValueKind != JsonValueKind.Object)
					return null;

				string type = error.TryGetProperty("type", out JsonElement typeElement) && typeElement.ValueKind == JsonValueKind.String ? typeElement.GetString() : null;
				string reason = error.TryGetProperty("reason", out JsonElement reasonElement) && reasonElement.ValueKind == JsonValueKind.String ? reasonElement.GetString() : null;
				if (type != null && reason != null) return $"{type}: {reason}";
				return type ?? reason;
			}
			catch (JsonException)
			{
				// the body is not valid JSON
				return null;
			}
		}

		/// <summary>
		/// Handles a request the server rejected as too large (HTTP status 413).
		/// The maximum bulk request size in effect is reduced and the request is rebuilt, so messages that do not fit
		/// into the smaller request remain in the send operation and are sent with subsequent requests. If the maximum
		/// size is already at its minimum, the oldest message is discarded to guarantee progress.
		/// </summary>
		private void HandleRequestEntityTooLarge()
		{
			if (!mStage.ReduceEffectiveBulkRequestMaxSize(mSentRequestSize))
			{
				// the request cannot be made any smaller by reducing the maximum request size
				// => discard the oldest message to avoid sending the same request over and over again
				mStage.WritePipelineError(
					$"Elasticsearch endpoint ({mSendBulkRequestMessage?.RequestUri}) rejected a bulk request of {mSentRequestSize} bytes as too large (413), " +
					$"although the maximum bulk request size is already at its minimum ({mStage.mEffectiveBulkRequestMaxSize} bytes). Discarding message...",
					null);

				mMessagesPreparedToSend.RemoveFromFront().Release();
			}

			// rebuild the request using the reduced maximum request size,
			// otherwise prepare for re-use
			if (mMessagesPreparedToSend.Count > 0) RebuildContentStream();
			else Reset();
		}

		#endregion
	}
}
