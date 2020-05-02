///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018-2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
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
		private const string TestMessage = "the quick brown fox jumps over the lazy dog";

		[Theory]
		[InlineData("Failure")]
		[InlineData("Error")]
		[InlineData("Warning")]
		[InlineData("Note")]
		[InlineData("Developer")]
		[InlineData("Trace0")]
		[InlineData("Trace1")]
		[InlineData("Trace2")]
		[InlineData("Trace3")]
		[InlineData("Trace4")]
		[InlineData("Trace5")]
		[InlineData("Trace6")]
		[InlineData("Trace7")]
		[InlineData("Trace8")]
		[InlineData("Trace9")]
		[InlineData("Trace10")]
		[InlineData("Trace11")]
		[InlineData("Trace12")]
		[InlineData("Trace13")]
		[InlineData("Trace14")]
		[InlineData("Trace15")]
		[InlineData("Trace16")]
		[InlineData("Trace17")]
		[InlineData("Trace18")]
		[InlineData("Trace19")]
		public void Log_Configuration_Should_Let_Messages_Below_BaseLevel_Pass(string baseLevel)
		{
			// convert log level name to LogLevel object
			LogLevel threshold = LogLevel.GetAspect(baseLevel);

			// set configuration to let all log levels below the specified log level pass
			VolatileLogConfiguration configuration = new VolatileLogConfiguration();
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
			Log.LogMessageProcessingPipeline = new CallbackPipelineStage(msg => {
				Assert.True(msg.LogLevel.Id <= threshold.Id);
				callbackInvokedCount++;
				return true;
			});

			// write a message using all predefined log levels
			LogWriter writer = Log.GetWriter(sLogWriterName);
			foreach (LogLevel level in LogLevel.PredefinedLogLevels)
			{
				writer.Write(level, TestMessage);
			}

			// check whether the callback has been invoked as often as expected
			Assert.Equal(threshold.Id + 1, callbackInvokedCount);
		}

		[Theory]
		[InlineData("Failure")]
		[InlineData("Error")]
		[InlineData("Warning")]
		[InlineData("Note")]
		[InlineData("Developer")]
		[InlineData("Trace0")]
		[InlineData("Trace1")]
		[InlineData("Trace2")]
		[InlineData("Trace3")]
		[InlineData("Trace4")]
		[InlineData("Trace5")]
		[InlineData("Trace6")]
		[InlineData("Trace7")]
		[InlineData("Trace8")]
		[InlineData("Trace9")]
		[InlineData("Trace10")]
		[InlineData("Trace11")]
		[InlineData("Trace12")]
		[InlineData("Trace13")]
		[InlineData("Trace14")]
		[InlineData("Trace15")]
		[InlineData("Trace16")]
		[InlineData("Trace17")]
		[InlineData("Trace18")]
		[InlineData("Trace19")]
		public void Log_Configuration_Should_Let_Messages_Of_Included_Levels_Pass(string levelToInclude)
		{
			// set configuration to let only the included log level pass
			VolatileLogConfiguration configuration = new VolatileLogConfiguration();
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
			Log.LogMessageProcessingPipeline = new CallbackPipelineStage(msg => {
				Assert.Equal(TestMessage, msg.Text);
				Assert.Equal(levelToInclude, msg.LogLevel.Name);
				Assert.Equal(levelToInclude, msg.LogLevelName);
				callbackInvokedCount++;
				return true;
			});

			// write a message using all log messages
			LogWriter writer = Log.GetWriter(sLogWriterName);
			foreach (LogLevel level in LogLevel.PredefinedLogLevels) {
				writer.Write(level, TestMessage);
			}

			// the callback should have been invoked exactly once
			Assert.Equal(1, callbackInvokedCount);
		}

		[Theory]
		[InlineData("Failure")]
		[InlineData("Error")]
		[InlineData("Warning")]
		[InlineData("Note")]
		[InlineData("Developer")]
		[InlineData("Trace0")]
		[InlineData("Trace1")]
		[InlineData("Trace2")]
		[InlineData("Trace3")]
		[InlineData("Trace4")]
		[InlineData("Trace5")]
		[InlineData("Trace6")]
		[InlineData("Trace7")]
		[InlineData("Trace8")]
		[InlineData("Trace9")]
		[InlineData("Trace10")]
		[InlineData("Trace11")]
		[InlineData("Trace12")]
		[InlineData("Trace13")]
		[InlineData("Trace14")]
		[InlineData("Trace15")]
		[InlineData("Trace16")]
		[InlineData("Trace17")]
		[InlineData("Trace18")]
		[InlineData("Trace19")]
		public void Log_Configuration_Should_Filter_Messages_Of_Excluded_Levels(string levelToExclude)
		{
			// set configuration to block the excluded log level only
			VolatileLogConfiguration configuration = new VolatileLogConfiguration();
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
			Log.LogMessageProcessingPipeline = new CallbackPipelineStage(msg => {
				Assert.NotEqual(levelToExclude, msg.LogLevel.Name);
				Assert.NotEqual(levelToExclude, msg.LogLevelName);
				callbackInvokedCount++;
				return true;
			});

			// write a message using all log messages
			LogWriter writer = Log.GetWriter(sLogWriterName);
			foreach (LogLevel level in LogLevel.PredefinedLogLevels) {
				writer.Write(level, TestMessage);
			}

			// check whether the callback has been invoked as often as expected
			Assert.Equal(LogLevel.PredefinedLogLevels.Count - 1, callbackInvokedCount);
		}

	}
}
