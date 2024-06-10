///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Tests targeting the interaction of multiple classes.
/// </summary>
public class IntegrationTests
{
	private static readonly string sLogWriterName = typeof(IntegrationTests).FullName;
	private const           string TestMessage    = "the quick brown fox jumps over the lazy dog";

	[Theory]
	[InlineData("Emergency")]
	[InlineData("Alert")]
	[InlineData("Critical")]
	[InlineData("Error")]
	[InlineData("Warning")]
	[InlineData("Notice")]
	[InlineData("Informational")]
	[InlineData("Debug")]
	[InlineData("Trace")]
	public void Log_Configuration_Should_Let_Messages_Below_BaseLevel_Pass(string baseLevel)
	{
		// convert log level name to LogLevel object
		LogLevel threshold = LogLevel.GetAspect(baseLevel);

		// initialize the logging subsystem
		int callbackInvokedCount = 0;
		Log.Initialize<VolatileLogConfiguration>(
			config =>
			{
				//
				// configuration
				//

				// set configuration to block the excluded log level only
				var settings = new LogWriterConfiguration[1];
				settings[0] = LogWriterConfigurationBuilder
					.New
					.MatchingWildcardPattern("*")
					.WithBaseLevel(baseLevel)
					.Build();
				config.SetLogWriterSettings(settings);
			},
			builder =>
			{
				//
				// processing pipeline stage
				//

				builder.Add<CallbackPipelineStage>(
					"Callback",
					stage =>
					{
						// set the processing stage test callback
						stage.ProcessingCallback = msg =>
						{
							Assert.True(msg.LogLevel.Id <= threshold.Id);
							callbackInvokedCount++;
							return true;
						};
					});
			});

		// write a message using all predefined log levels
		LogWriter writer = LogWriter.Get(sLogWriterName);
		foreach (LogLevel level in LogLevel.PredefinedLogLevels)
		{
			writer.Write(level, TestMessage);
		}

		// check whether the callback has been invoked as often as expected
		Assert.Equal(threshold.Id + 1, callbackInvokedCount);
	}

	[Theory]
	[InlineData("Emergency")]
	[InlineData("Alert")]
	[InlineData("Critical")]
	[InlineData("Error")]
	[InlineData("Warning")]
	[InlineData("Notice")]
	[InlineData("Informational")]
	[InlineData("Debug")]
	[InlineData("Trace")]
	public void Log_Configuration_Should_Let_Messages_Of_Included_Levels_Pass(string levelToInclude)
	{
		// initialize the logging subsystem
		int callbackInvokedCount = 0;
		Log.Initialize<VolatileLogConfiguration>(
			config =>
			{
				//
				// configuration
				//

				// set configuration to block the excluded log level only
				var settings = new LogWriterConfiguration[1];
				settings[0] = LogWriterConfigurationBuilder
					.New
					.MatchingWildcardPattern("*")
					.WithBaseLevel(LogLevel.None)
					.WithLevel(levelToInclude)
					.Build();
				config.SetLogWriterSettings(settings);
			},
			builder =>
			{
				//
				// processing pipeline stage
				//

				builder.Add<CallbackPipelineStage>(
					"Callback",
					stage =>
					{
						// set the processing stage test callback
						stage.ProcessingCallback = msg =>
						{
							Assert.Equal(TestMessage, msg.Text);
							Assert.Equal(levelToInclude, msg.LogLevel.Name);
							Assert.Equal(levelToInclude, msg.LogLevelName);
							callbackInvokedCount++;
							return true;
						};
					});
			});

		// write a message using all log messages
		LogWriter writer = LogWriter.Get(sLogWriterName);
		foreach (LogLevel level in LogLevel.PredefinedLogLevels)
		{
			writer.Write(level, TestMessage);
		}

		// the callback should have been invoked exactly once
		Assert.Equal(1, callbackInvokedCount);
	}

	[Theory]
	[InlineData("Emergency")]
	[InlineData("Alert")]
	[InlineData("Critical")]
	[InlineData("Error")]
	[InlineData("Warning")]
	[InlineData("Notice")]
	[InlineData("Informational")]
	[InlineData("Debug")]
	[InlineData("Trace")]
	public void Log_Configuration_Should_Filter_Messages_Of_Excluded_Levels(string levelToExclude)
	{
		// initialize the logging subsystem
		int callbackInvokedCount = 0;
		Log.Initialize<VolatileLogConfiguration>(
			config =>
			{
				//
				// configuration
				//

				// set configuration to block the excluded log level only
				var settings = new LogWriterConfiguration[1];
				settings[0] = LogWriterConfigurationBuilder
					.New
					.MatchingWildcardPattern("*")
					.WithBaseLevel(LogLevel.All)
					.WithoutLevel(levelToExclude)
					.Build();
				config.SetLogWriterSettings(settings);
			},
			builder =>
			{
				//
				// processing pipeline stage
				//

				builder.Add<CallbackPipelineStage>(
					"Callback",
					stage =>
					{
						// set the processing stage test callback
						stage.ProcessingCallback = msg =>
						{
							Assert.NotEqual(levelToExclude, msg.LogLevel.Name);
							Assert.NotEqual(levelToExclude, msg.LogLevelName);
							callbackInvokedCount++;
							return true;
						};
					});
			});

		// write a message using all log messages
		LogWriter writer = LogWriter.Get(sLogWriterName);
		foreach (LogLevel level in LogLevel.PredefinedLogLevels)
		{
			writer.Write(level, TestMessage);
		}

		// check whether the callback has been invoked as often as expected
		Assert.Equal(LogLevel.PredefinedLogLevels.Count - 1, callbackInvokedCount);
	}
}
