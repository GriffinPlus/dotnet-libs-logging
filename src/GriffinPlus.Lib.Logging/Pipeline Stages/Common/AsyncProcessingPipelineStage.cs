﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2019-2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable EmptyConstructor
// ReSharper disable ForCanBeConvertedToForeach

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Base class for stages in the log message processing pipeline that feature asynchronous processing.
	/// Messages can be processed synchronously or asynchronously. Heavyweight operations and all operations
	/// that might block should be done asynchronously only to ensure that the thread writing a message is
	/// not blocked.
	/// </summary>
	public abstract class AsyncProcessingPipelineStage<STAGE> : ProcessingPipelineBaseStage
		where STAGE: AsyncProcessingPipelineStage<STAGE>
	{
		private Task mAsyncProcessingTask;
		private AsyncAutoResetEvent mTriggerAsyncProcessingEvent;
		private LocklessStack<LocalLogMessage> mAsyncProcessingMessageStack;
		private bool mDiscardMessagesIfQueueFull;
		private int mMessageQueueSize = 500;
		private TimeSpan mShutdownTimeout = TimeSpan.FromMilliseconds(5000);
		private CancellationTokenSource mAsyncProcessingCancellationTokenSource;
		private bool mTerminateProcessingTask;

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncProcessingPipelineStage{T}"/> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		protected AsyncProcessingPipelineStage(string name) : base(name)
		{

		}

		#region Initialization / Shutdown

		/// <summary>
		/// Initializes the processing pipeline stage (base class specific part).
		/// </summary>
		internal override void OnInitializeBase()
		{
			try
			{
				// set up asynchronous processing
				mAsyncProcessingMessageStack = new LocklessStack<LocalLogMessage>(mMessageQueueSize, false);
				mTriggerAsyncProcessingEvent = new AsyncAutoResetEvent(false);
				mAsyncProcessingCancellationTokenSource = new CancellationTokenSource();
				mTerminateProcessingTask = false;
				mAsyncProcessingTask = Task.Factory.StartNew(ProcessingTask).Unwrap();

				// bind settings
				BindSettings();

				// Perform pipeline stage specific initialization
				OnInitialize();
			}
			catch (Exception)
			{
				(this as IProcessingPipelineStage).Shutdown();
				throw;
			}
		}

		/// <summary>
		/// Shuts the processing pipeline down (base class specific part).
		/// This method must not throw exceptions.
		/// </summary>
		internal override void OnShutdownBase()
		{
			// tell the processing thread to terminate
			// (it will try to process the last messages and exit)
			mTerminateProcessingTask = true;
			mAsyncProcessingCancellationTokenSource?.CancelAfter(mShutdownTimeout);
			mTriggerAsyncProcessingEvent?.Set();
			mAsyncProcessingTask?.Wait();

			// the processing task should have completed its work
			Debug.Assert(mAsyncProcessingTask == null || mAsyncProcessingTask.IsCompleted);

			// the stack should be empty now...
			Debug.Assert(mAsyncProcessingMessageStack == null || mAsyncProcessingMessageStack.UsedItemCount == 0);

			// clean up processing thread related stuff
			mAsyncProcessingCancellationTokenSource?.Dispose();
			mAsyncProcessingTask = null;
			mAsyncProcessingCancellationTokenSource = null;
			mAsyncProcessingMessageStack = null;

			// perform pipeline stage specific cleanup
			try
			{
				OnShutdown();
			}
			catch (Exception ex)
			{
				// swallow exception to avoid crashing the application, if the exception is not handled properly
				Debug.Fail("The pipeline stage failed shutting down.", ex.ToString());
			}
		}

		#endregion

		#region Settings

		/// <summary>
		/// Gets or sets a value indicating whether messages are discarded when the queue is full.
		/// The default is <c>false</c>.
		/// </summary>
		public bool DiscardMessagesIfQueueFull
		{
			get
			{
				lock (Sync)
				{
					return mDiscardMessagesIfQueueFull;
				}
			}

			set
			{
				lock (Sync)
				{
					EnsureNotAttachedToLoggingSubsystem();
					mDiscardMessagesIfQueueFull = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the capacity of the queue buffering messages to be processed asynchronously.
		/// The default is 500.
		/// </summary>
		public int MessageQueueSize
		{
			get
			{
				lock (Sync)
				{
					return mMessageQueueSize;
				}
			}

			set
			{
				if (value < 1) throw new ArgumentOutOfRangeException(nameof(MessageQueueSize), "The shutdown timeout must be >= 1.");

				lock (Sync)
				{
					EnsureNotAttachedToLoggingSubsystem();
					mMessageQueueSize = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the shutdown timeout (in ms).
		/// The default is 5000 ms.
		/// </summary>
		public TimeSpan ShutdownTimeout
		{
			get
			{
				lock (Sync)
				{
					return mShutdownTimeout;
				}
			}

			set
			{
				if (value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(value), "The shutdown timeout must be positive.");

				lock (Sync)
				{
					mShutdownTimeout = value;
				}
			}
		}

		#endregion

		#region Processing Messages and Notifications

		/// <summary>
		/// Is called on behalf of <see cref="IProcessingPipelineStage.Shutdown"/> (for internal use only).
		/// This method must not throw exceptions.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <returns>
		/// true to pass the message to the following stages;
		/// false to stop processing the message.
		/// </returns>
		internal override bool OnProcessMessageBase(LocalLogMessage message)
		{
			try
			{
				// synchronous processing
				bool proceed = ProcessSync(message, out var queueMessageForAsynchronousProcessing);

				// asynchronous processing
				if (queueMessageForAsynchronousProcessing)
				{
					if (mDiscardMessagesIfQueueFull)
					{
						message.AddRef();
						bool pushed = mAsyncProcessingMessageStack.Push(message);
						if (pushed) mTriggerAsyncProcessingEvent.Set();
						else message.Release();
					}
					else
					{
						message.AddRef();
						while (!mAsyncProcessingMessageStack.Push(message)) Thread.Sleep(10);
						mTriggerAsyncProcessingEvent.Set();
					}
				}

				return proceed;
			}
			catch (Exception ex)
			{
				// swallow exception to avoid crashing the application, if the exception is not handled properly
				Debug.Fail("The pipeline stage threw an exception processing the message.", ex.ToString());

				// let the following stages process the message
				// (hopefully this is the right decision in this case)
				return true;
			}
		}

		/// <summary>
		/// When overridden in a derived class, processes the specified log message synchronously.
		/// This method is called by the thread writing the message and from within the pipeline stage lock.
		/// This method must not throw exceptions.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <param name="queueForAsyncProcessing">
		/// Receives a value indicating whether the message should be enqueued for asynchronous processing.
		/// </param>
		/// <returns>
		/// true to pass the message to the following pipeline stages;
		/// otherwise false.
		/// </returns>
		/// <remarks>
		/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		protected virtual bool ProcessSync(LocalLogMessage message, out bool queueForAsyncProcessing)
		{
			queueForAsyncProcessing = true;
			return true;
		}

		/// <summary>
		/// When overridden in a derived class, processes the specified log messages asynchronously
		/// (is executed in the context of a worker thread).
		/// </summary>
		/// <param name="messages">Messages to process.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		/// <remarks>
		/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		protected virtual Task ProcessAsync(LocalLogMessage[] messages, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// The processing task that runs until the pipeline stage is shut down.
		/// </summary>
		private async Task ProcessingTask()
		{
			while (true)
			{
				// wait for messages to process
				await mTriggerAsyncProcessingEvent
					.WaitAsync(Timeout.Infinite, mAsyncProcessingCancellationTokenSource.Token)
					.ConfigureAwait(false);

				// process the messages
				await ProcessQueuedMessages(mAsyncProcessingCancellationTokenSource.Token).ConfigureAwait(false);

				// abort, if requested
				if (mTerminateProcessingTask)
				{
					// process the last messages, if there is time left...
					if (!mAsyncProcessingCancellationTokenSource.IsCancellationRequested) {
						await ProcessQueuedMessages(mAsyncProcessingCancellationTokenSource.Token).ConfigureAwait(false);
					}

					break;
				}
			}
		}

		/// <summary>
		/// Processes log messages that have been buffered in the <see cref="mAsyncProcessingMessageStack"/> (for asynchronous processing only).
		/// </summary>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		private async Task ProcessQueuedMessages(CancellationToken cancellationToken)
		{
			LocalLogMessage[] messages = mAsyncProcessingMessageStack.FlushAndReverse();
			if (messages == null) return;

			try
			{
				await ProcessAsync(messages, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Debug.Fail("ProcessAsync() threw an unhandled exception.", ex.ToString());
			}
			finally
			{
				// release message to let them return to the pool
				for (int i = 0; i < messages.Length; i++) {
					messages[i].Release();
				}
			}
		}

		#endregion
	}

}
