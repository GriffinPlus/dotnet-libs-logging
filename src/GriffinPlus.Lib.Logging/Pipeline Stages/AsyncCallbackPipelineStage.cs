///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// A processing pipeline stage that invokes a callback to process a log message (thread-safe).
/// This is a lightweight alternative to implementing an entire custom processing pipeline stage.
/// </summary>
public class AsyncCallbackPipelineStage : AsyncProcessingPipelineStage
{
	private SynchronousProcessingCallback  mSynchronousProcessingCallback;
	private AsynchronousProcessingCallback mAsynchronousProcessingCallback;

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncCallbackPipelineStage"/> class.
	/// Please set <see cref="SynchronousProcessingCallback"/> and <see cref="AsynchronousProcessingCallback"/> to callbacks of your choice.
	/// </summary>
	public AsyncCallbackPipelineStage() { }

	/// <summary>
	/// Gets or sets the synchronous message processing callback (may be <see langword="null"/>).
	/// The callback is executed in the context of the thread writing the message.
	/// </summary>
	/// <remarks>
	/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
	/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
	/// need the message anymore.
	/// </remarks>
	public SynchronousProcessingCallback SynchronousProcessingCallback
	{
		get => mSynchronousProcessingCallback;
		set
		{
			EnsureNotAttachedToLoggingSubsystem();
			mSynchronousProcessingCallback = value;
		}
	}

	/// <summary>
	/// Gets or sets the asynchronous message processing callback (may be <see langword="null"/>).
	/// The callback is executed by a worker thread.
	/// </summary>
	/// <remarks>
	/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
	/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
	/// need the message anymore.
	/// </remarks>
	public AsynchronousProcessingCallback AsynchronousProcessingCallback
	{
		get => mAsynchronousProcessingCallback;
		set
		{
			EnsureNotAttachedToLoggingSubsystem();
			mAsynchronousProcessingCallback = value;
		}
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
	/// need the message anymore.
	/// </remarks>
	protected override bool ProcessSync(LocalLogMessage message, out bool queueForAsyncProcessing)
	{
		return mSynchronousProcessingCallback != null
			       ? mSynchronousProcessingCallback(message, out queueForAsyncProcessing)
			       : base.ProcessSync(message, out queueForAsyncProcessing);
	}

	/// <summary>
	/// Processes the specified log messages asynchronously
	/// (the method is executed by the stage's processing thread, do not use <c>ConfigureAwait(false)</c> to resume
	/// execution in the processing thread when awaiting a task).
	/// </summary>
	/// <param name="messages">Messages to process.</param>
	/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
	/// <remarks>
	/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
	/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
	/// need the message anymore.
	/// </remarks>
	protected override Task ProcessAsync(LocalLogMessage[] messages, CancellationToken cancellationToken)
	{
		return mAsynchronousProcessingCallback != null
			       ? mAsynchronousProcessingCallback(messages, cancellationToken)
			       : base.ProcessAsync(messages, cancellationToken);
	}
}

/// <summary>
/// A delegate that processes the specified log message (synchronous processing).
/// </summary>
/// <param name="message">Log message to process.</param>
/// <param name="queueForAsyncProcessing">
/// Receives a value indicating whether the message should be enqueued for asynchronous processing.
/// </param>
/// <returns>
/// <see langword="true"/> to call the following pipeline stages;<br/>
/// <see langword="false"/> to stop processing.
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
/// need the message anymore.
/// </remarks>
public delegate Task AsynchronousProcessingCallback(LocalLogMessage[] messages, CancellationToken cancellationToken);
