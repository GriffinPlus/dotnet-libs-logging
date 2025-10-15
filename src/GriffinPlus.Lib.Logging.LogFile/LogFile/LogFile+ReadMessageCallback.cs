///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging;

partial class LogFile
{
	/// <summary>
	/// Delegate for methods that are called to process log messages read from a log file.
	/// </summary>
	/// <param name="message">Log message that was read from the log file.</param>
	/// <returns>
	/// <see langword="true"/> to continue reading;<br/>
	/// <see langword="false"/> to stop reading.
	/// </returns>
	public delegate bool ReadMessageCallback(LogFileMessage message);
}
