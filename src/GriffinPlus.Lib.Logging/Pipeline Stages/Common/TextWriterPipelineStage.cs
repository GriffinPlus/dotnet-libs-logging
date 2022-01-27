///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Base class for a log message processing pipeline stage that logs messages as a formatted string (thread-safe).
	/// </summary>
	public abstract class TextWriterPipelineStage : AsyncProcessingPipelineStage
	{
		private readonly Queue<FormattedMessage> mFormattedMessageQueue = new Queue<FormattedMessage>();

		/// <summary>
		/// A message and its formatted output.
		/// </summary>
		protected struct FormattedMessage
		{
			/// <summary>
			/// The message.
			/// </summary>
			public LocalLogMessage Message;

			/// <summary>
			/// The formatted message.
			/// </summary>
			public string Output;
		}

		private ILogMessageFormatter mFormatter = TableMessageFormatter.AllColumns;

		/// <summary>
		/// Initializes a new instance of the <see cref="TextWriterPipelineStage"/> class.
		/// </summary>
		protected TextWriterPipelineStage()
		{
		}

		/// <summary>
		/// Gets or sets the formatter the pipeline stage uses to format log messages.
		/// </summary>
		public ILogMessageFormatter Formatter
		{
			get
			{
				lock (Sync) return mFormatter;
			}

			set
			{
				if (value == null) throw new ArgumentNullException(nameof(value));

				lock (Sync)
				{
					EnsureNotAttachedToLoggingSubsystem();
					mFormatter = value;
				}
			}
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
		/// need the message any more.
		/// </remarks>
		protected override async Task ProcessAsync(LocalLogMessage[] messages, CancellationToken cancellationToken)
		{
			// enqueue messages to process
			// (helps to defer messages that could not be processed successfully)
			// ReSharper disable once ForCanBeConvertedToForeach
			for (int i = 0; i < messages.Length; i++)
			{
				var message = messages[i];
				message.AddRef();

				var formattedMessage = new FormattedMessage
				{
					Message = messages[i],

					// ReSharper disable once InconsistentlySynchronizedField
					Output = mFormatter.Format(messages[i])
				};

				mFormattedMessageQueue.Enqueue(formattedMessage);
			}

			if (mFormattedMessageQueue.Count > 0)
			{
				int count = await EmitOutputAsync(mFormattedMessageQueue.ToArray(), cancellationToken);
				for (int i = 0; i < count; i++)
				{
					var item = mFormattedMessageQueue.Dequeue();
					item.Message.Release();
				}
			}
		}

		/// <summary>
		/// Emits the formatted log messages (should not throw any exceptions).
		/// </summary>
		/// <param name="messages">The formatted log messages.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		/// <returns>Number of successfully written log messages.</returns>
		protected abstract Task<int> EmitOutputAsync(FormattedMessage[] messages, CancellationToken cancellationToken);
	}

}
