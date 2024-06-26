﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// A pool of log messages allowing log messages to be re-used to reduce garbage collection pressure (thread-safe).
/// </summary>
public class LocalLogMessagePool
{
	private readonly ConcurrentBag<LocalLogMessage> mMessages;

	/// <summary>
	/// Initializes a new instance of the <see cref="LocalLogMessagePool"/> class.
	/// </summary>
	public LocalLogMessagePool()
	{
		mMessages = [];
	}

	/// <summary>
	/// Gets the default log message pool.
	/// </summary>
	public static LocalLogMessagePool Default { get; } = new();

	/// <summary>
	/// Gets a log message from the pool, creates a new one, if the pool is empty. The returned message is not initialized.
	/// Call <see cref="LocalLogMessage.InitWith"/> to initialize it.
	/// </summary>
	/// <returns>The requested log message.</returns>
	public LocalLogMessage GetUninitializedMessage()
	{
		if (mMessages.TryTake(out LocalLogMessage message))
		{
			message.AddRef();
		}
		else
		{
			message = new LocalLogMessage(this);
		}

		Debug.Assert(message.RefCount == 1);
		return message;
	}

	/// <summary>
	/// Gets a log message from the pool, creates a new one, if the pool is empty.
	/// </summary>
	/// <param name="timestamp">Time the message was written to the log.</param>
	/// <param name="highPrecisionTimestamp">
	/// Timestamp for relative time measurements with high precision
	/// (the actual precision depends on the <see cref="System.Diagnostics.Stopwatch"/> class).
	/// </param>
	/// <param name="logWriter">Log writer that was used to emit the message.</param>
	/// <param name="logLevel">Log level that is associated with the message.</param>
	/// <param name="applicationName">
	/// Name of the application emitting the log message
	/// (can differ from the process name, if the application is using an interpreter (the actual process)).
	/// </param>
	/// <param name="processName">Name of the process emitting the log message.</param>
	/// <param name="processId">ID of the process emitting the log message.</param>
	/// <param name="text">The actual text the log message is about.</param>
	/// <returns>The requested log message.</returns>
	public LocalLogMessage GetMessage(
		DateTimeOffset timestamp,
		long           highPrecisionTimestamp,
		LogWriter      logWriter,
		LogLevel       logLevel,
		string         applicationName,
		string         processName,
		int            processId,
		string         text)
	{
		LocalLogMessage message = GetUninitializedMessage();
		return message.InitWith(
			timestamp,
			highPrecisionTimestamp,
			logWriter,
			logLevel,
			logWriter.Tags,
			applicationName,
			processName,
			processId,
			text);
	}

	/// <summary>
	/// Returns a log message to the pool, so it can be re-used.
	/// This message is called by the messages, if their reference counter gets 0.
	/// </summary>
	/// <param name="message">Message to return to the pool.</param>
	public void ReturnMessage(LocalLogMessage message)
	{
		Debug.Assert(message.RefCount == 0);
		message.Reset();
		mMessages.Add(message);
	}
}
