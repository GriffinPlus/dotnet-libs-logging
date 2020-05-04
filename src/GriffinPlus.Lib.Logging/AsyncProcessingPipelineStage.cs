///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Base class for stages in the log message processing pipeline that feature asynchronous processing.
	/// Messages can be processed synchronously or asynchronously. Heavyweight operations and all operations
	/// that might block should be done asynchronously only to ensure that the thread writing a message is
	/// not blocked.
	/// </summary>
	public abstract class AsyncProcessingPipelineStage<STAGE> : IProcessingPipelineStage
		where STAGE: AsyncProcessingPipelineStage<STAGE>
	{
		private bool mInitialized = false;
		private IProcessingPipelineStage[] mNextStages = new IProcessingPipelineStage[0];
		private Task mAsyncProcessingTask;
		private AsyncAutoResetEvent mTriggerAsyncProcessingEvent;
		private LocklessStack<LocalLogMessage> mAsyncProcessingMessageStack;
		private bool mDiscardMessagesIfQueueFull = false;
		private int mMessageQueueSize = 500;
		private TimeSpan mShutdownTimeout = TimeSpan.FromMilliseconds(5000);
		private CancellationTokenSource mAsyncProcessingCancellationTokenSource;
		private bool mTerminateProcessingTask = false;

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncProcessingPipelineStage{T}"/> class.
		/// </summary>
		protected AsyncProcessingPipelineStage()
		{
		}

		/// <summary>
		/// Gets the object to use for synchronization of changes to the pipeline stage using a monitor.
		/// </summary>
		protected object Sync { get; } = new object();

		#region Initialization / Shutdown

		/// <summary>
		/// Gets a value indicating whether the pipeline stage is initialized, i.e. it is attached to the logging subsystem.
		/// </summary>
		public bool IsInitialized
		{
			get { lock (Sync) return mInitialized; }
		}

		/// <summary>
		/// Initializes the processing pipeline stage.
		/// This method is called by the logging subsystem and should not be called explicitly.
		/// </summary>
		public void Initialize()
		{
			lock (Sync)
			{
				if (mInitialized) {
					throw new InvalidOperationException("The pipeline stage is already initialized.");
				}

				try
				{
					// set up asynchronous processing
					mAsyncProcessingMessageStack = new LocklessStack<LocalLogMessage>(mMessageQueueSize, false);
					mTriggerAsyncProcessingEvent = new AsyncAutoResetEvent(false);
					mAsyncProcessingCancellationTokenSource = new CancellationTokenSource();
					mTerminateProcessingTask = false;
					mAsyncProcessingTask = Task.Factory.StartNew(ProcessingTask).Unwrap();

					// Perform pipeline stage specific initializations.
					OnInitialize();

					// Initialize the following pipeline stages as well. This must be done within the pipeline lock of the current
					// stage to ensure that all pipeline stages or none at all are initialized.
					for (int i = 0; i < mNextStages.Length; i++) {
						mNextStages[i].Initialize();
					}

					// The pipeline stage is initialized now.
					mInitialized = true;
				}
				catch (Exception)
				{
					Shutdown();
					throw;
				}
			}
		}

		/// <summary>
		/// When overridden in a derived class, performs pipeline stage specific initialization tasks that must run when
		/// the pipeline stage is attached to the logging subsystem. This method is called from within the pipeline stage
		/// lock (<see cref="Sync"/>).
		/// </summary>
		protected virtual void OnInitialize()
		{

		}

		/// <summary>
		/// Shuts the processing pipeline stage down gracefully (works for a partially initialized pipeline stage as well).
		/// This method is called by the logging subsystem and should not be called explicitly.
		/// This method must not throw exceptions.
		/// </summary>
		public void Shutdown()
		{
			lock (Sync)
			{
				// shut down the following pipeline stages first
				for (int i = 0; i < mNextStages.Length; i++) {
					mNextStages[i].Shutdown();
				}

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
				try {
					OnShutdown();
				} catch (Exception ex) {
					Debug.Fail("OnShutdown() failed.", ex.ToString());
				}

				// shutting down completed
				mInitialized = false;
			}
		}

		/// <summary>
		/// When overridden in a derived class, performs pipeline stage specific cleanup tasks that must run when the
		/// pipeline stage is about to be detached from the logging subsystem. This method is called from within the
		/// pipeline stage lock (<see cref="Sync"/>).
		/// </summary>
		protected internal virtual void OnShutdown()
		{

		}

		#endregion

		#region Chaining Pipeline Stages

		/// <summary>
		/// Gets or sets processing pipeline stages that are called after the current stage has completed processing.
		/// The return value of <see cref="ProcessSync(LocalLogMessage, out bool)"/> determines whether these stages are called.
		/// </summary>
		public IProcessingPipelineStage[] NextStages
		{
			get
			{
				lock (Sync)
				{
					IProcessingPipelineStage[] copy = new IProcessingPipelineStage[mNextStages.Length];
					Array.Copy(mNextStages, copy, mNextStages.Length);
					return copy;
				}
			}

			set
			{
				if (value == null) throw new ArgumentNullException();

				lock (Sync)
				{
					EnsureNotAttachedToLoggingSubsystem();
					IProcessingPipelineStage[] copy = new IProcessingPipelineStage[value.Length];
					Array.Copy(value, copy, value.Length);
					mNextStages = copy;
				}
			}
		}

		/// <summary>
		/// Gets all pipeline stages following the current stage recursively (including the current one).
		/// </summary>
		/// <param name="stages">Set to add the pipeline stages to.</param>
		public void GetAllStages(HashSet<IProcessingPipelineStage> stages)
		{
			lock (Sync)
			{
				stages.Add(this);
				for (int i = 0; i < mNextStages.Length; i++)
				{
					mNextStages[i].GetAllStages(stages);
				}
			}
		}

		/// <summary>
		/// Configures the specified pipeline stage to receive log messages, when the current stage has completed running
		/// its <see cref="Process(LocalLogMessage)"/> method. The method must return <c>true</c> to call the following stage.
		/// </summary>
		/// <param name="stage">The pipeline stage that should follow the current stage.</param>
		public void AddNextStage(IProcessingPipelineStage stage)
		{
			lock (Sync)
			{
				EnsureNotAttachedToLoggingSubsystem();
				IProcessingPipelineStage[] copy = new IProcessingPipelineStage[mNextStages.Length + 1];
				Array.Copy(mNextStages, copy, mNextStages.Length);
				copy[copy.Length - 1] = stage;
				NextStages = copy;
			}
		}

		#endregion

		#region Pipeline Stage Settings

		/// <summary>
		/// When overridden in a derived class, returns a dictionary containing the default settings the pipeline stage
		/// operates with.
		/// </summary>
		/// <returns>Dictionary with default settings</returns>
		public virtual IReadOnlyDictionary<string,string> GetDefaultSettings()
		{
			return new Dictionary<string, string>();
		}

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

		#region Processing

		/// <summary>
		/// Processes the specified log message calling the <see cref="ProcessSync(LocalLogMessage, out bool)"/> method
		/// and passes the log message to the next processing stages, if <see cref="ProcessSync(LocalLogMessage, out bool)"/>
		/// returns <c>true</c>.
		/// </summary>
		/// <param name="message">Message to process.</param>
		public void Process(LocalLogMessage message)
		{
			if (message == null) throw new ArgumentNullException(nameof(message));

			lock (Sync)
			{
				if (!mInitialized)
				{
					throw new InvalidOperationException("The pipeline stage is not initialized. Ensure it is attached to the logging subsystem.");
				}

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

				if (proceed)
				{
					// pass log message to the next pipeline stages
					for (int i = 0; i < mNextStages.Length; i++)
					{
						mNextStages[i].Process(message);
					}
				}
			}
		}

		/// <summary>
		/// When overridden in a derived class, processes the specified log message synchronously.
		/// This method is called by the thread writing the message and from within the pipeline stage lock (<see cref="Sync"/>).
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

		#region Helpers

		/// <summary>
		/// Throws an <see cref="InvalidOperationException"/>, if the pipeline stage is already initialized (attached to the logging subsystem).
		/// </summary>
		protected void EnsureNotAttachedToLoggingSubsystem()
		{
			if (mInitialized) {
				throw new InvalidOperationException("The pipeline stage is already initialized. Configure the stage before attaching it to the logging subsystem.");
			}
		}

		#endregion

	}

}
