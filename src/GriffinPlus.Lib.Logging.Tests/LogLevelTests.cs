///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
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

		private readonly struct LogLevelItem
		{
			public readonly int    Id;
			public readonly string Name;

			public LogLevelItem(int id, string name)
			{
				Id = id;
				Name = name;
			}
		}

		private static readonly LogLevelItem[] sExpectedPredefinedLogLevels =
		{
			new LogLevelItem(0, "Emergency"),
			new LogLevelItem(1, "Alert"),
			new LogLevelItem(2, "Critical"),
			new LogLevelItem(3, "Error"),
			new LogLevelItem(4, "Warning"),
			new LogLevelItem(5, "Notice"),
			new LogLevelItem(6, "Informational"),
			new LogLevelItem(7, "Debug"),
			new LogLevelItem(8, "Trace")
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
			Assert.Equal(sExpectedPredefinedLogLevels[0].Id, LogLevel.Emergency.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[1].Id, LogLevel.Alert.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[2].Id, LogLevel.Critical.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[3].Id, LogLevel.Error.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[4].Id, LogLevel.Warning.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[5].Id, LogLevel.Notice.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[6].Id, LogLevel.Informational.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[7].Id, LogLevel.Debug.Id);
			Assert.Equal(sExpectedPredefinedLogLevels[8].Id, LogLevel.Trace.Id);

			// ensure that the log level name is as expected
			Assert.Equal(sExpectedPredefinedLogLevels[0].Name, LogLevel.Emergency.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[1].Name, LogLevel.Alert.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[2].Name, LogLevel.Critical.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[3].Name, LogLevel.Error.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[4].Name, LogLevel.Warning.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[5].Name, LogLevel.Notice.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[6].Name, LogLevel.Informational.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[7].Name, LogLevel.Debug.Name);
			Assert.Equal(sExpectedPredefinedLogLevels[8].Name, LogLevel.Trace.Name);
		}

		/// <summary>
		/// Checks that the predefined log level enumeration returns all predefined log levels in the proper order.
		/// </summary>
		[Fact]
		public void Check_Predefined_Log_Level_Enumeration()
		{
			var levels = LogLevel.PredefinedLogLevels.ToArray();
			Assert.Equal(sExpectedPredefinedLogLevels.Length, levels.Length);

			for (int i = 0; i < levels.Length; i++)
			{
				Assert.Equal(sExpectedPredefinedLogLevels[i].Id, levels[i].Id);
				Assert.Equal(sExpectedPredefinedLogLevels[i].Name, levels[i].Name);
			}
		}
	}

}
