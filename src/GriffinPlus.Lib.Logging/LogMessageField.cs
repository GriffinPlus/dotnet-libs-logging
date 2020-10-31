///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Flags corresponding to fields of a log message (primarily for testing purposes).
	/// </summary>
	[Flags]
	public enum LogMessageField
	{
		/// <summary>
		/// No fields.
		/// </summary>
		None = 0,

		/// <summary>
		/// The <see cref="ILogMessage.Timestamp"/> field.
		/// </summary>
		Timestamp = 1,

		/// <summary>
		/// The <see cref="ILogMessage.HighPrecisionTimestamp"/> field.
		/// </summary>
		HighPrecisionTimestamp = 1 << 1,

		/// <summary>
		/// The <see cref="ILogMessage.LogWriterName"/> field.
		/// </summary>
		LogWriterName = 1 << 2,

		/// <summary>
		/// The <see cref="ILogMessage.LogLevelName"/> field.
		/// </summary>
		LogLevelName = 1 << 3,

		/// <summary>
		/// The <see cref="ILogMessage.Tags"/> field.
		/// </summary>
		Tags = 1 << 4,

		/// <summary>
		/// The <see cref="ILogMessage.ApplicationName"/> field.
		/// </summary>
		ApplicationName = 1 << 5,

		/// <summary>
		/// The <see cref="ILogMessage.ProcessName"/> field.
		/// </summary>
		ProcessName = 1 << 6,

		/// <summary>
		/// The <see cref="ILogMessage.ProcessId"/> field.
		/// </summary>
		ProcessId = 1 << 7,

		/// <summary>
		/// The <see cref="ILogMessage.Text"/> field.
		/// </summary>
		Text = 1 << 8,

		/// <summary>
		/// All fields.
		/// </summary>
		All = Timestamp | HighPrecisionTimestamp | LogWriterName | LogLevelName | Tags | ApplicationName | ProcessName | ProcessId | Text
	}
}
