///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
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
using System.Collections.Concurrent;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A pool of log messages allowing log messages to be re-used to reduce garbage collection pressure (thread-safe).
	/// </summary>
	internal class LogMessagePool
	{
		private ConcurrentBag<LocalLogMessage> mMessages;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessagePool"/> class.
		/// </summary>
		public LogMessagePool()
		{
			mMessages = new ConcurrentBag<LocalLogMessage>();
		}

		/// <summary>
		/// Gets a log message from the pool, creates a new one, if the pool is empty.
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
		/// <returns>The requested log message.</returns>
		public LocalLogMessage GetMessage(
			DateTimeOffset timestamp,
			long highAccuracyTimestamp,
			int processId,
			string processName,
			string applicationName,
			LogWriter logWriter,
			LogLevel logLevel,
			string text)
		{
			LocalLogMessage message;
			if (!mMessages.TryTake(out message)) message = new LocalLogMessage();
			message.Init(timestamp, highAccuracyTimestamp, processId, processName, applicationName, logWriter, logLevel, text);
			return message;
		}

		/// <summary>
		/// Returns a log message to the pool, so it can be re-used.
		/// </summary>
		/// <param name="message">Message to return to the pool.</param>
		public void ReturnMessage(LocalLogMessage message)
		{
			message.Reset();
			mMessages.Add(message);
		}
	}

}
