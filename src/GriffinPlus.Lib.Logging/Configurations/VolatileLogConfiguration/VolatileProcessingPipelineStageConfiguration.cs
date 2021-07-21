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
		/// Registers the setting with the specified name (supports custom types using the specified converters).
		/// Creates a new setting with the specified value, if the setting does not exist.
		/// </summary>
		/// <typeparam name="T">Type of the setting.</typeparam>
		/// <param name="name">
		/// Name of the setting. The following characters are allowed:
		/// - alphanumeric characters ( a-z, A-Z, 0-9 )
		/// - square brackets ( [] )
		/// - Period (.)
		/// </param>
		/// <param name="defaultValue">Value of the setting, if the setting does not exist, yet.</param>
		/// <param name="valueToStringConverter">Delegate that converts a setting value to its string representation.</param>
		/// <param name="stringToValueConverter">Delegate that converts the string representation of a setting value to an object of the specified type.</param>
		/// <returns>The setting.</returns>
		/// <exception cref="ArgumentNullException">
		/// The argument <paramref name="name"/>, <paramref name="valueToStringConverter"/> and/or <paramref name="stringToValueConverter"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// The setting exists already, but the specified type differs from the value type of the existing setting.
		/// </exception>
		/// <exception cref="FormatException">
		/// The <paramref name="name"/> is not a valid setting name.
		/// </exception>
		public override IProcessingPipelineStageSetting<T> RegisterSetting<T>(
			string          name,
			T               defaultValue,
			Func<T, string> valueToStringConverter,
			Func<string, T> stringToValueConverter)
		{
			// check arguments
			if (valueToStringConverter == null) throw new ArgumentNullException(nameof(valueToStringConverter));
			if (stringToValueConverter == null) throw new ArgumentNullException(nameof(stringToValueConverter));
			CheckSettingName(name);

			lock (Sync)
			{
				if (!mSettings.TryGetValue(name, out var setting))
				{
					// the setting with the specified name was not requested before
					// => create a new setting 
					setting = new VolatileProcessingPipelineStageSetting<T>(this, name, valueToStringConverter, stringToValueConverter);
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
				if (!setting.HasDefaultValue)
					((VolatileProcessingPipelineStageSetting<T>)setting).DefaultValue = defaultValue;

				// ensure that the setting default values are the same
				string settingDefaultValueAsString = valueToStringConverter(((VolatileProcessingPipelineStageSetting<T>)setting).DefaultValue);
				string defaultValueAsString = valueToStringConverter(defaultValue);
				if (settingDefaultValueAsString != defaultValueAsString)
				{
					string message = $"The setting exists already, but the specified default value ({defaultValueAsString}) does not match the default value of the existing setting ({settingDefaultValueAsString}).";
					throw new ArgumentException(message);
				}

				return (IProcessingPipelineStageSetting<T>)setting;
			}
		}

		/// <summary>
		/// Gets the setting with the specified name (supports custom types using the specified converters).
		/// </summary>
		/// <typeparam name="T">Type of the setting.</typeparam>
		/// <param name="name">
		/// Name of the setting. The following characters are allowed:
		/// - alphanumeric characters ( a-z, A-Z, 0-9 )
		/// - square brackets ( [] )
		/// - Period (.)
		/// </param>
		/// <param name="valueToStringConverter">Delegate that converts a setting value to its string representation.</param>
		/// <param name="stringToValueConverter">Delegate that converts the string representation of a setting value to an object of the specified type.</param>
		/// <returns>The setting (<c>null</c> if the setting does not exist).</returns>
		/// <exception cref="ArgumentNullException">
		/// The argument <paramref name="name"/>, <paramref name="valueToStringConverter"/> and/or <paramref name="stringToValueConverter"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// The setting exists, but the specified type differs from the value type of the existing setting.
		/// </exception>
		/// <exception cref="FormatException">
		/// The <paramref name="name"/> is not a valid setting name.
		/// </exception>
		public override IProcessingPipelineStageSetting<T> GetSetting<T>(
			string          name,
			Func<T, string> valueToStringConverter,
			Func<string, T> stringToValueConverter)
		{
			// check arguments
			if (valueToStringConverter == null) throw new ArgumentNullException(nameof(valueToStringConverter));
			if (stringToValueConverter == null) throw new ArgumentNullException(nameof(stringToValueConverter));
			CheckSettingName(name);

			lock (Sync)
			{
				// abort, if there is no setting with that name
				if (!mSettings.TryGetValue(name, out var setting))
					return null;

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
		/// Sets the setting with the specified name (supports custom types using the specified converters).
		/// Creates a new setting, if it does not exist, yet.
		/// </summary>
		/// <typeparam name="T">Type of the setting.</typeparam>
		/// <param name="name">
		/// Name of the setting. The following characters are allowed:
		/// - alphanumeric characters ( a-z, A-Z, 0-9 )
		/// - square brackets ( [] )
		/// - Period (.)
		/// </param>
		/// <param name="value">New value of the setting.</param>
		/// <param name="valueToStringConverter">Delegate that converts the object to its string representation.</param>
		/// <param name="stringToValueConverter">Delegate that converts the string representation to an object of the type <typeparamref name="T"/>.</param>
		/// <returns>The setting.</returns>
		/// <exception cref="ArgumentNullException">
		/// The argument <paramref name="name"/>, <paramref name="valueToStringConverter"/> and/or <paramref name="stringToValueConverter"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// The setting exists already, but the specified type differs from the value type of the existing setting.
		/// </exception>
		/// <exception cref="FormatException">
		/// The <paramref name="name"/> is not a valid setting name.
		/// </exception>
		public override IProcessingPipelineStageSetting<T> SetSetting<T>(
			string          name,
			T               value,
			Func<T, string> valueToStringConverter,
			Func<string, T> stringToValueConverter)
		{
			// check arguments
			if (valueToStringConverter == null) throw new ArgumentNullException(nameof(valueToStringConverter));
			if (stringToValueConverter == null) throw new ArgumentNullException(nameof(stringToValueConverter));
			CheckSettingName(name);

			lock (Sync)
			{
				if (!mSettings.TryGetValue(name, out var setting))
				{
					// the setting with the specified name was not requested before
					// => create a new setting 
					setting = new VolatileProcessingPipelineStageSetting<T>(this, name, valueToStringConverter, stringToValueConverter);
					mSettings.Add(name, setting);
				}

				// setting with the same name exists

				// ensure that the setting value types are the same
				if (setting.ValueType != typeof(T))
				{
					string message = $"The setting exists already, but the specified types ({typeof(T).FullName}) differs from the value type of the existing setting ({setting.ValueType.FullName}).";
					throw new ArgumentException(message);
				}

				// set the value of the setting
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
