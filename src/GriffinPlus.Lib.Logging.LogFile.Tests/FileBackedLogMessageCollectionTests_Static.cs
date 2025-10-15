///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Xunit;

#pragma warning disable xUnit2013

namespace GriffinPlus.Lib.Logging.Collections;

/// <summary>
/// Unit tests targeting the <see cref="FileBackedLogMessageCollection"/> class.
/// </summary>
[Collection("LogFileTests")]
public class FileBackedLogMessageCollectionTests_Static
{
	private static readonly LogFilePurpose[]   sLogFilePurposes   = [LogFilePurpose.Recording, LogFilePurpose.Analysis];
	private static readonly LogFileWriteMode[] sLogFileWriteModes = [LogFileWriteMode.Robust, LogFileWriteMode.Fast];

	/// <summary>
	/// Initializes an instance of the <see cref="FileBackedLogMessageCollectionTests_Static"/> class.
	/// </summary>
	public FileBackedLogMessageCollectionTests_Static() { }

	#region CreateTemporaryCollection()

	/// <summary>
	/// Test data for the <see cref="CreateTemporaryCollection"/> test method.
	/// </summary>
	public static TheoryData<string, bool, LogFilePurpose, LogFileWriteMode, bool> CreateTemporaryCollectionTestData
	{
		get
		{
			var data = new TheoryData<string, bool, LogFilePurpose, LogFileWriteMode, bool>();

			foreach (bool deleteAutomatically in new[] { false, true })
			foreach (LogFilePurpose purpose in sLogFilePurposes)
			foreach (LogFileWriteMode writeMode in sLogFileWriteModes)
			foreach (bool populate in new[] { false, true })
			{
				// default temporary folder (null)
				data.Add(null, deleteAutomatically, purpose, writeMode, populate);

				// specific temporary folder (current directory)
				data.Add(Environment.CurrentDirectory, deleteAutomatically, purpose, writeMode, populate);
			}

			return data;
		}
	}

	/// <summary>
	/// Tests creating an instance of the <see cref="FileBackedLogMessageCollection"/> class with a temporary backing log file.
	/// </summary>
	/// <param name="deleteAutomatically">
	/// <see langword="true"/> to delete the file automatically when the collection is disposed
	/// (or the next time, a temporary collection is created in the same directory);<br/>
	/// <see langword="false"/> to keep it after the collection is disposed.
	/// </param>
	/// <param name="temporaryDirectoryPath">
	/// Path of the temporary directory to use;
	/// null to use the default temporary directory (default).
	/// </param>
	/// <param name="purpose">
	/// Purpose of the log file determining whether the log file is primarily used for recording or for analysis (default).
	/// </param>
	/// <param name="mode">
	/// Write mode determining whether to open the log file in 'robust' or 'fast' mode (default).
	/// </param>
	/// <param name="populate">
	/// <see langword="true"/> to populate the collection with log messages as part of the creation step;<br/>
	/// otherwise, <see langword="false"/>.
	/// </param>
	[Theory]
	[MemberData(nameof(CreateTemporaryCollectionTestData))]
	private void CreateTemporaryCollection(
		string           temporaryDirectoryPath,
		bool             deleteAutomatically,
		LogFilePurpose   purpose,
		LogFileWriteMode mode,
		bool             populate)
	{
		string effectiveTemporaryFolderPath = temporaryDirectoryPath ?? Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
		string backingFilePath;

		using (var collection = FileBackedLogMessageCollection.CreateTemporaryCollection(deleteAutomatically, temporaryDirectoryPath, purpose, mode))
		{
			Assert.True(File.Exists(collection.FilePath));
			Assert.Equal(effectiveTemporaryFolderPath, Path.GetDirectoryName(collection.FilePath));

			TestCollectionPropertyDefaults(
				collection,
				expectedCount: 0,
				isReadOnly: false,
				isFixedSize: false,
				isSynchronized: false);

			backingFilePath = collection.FilePath;
		}

		// the file should not persist after disposing the collection, if auto-deletion is enabled
		Assert.Equal(deleteAutomatically, !File.Exists(backingFilePath));

		// delete the file, if it still exists to avoid polluting the output directory
		File.Delete(backingFilePath);
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Checks whether properties of the specified collection have the expected default values and
	/// whether the collection contains the expected amount of log messages.
	/// </summary>
	/// <param name="collection">Collection to check.</param>
	/// <param name="expectedCount">Expected number of log messages in the collection.</param>
	/// <param name="isReadOnly">
	/// <see langword="true"/> if the collection is read-only;<br/>
	/// otherwise, <see langword="false"/>.
	/// </param>
	/// <param name="isFixedSize">
	/// <see langword="true"/> if the collection has a fixed size;<br/>
	/// otherwise, <see langword="false"/>.
	/// </param>
	/// <param name="isSynchronized">
	/// <see langword="true"/> if the collection is synchronized;<br/>
	/// otherwise, <see langword="false"/>.
	/// </param>
	internal static void TestCollectionPropertyDefaults(
		FileBackedLogMessageCollection collection,
		long                           expectedCount,
		bool                           isReadOnly,
		bool                           isFixedSize,
		bool                           isSynchronized)
	{
		using LogMessageCollectionEventWatcher eventWatcher = collection.AttachEventWatcher();

		// check collection specific properties
		Assert.NotNull(collection.FilePath);
		Assert.Equal(isReadOnly, collection.IsReadOnly);
		Assert.Equal(expectedCount, collection.Count);
		Assert.Equal(20, collection.MaxCachePageCount);
		Assert.Equal(100, collection.CachePageCapacity);

		// check log file specific properties
		Assert.NotNull(collection.LogFile);
		Assert.Equal(collection.FilePath, collection.LogFile.FilePath);
		Assert.Equal(isReadOnly, collection.LogFile.IsReadOnly);
		Assert.Equal(expectedCount, collection.LogFile.MessageCount);
		if (expectedCount > 0)
		{
			Assert.Equal(0, collection.LogFile.OldestMessageId);
			Assert.Equal(expectedCount - 1, collection.LogFile.NewestMessageId);
		}
		else
		{
			Assert.Equal(-1, collection.LogFile.OldestMessageId);
			Assert.Equal(-1, collection.LogFile.NewestMessageId);
		}

		// check properties exposed by IList implementation
		{
			IList list = collection;
			Assert.Equal(expectedCount, list.Count);
			Assert.Equal(isReadOnly, list.IsReadOnly);
			Assert.Equal(isFixedSize, list.IsFixedSize);
			Assert.Equal(isSynchronized, list.IsSynchronized);
			Assert.NotSame(collection, list.SyncRoot); // sync root must not be the same as the collection to avoid deadlocks
		}

		// check properties exposed by IList<T> implementation
		{
			IList<LogMessage> list = collection;
			Assert.Equal(isReadOnly, list.IsReadOnly);
			Assert.Equal(expectedCount, list.Count);
		}

		// no events should have been raised
		eventWatcher.CheckInvocations();
	}

	#endregion
}
