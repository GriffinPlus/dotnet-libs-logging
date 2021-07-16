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
		public static TMessage[] GetTestMessages<TMessage>(
			int count,
			int randomNumberGeneratorSeed     = 0,
			int maxDifferentWritersCount      = 50,
			int maxDifferentLevelsCount       = 50,
			int maxDifferentApplicationsCount = 3,
			int maxDifferentProcessIdsCount   = 10000) where TMessage : class, ILogMessage, new()
		{
			var messages = new List<TMessage>();

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
				// (the first and the last message must contain at least one tag for filter tests)
				int tagCount = random.Next(i == 0 || i == count - 1 ? 1 : 0, 3);
				var tags = new TagSet();
				for (int j = 0; j < tagCount; j++) tags += allTags[random.Next(0, allTags.Count - 1)];

				var message = new TMessage
				{
					Timestamp = new DateTimeOffset(utcTimestamp + timezoneOffset, timezoneOffset),
					HighPrecisionTimestamp = highPrecisionTimestamp,
					LostMessageCount = random.Next(0, 1),
					LogWriterName = $"Log Writer {random.Next(1, maxDifferentWritersCount)}",
					LogLevelName = $"Log Level {random.Next(1, maxDifferentLevelsCount)}",
					Tags = tags,
					ApplicationName = $"Application {random.Next(1, maxDifferentApplicationsCount)}",
					ProcessName = $"Process {processId}",
					ProcessId = processId,
					Text = $"[{i + 1:D6}/{count:D6}] Log message # Generated Text: {i / 10:D6}/{'a' + i % 10}" // lower-case letter in text only
				};

				// move the timestamps up to 1 day into the future
				var timeSkip = TimeSpan.FromMilliseconds(random.Next(1, 24 * 60 * 60 * 1000));
				utcTimestamp += timeSkip;
				highPrecisionTimestamp += timeSkip.Ticks * 10; // the high precision timestamp is in nanoseconds, ticks are in 100ps

				messages.Add(message);
			}

			// the first an the last message should have different field values to allow the tests to match properly
			if (count > 0)
			{
				var firstMessage = messages[0];
				firstMessage.LogWriterName = "log writer of first message";
				firstMessage.LogLevelName = "log level of first message";
				firstMessage.ApplicationName = "application name of first message";
				firstMessage.ProcessName = "process name of first message";
				firstMessage.ProcessId = int.MinValue;
				firstMessage.Tags = new TagSet("tag-of-first-message");
				// firstMessage.Text is just fine

				var lastMessage = messages[count - 1];
				lastMessage.LogWriterName = "log writer of last message";
				lastMessage.LogLevelName = "log level of last message";
				lastMessage.ApplicationName = "application name of last message";
				lastMessage.ProcessName = "process name of last message";
				lastMessage.ProcessId = int.MaxValue;
				lastMessage.Tags = new TagSet("tag-of-last-message");
				// lastMessage.Text is just fine
			}

			return messages.ToArray();
		}
	}

}
