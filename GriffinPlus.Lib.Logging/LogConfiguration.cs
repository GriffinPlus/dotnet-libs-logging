///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://griffin.plus)
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
using System.IO;
using System.Linq;
using System.Threading;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// The log configuration without persistence (purely in memory).
	/// </summary>
	public partial class LogConfiguration : ILogConfiguration
	{
		private string mApplicationName;
		private Dictionary<string, string> mGlobalSettings;
		private Dictionary<string, Dictionary<string, string>> mProcessingPipelineStageSettings;
		private List<LogWriter> mLogWriterSettings;

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
			get
			{
				return mApplicationName;
			}

			set
			{
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
		public LogLevelBitMask GetActiveLogLevelMask(GriffinPlus.Lib.Logging.LogWriter writer)
		{
			// get the first matching log writer settings
			var settings = mLogWriterSettings
				.Where(x => x.Pattern.Regex.IsMatch(writer.Name))
				.FirstOrDefault();

			if (settings != null)
			{
				LogLevelBitMask mask;

				// enable all log levels that are covered by the base level
				LogLevel level = LogLevel.GetAspect(settings.BaseLevel); // returns predefined log levels as well
				if (level == LogLevel.All) {
					mask = new LogLevelBitMask(LogLevel.MaxId + 1, true, false);
				} else {
					mask = new LogLevelBitMask(LogLevel.MaxId + 1, false, false);
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
				return new LogLevelBitMask(0, false, false);
			}
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
			mProcessingPipelineStageSettings[name] = new Dictionary<string, string>(settings);
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
