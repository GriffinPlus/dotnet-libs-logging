///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2019 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Fluent API extension methods for the <see cref="ConsoleWriterPipelineStage"/> class.
	/// </summary>
	public static class ConsoleWriterPipelineStageExtensions
	{
		/// <summary>
		/// Configures the console writer to emit log messages to the specified stream by default.
		/// Messages are emitted to stdout by default.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <param name="stream">Console stream to use by default.</param>
		/// <returns>The modified pipeline stage.</returns>
		public static ConsoleWriterPipelineStage UseDefaultStream(this ConsoleWriterPipelineStage @this, ConsoleOutputStream stream)
		{
			@this.DefaultStream = stream;
			return @this;
		}

		/// <summary>
		/// Configures the console writer to emit log messages written using the specified log level to stdout.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <param name="level">Log level of messages to emit to stdout.</param>
		/// <returns>The modified pipeline stage.</returns>
		public static ConsoleWriterPipelineStage WithLogLevelToStdout(this ConsoleWriterPipelineStage @this, LogLevel level)
		{
			@this.MapLogLevelToStream(level, ConsoleOutputStream.Stdout);
			return @this;
		}

		/// <summary>
		/// Configures the console writer to emit log messages written using the specified log level to stderr.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <param name="level">Log level of messages to emit to stderr.</param>
		/// <returns>The modified pipeline stage.</returns>
		public static ConsoleWriterPipelineStage WithLogLevelToStderr(this ConsoleWriterPipelineStage @this, LogLevel level)
		{
			@this.MapLogLevelToStream(level, ConsoleOutputStream.Stderr);
			return @this;
		}

		/// <summary>
		/// Configures the console writer to emit log messages written using the specified log level to the specified stream.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <param name="level">Log level of messages to emit to the specified stream.</param>
		/// <param name="stream">Output stream to emit log messages written using the specified log level to.</param>
		/// <returns>The modified pipeline stage.</returns>
		public static ConsoleWriterPipelineStage WithLogLevelToStream(this ConsoleWriterPipelineStage @this, LogLevel level, ConsoleOutputStream stream)
		{
			@this.MapLogLevelToStream(level, stream);
			return @this;
		}

	}
}
