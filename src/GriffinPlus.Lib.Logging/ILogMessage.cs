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
		/// Gets a value indicating whether the log message is read-only.
		/// </summary>
		bool IsReadOnly { get; }

		/// <summary>
		/// Gets or sets the date/time the message was written to the log.
		/// </summary>
		/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
		DateTimeOffset Timestamp { get; set; }

		/// <summary>
		/// Gets or sets the timestamp for relative time measurements with high precision
		/// (in nanoseconds, but the actual precision depends on the system timer).
		/// </summary>
		/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
		long HighPrecisionTimestamp { get; set; }

		/// <summary>
		/// Gets or sets the number of preceding messages that have been lost before this message
		/// (useful when dealing with message streams).
		/// </summary>
		/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
		int LostMessageCount { get; set; }

		/// <summary>
		/// Gets or sets the name of the log writer associated with the log message.
		/// </summary>
		/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
		string LogWriterName { get; set; }

		/// <summary>
		/// Gets or sets the name of the log level associated with the log message.
		/// </summary>
		/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
		string LogLevelName { get; set; }

		/// <summary>
		/// Gets or sets the tags attached to the log message.
		/// </summary>
		/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
		TagSet Tags { get; set; }

		/// <summary>
		/// Gets or sets the name of the application emitting the log message
		/// (can differ from the process name, if the application is using an interpreter (the actual process)).
		/// </summary>
		/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
		string ApplicationName { get; set; }

		/// <summary>
		/// Gets or sets the name of the process emitting the log message.
		/// </summary>
		/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
		string ProcessName { get; set; }

		/// <summary>
		/// Gets or sets the id of the process emitting the log message.
		/// </summary>
		/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
		int ProcessId { get; set; }

		/// <summary>
		/// Gets or sets the actual text the log message is about.
		/// </summary>
		/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
		string Text { get; set; }
	}

}
