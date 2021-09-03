///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using GriffinPlus.Lib.Collections;
using GriffinPlus.Lib.Threading;

// ReSharper disable ForCanBeConvertedToForeach

namespace GriffinPlus.Lib.Logging.Elasticsearch
{

	partial class ElasticsearchPipelineStage
	{
		/// <summary>
		/// A send operation in the pipeline stage.
		/// </summary>
		private sealed class SendOperation
		{
			private readonly ElasticsearchPipelineStage mStage;
			private readonly Deque<LocalLogMessage>     mMessagesPreparedToSend;
			private readonly MemoryStream               mContentStream;
			private readonly Utf8JsonWriter             mRequestContentWriter;
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
				mMessagesPreparedToSend = new Deque<LocalLogMessage>();
				mContentStream = new MemoryStream();
				var jsonWriterOptions = new JsonWriterOptions { SkipValidation = true };
				mRequestContentWriter = new Utf8JsonWriter(mContentStream, jsonWriterOptions);
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
			public bool HasCompletedSending => mSendBulkRequestTask != null && mSendBulkRequestTask.IsCompleted;

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

				// reset the content stream
				ResetContentStream();

				// release the send task
				// mSendBulkRequestTask?.Dispose(); // do not dispose, otherwise mContentStream is closed as well
				mSendBulkRequestTask = null;

				// release the request message
				// mSendBulkRequestMessage?.Dispose(); // do not dispose, otherwise mContentStream is closed as well
				mSendBulkRequestMessage = null;
			}

			/// <summary>
			/// Resets the content stream and the <see cref="IsFull"/> property.
			/// </summary>
			private void ResetContentStream()
			{
				mRequestContentWriter.Reset();
				mContentStream.SetLength(0);
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
			/// <c>true</c> if the the request was successfully added to the request;
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

				// abort, if the maximum number of bytes in the bulk request is reached
				if (mContentStream.Position > mStage.mBulkRequestMaxSize)
				{
					if (lastStreamPosition < 0)
					{
						// the current message is so big that it does not fit into a bulk request
						// => discard it
						mStage.WritePipelineError(
							$"Message is too large ({mContentStream.Position} bytes) to fit into a bulk request (max. {mStage.mBulkRequestMaxSize} bytes). Discarding message...",
							null);
						message.Release();
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
					var message = mMessagesPreparedToSend[i];
					SerializeIndexingRequest(message);

					// abort, if the maximum number of bytes in the bulk request is reached
					if (mContentStream.Position > mStage.mBulkRequestMaxSize)
					{
						if (lastStreamPosition < 0)
						{
							// the current message is so big that it does not fit into a bulk request
							// => discard it
							mStage.WritePipelineError(
								$"Message is too large ({mContentStream.Position} bytes) to fit into a bulk request (max. {mStage.mBulkRequestMaxSize} bytes). Discarding message...",
								null);
							message.Release();
							mMessagesPreparedToSend.RemoveAt(i--);
							continue;
						}

						// truncate the stream to contain only the last messages to avoid exceeding the maximum request size
						mContentStream.SetLength(lastStreamPosition);
						IsFull = true;
						break;
					}

					lastStreamPosition = mContentStream.Position;
				}
			}

			/// <summary>
			/// Serializes an indexing operation in a bulk request
			/// </summary>
			/// <param name="message"></param>
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
			/// <param name="endpoint">Endpoint the request is send to.</param>
			/// <param name="cancellationToken">Cancellation token that can be signaled to abort the send operation.</param>
			/// <returns>
			/// <c>true</c> if sending was started successfully;
			/// otherwise <c>false</c>.
			/// </returns>
			public bool StartSending(EndpointInfo endpoint, CancellationToken cancellationToken)
			{
				Debug.Assert(mSendBulkRequestMessage == null, "A send operation is already pending.");

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
			/// Processes a completed send operation (may be successful or not).
			/// </summary>
			/// <returns>
			/// <c>true</c> if the endpoint is considered operational;
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
					var response = mSendBulkRequestTask.WaitAndUnwrapException(); // should return immediately

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
							// => evaluate the the response
							var bulkResponseItems = bulkResponse.Items;
							for (int i = bulkResponseItems.Count - 1; i >= 0; i--)
							{
								int status = bulkResponseItems[i].Index.Status;
								if (status >= 200 && status <= 299) // usually 201 (created)
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
										$"Error: {bulkResponseItems[i].Index.Error}",
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
					return false;
				}
				catch (HttpRequestException ex)
				{
					// an error that occurred transporting the request to the elasticsearch server -or-
					// a timeout occurred (only .NET Framework)
					// => log and keep messages
					mStage.WritePipelineError($"Sending HTTP request to Elasticsearch endpoint ({mSendBulkRequestMessage?.RequestUri}) failed.", ex);
					return false;
				}
				catch (OperationCanceledException ex)
				{
					// the cancellation token was signaled -or-
					// a timeout occurred (only .NET Core, .NET 5.0 or higher)
					if (ex.CancellationToken == mCancellationToken) throw;
					mStage.WritePipelineError($"Sending HTTP request to Elasticsearch endpoint ({mSendBulkRequestMessage?.RequestUri}) failed.", ex);
					return false;
				}
				catch (Exception ex)
				{
					// an unexpected exception occurred
					mStage.WritePipelineError($"Sending HTTP request to Elasticsearch endpoint ({mSendBulkRequestMessage?.RequestUri}) failed.", ex);
					return false;
				}
				finally
				{
					bulkResponse?.ReturnToPool();
					// mSendBulkRequestMessage?.Dispose(); // do not dispose, otherwise mContentStream is closed as well
					mSendBulkRequestMessage = null;
					// mSendBulkRequestTask?.Dispose(); // do not dispose, otherwise mContentStream is closed as well
					mSendBulkRequestTask = null;
				}
			}

			#endregion
		}
	}

}
