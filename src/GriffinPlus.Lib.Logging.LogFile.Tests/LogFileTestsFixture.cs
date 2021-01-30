///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#define GENERATE_TESTDATA

using System;
using System.IO;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Fixture for the <seealso cref="LogFileTests"/> class.
	/// </summary>
	public class LogFileTestsFixture : IDisposable
	{
		public readonly string TestFilePath_Recording_RandomMessages_10K;
		public readonly string TestFilePath_Analysis_RandomMessages_10K;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogFileTestsFixture"/> class.
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

			using (var file = LogFile.OpenOrCreate(TestFilePath_Recording_RandomMessages_10K, LogFilePurpose.Recording, LogFileWriteMode.Fast))
			{
				file.Write(messages);
			}

			using (var file = LogFile.OpenOrCreate(TestFilePath_Analysis_RandomMessages_10K, LogFilePurpose.Analysis, LogFileWriteMode.Fast))
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
		public LogFileMessage[] GetLogMessages_Random_10K()
		{
			var messages = LoggingTestHelpers.GetTestMessages<LogFileMessage>(10000);
			for (long i = 0; i < messages.Length; i++) messages[i].Id = i;
			return messages;
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
		private static string GetCopyOfFile(string path)
		{
			string copyPath = Path.GetFullPath($"{Guid.NewGuid():D}.gplog");
			File.Copy(path, copyPath);
			return copyPath;
		}
	}

}
