///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018 Sascha Falk <sascha@falk-online.eu>
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
	/// The log configuration without persistence (purely in memory, thread-safe).
	/// </summary>
	public partial class LogConfiguration : ILogConfiguration
	{
		private string mApplicationName;
		private Dictionary<string, string> mGlobalSettings;
		private Dictionary<string, Dictionary<string, string>> mProcessingPipelineStageSettings;
		private List<LogWriter> mLogWriterSettings;
		private object mSync = new object();

		/// <summary>
		/// Initializes a new instance of the <see cref="LogConfiguration"/> class.
		/// </summary>
		public LogConfiguration()
		{
			mGlobalSettings = new Dictionary<string, string>();
			mProcessingPipelineStageSettings = new Dictionary<string, Dictionary<string, string>>();
			mLogWriterSettings = new List<LogWriter>();
			mLogWriterSettings.Add(new LogWriter()); // LogWriter comes with defaults...
			mApplicationName = AppDomain.CurrentDomain.FriendlyName;
		}

		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		public string ApplicationName
		{
			get { return mApplicationName; }
			set {
				if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Invalid application name.");
				mApplicationName = value;
			}
		}

		/// <summary>
		/// Gets a bit mask in which each bit is associated with a log level with the same id
		/// and expresses whether the corresponding log level is active for the specified writer.
		/// </summary>
		/// <param name="writer">Log writer to get the active log level mask for.</param>
		/// <returns>The requested active log level mask.</returns>
		public BitMask GetActiveLogLevelMask(GriffinPlus.Lib.Logging.LogWriter writer)
		{
			// get the first matching log writer settings
			var settings = mLogWriterSettings
				.Where(x => x.Pattern.Regex.IsMatch(writer.Name))
				.FirstOrDefault();

			if (settings != null)
			{
				BitMask mask;

				// enable all log levels that are covered by the base level
				LogLevel level = LogLevel.GetAspect(settings.BaseLevel); // returns predefined log levels as well
				if (level == LogLevel.All) {
					mask = new BitMask(LogLevel.MaxId + 1, true, false);
				} else {
					mask = new BitMask(LogLevel.MaxId + 1, false, false);
					mask.SetBits(0, level.Id + 1);
				}

				// add log levels explicitly included
				foreach (var include in settings.Includes)
				{
					level = LogLevel.GetAspect(include);
					mask.SetBit(level.Id);
				}

				// disable log levels explicitly excluded
				foreach (var exclude in settings.Excludes)
				{
					level = LogLevel.GetAspect(exclude);
					mask.ClearBit(level.Id);
				}

				return mask;
			}
			else
			{
				// no matching settings found
				// => disable all log levels...
				return new BitMask(0, false, false);
			}
		}

		/// <summary>
		/// Gets the current log writer settings.
		/// </summary>
		/// <returns>A copy of the internal log writer settings.</returns>
		public IList<LogWriter> GetLogWriterSettings()
		{
			// return copy to avoid uncontrolled modifications of the collection
			return new List<LogWriter>(mLogWriterSettings);
		}

		/// <summary>
		/// Sets the log writer settings to use.
		/// </summary>
		/// <param name="settings">Settings to use.</param>
		public void SetLogWriterSettings(IEnumerable<LogWriter> settings)
		{
			// copy mutable log writer settings and replace entire collection atomically to avoid threading issues
			mLogWriterSettings = new List<LogWriter>(settings.Select(x => new LogWriter(x)));
		}

		/// <summary>
		/// Sets the log writer settings to use.
		/// </summary>
		/// <param name="settings">Settings to use.</param>
		public void SetLogWriterSettings(params LogWriter[] settings)
		{
			// copy mutable log writer settings and replace entire collection atomically to avoid threading issues
			mLogWriterSettings = new List<LogWriter>(settings.Select(x => new LogWriter(x)));
		}

		/// <summary>
		/// Gets the settings for pipeline stages by their name.
		/// </summary>
		/// <returns>The requested settings.</returns>
		public IDictionary<string, IDictionary<string, string>> GetProcessingPipelineStageSettings()
		{
			// return copy to avoid uncontrolled modifications of the collection
			IDictionary<string, IDictionary<string, string>> settingsByName = new Dictionary<string, IDictionary<string, string>>();
			foreach (var kvp in mProcessingPipelineStageSettings) {
				settingsByName.Add(kvp.Key, new Dictionary<string, string>(kvp.Value));
			}

			return settingsByName;
		}

		/// <summary>
		/// Gets the settings for the pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the pipeline stage to get the settings for.</param>
		/// <returns>
		/// The requested settings;
		/// null, if the settings do not exist.
		/// </returns>
		public IDictionary<string, string> GetProcessingPipelineStageSettings(string name)
		{
			// return copy to avoid uncontrolled modifications of the collection
			Dictionary<string, string> settings;
			if (mProcessingPipelineStageSettings.TryGetValue(name, out settings)) {
				return new Dictionary<string, string>(settings);
			}

			return null;
		}

		/// <summary>
		/// Sets the settings for the pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the pipeline stage to set the settings for.</param>
		/// <param name="settings">Settings to set.</param>
		public void SetProcessingPipelineStageSettings(string name, IDictionary<string, string> settings)
		{
			if (settings == null) throw new ArgumentNullException(nameof(settings));

			// replace atomically to avoid threading issues
			lock (mSync)
			{
				var copy = new Dictionary<string, Dictionary<string, string>>(mProcessingPipelineStageSettings);
				copy[name] = new Dictionary<string, string>(settings);
				mProcessingPipelineStageSettings = copy;
			}
		}

		/// <summary>
		/// Saves the configuration (not supported).
		/// </summary>
		public void Save()
		{
			// no persistence => nothing to do...
		}

	}
}
