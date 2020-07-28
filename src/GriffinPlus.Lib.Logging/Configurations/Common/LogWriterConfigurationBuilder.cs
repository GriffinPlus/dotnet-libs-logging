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

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Fluent builder for a <see cref="LogWriterConfiguration"/>.
	/// </summary>
	public class LogWriterConfigurationBuilder
	{
		private readonly LogWriterConfiguration mConfiguration = new LogWriterConfiguration();

		/// <summary>
		/// Initializes a new instance of the <see cref="LogWriterConfigurationBuilder"/> class.
		/// Not supported, please use <see cref="New"/> instead to feature the fluent character.
		/// </summary>
		private LogWriterConfigurationBuilder()
		{

		}

		/// <summary>
		/// Creates a new builder.
		/// </summary>
		public static LogWriterConfigurationBuilder New => new LogWriterConfigurationBuilder();

		/// <summary>
		/// Adds the specified name of the log writer the configuration should apply to.
		/// </summary>
		/// <param name="name">Name of the log writer the configuration should apply to.</param>
		/// <returns>The log writer configuration builder.</returns>
		public LogWriterConfigurationBuilder MatchingExactly(string name)
		{
			mConfiguration.mPatterns.Add(new LogWriterConfiguration.ExactNameLogWriterPattern(name));
			return this;
		}

		/// <summary>
		/// Adds the specified wildcard pattern to match the name of log writers the configuration should apply to.
		/// </summary>
		/// <param name="pattern">A wildcard pattern matching the name of log writers the configuration should apply to.</param>
		/// <returns>The log writer configuration builder.</returns>
		public LogWriterConfigurationBuilder MatchingWildcardPattern(string pattern)
		{
			mConfiguration.mPatterns.Add(new LogWriterConfiguration.WildcardLogWriterPattern(pattern));
			return this;
		}

		/// <summary>
		/// Adds the specified regular expression to match the name of log writers the configuration should apply to.
		/// </summary>
		/// <param name="regex">A regex pattern matching the name of log writers the configuration should apply to.</param>
		/// <returns>The log writer configuration builder.</returns>
		public LogWriterConfigurationBuilder MatchingRegex(string regex)
		{
			mConfiguration.mPatterns.Add(new LogWriterConfiguration.RegexLogWriterPattern(regex));
			return this;
		}

		/// <summary>
		/// Sets the base log level of the log writer configuration.
		/// </summary>
		/// <param name="level">Log level to set as base log level.</param>
		/// <returns>The log writer configuration builder.</returns>
		public LogWriterConfigurationBuilder WithBaseLevel(LogLevel level)
		{
			if (level == null) throw new ArgumentNullException(nameof(level));
			return WithBaseLevel(level.Name);
		}

		/// <summary>
		/// Sets the base log level of the log writer configuration.
		/// </summary>
		/// <param name="level">Log level to set as base log level.</param>
		/// <returns>The log writer configuration builder.</returns>
		public LogWriterConfigurationBuilder WithBaseLevel(string level)
		{
			mConfiguration.mBaseLevel = level ?? throw new ArgumentNullException(nameof(level));
			return this;
		}

		/// <summary>
		/// Enables the specified log levels (or aspects) in addition to those already enabled via the base level.
		/// </summary>
		/// <param name="levels">Log levels to enable.</param>
		/// <returns>The log writer configuration builder.</returns>
		public LogWriterConfigurationBuilder WithLevel(params LogLevel[] levels)
		{
			return WithLevel(levels.Select(x => x.Name).ToArray());
		}

		/// <summary>
		/// Enables the specified log levels (or aspects) in addition to those already enabled via the base level.
		/// </summary>
		/// <param name="levels">Log levels to enable.</param>
		/// <returns>The log writer configuration builder.</returns>
		public LogWriterConfigurationBuilder WithLevel(params string[] levels)
		{
			if (levels == null) throw new ArgumentNullException(nameof(levels));
			foreach (var level in levels)
			{
				if (level == null) throw new ArgumentException("One of the specified log levels is a null reference.", nameof(levels));
				if (!mConfiguration.mIncludes.Contains(level)) mConfiguration.mIncludes.Add(level);
				mConfiguration.mExcludes.Remove(level);
			}
			return this;
		}

		/// <summary>
		/// Enables the specified range of log levels in addition to those already enabled via the base level.
		/// </summary>
		/// <param name="from">First log level to enable.</param>
		/// <param name="to">Last log level to enable.</param>
		/// <returns>The log writer configuration builder.</returns>
		public LogWriterConfigurationBuilder WithLevelRange(LogLevel from, LogLevel to)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to == null) throw new ArgumentNullException(nameof(to));
			return WithLevelRange(from.Name, to.Name);
		}

		/// <summary>
		/// Enables the specified range of log levels in addition to those already enabled via the base level.
		/// </summary>
		/// <param name="from">First log level to enable.</param>
		/// <param name="to">Last log level to enable.</param>
		/// <returns>The log writer configuration builder.</returns>
		public LogWriterConfigurationBuilder WithLevelRange(string from, string to)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to == null) throw new ArgumentNullException(nameof(to));

			// get log levels associated with the log level names
			var fromLevel = LogLevel.GetAspect(from);
			var toLevel = LogLevel.GetAspect(to);

			// swap order, if specified in wrong order
			if (fromLevel.Id > toLevel.Id)
			{
				var swap = toLevel;
				toLevel = fromLevel;
				fromLevel = swap;
			}

			// add one include per log level in the range
			var levels = LogLevel.KnownLevels; // index corresponds to log level id
			for (int id = fromLevel.Id; id <= toLevel.Id; id++)
			{
				if (!mConfiguration.mIncludes.Contains(levels[id].Name)) mConfiguration.mIncludes.Add(levels[id].Name);
				mConfiguration.mExcludes.Remove(levels[id].Name);
			}

			return this;
		}

		/// <summary>
		/// Disables the specified log levels (or aspects), although they might be enabled via the base level.
		/// </summary>
		/// <param name="levels">Log levels to disable.</param>
		/// <returns>The log writer configuration builder.</returns>
		public LogWriterConfigurationBuilder WithoutLevel(params LogLevel[] levels)
		{
			if (levels == null) throw new ArgumentNullException(nameof(levels));
			return WithoutLevel(levels.Select(x => x.Name).ToArray());
		}

		/// <summary>
		/// Disables the specified log levels (or aspects), although they might be enabled via the base level.
		/// </summary>
		/// <param name="levels">Log levels to disable.</param>
		/// <returns>The log writer configuration builder.</returns>
		public LogWriterConfigurationBuilder WithoutLevel(params string[] levels)
		{
			if (levels == null) throw new ArgumentNullException(nameof(levels));

			foreach (var level in levels)
			{
				if (level == null) throw new ArgumentException("One of the specified log levels is a null reference.", nameof(levels));
				mConfiguration.mIncludes.Remove(level);
				if (!mConfiguration.mExcludes.Contains(level)) mConfiguration.mExcludes.Add(level);
			}

			return this;
		}

		/// <summary>
		/// Disables the specified range of log levels, although they might be enabled via the base level.
		/// </summary>
		/// <param name="from">First log level to disable.</param>
		/// <param name="to">Last log level to disable.</param>
		/// <returns>The log writer configuration builder.</returns>
		public LogWriterConfigurationBuilder WithoutLevelRange(LogLevel from, LogLevel to)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to == null) throw new ArgumentNullException(nameof(to));
			return WithoutLevelRange(from.Name, to.Name);
		}

		/// <summary>
		/// Disables the specified range of log levels, although they might be enabled via the base level.
		/// </summary>
		/// <param name="from">First log level to disable.</param>
		/// <param name="to">Last log level to disable.</param>
		/// <returns>The log writer configuration builder.</returns>
		public LogWriterConfigurationBuilder WithoutLevelRange(string from, string to)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to == null) throw new ArgumentNullException(nameof(to));

			// get log levels associated with the log level names
			var fromLevel = LogLevel.GetAspect(from);
			var toLevel = LogLevel.GetAspect(to);

			// swap order, if specified in wrong order
			if (fromLevel.Id > toLevel.Id)
			{
				var swap = toLevel;
				toLevel = fromLevel;
				fromLevel = swap;
			}

			// add one exclude per log level in the range
			var levels = LogLevel.KnownLevels; // index corresponds to log level id
			for (int id = fromLevel.Id; id <= toLevel.Id; id++)
			{
				mConfiguration.mIncludes.Remove(levels[id].Name);
				if (!mConfiguration.mExcludes.Contains(levels[id].Name)) mConfiguration.mExcludes.Add(levels[id].Name);
			}

			return this;
		}

		/// <summary>
		/// Builds the configured log writer configuration.
		/// </summary>
		/// <returns>The built log writer configuration.</returns>
		public LogWriterConfiguration Build()
		{
			// add default pattern matching all log writer names
			var copy = new LogWriterConfiguration(mConfiguration);
			if (!copy.mPatterns.Any()) copy.mPatterns.Add(LogWriterConfiguration.DefaultPattern);
			return copy;
		}

	}

}
