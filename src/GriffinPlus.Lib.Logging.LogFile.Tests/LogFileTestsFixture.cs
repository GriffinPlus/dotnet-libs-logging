///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#define GENERATE_TESTDATA

using System;
using System.Collections.Generic;
using System.IO;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Fixture for the <seealso cref="LogFileTests" /> class.
	/// </summary>
	public class LogFileTestsFixture : IDisposable
	{
		public readonly string TestFilePath_Recording_RandomMessages_10K;
		public readonly string TestFilePath_Analysis_RandomMessages_10K;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogFileTestsFixture" /> class.
		/// </summary>
		public LogFileTestsFixture()
		{
			TestFilePath_Recording_RandomMessages_10K = Path.GetFullPath("TestData/Recording_RandomMessages_10K.gplog");
			TestFilePath_Analysis_RandomMessages_10K = Path.GetFullPath("TestData/Analysis_RandomMessages_10K.gplog");

#if GENERATE_TESTDATA

			// generate the reference log files containing the log message sets
			var messages = GetLogMessages_Random_10K();

			Directory.CreateDirectory("TestData");

			File.Delete(TestFilePath_Recording_RandomMessages_10K);
			File.Delete(TestFilePath_Analysis_RandomMessages_10K);

			using (var file = new LogFile(TestFilePath_Recording_RandomMessages_10K, LogFilePurpose.Recording, LogFileWriteMode.Fast))
			{
				file.Write(messages);
			}

			using (var file = new LogFile(TestFilePath_Analysis_RandomMessages_10K, LogFilePurpose.Analysis, LogFileWriteMode.Fast))
			{
				file.Write(messages);
			}
#else
			// use the shipped reference log files containing the log message sets
			Assert.True(File.Exists(TestFilePath_Recording_RandomMessages_10K));
			Assert.True(File.Exists(TestFilePath_Analysis_RandomMessages_10K));
#endif
		}

		/// <summary>
		/// Disposes the fixture.
		/// </summary>
		public void Dispose()
		{
		}

		/// <summary>
		/// Gets a set of log messages with deterministic content
		/// (each set is freshly created to avoid issues between tests that modify test data).
		/// </summary>
		public LogMessage[] GetLogMessages_Random_10K()
		{
			return GetTestMessages(10000);
		}

		/// <summary>
		/// Gets a copy of the reference log file with recording purpose and 10k messages.
		/// </summary>
		/// <returns></returns>
		public string GetCopyOfFile_Recording_RandomMessages_10K()
		{
			return GetCopyOfFile(TestFilePath_Recording_RandomMessages_10K);
		}

		/// <summary>
		/// Gets a copy of the reference log file with analysis purpose and 10k messages.
		/// </summary>
		/// <returns></returns>
		public string GetCopyOfFile_Analysis_RandomMessages_10K()
		{
			return GetCopyOfFile(TestFilePath_Analysis_RandomMessages_10K);
		}

		/// <summary>
		/// Copies the specified file to a temporary file in the working directory and returns its path.
		/// </summary>
		/// <param name="path">Path of the file to copy.</param>
		/// <returns>Path of the copy of the file.</returns>
		private string GetCopyOfFile(string path)
		{
			string copyPath = Path.GetFullPath($"{Guid.NewGuid():D}.gplog");
			File.Copy(path, copyPath);
			return copyPath;
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
		public static LogMessage[] GetTestMessages(
			int count,
			int maxDifferentWritersCount      = 50,
			int maxDifferentLevelsCount       = 50,
			int maxDifferentApplicationsCount = 3,
			int maxDifferentProcessIdsCount   = 100000)
		{
			var messages = new List<LogMessage>();

			var random = new Random(0);
			var utcTimestamp = DateTime.Parse("2020-01-01T01:02:03");
			long highPrecisionTimestamp = 0;

			for (long i = 0; i < count; i++)
			{
				var timezoneOffset = TimeSpan.FromHours(random.Next(-14, 14));
				int processId = random.Next(1, maxDifferentProcessIdsCount);
				var message = new LogMessage().InitWith(
					i, // message id is zero-based, so the index is a perfect match
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
