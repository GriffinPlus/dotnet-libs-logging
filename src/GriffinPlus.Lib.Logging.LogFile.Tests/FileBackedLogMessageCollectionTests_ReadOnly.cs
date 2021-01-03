///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="FileBackedLogMessageCollection" /> class.
	/// The collection is run with support for read operations only.
	/// </summary>
	[Collection("LogFileTests")]
	public class FileBackedLogMessageCollectionTests_ReadOnly : FileBackedLogMessageCollectionTests_Base
	{
		/// <summary>
		/// Creates a new instance of the collection class to test, pre-populated with the specified number of random log messages.
		/// The collection should be disposed at the end to avoid generating orphaned log files.
		/// </summary>
		/// <param name="count">Number of random log messages the collection should contain.</param>
		/// <param name="messages">Receives messages that have been put into the collection.</param>
		/// <returns>A new instance of the collection class to test.</returns>
		protected override ILogMessageCollection<LogMessage> CreateCollection(int count, out LogMessage[] messages)
		{
			string path = Guid.NewGuid().ToString("D") + ".gplog";

			// create a collection backed by a new file
			using (var file1 = LogFile.OpenOrCreate(path, LogFilePurpose.Analysis, LogFileWriteMode.Fast))
			{
				// generate the required number of log message and add them to the collection
				messages = LoggingTestHelpers.GetTestMessages(count);
				if (count > 0) file1.Write(messages);
			}

			// open the created log file again with support for reading only
			var file2 = LogFile.OpenReadOnly(path);

			// let the collection delete the log file on its disposal
			file2.Messages.AutoDelete = true;

			// the collection should now have the default state
			TestCollectionPropertyDefaults(file2.Messages, count, true);

			// the collection should now contain the messages written into it
			// (the file-backed collection assigns message ids on its own, but they should be the same as the ids assigned to the test set)
			Assert.Equal(messages, file2.Messages.ToArray());

			return file2.Messages;
		}

		/// <summary>
		/// Initializes an instance of the <see cref="FileBackedLogMessageCollectionTests_ReadOnly" /> class.
		/// </summary>
		/// <param name="fixture">Fixture providing static test data.</param>
		public FileBackedLogMessageCollectionTests_ReadOnly(LogFileTestsFixture fixture) : base(fixture)
		{
			CollectionIsReadOnly = true;
		}
	}

}
