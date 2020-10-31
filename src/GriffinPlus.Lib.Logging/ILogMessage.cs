///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Interface log messages must implement.
	/// </summary>
	public interface ILogMessage
	{
		/// <summary>
		/// Gets the date/time the message was written to the log.
		/// </summary>
		DateTimeOffset Timestamp { get; }

		/// <summary>
		/// Gets the timestamp for relative time measurements with high precision
		/// (in nanoseconds, but the actual precision depends on the system timer).
		/// </summary>
		long HighPrecisionTimestamp { get; }

		/// <summary>
		/// Gets the name of the log writer associated with the log message.
		/// </summary>
		string LogWriterName { get; }

		/// <summary>
		/// Gets the name of the log level associated with the log message.
		/// </summary>
		string LogLevelName { get; }

		/// <summary>
		/// Gets the tags attached to the log message.
		/// </summary>
		TagSet Tags { get; }

		/// <summary>
		/// Gets the name of the application emitting the log message
		/// (can differ from the process name, if the application is using an interpreter (the actual process)).
		/// </summary>
		string ApplicationName { get; }

		/// <summary>
		/// Gets the name of the process emitting the log message.
		/// </summary>
		string ProcessName { get; }

		/// <summary>
		/// Gets the id of the process emitting the log message.
		/// </summary>
		int ProcessId { get; }

		/// <summary>
		/// Gets the actual text the log message is about.
		/// </summary>
		string Text { get; }

	}
}
