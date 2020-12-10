///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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

		/// <summary>
		/// Gets a deterministic set of random log messages.
		/// </summary>
		/// <param name="count">Number of log messages to generate.</param>
		/// <param name="maxDifferentWritersCount">Maximum number of different log writer names.</param>
		/// <param name="maxDifferentLevelsCount">Maximum number of different log level names.</param>
		/// <param name="maxDifferentApplicationsCount">Maximum number of different application names.</param>
		/// <param name="maxDifferentProcessIdsCount">Maximum number of different process ids.</param>
		/// <returns>The requested log message set.</returns>
		private static LogMessage[] GetTestMessages(
			int count,
			int maxDifferentWritersCount = 50,
			int maxDifferentLevelsCount = 50,
			int maxDifferentApplicationsCount = 3,
			int maxDifferentProcessIdsCount = 100000)
		{
			List<LogMessage> messages = new List<LogMessage>();

			Random random = new Random(0);
			DateTime utcTimestamp = DateTime.Parse("2020-01-01T01:02:03");
			long highPrecisionTimestamp = 0;

			for (int i = 0; i < count; i++)
			{
				var timezoneOffset = TimeSpan.FromHours(random.Next(-14,14));
				var processId = random.Next(1, maxDifferentProcessIdsCount);
				LogMessage message = new LogMessage().InitWith(
					-1,
					new DateTimeOffset(utcTimestamp + timezoneOffset, timezoneOffset),
					highPrecisionTimestamp,
					random.Next(0, 1),
					$"Log Writer {random.Next(1, maxDifferentWritersCount)}",
					$"Log Level {random.Next(1, maxDifferentLevelsCount)}",
					null,
					$"Application {random.Next(1, maxDifferentApplicationsCount)}",
					$"Process {processId}",
					processId,
					$"Just a log message with some random content ({random.Next(0, 100000)})");

				// move the timestamps up to 1 day into the future
				var timeSkip = TimeSpan.FromMilliseconds(random.Next(1, 24 * 60 * 60 * 1000));
				utcTimestamp += timeSkip;
				highPrecisionTimestamp += timeSkip.Ticks * 10; // the high precision timestamp is in nanoseconds, ticks are in 100ps

				messages.Add(message);
			}

			return messages.ToArray();
		}
	}
}