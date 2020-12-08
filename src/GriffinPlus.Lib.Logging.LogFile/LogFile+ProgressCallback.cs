///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging
{
	partial class LogFile
	{
		/// <summary>
		/// Delegate for methods that receive progress information for operations that may take some time.
		/// </summary>
		/// <param name="progress">Progress (0 = 0%, 1 = 100%).</param>
		/// <param name="canceled">true, if the operation was canceled; otherwise false.</param>
		/// <returns>
		/// true to continue the running operation;
		/// false to stop running operation.
		/// </returns>
		public delegate bool ProgressCallback(float progress, bool canceled);
	}
}
