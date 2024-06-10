///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging.Collections;

/// <summary>
/// Unit tests targeting the <see cref="SelectableFileBackedLogMessageFilter"/> class.
/// The collection attached to the filter is run with support for read operations only.
/// Log file purpose is <see cref="LogFilePurpose.Recording"/> and write mode is <see cref="LogFileWriteMode.Robust"/>.
/// </summary>
[Collection("LogFileTests")]
public class SelectableFileBackedLogMessageFilterTests_ReadOnly_Recording_Robust : SelectableFileBackedLogMessageFilterTests_Base
{
	/// <summary>
	/// Initializes an instance of the <see cref="SelectableFileBackedLogMessageFilterTests_ReadOnly_Recording_Robust"/> class.
	/// </summary>
	/// <param name="fixture">Fixture providing static test data.</param>
	public SelectableFileBackedLogMessageFilterTests_ReadOnly_Recording_Robust(LogFileTestsFixture fixture) :
		base(
			fixture,
			true,
			LogFilePurpose.Recording,
			LogFileWriteMode.Robust) { }
}
