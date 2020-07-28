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
using System.Threading;

// ReSharper disable UnusedMember.Global

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message that was written by the current process (it therefore contains additional information
	/// about the <see cref="LogWriter"/> object and the <see cref="LogLevel"/> object involved).
	/// </summary>
	public sealed class LocalLogMessage : ILogMessage
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LocalLogMessage"/> class.
		/// </summary>
		public LocalLogMessage()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LocalLogMessage"/> class.
		/// </summary>
		/// <param name="pool">The pool the message belongs to.</param>
		internal LocalLogMessage(LocalLogMessagePool pool)
		{
			Pool = pool;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LocalLogMessage"/> class copying the specified one.
		/// </summary>
		/// <param name="other">Message to copy.</param>
		public LocalLogMessage(LocalLogMessage other)
		{
			Context = new Dictionary<string, object>(other.Context);
			Timestamp = other.Timestamp;
			HighPrecisionTimestamp = other.HighPrecisionTimestamp;
			ProcessId = other.ProcessId;
			ProcessName = other.ProcessName;
			ApplicationName = other.ApplicationName;
			LogWriter = other.LogWriter;
			LogLevel = other.LogLevel;
			Text = other.Text;
		}

		#region Message Properties

		/// <summary>
		/// Gets the context of the log message
		/// (transports custom information as the log message travels through the processing pipeline)
		/// </summary>
		public IDictionary<string,object> Context { get; } = new Dictionary<string, object>();

		/// <summary>
		/// Gets the date/time the message was written to the log.
		/// </summary>
		public DateTimeOffset Timestamp { get; private set; }

		/// <summary>
		/// Gets the timestamp for relative time measurements with high precision
		/// (in nanoseconds, but the actual precision depends on the system timer).
		/// </summary>
		public long HighPrecisionTimestamp { get; private set; }

		/// <summary>
		/// Gets the log level associated with the log message.
		/// </summary>
		public LogLevel LogLevel { get; private set; }

		/// <summary>
		/// Gets the name of the log level associated with the log message.
		/// </summary>
		public string LogLevelName => LogLevel?.Name;

		/// <summary>
		/// Gets the log writer associated with the log message.
		/// </summary>
		public LogWriter LogWriter { get; private set; }

		/// <summary>
		/// Gets the name of the log writer associated with the log message.
		/// </summary>
		public string LogWriterName => LogWriter?.Name;

		/// <summary>
		/// Gets the id of the process emitting the log message.
		/// </summary>
		public int ProcessId { get; private set; }

		/// <summary>
		/// Gets the name of the process emitting the log message.
		/// </summary>
		public string ProcessName { get; private set; }

		/// <summary>
		/// Gets the name of the application emitting the log message
		/// (can differ from the process name, if the application is using an interpreter (the actual process)).
		/// </summary>
		public string ApplicationName { get; private set; }

		/// <summary>
		/// Gets the actual text the log message is about.
		/// </summary>
		public string Text { get; private set; }

		#endregion

		#region Pooling Support

		private int mRefCount = 1;

		/// <summary>
		/// Increments the reference counter (needed for pool messages only).
		/// Call it to indicate that the message is still in use and avoid that it returns to the pool.
		/// </summary>
		/// <returns>The reference counter after incrementing.</returns>
		public int AddRef()
		{
			return Interlocked.Increment(ref mRefCount);
		}

		/// <summary>
		/// Decrements the reference counter (needed for pool messages only).
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
		internal LocalLogMessagePool Pool { get; }

		/// <summary>
		/// Initializes the log message.
		/// </summary>
		/// <param name="timestamp">Time the message was written to the log.</param>
		/// <param name="highPrecisionTimestamp">
		/// Timestamp for relative time measurements with high precision
		/// (in ns, the actual precision depends on the <see cref="System.Diagnostics.Stopwatch"/> class).
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
			long highPrecisionTimestamp,
			int processId,
			string processName,
			string applicationName,
			LogWriter logWriter,
			LogLevel logLevel,
			string text)
		{
			Timestamp = timestamp;
			HighPrecisionTimestamp = highPrecisionTimestamp;
			ProcessId = processId;
			ProcessName = processName;
			ApplicationName = applicationName;
			LogWriter = logWriter;
			LogLevel = logLevel;
			Text = text;
		}

		/// <summary>
		/// Resets the log message to defaults.
		/// </summary>
		internal void Reset()
		{
			Context.Clear();
			ProcessId = 0;
			ProcessName = null;
			ApplicationName = null;
			LogWriter = null;
			LogLevel = null;
			Text = null;
		}

		#endregion

	}
}
