///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// System logger that does not do anything (for missing integrations).
/// </summary>
public class NoopSystemLogger : ISystemLogger
{
	/// <summary>
	/// Initializes a new instances of the <see cref="NoopSystemLogger"/> class.
	/// </summary>
	public NoopSystemLogger() { }

	/// <summary>
	/// Disposes the system logger.
	/// </summary>
	public void Dispose() { }

	/// <summary>
	/// Writes an informational message to the system log.
	/// </summary>
	/// <param name="message">Message to write.</param>
	public void WriteInfo(string message) { }

	/// <summary>
	/// Writes a warning to the system log.
	/// </summary>
	/// <param name="message">Message to write.</param>
	public void WriteWarning(string message) { }

	/// <summary>
	/// Writes an error to the system log.
	/// </summary>
	/// <param name="message">Message to write.</param>
	public void WriteError(string message) { }
}
