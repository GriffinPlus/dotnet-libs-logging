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

using System.Linq;
using Xunit;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Unit tests targeting the <see cref="LogLevel"/> class.
	/// </summary>
	public class LogLevelTests
	{
		#region Expected Results

		private struct LogLevelItem
		{
			public readonly int Id;
			public readonly string Name;

			public LogLevelItem(int id, string name)
			{
				Id = id;
				Name = name;
			}
		}

		private static readonly LogLevelItem[] sExpectedPredefinedLogLevels =
		{
			new LogLevelItem( 0, "Failure"),
			new LogLevelItem( 1, "Error"),
			new LogLevelItem( 2, "Warning"),
			new LogLevelItem( 3, "Note"),
			new LogLevelItem( 4, "Developer"),
			new LogLevelItem( 5, "Trace0"),
			new LogLevelItem( 6, "Trace1"),
			new LogLevelItem( 7, "Trace2"),
			new LogLevelItem( 8, "Trace3"),
			new LogLevelItem( 9, "Trace4"),
			new LogLevelItem(10, "Trace5"),
			new LogLevelItem(11, "Trace6"),
			new LogLevelItem(12, "Trace7"),
			new LogLevelItem(13, "Trace8"),
			new LogLevelItem(14, "Trace9"),
			new LogLevelItem(15, "Trace10"),
			new LogLevelItem(16, "Trace11"),
			new LogLevelItem(17, "Trace12"),
			new LogLevelItem(18, "Trace13"),
			new LogLevelItem(19, "Trace14"),
			new LogLevelItem(20, "Trace15"),
			new LogLevelItem(21, "Trace16"),
			new LogLevelItem(22, "Trace17"),
			new LogLevelItem(23, "Trace18"),
			new LogLevelItem(24, "Trace19")
		};

		#endregion

		/// <summary>
		/// Checks whether the special log level 'None' has the expected name and id.
		/// </summary>
		[Fact]
		public void Check_Special_LogLevel_None()
		{
			Assert.Equal(-1, LogLevel.None.Id);
			Assert.Equal("None", LogLevel.None.Name);
		}

		/// <summary>
		/// Checks whether the special log level 'All' has the expected name and id.
		/// </summary>
		[Fact]
		public void Check_Special_LogLevel_All()
		{
			Assert.Equal(int.MaxValue, LogLevel.All.Id);
			Assert.Equal("All", LogLevel.All.Name);
		}

		/// <summary>
		/// Checks whether the static properties providing access to predefined log levels return a log level with the expected name and id.
		/// </summary>
		[Fact]
		public void Check_Predefined_LogLevel_Fields()
		{
			// ensure that the log level id is as expected
			Assert.Equal(sExpectedPredefinedLogLevels[ 0].Id, LogLevel.Failure.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[ 1].Id, LogLevel.Error.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[ 2].Id, LogLevel.Warning.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[ 3].Id, LogLevel.Note.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[ 4].Id, LogLevel.Developer.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[ 5].Id, LogLevel.Trace0.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[ 6].Id, LogLevel.Trace1.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[ 7].Id, LogLevel.Trace2.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[ 8].Id, LogLevel.Trace3.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[ 9].Id, LogLevel.Trace4.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[10].Id, LogLevel.Trace5.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[11].Id, LogLevel.Trace6.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[12].Id, LogLevel.Trace7.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[13].Id, LogLevel.Trace8.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[14].Id, LogLevel.Trace9.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[15].Id, LogLevel.Trace10.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[16].Id, LogLevel.Trace11.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[17].Id, LogLevel.Trace12.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[18].Id, LogLevel.Trace13.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[19].Id, LogLevel.Trace14.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[20].Id, LogLevel.Trace15.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[21].Id, LogLevel.Trace16.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[22].Id, LogLevel.Trace17.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[23].Id, LogLevel.Trace18.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[24].Id, LogLevel.Trace19.Id);

			// ensure that the log level name is as expected
			Assert.Equal(sExpectedPredefinedLogLevels[ 0].Name, LogLevel.Failure.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[ 1].Name, LogLevel.Error.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[ 2].Name, LogLevel.Warning.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[ 3].Name, LogLevel.Note.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[ 4].Name, LogLevel.Developer.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[ 5].Name, LogLevel.Trace0.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[ 6].Name, LogLevel.Trace1.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[ 7].Name, LogLevel.Trace2.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[ 8].Name, LogLevel.Trace3.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[ 9].Name, LogLevel.Trace4.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[10].Name, LogLevel.Trace5.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[11].Name, LogLevel.Trace6.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[12].Name, LogLevel.Trace7.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[13].Name, LogLevel.Trace8.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[14].Name, LogLevel.Trace9.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[15].Name, LogLevel.Trace10.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[16].Name, LogLevel.Trace11.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[17].Name, LogLevel.Trace12.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[18].Name, LogLevel.Trace13.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[19].Name, LogLevel.Trace14.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[20].Name, LogLevel.Trace15.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[21].Name, LogLevel.Trace16.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[22].Name, LogLevel.Trace17.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[23].Name, LogLevel.Trace18.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[24].Name, LogLevel.Trace19.Name);
		}

		/// <summary>
		/// Checks that the predefined log level enumeration returns all predefined log levels in the proper order.
		/// </summary>
		[Fact]
		public void Check_Predefined_Log_Level_Enumeration()
		{
			LogLevel[] levels = LogLevel.PredefinedLogLevels.ToArray();
			Assert.Equal(sExpectedPredefinedLogLevels.Length, levels.Length);

			for (int i = 0; i < levels.Length; i++)
			{
				Assert.Equal(sExpectedPredefinedLogLevels[i].Id, levels[i].Id);
				Assert.Equal(sExpectedPredefinedLogLevels[i].Name, levels[i].Name);
			}
		}
	}
}
