///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging
{

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
			var threshold = LogLevel.GetAspect(baseLevel);

			// set configuration to let all log levels below the specified log level pass
			var configuration = new VolatileLogConfiguration();
			var settings = new LogWriterConfiguration[1];
			settings[0] = LogWriterConfigurationBuilder
				.New
				.MatchingWildcardPattern("*")
				.WithBaseLevel(baseLevel)
				.Build();
			configuration.SetLogWriterSettings(settings);
			Log.Configuration = configuration;

			// set the processing stage test callback
			int callbackInvokedCount = 0;
			Log.ProcessingPipeline = new CallbackPipelineStage(
				"Callback",
				msg =>
				{
					Assert.True(msg.LogLevel.Id <= threshold.Id);
					callbackInvokedCount++;
					return true;
				});

			// write a message using all predefined log levels
			var writer = Log.GetWriter(sLogWriterName);
			foreach (var level in LogLevel.PredefinedLogLevels)
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
			// set configuration to let only the included log level pass
			var configuration = new VolatileLogConfiguration();
			var settings = new LogWriterConfiguration[1];
			settings[0] = LogWriterConfigurationBuilder
				.New
				.MatchingWildcardPattern("*")
				.WithBaseLevel(LogLevel.None)
				.WithLevel(levelToInclude)
				.Build();
			configuration.SetLogWriterSettings(settings);
			Log.Configuration = configuration;

			// set the processing stage test callback
			int callbackInvokedCount = 0;
			Log.ProcessingPipeline = new CallbackPipelineStage(
				"Callback",
				msg =>
				{
					Assert.Equal(TestMessage, msg.Text);
					Assert.Equal(levelToInclude, msg.LogLevel.Name);
					Assert.Equal(levelToInclude, msg.LogLevelName);
					callbackInvokedCount++;
					return true;
				});

			// write a message using all log messages
			var writer = Log.GetWriter(sLogWriterName);
			foreach (var level in LogLevel.PredefinedLogLevels)
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
			// set configuration to block the excluded log level only
			var configuration = new VolatileLogConfiguration();
			var settings = new LogWriterConfiguration[1];
			settings[0] = LogWriterConfigurationBuilder
				.New
				.MatchingWildcardPattern("*")
				.WithBaseLevel(LogLevel.All)
				.WithoutLevel(levelToExclude)
				.Build();
			configuration.SetLogWriterSettings(settings);
			Log.Configuration = configuration;

			// set the processing stage test callback
			int callbackInvokedCount = 0;
			Log.ProcessingPipeline = new CallbackPipelineStage(
				"Callback",
				msg =>
				{
					Assert.NotEqual(levelToExclude, msg.LogLevel.Name);
					Assert.NotEqual(levelToExclude, msg.LogLevelName);
					callbackInvokedCount++;
					return true;
				});

			// write a message using all log messages
			var writer = Log.GetWriter(sLogWriterName);
			foreach (var level in LogLevel.PredefinedLogLevels)
			{
				writer.Write(level, TestMessage);
			}

			// check whether the callback has been invoked as often as expected
			Assert.Equal(LogLevel.PredefinedLogLevels.Count - 1, callbackInvokedCount);
		}
	}

}
