///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Linq;

using Xunit;

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable JoinDeclarationAndInitializer

namespace GriffinPlus.Lib.Logging
{

	public class LogWriterConfigurationBuilderTests
	{
		#region MatchingExactly()

		/// <summary>
		/// Tests adding a matching pattern for an exact log writer name using <see cref="LogWriterConfigurationBuilder.MatchingExactly(string)"/>.
		/// </summary>
		[Fact]
		public void MatchingExactly()
		{
			var builder = LogWriterConfigurationBuilder.New.MatchingExactly("MyLogWriter");
			var writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Single(writer.NamePatterns);
			var pattern = writer.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.ExactNamePattern>(pattern);
			Assert.Equal("MyLogWriter", pattern.Pattern);
			Assert.Empty(writer.TagPatterns);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		#endregion

		#region MatchingWildcardPattern()

		/// <summary>
		/// Tests adding a wildcard matching pattern using <see cref="LogWriterConfigurationBuilder.MatchingWildcardPattern(string)"/>.
		/// </summary>
		[Fact]
		public void MatchingWildcardPattern()
		{
			const string wildcard = "MyLogWriter*";
			var builder = LogWriterConfigurationBuilder.New.MatchingWildcardPattern(wildcard);
			var writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Single(writer.NamePatterns);
			var pattern = writer.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.WildcardNamePattern>(pattern);
			Assert.Equal(wildcard, pattern.Pattern);
			Assert.Empty(writer.TagPatterns);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		#endregion

		#region MatchingRegex()

		/// <summary>
		/// Tests adding a regex pattern using <see cref="LogWriterConfigurationBuilder.MatchingRegex(string)"/>.
		/// </summary>
		[Fact]
		public void MatchingRegex()
		{
			const string regex = "^My.*LogWriter$";
			var builder = LogWriterConfigurationBuilder.New.MatchingRegex(regex);
			var writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Single(writer.NamePatterns);
			var pattern = writer.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.RegexNamePattern>(pattern);
			Assert.Equal(regex, pattern.Pattern);
			Assert.Empty(writer.TagPatterns);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		#endregion

		#region WithTag()

		/// <summary>
		/// Tests adding a matching pattern for an exact log writer name using <see cref="LogWriterConfigurationBuilder.WithTag(string)"/>.
		/// </summary>
		[Fact]
		public void WithTag()
		{
			var builder = LogWriterConfigurationBuilder.New.WithTag("MyTag");
			var writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);

			Assert.Single(writer.NamePatterns);
			var pattern1 = writer.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.WildcardNamePattern>(pattern1);
			Assert.Equal("*", pattern1.Pattern);

			Assert.Single(writer.TagPatterns);
			var pattern2 = writer.TagPatterns.First();
			Assert.IsType<LogWriterConfiguration.ExactNamePattern>(pattern2);
			Assert.Equal("MyTag", pattern2.Pattern);

			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		#endregion

		#region WithTagWildcardPattern()

		/// <summary>
		/// Tests adding a wildcard matching pattern using <see cref="LogWriterConfigurationBuilder.WithTagWildcardPattern(string)"/>.
		/// </summary>
		[Fact]
		public void WithTagWildcardPattern()
		{
			const string wildcard = "My*";
			var builder = LogWriterConfigurationBuilder.New.WithTagWildcardPattern(wildcard);
			var writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);

			Assert.Single(writer.NamePatterns);
			var pattern1 = writer.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.WildcardNamePattern>(pattern1);
			Assert.Equal("*", pattern1.Pattern);

			Assert.Single(writer.TagPatterns);
			var pattern = writer.TagPatterns.First();
			Assert.IsType<LogWriterConfiguration.WildcardNamePattern>(pattern);
			Assert.Equal(wildcard, pattern.Pattern);

			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		#endregion

		#region WithTagRegex()

		/// <summary>
		/// Tests adding a regex pattern using <see cref="LogWriterConfigurationBuilder.WithTagRegex(string)"/>.
		/// </summary>
		[Fact]
		public void WithTagRegex()
		{
			const string regex = "^My.*Tag$";
			var builder = LogWriterConfigurationBuilder.New.WithTagRegex(regex);
			var writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);

			Assert.Single(writer.NamePatterns);
			var pattern1 = writer.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.WildcardNamePattern>(pattern1);
			Assert.Equal("*", pattern1.Pattern);

			Assert.Single(writer.TagPatterns);
			var pattern2 = writer.TagPatterns.First();
			Assert.IsType<LogWriterConfiguration.RegexNamePattern>(pattern2);
			Assert.Equal(regex, pattern2.Pattern);

			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		#endregion

		#region WithBaseLevel()

		/// <summary>
		/// Tests changing the base level using <see cref="LogWriterConfigurationBuilder.WithBaseLevel(LogLevel)"/>.
		/// </summary>
		[Fact]
		public void WithBaseLogLevel_LevelAsLogLevel()
		{
			var builder = LogWriterConfigurationBuilder.New;
			var levels = LogLevel.KnownLevels;
			for (int i = 0; i < levels.Count; i++)
			{
				Assert.Same(builder, builder.WithBaseLevel(levels[i])); // <- level as LogLevel
				var writer = builder.Build();
				Assert.Equal(levels[i].Name, writer.BaseLevel);
			}
		}

		/// <summary>
		/// Tests changing the base level using <see cref="LogWriterConfigurationBuilder.WithBaseLevel(string)"/>.
		/// </summary>
		[Fact]
		public void WithBaseLogLevel_LevelAsString()
		{
			var builder = LogWriterConfigurationBuilder.New;
			var levels = LogLevel.KnownLevels;
			for (int i = 0; i < levels.Count; i++)
			{
				Assert.Same(builder, builder.WithBaseLevel(levels[i].Name)); // <- level as string
				var writer = builder.Build();
				Assert.Equal(levels[i].Name, writer.BaseLevel);
			}
		}

		#endregion

		#region WithLevel()

		/// <summary>
		/// Tests successively including log levels using <see cref="LogWriterConfigurationBuilder.WithLevel(LogLevel[])"/>.
		/// </summary>
		[Fact]
		public void WithLevel_AddOnly()
		{
			var builder = LogWriterConfigurationBuilder.New;
			var levels = LogLevel.KnownLevels;
			for (int i = 0; i < levels.Count; i++)
			{
				Assert.Same(builder, builder.WithLevel(levels[i])); // <- level as LogLevel
				var writer = builder.Build();
				Assert.Equal("Notice", writer.BaseLevel);
				Assert.Equal(levels.Take(i + 1).Select(x => x.Name), writer.Includes);
				Assert.Empty(writer.Excludes);
			}
		}

		/// <summary>
		/// Tests successively including log levels using <see cref="LogWriterConfigurationBuilder.WithLevel(string[])"/>.
		/// </summary>
		[Fact]
		public void WithLevel_AddOnly_LevelAsString()
		{
			var builder = LogWriterConfigurationBuilder.New;
			var levels = LogLevel.KnownLevels;
			for (int i = 0; i < levels.Count; i++)
			{
				Assert.Same(builder, builder.WithLevel(levels[i].Name)); // <- level as string
				var writer = builder.Build();
				Assert.Equal("Notice", writer.BaseLevel);
				Assert.Equal(levels.Take(i + 1).Select(x => x.Name), writer.Includes);
				Assert.Empty(writer.Excludes);
			}
		}

		/// <summary>
		/// Tests whether including log levels using <see cref="LogWriterConfigurationBuilder.WithLevel(string[])"/> removes
		/// the same log levels from the list of excluded log levels.
		/// </summary>
		[Fact]
		public void WithLevel_AddRemovesExclude_LevelAsLogLevel()
		{
			var builder = LogWriterConfigurationBuilder.New;
			LogWriterConfiguration writer;

			var allLevels = LogLevel.KnownLevels.ToArray();

			// populate the exclude list with all known log levels
			Assert.Same(builder, builder.WithoutLevel(allLevels));
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Equal(LogLevel.KnownLevels.Select(x => x.Name), writer.Excludes);

			// include the first log level
			// => log level should automatically be removed from the exclude list
			Assert.Same(builder, builder.WithLevel(allLevels[0])); // level as LogLevel
			writer = builder.Build();
			Assert.Single(writer.Includes);
			Assert.Equal(allLevels.First().Name, writer.Includes.First());
			Assert.Equal(allLevels.Skip(1).Take(allLevels.Length - 1).Select(x => x.Name), writer.Excludes);

			// include the last log level
			// => log level should automatically be removed from the exclude list
			Assert.Same(builder, builder.WithLevel(allLevels[allLevels.Length - 1])); // level as LogLevel
			writer = builder.Build();
			Assert.Equal(2, writer.Includes.Count());
			Assert.Equal(allLevels.First().Name, writer.Includes.First());
			Assert.Equal(allLevels.Last().Name, writer.Includes.Skip(1).First());
			Assert.Equal(allLevels.Skip(1).Take(allLevels.Length - 2).Select(x => x.Name), writer.Excludes);
		}

		/// <summary>
		/// Tests whether including log levels using <see cref="LogWriterConfigurationBuilder.WithLevel(string[])"/> removes
		/// the same log levels from the list of excluded log levels.
		/// </summary>
		[Fact]
		public void WithLevel_AddRemovesExclude_LevelAsString()
		{
			var builder = LogWriterConfigurationBuilder.New;
			LogWriterConfiguration writer;

			var allLevels = LogLevel.KnownLevels.ToArray();

			// populate the exclude list with all known log levels
			Assert.Same(builder, builder.WithoutLevel(allLevels));
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Equal(LogLevel.KnownLevels.Select(x => x.Name), writer.Excludes);

			// include the first log level
			// => log level should automatically be removed from the exclude list
			Assert.Same(builder, builder.WithLevel(allLevels[0].Name)); // level as string
			writer = builder.Build();
			Assert.Single(writer.Includes);
			Assert.Equal(allLevels.First().Name, writer.Includes.First());
			Assert.Equal(allLevels.Skip(1).Take(allLevels.Length - 1).Select(x => x.Name), writer.Excludes);

			// include the last log level
			// => log level should automatically be removed from the exclude list
			Assert.Same(builder, builder.WithLevel(allLevels[allLevels.Length - 1].Name)); // level as string
			writer = builder.Build();
			Assert.Equal(2, writer.Includes.Count());
			Assert.Equal(allLevels.First().Name, writer.Includes.First());
			Assert.Equal(allLevels.Last().Name, writer.Includes.Skip(1).First());
			Assert.Equal(allLevels.Skip(1).Take(allLevels.Length - 2).Select(x => x.Name), writer.Excludes);
		}

		#endregion

		#region WithLevelRange()

		/// <summary>
		/// Tests including a range of sequential log levels using <see cref="LogWriterConfigurationBuilder.WithLevelRange(LogLevel, LogLevel)"/>.
		/// </summary>
		/// <param name="fromLevelName">Name of the first log level in the range.</param>
		/// <param name="toLevelName">Name of the last log level in the range.</param>
		[Theory]
		[InlineData("Emergency", "Emergency")] // include 'Emergency' only
		[InlineData("Emergency", "Alert")]     // include 'Emergency' and 'Alert'
		[InlineData("Emergency", "Critical")]  // include 'Emergency', 'Alert' and 'Critical'
		[InlineData("Emergency", "Error")]     // include 'Emergency', 'Alert', 'Critical' and 'Error'
		[InlineData("Emergency", "Warning")]   // include 'Emergency', 'Alert', 'Critical', 'Error' and 'Warning'
		[InlineData("Alert", "Alert")]         // include 'Alert' only
		[InlineData("Alert", "Critical")]      // include 'Alert' and 'Critical'
		[InlineData("Alert", "Error")]         // include 'Alert', 'Critical' and 'Error'
		public void WithLevelRange_AddOnly_LevelAsLogLevel(string fromLevelName, string toLevelName)
		{
			var builder = LogWriterConfigurationBuilder.New;
			LogWriterConfiguration writer;

			var fromLevel = LogLevel.GetAspect(fromLevelName);
			var toLevel = LogLevel.GetAspect(toLevelName);
			Assert.Equal(fromLevelName, fromLevel.Name);
			Assert.Equal(toLevelName, toLevel.Name);
			Assert.True(fromLevel.Id <= toLevel.Id);

			var levels = LogLevel.KnownLevels
				.Where(x => x.Id >= fromLevel.Id && x.Id <= toLevel.Id)
				.ToArray();

			// from small level id to big level id
			Assert.Same(builder, builder.WithLevelRange(fromLevel, toLevel)); // <- level as LogLevel
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Equal(levels.Select(x => x.Name), writer.Includes);
			Assert.Empty(writer.Excludes);

			// from big level id to small level id (order should be swapped automatically)
			Assert.Same(builder, builder.WithLevelRange(toLevel, fromLevel)); // <- level as LogLevel
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Equal(levels.Select(x => x.Name), writer.Includes);
			Assert.Empty(writer.Excludes);
		}

		/// <summary>
		/// Tests including a range of sequential log levels using <see cref="LogWriterConfigurationBuilder.WithLevelRange(string, string)"/>.
		/// </summary>
		/// <param name="fromLevelName">Name of the first log level in the range.</param>
		/// <param name="toLevelName">Name of the last log level in the range.</param>
		[Theory]
		[InlineData("Emergency", "Emergency")] // include 'Emergency' only
		[InlineData("Emergency", "Alert")]     // include 'Emergency' and 'Alert'
		[InlineData("Emergency", "Critical")]  // include 'Emergency', 'Alert' and 'Critical'
		[InlineData("Emergency", "Error")]     // include 'Emergency', 'Alert', 'Critical' and 'Error'
		[InlineData("Emergency", "Warning")]   // include 'Emergency', 'Alert', 'Critical', 'Error' and 'Warning'
		[InlineData("Alert", "Alert")]         // include 'Alert' only
		[InlineData("Alert", "Critical")]      // include 'Alert' and 'Critical'
		[InlineData("Alert", "Error")]         // include 'Alert', 'Critical' and 'Error'
		public void WithLevelRange_AddOnly_LevelAsString(string fromLevelName, string toLevelName)
		{
			var builder = LogWriterConfigurationBuilder.New;
			LogWriterConfiguration writer;

			var fromLevel = LogLevel.GetAspect(fromLevelName);
			var toLevel = LogLevel.GetAspect(toLevelName);
			Assert.Equal(fromLevelName, fromLevel.Name);
			Assert.Equal(toLevelName, toLevel.Name);
			Assert.True(fromLevel.Id <= toLevel.Id);

			var levels = LogLevel.KnownLevels
				.Where(x => x.Id >= fromLevel.Id && x.Id <= toLevel.Id)
				.ToArray();

			// from small level id to big level id
			Assert.Same(builder, builder.WithLevelRange(fromLevel.Name, toLevel.Name)); // <- level as string
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Equal(levels.Select(x => x.Name), writer.Includes);
			Assert.Empty(writer.Excludes);

			// from big level id to small level id (order should be swapped automatically)
			Assert.Same(builder, builder.WithLevelRange(toLevel.Name, fromLevel.Name)); // <- level as string
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Equal(levels.Select(x => x.Name), writer.Includes);
			Assert.Empty(writer.Excludes);
		}

		/// <summary>
		/// Tests whether including a range of sequential log levels using <see cref="LogWriterConfigurationBuilder.WithLevelRange(LogLevel, LogLevel)"/>
		/// removes the same log levels from the list of excluded log levels.
		/// </summary>
		[Theory]
		[InlineData("Emergency", "Emergency")] // include 'Emergency' only
		[InlineData("Emergency", "Alert")]     // include 'Emergency' and 'Alert'
		[InlineData("Emergency", "Critical")]  // include 'Emergency', 'Alert' and 'Critical'
		[InlineData("Emergency", "Error")]     // include 'Emergency', 'Alert', 'Critical' and 'Error'
		[InlineData("Emergency", "Warning")]   // include 'Emergency', 'Alert', 'Critical', 'Error' and 'Warning'
		[InlineData("Alert", "Alert")]         // include 'Alert' only
		[InlineData("Alert", "Critical")]      // include 'Alert' and 'Critical'
		[InlineData("Alert", "Error")]         // include 'Alert', 'Critical' and 'Error'
		public void WithLevelRange_AddRemovesExclude_LevelAsLogLevel(string fromLevelName, string toLevelName)
		{
			var builder = LogWriterConfigurationBuilder.New;
			LogWriterConfiguration writer;

			var fromLevel = LogLevel.GetAspect(fromLevelName);
			var toLevel = LogLevel.GetAspect(toLevelName);
			Assert.Equal(fromLevelName, fromLevel.Name);
			Assert.Equal(toLevelName, toLevel.Name);
			Assert.True(fromLevel.Id <= toLevel.Id);

			var allLevels = LogLevel.KnownLevels.ToArray();

			// populate the exclude list with all known log levels
			Assert.Same(builder, builder.WithoutLevel(allLevels));
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Equal(LogLevel.KnownLevels.Select(x => x.Name), writer.Excludes);

			// determine log levels that should be included and those that should be excluded
			var levelsToInclude = allLevels.Where(x => x.Id >= fromLevel.Id && x.Id <= toLevel.Id).ToArray();
			var levelsToExclude = allLevels.Where(x => !levelsToInclude.Contains(x)).ToArray();

			// include the specified levels
			// => log levels should automatically be removed from the exclude list
			Assert.Same(builder, builder.WithLevelRange(fromLevel, toLevel)); // level as LogLevel
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Equal(levelsToInclude.Select(x => x.Name), writer.Includes);
			Assert.Equal(levelsToExclude.Select(x => x.Name), writer.Excludes);
		}

		/// <summary>
		/// Tests whether including a range of sequential log levels using <see cref="LogWriterConfigurationBuilder.WithLevelRange(string, string)"/>
		/// removes the same log levels from the list of excluded log levels.
		/// </summary>
		[Theory]
		[InlineData("Emergency", "Emergency")] // include 'Emergency' only
		[InlineData("Emergency", "Alert")]     // include 'Emergency' and 'Alert'
		[InlineData("Emergency", "Critical")]  // include 'Emergency', 'Alert' and 'Critical'
		[InlineData("Emergency", "Error")]     // include 'Emergency', 'Alert', 'Critical' and 'Error'
		[InlineData("Emergency", "Warning")]   // include 'Emergency', 'Alert', 'Critical', 'Error' and 'Warning'
		[InlineData("Alert", "Alert")]         // include 'Alert' only
		[InlineData("Alert", "Critical")]      // include 'Alert' and 'Critical'
		[InlineData("Alert", "Error")]         // include 'Alert', 'Critical' and 'Error'
		public void WithLevelRange_AddRemovesExclude_LevelAsString(string fromLevelName, string toLevelName)
		{
			var builder = LogWriterConfigurationBuilder.New;
			LogWriterConfiguration writer;

			var fromLevel = LogLevel.GetAspect(fromLevelName);
			var toLevel = LogLevel.GetAspect(toLevelName);
			Assert.Equal(fromLevelName, fromLevel.Name);
			Assert.Equal(toLevelName, toLevel.Name);
			Assert.True(fromLevel.Id <= toLevel.Id);

			var allLevels = LogLevel.KnownLevels.ToArray();

			// populate the exclude list with all known log levels
			Assert.Same(builder, builder.WithoutLevel(allLevels));
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Equal(LogLevel.KnownLevels.Select(x => x.Name), writer.Excludes);

			// determine log levels that should be included and those that should be excluded
			var levelsToInclude = allLevels.Where(x => x.Id >= fromLevel.Id && x.Id <= toLevel.Id).ToArray();
			var levelsToExclude = allLevels.Where(x => !levelsToInclude.Contains(x)).ToArray();

			// include the specified levels
			// => log levels should automatically be removed from the exclude list
			Assert.Same(builder, builder.WithLevelRange(fromLevel.Name, toLevel.Name)); // level as string
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Equal(levelsToInclude.Select(x => x.Name), writer.Includes);
			Assert.Equal(levelsToExclude.Select(x => x.Name), writer.Excludes);
		}

		#endregion

		#region WithoutLevel()

		/// <summary>
		/// Tests successively excluding log levels using <see cref="LogWriterConfigurationBuilder.WithoutLevel(LogLevel[])"/>.
		/// </summary>
		[Fact]
		public void WithoutLevel_AddOnly()
		{
			var builder = LogWriterConfigurationBuilder.New;
			var levels = LogLevel.KnownLevels;
			for (int i = 0; i < levels.Count; i++)
			{
				Assert.Same(builder, builder.WithoutLevel(levels[i])); // <- level as LogLevel
				var writer = builder.Build();
				Assert.Equal("Notice", writer.BaseLevel);
				Assert.Empty(writer.Includes);
				Assert.Equal(levels.Take(i + 1).Select(x => x.Name), writer.Excludes);
			}
		}

		/// <summary>
		/// Tests successively excluding log levels using <see cref="LogWriterConfigurationBuilder.WithoutLevel(string[])"/>.
		/// </summary>
		[Fact]
		public void WithoutLevel_AddOnly_LevelAsString()
		{
			var builder = LogWriterConfigurationBuilder.New;
			var levels = LogLevel.KnownLevels;
			for (int i = 0; i < levels.Count; i++)
			{
				Assert.Same(builder, builder.WithoutLevel(levels[i].Name)); // <- level as string
				var writer = builder.Build();
				Assert.Equal("Notice", writer.BaseLevel);
				Assert.Empty(writer.Includes);
				Assert.Equal(levels.Take(i + 1).Select(x => x.Name), writer.Excludes);
			}
		}

		/// <summary>
		/// Tests whether excluding log levels using <see cref="LogWriterConfigurationBuilder.WithoutLevel(string[])"/> removes
		/// the same log levels from the list of included log levels.
		/// </summary>
		[Fact]
		public void WithoutLevel_AddRemovesInclude_LevelAsLogLevel()
		{
			var builder = LogWriterConfigurationBuilder.New;
			LogWriterConfiguration writer;

			var allLevels = LogLevel.KnownLevels.ToArray();

			// populate the include list with all known log levels
			Assert.Same(builder, builder.WithLevel(allLevels));
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Equal(LogLevel.KnownLevels.Select(x => x.Name), writer.Includes);
			Assert.Empty(writer.Excludes);

			// exclude the first log level
			// => log level should automatically be removed from the include list
			Assert.Same(builder, builder.WithoutLevel(allLevels[0])); // level as LogLevel
			writer = builder.Build();
			Assert.Equal(allLevels.Skip(1).Take(allLevels.Length - 1).Select(x => x.Name), writer.Includes);
			Assert.Single(writer.Excludes);
			Assert.Equal(allLevels.First().Name, writer.Excludes.First());

			// exclude the last log level
			// => log level should automatically be removed from the include list
			Assert.Same(builder, builder.WithoutLevel(allLevels[allLevels.Length - 1])); // level as LogLevel
			writer = builder.Build();
			Assert.Equal(allLevels.Skip(1).Take(allLevels.Length - 2).Select(x => x.Name), writer.Includes);
			Assert.Equal(2, writer.Excludes.Count());
			Assert.Equal(allLevels.First().Name, writer.Excludes.First());
			Assert.Equal(allLevels.Last().Name, writer.Excludes.Skip(1).First());
		}

		/// <summary>
		/// Tests whether excluding log levels using <see cref="LogWriterConfigurationBuilder.WithoutLevel(string[])"/> removes
		/// the same log levels from the list of included log levels.
		/// </summary>
		[Fact]
		public void WithoutLevel_AddRemovesInclude_LevelAsString()
		{
			var builder = LogWriterConfigurationBuilder.New;
			LogWriterConfiguration writer;

			var allLevels = LogLevel.KnownLevels.ToArray();

			// populate the include list with all known log levels
			Assert.Same(builder, builder.WithLevel(allLevels));
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Equal(LogLevel.KnownLevels.Select(x => x.Name), writer.Includes);
			Assert.Empty(writer.Excludes);

			// exclude the first log level
			// => log level should automatically be removed from the include list
			Assert.Same(builder, builder.WithoutLevel(allLevels[0].Name)); // level as string
			writer = builder.Build();
			Assert.Equal(allLevels.Skip(1).Take(allLevels.Length - 1).Select(x => x.Name), writer.Includes);
			Assert.Single(writer.Excludes);
			Assert.Equal(allLevels.First().Name, writer.Excludes.First());

			// exclude the last log level
			// => log level should automatically be removed from the include list
			Assert.Same(builder, builder.WithoutLevel(allLevels[allLevels.Length - 1].Name)); // level as string
			writer = builder.Build();
			Assert.Equal(allLevels.Skip(1).Take(allLevels.Length - 2).Select(x => x.Name), writer.Includes);
			Assert.Equal(2, writer.Excludes.Count());
			Assert.Equal(allLevels.First().Name, writer.Excludes.First());
			Assert.Equal(allLevels.Last().Name, writer.Excludes.Skip(1).First());
		}

		#endregion

		#region WithoutLevelRange()

		/// <summary>
		/// Tests excluding a range of sequential log levels using <see cref="LogWriterConfigurationBuilder.WithoutLevelRange(LogLevel, LogLevel)"/>.
		/// </summary>
		/// <param name="fromLevelName">Name of the first log level in the range.</param>
		/// <param name="toLevelName">Name of the last log level in the range.</param>
		[Theory]
		[InlineData("Emergency", "Emergency")] // exclude 'Emergency' only
		[InlineData("Emergency", "Alert")]     // exclude 'Emergency' and 'Alert'
		[InlineData("Emergency", "Critical")]  // exclude 'Emergency', 'Alert' and 'Critical'
		[InlineData("Emergency", "Error")]     // exclude 'Emergency', 'Alert', 'Critical' and 'Error'
		[InlineData("Emergency", "Warning")]   // exclude 'Emergency', 'Alert', 'Critical', 'Error' and 'Warning'
		[InlineData("Alert", "Alert")]         // exclude 'Alert' only
		[InlineData("Alert", "Critical")]      // exclude 'Alert' and 'Critical'
		[InlineData("Alert", "Error")]         // exclude 'Alert', 'Critical' and 'Error'
		public void WithoutLevelRange_AddOnly_LevelAsLogLevel(string fromLevelName, string toLevelName)
		{
			var builder = LogWriterConfigurationBuilder.New;
			LogWriterConfiguration writer;

			var fromLevel = LogLevel.GetAspect(fromLevelName);
			var toLevel = LogLevel.GetAspect(toLevelName);
			Assert.Equal(fromLevelName, fromLevel.Name);
			Assert.Equal(toLevelName, toLevel.Name);
			Assert.True(fromLevel.Id <= toLevel.Id);

			var levels = LogLevel.KnownLevels
				.Where(x => x.Id >= fromLevel.Id && x.Id <= toLevel.Id)
				.ToArray();

			// from small level id to big level id
			Assert.Same(builder, builder.WithoutLevelRange(fromLevel, toLevel)); // <- level as LogLevel
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Equal(levels.Select(x => x.Name), writer.Excludes);

			// from big level id to small level id (order should be swapped automatically)
			Assert.Same(builder, builder.WithoutLevelRange(toLevel, fromLevel)); // <- level as LogLevel
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Equal(levels.Select(x => x.Name), writer.Excludes);
		}

		/// <summary>
		/// Tests excluding a range of sequential log levels using <see cref="LogWriterConfigurationBuilder.WithoutLevelRange(string, string)"/>.
		/// </summary>
		/// <param name="fromLevelName">Name of the first log level in the range.</param>
		/// <param name="toLevelName">Name of the last log level in the range.</param>
		[Theory]
		[InlineData("Emergency", "Emergency")] // exclude 'Emergency' only
		[InlineData("Emergency", "Alert")]     // exclude 'Emergency' and 'Alert'
		[InlineData("Emergency", "Critical")]  // exclude 'Emergency', 'Alert' and 'Critical'
		[InlineData("Emergency", "Error")]     // exclude 'Emergency', 'Alert', 'Critical' and 'Error'
		[InlineData("Emergency", "Warning")]   // exclude 'Emergency', 'Alert', 'Critical', 'Error' and 'Warning'
		[InlineData("Alert", "Alert")]         // exclude 'Alert' only
		[InlineData("Alert", "Critical")]      // exclude 'Alert' and 'Critical'
		[InlineData("Alert", "Error")]         // exclude 'Alert', 'Critical' and 'Error'
		public void WithoutLevelRange_AddOnly_LevelAsString(string fromLevelName, string toLevelName)
		{
			var builder = LogWriterConfigurationBuilder.New;
			LogWriterConfiguration writer;

			var fromLevel = LogLevel.GetAspect(fromLevelName);
			var toLevel = LogLevel.GetAspect(toLevelName);
			Assert.Equal(fromLevelName, fromLevel.Name);
			Assert.Equal(toLevelName, toLevel.Name);
			Assert.True(fromLevel.Id <= toLevel.Id);

			var levels = LogLevel.KnownLevels
				.Where(x => x.Id >= fromLevel.Id && x.Id <= toLevel.Id)
				.ToArray();

			// from small level id to big level id
			Assert.Same(builder, builder.WithoutLevelRange(fromLevel.Name, toLevel.Name)); // <- level as string
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Equal(levels.Select(x => x.Name), writer.Excludes);

			// from big level id to small level id (order should be swapped automatically)
			Assert.Same(builder, builder.WithoutLevelRange(toLevel.Name, fromLevel.Name)); // <- level as string
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Equal(levels.Select(x => x.Name), writer.Excludes);
		}

		/// <summary>
		/// Tests whether excluding a range of sequential log levels using <see cref="LogWriterConfigurationBuilder.WithoutLevelRange(LogLevel, LogLevel)"/>
		/// removes the same log levels from the list of included log levels.
		/// </summary>
		[Theory]
		[InlineData("Emergency", "Emergency")] // exclude 'Emergency' only
		[InlineData("Emergency", "Alert")]     // exclude 'Emergency' and 'Alert'
		[InlineData("Emergency", "Critical")]  // exclude 'Emergency', 'Alert' and 'Critical'
		[InlineData("Emergency", "Error")]     // exclude 'Emergency', 'Alert', 'Critical' and 'Error'
		[InlineData("Emergency", "Warning")]   // exclude 'Emergency', 'Alert', 'Critical', 'Error' and 'Warning'
		[InlineData("Alert", "Alert")]         // exclude 'Alert' only
		[InlineData("Alert", "Critical")]      // exclude 'Alert' and 'Critical'
		[InlineData("Alert", "Error")]         // exclude 'Alert', 'Critical' and 'Error'
		public void WithoutLevelRange_AddRemovesInclude_LevelAsLogLevel(string fromLevelName, string toLevelName)
		{
			var builder = LogWriterConfigurationBuilder.New;
			LogWriterConfiguration writer;

			var fromLevel = LogLevel.GetAspect(fromLevelName);
			var toLevel = LogLevel.GetAspect(toLevelName);
			Assert.Equal(fromLevelName, fromLevel.Name);
			Assert.Equal(toLevelName, toLevel.Name);
			Assert.True(fromLevel.Id <= toLevel.Id);

			var allLevels = LogLevel.KnownLevels.ToArray();

			// populate the include list with all known log levels
			Assert.Same(builder, builder.WithLevel(allLevels));
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Equal(LogLevel.KnownLevels.Select(x => x.Name), writer.Includes);
			Assert.Empty(writer.Excludes);

			// determine log levels that should be included and those that should be excluded
			var levelsToExclude = allLevels.Where(x => x.Id >= fromLevel.Id && x.Id <= toLevel.Id).ToArray();
			var levelsToInclude = allLevels.Where(x => !levelsToExclude.Contains(x)).ToArray();

			// include the specified levels
			// => log levels should automatically be removed from the exclude list
			Assert.Same(builder, builder.WithoutLevelRange(fromLevel, toLevel)); // level as LogLevel
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Equal(levelsToInclude.Select(x => x.Name), writer.Includes);
			Assert.Equal(levelsToExclude.Select(x => x.Name), writer.Excludes);
		}

		/// <summary>
		/// Tests whether excluding a range of sequential log levels using <see cref="LogWriterConfigurationBuilder.WithoutLevelRange(string, string)"/>
		/// removes the same log levels from the list of included log levels.
		/// </summary>
		[Theory]
		[InlineData("Emergency", "Emergency")] // exclude 'Emergency' only
		[InlineData("Emergency", "Alert")]     // exclude 'Emergency' and 'Alert'
		[InlineData("Emergency", "Critical")]  // exclude 'Emergency', 'Alert' and 'Critical'
		[InlineData("Emergency", "Error")]     // exclude 'Emergency', 'Alert', 'Critical' and 'Error'
		[InlineData("Emergency", "Warning")]   // exclude 'Emergency', 'Alert', 'Critical', 'Error' and 'Warning'
		[InlineData("Alert", "Alert")]         // exclude 'Alert' only
		[InlineData("Alert", "Critical")]      // exclude 'Alert' and 'Critical'
		[InlineData("Alert", "Error")]         // exclude 'Alert', 'Critical' and 'Error'
		public void WithoutLevelRange_AddRemovesInclude_LevelAsString(string fromLevelName, string toLevelName)
		{
			var builder = LogWriterConfigurationBuilder.New;
			LogWriterConfiguration writer;

			var fromLevel = LogLevel.GetAspect(fromLevelName);
			var toLevel = LogLevel.GetAspect(toLevelName);
			Assert.Equal(fromLevelName, fromLevel.Name);
			Assert.Equal(toLevelName, toLevel.Name);
			Assert.True(fromLevel.Id <= toLevel.Id);

			var allLevels = LogLevel.KnownLevels.ToArray();

			// populate the include list with all known log levels
			Assert.Same(builder, builder.WithLevel(allLevels));
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Equal(LogLevel.KnownLevels.Select(x => x.Name), writer.Includes);
			Assert.Empty(writer.Excludes);

			// determine log levels that should be included and those that should be excluded
			var levelsToExclude = allLevels.Where(x => x.Id >= fromLevel.Id && x.Id <= toLevel.Id).ToArray();
			var levelsToInclude = allLevels.Where(x => !levelsToExclude.Contains(x)).ToArray();

			// include the specified levels
			// => log levels should automatically be removed from the exclude list
			Assert.Same(builder, builder.WithoutLevelRange(fromLevel.Name, toLevel.Name)); // level as string
			writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Equal(levelsToInclude.Select(x => x.Name), writer.Includes);
			Assert.Equal(levelsToExclude.Select(x => x.Name), writer.Excludes);
		}

		#endregion

		#region Build()

		/// <summary>
		/// Tests building a <see cref="LogWriterConfiguration"/> with defaults.
		/// </summary>
		[Fact]
		public void Build_Default()
		{
			var builder = LogWriterConfigurationBuilder.New;
			var writer = builder.Build();
			Assert.Equal("Notice", writer.BaseLevel);
			Assert.Single(writer.NamePatterns);
			var pattern = writer.NamePatterns.First();
			Assert.IsType<LogWriterConfiguration.WildcardNamePattern>(pattern);
			Assert.Empty(writer.TagPatterns);
			Assert.Equal("*", pattern.Pattern);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		#endregion
	}

}
