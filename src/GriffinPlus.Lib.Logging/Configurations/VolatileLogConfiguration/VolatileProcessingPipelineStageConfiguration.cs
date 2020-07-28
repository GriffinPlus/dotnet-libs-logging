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

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// The configuration for a pipeline stage configuration (thread-safe).
	/// </summary>
	public class VolatileProcessingPipelineStageConfiguration : ProcessingPipelineStageConfigurationBase
	{
		private readonly Dictionary<string, VolatileProcessingPipelineStageRawSetting> mRawSettings = new Dictionary<string, VolatileProcessingPipelineStageRawSetting>();
		private readonly Dictionary<string, IUntypedProcessingPipelineStageSetting> mSettings = new Dictionary<string, IUntypedProcessingPipelineStageSetting>();

		/// <summary>
		/// Initializes a new instance of the <see cref="VolatileProcessingPipelineStageConfiguration"/> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage the configuration belongs to.</param>
		/// <param name="sync">
		/// The configuration lock used to synchronize access to the configuration.
		/// Specify <c>null</c> to create a new lock.
		/// </param>
		public VolatileProcessingPipelineStageConfiguration(string name, object sync) : base(sync)
		{
			Name = name;
		}

		/// <summary>
		/// Gets the name of the processing pipeline stage the configuration belongs to.
		/// </summary>
		public override string Name { get; }

		/// <summary>
		/// Gets the setting with the specified name.
		/// </summary>
		/// <typeparam name="T">Type of the setting (can be a primitive type, a string or an enum).</typeparam>
		/// <param name="name">
		/// Name of the setting. The following characters are allowed:
		/// - alphanumeric characters ( a-z, A-Z, 0-9 )
		/// - square brackets ( [] )
		/// - Period (.)
		/// </param>
		/// <param name="defaultValue">Default value of the setting.</param>
		/// <returns>The requested setting.</returns>
		public override IProcessingPipelineStageSetting<T> GetSetting<T>(string name, T defaultValue)
		{
			// ensure that the specified name is well-formed and the setting value type is supported
			CheckSettingName(name);
			CheckSettingTypeIsSupported(typeof(T));

			lock (Sync)
			{
				if (!mSettings.TryGetValue(name, out var setting))
				{
					ValueFromStringConverter<T> fromConverter;
					ValueToStringConverter<T> toConverter;

					if (typeof(T).IsEnum)
					{
						fromConverter = ConvertStringToEnum<T>;
						toConverter = ConvertEnumToString;
					}
					else
					{
						fromConverter = (ValueFromStringConverter<T>)ValueFromStringConverters[typeof(T)];
						toConverter = (ValueToStringConverter<T>)ValueToStringConverters[typeof(T)];
					}

					if (!mRawSettings.TryGetValue(name, out var rawSetting))
					{
						rawSetting = new VolatileProcessingPipelineStageRawSetting(
							this,
							name,
							toConverter(defaultValue));

						mRawSettings.Add(name, rawSetting);
					}

					var newSetting = new VolatileProcessingPipelineStageSetting<T>(
						rawSetting,
						fromConverter,
						toConverter);

					mSettings.Add(name, newSetting);

					return newSetting;
				}

				// setting with the same name exists already

				// ensure that the setting value types are the same
				if (setting.ValueType != typeof(T))
				{
					var message = $"The setting exists already, but the specified types ({typeof(T).FullName}) differs from the value type of the existing setting ({setting.ValueType.FullName}).";
					throw new ArgumentException(message);
				}

				// ensure that the setting default values are the same
				if (!Equals(setting.DefaultValue, defaultValue))
				{
					var message = $"The setting exists already, but the specified default value ({defaultValue}) does not match the default value of the existing setting ({setting.DefaultValue}).";
					throw new ArgumentException(message);
				}

				return setting as IProcessingPipelineStageSetting<T>;
			}
		}

		#region Implementation of IReadOnlyDictionary<>

		/// <summary>
		/// Gets the names of the settings in the configuration.
		/// </summary>
		public override IEnumerable<string> Keys
		{
			get
			{
				lock (Sync)
				{
					return mSettings.Keys;
				}
			}
		}

		/// <summary>
		/// Gets the settings in the configuration.
		/// </summary>
		public override IEnumerable<IUntypedProcessingPipelineStageSetting> Values
		{
			get
			{
				lock (Sync)
				{
					return mSettings.Values;
				}
			}
		}

		/// <summary>
		/// Gets the number of settings in the configuration
		/// </summary>
		public override int Count
		{
			get
			{
				lock (Sync)
				{
					return mSettings.Count;
				}
			}
		}

		/// <summary>
		/// Gets the setting with the specified name.
		/// </summary>
		/// <param name="key">Name of the setting to get.</param>
		/// <returns>The setting with the specified name.</returns>
		public override IUntypedProcessingPipelineStageSetting this[string key]
		{
			get
			{
				lock (Sync)
				{
					return mSettings[key];
				}
			}
		}

		/// <summary>
		/// Checks whether the configuration contains a setting with the specified name.
		/// </summary>
		/// <param name="key">Name of the setting to check.</param>
		/// <returns>true, if the setting with the specified name exists in the configuration; otherwise false.</returns>
		public override bool ContainsKey(string key)
		{
			lock (Sync)
			{
				return mSettings.ContainsKey(key);
			}
		}

		/// <summary>
		/// Tries to get the setting with the specified name.
		/// </summary>
		/// <param name="key">Name of the setting to get.</param>
		/// <param name="value">Receives the setting, if it exists.</param>
		/// <returns>true, if the requested setting was successfully returned; otherwise false.</returns>
		public override bool TryGetValue(string key, out IUntypedProcessingPipelineStageSetting value)
		{
			lock (Sync)
			{
				return mSettings.TryGetValue(key, out value);
			}
		}

		/// <summary>
		/// Gets an enumerator for iterating over the settings in the configuration
		/// (the enumerator keeps the configuration locked until it is disposed, so ensure it's Dispose() method is called).
		/// </summary>
		/// <returns>An enumerator for iterating over the settings in the configuration.</returns>
		public override IEnumerator<KeyValuePair<string, IUntypedProcessingPipelineStageSetting>> GetEnumerator()
		{
			lock (Sync)
			{
				return new MonitorSynchronizedEnumerator<KeyValuePair<string, IUntypedProcessingPipelineStageSetting>>(mSettings.GetEnumerator(), Sync);
			}
		}

		#endregion

	}
}
