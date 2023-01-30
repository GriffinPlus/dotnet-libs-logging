///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A processing pipeline stage that invokes a callback to process a log message (thread-safe).
	/// This is a lightweight alternative to implementing an entire custom processing pipeline stage.
	/// </summary>
	public class CallbackPipelineStage : SyncProcessingPipelineStage
	{
		private ProcessingCallback mProcessingCallback;

		/// <summary>
		/// Initializes a new instance of the <see cref="CallbackPipelineStage"/> class.
		/// Please set <see cref="ProcessingCallback"/> to a callback of your choice.
		/// </summary>
		public CallbackPipelineStage() { }

		/// <summary>
		/// Gets or sets the message processing callback (may be <c>null</c>).
		/// The callback is executed in the context of the thread writing the message.
		/// </summary>
		/// <remarks>
		/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		public ProcessingCallback ProcessingCallback
		{
			get => mProcessingCallback;
			set
			{
				EnsureNotAttachedToLoggingSubsystem();
				mProcessingCallback = value;
			}
		}

		/// <summary>
		/// Processes the specified log message synchronously (is executed in the context of the thread writing the message).
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <remarks>
		/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		protected override bool ProcessSync(LocalLogMessage message)
		{
			return mProcessingCallback?.Invoke(message) ?? base.ProcessSync(message);
		}
	}

	/// <summary>
	/// A delegate that processes the specified log message.
	/// </summary>
	/// <param name="message">Log message to process.</param>
	/// <returns>
	/// <c>true</c> to call the following pipeline stages;<br/>
	/// <c>false</c> to stop processing.
	/// </returns>
	public delegate bool ProcessingCallback(LocalLogMessage message);

}
