///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// The configuration for a file-backed pipeline stage configuration (thread-safe).
	/// </summary>
	public class FileBackedProcessingPipelineStageConfiguration : ProcessingPipelineStageConfigurationBase
	{
		// private readonly Dictionary<string, FileBackedProcessingPipelineStageRawSetting> mRawSettings = new Dictionary<string, FileBackedProcessingPipelineStageRawSetting>();
		private readonly Dictionary<string, IUntypedProcessingPipelineStageSetting> mSettings = new Dictionary<string, IUntypedProcessingPipelineStageSetting>();

		/// <summary>
		/// Initializes a new instance of the <see cref="FileBackedProcessingPipelineStageConfiguration"/> class.
		/// </summary>
		/// <param name="configuration">The log configuration the pipeline stage configuration belongs to.</param>
		/// <param name="name">Name of the pipeline stage the configuration belongs to.</param>
		internal FileBackedProcessingPipelineStageConfiguration(FileBackedLogConfiguration configuration, string name) : base(configuration.Sync)
		{
			LogConfiguration = configuration;
			Name = name ?? throw new ArgumentNullException(nameof(name));
		}

		/// <summary>
		/// Gets the log configuration the pipeline stage configuration belongs to.
		/// </summary>
		internal FileBackedLogConfiguration LogConfiguration { get; }

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

					var rawSetting = new FileBackedProcessingPipelineStageRawSetting(
						this,
						name,
						toConverter(defaultValue));

					var newSetting = new FileBackedProcessingPipelineStageSetting<T>(
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
					string message = $"The setting exists already, but the specified types ({typeof(T).FullName}) differs from the value type of the existing setting ({setting.ValueType.FullName}).";
					throw new ArgumentException(message);
				}

				// ensure that the setting default values are the same
				if (!Equals(setting.DefaultValue, defaultValue))
				{
					string message = $"The setting exists already, but the specified default value ({defaultValue}) does not match the default value of the existing setting ({setting.DefaultValue}).";
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
