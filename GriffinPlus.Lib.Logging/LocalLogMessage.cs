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

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message that was written by the current process (it therefore contains additional information
	/// about the <see cref="LogWriter"/> object and the <see cref="LogLevel"/> object involved).
	/// </summary>
	public class LocalLogMessage : LogMessage
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LocalLogMessage"/> class.
		/// </summary>
		public LocalLogMessage()
		{

		}

		/// <summary>
		/// Resets the log message to defaults (for internal use only).
		/// </summary>
		internal protected override void Reset()
		{
			base.Reset();

			LogWriter = null;
			LogLevel = null;
		}

		/// <summary>
		/// Initializes the log message.
		/// </summary>
		/// <param name="timestamp">Time the message was written to the log.</param>
		/// <param name="highAccuracyTimestamp">
		/// Timestamp for relative time measurements with high accuracy
		/// (the actual precision depends on the <see cref="System.Diagnostics.Stopwatch"/> class).
		/// </param>
		/// <param name="processId">Id of the process emitting the log message.</param>
		/// <param name="processName">Name of the process emitting the log message.</param>
		/// <param name="applicationName">
		/// Name of the application emitting the log message
		/// (can differ from the process name, if the application is using an interpreter (the actual process)).
		/// </param>
		/// <param name="logWriter">Log writer that was used to emit the message.</param>
		/// <param name="logLevel">Log level that is associated with the message.</param>
		/// <param name="text">The actual text the log message is about.</param>
		internal void Init(
			DateTimeOffset timestamp,
			long highAccuracyTimestamp,
			int processId,
			string processName,
			string applicationName,
			LogWriter logWriter,
			LogLevel logLevel,
			string text)
		{
			base.Init(
				timestamp,
				highAccuracyTimestamp,
				processId,
				processName,
				applicationName,
				logWriter.Name,
				logLevel.Name,
				text);

			LogWriter = logWriter;
			LogLevel = logLevel;
		}

		/// <summary>
		/// The log writer that was used to emit the log message.
		/// </summary>
		public LogWriter LogWriter { get; private set; }

		/// <summary>
		/// Log level associated with the current log message.
		/// </summary>
		public LogLevel LogLevel { get; private set; }

	}
}
