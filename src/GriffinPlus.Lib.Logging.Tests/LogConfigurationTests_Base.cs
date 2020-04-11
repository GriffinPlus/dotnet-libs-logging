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
	public abstract class LogConfigurationTests_Base<T> where T: LogConfiguration, new()
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
			T configuration = new T();

			// global settings
			Assert.Equal(AppDomain.CurrentDomain.FriendlyName, configuration.ApplicationName);

			// pipeline stage settings
			var stageSettings = configuration.GetProcessingPipelineStageSettings();
			Assert.Empty(stageSettings);

			// log writer settings
			var logWriterSettings = configuration.GetLogWriterSettings();
			Assert.Single(logWriterSettings);
			Assert.Equal("Note", logWriterSettings.First().BaseLevel);
			Assert.Empty(logWriterSettings.First().Includes);
			Assert.Empty(logWriterSettings.First().Excludes);
			Assert.IsType<LogConfiguration.WildcardLogWriterPattern>(logWriterSettings.First().Pattern);
			Assert.Equal("*", logWriterSettings.First().Pattern.Pattern);
		}

		[Fact]
		public void Setting_ApplicationName()
		{
			T configuration = new T();
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
			T configuration = new T();
			configuration.SetLogWriterSettings(new LogConfiguration.LogWriter(
				new LogConfiguration.WildcardLogWriterPattern("*"),
				level,
				null,
				null));

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
			T configuration = new T();
			configuration.SetLogWriterSettings(new LogConfiguration.LogWriter(
				new LogConfiguration.WildcardLogWriterPattern("*"),
				baseLevel,
				includes,
				excludes));

			LogWriter writer = Log.GetWriter("UnitTest");
			LogLevelBitMask mask = configuration.GetActiveLogLevelMask(writer);
			uint[] bitArray = mask.AsArray();
			Assert.Single(bitArray);
			Assert.Equal(expectedMask, bitArray[0]);
		}

		[Fact]
		public abstract void Saving_Default_Configuration();
	}
}
