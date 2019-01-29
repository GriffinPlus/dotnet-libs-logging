///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://griffin.plus)
//
// Copyright 2018 Sascha Falk <sascha@falk-online.eu>
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
	/// A log message processing pipeline stage that logs messages to stdout/stderr (thread-safe).
	/// By default all log messages are written to stdout.
	/// </summary>
	public class ConsoleWriterPipelineStage : TextWriterPipelineStage<ConsoleWriterPipelineStage>
	{
		private ConsoleOutputStream mDefaultStream = ConsoleOutputStream.Stdout;
		private Dictionary<LogLevel, ConsoleOutputStream> mStreamByLevel = new Dictionary<LogLevel, ConsoleOutputStream>();

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleWriterPipelineStage"/> class.
		/// </summary>
		public ConsoleWriterPipelineStage()
		{

		}

		/// <summary>
		/// Emits the formatted log message.
		/// This method is called from within a locked block.
		/// </summary>
		/// <param name="message">The current log message.</param>
		/// <param name="output">The formatted output of the current log message.</param>
		protected override void EmitOutput(LocalLogMessage message, StringBuilder output)
		{
			if (!mStreamByLevel.TryGetValue(message.LogLevel, out ConsoleOutputStream stream)) {
				stream = mDefaultStream;
			}

			if (stream == ConsoleOutputStream.Stdout) {
				Console.Error.Write(output);
			} else {
				Console.Out.Write(output);
			}
		}

		/// <summary>
		/// Configures the console writer to emit log messages to the specified stream by default.
		/// Messages are emitted to stdout by default.
		/// </summary>
		/// <returns>The modified pipeline stage.</returns>
		public ConsoleWriterPipelineStage UseDefaultStream(ConsoleOutputStream stream)
		{
			lock (mSync)
			{
				mDefaultStream = stream;
			}

			return this;
		}

		/// <summary>
		/// Configures the console writer to emit log messages written using the specified log level to stdout.
		/// </summary>
		/// <param name="level">Log level of messages to emit to stdout.</param>
		/// <returns>The modified pipeline stage.</returns>
		public ConsoleWriterPipelineStage WithLogLevelToStdout(LogLevel level)
		{
			lock (mSync)
			{
				mStreamByLevel[level] = ConsoleOutputStream.Stdout;
			}

			return this;
		}

		/// <summary>
		/// Configures the console writer to emit log messages written using the specified log level to stderr.
		/// </summary>
		/// <param name="level">Log level of messages to emit to stderr.</param>
		/// <returns>The modified pipeline stage.</returns>
		public ConsoleWriterPipelineStage WithLogLevelToStderr(LogLevel level)
		{
			lock (mSync)
			{
				mStreamByLevel[level] = ConsoleOutputStream.Stderr;
			}

			return this;
		}

		/// <summary>
		/// Configures the console writer to emit log messages written using the specified log level to the specified stream.
		/// </summary>
		/// <param name="level">Log level of messages to emit to the specified stream.</param>
		/// <param name="stream">Output stream to emit log messages written using the specified log level to.</param>
		/// <returns>The modified pipeline stage.</returns>
		public ConsoleWriterPipelineStage WithLogLevelToStream(LogLevel level, ConsoleOutputStream stream)
		{
			lock (mSync)
			{
				mStreamByLevel[level] = stream;
			}

			return this;
		}

	}
}
