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

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message.
	/// </summary>
	public class LogMessage
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessage"/> class.
		/// </summary>
		public LogMessage()
		{
			Context = new Dictionary<string, object>();
		}

		/// <summary>
		/// Resets the log message to defaults (for internal use only).
		/// </summary>
		internal void Reset()
		{
			Context.Clear();
			ProcessId = 0;
			ProcessName = null;
			ApplicationName = null;
			LogWriter = null;
			LogWriterName = null;
			LogLevel = null;
			LogLevelName = null;
			Text = null;
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
			Timestamp = timestamp;
			HighAccuracyTimestamp = highAccuracyTimestamp;
			ProcessId = processId;
			ProcessName = processName;
			ApplicationName = applicationName;
			LogWriter = logWriter;
			LogWriterName = logWriter.Name;
			LogLevel = logLevel;
			LogLevelName = logLevel.Name;
			Text = text;
		}

		/// <summary>
		/// Gets the context of the log message
		/// (transports custom information as the log message travels through the processing pipeline)
		/// </summary>
		public IDictionary<string,object> Context { get; private set; }

		/// <summary>
		/// Time the message was written to the log.
		/// </summary>
		public DateTimeOffset Timestamp { get; private set; }

		/// <summary>
		/// Timestamp for relative time measurements with high accuracy
		/// (see <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>).
		/// </summary>
		public long HighAccuracyTimestamp { get; private set; }

		/// <summary>
		/// Log level associated with the current log message
		/// (only set for messages that were written by the current process).
		/// </summary>
		public LogLevel LogLevel { get; private set; }

		/// <summary>
		/// Name of the log level associated with the current log message.
		/// </summary>
		public string LogLevelName { get; private set; }

		/// <summary>
		/// The log writer that was used to emit the log message
		/// (only set for messages that were written by the current process).
		/// </summary>
		public LogWriter LogWriter { get; private set; }

		/// <summary>
		/// Name of the log writer associated with the current log message.
		/// </summary>
		public string LogWriterName { get; private set; }

		/// <summary>
		/// The id of the process emitting the log message.
		/// </summary>
		public int ProcessId { get; private set; }

		/// <summary>
		/// The name of the process emitting the log message.
		/// </summary>
		public string ProcessName { get; private set; }

		/// <summary>
		/// The name of the application emitting the log message
		/// (can differ from the process name, if the application is using an interpreter (the actual process)).
		/// </summary>
		public string ApplicationName { get; private set; }

		/// <summary>
		/// The actual text the log message is about.
		/// </summary>
		public string Text { get; private set; }

	}
}
