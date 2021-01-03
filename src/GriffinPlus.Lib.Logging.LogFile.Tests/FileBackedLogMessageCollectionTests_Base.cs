///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="FileBackedLogMessageCollection" /> class.
	/// </summary>
	public abstract class FileBackedLogMessageCollectionTests_Base : LogMessageCollectionBaseTests, IClassFixture<LogFileTestsFixture>
	{
		protected static readonly LogFilePurpose[]    LogFilePurposes   = { LogFilePurpose.Recording, LogFilePurpose.Analysis };
		protected static readonly LogFileWriteMode[]  LogFileWriteModes = { LogFileWriteMode.Robust, LogFileWriteMode.Fast };
		protected readonly        LogFileTestsFixture Fixture;

		/// <summary>
		/// Creates a new instance of the collection class to test, pre-populated with the specified number of random log messages.
		/// </summary>
		/// <param name="count">Number of random log messages the collection should contain.</param>
		/// <param name="messages">Receives messages that have been put into the collection.</param>
		/// <returns>A new instance of the collection class to test.</returns>
		protected override ILogMessageCollection<LogMessage> CreateCollection(int count, out LogMessage[] messages)
		{
			// running common collection tests with a fresh temporary collection should be sufficient and cover all common collection specific functionality
			messages = LoggingTestHelpers.GetTestMessages(count);
			var collection = FileBackedLogMessageCollection.CreateTemporaryCollection(true);
			if (count > 0) collection.AddRange(messages);

			// the collection should now have the default state
			TestCollectionPropertyDefaults(collection, count, false);

			// the collection should now contain the messages written into it
			// (the file-backed collection assigns message ids on its own, but they should be the same as the ids assigned to the test set)
			Assert.Equal(messages, collection.ToArray());

			return collection;
		}

		/// <summary>
		/// Initializes an instance of the <see cref="FileBackedLogMessageCollectionTests_Base" /> class.
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
		public static IEnumerable<object[]> CreateTestData
		{
			get
			{
				foreach (var purpose in LogFilePurposes)
				foreach (var writeMode in LogFileWriteModes)
				{
					yield return new object[] { purpose, writeMode };
				}
			}
		}

		/// <summary>
		/// Tests creating a new instance of the <see cref="FileBackedLogMessageCollection" /> class with a new backing log file in the working directory.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(CreateTestData))]
		private void OpenOrCreate_NewFile(LogFilePurpose purpose, LogFileWriteMode writeMode)
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
					TestCollectionPropertyDefaults(collection, 0, false);
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
		/// Tests creating a new instance of the <see cref="FileBackedLogMessageCollection" /> class with an existing backing log file.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(CreateTestData))]
		private void OpenOrCreate_ExistingFile(LogFilePurpose purpose, LogFileWriteMode writeMode)
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
					TestCollectionPropertyDefaults(collection, 10000, false);
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
		public static IEnumerable<object[]> OpenTestData
		{
			get
			{
				foreach (var purpose in LogFilePurposes)
				foreach (var writeMode in LogFileWriteModes)
				{
					yield return new object[] { purpose, writeMode };
				}
			}
		}

		/// <summary>
		/// Tests creating a new instance of the <see cref="FileBackedLogMessageCollection" /> class with an existing backing log file.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(OpenTestData))]
		private void Open(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? Fixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : Fixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				Assert.True(File.Exists(path));

				using (var collection = FileBackedLogMessageCollection.Open(path, writeMode))
				{
					Assert.True(File.Exists(path));
					Assert.Equal(path, collection.FilePath);
					TestCollectionPropertyDefaults(collection, 10000, false);
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
		/// Tests creating a new instance of the <see cref="FileBackedLogMessageCollection" /> class with an existing backing log file.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(OpenTestData))]
		private void OpenReadOnly(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? Fixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : Fixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				Assert.True(File.Exists(path));

				using (var collection = FileBackedLogMessageCollection.OpenReadOnly(path))
				{
					Assert.True(File.Exists(path));
					Assert.Equal(path, collection.FilePath);
					TestCollectionPropertyDefaults(collection, 10000, true);
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

		#region CreateTemporaryCollection()

		/// <summary>
		/// Test data for the <see cref="CreateTemporaryCollection" /> test method.
		/// </summary>
		public static IEnumerable<object[]> CreateTemporaryCollectionTestData
		{
			get
			{
				foreach (bool deleteAutomatically in new[] { false, true })
				foreach (var purpose in LogFilePurposes)
				foreach (var writeMode in LogFileWriteModes)
				{
					yield return new object[] { null, deleteAutomatically, purpose, writeMode };                         // default temporary folder
					yield return new object[] { Environment.CurrentDirectory, deleteAutomatically, purpose, writeMode }; // specific temporary folder
				}
			}
		}

		/// <summary>
		/// Tests creating an instance of the <see cref="FileBackedLogMessageCollection" /> class with a temporary backing log file.
		/// </summary>
		/// <param name="deleteAutomatically">
		/// true to delete the file automatically when the collection is disposed (or the next time, a temporary collection is created in the same directory);
		/// false to keep it after the collection is disposed.
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
		[Theory]
		[MemberData(nameof(CreateTemporaryCollectionTestData))]
		private void CreateTemporaryCollection(
			string           temporaryDirectoryPath,
			bool             deleteAutomatically,
			LogFilePurpose   purpose,
			LogFileWriteMode mode)
		{
			string effectiveTemporaryFolderPath = temporaryDirectoryPath ?? Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
			string backingFilePath;

			using (var collection = FileBackedLogMessageCollection.CreateTemporaryCollection(deleteAutomatically, temporaryDirectoryPath, purpose, mode))
			{
				Assert.True(File.Exists(collection.FilePath));
				Assert.Equal(effectiveTemporaryFolderPath, Path.GetDirectoryName(collection.FilePath));
				TestCollectionPropertyDefaults(collection, 0, false);

				backingFilePath = collection.FilePath;
			}

			// the file should not persist after disposing the collection, if auto-deletion is enabled
			Assert.Equal(deleteAutomatically, !File.Exists(backingFilePath));
		}

		#endregion

		#region LogFile and FilePath

		/// <summary>
		/// Tests whether the collection provides its backing <see cref="LogFile" /> instance via its
		/// <see cref="FileBackedLogMessageCollection.LogFile" /> property and whether the <see cref="FileBackedLogMessageCollection.FilePath" />
		/// property returns the same path as the log file.
		/// </summary>
		[Fact]
		protected virtual void LogFileAndFilePath()
		{
			using (var collection = (FileBackedLogMessageCollection)CreateCollection(0, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// check whether the actual state of the property matches the expected state
				Assert.Equal(100, collection.CachePageCapacity);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Checks whether properties of the specified collection have the expected default values and
		/// whether the collection contains the expected amount of log messages.
		/// </summary>
		/// <param name="collection">Collection to check.</param>
		/// <param name="expectedCount">Expected number of log messages in the collection.</param>
		/// <param name="isReadOnly">true, if the collection is read-only; otherwise false.</param>
		protected void TestCollectionPropertyDefaults(FileBackedLogMessageCollection collection, long expectedCount, bool isReadOnly)
		{
			using (var eventWatcher = collection.AttachEventWatcher())
			{
				// check collection specific properties
				Assert.Equal(expectedCount, collection.Count);
				Assert.Equal(20, collection.MaxCachePageCount);
				Assert.Equal(100, collection.CachePageCapacity);

				// check log file specific properties
				Assert.NotNull(collection.LogFile);
				Assert.NotNull(collection.FilePath);
				Assert.Equal(collection.LogFile.FilePath, collection.FilePath);

				// check properties exposed by IList implementation
				{
					var list = collection as IList;
					Assert.Equal(expectedCount, list.Count);
					Assert.Equal(isReadOnly, list.IsReadOnly);
					Assert.Equal(CollectionIsFixedSize, list.IsFixedSize);
					Assert.Equal(CollectionIsSynchronized, list.IsSynchronized);
					Assert.NotSame(collection, list.SyncRoot); // sync root must not be the same as the collection to avoid deadlocks
				}

				// check properties exposed by IList<T> implementation
				{
					var list = collection as IList<LogMessage>;
					Assert.Equal(isReadOnly, list.IsReadOnly);
					Assert.Equal(expectedCount, list.Count);
				}

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion
	}

}
