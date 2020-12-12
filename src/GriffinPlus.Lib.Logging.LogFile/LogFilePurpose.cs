///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// The purpose of a log file determining how the file is primarily used.
	/// </summary>
	public enum LogFilePurpose
	{
		/// <summary>
		/// The log file is primarily used for recording messages (optimized for writing, not for analysis).
		/// </summary>
		Recording = 1,

		/// <summary>
		/// The log file is primarily used for analyzing messages (optimized for analysis, not for writing).
		/// </summary>
		Analysis = 2
	}

}
