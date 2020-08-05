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
using System.Threading;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message for general purpose use.
	/// </summary>
	public sealed class LogMessage : ILogMessage
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessage"/> class.
		/// </summary>
		public LogMessage()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessage"/> class.
		/// </summary>
		/// <param name="pool">The pool the message belongs to.</param>
		internal LogMessage(LogMessagePool pool)
		{
			Pool = pool;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessage"/> class copying the specified one.
		/// </summary>
		/// <param name="other">Message to copy.</param>
		public LogMessage(ILogMessage other)
		{
			Timestamp = other.Timestamp;
			HighPrecisionTimestamp = other.HighPrecisionTimestamp;
			ProcessId = other.ProcessId;
			ProcessName = other.ProcessName;
			ApplicationName = other.ApplicationName;
			LogWriterName = other.LogWriterName;
			LogLevelName = other.LogLevelName;
			Text = other.Text;
		}

		#region Message Properties

		/// <summary>
		/// Gets the date/time the message was written to the log.
		/// </summary>
		public DateTimeOffset Timestamp { get; set; }

		/// <summary>
		/// Gets the timestamp for relative time measurements with high precision
		/// (in nanoseconds, but the actual precision depends on the system timer).
		/// </summary>
		public long HighPrecisionTimestamp { get; set; }

		/// <summary>
		/// Gets the name of the log level associated with the log message.
		/// </summary>
		public string LogLevelName { get; set; }

		/// <summary>
		/// Gets the name of the log writer associated with the log message.
		/// </summary>
		public string LogWriterName { get; set; }

		/// <summary>
		/// Gets the id of the process emitting the log message.
		/// </summary>
		public int ProcessId { get; set; }

		/// <summary>
		/// Gets the name of the process emitting the log message.
		/// </summary>
		public string ProcessName { get; set; }

		/// <summary>
		/// Gets the name of the application emitting the log message
		/// (can differ from the process name, if the application is using an interpreter (the actual process)).
		/// </summary>
		public string ApplicationName { get; set; }

		/// <summary>
		/// Gets the actual text the log message is about.
		/// </summary>
		public string Text { get; set; }

		#endregion

		#region Pooling Support

		private int mRefCount = 1;

		/// <summary>
		/// Increments the reference counter of the log message (needed for pool messages only).
		/// Call it to indicate that the message is still in use and avoid that it returns to the pool.
		/// </summary>
		/// <returns>The reference counter after incrementing.</returns>
		public int AddRef()
		{
			return Interlocked.Increment(ref mRefCount);
		}

		/// <summary>
		/// Decrements the reference counter of the log message (needed for pool messages only).
		/// The message returns to the pool, if the counter gets 0.
		/// </summary>
		/// <returns>The reference counter after decrementing.</returns>
		public int Release()
		{
			int refCount = Interlocked.Decrement(ref mRefCount);

			if (refCount < 0) {
				Interlocked.Increment(ref mRefCount);
				throw new InvalidOperationException("The reference count is already 0.");
			}

			if (refCount == 0) {
				Pool?.ReturnMessage(this);
			}

			return refCount;
		}

		/// <summary>
		/// Gets the current value of the reference counter of the log message.
		/// </summary>
		public int RefCount => Volatile.Read(ref mRefCount);

		/// <summary>
		/// Gets the pool the log message belongs to.
		/// </summary>
		internal LogMessagePool Pool { get; }

		/// <summary>
		/// Initializes the log message.
		/// </summary>
		/// <param name="timestamp">Time the message was written to the log.</param>
		/// <param name="highPrecisionTimestamp">
		/// Timestamp for relative time measurements with high precision
		/// (the actual precision depends on the <see cref="System.Diagnostics.Stopwatch"/> class).
		/// </param>
		/// <param name="processId">Id of the process emitting the log message.</param>
		/// <param name="processName">Name of the process emitting the log message.</param>
		/// <param name="applicationName">
		/// Name of the application emitting the log message
		/// (can differ from the process name, if the application is using an interpreter (the actual process)).
		/// </param>
		/// <param name="logWriterName">Name of the log writer that was used to emit the message.</param>
		/// <param name="logLevelName">Name of the log level that is associated with the message.</param>
		/// <param name="text">The actual text the log message is about.</param>
		public void Init(
			DateTimeOffset timestamp,
			long highPrecisionTimestamp,
			int processId,
			string processName,
			string applicationName,
			string logWriterName,
			string logLevelName,
			string text)
		{
			Timestamp = timestamp;
			HighPrecisionTimestamp = highPrecisionTimestamp;
			ProcessId = processId;
			ProcessName = processName;
			ApplicationName = applicationName;
			LogWriterName = logWriterName;
			LogLevelName = logLevelName;
			Text = text;
		}

		/// <summary>
		/// Resets the log message to defaults.
		/// </summary>
		internal void Reset()
		{
			ProcessId = 0;
			ProcessName = null;
			ApplicationName = null;
			LogWriterName = null;
			LogLevelName = null;
			Text = null;
		}

		#endregion
	}
}
