///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Unit tests targeting the <see cref="LogFile"/> class.
	/// </summary>
	public class LogFileTests
	{
		/// <summary>
		/// Tests getting the <see cref="LogFile.SqliteVersion"/> property.
		/// </summary>
		[Fact]
		private void GetSqliteVersion()
		{
			Assert.Equal("3.32.1", LogFile.SqliteVersion);
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
		/// Tests writing a single message to an empty log file.
		/// </summary>
		/// <param name="purpose">Log file purpose to test.</param>
		/// <param name="writeMode">Log file write mode to test.</param>
		[Theory]
		[MemberData(nameof(PurposeWriteModeMixTestData))]
		private void SingleMessage(LogFilePurpose purpose, LogFileWriteMode writeMode)
		{
			const string filename = "Test.gplog";
			string       fullPath = Path.GetFullPath(filename);

			// ensure the log file does not exist
			File.Delete(fullPath);

			// generate a message to write into the file
			LogMessage message = GetTestMessage();

			// create a new log file
			using (LogFile file = new LogFile(filename, purpose, writeMode))
			{
				// the state of an empty log file is already tested in CreateEmptyFile()
				// => nothing to do here...

				// write the message
				file.Write(message);

				// the file should contain exactly one message now
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
		/// Gets a well defined log message.
		/// </summary>
		/// <returns></returns>
		private LogMessage GetTestMessage()
		{
			return new LogMessage().InitWith(
				-1,
				DateTimeOffset.Parse("2020-01-01T01:02:03.456+01:00"),
				123,
				456,
				"LogWriterName",
				"LogLevelName",
				null,
				"ApplicationName",
				"ProcessName",
				42,
				"The quick brown fox jumps over the lazy dog.");
		}
	}
}