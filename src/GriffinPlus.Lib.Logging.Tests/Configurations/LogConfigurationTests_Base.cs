///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

// ReSharper disable RedundantExplicitArrayCreation

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Common unit tests targeting the <see cref="VolatileLogConfiguration" /> and the <see cref="FileBackedLogConfiguration" /> class.
	/// </summary>
	public abstract class LogConfigurationTests_Base<TConfiguration> where TConfiguration : LogConfiguration<TConfiguration>, new()
	{
		private const string Aspect1Name = "Aspect1";
		private const string Aspect2Name = "Aspect2";
		private const string Aspect3Name = "Aspect3";

		protected LogConfigurationTests_Base()
		{
			var aspect1 = LogLevel.GetAspect(Aspect1Name);
			Assert.Equal(Aspect1Name, aspect1.Name);

			var aspect2 = LogLevel.GetAspect(Aspect2Name);
			Assert.Equal(Aspect2Name, aspect2.Name);

			var aspect3 = LogLevel.GetAspect(Aspect3Name);
			Assert.Equal(Aspect3Name, aspect3.Name);

			// the ids of the log levels should be different
			var ids = new HashSet<int>(new[] { aspect1.Id, aspect2.Id, aspect3.Id });
			Assert.Equal(3, ids.Count);
		}

		[Fact]
		public void Creating_Default_Configuration()
		{
			var configuration = new TConfiguration();

			// global settings
			Assert.Equal(AppDomain.CurrentDomain.FriendlyName, configuration.ApplicationName);

			// pipeline stage settings
			var stageSettings = configuration.ProcessingPipeline.Stages;
			Assert.Empty(stageSettings);

			// log writer settings
			var logWriterSettings = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(logWriterSettings);
			var logWriterSetting = logWriterSettings[0];
			Assert.Equal("Note", logWriterSetting.BaseLevel);
			Assert.Empty(logWriterSetting.Includes);
			Assert.Empty(logWriterSetting.Excludes);
			Assert.Single(logWriterSetting.NamePatterns);
			var pattern = logWriterSetting.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.WildcardNamePattern>(pattern);
			Assert.Equal("*", pattern.Pattern);
			Assert.Empty(logWriterSetting.TagPatterns);
		}

		[Fact]
		public void Setting_ApplicationName()
		{
			var configuration = new TConfiguration { ApplicationName = "My App" };
			Assert.Equal("My App", configuration.ApplicationName);
		}

		[Theory]
		[InlineData("Failure", 0x00000001u)]
		[InlineData("Error", 0x00000003u)]
		[InlineData("Warning", 0x00000007u)]
		[InlineData("Note", 0x0000000Fu)]
		[InlineData("Developer", 0x0000001Fu)]
		[InlineData("Trace0", 0x0000003Fu)]
		[InlineData("Trace1", 0x0000007Fu)]
		[InlineData("Trace2", 0x000000FFu)]
		[InlineData("Trace3", 0x000001FFu)]
		[InlineData("Trace4", 0x000003FFu)]
		[InlineData("Trace5", 0x000007FFu)]
		[InlineData("Trace6", 0x00000FFFu)]
		[InlineData("Trace7", 0x00001FFFu)]
		[InlineData("Trace8", 0x00003FFFu)]
		[InlineData("Trace9", 0x00007FFFu)]
		[InlineData("Trace10", 0x0000FFFFu)]
		[InlineData("Trace11", 0x0001FFFFu)]
		[InlineData("Trace12", 0x0003FFFFu)]
		[InlineData("Trace13", 0x0007FFFFu)]
		[InlineData("Trace14", 0x000FFFFFu)]
		[InlineData("Trace15", 0x001FFFFFu)]
		[InlineData("Trace16", 0x003FFFFFu)]
		[InlineData("Trace17", 0x007FFFFFu)]
		[InlineData("Trace18", 0x00FFFFFFu)]
		[InlineData("Trace19", 0x01FFFFFFu)]
		public void Getting_Active_Log_Level_Mask_For_Specific_BaseLevel(string level, uint expectedMask)
		{
			var configuration = new TConfiguration();
			var settings = new LogWriterConfiguration[1];
			settings[0] = LogWriterConfigurationBuilder
				.New
				.MatchingWildcardPattern("*")
				.WithBaseLevel(level)
				.Build();
			configuration.SetLogWriterSettings(settings);
			var writer = Log.GetWriter("UnitTest");
			var mask = configuration.GetActiveLogLevelMask(writer);
			var bitArray = mask.AsArray();
			Assert.Single(bitArray);
			Assert.Equal(expectedMask, bitArray[0]);
		}

		[Theory]

		// base level only
		[InlineData("None", new string[] { }, new string[] { }, 0x00000000u)]
		[InlineData("All", new string[] { }, new string[] { }, 0xFFFFFFFFu)]

		// includes
		[InlineData("None", new string[] { "Note" }, new string[] { }, 0x00000008u)]   // single predefined level only
		[InlineData("Note", new string[] { "Trace0" }, new string[] { }, 0x0000002Fu)] // base level + predefined level
		// excludes
		[InlineData("All", new string[] { }, new string[] { "Note" }, 0xFFFFFFF7u)] // all except a single level
		[InlineData("Note", new string[] { }, new string[] { "Note" }, 0x00000007u)]
		[InlineData("Note", new string[] { }, new string[] { "Error" }, 0x0000000Du)]

		// mixed
		[InlineData(
			"Developer",
			new string[] { "Error", "Trace0", "Trace19" },
			new string[] { "Error" }, // exclude overrides include for 'Error'
			0x0100003D)]
		public void Getting_Active_Log_Level_Mask_With_Includes_And_Excludes(
			string   baseLevel,
			string[] includes,
			string[] excludes,
			uint     expectedMask)
		{
			var configuration = new TConfiguration();
			var settings = new LogWriterConfiguration[1];
			settings[0] = LogWriterConfigurationBuilder
				.New
				.MatchingWildcardPattern("*")
				.WithBaseLevel(baseLevel)
				.WithLevel(includes)
				.WithoutLevel(excludes)
				.Build();
			configuration.SetLogWriterSettings(settings);
			var writer = Log.GetWriter("UnitTest");
			var mask = configuration.GetActiveLogLevelMask(writer);
			var bitArray = mask.AsArray();
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
			var configuration = GetDefaultConfiguration();

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				configuration.AddLogWriter<TConfiguration>(Assert.NotNull);
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}
			else
			{
				configuration.AddLogWriter<TConfiguration>();
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}

			Assert.Single(writer.NamePatterns);
			var pattern = writer.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.ExactNamePattern>(pattern);
			Assert.Equal($"{typeof(TConfiguration).FullName}", pattern.Pattern);
			Assert.Empty(writer.TagPatterns);
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
			var configuration = GetDefaultConfiguration();
			var type = typeof(TConfiguration);

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				configuration.AddLogWriter(type, Assert.NotNull);
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

			Assert.Single(writer.NamePatterns);
			var pattern = writer.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.ExactNamePattern>(pattern);
			Assert.Equal($"{type.FullName}", pattern.Pattern);
			Assert.Empty(writer.TagPatterns);
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
			var configuration = GetDefaultConfiguration();
			const string wildcard = "MyDemo*";

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				configuration.AddLogWritersByWildcard(wildcard, Assert.NotNull);
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

			Assert.Single(writer.NamePatterns);
			var pattern = writer.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.WildcardNamePattern>(pattern);
			Assert.Equal(wildcard, pattern.Pattern);
			Assert.Empty(writer.TagPatterns);
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
			var configuration = GetDefaultConfiguration();
			const string regex = "^[a-z][A-Z][0-9]$";

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				configuration.AddLogWritersByRegex(regex, Assert.NotNull);
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

			Assert.Single(writer.NamePatterns);
			var pattern = writer.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.RegexNamePattern>(pattern);
			Assert.Equal(regex, pattern.Pattern);
			Assert.Empty(writer.TagPatterns);
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		[Fact]
		public void AddLogWriterTiming()
		{
			var configuration = GetDefaultConfiguration();
			configuration.AddLogWriterTiming();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];
			Assert.Single(writer.NamePatterns);
			var pattern = writer.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.ExactNamePattern>(pattern);
			Assert.Equal("Timing", pattern.Pattern);
			Assert.Empty(writer.TagPatterns);
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
			var configuration = GetDefaultConfiguration();

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				configuration.AddLogWriterDefault(Assert.NotNull);
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

			Assert.Single(writer.NamePatterns);
			var pattern = writer.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.WildcardNamePattern>(pattern);
			Assert.Equal("*", pattern.Pattern);
			Assert.Empty(writer.TagPatterns);
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

			var configuration = GetDefaultConfiguration();

			const string wildcard = "MyDemo*";
			const string regex = "^[a-z][A-Z][0-9]$";

			configuration.AddLogWriter<TConfiguration>();
			configuration.AddLogWriter(typeof(TConfiguration));
			configuration.AddLogWritersByWildcard(wildcard);
			configuration.AddLogWritersByRegex(regex);
			configuration.AddLogWriterTiming();
			configuration.AddLogWriterDefault();

			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Equal(6, writers.Length);

			for (int i = 0; i < writers.Length; i++)
			{
				var writer = writers[i];
				Assert.Single(writer.NamePatterns);
				var pattern = writer.NamePatterns.First();

				switch (i)
				{
					// effect of .WithLogWriter<T>()
					case 0:
						Assert.IsType<LogWriterConfiguration.ExactNamePattern>(pattern);
						Assert.Equal($"{typeof(TConfiguration).FullName}", pattern.Pattern);
						break;

					// effect of .WithLogWriter(typeof(T))
					case 1:
						Assert.IsType<LogWriterConfiguration.ExactNamePattern>(pattern);
						Assert.Equal($"{typeof(TConfiguration).FullName}", pattern.Pattern);
						break;

					// effect of .WithLogWritersByWildcard(wildcard)
					case 2:
						Assert.IsType<LogWriterConfiguration.WildcardNamePattern>(pattern);
						Assert.Equal(wildcard, pattern.Pattern);
						break;

					// effect of WithLogWritersByRegex(regex)
					case 3:
						Assert.IsType<LogWriterConfiguration.RegexNamePattern>(pattern);
						Assert.Equal(regex, pattern.Pattern);
						break;

					// effect of .WithLogWriterTiming()
					case 4:
						Assert.IsType<LogWriterConfiguration.ExactNamePattern>(pattern);
						Assert.Equal("Timing", pattern.Pattern);
						Assert.Equal("None", writers[i].BaseLevel);
						Assert.Single(writers[i].Includes, "Timing");
						Assert.Empty(writers[i].Excludes);
						Assert.False(writers[i].IsDefault);
						continue;

					// effect of .WithLogWriterDefault()
					case 5:
						Assert.IsType<LogWriterConfiguration.WildcardNamePattern>(pattern);
						Assert.Equal("*", pattern.Pattern);
						break;
				}

				Assert.Empty(writer.TagPatterns);
				Assert.Equal("Note", writer.BaseLevel);
				Assert.Empty(writer.Includes);
				Assert.Empty(writer.Excludes);
				Assert.False(writer.IsDefault);
			}
		}

		#endregion

		#region Helpers

		private static TConfiguration GetDefaultConfiguration()
		{
			var configuration = new TConfiguration();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];
			Assert.Single(writer.NamePatterns);
			var pattern = writer.NamePatterns.First();
			Assert.Equal("Wildcard: *", pattern.ToString());
			Assert.Empty(writer.TagPatterns);
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.True(writer.IsDefault);
			return configuration;
		}

		#endregion
	}

}
