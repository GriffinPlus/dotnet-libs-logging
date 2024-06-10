///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging.Collections;

/// <summary>
/// Unit tests targeting the <see cref="SelectableFileBackedLogMessageFilter"/> class.
/// The <see cref="FileBackedLogMessageCollection"/> is run with support for read operations only.
/// Log file purpose is <see cref="LogFilePurpose.Analysis"/> and write mode is <see cref="LogFileWriteMode.Fast"/>.
/// </summary>
[Collection("LogFileTests")]
public class SelectableFileBackedLogMessageFilterTests_ReadOnly_Analysis_Fast : SelectableFileBackedLogMessageFilterTests_Base
{
	/// <summary>
	/// Initializes an instance of the <see cref="SelectableFileBackedLogMessageFilterTests_ReadOnly_Analysis_Fast"/> class.
	/// </summary>
	/// <param name="fixture">Fixture providing static test data.</param>
	public SelectableFileBackedLogMessageFilterTests_ReadOnly_Analysis_Fast(LogFileTestsFixture fixture) :
		base(
			fixture,
			true,
			LogFilePurpose.Analysis,
			LogFileWriteMode.Fast) { }
}
