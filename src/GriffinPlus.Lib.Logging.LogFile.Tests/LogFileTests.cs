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
	public class LogFileTests : IClassFixture<LogFileTestsFixture>
	{
		private readonly LogFileTestsFixture mFixture;

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
		/// Tests creating a new instance of the <see cref="LogFile"/> class.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeMixTestData))]
		private void CreateEmptyFile(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			const string filename = "Test.gplog";
			string       fullPath = Path.GetFullPath(filename);

			// ensure the log file does not exist
			File.Delete(fullPath);

			// create a new log file
			using (LogFile file = new LogFile(filename, purpose, writeMode))
			{
				// the log file itself
				Assert.Equal(fullPath,  file.FilePath);
				Assert.Equal(purpose,   file.Purpose);
				Assert.Equal(writeMode, file.WriteMode);
				Assert.Equal(0,         file.MessageCount);
				Assert.Equal(-1,        file.OldestMessageId);
				Assert.Equal(-1,        file.NewestMessageId);

				// the message collection working on top of the log file
				var collection = file.Messages;
				Assert.Same(file, collection.LogFile);
				Assert.Empty(collection);
				Assert.False(collection.IsReadOnly);
				Assert.Equal(0,        collection.Count);
				Assert.Equal(20,       collection.MaxCachePageCount);
				Assert.Equal(100,      collection.CachePageCapacity);
				Assert.Equal(fullPath, collection.FilePath);
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
			string       fullPath = Path.GetFullPath(filename);

			// ensure the log file does not exist
			File.Delete(fullPath);

			// generate a message to write into the file
			LogMessage message = LogFileTestsFixture.GetTestMessages(1)[0];

			// create a new log file
			using (LogFile file = new LogFile(filename, purpose, writeMode))
			{
				// the state of an empty log file is already tested in CreateEmptyFile()
				// => nothing to do here...

				// write the message
				file.Write(message);

				// the file should contain the written message only now
				Assert.Equal(1, file.MessageCount);
				Assert.Equal(0, file.OldestMessageId);
				Assert.Equal(0, file.NewestMessageId);
				LogMessage[] readMessages = file.Read(0, 100);
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
			string       fullPath = Path.GetFullPath(filename);

			// ensure the log file does not exist
			File.Delete(fullPath);

			// generate messages to write into the file
			LogMessage[] messages = mFixture.GetLogMessages_Random_10K();

			// create a new log file
			using (LogFile file = new LogFile(filename, purpose, writeMode))
			{
				// the state of an empty log file is already tested in CreateEmptyFile()
				// => nothing to do here...

				// write the message
				file.Write(messages);

				// the file should now contain the written messages
				LogMessage[] readMessages = file.Read(0, messages.Length + 1);
				Assert.Equal(messages.Length,     file.MessageCount);
				Assert.Equal(0,                   file.OldestMessageId);
				Assert.Equal(messages.Length - 1, file.NewestMessageId);
				Assert.Equal(messages.Length,     readMessages.Length);

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

			LogMessage[] expectedMessages = mFixture.GetLogMessages_Random_10K();

			try
			{
				int              totalMessageCount = expectedMessages.Length;
				List<LogMessage> readMessages      = new List<LogMessage>();
				using (LogFile file = new LogFile(path, purpose, writeMode))
				{
					// check initial status
					Assert.Equal(totalMessageCount,     file.MessageCount);
					Assert.Equal(0,                     file.OldestMessageId);
					Assert.Equal(totalMessageCount - 1, file.NewestMessageId);

					// read messages in chunks of 999, so the last chunk does not return a full set
					// (covers reading full set and partial set)
					int chunkSize = 999;
					for (int readMessageCount = 0; readMessageCount < totalMessageCount;)
					{
						LogMessage[] messages = file.Read(file.OldestMessageId + readMessageCount, chunkSize);

						int expectedReadCount = Math.Min(chunkSize, totalMessageCount - readMessageCount);
						Assert.Equal(expectedReadCount, messages.Length);
						readMessages.AddRange(messages);
						readMessageCount += messages.Length;
					}
				}

				// the list of read messages should now equal the original test data set
				Assert.Equal(expectedMessages.Length, readMessages.Count);
				Assert.Equal(expectedMessages,        readMessages.ToArray());
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

			// get expected result set
			// (when cancellation is requested, it is done after half the log messages)
			LogMessage[] allMessages = mFixture.GetLogMessages_Random_10K();
			LogMessage[] expectedMessages = cancelReading ? allMessages.Take(5000).ToArray() : allMessages;
			int totalMessageCount = allMessages.Length;
			int expectedMessageCount = expectedMessages.Length;

			try
			{
				List<LogMessage> readMessages = new List<LogMessage>();
				using (LogFile file = new LogFile(path, purpose, writeMode))
				{
					// check initial status
					Assert.Equal(totalMessageCount,     file.MessageCount);
					Assert.Equal(0,                     file.OldestMessageId);
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
				Assert.Equal(expectedMessages,        readMessages.ToArray());
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
