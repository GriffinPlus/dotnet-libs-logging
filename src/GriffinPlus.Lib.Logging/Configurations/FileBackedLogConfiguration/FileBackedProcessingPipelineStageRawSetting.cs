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

using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A raw setting in a <see cref="FileBackedProcessingPipelineStageConfiguration"/>.
	/// </summary>
	public class FileBackedProcessingPipelineStageRawSetting : IProcessingPipelineStageRawSetting
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FileBackedProcessingPipelineStageRawSetting"/> class.
		/// </summary>
		/// <param name="configuration">The configuration the setting belongs to.</param>
		/// <param name="name">Name of the setting.</param>
		/// <param name="defaultValue">The default value of the setting.</param>
		internal FileBackedProcessingPipelineStageRawSetting(
			FileBackedProcessingPipelineStageConfiguration configuration,
			string name,
			string defaultValue)
		{
			StageConfiguration = configuration;
			Name = name;
			DefaultValue = defaultValue;
		}

		/// <summary>
		/// Gets the configuration the setting belongs to.
		/// </summary>
		public FileBackedProcessingPipelineStageConfiguration StageConfiguration { get; }

		/// <summary>
		/// Gets the configuration the setting belongs to.
		/// </summary>
		IProcessingPipelineStageConfiguration IProcessingPipelineStageRawSetting.Configuration { get; }

		/// <summary>
		/// Gets the name of the setting.
		/// </summary>
		public string Name { get; } // immutable => no sync necessary

		/// <summary>
		/// Gets a value indicating whether the setting has valid value (true) or just its default value (false).
		/// </summary>
		public bool HasValue
		{
			get
			{
				lock (StageConfiguration.Sync)
				{
					if (StageConfiguration.LogConfiguration.File.ProcessingPipelineStageSettings.TryGetValue(StageConfiguration.Name, out var settings))
					{
						if (settings.TryGetValue(Name, out _))
						{
							return true;
						}
					}

					return false;
				}
			}
		}

		/// <summary>
		/// Gets or sets the value of the setting.
		/// </summary>
		public string Value
		{
			get
			{
				lock (StageConfiguration.Sync)
				{
					if (StageConfiguration.LogConfiguration.File.ProcessingPipelineStageSettings.TryGetValue(StageConfiguration.Name, out var settings))
					{
						if (settings.TryGetValue(Name, out var value))
						{
							return value;
						}
					}

					return DefaultValue;
				}
			}

			set
			{
				lock (StageConfiguration.Sync)
				{
					if (!StageConfiguration.LogConfiguration.File.ProcessingPipelineStageSettings.TryGetValue(StageConfiguration.Name, out var settings))
					{
						settings = new Dictionary<string, string>();
						StageConfiguration.LogConfiguration.File.ProcessingPipelineStageSettings.Add(StageConfiguration.Name, settings);
					}

					settings[Name] = value;
				}
			}
		}

		/// <summary>
		/// Gets the default value of the setting.
		/// </summary>
		public string DefaultValue { get; }

		/// <summary>
		/// Gets the string representation of the setting.
		/// </summary>
		/// <returns>String representation of the setting.</returns>
		public override string ToString()
		{
			lock (StageConfiguration.Sync)
			{
				return HasValue
					? $"Name: '{Name}', Value: '{Value}'"
					: $"Name: '{Name}', Value: <no value> (defaults to: '{DefaultValue}'";
			}
		}
	}
}
