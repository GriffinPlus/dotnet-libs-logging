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
using System.Collections.Generic;
using System.Linq;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Fluent API extension methods for the <see cref="LogConfiguration"/> class and its inner classes.
	/// </summary>
	public static class LogConfigurationExtensions
	{
		/// <summary>
		/// Callback for log writer configurations.
		/// </summary>
		/// <param name="writer"></param>
		public delegate void LogWriterConfigurationCallback(LogConfiguration.LogWriter writer);

		/// <summary>
		/// Adds a log writer configuration using the full name of the specified type as its name.
		/// The configuration will match exactly the log writer with this name.
		/// </summary>
		/// <typeparam name="T">The type whose full name should serve as the log writer name the configuration should apply to.</typeparam>
		/// <param name="this">The log configuration.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		/// <returns>The same log configuration.</returns>
		public static LogConfiguration WithLogWriter<T>(this LogConfiguration @this, LogWriterConfigurationCallback configuration = null)
		{
			return @this.WithLogWriter(typeof(T).FullName, configuration);
		}

		/// <summary>
		/// Adds a log writer configuration using the full name of the specified type as its name.
		/// The configuration will match exactly the log writer with this name.
		/// </summary>
		/// <param name="this">The log configuration.</param>
		/// <param name="type">The type whose full name should serve as the log writer name the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		/// <returns>The same log configuration.</returns>
		public static LogConfiguration WithLogWriter(this LogConfiguration @this, Type type, LogWriterConfigurationCallback configuration = null)
		{
			return @this.WithLogWriter(type.FullName, configuration);
		}

		/// <summary>
		/// Adds a log writer configuration using the full name of the specified type as its name.
		/// The configuration will match exactly the log writer with this name.
		/// </summary>
		/// <param name="this">The log configuration.</param>
		/// <param name="name">Name of the log writer the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		/// <returns>The same log configuration.</returns>
		public static LogConfiguration WithLogWriter(this LogConfiguration @this, string name, LogWriterConfigurationCallback configuration = null)
		{
			var writer = new LogConfiguration.LogWriter();
			writer.Pattern = new LogConfiguration.ExactNameLogWriterPattern(name);
			configuration?.Invoke(writer);
			@this.SetLogWriterSettings(JoinLogWriterConfiguration(@this, writer));
			return @this;
		}

		/// <summary>
		/// Adds a log writer configuration, using the specified wildcard pattern to match the name of log writers the configuration should apply to.
		/// </summary>
		/// <param name="this">The log configuration.</param>
		/// <param name="pattern">A wildcard pattern matching the name of log writers the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		/// <returns>The same log configuration.</returns>
		public static LogConfiguration WithLogWritersByWildcard(this LogConfiguration @this, string pattern, LogWriterConfigurationCallback configuration = null)
		{
			var writer = new LogConfiguration.LogWriter();
			writer.Pattern = new LogConfiguration.WildcardLogWriterPattern(pattern);
			configuration?.Invoke(writer);
			@this.SetLogWriterSettings(JoinLogWriterConfiguration(@this, writer));
			return @this;
		}

		/// <summary>
		/// Adds a log writer configuration using the specified regular expression to match the name of log writers the configuration should apply to.
		/// </summary>
		/// <param name="this">The log configuration.</param>
		/// <param name="regex">A regular expression matching the name of log writers the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		/// <returns>The same log configuration.</returns>
		public static LogConfiguration WithLogWritersByRegex(this LogConfiguration @this, string regex, LogWriterConfigurationCallback configuration = null)
		{
			var writer = new LogConfiguration.LogWriter();
			writer.Pattern = new LogConfiguration.RegexLogWriterPattern(regex);
			configuration?.Invoke(writer);
			@this.SetLogWriterSettings(JoinLogWriterConfiguration(@this, writer));
			return @this;
		}

		/// <summary>
		/// Adds a log writer configuration for the internal 'Timing' log writer.
		/// By default, the log writer is used by <see cref="TimingLogger"/> when logging time measurements.
		/// </summary>
		/// <param name="this">The log configuration.</param>
		/// <returns>The same log configuration.</returns>
		public static LogConfiguration WithLogWriterTiming(this LogConfiguration @this)
		{
			var writer = new LogConfiguration.LogWriter();
			writer.Pattern = new LogConfiguration.ExactNameLogWriterPattern("Timing");
			writer.BaseLevel = "None";
			writer.Includes.Add("Timing");
			@this.SetLogWriterSettings(JoinLogWriterConfiguration(@this, writer));
			return @this;
		}

		/// <summary>
		/// Adds a log writer configuration which matches any log writer name catching all log writers that were not handled in
		/// a preceding step. This log writer configuration should be added last.
		/// </summary>
		/// <param name="this">The log configuration.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		/// <returns>The log writer configuration.</returns>
		public static LogConfiguration WithLogWriterDefault(this LogConfiguration @this, LogWriterConfigurationCallback configuration = null)
		{
			var writer = new LogConfiguration.LogWriter();
			writer.Pattern = new LogConfiguration.WildcardLogWriterPattern("*");
			configuration?.Invoke(writer);
			@this.SetLogWriterSettings(JoinLogWriterConfiguration(@this, writer));
			return @this;
		}

		/// <summary>
		/// Appends the specified log writer configuration to the configuration already stored in the specified log configuration.
		/// </summary>
		/// <param name="log">Log configuration to append the log writer configuration to.</param>
		/// <param name="writer">Log writer configuration to append to the log configuration.</param>
		/// <returns>A list of log writer configurations containing the old log writer configuration and the new one.</returns>
		private static List<LogConfiguration.LogWriter> JoinLogWriterConfiguration(LogConfiguration log, LogConfiguration.LogWriter writer)
		{
			List<LogConfiguration.LogWriter> writers = new List<LogConfiguration.LogWriter>(log.GetLogWriterSettings().Where(x => !x.IsDefault));
			writers.Add(writer);
			return writers;
		}

		/// <summary>
		/// Sets the base log level of the log writer configuration.
		/// </summary>
		/// <param name="this">The log writer configuration.</param>
		/// <param name="level">Log level to set as base log level.</param>
		/// <returns>The same log writer configuration.</returns>
		public static LogConfiguration.LogWriter WithBaseLevel(this LogConfiguration.LogWriter @this, LogLevel level)
		{
			if (level == null) throw new ArgumentNullException(nameof(level));
			return @this.WithBaseLevel(level.Name);
		}

		/// <summary>
		/// Sets the base log level of the log writer configuration.
		/// </summary>
		/// <param name="this">The log writer configuration.</param>
		/// <param name="level">Log level to set as base log level.</param>
		/// <returns>The same log writer configuration.</returns>
		public static LogConfiguration.LogWriter WithBaseLevel(this LogConfiguration.LogWriter @this, string level)
		{
			@this.BaseLevel = level ?? throw new ArgumentNullException(nameof(level));
			return @this;
		}

		/// <summary>
		/// Enables the specified log levels (or aspects) in addition to those already enabled via the base level.
		/// </summary>
		/// <param name="this">The log writer configuration.</param>
		/// <param name="levels">Log levels to enable.</param>
		/// <returns>The same log writer configuration.</returns>
		public static LogConfiguration.LogWriter WithLevel(this LogConfiguration.LogWriter @this, params LogLevel[] levels)
		{
			return @this.WithLevel(levels.Select(x => x.Name).ToArray());
		}

		/// <summary>
		/// Enables the specified log levels (or aspects) in addition to those already enabled via the base level.
		/// </summary>
		/// <param name="this">The log writer configuration.</param>
		/// <param name="levels">Log levels to enable.</param>
		/// <returns>The same log writer configuration.</returns>
		public static LogConfiguration.LogWriter WithLevel(this LogConfiguration.LogWriter @this, params string[] levels)
		{
			if (levels == null) throw new ArgumentNullException(nameof(levels));
			foreach (var level in levels) {
				if (level == null) throw new ArgumentException("One of the specified log levels is a null reference.", nameof(levels));
				@this.Includes.Add(level);
				@this.Excludes.Remove(level);
			}
			return @this;
		}

		/// <summary>
		/// Enables the specified range of log levels in addition to those already enabled via the base level.
		/// </summary>
		/// <param name="this">The log writer configuration.</param>
		/// <param name="from">First log level to enable.</param>
		/// <param name="to">Last log level to enable.</param>
		/// <returns>The same log writer configuration.</returns>
		public static LogConfiguration.LogWriter WithLevelRange(this LogConfiguration.LogWriter @this, LogLevel from, LogLevel to)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to == null) throw new ArgumentNullException(nameof(to));
			return @this.WithLevelRange(from.Name, to.Name);
		}

		/// <summary>
		/// Enables the specified range of log levels in addition to those already enabled via the base level.
		/// </summary>
		/// <param name="this">The log writer configuration.</param>
		/// <param name="from">First log level to enable.</param>
		/// <param name="to">Last log level to enable.</param>
		/// <returns>The same log writer configuration.</returns>
		public static LogConfiguration.LogWriter WithLevelRange(this LogConfiguration.LogWriter @this, string from, string to)
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
				@this.Includes.Add(levels[id].Name);
				@this.Excludes.Remove(levels[id].Name);
			}

			return @this;
		}

		/// <summary>
		/// Disables the specified log levels (or aspects), although they might be enabled via the base level.
		/// </summary>
		/// <param name="this">The log writer configuration.</param>
		/// <param name="levels">Log levels to disable.</param>
		/// <returns>The same log writer configuration.</returns>
		public static LogConfiguration.LogWriter WithoutLevel(this LogConfiguration.LogWriter @this, params LogLevel[] levels)
		{
			if (levels == null) throw new ArgumentNullException(nameof(levels));
			return @this.WithoutLevel(levels.Select(x => x.Name).ToArray());
		}

		/// <summary>
		/// Disables the specified log levels (or aspects), although they might be enabled via the base level.
		/// </summary>
		/// <param name="this">The log writer configuration.</param>
		/// <param name="levels">Log levels to disable.</param>
		/// <returns>The same log writer configuration.</returns>
		public static LogConfiguration.LogWriter WithoutLevel(this LogConfiguration.LogWriter @this, params string[] levels)
		{
			if (levels == null) throw new ArgumentNullException(nameof(levels));
			foreach (var level in levels) {
				if (level == null) throw new ArgumentException("One of the specified log levels is a null reference.", nameof(levels));
				@this.Includes.Remove(level);
				@this.Excludes.Add(level);
			}
			return @this;
		}

		/// <summary>
		/// Disables the specified range of log levels, although they might be enabled via the base level.
		/// </summary>
		/// <param name="this">The log writer configuration.</param>
		/// <param name="from">First log level to disable.</param>
		/// <param name="to">Last log level to disable.</param>
		/// <returns>The same log writer configuration.</returns>
		public static LogConfiguration.LogWriter WithoutLevelRange(this LogConfiguration.LogWriter @this, LogLevel from, LogLevel to)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to == null) throw new ArgumentNullException(nameof(to));
			return @this.WithoutLevelRange(from.Name, to.Name);
		}

		/// <summary>
		/// Disables the specified range of log levels, although they might be enabled via the base level.
		/// </summary>
		/// <param name="this">The log writer configuration.</param>
		/// <param name="from">First log level to disable.</param>
		/// <param name="to">Last log level to disable.</param>
		/// <returns>The same log writer configuration.</returns>
		public static LogConfiguration.LogWriter WithoutLevelRange(this LogConfiguration.LogWriter @this, string from, string to)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to == null) throw new ArgumentNullException(nameof(to));

			// get log levels associated with the log level names
			var fromLevel = LogLevel.GetAspect(from);
			var toLevel = LogLevel.GetAspect(to);

			// swap order, if specified in wrong order
			if (fromLevel.Id > toLevel.Id) {
				var swap = toLevel;
				toLevel = fromLevel;
				fromLevel = swap;
			}

			// add one exclude per log level in the range
			var levels = LogLevel.KnownLevels; // index corresponds to log level id
			for (int id = fromLevel.Id; id <= toLevel.Id; id++) {
				@this.Includes.Remove(levels[id].Name);
				@this.Excludes.Add(levels[id].Name);
			}

			return @this;
		}
	}
}
