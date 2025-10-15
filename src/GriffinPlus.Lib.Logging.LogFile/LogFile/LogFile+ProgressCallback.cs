///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging;

partial class LogFile
{
	/// <summary>
	/// Delegate for methods that receive progress information for operations that may take some time.
	/// </summary>
	/// <param name="progress">Progress (0 = 0%, 1 = 100%).</param>
	/// <param name="canceled">
	/// <see langword="true"/> if the operation was canceled;<br/>
	/// otherwise, <see langword="false"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> to continue the running operation;<br/>
	/// <see langword="false"/> to stop running operation.
	/// </returns>
	public delegate bool ProgressCallback(float progress, bool canceled);
}
