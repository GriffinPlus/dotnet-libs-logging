///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="LogFile"/> class.
	/// </summary>
	[Collection("LogFileTests")]
	public class LogFileTests : IClassFixture<LogFileTestsFixture>
	{
		private static readonly LogFilePurpose[]    sLogFilePurposes   = { LogFilePurpose.Recording, LogFilePurpose.Analysis };
		private static readonly LogFileWriteMode[]  sLogFileWriteModes = { LogFileWriteMode.Robust, LogFileWriteMode.Fast };
		private readonly        LogFileTestsFixture mFixture;

		/// <summary>
		/// Initializes an instance of the <see cref="LogFileTests"/> class.
		/// </summary>
		/// <param name="fixture">Fixture providing static test data.</param>
		public LogFileTests(LogFileTestsFixture fixture)
		{
			mFixture = fixture;
		}

		#region GetSqliteVersion()

		/// <summary>
		/// Tests getting the <see cref="LogFile.SqliteVersion"/> property.
		/// </summary>
		/// <remarks>
		/// This test fails on linux due to different sqlite versions in the nuget package, see also
		/// https://system.data.sqlite.org/index.html/tktview/bc327ea1423cfd9c4fbe
		/// </remarks>
		[Fact]
		private void GetSqliteVersion()
		{
			Assert.Equal("3.32.1", LogFile.SqliteVersion);
		}

		#endregion

		#region Common Test Data

		/// <summary>
		/// Test data providing a mix of purposes.
		/// </summary>
		public static IEnumerable<object[]> PurposeMixTestData
		{
			get
			{
				foreach (var purpose in sLogFilePurposes)
				{
					yield return new object[] { purpose };
				}
			}
		}

		/// <summary>
		/// Test data providing a mix of write modes.
		/// </summary>
		public static IEnumerable<object[]> WriteModeMixTestData
		{
			get
			{
				foreach (var writeMode in sLogFileWriteModes)
				{
					yield return new object[] { writeMode };
				}
			}
		}

		/// <summary>
		/// Test data providing a mix of purposes and write modes.
		/// </summary>
		public static IEnumerable<object[]> PurposeWriteModeMixTestData
		{
			get
			{
				foreach (var purpose in sLogFilePurposes)
				foreach (var writeMode in sLogFileWriteModes)
				{
					yield return new object[] { purpose, writeMode };
				}
			}
		}

		/// <summary>
		/// Test data providing a mix of purposes and read-only flags.
		/// </summary>
		public static IEnumerable<object[]> PurposeReadOnlyMixTestData
		{
			get
			{
				foreach (var purpose in sLogFilePurposes)
				foreach (bool readOnly in new[] { false, true })
				{
					yield return new object[] { purpose, readOnly };
				}
			}
		}

		/// <summary>
		/// Test data providing a mix of purposes, write modes and read-only flags.
		/// </summary>
		public static IEnumerable<object[]> PurposeWriteModeReadOnlyMixTestData
		{
			get
			{
				foreach (var purpose in sLogFilePurposes)
				foreach (var writeMode in sLogFileWriteModes)
				foreach (bool readOnly in new[] { false, true })
				{
					yield return new object[] { purpose, writeMode, readOnly };
				}
			}
		}

		#endregion

		#region OpenOrCreate()

		/// <summary>
		/// Tests creating a new instance of the <see cref="LogFile"/> class with a new log file.
		/// The file is opened for reading and writing.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeMixTestData))]
		private void OpenOrCreate_NewFile(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			const string filename = "Test.gplog";
			string fullPath = Path.GetFullPath(filename);

			// ensure the log file does not exist
			File.Delete(fullPath);

			// create a new log file
			using (var file = LogFile.OpenOrCreate(filename, purpose, writeMode))
			{
				// the log file itself
				Assert.Equal(fullPath, file.FilePath);
				Assert.Equal(purpose, file.Purpose);
				Assert.Equal(writeMode, file.WriteMode);
				Assert.False(file.IsReadOnly);
				Assert.Equal(0, file.MessageCount);
				Assert.Equal(-1, file.OldestMessageId);
				Assert.Equal(-1, file.NewestMessageId);

				// the message collection working on top of the log file
				var collection = file.Messages;
				Assert.Same(file, collection.LogFile);
				Assert.Empty(collection);
				Assert.False(collection.IsReadOnly);
				Assert.Equal(0, collection.Count);
				Assert.Equal(20, collection.MaxCachePageCount);
				Assert.Equal(100, collection.CachePageCapacity);
				Assert.Equal(fullPath, collection.FilePath);
			}
		}

		/// <summary>
		/// Tests creating a new instance of the <see cref="LogFile"/> class with an existing log file.
		/// The file is opened for reading and writing.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeMixTestData))]
		private void OpenOrCreate_ExistingFile(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				using (var file = LogFile.OpenOrCreate(path, purpose, writeMode))
				{
					// the log file itself
					Assert.Equal(path, file.FilePath);
					Assert.Equal(purpose, file.Purpose);
					Assert.Equal(writeMode, file.WriteMode);
					Assert.False(file.IsReadOnly);
					Assert.Equal(10000, file.MessageCount);
					Assert.Equal(0, file.OldestMessageId);
					Assert.Equal(9999, file.NewestMessageId);

					// the message collection working on top of the log file
					var collection = file.Messages;
					Assert.Same(file, collection.LogFile);
					Assert.False(collection.IsReadOnly);
					Assert.Equal(10000, collection.Count);
					Assert.Equal(20, collection.MaxCachePageCount);
					Assert.Equal(100, collection.CachePageCapacity);
					Assert.Equal(path, collection.FilePath);
				}
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		#endregion

		#region Open()

		/// <summary>
		/// Tests creating a new instance of the <see cref="LogFile"/> class with an existing log file.
		/// The file is opened for reading and writing.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeMixTestData))]
		private void Open_Success(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				// open the existing log file
				using (var file = LogFile.Open(path, writeMode))
				{
					// the log file itself
					Assert.Equal(path, file.FilePath);
					Assert.Equal(purpose, file.Purpose);
					Assert.Equal(writeMode, file.WriteMode);
					Assert.False(file.IsReadOnly);
					Assert.Equal(10000, file.MessageCount);
					Assert.Equal(0, file.OldestMessageId);
					Assert.Equal(9999, file.NewestMessageId);

					// the message collection working on top of the log file
					var collection = file.Messages;
					Assert.Same(file, collection.LogFile);
					Assert.False(collection.IsReadOnly);
					Assert.Equal(10000, collection.Count);
					Assert.Equal(20, collection.MaxCachePageCount);
					Assert.Equal(100, collection.CachePageCapacity);
					Assert.Equal(path, collection.FilePath);
				}
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		/// <summary>
		/// Tests creating a new instance of the <see cref="LogFile"/> class with an existing log file.
		/// The file does not exist, so an exception should be thrown.
		/// </summary>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(WriteModeMixTestData))]
		private void Open_FileNotFound(LogFileWriteMode writeMode)
		{
			Assert.Throws<FileNotFoundException>(() => LogFile.Open("Not-Existent-File.gplog", writeMode));
		}

		#endregion

		#region OpenReadOnly()

		/// <summary>
		/// Tests creating a new instance of the <see cref="LogFile"/> class with an existing log file.
		/// The file is opened for reading only.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		[Theory]
		[MemberData(nameof(PurposeMixTestData))]
		private void OpenReadOnly_Success(LogFilePurpose purpose)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			var expectedMessages = mFixture.GetLogMessages_Random_10K();

			try
			{
				// open the existing log file
				using (var file = LogFile.OpenReadOnly(path))
				{
					// the log file itself
					Assert.Equal(path, file.FilePath);
					Assert.Equal(purpose, file.Purpose);
					Assert.Equal(LogFileWriteMode.Fast, file.WriteMode);
					Assert.True(file.IsReadOnly);
					Assert.Equal(10000, file.MessageCount);
					Assert.Equal(0, file.OldestMessageId);
					Assert.Equal(9999, file.NewestMessageId);

					// the message collection working on top of the log file
					var collection = file.Messages;
					Assert.Same(file, collection.LogFile);
					Assert.True(collection.IsReadOnly);
					Assert.Equal(10000, collection.Count);
					Assert.Equal(20, collection.MaxCachePageCount);
					Assert.Equal(100, collection.CachePageCapacity);
					Assert.Equal(path, collection.FilePath);
					Assert.Equal(expectedMessages, collection.ToArray());
				}
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		/// <summary>
		/// Tests creating a new instance of the <see cref="LogFile"/> class with an existing log file.
		/// The file does not exist, so an exception should be thrown.
		/// </summary>
		[Fact]
		private void OpenReadOnly_FileNotFound()
		{
			Assert.Throws<FileNotFoundException>(() => LogFile.OpenReadOnly("Not-Existent-File.gplog"));
		}

		#endregion

		#region GetLogWriterNames(), GetLogLevelNames(), GetProcessNames(), GetApplicationNames(), GetTags()

		/// <summary>
		/// Test data for methods that return names of log writers, log levels, processes, applications and tags.
		/// </summary>
		public static IEnumerable<object[]> GetNamesTestData
		{
			get
			{
				foreach (var purpose in sLogFilePurposes)
				foreach (var writeMode in sLogFileWriteModes)
				foreach (bool readOnly in new[] { false, true })
				{
					yield return new object[] { purpose, writeMode, readOnly, false };

					// usedOnly = true requires the file to be opened for reading and writing,
					// as the file is pruned to determine whether only used names are returned
					if (!readOnly) yield return new object[] { purpose, writeMode, false, true };
				}
			}
		}

		/// <summary>
		/// Tests for getting log writer names.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="readOnly">
		/// true to open the file in read-only mode;
		/// false to open the file for reading and writing.
		/// </param>
		/// <param name="usedOnly">
		/// true to test getting names that are referenced in the log file only;
		/// false to get all names.
		/// </param>
		[Theory]
		[MemberData(nameof(GetNamesTestData))]
		private void GetLogWriterNames(
			LogFilePurpose   purpose,
			LogFileWriteMode writeMode,
			bool             readOnly,
			bool             usedOnly)
		{
			CommonGetNames(
				purpose,
				writeMode,
				readOnly,
				usedOnly,
				message => new[] { message.LogWriterName },
				file => file.GetLogWriterNames(usedOnly));
		}

		/// <summary>
		/// Tests for getting log level names.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="readOnly">
		/// true to open the file in read-only mode;
		/// false to open the file for reading and writing.
		/// </param>
		/// <param name="usedOnly">
		/// true to test getting names that are referenced in the log file only;
		/// false to get all names.
		/// </param>
		[Theory]
		[MemberData(nameof(GetNamesTestData))]
		private void GetLogLevelNames(
			LogFilePurpose   purpose,
			LogFileWriteMode writeMode,
			bool             readOnly,
			bool             usedOnly)
		{
			CommonGetNames(
				purpose,
				writeMode,
				readOnly,
				usedOnly,
				message => new[] { message.LogLevelName },
				file => file.GetLogLevelNames(usedOnly));
		}

		/// <summary>
		/// Tests for getting process names.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="readOnly">
		/// true to open the file in read-only mode;
		/// false to open the file for reading and writing.
		/// </param>
		/// <param name="usedOnly">
		/// true to test getting names that are referenced in the log file only;
		/// false to get all names.
		/// </param>
		[Theory]
		[MemberData(nameof(GetNamesTestData))]
		private void GetProcessNames(
			LogFilePurpose   purpose,
			LogFileWriteMode writeMode,
			bool             readOnly,
			bool             usedOnly)
		{
			CommonGetNames(
				purpose,
				writeMode,
				readOnly,
				usedOnly,
				message => new[] { message.ProcessName },
				file => file.GetProcessNames(usedOnly));
		}

		/// <summary>
		/// Tests for getting all application names.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="readOnly">
		/// true to open the file in read-only mode;
		/// false to open the file for reading and writing.
		/// </param>
		/// <param name="usedOnly">
		/// true to test getting names that are referenced in the log file only;
		/// false to get all names.
		/// </param>
		[Theory]
		[MemberData(nameof(GetNamesTestData))]
		private void GetApplicationNames(
			LogFilePurpose   purpose,
			LogFileWriteMode writeMode,
			bool             readOnly,
			bool             usedOnly)
		{
			CommonGetNames(
				purpose,
				writeMode,
				readOnly,
				usedOnly,
				message => new[] { message.ApplicationName },
				file => file.GetApplicationNames(usedOnly));
		}

		/// <summary>
		/// Tests for getting tags.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="readOnly">
		/// true to open the file in read-only mode;
		/// false to open the file for reading and writing.
		/// </param>
		/// <param name="usedOnly">
		/// true to test getting tags that are referenced in the log file only;
		/// false to get all tags.
		/// </param>
		[Theory]
		[MemberData(nameof(GetNamesTestData))]
		private void GetTags(
			LogFilePurpose   purpose,
			LogFileWriteMode writeMode,
			bool             readOnly,
			bool             usedOnly)
		{
			CommonGetNames(
				purpose,
				writeMode,
				readOnly,
				usedOnly,
				message => message.Tags,
				file => file.GetTags(usedOnly));
		}

		/// <summary>
		/// Helper method for other test methods that check for lists of strings returned by the log file.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="readOnly">
		/// true to open the file in read-only mode;
		/// false to open the file for reading and writing.
		/// </param>
		/// <param name="usedOnly">
		/// true to test getting names that are referenced in the log file only;
		/// false to get all names.
		/// </param>
		/// <param name="selector">Selects a string property value out of a log message.</param>
		/// <param name="action">Action to perform on the log file, returns the strings to compare with the list of selected strings.</param>
		private void CommonGetNames(
			LogFilePurpose                        purpose,
			LogFileWriteMode                      writeMode,
			bool                                  readOnly,
			bool                                  usedOnly,
			Func<LogMessage, IEnumerable<string>> selector,
			Func<LogFile, string[]>               action)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				// get test data set with log messages that should be in the file
				var expectedMessages = mFixture.GetLogMessages_Random_10K();

				// query the log file and check whether it returns the expected names
				using (var file = readOnly ? LogFile.OpenReadOnly(path) : LogFile.Open(path, writeMode))
				{
					Assert.Equal(readOnly, file.IsReadOnly);

					if (usedOnly)
					{
						// testing to return names that are actually referenced by messages
						// => clear out all except 1 message
						// => the remaining message should then define the returned names
						file.Prune(1, DateTime.MinValue, false);
						Assert.Equal(1, file.MessageCount);
						expectedMessages = new[] { expectedMessages[expectedMessages.Length - 1] };
					}

					// collect name(s) from message properties and build the set of expected names
					var expectedNamesSet = new HashSet<string>();
					foreach (var message in expectedMessages) expectedNamesSet.UnionWith(selector(message));
					var expectedNames = new List<string>(expectedNamesSet);
					expectedNames.Sort(); // the list returned by the log file is expected to be sorted ascendingly

					// perform the action to test on the log file and compare with the expected result
					string[] names = action(file);
					Assert.Equal(expectedNames, names);
				}
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		#endregion

		#region Write() - Single Message

		/// <summary>
		/// Tests writing a single message to a new log file and reading the message back.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeMixTestData))]
		private void WriteFollowedByRead_SingleMessage_Success(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			const string filename = "Test.gplog";
			string fullPath = Path.GetFullPath(filename);

			// ensure the log file does not exist
			File.Delete(fullPath);

			// generate a message to write into the file
			var message = LoggingTestHelpers.GetTestMessages(1)[0];

			// create a new log file
			using (var file = LogFile.OpenOrCreate(filename, purpose, writeMode))
			{
				// the state of an empty log file is already tested in CreateEmptyFile()
				// => nothing to do here...

				// write the message
				file.Write(message);

				// the file should contain the written message only now
				Assert.Equal(1, file.MessageCount);
				Assert.Equal(0, file.OldestMessageId);
				Assert.Equal(0, file.NewestMessageId);
				var readMessages = file.Read(0, 100);
				Assert.Single(readMessages);

				// the message returned by the log file should be the same as the inserted message
				// (except the message id that is set by the log file and the read-only state)
				Assert.Equal(0, readMessages[0].Id);
				message.Id = readMessages[0].Id;
				Assert.Equal(message, readMessages[0]); // does not take IsReadOnly into account
				Assert.True(readMessages[0].IsReadOnly);

				// the wrapping collection should also reflect the change
				var collection = file.Messages;
				Assert.Equal(1, collection.Count);
				Assert.Single(collection);

				// the message returned by the collection should be the same as the inserted message
				// (except the message id that is set by the log file, message id was adjusted above)
				Assert.Equal(message, collection[0]); // does not take IsReadOnly into account
				Assert.True(collection[0].IsReadOnly);
			}
		}

		/// <summary>
		/// Tests writing a single message to an existing log file.
		/// The file is opened read-only, so writing should throw an exception.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		[Theory]
		[MemberData(nameof(PurposeMixTestData))]
		private void WriteFollowedByRead_SingleMessage_ReadOnly(LogFilePurpose purpose)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			// generate a message to write into the file
			var message = LoggingTestHelpers.GetTestMessages(1)[0];

			try
			{
				using (var file = LogFile.OpenReadOnly(path))
				{
					Assert.Throws<NotSupportedException>(() => file.Write(message));
				}
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		#endregion

		#region Write() - Multiple Messages

		/// <summary>
		/// Tests writing multiple messages to a new file and reading the messages back.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeMixTestData))]
		private void WriteFollowedByRead_MultipleMessages_Success(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			const string filename = "Test.gplog";
			string fullPath = Path.GetFullPath(filename);

			// ensure the log file does not exist
			File.Delete(fullPath);

			// generate messages to write into the file
			var messages = mFixture.GetLogMessages_Random_10K();

			// create a new log file
			using (var file = LogFile.OpenOrCreate(filename, purpose, writeMode))
			{
				// the state of an empty log file is already tested in CreateEmptyFile()
				// => nothing to do here...

				// write the message
				file.Write(messages);

				// the file should now contain the written messages
				var readMessages = file.Read(0, messages.Length + 1);
				Assert.Equal(messages.Length, file.MessageCount);
				Assert.Equal(0, file.OldestMessageId);
				Assert.Equal(messages.Length - 1, file.NewestMessageId);
				Assert.Equal(messages.Length, readMessages.Length);

				// the messages returned by the log file should be the same as the inserted message
				// (except the message id that is set by the log file and the read-only state)
				long expectedId = 0;
				for (int i = 0; i < messages.Length; i++)
				{
					Assert.Equal(expectedId++, readMessages[i].Id);
					messages[i].Id = readMessages[i].Id;
					Assert.Equal(messages[i], readMessages[i]); // does not take IsReadOnly into account
					Assert.True(readMessages[i].IsReadOnly);
				}

				// the wrapping collection should also reflect the change
				var collection = file.Messages;
				Assert.Equal(messages.Length, collection.Count);

				// the messages returned by the collection should be the same as the inserted messages
				// (except the message id that is set by the log file, message id was adjusted above)
				Assert.Equal(messages, collection.ToArray()); // does not take IsReadOnly into account
				Assert.All(collection, message => Assert.True(message.IsReadOnly));
			}
		}

		/// <summary>
		/// Tests writing multiple messages to an existing log file.
		/// The file is opened read-only, so writing should throw an exception.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		[Theory]
		[MemberData(nameof(PurposeMixTestData))]
		private void WriteFollowedByRead_MultipleMessages_ReadOnly(LogFilePurpose purpose)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			// generate messages to write into the file
			var messages = mFixture.GetLogMessages_Random_10K();

			try
			{
				using (var file = LogFile.OpenReadOnly(path))
				{
					Assert.Throws<NotSupportedException>(() => file.Write(messages));
				}
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		#endregion

		#region Read()

		/// <summary>
		/// Tests reading log messages in chunks from the log file returning an array of log messages at the end.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="readOnly">
		/// true to open the file for reading only;
		/// false to open the file for reading and writing.
		/// </param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeReadOnlyMixTestData))]
		private void Read_ReturnMessages(LogFilePurpose purpose, LogFileWriteMode writeMode, bool readOnly)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				var expectedMessages = mFixture.GetLogMessages_Random_10K();

				int totalMessageCount = expectedMessages.Length;
				var readMessages = new List<LogMessage>();
				using (var file = readOnly ? LogFile.OpenReadOnly(path) : LogFile.Open(path, writeMode))
				{
					// check initial status
					Assert.Equal(readOnly, file.IsReadOnly);
					Assert.Equal(totalMessageCount, file.MessageCount);
					Assert.Equal(0, file.OldestMessageId);
					Assert.Equal(totalMessageCount - 1, file.NewestMessageId);

					// read messages in chunks of 999, so the last chunk does not return a full set
					// (covers reading full set and partial set)
					const int chunkSize = 999;
					for (int readMessageCount = 0; readMessageCount < totalMessageCount;)
					{
						var messages = file.Read(file.OldestMessageId + readMessageCount, chunkSize);

						int expectedReadCount = Math.Min(chunkSize, totalMessageCount - readMessageCount);
						Assert.Equal(expectedReadCount, messages.Length);
						readMessages.AddRange(messages);
						readMessageCount += messages.Length;
					}
				}

				// the list of read messages should now equal the original test data set
				Assert.Equal(expectedMessages.Length, readMessages.Count);
				Assert.Equal(expectedMessages, readMessages.ToArray()); // does not take IsReadOnly into account
				Assert.All(readMessages, message => Assert.True(message.IsReadOnly));
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		/// <summary>
		/// Test data providing a mix of purpose and write modes.
		/// </summary>
		public static IEnumerable<object[]> Read_WithCallbackTestData
		{
			get
			{
				foreach (var purpose in sLogFilePurposes)
				foreach (var writeMode in sLogFileWriteModes)
				foreach (bool readOnly in new[] { false, true })
				foreach (bool cancelReading in new[] { false, true })
				{
					yield return new object[] { purpose, writeMode, readOnly, cancelReading };
				}
			}
		}

		/// <summary>
		/// Tests reading log messages in chunks from the log file invoking a callback for each log message.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="readOnly">
		/// true to open the file for reading only;
		/// false to open the file for reading and writing.
		/// </param>
		/// <param name="cancelReading">
		/// true to cancel reading at half of the expected log messages;
		/// false to read to the end.
		/// </param>
		[Theory]
		[MemberData(nameof(Read_WithCallbackTestData))]
		private void Read_PassMessagesToCallback(
			LogFilePurpose   purpose,
			LogFileWriteMode writeMode,
			bool             readOnly,
			bool             cancelReading)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				// get expected result set
				// (when cancellation is requested, it is done after half the log messages)
				var allMessages = mFixture.GetLogMessages_Random_10K();
				var expectedMessages = cancelReading ? allMessages.Take(5000).ToArray() : allMessages;
				int totalMessageCount = allMessages.Length;
				int expectedMessageCount = expectedMessages.Length;

				var readMessages = new List<LogMessage>();
				using (var file = readOnly ? LogFile.OpenReadOnly(path) : LogFile.Open(path, writeMode))
				{
					// check initial status
					Assert.Equal(readOnly, file.IsReadOnly);
					Assert.Equal(totalMessageCount, file.MessageCount);
					Assert.Equal(0, file.OldestMessageId);
					Assert.Equal(totalMessageCount - 1, file.NewestMessageId);

					// read messages in chunks of 999, so the last chunk does not return a full set
					// (covers reading full set and partial set)
					const int chunkSize = 999;
					for (int readMessageCount = 0; readMessageCount < totalMessageCount;)
					{
						int readMessagesInStep = 0;

						bool ReadCallback(LogMessage message)
						{
							readMessages.Add(message);
							readMessagesInStep++;

							// if testing cancellation, cancel after half the number of log messages,
							// otherwise proceed up to the end
							if (cancelReading) return readMessages.Count < expectedMessageCount;
							return true;
						}

						bool proceed = file.Read(file.OldestMessageId + readMessageCount, chunkSize, ReadCallback);

						int expectedReadCount = Math.Min(chunkSize, expectedMessageCount - readMessageCount);
						Assert.Equal(expectedReadCount, readMessagesInStep);
						readMessageCount += readMessagesInStep;

						if (!proceed) break;
					}
				}

				// the list of read messages should now equal the original test data set
				Assert.Equal(expectedMessages.Length, readMessages.Count);
				Assert.Equal(expectedMessages, readMessages.ToArray()); // does not take IsReadOnly into account
				Assert.All(readMessages, message => Assert.True(message.IsReadOnly));
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		#endregion

		#region Clear()

		/// <summary>
		/// Test data providing a mix of purpose and write modes.
		/// </summary>
		public static IEnumerable<object[]> ClearTestData_Success
		{
			get
			{
				foreach (var purpose in sLogFilePurposes)
				foreach (var writeMode in sLogFileWriteModes)
				foreach (bool messagesOnly in new[] { false, true })
				foreach (bool compact in new[] { false, true })
				{
					yield return new object[] { purpose, writeMode, messagesOnly, compact };
				}
			}
		}

		/// <summary>
		/// Tests clearing an existing log file.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="messagesOnly">
		/// true to remove messages only;
		/// false to remove processes, applications, log writers, log levels and tags as well.
		/// </param>
		/// <param name="compact">
		/// true to compact the log file after clearing (default);
		/// false to clear the log file, but do not compact it.
		/// </param>
		[Theory]
		[MemberData(nameof(ClearTestData_Success))]
		private void Clear_Success(
			LogFilePurpose   purpose,
			LogFileWriteMode writeMode,
			bool             messagesOnly,
			bool             compact)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				// get the initial size of the log file
				long fileSizeAtStart = new FileInfo(path).Length;

				// get test data set with log messages that should be in the file
				var expectedMessages = mFixture.GetLogMessages_Random_10K();

				int totalMessageCount = expectedMessages.Length;
				using (var file = LogFile.Open(path, writeMode))
				{
					// check initial status of the file
					Assert.Equal(totalMessageCount, file.MessageCount);
					Assert.Equal(0, file.OldestMessageId);
					Assert.Equal(totalMessageCount - 1, file.NewestMessageId);

					// retrieve the lists of log writers, levels, process names etc.
					string[] logWriterNames = file.GetLogWriterNames(false);
					string[] logLevelNames = file.GetLogLevelNames(false);
					string[] processNames = file.GetProcessNames(false);
					string[] applicationNames = file.GetApplicationNames(false);
					string[] tags = file.GetTags(false);

					// clear log file
					file.Clear(messagesOnly, compact);

					// the file should not contain any messages now
					Assert.Equal(0, file.MessageCount);
					Assert.Equal(-1, file.OldestMessageId);
					Assert.Equal(-1, file.NewestMessageId);

					// the collection should reflect the change as well
					Assert.Equal(0, file.Messages.Count);
					Assert.Empty(file.Messages);

					if (messagesOnly)
					{
						// clearing should not have touched any non-message data
						Assert.Equal(logWriterNames, file.GetLogWriterNames(false));
						Assert.Equal(logLevelNames, file.GetLogLevelNames(false));
						Assert.Equal(processNames, file.GetProcessNames(false));
						Assert.Equal(applicationNames, file.GetApplicationNames(false));
						Assert.Equal(tags, file.GetTags(false));
					}
					else
					{
						// clearing should not have cleared non-message data as well
						Assert.Empty(file.GetLogWriterNames(false));
						Assert.Empty(file.GetLogLevelNames(false));
						Assert.Empty(file.GetProcessNames(false));
						Assert.Empty(file.GetApplicationNames(false));
						Assert.Empty(file.GetTags(false));
					}
				}

				// if compacting is requested, the file should be smaller now
				long fileSizeAtEnd = new FileInfo(path).Length;
				if (compact) Assert.True(fileSizeAtEnd < fileSizeAtStart);
				else Assert.Equal(fileSizeAtStart, fileSizeAtEnd);
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		/// <summary>
		/// Test data providing a mix of purpose and write modes.
		/// </summary>
		public static IEnumerable<object[]> ClearTestData_ReadOnly
		{
			get
			{
				foreach (var purpose in sLogFilePurposes)
				foreach (bool messagesOnly in new[] { false, true })
				foreach (bool compact in new[] { false, true })
				{
					yield return new object[] { purpose, messagesOnly, compact };
				}
			}
		}

		/// <summary>
		/// Tests clearing an existing log file.
		/// The log file is read-only and should throw an exception.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="messagesOnly">
		/// true to remove messages only;
		/// false to remove processes, applications, log writers, log levels and tags as well.
		/// </param>
		/// <param name="compact">
		/// true to compact the log file after clearing (default);
		/// false to clear the log file, but do not compact it.
		/// </param>
		[Theory]
		[MemberData(nameof(ClearTestData_ReadOnly))]
		private void Clear_ReadOnly(
			LogFilePurpose purpose,
			bool           messagesOnly,
			bool           compact)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				using (var file = LogFile.OpenReadOnly(path))
				{
					Assert.True(file.IsReadOnly);
					Assert.Throws<NotSupportedException>(() => file.Clear(messagesOnly, compact));
				}
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		#endregion

		#region Prune()

		/// <summary>
		/// Test data providing a mix of purpose and write modes.
		/// </summary>
		public static IEnumerable<object[]> PruneTestData
		{
			get
			{
				foreach (var purpose in sLogFilePurposes)
				foreach (var writeMode in sLogFileWriteModes)
				foreach (bool readOnly in new[] { false, true })
				foreach (bool compact in new[] { false, true })
				{
					yield return new object[] { purpose, writeMode, readOnly, compact, -1, -1 };     // do not discard anything
					yield return new object[] { purpose, writeMode, readOnly, compact, 10000, -1 };  // discard by message count limit, but the file does not contain more than that messages
					yield return new object[] { purpose, writeMode, readOnly, compact, 9999, -1 };   // discard the oldest message by message count
					yield return new object[] { purpose, writeMode, readOnly, compact, 5000, -1 };   // discard half of the messages by message count
					yield return new object[] { purpose, writeMode, readOnly, compact, 2, -1 };      // discard all but the newest two messages by message count
					yield return new object[] { purpose, writeMode, readOnly, compact, 1, -1 };      // discard all but the newest message by message count
					yield return new object[] { purpose, writeMode, readOnly, compact, 0, -1 };      // discard all messages by message count
					yield return new object[] { purpose, writeMode, readOnly, compact, -1, 0 };      // do not discard anything
					yield return new object[] { purpose, writeMode, readOnly, compact, -1, 1 };      // discard the oldest message by timestamp
					yield return new object[] { purpose, writeMode, readOnly, compact, -1, 2 };      // discard the oldest two messages by timestamp
					yield return new object[] { purpose, writeMode, readOnly, compact, -1, 5000 };   // discard half of the messages by timestamp
					yield return new object[] { purpose, writeMode, readOnly, compact, -1, 9999 };   // discard all messages but the newest message by timestamp
					yield return new object[] { purpose, writeMode, readOnly, compact, -1, 10000 };  // discard all messages by timestamp
					yield return new object[] { purpose, writeMode, readOnly, compact, 5000, 4999 }; // discard half of the messages (by message count discards one more than by timestamp)
					yield return new object[] { purpose, writeMode, readOnly, compact, 4999, 5000 }; // discard half of the messages (by timestamp discards one more than by message count)
				}
			}
		}

		/// <summary>
		/// Tests pruning an existing log file.
		/// Covers the case of success and failing due to the file being opened read-only.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="readOnly">true to open the file read-only; false to open the file read/write.</param>
		/// <param name="compact">true to compact the log file; otherwise false.</param>
		/// <param name="maximumMessageCount">Number of messages to keep in the log file.</param>
		/// <param name="pruneByTimestampCount">Number of old messages to remove by timestamp.</param>
		[Theory]
		[MemberData(nameof(PruneTestData))]
		private void Prune(
			LogFilePurpose   purpose,
			LogFileWriteMode writeMode,
			bool             readOnly,
			bool             compact,
			int              maximumMessageCount,
			int              pruneByTimestampCount)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				// get the initial size of the log file
				long fileSizeAtStart = new FileInfo(path).Length;

				// get test data set with log messages that should be in the file
				var expectedMessages = mFixture.GetLogMessages_Random_10K();

				int totalMessageCount = expectedMessages.Length;
				int pruneTotalCount;
				using (var file = readOnly ? LogFile.OpenReadOnly(path) : LogFile.Open(path, writeMode))
				{
					// check initial status of the file
					Assert.Equal(readOnly, file.IsReadOnly);
					Assert.Equal(totalMessageCount, file.MessageCount);
					Assert.Equal(0, file.OldestMessageId);
					Assert.Equal(totalMessageCount - 1, file.NewestMessageId);

					// generate the appropriate prune timestamp
					var pruneTimestamp = DateTime.MinValue;
					pruneTotalCount = maximumMessageCount >= 0 ? Math.Min(expectedMessages.Length, expectedMessages.Length - maximumMessageCount) : 0;
					if (pruneByTimestampCount > 0)
					{
						if (pruneByTimestampCount >= expectedMessages.Length)
						{
							var newestMessage = expectedMessages[expectedMessages.Length - 1];
							pruneTimestamp = newestMessage.Timestamp.UtcDateTime + new TimeSpan(1);
							pruneTotalCount = Math.Max(pruneTotalCount, expectedMessages.Length);
						}
						else
						{
							var firstMessageToKeep = expectedMessages[pruneByTimestampCount];
							pruneTimestamp = firstMessageToKeep.Timestamp.UtcDateTime;
							pruneTotalCount = Math.Max(pruneTotalCount, pruneByTimestampCount);
						}
					}

					if (readOnly)
					{
						// try pruning log file
						Assert.Throws<NotSupportedException>(() => file.Prune(maximumMessageCount, pruneTimestamp, compact));
					}
					else
					{
						// prune log file
						file.Prune(maximumMessageCount, pruneTimestamp, compact);

						// determine the expected number of remaining messages
						int expectedMessageCount = expectedMessages.Length;
						if (maximumMessageCount >= 0) expectedMessageCount = Math.Min(expectedMessageCount, maximumMessageCount);
						if (pruneByTimestampCount >= 0) expectedMessageCount = Math.Min(expectedMessageCount, expectedMessages.Length - pruneByTimestampCount);

						// check the number of messages in the file, the ids of the oldest/newest message
						// and the remaining messages in the file
						int newOldestMessageId = pruneTotalCount; // the id of the oldest message was 0, so the prune count is the id of the now oldest message
						Assert.Equal(expectedMessageCount, file.MessageCount);
						if (expectedMessageCount > 0)
						{
							Assert.Equal(totalMessageCount - expectedMessageCount, file.OldestMessageId);
							Assert.Equal(totalMessageCount - 1, file.NewestMessageId);
							var readMessages = file.Read(newOldestMessageId, expectedMessageCount + 1);
							Assert.Equal(expectedMessageCount, readMessages.Length);
							Assert.Equal(
								expectedMessages.Skip(expectedMessages.Length - expectedMessageCount).ToArray(),
								readMessages); // does not take IsReadOnly into account
							Assert.All(readMessages, message => Assert.True(message.IsReadOnly));
						}
						else
						{
							Assert.Equal(-1, file.OldestMessageId);
							Assert.Equal(-1, file.NewestMessageId);
						}

						// the collection should reflect the change as well
						if (expectedMessageCount > 0)
						{
							Assert.Equal(expectedMessageCount, file.Messages.Count);
							Assert.Equal(
								expectedMessages.Skip(expectedMessages.Length - expectedMessageCount).ToArray(),
								file.Messages.ToArray()); // does not take IsReadOnly into account
							Assert.All(file.Messages, message => Assert.True(message.IsReadOnly));
						}
						else
						{
							Assert.Equal(0, file.Messages.Count);
							Assert.Empty(file.Messages);
						}
					}
				}

				if (!readOnly)
				{
					// the file should be smaller now as the database is vacuum'ed after pruning
					// (this test is a bit tricky as a database page contains multiple records and removing only few records does not guarantee that a page is removed)
					if (compact && pruneTotalCount > 10)
					{
						long fileSizeAtEnd = new FileInfo(path).Length;
						Assert.True(fileSizeAtEnd < fileSizeAtStart);
					}
				}
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		#endregion

		#region Compact()

		/// <summary>
		/// Tests compacting a log file after clearing.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeMixTestData))]
		private void Compact_Success(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				// get the initial size of the log file
				long fileSizeAtStart = new FileInfo(path).Length;

				// get test data set with log messages that should be in the file
				var expectedMessages = mFixture.GetLogMessages_Random_10K();

				int totalMessageCount = expectedMessages.Length;
				using (var file = LogFile.Open(path, writeMode))
				{
					// check initial status of the file
					Assert.Equal(totalMessageCount, file.MessageCount);
					Assert.Equal(0, file.OldestMessageId);
					Assert.Equal(totalMessageCount - 1, file.NewestMessageId);

					// remove all messages from the log file, but do not compact the file afterwards
					file.Clear(false, false);
					Assert.Equal(0, file.MessageCount);
				}

				// the log file must be closed as robust write mode lets its sqlite database run in WAL mode
				// => closing the database file merges the WAL into the database file creating the final file
				//    (otherwise the file keeps its initial size)

				// the log file should still have the same size
				long fileSizeAtClearing = new FileInfo(path).Length;
				Assert.Equal(fileSizeAtStart, fileSizeAtClearing);

				// compact the log file
				using (var file = LogFile.Open(path, writeMode))
				{
					file.Compact();
				}

				// the file should be smaller now
				long fileSizeAtEnd = new FileInfo(path).Length;
				Assert.True(fileSizeAtEnd < fileSizeAtStart);
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		/// <summary>
		/// Tests compacting a log file after clearing.
		/// The file is read-only and should throw an exception.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		[Theory]
		[MemberData(nameof(PurposeMixTestData))]
		private void Compact_ReadOnly(LogFilePurpose purpose)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				using (var file = LogFile.OpenReadOnly(path))
				{
					Assert.True(file.IsReadOnly);
					Assert.Throws<NotSupportedException>(() => file.Compact());
				}
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		#endregion

		#region SaveSnapshot()

		/// <summary>
		/// Tests saving a snapshot of an opened log file.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="readOnly">true to open the file read-only; false to open the file read/write.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeReadOnlyMixTestData))]
		private void SaveSnapshot(LogFilePurpose purpose, LogFileWriteMode writeMode, bool readOnly)
		{
			string logFilePath = purpose == LogFilePurpose.Recording
				                     ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				                     : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();
			string snapshotFilePath = logFilePath + ".snapshot";

			try
			{
				// get the initial size of the log file
				long logFileSizeAtStart = new FileInfo(logFilePath).Length;

				// get test data set with all log messages
				var allMessages = mFixture.GetLogMessages_Random_10K();

				// prune half of the messages, so taking a snapshot should return in a smaller file
				int totalMessageCount = allMessages.Length;
				using (var file = LogFile.Open(logFilePath, writeMode))
				{
					// check initial status of the file
					Assert.Equal(totalMessageCount, file.MessageCount);
					Assert.Equal(0, file.OldestMessageId);
					Assert.Equal(totalMessageCount - 1, file.NewestMessageId);

					// remove half of the messages from the log file, but do not compact the file afterwards
					file.Prune(5000, DateTime.MinValue, false);
					Assert.Equal(5000, file.MessageCount);
				}

				// the log file must be closed as robust write mode lets its sqlite database run in WAL mode
				// => closing the database file merges the WAL into the database file creating the final file
				//    (otherwise the file keeps its initial size)

				// the log file should still have the same size
				long fileSizeAtPruning = new FileInfo(logFilePath).Length;
				Assert.Equal(logFileSizeAtStart, fileSizeAtPruning);

				// save a snapshot of the log file
				using (var file = readOnly ? LogFile.OpenReadOnly(logFilePath) : LogFile.Open(logFilePath, writeMode))
				{
					file.SaveSnapshot(snapshotFilePath);
				}

				// the snapshot file should be smaller than the original file as it is compacted
				// on the fly when taking a snapshot
				long snapshotFileSize = new FileInfo(snapshotFilePath).Length;
				Assert.True(snapshotFileSize < logFileSizeAtStart);

				// the snapshot should now contain the 5000 newest messages
				var expectedMessages = allMessages.Skip(5000).ToArray();
				using (var file = LogFile.OpenReadOnly(snapshotFilePath))
				{
					Assert.Equal(5000, file.OldestMessageId);
					Assert.Equal(9999, file.NewestMessageId);
					Assert.Equal(5000, file.MessageCount);
					var readMessages = file.Read(file.OldestMessageId, (int)file.MessageCount + 1); // +1 to check for no more than the expected messages
					Assert.Equal(file.MessageCount, readMessages.Length);
					Assert.Equal(expectedMessages, readMessages); // does not take IsReadOnly into account
					Assert.All(readMessages, message => Assert.True(message.IsReadOnly));
				}
			}
			finally
			{
				// remove temporary log files to avoid polluting the output directory
				File.Delete(logFilePath);
				File.Delete(snapshotFilePath);
			}
		}

		#endregion
	}

}
