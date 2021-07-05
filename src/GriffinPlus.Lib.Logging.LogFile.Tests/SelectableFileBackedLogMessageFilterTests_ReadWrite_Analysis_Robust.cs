///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Unit tests targeting the <see cref="SelectableFileBackedLogMessageFilter"/> class.
	/// The collection attached to the filter is run with support for read and write operations.
	/// Log file purpose is <see cref="LogFilePurpose.Analysis"/> and write mode is <see cref="LogFileWriteMode.Robust"/>.
	/// </summary>
	[Collection("LogFileTests")]
	public class SelectableFileBackedLogMessageFilterTests_ReadWrite_Analysis_Robust : SelectableFileBackedLogMessageFilterTests_Base
	{
		/// <summary>
		/// Initializes an instance of the <see cref="SelectableFileBackedLogMessageFilterTests_ReadOnly_Recording_Robust"/> class.
		/// </summary>
		/// <param name="fixture">Fixture providing static test data.</param>
		public SelectableFileBackedLogMessageFilterTests_ReadWrite_Analysis_Robust(LogFileTestsFixture fixture) :
			base(
				fixture,
				false,
				LogFilePurpose.Analysis,
				LogFileWriteMode.Robust)
		{
		}
	}

}
