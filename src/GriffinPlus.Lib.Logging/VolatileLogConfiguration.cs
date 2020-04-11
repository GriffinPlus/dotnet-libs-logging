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
	/// The log configuration without persistence (purely in memory, thread-safe).
	/// </summary>
	public class VolatileLogConfiguration : LogConfiguration
	{
		private string mApplicationName;
		private Dictionary<string, IReadOnlyDictionary<string, string>> mProcessingPipelineStageSettings;
		private List<LogWriter> mLogWriterSettings;
		private readonly object mSync = new object();

		/// <summary>
		/// Initializes a new instance of the <see cref="VolatileLogConfiguration"/> class.
		/// </summary>
		public VolatileLogConfiguration()
		{
			mProcessingPipelineStageSettings = new Dictionary<string, IReadOnlyDictionary<string, string>>();
			mLogWriterSettings = new List<LogWriter>();
			mLogWriterSettings.Add(new LogWriter() { IsDefault = true }); // LogWriter comes with defaults...
			mApplicationName = AppDomain.CurrentDomain.FriendlyName;
		}

		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		public override string ApplicationName
		{
			get => mApplicationName;
			set {
				lock (mSync)
				{
					if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Invalid application name.");
					mApplicationName = value;
				}
			}
		}

		/// <summary>
		/// Gets a bit mask in which each bit is associated with a log level with the same id
		/// and expresses whether the corresponding log level is active for the specified writer.
		/// </summary>
		/// <param name="writer">Log writer to get the active log level mask for.</param>
		/// <returns>The requested active log level mask.</returns>
		public override LogLevelBitMask GetActiveLogLevelMask(GriffinPlus.Lib.Logging.LogWriter writer)
		{
			lock (mSync)
			{
				// get the first matching log writer settings
				var settings = mLogWriterSettings.FirstOrDefault(x => x.Pattern.Regex.IsMatch(writer.Name));

				if (settings != null)
				{
					LogLevelBitMask mask;

					// enable all log levels that are covered by the base level
					LogLevel level = LogLevel.GetAspect(settings.BaseLevel); // returns predefined log levels as well
					if (level == LogLevel.All)
					{
						mask = new LogLevelBitMask(LogLevel.MaxId + 1, true, false);
					}
					else
					{
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
		}

		/// <summary>
		/// Gets the current log writer settings.
		/// </summary>
		/// <returns>A copy of the internal log writer settings.</returns>
		public override IEnumerable<LogWriter> GetLogWriterSettings()
		{
			lock (mSync)
			{
				// mLogWriterSettings is immutable after it has been set
				// => copying is not necessary
				return mLogWriterSettings;
			}
		}

		/// <summary>
		/// Sets the log writer settings to use.
		/// </summary>
		/// <param name="settings">Settings to use.</param>
		public override void SetLogWriterSettings(IEnumerable<LogWriter> settings)
		{
			lock (mSync)
			{
				// copy mutable log writer settings and replace entire collection atomically to avoid threading issues
				mLogWriterSettings = new List<LogWriter>(settings.Select(x => new LogWriter(x)));
			}
		}

		/// <summary>
		/// Sets the log writer settings to use.
		/// </summary>
		/// <param name="settings">Settings to use.</param>
		public override void SetLogWriterSettings(params LogWriter[] settings)
		{
			lock (mSync)
			{
				// copy mutable log writer settings and replace entire collection atomically to avoid threading issues
				mLogWriterSettings = new List<LogWriter>(settings.Select(x => new LogWriter(x)));
			}
		}

		/// <summary>
		/// Gets the settings for pipeline stages by their name.
		/// </summary>
		/// <returns>The requested settings.</returns>
		public override IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetProcessingPipelineStageSettings()
		{
			lock (mSync)
			{
				// mProcessingPipelineStageSettings is immutable after it has been set
				// => copying is not necessary
				return mProcessingPipelineStageSettings;
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
		public override IReadOnlyDictionary<string, string> GetProcessingPipelineStageSettings(string name)
		{
			lock (mSync)
			{
				// mProcessingPipelineStageSettings is immutable after it has been set
				// => copying is not necessary
				if (mProcessingPipelineStageSettings.TryGetValue(name, out var settings))
				{
					return settings;
				}

				return null;
			}
		}

		/// <summary>
		/// Sets the settings for the pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the pipeline stage to set the settings for.</param>
		/// <param name="settings">Settings to set.</param>
		public override void SetProcessingPipelineStageSettings(string name, IReadOnlyDictionary<string, string> settings)
		{
			if (settings == null) throw new ArgumentNullException(nameof(settings));

			lock (mSync)
			{
				// replace atomically to avoid threading issues
				lock (mSync)
				{
					var copy = new Dictionary<string, IReadOnlyDictionary<string, string>>(mProcessingPipelineStageSettings);
					if (settings is IDictionary<string, string> dict)
					{
						copy[name] = new Dictionary<string, string>(dict);
					}
					else
					{
						var tmp = new Dictionary<string, string>();
						foreach (var kvp in settings) tmp.Add(kvp.Key, kvp.Value);
						copy[name] = tmp;
					}

					mProcessingPipelineStageSettings = copy;
				}
			}
		}

		/// <summary>
		/// Saves the configuration (not supported).
		/// </summary>
		public override void Save()
		{
			// no persistence => nothing to do...
		}

	}
}
