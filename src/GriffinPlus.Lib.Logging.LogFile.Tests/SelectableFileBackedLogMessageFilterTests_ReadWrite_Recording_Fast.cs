///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Unit tests targeting the <see cref="SelectableFileBackedLogMessageFilter"/> class.
	/// The collection is run with support for read and write operations.
	/// Log file purpose is <see cref="LogFilePurpose.Recording"/> and write mode is <see cref="LogFileWriteMode.Fast"/>.
	/// </summary>
	[Collection("LogFileTests")]
	public class SelectableFileBackedLogMessageFilterTests_ReadWrite_Recording_Fast : SelectableFileBackedLogMessageFilterTests_Base
	{
		/// <summary>
		/// Initializes an instance of the <see cref="SelectableFileBackedLogMessageFilterTests_ReadWrite_Recording_Fast"/> class.
		/// </summary>
		/// <param name="fixture">Fixture providing static test data.</param>
		public SelectableFileBackedLogMessageFilterTests_ReadWrite_Recording_Fast(LogFileTestsFixture fixture) :
			base(
				fixture,
				false,
				LogFilePurpose.Recording,
				LogFileWriteMode.Fast) { }
	}

}
