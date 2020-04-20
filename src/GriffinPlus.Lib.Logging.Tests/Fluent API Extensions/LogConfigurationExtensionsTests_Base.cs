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
	public abstract class LogWriterConfigurationBuilderTests<T> where T : LogConfiguration, new()
	{
		#region Adding Log Writer Configurations

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void WithLogWriter_ExactNameByGenericArgument(bool useConfigurationCallback)
		{
			T configuration = GetDefaultConfiguration();

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				Assert.Same(configuration, configuration.WithLogWriter<T>(x => Assert.NotNull(x)));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}
			else {
				Assert.Same(configuration, configuration.WithLogWriter<T>());
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}

			Assert.Single(writer.Patterns);
			var pattern = writer.Patterns.First();
			Assert.IsType<LogWriterConfiguration.ExactNameLogWriterPattern>(pattern);
			Assert.Equal($"{typeof(T).FullName}", pattern.Pattern);
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

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				Assert.Same(configuration, configuration.WithLogWriter(type, x => Assert.NotNull(x)));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}
			else
			{
				Assert.Same(configuration, configuration.WithLogWriter(type));
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
		public void WithLogWriter_WithWildcardPattern(bool useConfigurationCallback)
		{
			T configuration = GetDefaultConfiguration();
			string wildcard = "MyDemo*";

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				Assert.Same(configuration, configuration.WithLogWritersByWildcard(wildcard, x => Assert.NotNull(x)));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}
			else
			{
				Assert.Same(configuration, configuration.WithLogWritersByWildcard(wildcard));
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
		public void WithLogWriter_WithRegexPattern(bool useConfigurationCallback)
		{
			T configuration = GetDefaultConfiguration();
			string regex = "^[a-z][A-Z][0-9]$";

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				Assert.Same(configuration, configuration.WithLogWritersByRegex(regex, x => Assert.NotNull(x)));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}
			else
			{
				Assert.Same(configuration, configuration.WithLogWritersByRegex(regex));
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
		public void WithLogWriterTiming()
		{
			T configuration = GetDefaultConfiguration();
			Assert.Same(configuration, configuration.WithLogWriterTiming());
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
		public void WithLogWriterDefault(bool useConfigurationCallback)
		{
			T configuration = GetDefaultConfiguration();

			LogWriterConfiguration writer;
			if (useConfigurationCallback)
			{
				Assert.Same(configuration, configuration.WithLogWriterDefault(x => Assert.NotNull(x)));
				var writers = configuration.GetLogWriterSettings().ToArray();
				Assert.Single(writers);
				writer = writers[0];
			}
			else
			{
				Assert.Same(configuration, configuration.WithLogWriterDefault());
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
				var writer = writers[i];
				Assert.Single(writer.Patterns);
				var pattern = writer.Patterns.First();

				switch (i)
				{
					// effect of .WithLogWriter<T>()
					case 0:
						Assert.IsType<LogWriterConfiguration.ExactNameLogWriterPattern>(pattern);
						Assert.Equal($"{typeof(T).FullName}", pattern.Pattern);
						break;

					// effect of .WithLogWriter(typeof(T))
					case 1:
						Assert.IsType<LogWriterConfiguration.ExactNameLogWriterPattern>(pattern);
						Assert.Equal($"{typeof(T).FullName}", pattern.Pattern);
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

		private T GetDefaultConfiguration()
		{
			T configuration = new T();
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