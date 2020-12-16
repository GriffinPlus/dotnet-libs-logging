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
	/// Unit tests targeting the <see cref="LogFile" /> class.
	/// </summary>
	public class LogFileTests : IClassFixture<LogFileTestsFixture>
	{
		private readonly LogFileTestsFixture mFixture;

		/// <summary>
		/// Initializes an instance of the <see cref="LogFileTests" /> class.
		/// </summary>
		/// <param name="fixture">Fixture providing static test data.</param>
		public LogFileTests(LogFileTestsFixture fixture)
		{
			mFixture = fixture;
		}

		#region GetSqliteVersion()

		/// <summary>
		/// Tests getting the <see cref="LogFile.SqliteVersion" /> property.
		/// </summary>
		/// <remarks>
		/// This test fails on linux due to different sqlite versions in the nuget package, see also
		/// https://system.data.sqlite.org/index.html/tktview/bc327ea1423cfd9c4fbe
		/// </remarks>
		[Fact]
		private void GetSqliteVersion()
		{
			// TODO: Replace nuget package with fixed one and enable test again.
			// Assert.Equal("3.32.1", LogFile.SqliteVersion);
		}

		#endregion

		#region Construction

		/// <summary>
		/// Test data providing a mix of purpose and write modes.
		/// </summary>
		public static IEnumerable<object[]> PurposeWriteModeMixTestData
		{
			get
			{
				foreach (LogFilePurpose purpose in Enum.GetValues(typeof(LogFilePurpose)))
				foreach (LogFileWriteMode writeMode in Enum.GetValues(typeof(LogFileWriteMode)))
				{
					yield return new object[] { purpose, writeMode };
				}
			}
		}

		/// <summary>
		/// Tests creating a new instance of the <see cref="LogFile" /> class.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeMixTestData))]
		private void CreateEmptyFile(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			const string filename = "Test.gplog";
			string fullPath = Path.GetFullPath(filename);

			// ensure the log file does not exist
			File.Delete(fullPath);

			// create a new log file
			using (var file = new LogFile(filename, purpose, writeMode))
			{
				// the log file itself
				Assert.Equal(fullPath, file.FilePath);
				Assert.Equal(purpose, file.Purpose);
				Assert.Equal(writeMode, file.WriteMode);
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

		#endregion

		#region GetLogWriterNames(), GetLogLevelNames(), GetProcessNames(), GetApplicationNames()

		/// <summary>
		/// Test data for methods that return names of log writers, log levels, processes, applications and tags.
		/// </summary>
		public static IEnumerable<object[]> GetNamesTestData
		{
			get
			{
				foreach (LogFilePurpose purpose in Enum.GetValues(typeof(LogFilePurpose)))
				foreach (LogFileWriteMode writeMode in Enum.GetValues(typeof(LogFileWriteMode)))
				foreach (bool usedOnly in new[] { false, true })
				{
					yield return new object[] { purpose, writeMode, usedOnly };
				}
			}
		}

		/// <summary>
		/// Tests for getting log writer names.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="usedOnly">
		/// true to test getting names that are referenced in the log file only;
		/// false to get all names.
		/// </param>
		[Theory]
		[MemberData(nameof(GetNamesTestData))]
		private void GetLogWriterNames(LogFilePurpose purpose, LogFileWriteMode writeMode, bool usedOnly)
		{
			CommonGetNames(
				purpose,
				writeMode,
				usedOnly,
				message => new[] { message.LogWriterName },
				file => file.GetLogWriterNames(usedOnly));
		}

		/// <summary>
		/// Tests for getting log level names.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="usedOnly">
		/// true to test getting names that are referenced in the log file only;
		/// false to get all names.
		/// </param>
		[Theory]
		[MemberData(nameof(GetNamesTestData))]
		private void GetLogLevelNames(LogFilePurpose purpose, LogFileWriteMode writeMode, bool usedOnly)
		{
			CommonGetNames(
				purpose,
				writeMode,
				usedOnly,
				message => new[] { message.LogLevelName },
				file => file.GetLogLevelNames(usedOnly));
		}

		/// <summary>
		/// Tests for getting process names.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="usedOnly">
		/// true to test getting names that are referenced in the log file only;
		/// false to get all names.
		/// </param>
		[Theory]
		[MemberData(nameof(GetNamesTestData))]
		private void GetProcessNames(LogFilePurpose purpose, LogFileWriteMode writeMode, bool usedOnly)
		{
			CommonGetNames(
				purpose,
				writeMode,
				usedOnly,
				message => new[] { message.ProcessName },
				file => file.GetProcessNames(usedOnly));
		}

		/// <summary>
		/// Tests for getting all application names.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="usedOnly">
		/// true to test getting names that are referenced in the log file only;
		/// false to get all names.
		/// </param>
		[Theory]
		[MemberData(nameof(GetNamesTestData))]
		private void GetApplicationNames(LogFilePurpose purpose, LogFileWriteMode writeMode, bool usedOnly)
		{
			CommonGetNames(
				purpose,
				writeMode,
				usedOnly,
				message => new[] { message.ApplicationName },
				file => file.GetApplicationNames(usedOnly));
		}

		/// <summary>
		/// Helper method for other test methods that check for lists of strings returned by the log file.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="usedOnly">
		/// true to test getting names that are referenced in the log file only;
		/// false to get all names.
		/// </param>
		/// <param name="selector">Selects a string property value out of a log message.</param>
		/// <param name="action">Action to perform on the log file, returns the strings to compare with the list of selected strings.</param>
		private void CommonGetNames(
			LogFilePurpose purpose,
			LogFileWriteMode writeMode,
			bool usedOnly,
			Func<LogMessage, IEnumerable<string>> selector,
			Func<LogFile, string[]> action)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				// get test data set with log messages that should be in the file
				var expectedMessages = mFixture.GetLogMessages_Random_10K();

				// query the log file and check whether it returns the expected names
				using (var file = new LogFile(path, purpose, writeMode))
				{
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
					HashSet<string> expectedNamesSet = new HashSet<string>();
					foreach (var message in expectedMessages) expectedNamesSet.UnionWith(selector(message));
					var expectedNames = new List<string>(expectedNamesSet);
					expectedNames.Sort(); // the list returned by the log file is expected to be sorted ascendingly

					// perform the action to test on the log file and compare with the expected result
					var names = action(file);
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

		#region Write()

		/// <summary>
		/// Tests writing a single message to an empty log file and reading the message back.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeMixTestData))]
		private void WriteFollowedByRead_SingleMessage(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			const string filename = "Test.gplog";
			string fullPath = Path.GetFullPath(filename);

			// ensure the log file does not exist
			File.Delete(fullPath);

			// generate a message to write into the file
			var message = LogFileTestsFixture.GetTestMessages(1)[0];

			// create a new log file
			using (var file = new LogFile(filename, purpose, writeMode))
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
				// (except the message id that is set by the log file)
				Assert.Equal(0, readMessages[0].Id);
				message.Id = readMessages[0].Id;
				Assert.Equal(message, readMessages[0]);

				// the wrapping collection should also reflect the change
				var collection = file.Messages;
				Assert.Equal(1, collection.Count);
				Assert.Single(collection);

				// the message returned by the collection should be the same as the inserted message
				// (except the message id that is set by the log file, message id was adjusted above)
				Assert.Equal(message, collection[0]);
			}
		}

		/// <summary>
		/// Tests writing multiple messages to an empty log file and reading the messages back.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeMixTestData))]
		private void WriteFollowedByRead_MultipleMessages(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			const string filename = "Test.gplog";
			string fullPath = Path.GetFullPath(filename);

			// ensure the log file does not exist
			File.Delete(fullPath);

			// generate messages to write into the file
			var messages = mFixture.GetLogMessages_Random_10K();

			// create a new log file
			using (var file = new LogFile(filename, purpose, writeMode))
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
				// (except the message id that is set by the log file)
				long expectedId = 0;
				for (int i = 0; i < messages.Length; i++)
				{
					Assert.Equal(expectedId++, readMessages[i].Id);
					messages[i].Id = readMessages[i].Id;
					Assert.Equal(messages[i], readMessages[i]);
				}

				// the wrapping collection should also reflect the change
				var collection = file.Messages;
				Assert.Equal(messages.Length, collection.Count);

				// the messages returned by the collection should be the same as the inserted messages
				// (except the message id that is set by the log file, message id was adjusted above)
				Assert.Equal(messages, collection.ToArray());
			}
		}

		#endregion

		#region Read()

		/// <summary>
		/// Tests reading log messages in chunks from the log file returning an array of log messages at the end.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeMixTestData))]
		private void Read_ReturnMessages(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			string path = purpose == LogFilePurpose.Recording
				              ? mFixture.GetCopyOfFile_Recording_RandomMessages_10K()
				              : mFixture.GetCopyOfFile_Analysis_RandomMessages_10K();

			try
			{
				var expectedMessages = mFixture.GetLogMessages_Random_10K();

				int totalMessageCount = expectedMessages.Length;
				var readMessages = new List<LogMessage>();
				using (var file = new LogFile(path, purpose, writeMode))
				{
					// check initial status
					Assert.Equal(totalMessageCount, file.MessageCount);
					Assert.Equal(0, file.OldestMessageId);
					Assert.Equal(totalMessageCount - 1, file.NewestMessageId);

					// read messages in chunks of 999, so the last chunk does not return a full set
					// (covers reading full set and partial set)
					int chunkSize = 999;
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
				Assert.Equal(expectedMessages, readMessages.ToArray());
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
				foreach (LogFilePurpose purpose in Enum.GetValues(typeof(LogFilePurpose)))
				foreach (LogFileWriteMode writeMode in Enum.GetValues(typeof(LogFileWriteMode)))
				{
					yield return new object[] { purpose, writeMode, false };
					yield return new object[] { purpose, writeMode, true };
				}
			}
		}

		/// <summary>
		/// Tests reading log messages in chunks from the log file invoking a callback for each log message.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="cancelReading">
		/// true to cancel reading at half of the expected log messages;
		/// false to read to the end.
		/// </param>
		[Theory]
		[MemberData(nameof(Read_WithCallbackTestData))]
		private void Read_PassMessagesToCallback(LogFilePurpose purpose, LogFileWriteMode writeMode, bool cancelReading)
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
				using (var file = new LogFile(path, purpose, writeMode))
				{
					// check initial status
					Assert.Equal(totalMessageCount, file.MessageCount);
					Assert.Equal(0, file.OldestMessageId);
					Assert.Equal(totalMessageCount - 1, file.NewestMessageId);

					// read messages in chunks of 999, so the last chunk does not return a full set
					// (covers reading full set and partial set)
					int chunkSize = 999;
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
				Assert.Equal(expectedMessages, readMessages.ToArray());
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
		/// Tests clearing an existing log file.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeMixTestData))]
		private void Clear(LogFilePurpose purpose, LogFileWriteMode writeMode)
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
				using (var file = new LogFile(path, purpose, writeMode))
				{
					// check initial status of the file
					Assert.Equal(totalMessageCount, file.MessageCount);
					Assert.Equal(0, file.OldestMessageId);
					Assert.Equal(totalMessageCount - 1, file.NewestMessageId);

					// clear log file
					file.Clear();

					// the file should be empty now
					Assert.Equal(0, file.MessageCount);
					Assert.Equal(-1, file.OldestMessageId);
					Assert.Equal(-1, file.NewestMessageId);

					// the collection should reflect the change as well
					Assert.Equal(0, file.Messages.Count);
					Assert.Empty(file.Messages);
				}

				// the file should be smaller now as the database is vacuum'ed after clearing
				long fileSizeAtEnd = new FileInfo(path).Length;
				Assert.True(fileSizeAtEnd < fileSizeAtStart);
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
				foreach (LogFilePurpose purpose in Enum.GetValues(typeof(LogFilePurpose)))
				foreach (LogFileWriteMode writeMode in Enum.GetValues(typeof(LogFileWriteMode)))
				foreach (bool compact in new[] { false, true })
				{
					yield return new object[] { purpose, writeMode, compact, -1, -1 };     // do not discard anything
					yield return new object[] { purpose, writeMode, compact, 10000, -1 };  // discard by message count limit, but the file does not contain more than that messages
					yield return new object[] { purpose, writeMode, compact, 9999, -1 };   // discard the oldest message by message count
					yield return new object[] { purpose, writeMode, compact, 5000, -1 };   // discard half of the messages by message count
					yield return new object[] { purpose, writeMode, compact, 2, -1 };      // discard all but the newest two messages by message count
					yield return new object[] { purpose, writeMode, compact, 1, -1 };      // discard all but the newest message by message count
					yield return new object[] { purpose, writeMode, compact, 0, -1 };      // discard all messages by message count
					yield return new object[] { purpose, writeMode, compact, -1, 0 };      // do not discard anything
					yield return new object[] { purpose, writeMode, compact, -1, 1 };      // discard the oldest message by timestamp
					yield return new object[] { purpose, writeMode, compact, -1, 2 };      // discard the oldest two messages by timestamp
					yield return new object[] { purpose, writeMode, compact, -1, 5000 };   // discard half of the messages by timestamp
					yield return new object[] { purpose, writeMode, compact, -1, 9999 };   // discard all messages but the newest message by timestamp
					yield return new object[] { purpose, writeMode, compact, -1, 10000 };  // discard all messages by timestamp
					yield return new object[] { purpose, writeMode, compact, 5000, 4999 }; // discard half of the messages (by message count discards one more than by timestamp)
					yield return new object[] { purpose, writeMode, compact, 4999, 5000 }; // discard half of the messages (by timestamp discards one more than by message count)
				}
			}
		}

		/// <summary>
		/// Tests pruning an existing log file.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		/// <param name="compact">true to compact the log file; otherwise false.</param>
		/// <param name="maximumMessageCount">Number of messages to keep in the log file.</param>
		/// <param name="pruneByTimestampCount">Number of old messages to remove by timestamp.</param>
		[Theory]
		[MemberData(nameof(PruneTestData))]
		private void Prune(
			LogFilePurpose   purpose,
			LogFileWriteMode writeMode,
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
				using (var file = new LogFile(path, purpose, writeMode))
				{
					// check initial status of the file
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
							readMessages);
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
							file.Messages.ToArray());
					}
					else
					{
						Assert.Equal(0, file.Messages.Count);
						Assert.Empty(file.Messages);
					}
				}

				// the file should be smaller now as the database is vacuum'ed after pruning
				// (this test is a bit tricky as a database page contains multiple records and removing only few records does not guarantee that a page is removed)
				if (compact && pruneTotalCount > 10)
				{
					long fileSizeAtEnd = new FileInfo(path).Length;
					Assert.True(fileSizeAtEnd < fileSizeAtStart);
				}
			}
			finally
			{
				// remove temporary log file to avoid polluting the output directory
				File.Delete(path);
			}
		}

		#endregion
	}

}
