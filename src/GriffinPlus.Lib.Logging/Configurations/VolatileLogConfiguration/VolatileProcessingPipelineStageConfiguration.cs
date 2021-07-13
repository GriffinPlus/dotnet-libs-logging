///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

using GriffinPlus.Lib.Threading;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// The configuration for a pipeline stage configuration (thread-safe).
	/// </summary>
	public class VolatileProcessingPipelineStageConfiguration : ProcessingPipelineStageConfigurationBase
	{
		private readonly Dictionary<string, VolatileProcessingPipelineStageRawSetting> mRawSettings = new Dictionary<string, VolatileProcessingPipelineStageRawSetting>();
		private readonly Dictionary<string, IUntypedProcessingPipelineStageSetting>    mSettings    = new Dictionary<string, IUntypedProcessingPipelineStageSetting>();

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
		/// Registers the setting with the specified name
		/// (creates a new setting with the specified default value, if the setting does not exist).
		/// </summary>
		/// <typeparam name="T">Type of the setting (can be a primitive type or string).</typeparam>
		/// <param name="name">Name of the setting.</param>
		/// <param name="defaultValue">Default value of the setting, if the setting does not exist, yet.</param>
		/// <returns>The setting.</returns>
		public override IProcessingPipelineStageSetting<T> RegisterSetting<T>(string name, T defaultValue)
		{
			// ensure that the specified name is well-formed and the setting value type is supported
			CheckSettingName(name);
			CheckSettingTypeIsSupported(typeof(T));

			lock (Sync)
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

				VolatileProcessingPipelineStageRawSetting rawSetting;
				if (!mSettings.TryGetValue(name, out var setting))
				{
					// the setting with the specified name was not requested before
					// => check whether there is an underlying raw setting
					if (!mRawSettings.TryGetValue(name, out rawSetting))
					{
						// the underlying raw setting does not exist => create it
						rawSetting = new VolatileProcessingPipelineStageRawSetting(this, name, toConverter(defaultValue));
						mRawSettings.Add(name, rawSetting);
					}

					// create a new setting with the specified name
					setting = new VolatileProcessingPipelineStageSetting<T>(rawSetting, fromConverter, toConverter);
					mSettings.Add(name, setting);
				}

				// setting with the same name exists

				// ensure that the setting value types are the same
				if (setting.ValueType != typeof(T))
				{
					string message = $"The setting exists already, but the specified types ({typeof(T).FullName}) differs from the value type of the existing setting ({setting.ValueType.FullName}).";
					throw new ArgumentException(message);
				}

				// set the default value of the raw item, if it is not already set
				// (this can happen, if a setting has been created without a default value before)
				rawSetting = ((VolatileProcessingPipelineStageSetting<T>)setting).Raw;
				if (!rawSetting.HasDefaultValue) rawSetting.DefaultValue = toConverter(defaultValue);

				// ensure that the setting default values are the same
				if (!Equals(setting.DefaultValue, defaultValue))
				{
					string message = $"The setting exists already, but the specified default value ({defaultValue}) does not match the default value of the existing setting ({setting.DefaultValue}).";
					throw new ArgumentException(message);
				}

				return (IProcessingPipelineStageSetting<T>)setting;
			}
		}

		/// <summary>
		/// Gets the setting with the specified name.
		/// </summary>
		/// <typeparam name="T">Type of the setting (can be a primitive type or string).</typeparam>
		/// <param name="name">Name of the setting.</param>
		/// <returns>The setting (<c>null</c> if the setting does not exist).</returns>
		public override IProcessingPipelineStageSetting<T> GetSetting<T>(string name)
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

					// abort, if there is no raw setting backing the setting
					if (!mRawSettings.TryGetValue(name, out var rawSetting))
						return null;

					// a raw setting with the specified name exists
					// => create a new setting based on the raw setting
					setting = new VolatileProcessingPipelineStageSetting<T>(rawSetting, fromConverter, toConverter);
					mSettings.Add(name, setting);
				}

				// setting with the name exists

				// ensure that the setting value types are the same
				if (setting.ValueType != typeof(T))
				{
					string message = $"The setting exists already, but the specified types ({typeof(T).FullName}) differs from the value type of the existing setting ({setting.ValueType.FullName}).";
					throw new ArgumentException(message);
				}

				return (IProcessingPipelineStageSetting<T>)setting;
			}
		}

		/// <summary>
		/// Sets the setting with the specified name (creates a new setting, if it does not exist, yet).
		/// </summary>
		/// <typeparam name="T">Type of the setting (can be a primitive type or string).</typeparam>
		/// <param name="name">Name of the setting.</param>
		/// <param name="value">New value of the setting.</param>
		/// <returns>The setting.</returns>
		public override IProcessingPipelineStageSetting<T> SetSetting<T>(string name, T value)
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

					// create a new raw setting, if it does not exist, yet
					if (!mRawSettings.TryGetValue(name, out var rawSetting))
					{
						rawSetting = new VolatileProcessingPipelineStageRawSetting(this, name);
						mRawSettings.Add(name, rawSetting);
					}

					// create a new setting based on the raw setting
					setting = new VolatileProcessingPipelineStageSetting<T>(rawSetting, fromConverter, toConverter);
					mSettings.Add(name, setting);
				}

				// setting with the same name exists

				// ensure that the setting value types are the same
				if (setting.ValueType != typeof(T))
				{
					string message = $"The setting exists already, but the specified types ({typeof(T).FullName}) differs from the value type of the existing setting ({setting.ValueType.FullName}).";
					throw new ArgumentException(message);
				}

				// set the value
				setting.Value = value;

				return (IProcessingPipelineStageSetting<T>)setting;
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
