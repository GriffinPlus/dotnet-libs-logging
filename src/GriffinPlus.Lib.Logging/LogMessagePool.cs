///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Concurrent;
using System.Diagnostics;

// ReSharper disable UnusedMember.Global

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A pool of log messages allowing log messages to be re-used to reduce garbage collection pressure (thread-safe).
	/// </summary>
	public class LogMessagePool
	{
		private readonly ConcurrentBag<LogMessage> mMessages;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessagePool"/> class.
		/// </summary>
		public LogMessagePool()
		{
			mMessages = new ConcurrentBag<LogMessage>();
		}

		/// <summary>
		/// Gets the default log message pool.
		/// </summary>
		public static LogMessagePool Default { get; } = new LogMessagePool();

		/// <summary>
		/// Gets a log message from the pool, creates a new one, if the pool is empty.
		/// </summary>
		/// <param name="timestamp">Time the message was written to the log.</param>
		/// <param name="highPrecisionTimestamp">
		/// Timestamp for relative time measurements with high precision
		/// (the actual precision depends on the <see cref="System.Diagnostics.Stopwatch"/> class).
		/// </param>
		/// <param name="logWriterName">Name of the log writer that was used to emit the message.</param>
		/// <param name="logLevelName">Name of the log level that is associated with the message.</param>
		/// <param name="tags">Tags that are associated with the message.</param>
		/// <param name="applicationName">
		/// Name of the application emitting the log message
		/// (can differ from the process name, if the application is using an interpreter (the actual process)).
		/// </param>
		/// <param name="processName">Name of the process emitting the log message.</param>
		/// <param name="processId">Id of the process emitting the log message.</param>
		/// <param name="text">The actual text the log message is about.</param>
		/// <returns>The requested log message.</returns>
		public LogMessage GetMessage(
			DateTimeOffset timestamp,
			long highPrecisionTimestamp,
			string logWriterName,
			string logLevelName,
			TagSet tags,
			string applicationName,
			string processName,
			int processId,
			string text)
		{
			if (mMessages.TryTake(out var message))
			{
				// ReSharper disable once RedundantAssignment
				int refCount = message.AddRef();
				Debug.Assert(refCount == 1);
			}
			else
			{
				message = new LogMessage(this);
			}

			message.Init(timestamp, highPrecisionTimestamp, logWriterName, logLevelName, tags, applicationName, processName, processId, text);
			return message;
		}

		/// <summary>
		/// Returns a log message to the pool, so it can be re-used.
		/// This message is called by the messages, if their reference counter gets 0.
		/// </summary>
		/// <param name="message">Message to return to the pool.</param>
		internal void ReturnMessage(LogMessage message)
		{
			message.Reset();
			mMessages.Add(message);
		}

	}

}
