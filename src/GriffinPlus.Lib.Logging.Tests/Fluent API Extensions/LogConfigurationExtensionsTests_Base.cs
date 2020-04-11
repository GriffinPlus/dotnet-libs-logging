///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2020 Sascha Falk <sascha@falk-online.eu>
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
	public abstract class LogConfigurationExtensionsTests_Base<T> where T : LogConfiguration, new()
	{
		#region Adding Log Writer Configurations

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void WithLogWriter_ExactNameByGenericArgument(bool useConfigurationCallback)
		{
			T configuration = GetDefaultConfiguration();

			LogConfiguration.LogWriter writer;
			if (useConfigurationCallback)
			{
				LogConfiguration.LogWriter writerInCallback = null;
				Assert.Same(configuration, configuration.WithLogWriter<T>(x => writerInCallback = x));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
				Assert.Equal(writer, writerInCallback);    // the log writer in the callback must be equal,
				Assert.NotSame(writer, writerInCallback);  // but not the same as it should have been copied.
			}
			else {
				Assert.Same(configuration, configuration.WithLogWriter<T>());
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}

			Assert.IsType<LogConfiguration.ExactNameLogWriterPattern>(writer.Pattern);
			Assert.Equal($"{typeof(T).FullName}", writer.Pattern.Pattern);
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void WithLogWriter_ExactNameByType(bool useConfigurationCallback)
		{
			T configuration = GetDefaultConfiguration();
			Type type = typeof(T);

			LogConfiguration.LogWriter writer;
			if (useConfigurationCallback)
			{
				LogConfiguration.LogWriter writerInCallback = null;
				Assert.Same(configuration, configuration.WithLogWriter(type, x => writerInCallback = x));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
				Assert.Equal(writer, writerInCallback);    // the log writer in the callback must be equal,
				Assert.NotSame(writer, writerInCallback);  // but not the same as it should have been copied.
			}
			else
			{
				Assert.Same(configuration, configuration.WithLogWriter(type));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}

			Assert.IsType<LogConfiguration.ExactNameLogWriterPattern>(writer.Pattern);
			Assert.Equal($"{type.FullName}", writer.Pattern.Pattern);
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void WithLogWriter_WithWildcardPattern(bool useConfigurationCallback)
		{
			T configuration = GetDefaultConfiguration();
			string wildcard = "MyDemo*";

			LogConfiguration.LogWriter writer;
			if (useConfigurationCallback)
			{
				LogConfiguration.LogWriter writerInCallback = null;
				Assert.Same(configuration, configuration.WithLogWritersByWildcard(wildcard, x => writerInCallback = x));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
				Assert.Equal(writer, writerInCallback);    // the log writer in the callback must be equal,
				Assert.NotSame(writer, writerInCallback);  // but not the same as it should have been copied.
			}
			else
			{
				Assert.Same(configuration, configuration.WithLogWritersByWildcard(wildcard));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}

			Assert.IsType<LogConfiguration.WildcardLogWriterPattern>(writer.Pattern);
			Assert.Equal(wildcard, writer.Pattern.Pattern);
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void WithLogWriter_WithRegexPattern(bool useConfigurationCallback)
		{
			T configuration = GetDefaultConfiguration();
			string regex = "^[a-z][A-Z][0-9]$";

			LogConfiguration.LogWriter writer;
			if (useConfigurationCallback)
			{
				LogConfiguration.LogWriter writerInCallback = null;
				Assert.Same(configuration, configuration.WithLogWritersByRegex(regex, x => writerInCallback = x));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
				Assert.Equal(writer, writerInCallback);    // the log writer in the callback must be equal,
				Assert.NotSame(writer, writerInCallback);  // but not the same as it should have been copied.
			}
			else
			{
				Assert.Same(configuration, configuration.WithLogWritersByRegex(regex));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}

			Assert.IsType<LogConfiguration.RegexLogWriterPattern>(writer.Pattern);
			Assert.Equal(regex, writer.Pattern.Pattern);
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		[Fact]
		public void WithLogWriterTiming()
		{
			T configuration = GetDefaultConfiguration();
			Assert.Same(configuration, configuration.WithLogWriterTiming());
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];
			Assert.IsType<LogConfiguration.ExactNameLogWriterPattern>(writer.Pattern);
			Assert.Equal("Timing", writer.Pattern.Pattern);
			Assert.Equal("None", writer.BaseLevel);
			Assert.Single(writer.Includes, "Timing");
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void WithLogWriterDefault(bool useConfigurationCallback)
		{
			T configuration = GetDefaultConfiguration();

			LogConfiguration.LogWriter writer;
			if (useConfigurationCallback)
			{
				LogConfiguration.LogWriter writerInCallback = null;
				Assert.Same(configuration, configuration.WithLogWriterDefault(x => writerInCallback = x));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
				Assert.Equal(writer, writerInCallback);    // the log writer in the callback must be equal,
				Assert.NotSame(writer, writerInCallback);  // but not the same as it should have been copied.
			}
			else
			{
				Assert.Same(configuration, configuration.WithLogWriterDefault());
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}

			Assert.IsType<LogConfiguration.WildcardLogWriterPattern>(writer.Pattern);
			Assert.Equal("*", writer.Pattern.Pattern);
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.False(writer.IsDefault);
		}

		[Fact]
		public void WithLogWriter_AllTogether()
		{
			// Testing with/without configuration callback is not necessary as the methods invoking the callbacks
			// were already covered above.

			T configuration = GetDefaultConfiguration();

			string wildcard = "MyDemo*";
			string regex = "^[a-z][A-Z][0-9]$";

			var returnedConfiguration = configuration
				.WithLogWriter<T>()
				.WithLogWriter(typeof(T))
				.WithLogWritersByWildcard(wildcard)
				.WithLogWritersByRegex(regex)
				.WithLogWriterTiming()
				.WithLogWriterDefault();
			Assert.Same(configuration, returnedConfiguration);

			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Equal(6, writers.Length);

			for (int i = 0; i < writers.Length; i++)
			{
				switch (i)
				{
					// effect of .WithLogWriter<T>()
					case 0:
						Assert.IsType<LogConfiguration.ExactNameLogWriterPattern>(writers[i].Pattern);
						Assert.Equal($"{typeof(T).FullName}", writers[i].Pattern.Pattern);
						break;

					// effect of .WithLogWriter(typeof(T))
					case 1:
						Assert.IsType<LogConfiguration.ExactNameLogWriterPattern>(writers[i].Pattern);
						Assert.Equal($"{typeof(T).FullName}", writers[i].Pattern.Pattern);
						break;

					// effect of .WithLogWritersByWildcard(wildcard)
					case 2:
						Assert.IsType<LogConfiguration.WildcardLogWriterPattern>(writers[i].Pattern);
						Assert.Equal(wildcard, writers[i].Pattern.Pattern);
						break;

					// effect of WithLogWritersByRegex(regex)
					case 3:
						Assert.IsType<LogConfiguration.RegexLogWriterPattern>(writers[i].Pattern);
						Assert.Equal(regex, writers[i].Pattern.Pattern);
						break;

					// effect of .WithLogWriterTiming()
					case 4:
						Assert.IsType<LogConfiguration.ExactNameLogWriterPattern>(writers[i].Pattern);
						Assert.Equal("Timing", writers[i].Pattern.Pattern);
						Assert.Equal("None", writers[i].BaseLevel);
						Assert.Single(writers[i].Includes, "Timing");
						Assert.Empty(writers[i].Excludes);
						Assert.False(writers[i].IsDefault);
						continue;

					// effect of .WithLogWriterDefault()
					case 5:
						Assert.IsType<LogConfiguration.WildcardLogWriterPattern>(writers[i].Pattern);
						Assert.Equal("*", writers[i].Pattern.Pattern);
						break;
				}

				Assert.Equal("Note", writers[i].BaseLevel);
				Assert.Empty(writers[i].Includes);
				Assert.Empty(writers[i].Excludes);
				Assert.False(writers[i].IsDefault);
			}
		}

		#endregion

		#region Adjusting the Base Level of a Log Writer Configuration

		// The following tests use LogLevel objects only.
		// The Fluent API supports overloads with strings identifying log levels as well,
		// but the implementation taking LogLevel objects only calls the corresponding overloads.
		// => There is no need to test the string overloads separately.

		[Fact]
		public void WithBaseLogLevel()
		{
			T configuration = GetDefaultConfiguration();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];
			var levels = LogLevel.KnownLevels;
			for (int i = 0; i < levels.Count; i++) {
				Assert.Same(writer, writer.WithBaseLevel(levels[i]));
				Assert.Equal(levels[i].Name, writer.BaseLevel);
			}
		}

		#endregion

		#region Including/Excluding Log Levels in a Log Writer Configuration

		// The following tests use LogLevel objects only.
		// The Fluent API supports overloads with strings identifying log levels as well,
		// but the implementation taking LogLevel objects only calls the corresponding overloads.
		// => There is no need to test the string overloads separately.

		[Fact]
		public void WithLevel_AddOnly()
		{
			T configuration = GetDefaultConfiguration();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];
			var baseLevel = writer.BaseLevel;
			var levels = LogLevel.KnownLevels;
			for (int i = 0; i < levels.Count; i++)
			{
				Assert.Same(writer, writer.WithLevel(levels[i]));
				Assert.Equal(baseLevel, writer.BaseLevel);
				Assert.Equal(levels.Take(i+1).Select(x => x.Name), writer.Includes);
				Assert.Empty(writer.Excludes);
			}
		}

		[Fact]
		public void WithLevel_AddRemovesExclude()
		{
			T configuration = GetDefaultConfiguration();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];

			// populate the include list
			var baseLevel = writer.BaseLevel;
			var levels = LogLevel.KnownLevels;
			for (int i = 0; i < levels.Count; i++)
			{
				Assert.Same(writer, writer.WithLevel(levels[i]));
				Assert.Equal(baseLevel, writer.BaseLevel);
				Assert.Equal(levels.Take(i + 1).Select(x => x.Name), writer.Includes);
				Assert.Empty(writer.Excludes);
			}

			// exclude the first and the last log level
			// => these log levels should automatically be removed from the include list 
			Assert.Same(writer, writer.WithoutLevel(levels[0].Name));
			Assert.Equal(levels.Skip(1).Take(levels.Count - 1).Select(x => x.Name), writer.Includes);
			Assert.Single(writer.Excludes);
			Assert.Equal(writer.Excludes[0], levels.First().Name);
			Assert.Same(writer, writer.WithoutLevel(levels[levels.Count - 1].Name));
			Assert.Equal(levels.Skip(1).Take(levels.Count - 2).Select(x => x.Name), writer.Includes);
			Assert.Equal(2, writer.Excludes.Count);
			Assert.Equal(writer.Excludes[0], levels.First().Name);
			Assert.Equal(writer.Excludes[1], levels.Last().Name);
		}

		[Fact]
		public void WithLevelRange_AddOnly()
		{
			T configuration = GetDefaultConfiguration();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];
			var baseLevel = writer.BaseLevel;
			var from = LogLevel.Failure;
			var to = LogLevel.Trace10;
			var levels = LogLevel.KnownLevels.Where(x => x.Id >= from.Id && x.Id <= to.Id);
			Assert.Same(writer, writer.WithLevelRange(from, to));
			Assert.Equal(baseLevel, writer.BaseLevel);
			Assert.Equal(levels.Select(x => x.Name), writer.Includes);
			Assert.Empty(writer.Excludes);
		}

		[Fact]
		public void WithLevelRange_AddRemovesExclude()
		{
			T configuration = GetDefaultConfiguration();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];

			// populate the include list
			var baseLevel = writer.BaseLevel;
			var from = LogLevel.Failure;
			var to = LogLevel.Trace10;
			var levels = LogLevel.KnownLevels.Where(x => x.Id >= from.Id && x.Id <= to.Id).ToArray();
			Assert.Same(writer, writer.WithLevelRange(from, to));

			// exclude the first three log levels
			// => these log levels should automatically be removed from the include list 
			var levelsToInclude = levels.Skip(3).ToArray();
			var levelsToExclude = levels.Take(3).ToArray();
			Assert.Same(writer, writer.WithoutLevelRange(levelsToExclude.First(), levelsToExclude.Last()));
			Assert.Equal(levelsToInclude.Select(x => x.Name), writer.Includes);
			Assert.Equal(levelsToExclude.Select(x => x.Name), writer.Excludes);
		}

		[Fact]
		public void WithoutLevel_AddOnly()
		{
			T configuration = GetDefaultConfiguration();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];
			var baseLevel = writer.BaseLevel;
			var levels = LogLevel.KnownLevels;
			for (int i = 0; i < levels.Count; i++)
			{
				Assert.Same(writer, writer.WithoutLevel(levels[i]));
				Assert.Equal(baseLevel, writer.BaseLevel);
				Assert.Empty(writer.Includes);
				Assert.Equal(levels.Take(i + 1).Select(x => x.Name), writer.Excludes);
			}
		}

		[Fact]
		public void WithoutLevel_AddRemovesInclude()
		{
			T configuration = GetDefaultConfiguration();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];

			// populate the exclude list
			var baseLevel = writer.BaseLevel;
			var levels = LogLevel.KnownLevels;
			for (int i = 0; i < levels.Count; i++)
			{
				Assert.Same(writer, writer.WithoutLevel(levels[i]));
				Assert.Equal(baseLevel, writer.BaseLevel);
				Assert.Empty(writer.Includes);
				Assert.Equal(levels.Take(i + 1).Select(x => x.Name), writer.Excludes);
			}

			// include the first and the last log level
			// => these log levels should automatically be removed from the exclude list 
			Assert.Same(writer, writer.WithLevel(levels[0].Name));
			Assert.Equal(levels.Skip(1).Take(levels.Count - 1).Select(x => x.Name), writer.Excludes);
			Assert.Single(writer.Includes);
			Assert.Equal(writer.Includes[0], levels.First().Name);
			Assert.Same(writer, writer.WithLevel(levels[levels.Count - 1].Name));
			Assert.Equal(levels.Skip(1).Take(levels.Count - 2).Select(x => x.Name), writer.Excludes);
			Assert.Equal(2, writer.Includes.Count);
			Assert.Equal(writer.Includes[0], levels.First().Name);
			Assert.Equal(writer.Includes[1], levels.Last().Name);
		}

		[Fact]
		public void WithoutLevelRange_AddOnly()
		{
			T configuration = GetDefaultConfiguration();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];
			var baseLevel = writer.BaseLevel;
			var from = LogLevel.Failure;
			var to = LogLevel.Trace10;
			var levels = LogLevel.KnownLevels.Where(x => x.Id >= from.Id && x.Id <= to.Id);
			Assert.Same(writer, writer.WithoutLevelRange(from, to));
			Assert.Equal(baseLevel, writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Equal(levels.Select(x => x.Name), writer.Excludes);
		}

		[Fact]
		public void WithoutLevelRange_AddRemovesInclude()
		{
			T configuration = GetDefaultConfiguration();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];

			// populate the exclude list
			var baseLevel = writer.BaseLevel;
			var from = LogLevel.Failure;
			var to = LogLevel.Trace10;
			var levels = LogLevel.KnownLevels.Where(x => x.Id >= from.Id && x.Id <= to.Id).ToArray();
			Assert.Same(writer, writer.WithoutLevelRange(from, to));

			// include the first three log levels
			// => these log levels should automatically be removed from the exclude list 
			var levelsToInclude = levels.Take(3).ToArray();
			var levelsToExclude = levels.Skip(3).ToArray();
			Assert.Same(writer, writer.WithLevelRange(levelsToInclude.First(), levelsToInclude.Last()));
			Assert.Equal(levelsToInclude.Select(x => x.Name), writer.Includes);
			Assert.Equal(levelsToExclude.Select(x => x.Name), writer.Excludes);
		}

		#endregion

		#region Helpers

		private T GetDefaultConfiguration()
		{
			T configuration = new T();
			var writers = configuration.GetLogWriterSettings().ToArray();
			Assert.Single(writers);
			var writer = writers[0];
			Assert.Equal("Wildcard: *", writer.Pattern.ToString());
			Assert.Equal("Note", writer.BaseLevel);
			Assert.Empty(writer.Includes);
			Assert.Empty(writer.Excludes);
			Assert.True(writer.IsDefault);
			return configuration;
		}

		#endregion

	}
}