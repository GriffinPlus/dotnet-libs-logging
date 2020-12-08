///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Modes influencing how the log file will handle write operations.
	/// </summary>
	public enum LogFileWriteMode
	{
		/// <summary>
		/// Write to the log file in a way that always keeps the log file consistent - even in case of sudden power loss.
		/// </summary>
		Robust = 1,

		/// <summary>
		/// Write to the log file as fast as possible using all available caching mechanisms
		/// (fast, but not reliable - the database may be corrupted in case of sudden power loss).
		/// </summary>
		Fast = 2
	}
}
