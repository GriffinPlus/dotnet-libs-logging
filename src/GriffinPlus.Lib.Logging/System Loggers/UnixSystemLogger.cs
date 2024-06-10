///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// System logger that uses syslog to log messages (for UNIX systems).
/// Not implemented, yet.
/// </summary>
public class UnixSystemLogger : ISystemLogger
{
	/// <summary>
	/// Initializes a new instances of the <see cref="UnixSystemLogger"/> class.
	/// </summary>
	public UnixSystemLogger()
	{
		// TODO Implement.
	}

	/// <summary>
	/// Disposes the system logger.
	/// </summary>
	public void Dispose()
	{
		// TODO Implement.
	}

	/// <summary>
	/// Writes an informational message to the system log.
	/// </summary>
	/// <param name="message">Message to write.</param>
	public void WriteInfo(string message)
	{
		// TODO Implement.
	}

	/// <summary>
	/// Writes a warning to the system log.
	/// </summary>
	/// <param name="message">Message to write.</param>
	public void WriteWarning(string message)
	{
		// TODO Implement.
	}

	/// <summary>
	/// Writes an error to the system log.
	/// </summary>
	/// <param name="message">Message to write.</param>
	public void WriteError(string message)
	{
		// TODO Implement.
	}
}
