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
using System.Collections.Generic;
using System.Linq;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Delegate for callbacks that are invoked to set up a <see cref="LogWriterConfiguration"/> using a <see cref="LogWriterConfigurationBuilder"/>.
	/// </summary>
	/// <param name="writer"></param>
	public delegate void LogWriterConfigurationCallback(LogWriterConfigurationBuilder writer);

	/// <summary>
	/// The base class for log configurations.
	/// </summary>
	public abstract partial class LogConfiguration<CONFIGURATION> : ILogConfiguration
		where CONFIGURATION: LogConfiguration<CONFIGURATION>
	{
		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		public abstract string ApplicationName { get; set; }

		/// <summary>
		/// Gets a bit mask in which each bit is associated with a log level with the same id
		/// and expresses whether the corresponding log level is active for the specified writer.
		/// </summary>
		/// <param name="writer">Log writer to get the active log level mask for.</param>
		/// <returns>The requested active log level mask.</returns>
		public abstract LogLevelBitMask GetActiveLogLevelMask(LogWriter writer);

		/// <summary>
		/// Gets the current log writer settings.
		/// </summary>
		/// <returns>A copy of the internal log writer settings.</returns>
		public abstract IEnumerable<LogWriterConfiguration> GetLogWriterSettings();

		/// <summary>
		/// Sets the log writer settings to use.
		/// </summary>
		/// <param name="settings">Settings to use.</param>
		public abstract void SetLogWriterSettings(IEnumerable<LogWriterConfiguration> settings);

		/// <summary>
		/// Gets the settings for pipeline stages by their name.
		/// </summary>
		/// <returns>The requested settings.</returns>
		public abstract IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetProcessingPipelineStageSettings();

		/// <summary>
		/// Gets the settings for the pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the pipeline stage to get the settings for.</param>
		/// <returns>
		/// The requested settings;
		/// null, if the settings do not exist.
		/// </returns>
		public abstract IReadOnlyDictionary<string, string> GetProcessingPipelineStageSettings(string name);

		/// <summary>
		/// Sets the settings for the pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the pipeline stage to set the settings for.</param>
		/// <param name="settings">Settings to set.</param>
		public abstract void SetProcessingPipelineStageSettings(string name, IReadOnlyDictionary<string, string> settings);

		/// <summary>
		/// Adds a log writer configuration using the full name of the specified type as its name.
		/// The configuration will match exactly the log writer with this name.
		/// </summary>
		/// <typeparam name="T">The type whose full name should serve as the log writer name the configuration should apply to.</typeparam>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		public void AddLogWriter<T>(LogWriterConfigurationCallback configuration = null)
		{
			AddLogWriter(typeof(T).FullName, configuration);
		}

		/// <summary>
		/// Adds a log writer configuration using the full name of the specified type as its name.
		/// The configuration will match exactly the log writer with this name.
		/// </summary>
		/// <param name="type">The type whose full name should serve as the log writer name the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		public void AddLogWriter(Type type, LogWriterConfigurationCallback configuration = null)
		{
			AddLogWriter(type.FullName, configuration);
		}

		/// <summary>
		/// Adds a log writer configuration using the full name of the specified type as its name.
		/// The configuration will match exactly the log writer with this name.
		/// </summary>
		/// <param name="name">Name of the log writer the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		public void AddLogWriter(string name, LogWriterConfigurationCallback configuration = null)
		{
			var writer = LogWriterConfigurationBuilder.New.MatchingExactly(name);
			configuration?.Invoke(writer);
			AppendLogWriterConfiguration(writer.Build());
		}

		/// <summary>
		/// Adds a log writer configuration, using the specified wildcard pattern to match the name of log writers the configuration should apply to.
		/// </summary>
		/// <param name="pattern">A wildcard pattern matching the name of log writers the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		public void AddLogWritersByWildcard(string pattern, LogWriterConfigurationCallback configuration = null)
		{
			var writer = LogWriterConfigurationBuilder.New.MatchingWildcardPattern(pattern);
			configuration?.Invoke(writer);
			AppendLogWriterConfiguration(writer.Build());
		}

		/// <summary>
		/// Adds a log writer configuration using the specified regular expression to match the name of log writers the configuration should apply to.
		/// </summary>
		/// <param name="regex">A regular expression matching the name of log writers the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		public void AddLogWritersByRegex(string regex, LogWriterConfigurationCallback configuration = null)
		{
			var writer = LogWriterConfigurationBuilder.New.MatchingRegex(regex);
			configuration?.Invoke(writer);
			AppendLogWriterConfiguration(writer.Build());
		}

		/// <summary>
		/// Adds a log writer configuration for the internal 'Timing' log writer.
		/// By default, the log writer is used by <see cref="TimingLogger"/> when logging time measurements.
		/// </summary>
		public void AddLogWriterTiming()
		{
			AppendLogWriterConfiguration(LogWriterConfiguration.TimingWriter);
		}

		/// <summary>
		/// Adds a log writer configuration which matches any log writer name catching all log writers that were not handled in
		/// a preceding step. This log writer configuration should be added last.
		/// </summary>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		/// <returns>The updated log configuration.</returns>
		public void AddLogWriterDefault(LogWriterConfigurationCallback configuration = null)
		{
			var writer = LogWriterConfigurationBuilder.New.MatchingWildcardPattern("*");
			configuration?.Invoke(writer);
			AppendLogWriterConfiguration(writer.Build());
		}

		/// <summary>
		/// Appends the specified log writer configuration to the configuration already stored in the log configuration.
		/// </summary>
		/// <param name="writer">Log writer configuration to append to the log configuration.</param>
		private void AppendLogWriterConfiguration(LogWriterConfiguration writer)
		{
			List<LogWriterConfiguration> settings = new List<LogWriterConfiguration>(GetLogWriterSettings().Where(x => !x.IsDefault));
			settings.Add(writer);
			SetLogWriterSettings(settings);
		}

	}
}
