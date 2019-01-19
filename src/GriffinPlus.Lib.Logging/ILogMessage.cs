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
	/// Interface log messages must implement.
	/// </summary>
	public interface ILogMessage
	{
		/// <summary>
		/// Gets the context of the log message
		/// (transports custom information as the log message travels through the processing pipeline)
		/// </summary>
		IDictionary<string,object> Context { get; }

		/// <summary>
		/// Time the message was written to the log.
		/// </summary>
		DateTimeOffset Timestamp { get; }

		/// <summary>
		/// Timestamp for relative time measurements with high accuracy
		/// (see <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>).
		/// </summary>
		long HighAccuracyTimestamp { get; }

		/// <summary>
		/// Name of the log level associated with the current log message.
		/// </summary>
		string LogLevelName { get; }

		/// <summary>
		/// Name of the log writer associated with the current log message.
		/// </summary>
		string LogWriterName { get; }

		/// <summary>
		/// The id of the process emitting the log message.
		/// </summary>
		int ProcessId { get; }

		/// <summary>
		/// The name of the process emitting the log message.
		/// </summary>
		string ProcessName { get; }

		/// <summary>
		/// The name of the application emitting the log message
		/// (can differ from the process name, if the application is using an interpreter (the actual process)).
		/// </summary>
		string ApplicationName { get; }

		/// <summary>
		/// The actual text the log message is about.
		/// </summary>
		string Text { get; }

	}
}
