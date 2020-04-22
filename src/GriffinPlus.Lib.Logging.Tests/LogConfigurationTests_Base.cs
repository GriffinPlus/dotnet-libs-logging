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

using System;
using System.Linq;
using Xunit;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Common unit tests targeting the <see cref="LogConfiguration"/> and the <see cref="FileBackedLogConfiguration"/> class.
	/// </summary>
	public abstract class LogConfigurationTests_Base<CONFIGURATION> where CONFIGURATION: LogConfiguration<CONFIGURATION>, new()
	{
		const string Aspect1Name = "Aspect1";
		const string Aspect2Name = "Aspect2";
		const string Aspect3Name = "Aspect3";
		const int Aspect1Id = 26;
		const int Aspect2Id = 27;
		const int Aspect3Id = 28;

		protected LogConfigurationTests_Base()
		{
			LogLevel aspect1 = LogLevel.GetAspect(Aspect1Name);
			Assert.Equal(Aspect1Name, aspect1.Name);
			Assert.Equal(Aspect1Id, aspect1.Id);

			LogLevel aspect2 = LogLevel.GetAspect(Aspect2Name);
			Assert.Equal(Aspect2Name, aspect2.Name);
			Assert.Equal(Aspect2Id, aspect2.Id);

			LogLevel aspect3 = LogLevel.GetAspect(Aspect3Name);
			Assert.Equal(Aspect3Name, aspect3.Name);
			Assert.Equal(Aspect3Id, aspect3.Id);
		}

		[Fact]
		public void Creating_Default_Configuration()
		{
			CONFIGURATION configuration = new CONFIGURATION();

			// global settings
			Assert.Equal(AppDomain.CurrentDomain.FriendlyName, configuration.ApplicationName);

			// pipeline stage settings
			var stageSettings = configuration.GetProcessingPipelineStageSettings();
			Assert.Empty(stageSettings);

			// log writer settings
			var logWriterSettings = configuration.GetLogWriterSettings();
			Assert.Single(logWriterSettings);
			var logWriterSetting = logWriterSettings.First();
			Assert.Equal("Note", logWriterSetting.BaseLevel);
			Assert.Empty(logWriterSetting.Includes);
			Assert.Empty(logWriterSetting.Excludes);
			Assert.Single(logWriterSetting.Patterns);
			var pattern = logWriterSetting.Patterns.First();
			Assert.IsType<LogWriterConfiguration.WildcardLogWriterPattern>(pattern);
			Assert.Equal("*", pattern.Pattern);
		}

		[Fact]
		public void Setting_ApplicationName()
		{
			CONFIGURATION configuration = new CONFIGURATION();
			configuration.ApplicationName = "My App";
			Assert.Equal("My App", configuration.ApplicationName);
		}

		[Theory]
		[InlineData("Failure",   0x00000001u)]
		[InlineData("Error",     0x00000003u)]
		[InlineData("Warning",   0x00000007u)]
		[InlineData("Note",      0x0000000Fu)]
		[InlineData("Developer", 0x0000001Fu)]
		[InlineData("Trace0",    0x0000003Fu)]
		[InlineData("Trace1",    0x0000007Fu)]
		[InlineData("Trace2",    0x000000FFu)]
		[InlineData("Trace3",    0x000001FFu)]
		[InlineData("Trace4",    0x000003FFu)]
		[InlineData("Trace5",    0x000007FFu)]
		[InlineData("Trace6",    0x00000FFFu)]
		[InlineData("Trace7",    0x00001FFFu)]
		[InlineData("Trace8",    0x00003FFFu)]
		[InlineData("Trace9",    0x00007FFFu)]
		[InlineData("Trace10",   0x0000FFFFu)]
		[InlineData("Trace11",   0x0001FFFFu)]
		[InlineData("Trace12",   0x0003FFFFu)]
		[InlineData("Trace13",   0x0007FFFFu)]
		[InlineData("Trace14",   0x000FFFFFu)]
		[InlineData("Trace15",   0x001FFFFFu)]
		[InlineData("Trace16",   0x003FFFFFu)]
		[InlineData("Trace17",   0x007FFFFFu)]
		[InlineData("Trace18",   0x00FFFFFFu)]
		[InlineData("Trace19",   0x01FFFFFFu)]
		public void Getting_Active_Log_Level_Mask_For_Specific_BaseLevel(string level, uint expectedMask)
		{
			CONFIGURATION configuration = new CONFIGURATION();
			var settings = new LogWriterConfiguration[1];
			settings[0] = LogWriterConfigurationBuilder
				.New
				.MatchingWildcardPattern("*")
				.WithBaseLevel(level)
				.Build();
			configuration.SetLogWriterSettings(settings);
			LogWriter writer = Log.GetWriter("UnitTest");
			LogLevelBitMask mask = configuration.GetActiveLogLevelMask(writer);
			uint[] bitArray = mask.AsArray();
			Assert.Single(bitArray);
			Assert.Equal(expectedMask, bitArray[0]);
		}

		[Theory]
		// base level only
		[InlineData("None",    new string[] { },           new string[] { },           0x00000000u)]
		[InlineData("All",     new string[] { },           new string[] { },           0xFFFFFFFFu)]
		// includes
		[InlineData("None",    new string[] { "Note" },    new string[] { },           0x00000008u)] // single predefined level only
		[InlineData("None",    new string[] { "Aspect1" }, new string[] { },           0x04000000u)] // single aspect level only
		[InlineData("Note",    new string[] { "Trace0" },  new string[] { },           0x0000002Fu)] // base level + predefined level
		[InlineData("Note",    new string[] { "Aspect1" }, new string[] { },           0x0400000Fu)] // base level + aspect level
		// excludes
		[InlineData("All",     new string[] { },           new string[] { "Note" },    0xFFFFFFF7u)] // all except a single level
		[InlineData("Note",    new string[] { },           new string[] { "Note" },    0x00000007u)]
		[InlineData("Note",    new string[] { },           new string[] { "Error" },   0x0000000Du)]
		// mixed
		[InlineData(
			"Developer",
			new string[] { "Trace0", "Trace19", "Aspect1", "Aspect2", "Aspect3" },
			new string[] { "Error", "Aspect3" }, // exclude overrides include for 'Aspect3'
			0x0D00003Du)]
		public void Getting_Active_Log_Level_Mask_With_Includes_And_Excludes(string baseLevel, string[] includes, string[] excludes, uint expectedMask)
		{
			CONFIGURATION configuration = new CONFIGURATION();
			var settings = new LogWriterConfiguration[1];
			settings[0] = LogWriterConfigurationBuilder
				.New
				.MatchingWildcardPattern("*")
				.WithBaseLevel(baseLevel)
				.WithLevel(includes)
				.WithoutLevel(excludes)
				.Build();
			configuration.SetLogWriterSettings(settings);
			LogWriter writer = Log.GetWriter("UnitTest");
			LogLevelBitMask mask = configuration.GetActiveLogLevelMask(writer);
			uint[] bitArray = mask.AsArray();
			Assert.Single(bitArray);
			Assert.Equal(expectedMask, bitArray[0]);
		}

		[Fact]
		public abstract void Saving_Default_Configuration();

		#region AddLogWriter()

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void AddLogWriter_ExactNameByGenericArgument(bool useConfigurationCallback)
		{
			CONFIGURATION configuration = GetDefaultConfiguration();

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				configuration.AddLogWriter<CONFIGURATION>(x => Assert.NotNull(x));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}
			else
			{
				configuration.AddLogWriter<CONFIGURATION>();
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}

			Assert.Single(writer.Patterns);
			var pattern = writer.Patterns.First();
			Assert.IsType<LogWriterConfiguration.ExactNameLogWriterPattern>(pattern);
			Assert.Equal($"{typeof(CONFIGURATION).FullName}", pattern.Pattern);
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void AddLogWriter_ExactNameByType(bool useConfigurationCallback)
		{
			CONFIGURATION configuration = GetDefaultConfiguration();
			Type type = typeof(CONFIGURATION);

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				configuration.AddLogWriter(type, x => Assert.NotNull(x));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}
			else
			{
				configuration.AddLogWriter(type);
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}

			Assert.Single(writer.Patterns);
			var pattern = writer.Patterns.First();
			Assert.IsType<LogWriterConfiguration.ExactNameLogWriterPattern>(pattern);
			Assert.Equal($"{type.FullName}", pattern.Pattern);
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void AddLogWriter_WithWildcardPattern(bool useConfigurationCallback)
		{
			CONFIGURATION configuration = GetDefaultConfiguration();
			string wildcard = "MyDemo*";

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				configuration.AddLogWritersByWildcard(wildcard, x => Assert.NotNull(x));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}
			else
			{
				configuration.AddLogWritersByWildcard(wildcard);
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}

			Assert.Single(writer.Patterns);
			var pattern = writer.Patterns.First();
			Assert.IsType<LogWriterConfiguration.WildcardLogWriterPattern>(pattern);
			Assert.Equal(wildcard, pattern.Pattern);
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void AddLogWriter_WithRegexPattern(bool useConfigurationCallback)
		{
			CONFIGURATION configuration = GetDefaultConfiguration();
			string regex = "^[a-z][A-Z][0-9]$";

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				configuration.AddLogWritersByRegex(regex, x => Assert.NotNull(x));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}
			else
			{
				configuration.AddLogWritersByRegex(regex);
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}

			Assert.Single(writer.Patterns);
			var pattern = writer.Patterns.First();
			Assert.IsType<LogWriterConfiguration.RegexLogWriterPattern>(pattern);
			Assert.Equal(regex, pattern.Pattern);
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		[Fact]
		public void AddLogWriterTiming()
		{
			CONFIGURATION configuration = GetDefaultConfiguration();
			configuration.AddLogWriterTiming();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];
			Assert.Single(writer.Patterns);
			var pattern = writer.Patterns.First();
			Assert.IsType<LogWriterConfiguration.ExactNameLogWriterPattern>(pattern);
			Assert.Equal("Timing", pattern.Pattern);
			Assert.Equal("None", writer.BaseLevel);
			Assert.Single(writer.Includes, "Timing");
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void AddLogWriterDefault(bool useConfigurationCallback)
		{
			CONFIGURATION configuration = GetDefaultConfiguration();

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				configuration.AddLogWriterDefault(x => Assert.NotNull(x));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}
			else
			{
				configuration.AddLogWriterDefault();
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}

			Assert.Single(writer.Patterns);
			var pattern = writer.Patterns.First();
			Assert.IsType<LogWriterConfiguration.WildcardLogWriterPattern>(pattern);
			Assert.Equal("*", pattern.Pattern);
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		[Fact]
		public void AddLogWriter_AllTogether()
		{
			// Testing with/without configuration callback is not necessary as the methods invoking the callbacks
			// were already covered above.

			CONFIGURATION configuration = GetDefaultConfiguration();

			string wildcard = "MyDemo*";
			string regex = "^[a-z][A-Z][0-9]$";

			configuration.AddLogWriter<CONFIGURATION>();
			configuration.AddLogWriter(typeof(CONFIGURATION));
			configuration.AddLogWritersByWildcard(wildcard);
			configuration.AddLogWritersByRegex(regex);
			configuration.AddLogWriterTiming();
			configuration.AddLogWriterDefault();

			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Equal(6, writers.Length);

			for (int i = 0; i < writers.Length; i++)
			{
				var writer = writers[i];
				Assert.Single(writer.Patterns);
				var pattern = writer.Patterns.First();

				switch (i)
				{
					// effect of .WithLogWriter<T>()
					case 0:
						Assert.IsType<LogWriterConfiguration.ExactNameLogWriterPattern>(pattern);
						Assert.Equal($"{typeof(CONFIGURATION).FullName}", pattern.Pattern);
						break;

					// effect of .WithLogWriter(typeof(T))
					case 1:
						Assert.IsType<LogWriterConfiguration.ExactNameLogWriterPattern>(pattern);
						Assert.Equal($"{typeof(CONFIGURATION).FullName}", pattern.Pattern);
						break;

					// effect of .WithLogWritersByWildcard(wildcard)
					case 2:
						Assert.IsType<LogWriterConfiguration.WildcardLogWriterPattern>(pattern);
						Assert.Equal(wildcard, pattern.Pattern);
						break;

					// effect of WithLogWritersByRegex(regex)
					case 3:
						Assert.IsType<LogWriterConfiguration.RegexLogWriterPattern>(pattern);
						Assert.Equal(regex, pattern.Pattern);
						break;

					// effect of .WithLogWriterTiming()
					case 4:
						Assert.IsType<LogWriterConfiguration.ExactNameLogWriterPattern>(pattern);
						Assert.Equal("Timing", pattern.Pattern);
						Assert.Equal("None", writers[i].BaseLevel);
						Assert.Single(writers[i].Includes, "Timing");
						Assert.Empty(writers[i].Excludes);
						Assert.False(writers[i].IsDefault);
						continue;

					// effect of .WithLogWriterDefault()
					case 5:
						Assert.IsType<LogWriterConfiguration.WildcardLogWriterPattern>(pattern);
						Assert.Equal("*", pattern.Pattern);
						break;
				}

				Assert.Equal("Note", writer.BaseLevel);
				Assert.Empty(writer.Includes);
				Assert.Empty(writer.Excludes);
				Assert.False(writer.IsDefault);
			}
		}

		#endregion

		#region Helpers

		private CONFIGURATION GetDefaultConfiguration()
		{
			CONFIGURATION configuration = new CONFIGURATION();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];
			Assert.Single(writer.Patterns);
			var pattern = writer.Patterns.First();
			Assert.Equal("Wildcard: *", pattern.ToString());
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.True(writer.IsDefault);
			return configuration;
		}

		#endregion
	}
}
