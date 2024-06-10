///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Interface of the system's logging facility, i.e. the windows event log on Windows and syslog on Linux.
/// </summary>
public interface ISystemLogger : IDisposable
{
	/// <summary>
	/// Writes an informational message to the system log.
	/// </summary>
	/// <param name="message">Message to write.</param>
	void WriteInfo(string message);

	/// <summary>
	/// Writes a warning to the system log.
	/// </summary>
	/// <param name="message">Message to write.</param>
	void WriteWarning(string message);

	/// <summary>
	/// Writes an error to the system log.
	/// </summary>
	/// <param name="message">Message to write.</param>
	void WriteError(string message);
}
