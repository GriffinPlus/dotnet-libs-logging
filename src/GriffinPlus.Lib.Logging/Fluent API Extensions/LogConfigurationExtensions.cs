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
	/// Fluent API extension methods for the <see cref="LogConfiguration"/> class.
	/// </summary>
	public static class LogConfigurationExtensions
	{
		/// <summary>
		/// Delegate for callbacks that are invoked to set up a <see cref="LogWriterConfiguration"/> using a <see cref="LogWriterConfigurationBuilder"/>.
		/// </summary>
		/// <param name="writer"></param>
		public delegate void LogWriterConfigurationCallback(LogWriterConfigurationBuilder writer);

		/// <summary>
		/// Adds a log writer configuration using the full name of the specified type as its name.
		/// The configuration will match exactly the log writer with this name.
		/// </summary>
		/// <typeparam name="T">The type whose full name should serve as the log writer name the configuration should apply to.</typeparam>
		/// <param name="this">The log configuration.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		/// <returns>The updated log configuration.</returns>
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
		/// <returns>The updated log configuration.</returns>
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
		/// <returns>The updated log configuration.</returns>
		public static LogConfiguration WithLogWriter(this LogConfiguration @this, string name, LogWriterConfigurationCallback configuration = null)
		{
			var writer = LogWriterConfigurationBuilder.New.MatchingExactly(name);
			configuration?.Invoke(writer);
			@this.SetLogWriterSettings(JoinLogWriterConfiguration(@this, writer.Build()));
			return @this;
		}

		/// <summary>
		/// Adds a log writer configuration, using the specified wildcard pattern to match the name of log writers the configuration should apply to.
		/// </summary>
		/// <param name="this">The log configuration.</param>
		/// <param name="pattern">A wildcard pattern matching the name of log writers the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		/// <returns>The updated log configuration.</returns>
		public static LogConfiguration WithLogWritersByWildcard(this LogConfiguration @this, string pattern, LogWriterConfigurationCallback configuration = null)
		{
			var writer = LogWriterConfigurationBuilder.New.MatchingWildcardPattern(pattern);
			configuration?.Invoke(writer);
			@this.SetLogWriterSettings(JoinLogWriterConfiguration(@this, writer.Build()));
			return @this;
		}

		/// <summary>
		/// Adds a log writer configuration using the specified regular expression to match the name of log writers the configuration should apply to.
		/// </summary>
		/// <param name="this">The log configuration.</param>
		/// <param name="regex">A regular expression matching the name of log writers the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		/// <returns>The updated log configuration.</returns>
		public static LogConfiguration WithLogWritersByRegex(this LogConfiguration @this, string regex, LogWriterConfigurationCallback configuration = null)
		{
			var writer = LogWriterConfigurationBuilder.New.MatchingRegex(regex);
			configuration?.Invoke(writer);
			@this.SetLogWriterSettings(JoinLogWriterConfiguration(@this, writer.Build()));
			return @this;
		}

		/// <summary>
		/// Adds a log writer configuration for the internal 'Timing' log writer.
		/// By default, the log writer is used by <see cref="TimingLogger"/> when logging time measurements.
		/// </summary>
		/// <param name="this">The log configuration.</param>
		/// <returns>The updated log configuration.</returns>
		public static LogConfiguration WithLogWriterTiming(this LogConfiguration @this)
		{
			@this.SetLogWriterSettings(JoinLogWriterConfiguration(@this, LogWriterConfiguration.TimingWriter));
			return @this;
		}

		/// <summary>
		/// Adds a log writer configuration which matches any log writer name catching all log writers that were not handled in
		/// a preceding step. This log writer configuration should be added last.
		/// </summary>
		/// <param name="this">The log configuration.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		/// <returns>The updated log configuration.</returns>
		public static LogConfiguration WithLogWriterDefault(this LogConfiguration @this, LogWriterConfigurationCallback configuration = null)
		{
			var writer = LogWriterConfigurationBuilder.New.MatchingWildcardPattern("*");
			configuration?.Invoke(writer);
			@this.SetLogWriterSettings(JoinLogWriterConfiguration(@this, writer.Build()));
			return @this;
		}

		/// <summary>
		/// Appends the specified log writer configuration to the configuration already stored in the specified log configuration.
		/// </summary>
		/// <param name="log">Log configuration to append the log writer configuration to.</param>
		/// <param name="writer">Log writer configuration to append to the log configuration.</param>
		/// <returns>A list of log writer configurations containing the old log writer configuration and the new one.</returns>
		private static List<LogWriterConfiguration> JoinLogWriterConfiguration(LogConfiguration log, LogWriterConfiguration writer)
		{
			List<LogWriterConfiguration> writers = new List<LogWriterConfiguration>(log.GetLogWriterSettings().Where(x => !x.IsDefault));
			writers.Add(writer);
			return writers;
		}

	}
}
