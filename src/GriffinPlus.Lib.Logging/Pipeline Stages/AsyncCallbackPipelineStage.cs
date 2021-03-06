﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A processing pipeline stage that invokes a callback to process a log message (thread-safe).
	/// This is a lightweight alternative to implementing an entire custom processing pipeline stage.
	/// </summary>
	public class AsyncCallbackPipelineStage : AsyncProcessingPipelineStage<AsyncCallbackPipelineStage>
	{
		/// <summary>
		/// A delegate that processes the specified log message (synchronous processing).
		/// </summary>
		/// <param name="message">Log message to process.</param>
		/// <param name="queueForAsyncProcessing">
		/// Receives a value indicating whether the message should be enqueued for asynchronous processing.
		/// </param>
		/// <returns>
		/// true to call the following pipeline stages;
		/// false to stop processing.
		/// </returns>
		public delegate bool SynchronousProcessingCallback(LocalLogMessage message, out bool queueForAsyncProcessing);

		/// <summary>
		/// A delegate that processes the specified log messages (asynchronous processing).
		/// </summary>
		/// <param name="messages">Messages to process.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		/// <remarks>
		/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		public delegate Task AsynchronousProcessingCallback(LocalLogMessage[] messages, CancellationToken cancellationToken);

		/// <summary>
		/// The synchronous message processing callback.
		/// </summary>
		private readonly SynchronousProcessingCallback mSynchronousProcessingCallback;

		/// <summary>
		/// The asynchronous message processing callback.
		/// </summary>
		private readonly AsynchronousProcessingCallback mAsynchronousProcessingCallback;

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncCallbackPipelineStage"/> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <param name="processSyncCallback">
		/// Callback processing a log message traveling through the pipeline (may be null).
		/// The callback is executed in the context of the thread writing the message.
		/// </param>
		/// <param name="processAsyncCallback">
		/// Callback processing a log message traveling through the pipeline (may be null).
		/// The callback is executed by a worker thread.
		/// </param>
		/// <remarks>
		/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		public AsyncCallbackPipelineStage(
			string                         name,
			SynchronousProcessingCallback  processSyncCallback,
			AsynchronousProcessingCallback processAsyncCallback) : base(name)
		{
			mSynchronousProcessingCallback = processSyncCallback;
			mAsynchronousProcessingCallback = processAsyncCallback;
		}

		/// <summary>
		/// Processes the specified log message synchronously (is executed in the context of the thread writing the message).
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <param name="queueForAsyncProcessing">
		/// Receives a value indicating whether the message should be enqueued for asynchronous processing.
		/// </param>
		/// <remarks>
		/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		protected override bool ProcessSync(LocalLogMessage message, out bool queueForAsyncProcessing)
		{
			if (mSynchronousProcessingCallback != null) return mSynchronousProcessingCallback(message, out queueForAsyncProcessing);
			return base.ProcessSync(message, out queueForAsyncProcessing);
		}

		/// <summary>
		/// Processes the specified log messages asynchronously
		/// (is executed in the context of a worker thread).
		/// </summary>
		/// <param name="messages">Messages to process.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		/// <remarks>
		/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		protected override Task ProcessAsync(LocalLogMessage[] messages, CancellationToken cancellationToken)
		{
			if (mAsynchronousProcessingCallback != null) return mAsynchronousProcessingCallback(messages, cancellationToken);
			return base.ProcessAsync(messages, cancellationToken);
		}
	}

}
