///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018-2019 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using GriffinPlus.Lib.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Base class for stages in the log message processing pipeline.
	/// </summary>
	public abstract class ProcessingPipelineStage<T> : IProcessingPipelineStage
		where T: ProcessingPipelineStage<T>
	{
		// generic
		private bool mIsInitialized = false;
		private IProcessingPipelineStage[] mNextStages;
		private ThreadLocal<LocalLogMessage[]> mLogMessagesBeingProcessed = new ThreadLocal<LocalLogMessage[]>(() => new LocalLogMessage[1]);

		// asynchronous processing stuff
		private Thread mAsyncProcessingThread;
		private ManualResetEventSlim mTriggerAsyncProcessingEvent;
		private LocklessStack<LocalLogMessage> mAsyncProcessingMessageStack;
		private bool mDiscardIfAsyncProcessingQueueFull = false;
		private bool mAsyncProcessingEnabled = false;
		private int mAsyncProcessingQueueSize = 1;
		private bool mTerminateProcessingThread = false;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessingPipelineStage{T}"/> class.
		/// </summary>
		public ProcessingPipelineStage()
		{
			mNextStages = new IProcessingPipelineStage[0];
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessingPipelineStage{T}"/> class by copying another instance.
		/// </summary>
		/// <param name="other">Instance to copy.</param>
		protected ProcessingPipelineStage(T other)
		{
			mNextStages = new IProcessingPipelineStage[0];
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessingPipelineStage{T}"/> class (for internal use only).
		/// </summary>
		/// <param name="nextStages">The next processing pipeline stages.</param>
		private ProcessingPipelineStage(IProcessingPipelineStage[] nextStages)
		{
			mNextStages = nextStages;
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
			get { lock (Sync) return mIsInitialized; }
		}

		/// <summary>
		/// Initializes the processing pipeline stage.
		/// This method is called by the logging subsystem and should not be called explicitly.
		/// </summary>
		public void Initialize()
		{
			lock (Sync)
			{
				if (mIsInitialized) {
					throw new InvalidOperationException("The pipeline stage is already initialized.");
				}

				try
				{
					// set up asynchronous processing, if configured
					if (mAsyncProcessingEnabled)
					{
						mAsyncProcessingMessageStack = new LocklessStack<LocalLogMessage>(mAsyncProcessingQueueSize, false);
						mTriggerAsyncProcessingEvent = new ManualResetEventSlim(false);

						// start processing thread as foreground thread to force the user to explicitly
						// shut the logging subsystem down and allow pipeline stages to flush their pipelines
						mAsyncProcessingThread = new Thread(ProcessingThreadProc);
						mAsyncProcessingThread.Name = $"Log Message Processing Thread ({GetType().FullName})";
						mAsyncProcessingThread.Start();
					}

					// perform pipeline stage specific initializations
					InitializeCore();

					// Initialize the following pipeline stages as well. This must be done within the pipeline lock of the current
					// stage to ensure that all pipeline stages or none at all are initialized.
					var nextStages = Volatile.Read(ref mNextStages);
					for (int i = 0; i < nextStages.Length; i++) {
						nextStages[i].Initialize();
					}

					// the pipeline stage is initialized now
					mIsInitialized = true;
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
		/// the pipeline stage is attached to the logging subsystem. This method is called from within the pipeline stage lock.
		/// </summary>
		protected virtual void InitializeCore()
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
				var nextStages = Volatile.Read(ref mNextStages);
				for (int i = 0; i < nextStages.Length; i++) {
					nextStages[i].Shutdown();
				}

				// tell the processing thread to terminate
				// (it will try to process the last messages and exit)
				mTerminateProcessingThread = true;
				mTriggerAsyncProcessingEvent?.Set();
				mAsyncProcessingThread?.Join();

				// the stack should be empty now...
				Debug.Assert(mAsyncProcessingMessageStack == null || mAsyncProcessingMessageStack.UsedItemCount == 0);

				// clean up processing thread related stuff
				mTriggerAsyncProcessingEvent?.Dispose();
				mTriggerAsyncProcessingEvent = null;
				mAsyncProcessingThread = null;
				mAsyncProcessingMessageStack = null;

				// perform pipeline stage specific cleanup
				try {
					ShutdownCore();
				} catch (Exception ex) {
					Debug.Fail("ShutdownCore() failed.", ex.ToString());
				}

				// shutting down completed
				mIsInitialized = false;
			}
		}

		/// <summary>
		/// When overridden in a derived class, performs pipeline stage specific cleanup tasks that must run when the
		/// pipeline stage is about to be detached from the logging subsystem. This method is called from within the
		/// pipeline stage lock.
		/// </summary>
		protected internal virtual void ShutdownCore()
		{

		}

		#endregion

		#region Next Pipeline Stages

		/// <summary>
		/// Gets processing pipeline stages to call after the current stage has completed processing.
		/// </summary>
		protected IProcessingPipelineStage[] NextStages
		{
			get { return Volatile.Read(ref mNextStages); }
		}

		/// <summary>
		/// Gets all pipeline stages following the current stage (including the current one).
		/// </summary>
		/// <param name="stages">Set to add the pipeline stages to.</param>
		public void GetAllStages(HashSet<IProcessingPipelineStage> stages)
		{
			stages.Add(this);
			var nextStages = Volatile.Read(ref mNextStages);
			for (int i = 0; i < nextStages.Length; i++) {
				nextStages[i].GetAllStages(stages);
			}
		}

		/// <summary>
		/// Links the specified pipeline stages to the current stage.
		/// </summary>
		/// <param name="nextStages">Pipeline stages to pass log messages to, when the current stage has completed.</param>
		/// <returns>The updated pipeline stage.</returns>
		public T FollowedBy(params IProcessingPipelineStage[] nextStages)
		{
			lock (Sync)
			{
				int count = NextStages.Length + nextStages.Length;
				IProcessingPipelineStage[] newNextStages = new IProcessingPipelineStage[count];
				Array.Copy(NextStages, newNextStages, NextStages.Length);
				Array.Copy(nextStages, 0, newNextStages, NextStages.Length, nextStages.Length);
				Volatile.Write(ref mNextStages, newNextStages);
			}

			return this as T;
		}

		#endregion

		#region Pipeline Stage Settings

		/// <summary>
		/// When overridden in a derived class, returns a dictionary containing the default settings the
		/// pipeline stage operates with.
		/// </summary>
		/// <returns>Dictionary with default settings</returns>
		public virtual IDictionary<string,string> GetDefaultSettings()
		{
			return new Dictionary<string, string>();
		}

		#endregion

		#region Processing Log Messages

		/// <summary>
		/// Processes the specified log message calling the <see cref="ProcessCore(LocalLogMessage[])"/> method
		/// and passes the log message to the next processing stages, if <see cref="ProcessCore(LocalLogMessage[])"/>
		/// returns with <c>true</c>.
		/// </summary>
		/// <param name="message">Message to process.</param>
		public void Process(LocalLogMessage message)
		{
			if (!mIsInitialized) {
				throw new InvalidOperationException("The pipeline stage is not initialized. Ensure it is attached to the logging subsystem.");
			}

			bool proceed = true;

			if (mAsyncProcessingThread != null)
			{
				// asynchronous processing
				message.AddRef();
				if (mDiscardIfAsyncProcessingQueueFull)
				{
					bool pushed = mAsyncProcessingMessageStack.Push(message);
					if (pushed) mTriggerAsyncProcessingEvent.Set();
				}
				else
				{
					while (!mAsyncProcessingMessageStack.Push(message)) Thread.Sleep(10);
					mTriggerAsyncProcessingEvent.Set();
				}
			}
			else
			{
				// synchronous processing
				var messages = mLogMessagesBeingProcessed.Value; // thread-local to reduce gc pressure
				messages[0] = message;
				proceed = ProcessCore(messages);
			}

			if (proceed)
			{
				// pass log message to the next pipeline stages
				var stages = Volatile.Read(ref mNextStages);
				for (int i = 0; i < stages.Length; i++) {
					stages[i].Process(message);
				}
			}
		}

		/// <summary>
		/// When overridden in a derived class, processes the specified log messages.
		/// </summary>
		/// <param name="messages">Messages to process.</param>
		/// <returns>
		/// true to call the following processing stages;
		/// false to stop processing.
		/// </returns>
		/// <remarks>
		/// Call <see cref="LogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		protected abstract bool ProcessCore(LocalLogMessage[] messages);

		/// <summary>
		/// Enables processing log messages asynchronously.
		/// The following stages are always called, independent of the return value of <see cref="ProcessCore(LocalLogMessage[])"/>.
		/// </summary>
		/// <param name="queueSize">Maximum number of messages that should be queued for asynchronous processing.</param>
		/// <param name="discardIfQueueFull">
		/// true to discard messages, if the queue is full (recommended);
		/// false to block until the message is processed (lossless-mode, not recommended).
		/// </param>
		/// <returns>The updated pipeline stage.</returns>
		public T UseAsynchronousProcessing(int queueSize = 500, bool discardIfQueueFull = true)
		{
			if (queueSize < 1) throw new ArgumentException("The queue size must be positive.", nameof(queueSize));

			lock (Sync)
			{
				if (mIsInitialized) {
					throw new InvalidOperationException("The pipeline stage is already attached to the logging subsystem. Please configure it before attaching it.");
				}

				mAsyncProcessingEnabled = true;
				mAsyncProcessingQueueSize = queueSize;
				mDiscardIfAsyncProcessingQueueFull = discardIfQueueFull;
			}

			return this as T;
		}

		/// <summary>
		/// The entry point of the thread processing the log messages asynchronously.
		/// </summary>
		private void ProcessingThreadProc()
		{
			while (true)
			{
				mTriggerAsyncProcessingEvent.Wait();
				mTriggerAsyncProcessingEvent.Reset();
				ProcessQueuedMessages();

				// abort, if requested
				if (mTerminateProcessingThread) {
					ProcessQueuedMessages(); // some messages might have arrived between last processing and requesting shutdown...
					break;
				}
			}
		}

		/// <summary>
		/// Processes log messages that have been buffered in the <see cref="mAsyncProcessingMessageStack"/> (for asynchronous processing only).
		/// </summary>
		private void ProcessQueuedMessages()
		{
			LocalLogMessage[] messages = mAsyncProcessingMessageStack.FlushAndReverse();
			if (messages != null)
			{
				try
				{
					ProcessCore(messages);
				}
				catch (Exception ex)
				{
					Debug.Fail("ProcessCore() threw an unhandled exception.", ex.ToString());
				}

				// release message to let them return to the pool
				for (int i = 0; i < messages.Length; i++) {
					messages[i].Release();
				}
			}
		}

		#endregion

	}

}
