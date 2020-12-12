﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading;

// ReSharper disable UnusedMember.Global
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A log message that was written by the current process (it therefore contains additional information
	/// about the <see cref="LogWriter" /> object and the <see cref="LogLevel" /> object involved).
	/// </summary>
	public sealed class LocalLogMessage : ILogMessage, IEquatable<ILogMessage>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LocalLogMessage" /> class.
		/// </summary>
		public LocalLogMessage()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LocalLogMessage" /> class.
		/// </summary>
		/// <param name="pool">The pool the message belongs to.</param>
		internal LocalLogMessage(LocalLogMessagePool pool)
		{
			Pool = pool;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LocalLogMessage" /> class copying the specified one.
		/// </summary>
		/// <param name="other">Message to copy.</param>
		public LocalLogMessage(LocalLogMessage other)
		{
			Context = new Dictionary<string, object>(other.Context);
			Timestamp = other.Timestamp;
			HighPrecisionTimestamp = other.HighPrecisionTimestamp;
			LogWriter = other.LogWriter;
			LogLevel = other.LogLevel;
			Tags = other.Tags;
			ApplicationName = other.ApplicationName;
			ProcessName = other.ProcessName;
			ProcessId = other.ProcessId;
			Text = other.Text;
		}

		#region Message Properties

		/// <summary>
		/// Gets the context of the log message
		/// (transports custom information as the log message travels through the processing pipeline)
		/// </summary>
		public IDictionary<string, object> Context { get; } = new Dictionary<string, object>();

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
		/// Gets the log writer associated with the log message.
		/// </summary>
		public LogWriter LogWriter { get; private set; }

		/// <summary>
		/// Gets the name of the log writer associated with the log message.
		/// </summary>
		public string LogWriterName => LogWriter?.Name;

		/// <summary>
		/// Gets the log level associated with the log message.
		/// </summary>
		public LogLevel LogLevel { get; private set; }

		/// <summary>
		/// Gets the name of the log level associated with the log message.
		/// </summary>
		public string LogLevelName => LogLevel?.Name;

		/// <summary>
		/// Gets the tags attached to the log message.
		/// </summary>
		public TagSet Tags { get; private set; }

		/// <summary>
		/// Gets the name of the application emitting the log message
		/// (can differ from the process name, if the application is using an interpreter (the actual process)).
		/// </summary>
		public string ApplicationName { get; private set; }

		/// <summary>
		/// Gets the name of the process emitting the log message.
		/// </summary>
		public string ProcessName { get; private set; }

		/// <summary>
		/// Gets the id of the process emitting the log message.
		/// </summary>
		public int ProcessId { get; private set; }

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

			if (refCount < 0)
			{
				Interlocked.Increment(ref mRefCount);
				throw new InvalidOperationException("The reference count is already 0.");
			}

			if (refCount == 0)
			{
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
		/// (in ns, the actual precision depends on the <see cref="System.Diagnostics.Stopwatch" /> class).
		/// </param>
		/// <param name="logWriter">Log writer that was used to emit the message.</param>
		/// <param name="logLevel">Log level that is associated with the message.</param>
		/// <param name="tags">Tags that are associated with the log message.</param>
		/// <param name="applicationName">
		/// Name of the application emitting the log message
		/// (can differ from the process name, if the application is using an interpreter (the actual process)).
		/// </param>
		/// <param name="processName">Name of the process emitting the log message.</param>
		/// <param name="processId">Id of the process emitting the log message.</param>
		/// <param name="text">The actual text the log message is about.</param>
		/// <returns>The log message itself.</returns>
		internal LocalLogMessage InitWith(
			DateTimeOffset timestamp,
			long           highPrecisionTimestamp,
			LogWriter      logWriter,
			LogLevel       logLevel,
			TagSet         tags,
			string         applicationName,
			string         processName,
			int            processId,
			string         text)
		{
			Timestamp = timestamp;
			HighPrecisionTimestamp = highPrecisionTimestamp;
			LogWriter = logWriter;
			LogLevel = logLevel;
			Tags = tags;
			ApplicationName = applicationName;
			ProcessName = processName;
			ProcessId = processId;
			Text = text;

			return this;
		}

		/// <summary>
		/// Resets the log message to defaults.
		/// </summary>
		internal void Reset()
		{
			Context.Clear();
			LogWriter = null;
			LogLevel = null;
			Tags = null;
			ApplicationName = null;
			ProcessName = null;
			ProcessId = 0;
			Text = null;
		}

		#endregion

		#region Equality Check

		/// <summary>
		/// Checks whether the current log message equals the specified one.
		/// </summary>
		/// <param name="other">Log message to compare with.</param>
		/// <returns>
		/// true, if the current log message equals the specified one;
		/// otherwise false.
		/// </returns>
		public bool Equals(ILogMessage other)
		{
			if (other == null) return false;
			return Timestamp.Equals(other.Timestamp) &&
			       HighPrecisionTimestamp == other.HighPrecisionTimestamp &&
			       LogWriterName == other.LogWriterName &&
			       LogLevelName == other.LogLevelName &&
			       Equals(Tags, other.Tags) &&
			       ApplicationName == other.ApplicationName &&
			       ProcessName == other.ProcessName &&
			       ProcessId == other.ProcessId &&
			       Text == other.Text;
		}

		/// <summary>
		/// Checks whether the current log message equals the specified one.
		/// </summary>
		/// <param name="obj">Log message to compare with.</param>
		/// <returns>
		/// true, if the current log message equals the specified one;
		/// otherwise false.
		/// </returns>
		public override bool Equals(object obj)
		{
			return ReferenceEquals(this, obj) || obj is ILogMessage other && Equals(other);
		}

		/// <summary>
		/// Gets the hash code of the log message.
		/// </summary>
		/// <returns>Hash code of the log message.</returns>
		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = Timestamp.GetHashCode();
				hashCode = (hashCode * 397) ^ HighPrecisionTimestamp.GetHashCode();
				hashCode = (hashCode * 397) ^ (LogWriterName != null ? LogWriterName.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (LogLevelName != null ? LogLevelName.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Tags != null ? Tags.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (ApplicationName != null ? ApplicationName.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (ProcessName != null ? ProcessName.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ ProcessId;
				hashCode = (hashCode * 397) ^ (Text != null ? Text.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion
	}

}
