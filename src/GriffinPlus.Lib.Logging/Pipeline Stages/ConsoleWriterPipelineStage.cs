﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018-2020 Sascha Falk <sascha@falk-online.eu>
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
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Output streams the <see cref="ConsoleWriterPipelineStage"/> can emit log messages to.
	/// </summary>
	public enum ConsoleOutputStream
	{
		/// <summary>
		/// The standard output stream.
		/// </summary>
		Stdout,

		/// <summary>
		/// The standard error stream.
		/// </summary>
		Stderr
	};

	/// <summary>
	/// A log message processing pipeline stage that writes log messages to stdout/stderr (thread-safe).
	/// By default all log messages are written to stdout.
	/// </summary>
	public class ConsoleWriterPipelineStage : TextWriterPipelineStage<ConsoleWriterPipelineStage>
	{
		private ConsoleOutputStream mDefaultStream = ConsoleOutputStream.Stdout;
		private readonly Dictionary<LogLevel, ConsoleOutputStream> mStreamByLevel = new Dictionary<LogLevel, ConsoleOutputStream>();

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleWriterPipelineStage"/> class.
		/// </summary>
		public ConsoleWriterPipelineStage()
		{

		}

		/// <summary>
		/// Gets or sets the default stream log messages are emitted to by default.
		/// </summary>
		public ConsoleOutputStream DefaultStream
		{
			get
			{
				lock (Sync) return mDefaultStream;
			}

			set
			{
				lock (Sync)
				{
					EnsureNotAttachedToLoggingSubsystem();
					mDefaultStream = value;
				}
			}
		}

		/// <summary>
		/// Configures the console writer to emit log messages written using the specified log level to the specified stream.
		/// Only necessary, if the stream is different from the default stream (<see cref="DefaultStream"/>).
		/// </summary>
		/// <param name="level">Log level of messages to emit to the specified stream.</param>
		/// <param name="stream">Output stream to emit log messages written using the specified log level to.</param>
		public void MapLogLevelToStream(LogLevel level, ConsoleOutputStream stream)
		{
			lock (Sync)
			{
				EnsureNotAttachedToLoggingSubsystem();
				mStreamByLevel[level] = stream;
			}
		}

		/// <summary>
		/// Emits the formatted log message.
		/// This method is called from within the pipeline stage lock (<see cref="AsyncProcessingPipelineStage{T}.Sync"/>).
		/// </summary>
		/// <param name="message">The current log message.</param>
		/// <param name="output">The formatted output of the current log message.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		protected override async Task EmitOutputAsync(LocalLogMessage message, string output, CancellationToken cancellationToken)
		{
			// NOTE: After attaching the pipeline stage to the logging subsystem, mStreamByLevel will not change.
			if (!mStreamByLevel.TryGetValue(message.LogLevel, out ConsoleOutputStream stream)) {
				stream = mDefaultStream;
			}

			if (stream == ConsoleOutputStream.Stdout) {
				await Console.Out.WriteAsync(output).ConfigureAwait(false);
			} else {
				await Console.Error.WriteAsync(output).ConfigureAwait(false);
			}
		}

	}
}
