///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;

using Xunit;

namespace GriffinPlus.Lib.Logging.Collections;

using static FileBackedLogMessageCollectionTests_Static;

/// <summary>
/// Unit tests targeting the <see cref="FileBackedLogMessageCollection"/> class.
/// The collection is run with support for read and write operations.
/// </summary>
[Collection("LogFileTests")]
public class FileBackedLogMessageCollectionTests_ReadWrite : FileBackedLogMessageCollectionTests_Base
{
	/// <summary>
	/// Creates a new instance of the collection class to test, pre-populated with the specified number of random log messages.
	/// The collection should be disposed at the end to avoid generating orphaned log files.
	/// </summary>
	/// <param name="count">Number of random log messages the collection should contain.</param>
	/// <param name="messages">Receives messages that have been put into the collection.</param>
	/// <returns>A new instance of the collection class to test.</returns>
	protected override FileBackedLogMessageCollection CreateCollection(int count, out LogMessage[] messages)
	{
		string path = Guid.NewGuid().ToString("D") + ".gplog";

		// create a collection backed by a new file
		using (var file1 = LogFile.OpenOrCreate(path, LogFilePurpose.Analysis, LogFileWriteMode.Fast))
		{
			// generate the required number of log message and add them to the collection
			LogFileMessage[] fileLogMessages = LoggingTestHelpers.GetTestMessages<LogFileMessage>(count);
			for (long i = 0; i < fileLogMessages.Length; i++) fileLogMessages[i].Id = i;
			messages = fileLogMessages.Cast<LogMessage>().ToArray();
			if (count > 0) file1.Write(messages);
		}

		// open the created log file again with support for reading and writing only
		LogFile file2 = LogFile.Open(path, LogFileWriteMode.Fast);

		// let the collection delete the log file on its disposal
		file2.Messages.AutoDelete = true;

		// the collection should now have the default state
		TestCollectionPropertyDefaults(
			file2.Messages,
			count,
			isReadOnly: false,
			isFixedSize: CollectionIsFixedSize,
			isSynchronized: CollectionIsSynchronized);

		// the collection should now contain the messages written into it
		// (the file-backed collection assigns message ids on its own, but they should be the same as the ids assigned to the test set)
		Assert.Equal(messages, file2.Messages.ToArray());

		// the test assumes that the collection uses single-item notifications
		file2.Messages.UseMultiItemNotifications = false;

		return file2.Messages;
	}

	/// <summary>
	/// Initializes an instance of the <see cref="FileBackedLogMessageCollectionTests_ReadWrite"/> class.
	/// </summary>
	/// <param name="fixture">Fixture providing static test data.</param>
	public FileBackedLogMessageCollectionTests_ReadWrite(LogFileTestsFixture fixture) : base(fixture)
	{
		CollectionIsReadOnly = false;
	}
}
