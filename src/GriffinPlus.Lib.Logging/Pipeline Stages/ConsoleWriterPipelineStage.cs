///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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
using System.Text;
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
		private readonly StringBuilder mStdoutBuilder = new StringBuilder();
		private readonly StringBuilder mStderrBuilder = new StringBuilder();

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
		/// Emits the formatted log messages.
		/// </summary>
		/// <param name="messages">The formatted log messages.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		protected override async Task EmitOutputAsync(FormattedMessage[] messages, CancellationToken cancellationToken)
		{
			mStdoutBuilder.Clear();
			mStderrBuilder.Clear();

			for (int i = 0; i < messages.Length; i++)
			{
				var message = messages[i];

				// NOTE: After attaching the pipeline stage to the logging subsystem, mStreamByLevel will not change.
				if (!mStreamByLevel.TryGetValue(message.Message.LogLevel, out ConsoleOutputStream stream))
				{
					stream = mDefaultStream;
				}

				if (stream == ConsoleOutputStream.Stdout)
				{
					mStdoutBuilder.Append(message.Output);
					mStdoutBuilder.AppendLine();
				}
				else
				{
					mStderrBuilder.Append(message.Output);
					mStderrBuilder.AppendLine();
				}
			}

			if (mStdoutBuilder.Length > 0) await Console.Out.WriteAsync(mStdoutBuilder.ToString()).ConfigureAwait(false);
			if (mStderrBuilder.Length > 0) await Console.Error.WriteAsync(mStderrBuilder.ToString()).ConfigureAwait(false);
		}

	}
}
