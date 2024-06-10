///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Interface for the asynchronous initialization feature of the <see cref="LogFileMessage"/> class.
/// </summary>
public interface IFileLogMessageInitializer
{
	/// <summary>
	/// Initializes the log message.
	/// </summary>
	/// <param name="id">Id uniquely identifying the message in the log file.</param>
	/// <param name="timestamp">Time the message was written to the log.</param>
	/// <param name="highPrecisionTimestamp">
	/// Timestamp for relative time measurements with high precision
	/// (the actual precision depends on the <see cref="System.Diagnostics.Stopwatch"/> class).
	/// </param>
	/// <param name="lostMessageCount">
	/// Gets or sets the number of preceding messages that have been lost before this message
	/// (useful when dealing with message streams).
	/// </param>
	/// <param name="logWriterName">Name of the log writer that was used to emit the message.</param>
	/// <param name="logLevelName">Name of the log level that is associated with the message.</param>
	/// <param name="tags">Tags that are associated with the message.</param>
	/// <param name="applicationName">
	/// Name of the application emitting the log message
	/// (can differ from the process name, if the application is using an interpreter (the actual process)).
	/// </param>
	/// <param name="processName">Name of the process emitting the log message.</param>
	/// <param name="processId">ID of the process emitting the log message.</param>
	/// <param name="text">The actual text the log message is about.</param>
	/// <returns>The initialized log message.</returns>
	/// <exception cref="InvalidOperationException">The log message is already initialized.</exception>
	LogFileMessage Initialize(
		long           id,
		DateTimeOffset timestamp,
		long           highPrecisionTimestamp,
		int            lostMessageCount,
		string         logWriterName,
		string         logLevelName,
		TagSet         tags,
		string         applicationName,
		string         processName,
		int            processId,
		string         text);
}
