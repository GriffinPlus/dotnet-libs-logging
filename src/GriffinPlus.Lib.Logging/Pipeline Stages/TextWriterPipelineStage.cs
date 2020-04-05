﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Base class for a log message processing pipeline stage that logs messages as a formatted string (thread-safe).
	/// </summary>
	public abstract class TextWriterPipelineStage<STAGE> : AsyncProcessingPipelineStage<STAGE>
		where STAGE: TextWriterPipelineStage<STAGE>
	{
		private ILogMessageFormatter mFormatter = new TableMessageFormatter();

		/// <summary>
		/// Initializes a new instance of the <see cref="TextWriterPipelineStage{STAGE}"/> class.
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
				lock(Sync) return mFormatter;
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
		/// Processes the specified log messages asynchronously.
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
			for (int i = 0; i < messages.Length; i++)
			{
				// ReSharper disable once InconsistentlySynchronizedField
				// (after attaching the pipeline stage to the logging subsystem, mFormatter will not change)
				string output = mFormatter.Format(messages[i]);
				await EmitOutputAsync(messages[i], output, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Emits the formatted log message.
		/// This method is called from within the pipeline stage lock (<see cref="AsyncProcessingPipelineStage{T}.Sync"/>).
		/// </summary>
		/// <param name="message">The current log message.</param>
		/// <param name="output">The formatted output of the current log message.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		protected abstract Task EmitOutputAsync(LocalLogMessage message, string output, CancellationToken cancellationToken);

	}
}
