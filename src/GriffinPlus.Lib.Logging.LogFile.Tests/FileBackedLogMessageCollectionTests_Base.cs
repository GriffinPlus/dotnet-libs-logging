///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.IO;

using Xunit;

namespace GriffinPlus.Lib.Logging.Collections;

using static FileBackedLogMessageCollectionTests_Static;

/// <summary>
/// Unit tests targeting the <see cref="FileBackedLogMessageCollection"/> class.
/// </summary>
public abstract class FileBackedLogMessageCollectionTests_Base :
	LogMessageCollectionBaseTests<FileBackedLogMessageCollection>,
	IClassFixture<LogFileTestsFixture>
{
	protected static readonly LogFilePurpose[]    LogFilePurposes   = [LogFilePurpose.Recording, LogFilePurpose.Analysis];
	protected static readonly LogFileWriteMode[]  LogFileWriteModes = [LogFileWriteMode.Robust, LogFileWriteMode.Fast];
	protected readonly        LogFileTestsFixture Fixture;

	/// <summary>
	/// Initializes an instance of the <see cref="FileBackedLogMessageCollectionTests_Base"/> class.
	/// </summary>
	/// <param name="fixture">Fixture providing static test data.</param>
	protected FileBackedLogMessageCollectionTests_Base(LogFileTestsFixture fixture)
	{
		CollectionProvidesProtectedMessages = true;
		Fixture = fixture;
	}

	#region Construction

	/// <summary>
	/// Test data for test methods testing the instantiation of the collection.
	/// </summary>
	public static TheoryData<LogFilePurpose, LogFileWriteMode, bool> CreateTestData
	{
		get
		{
			var data = new TheoryData<LogFilePurpose, LogFileWriteMode, bool>();

			foreach (LogFilePurpose purpose in LogFilePurposes)
			foreach (LogFileWriteMode writeMode in LogFileWriteModes)
			foreach (bool populate in new[] { false, true })
			{
				data.Add(purpose, writeMode, populate);
			}

			return data;
		}
	}

	/// <summary>
	/// Tests creating a new instance of the <see cref="FileBackedLogMessageCollection"/> class with a new backing log file in the working directory.
	/// </summary>
	/// <param name="purpose">Log file purpose to test.</param>
	/// <param name="writeMode">Log file write mode to test.</param>
	/// <param name="populate">
	/// <see langword="true"/> to populate the log file with messages;<br/>
	/// otherwise, <see langword="false"/>.
	/// </param>
	[Theory]
	[MemberData(nameof(CreateTestData))]
	protected void Create_NewFile(LogFilePurpose purpose, LogFileWriteMode writeMode, bool populate)
	{
		string backingFilePath = Path.Combine(Environment.CurrentDirectory, "FileBackedLogMessageCollectionBuffer.gplog");

		// delete the file to work with to ensure that the collection creates a new one
		File.Delete(backingFilePath);
		Assert.False(File.Exists(backingFilePath));

		try
		{
			LogFileMessage[] messages = populate ? Fixture.GetLogMessages_Random_10K() : null;
			using (var collection = FileBackedLogMessageCollection.Create(backingFilePath, purpose, writeMode, messages))
			{
				Assert.True(File.Exists(backingFilePath));
				Assert.Equal(backingFilePath, collection.FilePath);

				TestCollectionPropertyDefaults(
					collection,
					populate ? messages.Length : 0,
					isReadOnly: false,
					isFixedSize: CollectionIsFixedSize,
					isSynchronized: CollectionIsSynchronized);

				if (populate)
				{
					Assert.Equal(messages, collection);
					Assert.Equal(messages, collection.LogFile.Read(0, messages.Length + 1));
				}
				else
				{
					Assert.Empty(collection);
					Assert.Equal(0, collection.LogFile.MessageCount);
				}
			}

			// the file should persist after disposing the collection
			Assert.True(File.Exists(backingFilePath));
		}
		finally
		{
			File.Delete(backingFilePath);
		}
	}

	/// <summary>
	/// Tests creating a new instance of the <see cref="FileBackedLogMessageCollection"/> class with an existing backing log file in the working directory.
	/// An exception should be thrown.
	/// </summary>
	/// <param name="purpose">Log file purpose to test.</param>
	/// <param name="writeMode">Log file write mode to test.</param>
	/// <param name="populate">
	/// <see langword="true"/> to populate the log file with messages;<br/>
	/// otherwise, <see langword="false"/>.
	/// </param>
	[Theory]
	[MemberData(nameof(CreateTestData))]
	protected void Create_ExistingFile(LogFilePurpose purpose, LogFileWriteMode writeMode, bool populate)
	{
		string backingFilePath = purpose == LogFilePurpose.Recording
			                         ? Fixture.GetCopyOfFile_Recording_RandomMessages_10K()
			                         : Fixture.GetCopyOfFile_Analysis_RandomMessages_10K();

		LogFileMessage[] messages = populate ? Fixture.GetLogMessages_Random_10K() : null;

		try
		{
			Assert.Throws<LogFileException>(() => FileBackedLogMessageCollection.Create(backingFilePath, purpose, writeMode, messages));
		}
		finally
		{
			// remove temporary log file to avoid polluting the output directory
			File.Delete(backingFilePath);
		}
	}

	/// <summary>
	/// Test data for test methods testing the instantiation of the collection.
	/// </summary>
	public static TheoryData<LogFilePurpose, LogFileWriteMode> CreateOrOpenTestData
	{
		get
		{
			var data = new TheoryData<LogFilePurpose, LogFileWriteMode>();

			foreach (LogFilePurpose purpose in LogFilePurposes)
			foreach (LogFileWriteMode writeMode in LogFileWriteModes)
			{
				data.Add(purpose, writeMode);
			}

			return data;
		}
	}

	/// <summary>
	/// Tests creating a new instance of the <see cref="FileBackedLogMessageCollection"/> class with a new backing log file in the working directory.
	/// </summary>
	/// <param name="purpose">Log file purpose to test.</param>
	/// <param name="writeMode">Log file write mode to test.</param>
	[Theory]
	[MemberData(nameof(CreateOrOpenTestData))]
	protected void OpenOrCreate_NewFile(LogFilePurpose purpose, LogFileWriteMode writeMode)
	{
		string backingFilePath = Path.Combine(Environment.CurrentDirectory, "FileBackedLogMessageCollectionBuffer.gplog");

		// delete the file to work with to ensure that the collection creates a new one
		File.Delete(backingFilePath);
		Assert.False(File.Exists(backingFilePath));

		try
		{
			using (var collection = FileBackedLogMessageCollection.OpenOrCreate(backingFilePath, purpose, writeMode))
			{
				Assert.True(File.Exists(backingFilePath));
				Assert.Equal(backingFilePath, collection.FilePath);
				TestCollectionPropertyDefaults(
					collection,
					expectedCount: 0,
					isReadOnly: false,
					isFixedSize: CollectionIsFixedSize,
					isSynchronized: CollectionIsSynchronized);
			}

			// the file should persist after disposing the collection
			Assert.True(File.Exists(backingFilePath));
		}
		finally
		{
			File.Delete(backingFilePath);
		}
	}

	/// <summary>
	/// Tests creating a new instance of the <see cref="FileBackedLogMessageCollection"/> class with an existing backing log file.
	/// </summary>
	/// <param name="purpose">Log file purpose to test.</param>
	/// <param name="writeMode">Log file write mode to test.</param>
	[Theory]
	[MemberData(nameof(CreateOrOpenTestData))]
	protected void OpenOrCreate_ExistingFile(LogFilePurpose purpose, LogFileWriteMode writeMode)
	{
		string path = purpose == LogFilePurpose.Recording
			              ? Fixture.GetCopyOfFile_Recording_RandomMessages_10K()
			              : Fixture.GetCopyOfFile_Analysis_RandomMessages_10K();

		try
		{
			Assert.True(File.Exists(path));

			using (var collection = FileBackedLogMessageCollection.OpenOrCreate(path, purpose, writeMode))
			{
				Assert.True(File.Exists(path));
				Assert.Equal(path, collection.FilePath);
				TestCollectionPropertyDefaults(
					collection,
					expectedCount: 10000,
					isReadOnly: false,
					isFixedSize: CollectionIsFixedSize,
					isSynchronized: CollectionIsSynchronized);
			}

			// the file should persist after disposing the collection
			Assert.True(File.Exists(path));
		}
		finally
		{
			// remove temporary log file to avoid polluting the output directory
			File.Delete(path);
		}
	}

	/// <summary>
	/// Test data for test methods testing the instantiation of the collection.
	/// </summary>
	public static TheoryData<LogFilePurpose, LogFileWriteMode> OpenTestData
	{
		get
		{
			var data = new TheoryData<LogFilePurpose, LogFileWriteMode>();

			foreach (LogFilePurpose purpose in LogFilePurposes)
			foreach (LogFileWriteMode writeMode in LogFileWriteModes)
			{
				data.Add(purpose, writeMode);
			}

			return data;
		}
	}

	/// <summary>
	/// Tests creating a new instance of the <see cref="FileBackedLogMessageCollection"/> class with an existing backing log file.
	/// </summary>
	/// <param name="purpose">Log file purpose to test.</param>
	/// <param name="writeMode">Log file write mode to test.</param>
	[Theory]
	[MemberData(nameof(OpenTestData))]
	protected void Open(LogFilePurpose purpose, LogFileWriteMode writeMode)
	{
		string path = purpose == LogFilePurpose.Recording
			              ? Fixture.GetCopyOfFile_Recording_RandomMessages_10K()
			              : Fixture.GetCopyOfFile_Analysis_RandomMessages_10K();

		try
		{
			Assert.True(File.Exists(path));

			using (FileBackedLogMessageCollection collection = FileBackedLogMessageCollection.Open(path, writeMode))
			{
				Assert.True(File.Exists(path));
				Assert.Equal(path, collection.FilePath);

				TestCollectionPropertyDefaults(
					collection,
					expectedCount: 10000,
					isReadOnly: false,
					isFixedSize: CollectionIsFixedSize,
					isSynchronized: CollectionIsSynchronized);
			}

			// the file should persist after disposing the collection
			Assert.True(File.Exists(path));
		}
		finally
		{
			// remove temporary log file to avoid polluting the output directory
			File.Delete(path);
		}
	}

	/// <summary>
	/// Tests creating a new instance of the <see cref="FileBackedLogMessageCollection"/> class with an existing backing log file.
	/// </summary>
	/// <param name="purpose">Log file purpose to test.</param>
	/// <param name="writeMode">Log file write mode to test.</param>
	[Theory]
	[MemberData(nameof(OpenTestData))]
	protected void OpenReadOnly(LogFilePurpose purpose, LogFileWriteMode writeMode)
	{
		string path = purpose == LogFilePurpose.Recording
			              ? Fixture.GetCopyOfFile_Recording_RandomMessages_10K()
			              : Fixture.GetCopyOfFile_Analysis_RandomMessages_10K();

		try
		{
			Assert.True(File.Exists(path));

			using (FileBackedLogMessageCollection collection = FileBackedLogMessageCollection.OpenReadOnly(path))
			{
				Assert.True(File.Exists(path));
				Assert.Equal(path, collection.FilePath);

				TestCollectionPropertyDefaults(
					collection,
					expectedCount: 10000,
					isReadOnly: true,
					isFixedSize: CollectionIsFixedSize,
					isSynchronized: CollectionIsSynchronized);
			}

			// the file should persist after disposing the collection
			Assert.True(File.Exists(path));
		}
		finally
		{
			// remove temporary log file to avoid polluting the output directory
			File.Delete(path);
		}
	}

	#endregion

	#region LogFile and FilePath

	/// <summary>
	/// Tests whether the collection provides its backing <see cref="LogFile"/> instance via its
	/// <see cref="FileBackedLogMessageCollection.LogFile"/> property and whether the <see cref="FileBackedLogMessageCollection.FilePath"/>
	/// property returns the same path as the log file.
	/// </summary>
	[Fact]
	protected virtual void LogFileAndFilePath()
	{
		using FileBackedLogMessageCollection collection = CreateCollection(0, out LogMessage[] _);
		LogMessageCollectionEventWatcher eventWatcher = collection.AttachEventWatcher();

		// check whether the actual state of the property matches the expected state
		Assert.Equal(100, collection.CachePageCapacity);

		// no events should have been raised
		eventWatcher.CheckInvocations();
	}

	#endregion

	#region Copying Message (For Test Purposes)

	/// <summary>
	/// Creates a copy of the specified log message.
	/// </summary>
	/// <param name="message">Log message to copy.</param>
	/// <returns>A copy of the specified log message.</returns>
	protected override LogMessage CopyMessage(LogMessage message)
	{
		return new LogFileMessage((LogFileMessage)message);
	}

	#endregion
}
