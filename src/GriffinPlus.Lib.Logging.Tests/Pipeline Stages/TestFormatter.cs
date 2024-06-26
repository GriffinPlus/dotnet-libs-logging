﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// A simple log message formatter for unit test purposes only.
/// </summary>
public class TestFormatter : ILogMessageFormatter
{
	/// <summary>
	/// Formats the specified log message.
	/// </summary>
	/// <param name="message">Message to format.</param>
	/// <returns>The formatted log message.</returns>
	public string Format(ILogMessage message)
	{
		// specify format of the timestamp explicitly to work around an issue with duplicating the timezone offset
		// (see https://github.com/microsoft/dotnet/issues/1144)
		// ReSharper disable once UseStringInterpolation
		return string.Format(
			"{0:O} ### {1} ### {2} ### {3} ### {4} ### {5} ### {6} ### {7} ### {8}",
			message.Timestamp,
			message.HighPrecisionTimestamp,
			message.LogWriterName,
			message.LogLevelName,
			string.Join(",", message.Tags),
			message.ApplicationName,
			message.ProcessName,
			message.ProcessId,
			message.Text);
	}
}
