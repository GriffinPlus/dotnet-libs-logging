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
	public class CallbackPipelineStage : ProcessingPipelineStage<CallbackPipelineStage>
	{
		/// <summary>
		/// A delegate that processes the specified log message.
		/// </summary>
		/// <param name="message">Log message to process.</param>
		/// <returns>
		/// true to call the following pipeline stages;
		/// false to stop processing.
		/// </returns>
		public delegate bool ProcessingCallback(LocalLogMessage message);

		/// <summary>
		/// The message processing callback.
		/// </summary>
		private readonly ProcessingCallback mProcessingCallback;

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncCallbackPipelineStage"/> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <param name="processCallback">
		/// Callback processing a log message traveling through the pipeline (may be null).
		/// The callback is executed in the context of the thread writing the message.
		/// </param>
		/// <remarks>
		/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		public CallbackPipelineStage(string name, ProcessingCallback processCallback) : base(name)
		{
			mProcessingCallback = processCallback;
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
			if (mProcessingCallback != null) return mProcessingCallback(message);
			return base.ProcessSync(message);
		}

	}
}
