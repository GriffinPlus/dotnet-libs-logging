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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// The configuration for a pipeline stage configuration (thread-safe).
	/// </summary>
	public class ProcessingPipelineStageConfiguration : IProcessingPipelineStageConfiguration
	{
		private static readonly Dictionary<Type, Delegate> sValueFromStringConverters = new Dictionary<Type, Delegate>();
		private static readonly Dictionary<Type, Delegate> sValueToStringConverters = new Dictionary<Type, Delegate>();
		private Dictionary<string, IUntypedProcessingPipelineStageSetting> mSettings = new Dictionary<string, IUntypedProcessingPipelineStageSetting>();
		private static Regex sValidSettingNameRegex = new Regex("^[a-zA-Z0-9\\[\\]\\.]+$", RegexOptions.Compiled);

		/// <summary>
		/// Initializes the <see cref="ProcessingPipelineStageConfiguration"/> class.
		/// </summary>
		static ProcessingPipelineStageConfiguration()
		{
			var culture = CultureInfo.InvariantCulture;

			// signed integers
			sValueFromStringConverters[typeof(sbyte)] = new ProcessingPipelineStageSetting<sbyte>.ValueFromStringConverter(s => Convert.ToSByte(s, culture));
			sValueToStringConverters[typeof(sbyte)] = new ProcessingPipelineStageSetting<sbyte>.ValueToStringConverter(v => Convert.ToString(v, culture));
			sValueFromStringConverters[typeof(short)] = new ProcessingPipelineStageSetting<short>.ValueFromStringConverter(s => Convert.ToInt16(s, culture));
			sValueToStringConverters[typeof(short)] = new ProcessingPipelineStageSetting<short>.ValueToStringConverter(v => Convert.ToString(v, culture));
			sValueFromStringConverters[typeof(int)] = new ProcessingPipelineStageSetting<int>.ValueFromStringConverter(s => Convert.ToInt32(s, culture));
			sValueToStringConverters[typeof(int)] = new ProcessingPipelineStageSetting<int>.ValueToStringConverter(v => Convert.ToString(v, culture));
			sValueFromStringConverters[typeof(long)] = new ProcessingPipelineStageSetting<long>.ValueFromStringConverter(s => Convert.ToInt64(s, culture));
			sValueToStringConverters[typeof(long)] = new ProcessingPipelineStageSetting<long>.ValueToStringConverter(v => Convert.ToString(v, culture));

			// unsigned integers
			sValueFromStringConverters[typeof(byte)] = new ProcessingPipelineStageSetting<byte>.ValueFromStringConverter(s => Convert.ToByte(s, culture));
			sValueToStringConverters[typeof(byte)] = new ProcessingPipelineStageSetting<byte>.ValueToStringConverter(v => Convert.ToString(v, culture));
			sValueFromStringConverters[typeof(ushort)] = new ProcessingPipelineStageSetting<ushort>.ValueFromStringConverter(s => Convert.ToUInt16(s, culture));
			sValueToStringConverters[typeof(ushort)] = new ProcessingPipelineStageSetting<ushort>.ValueToStringConverter(v => Convert.ToString(v, culture));
			sValueFromStringConverters[typeof(uint)] = new ProcessingPipelineStageSetting<uint>.ValueFromStringConverter(s => Convert.ToUInt32(s, culture));
			sValueToStringConverters[typeof(uint)] = new ProcessingPipelineStageSetting<uint>.ValueToStringConverter(v => Convert.ToString(v, culture));
			sValueFromStringConverters[typeof(ulong)] = new ProcessingPipelineStageSetting<ulong>.ValueFromStringConverter(s => Convert.ToUInt64(s, culture));
			sValueToStringConverters[typeof(ulong)] = new ProcessingPipelineStageSetting<ulong>.ValueToStringConverter(v => Convert.ToString(v, culture));

			// floating point numbers
			sValueFromStringConverters[typeof(float)] = new ProcessingPipelineStageSetting<float>.ValueFromStringConverter(s => Convert.ToSingle(s, culture));
			sValueToStringConverters[typeof(float)] = new ProcessingPipelineStageSetting<float>.ValueToStringConverter(v => Convert.ToString(v, culture));
			sValueFromStringConverters[typeof(double)] = new ProcessingPipelineStageSetting<double>.ValueFromStringConverter(s => Convert.ToDouble(s, culture));
			sValueToStringConverters[typeof(double)] = new ProcessingPipelineStageSetting<double>.ValueToStringConverter(v => Convert.ToString(v, culture));

			// decimal numbers
			sValueFromStringConverters[typeof(decimal)] = new ProcessingPipelineStageSetting<decimal>.ValueFromStringConverter(s => Convert.ToDecimal(s, culture));
			sValueToStringConverters[typeof(decimal)] = new ProcessingPipelineStageSetting<decimal>.ValueToStringConverter(v => Convert.ToString(v, culture));

			// strings
			sValueFromStringConverters[typeof(string)] = new ProcessingPipelineStageSetting<string>.ValueFromStringConverter(s => s);
			sValueToStringConverters[typeof(string)] = new ProcessingPipelineStageSetting<string>.ValueToStringConverter(v => v);

			// enumerations are handled in a generic way, see below...
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessingPipelineStageConfiguration"/> class.
		/// </summary>
		/// <param name="sync">Object to use for synchronizing access to the configuration (may be null).</param>
		public ProcessingPipelineStageConfiguration(object sync = null)
		{
			Sync = sync ?? new object();
		}

		/// <summary>
		/// Gets the object used to synchronize access to the configuration (and its settings).
		/// </summary>
		internal object Sync { get; }

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
		public IProcessingPipelineStageSetting<T> GetSetting<T>(string name, T defaultValue)
		{
			// ensure that the specified name is well-formed and the setting value type is supported
			CheckSettingName(name);
			CheckSettingTypeIsSupported(typeof(T));

			lock (Sync)
			{
				// add setting, if it does not exist, yet
				if (!mSettings.TryGetValue(name, out var setting))
				{
					ProcessingPipelineStageSetting<T>.ValueFromStringConverter fromConverter;
					ProcessingPipelineStageSetting<T>.ValueToStringConverter toConverter;

					if (typeof(T).IsEnum)
					{
						fromConverter = ConvertStringToEnum<T>;
						toConverter = ConvertEnumToString<T>;
					}
					else
					{
						fromConverter = sValueFromStringConverters[typeof(T)] as ProcessingPipelineStageSetting<T>.ValueFromStringConverter;
						toConverter = sValueToStringConverters[typeof(T)] as ProcessingPipelineStageSetting<T>.ValueToStringConverter;
					}

					var newSetting = new ProcessingPipelineStageSetting<T>(
						this,
						fromConverter,
						toConverter,
						name,
						defaultValue);

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

		/// <summary>
		/// Checks the name of a setting and throws an exception, if it is not valid.
		/// </summary>
		/// <param name="name">Name to check.</param>
		/// <exception cref="ArgumentNullException"><paramref name="name"/> is a null reference.</exception>
		/// <exception cref="FormatException"><paramref name="name"/> is not a valid setting name.</exception>
		private void CheckSettingName(string name)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));
			if (!sValidSettingNameRegex.IsMatch(name)) throw new ArgumentException($"The specified setting name ({name}) is malformed.", nameof(name));
		}

		/// <summary>
		/// Checks whether the specified type is supported as a setting value.
		/// </summary>
		/// <param name="type">Type to check.</param>
		private void CheckSettingTypeIsSupported(Type type)
		{
			if (type.IsEnum) return;
			if (!sValueFromStringConverters.ContainsKey(type)) throw new NotSupportedException($"The specified setting type ({type.FullName}) is not supported.");
		}

		#region Implementation of IReadOnlyDictionary<>

		/// <summary>
		/// Gets the names of the settings in the configuration.
		/// </summary>
		IEnumerable<string> IReadOnlyDictionary<string, IUntypedProcessingPipelineStageSetting>.Keys
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
		IEnumerable<IUntypedProcessingPipelineStageSetting> IReadOnlyDictionary<string, IUntypedProcessingPipelineStageSetting>.Values
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
		int IReadOnlyCollection<KeyValuePair<string, IUntypedProcessingPipelineStageSetting>>.Count
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
		IUntypedProcessingPipelineStageSetting IReadOnlyDictionary<string, IUntypedProcessingPipelineStageSetting>.this[string key]
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
		bool IReadOnlyDictionary<string, IUntypedProcessingPipelineStageSetting>.ContainsKey(string key)
		{
			lock (Sync)
			{
				return mSettings.ContainsKey(key);
			}
		}

		/// <summary>
		/// Trys to get the setting with the specified name.
		/// </summary>
		/// <param name="key">Name of the setting to get.</param>
		/// <param name="value">Receives the setting, if it exists.</param>
		/// <returns>true, if the requested setting was successfully returned; otherwise false.</returns>
		bool IReadOnlyDictionary<string, IUntypedProcessingPipelineStageSetting>.TryGetValue(string key, out IUntypedProcessingPipelineStageSetting value)
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
		IEnumerator<KeyValuePair<string, IUntypedProcessingPipelineStageSetting>> IEnumerable<KeyValuePair<string, IUntypedProcessingPipelineStageSetting>>.GetEnumerator()
		{
			return new MonitorSynchronizedEnumerator<KeyValuePair<string, IUntypedProcessingPipelineStageSetting>>(mSettings.GetEnumerator(), Sync);
		}

		/// <summary>
		/// Gets an enumerator for iterating over the settings in the configuration
		/// (the enumerator keeps the configuration locked until it is disposed, so ensure it's Dispose() method is called).
		/// </summary>
		/// <returns>An enumerator for iterating over the settings in the configuration.</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return new MonitorSynchronizedEnumerator<KeyValuePair<string, IUntypedProcessingPipelineStageSetting>>(mSettings.GetEnumerator(), Sync);
		}

		#endregion

		#region Conversion of Setting Values

		/// <summary>
		/// Converts the specified enumeration value to a string.
		/// </summary>
		/// <typeparam name="T">Enumeration type to convert.</typeparam>
		/// <param name="value">Enumeration value to convert.</param>
		/// <returns>The string representation of the specified enumeration value.</returns>
		private static string ConvertEnumToString<T>(T value)
		{
			return value.ToString();
		}

		/// <summary>
		/// Converts the specified string to a value of the specified enumeration type.
		/// </summary>
		/// <typeparam name="T">Enumeration type to convert the string to.</typeparam>
		/// <param name="s">String to convert.</param>
		/// <returns>The converted enumeration value.</returns>
		private static T ConvertStringToEnum<T>(string s)
		{
			return (T)Enum.Parse(typeof(T), s);
		}

		#endregion

	}
}
