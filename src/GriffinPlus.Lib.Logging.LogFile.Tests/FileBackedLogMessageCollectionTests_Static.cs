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

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Unit tests targeting the <see cref="FileBackedLogMessageCollection"/> class.
	/// </summary>
	[Collection("LogFileTests")]
	public class FileBackedLogMessageCollectionTests_Static : IClassFixture<LogFileTestsFixture>
	{
		private static readonly LogFilePurpose[]    sLogFilePurposes   = { LogFilePurpose.Recording, LogFilePurpose.Analysis };
		private static readonly LogFileWriteMode[]  sLogFileWriteModes = { LogFileWriteMode.Robust, LogFileWriteMode.Fast };
		private readonly        LogFileTestsFixture mFixture;

		/// <summary>
		/// Initializes an instance of the <see cref="FileBackedLogMessageCollectionTests_Static"/> class.
		/// </summary>
		/// <param name="fixture">Fixture providing static test data.</param>
		public FileBackedLogMessageCollectionTests_Static(LogFileTestsFixture fixture)
		{
			mFixture = fixture;
		}

		#region CreateTemporaryCollection()

		/// <summary>
		/// Test data for the <see cref="CreateTemporaryCollection"/> test method.
		/// </summary>
		public static IEnumerable<object[]> CreateTemporaryCollectionTestData
		{
			get
			{
				foreach (bool deleteAutomatically in new[] { false, true })
				foreach (LogFilePurpose purpose in sLogFilePurposes)
				foreach (LogFileWriteMode writeMode in sLogFileWriteModes)
				foreach (bool populate in new[] { false, true })
				{
					yield return new object[] { null, deleteAutomatically, purpose, writeMode, populate };                         // default temporary folder
					yield return new object[] { Environment.CurrentDirectory, deleteAutomatically, purpose, writeMode, populate }; // specific temporary folder
				}
			}
		}

		/// <summary>
		/// Tests creating an instance of the <see cref="FileBackedLogMessageCollection"/> class with a temporary backing log file.
		/// </summary>
		/// <param name="deleteAutomatically">
		/// <c>true</c> to delete the file automatically when the collection is disposed
		/// (or the next time, a temporary collection is created in the same directory);<br/>
		/// <c>false</c> to keep it after the collection is disposed.
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
		/// <c>true</c> to populate the collection with log messages as part of the creation step;<br/>
		/// otherwise <c>false</c>.
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

			LogFileMessage[] messages = populate ? mFixture.GetLogMessages_Random_10K() : null;
			using (var collection = FileBackedLogMessageCollection.CreateTemporaryCollection(deleteAutomatically, temporaryDirectoryPath, purpose, mode, messages))
			{
				Assert.True(File.Exists(collection.FilePath));
				Assert.Equal(effectiveTemporaryFolderPath, Path.GetDirectoryName(collection.FilePath));

				using (LogMessageCollectionEventWatcher eventWatcher = collection.AttachEventWatcher())
				{
					// check collection specific properties
					// ---------------------------------------------------------------------------------------------------------------
					Assert.Equal(20, collection.MaxCachePageCount);
					Assert.Equal(100, collection.CachePageCapacity);

					if (messages != null)
					{
						Assert.Equal(messages.Length, collection.Count);
						Assert.Equal(messages, collection);
					}
					else
					{
						Assert.Equal(0, collection.Count);
						Assert.Empty(collection);
					}


					// check log file specific properties
					// ---------------------------------------------------------------------------------------------------------------
					Assert.NotNull(collection.LogFile);
					Assert.NotNull(collection.FilePath);
					Assert.Equal(collection.LogFile.FilePath, collection.FilePath);

					// check properties exposed by IList implementation
					// ---------------------------------------------------------------------------------------------------------------
					{
						var list = (IList)collection;
						Assert.False(list.IsReadOnly);
						Assert.False(list.IsFixedSize);
						Assert.False(list.IsSynchronized);
						Assert.NotSame(collection, list.SyncRoot); // sync root must not be the same as the collection to avoid deadlocks

						if (messages != null)
						{
							Assert.Equal(messages.Length, list.Count);
							Assert.Equal(messages, list);
						}
						else
						{
							Assert.Equal(0, list.Count);
							Assert.Empty(collection);
						}
					}

					// check properties exposed by IList<T> implementation
					// ---------------------------------------------------------------------------------------------------------------
					{
						var list = (IList<LogMessage>)collection;
						Assert.False(list.IsReadOnly);
						if (messages != null)
						{
							Assert.Equal(messages.Length, list.Count);
							Assert.Equal(messages, list.Cast<LogFileMessage>());
						}
						else
						{
							Assert.Equal(0, list.Count);
							Assert.Empty(collection);
						}
					}

					// no events should have been raised
					eventWatcher.CheckInvocations();
				}

				backingFilePath = collection.FilePath;
			}

			// the file should not persist after disposing the collection, if auto-deletion is enabled
			Assert.Equal(deleteAutomatically, !File.Exists(backingFilePath));

			// delete the file, if it still exists to avoid polluting the output directory
			File.Delete(backingFilePath);
		}

		#endregion
	}

}
