///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#define GENERATE_TESTDATA

using System;
using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Helper methods for logging related tests.
	/// </summary>
	public static class LoggingTestHelpers
	{
		/// <summary>
		/// Gets a deterministic set of random log messages.
		/// </summary>
		/// <param name="count">Number of log messages to generate.</param>
		/// <param name="randomNumberGeneratorSeed">
		/// Seed for the random number generator
		/// (0 is usually used to generate default log message sets, use some other value to generate a different set).
		/// </param>
		/// <param name="maxDifferentWritersCount">Maximum number of different log writer names.</param>
		/// <param name="maxDifferentLevelsCount">Maximum number of different log level names.</param>
		/// <param name="maxDifferentApplicationsCount">Maximum number of different application names.</param>
		/// <param name="maxDifferentProcessIdsCount">Maximum number of different process ids.</param>
		/// <returns>The requested log message set.</returns>
		public static LogMessage[] GetTestMessages(
			int count,
			int randomNumberGeneratorSeed     = 0,
			int maxDifferentWritersCount      = 50,
			int maxDifferentLevelsCount       = 50,
			int maxDifferentApplicationsCount = 3,
			int maxDifferentProcessIdsCount   = 100000)
		{
			var messages = new List<LogMessage>();

			var random = new Random(randomNumberGeneratorSeed);
			var utcTimestamp = DateTime.Parse("2020-01-01T01:02:03");
			long highPrecisionTimestamp = 0;

			// init list of tags to select from
			var allTags = new List<string>();
			for (int i = 0; i < 50; i++) allTags.Add($"Tag{i}");

			for (long i = 0; i < count; i++)
			{
				var timezoneOffset = TimeSpan.FromHours(random.Next(-14, 14));
				int processId = random.Next(1, maxDifferentProcessIdsCount);

				// build tag set to associate with a message (up to 3 tags per message)
				int tagCount = random.Next(0, 3);
				var tags = new TagSet();
				for (int j = 0; j < tagCount; j++) tags += allTags[random.Next(0, allTags.Count - 1)];

				var message = new LogMessage().InitWith(
					i, // message id is zero-based, so the index is a perfect match
					new DateTimeOffset(utcTimestamp + timezoneOffset, timezoneOffset),
					highPrecisionTimestamp,
					random.Next(0, 1),
					$"Log Writer {random.Next(1, maxDifferentWritersCount)}",
					$"Log Level {random.Next(1, maxDifferentLevelsCount)}",
					tags,
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
