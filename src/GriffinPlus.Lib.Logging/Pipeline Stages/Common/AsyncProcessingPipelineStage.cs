///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using GriffinPlus.Lib.Threading;

// ReSharper disable ForCanBeConvertedToForeach

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Base class for stages in the log message processing pipeline that feature asynchronous processing.
/// Messages can be processed synchronously or asynchronously. Heavyweight operations and all operations
/// that might block should be done asynchronously only to ensure that the thread writing a message is
/// not blocked.
/// </summary>
public abstract class AsyncProcessingPipelineStage : ProcessingPipelineStage
{
	private          AsyncContextThread             mAsyncContextThread;
	private          Task                           mAsyncProcessingTask;
	private          AsyncAutoResetEvent            mTriggerAsyncProcessingEvent;
	private          LocklessStack<LocalLogMessage> mAsyncProcessingMessageStack;
	private          bool                           mDiscardMessagesIfQueueFull;
	private          int                            mMessageQueueSize = 500;
	private          TimeSpan                       mShutdownTimeout  = TimeSpan.FromMilliseconds(5000);
	private          CancellationTokenSource        mAsyncProcessingCancellationTokenSource;
	private volatile bool                           mTerminateProcessingTask;

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncProcessingPipelineStage"/> class.
	/// </summary>
	protected AsyncProcessingPipelineStage() { }

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
			mAsyncContextThread = new AsyncContextThread();
			mAsyncProcessingTask = mAsyncContextThread.Factory.Run(ProcessingTask);

			// Perform pipeline stage specific initialization
			OnInitialize();
		}
		catch (Exception)
		{
			Shutdown();
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
		mAsyncContextThread?.Join();
		mAsyncProcessingCancellationTokenSource?.Dispose();
		mAsyncContextThread = null;
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

	#region Processing Setting Changes

	private readonly HashSet<IUntypedSettingProxy> mChangedSettings = new();

	/// <summary>
	/// Notifies that the specified setting has changed (for internal use only).
	/// </summary>
	/// <param name="setting">The setting that has changed.</param>
	internal override void ProcessSettingChanged(IUntypedSettingProxy setting)
	{
		lock (mChangedSettings)
		{
			mChangedSettings.Add(setting);
		}
	}

	/// <summary>
	/// When overridden in a derived class, processes pending changes to registered setting proxies
	/// (the method is executed by the stage's processing thread, do not use <c>ConfigureAwait(false)</c> to resume
	/// execution in the processing thread when awaiting a task).
	/// </summary>
	/// <param name="settings">Settings that have changed.</param>
	/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
	protected virtual Task OnSettingsChangedAsync(IUntypedProcessingPipelineStageSetting[] settings, CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	#endregion

	#region Processing Messages and Notifications

	/// <summary>
	/// Is called on behalf of <see cref="ProcessingPipelineStage.ProcessMessage"/> (for internal use only).
	/// This method must not throw exceptions.
	/// </summary>
	/// <param name="message">Message to process.</param>
	/// <returns>
	/// <c>true</c> to pass the message to the following stages;<br/>
	/// <c>false</c> to stop processing the message.
	/// </returns>
	internal override bool OnProcessMessageBase(LocalLogMessage message)
	{
		try
		{
			// synchronous processing
			bool proceed = ProcessSync(message, out bool queueMessageForAsynchronousProcessing);

			// asynchronous processing
			if (queueMessageForAsynchronousProcessing)
			{
				if (mDiscardMessagesIfQueueFull)
				{
					message.AddRef();
					bool pushed = mAsyncProcessingMessageStack.TryPush(message);
					if (pushed) mTriggerAsyncProcessingEvent.Set();
					else message.Release();
				}
				else
				{
					message.AddRef();
					while (!mAsyncProcessingMessageStack.TryPush(message)) Thread.Sleep(10);
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
	/// <c>true</c> to pass the message to the following pipeline stages;<br/>
	/// otherwise <c>false</c>.
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
	/// (the method is executed by the stage's processing thread, do not use <c>ConfigureAwait(false)</c> to resume
	/// execution in the processing thread when awaiting a task).
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
		CancellationToken cancellationToken = mAsyncProcessingCancellationTokenSource.Token;

		while (true)
		{
			// wait for messages to process
			await mTriggerAsyncProcessingEvent.WaitAsync(cancellationToken);

			// pull setting changes, if available
			IUntypedProcessingPipelineStageSetting[] settings = null;
			lock (mChangedSettings)
			{
				if (mChangedSettings.Count > 0)
				{
					settings = mChangedSettings.Cast<IUntypedProcessingPipelineStageSetting>().ToArray();
					mChangedSettings.Clear();
				}
			}

			// process setting changes
			if (settings != null)
				await OnSettingsChangedAsync(settings, cancellationToken);

			// process the messages
			await ProcessQueuedMessages(cancellationToken);

			// abort, if requested
			if (mTerminateProcessingTask)
			{
				// process the last messages, if there is time left...
				if (!cancellationToken.IsCancellationRequested)
				{
					await ProcessQueuedMessages(cancellationToken);
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
			await ProcessAsync(messages, cancellationToken);
		}
		catch (Exception ex)
		{
			Debug.Fail("ProcessAsync() threw an unhandled exception.", ex.ToString());
		}
		finally
		{
			// release message to let them return to the pool
			foreach (LocalLogMessage message in messages) message.Release();
		}
	}

	#endregion
}
