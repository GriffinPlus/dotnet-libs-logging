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
using System.Text.RegularExpressions;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Base class for pipeline stage configurations (thread-safe).
	/// </summary>
	public abstract class ProcessingPipelineStageConfigurationBase : IProcessingPipelineStageConfiguration
	{
		/// <summary>
		/// Delegate to a method that converts an object of the specified type to its string representation.
		/// </summary>
		/// <typeparam name="T">Type of the object to convert.</typeparam>
		/// <param name="obj">Object to convert to its string representation.</param>
		/// <returns>String representation of the specified object.</returns>
		protected internal delegate string ValueToStringConverter<in T>(T obj);

		/// <summary>
		/// Delegate to a method that converts a string to an object of the specified type.
		/// </summary>
		/// <typeparam name="T">Type of the object to convert the string to.</typeparam>
		/// <param name="s">String to convert to the specified object.</param>
		/// <returns>The object.</returns>
		protected internal delegate T ValueFromStringConverter<out T>(string s);

		/// <summary>
		/// Mapping of methods for converting objects of a specific type to string.
		/// </summary>
		protected static readonly Dictionary<Type, Delegate> ValueFromStringConverters = new Dictionary<Type, Delegate>();

		/// <summary>
		/// Mapping of methods for converting strings to an object of the specified type.
		/// </summary>
		protected static readonly Dictionary<Type, Delegate> ValueToStringConverters = new Dictionary<Type, Delegate>();

		/// <summary>
		/// Regex matching valid setting names.
		/// </summary>
		private static readonly Regex sValidSettingNameRegex = new Regex("^[a-zA-Z0-9\\[\\]\\.]+$", RegexOptions.Compiled);

		/// <summary>
		/// Initializes the <see cref="ProcessingPipelineStageConfigurationBase"/> class.
		/// </summary>
		static ProcessingPipelineStageConfigurationBase()
		{
			var culture = CultureInfo.InvariantCulture;

			// signed integers
			ValueFromStringConverters[typeof(sbyte)] = new ValueFromStringConverter<sbyte>(s => Convert.ToSByte(s, culture));
			ValueToStringConverters[typeof(sbyte)] = new ValueToStringConverter<sbyte>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(short)] = new ValueFromStringConverter<short>(s => Convert.ToInt16(s, culture));
			ValueToStringConverters[typeof(short)] = new ValueToStringConverter<short>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(int)] = new ValueFromStringConverter<int>(s => Convert.ToInt32(s, culture));
			ValueToStringConverters[typeof(int)] = new ValueToStringConverter<int>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(long)] = new ValueFromStringConverter<long>(s => Convert.ToInt64(s, culture));
			ValueToStringConverters[typeof(long)] = new ValueToStringConverter<long>(v => Convert.ToString(v, culture));

			// unsigned integers
			ValueFromStringConverters[typeof(byte)] = new ValueFromStringConverter<byte>(s => Convert.ToByte(s, culture));
			ValueToStringConverters[typeof(byte)] = new ValueToStringConverter<byte>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(ushort)] = new ValueFromStringConverter<ushort>(s => Convert.ToUInt16(s, culture));
			ValueToStringConverters[typeof(ushort)] = new ValueToStringConverter<ushort>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(uint)] = new ValueFromStringConverter<uint>(s => Convert.ToUInt32(s, culture));
			ValueToStringConverters[typeof(uint)] = new ValueToStringConverter<uint>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(ulong)] = new ValueFromStringConverter<ulong>(s => Convert.ToUInt64(s, culture));
			ValueToStringConverters[typeof(ulong)] = new ValueToStringConverter<ulong>(v => Convert.ToString(v, culture));

			// floating point numbers
			ValueFromStringConverters[typeof(float)] = new ValueFromStringConverter<float>(s => Convert.ToSingle(s, culture));
			ValueToStringConverters[typeof(float)] = new ValueToStringConverter<float>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(double)] = new ValueFromStringConverter<double>(s => Convert.ToDouble(s, culture));
			ValueToStringConverters[typeof(double)] = new ValueToStringConverter<double>(v => Convert.ToString(v, culture));

			// decimal numbers
			ValueFromStringConverters[typeof(decimal)] = new ValueFromStringConverter<decimal>(s => Convert.ToDecimal(s, culture));
			ValueToStringConverters[typeof(decimal)] = new ValueToStringConverter<decimal>(v => Convert.ToString(v, culture));

			// strings
			ValueFromStringConverters[typeof(string)] = new ValueFromStringConverter<string>(s => s);
			ValueToStringConverters[typeof(string)] = new ValueToStringConverter<string>(v => v);

			// enumerations are handled in a generic way, see below...
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessingPipelineStageConfigurationBase"/> class.
		/// </summary>
		/// <param name="sync">
		/// The configuration lock used to synchronize access to the configuration.
		/// Specify <c>null</c> to create a new lock.
		/// </param>
		protected ProcessingPipelineStageConfigurationBase(object sync)
		{
			Sync = sync ?? new object();
		}

		/// <summary>
		/// Gets the name of the processing pipeline stage the configuration belongs to.
		/// </summary>
		public abstract string Name { get; }

		/// <summary>
		/// Gets the object used to synchronize access to the configuration (and its settings).
		/// </summary>
		protected internal object Sync { get; }

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
		public abstract IProcessingPipelineStageSetting<T> GetSetting<T>(string name, T defaultValue);

		#region Implementation of IReadOnlyDictionary<>

		/// <summary>
		/// Gets the names of the settings in the configuration.
		/// </summary>
		public abstract IEnumerable<string> Keys { get; }

		/// <summary>
		/// Gets the settings in the configuration.
		/// </summary>
		public abstract IEnumerable<IUntypedProcessingPipelineStageSetting> Values { get; }

		/// <summary>
		/// Gets the number of settings in the configuration
		/// </summary>
		public abstract int Count { get; }

		/// <summary>
		/// Gets the setting with the specified name.
		/// </summary>
		/// <param name="key">Name of the setting to get.</param>
		/// <returns>The setting with the specified name.</returns>
		public abstract IUntypedProcessingPipelineStageSetting this[string key] { get; }

		/// <summary>
		/// Checks whether the configuration contains a setting with the specified name.
		/// </summary>
		/// <param name="key">Name of the setting to check.</param>
		/// <returns>true, if the setting with the specified name exists in the configuration; otherwise false.</returns>
		public abstract bool ContainsKey(string key);

		/// <summary>
		/// Tries to get the setting with the specified name.
		/// </summary>
		/// <param name="key">Name of the setting to get.</param>
		/// <param name="value">Receives the setting, if it exists.</param>
		/// <returns>true, if the requested setting was successfully returned; otherwise false.</returns>
		public abstract bool TryGetValue(string key, out IUntypedProcessingPipelineStageSetting value);

		/// <summary>
		/// Gets an enumerator for iterating over the settings in the configuration
		/// (the enumerator keeps the configuration locked until it is disposed, so ensure it's Dispose() method is called).
		/// </summary>
		/// <returns>An enumerator for iterating over the settings in the configuration.</returns>
		public abstract IEnumerator<KeyValuePair<string, IUntypedProcessingPipelineStageSetting>> GetEnumerator();

		/// <summary>
		/// Gets an enumerator for iterating over the settings in the configuration
		/// (the enumerator keeps the configuration locked until it is disposed, so ensure it's Dispose() method is called).
		/// </summary>
		/// <returns>An enumerator for iterating over the settings in the configuration.</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion

		#region Argument Validation

		/// <summary>
		/// Checks the name of a setting and throws an exception, if it is not valid.
		/// </summary>
		/// <param name="name">Name to check.</param>
		/// <exception cref="ArgumentNullException"><paramref name="name"/> is a null reference.</exception>
		/// <exception cref="FormatException"><paramref name="name"/> is not a valid setting name.</exception>
		protected static void CheckSettingName(string name)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));
			if (!sValidSettingNameRegex.IsMatch(name)) throw new ArgumentException($"The specified setting name ({name}) is malformed.", nameof(name));
		}

		/// <summary>
		/// Checks whether the specified type is supported as a setting value.
		/// </summary>
		/// <param name="type">Type to check.</param>
		protected static void CheckSettingTypeIsSupported(Type type)
		{
			if (type.IsEnum) return;
			if (!ValueFromStringConverters.ContainsKey(type)) throw new NotSupportedException($"The specified setting type ({type.FullName}) is not supported.");
		}

		#endregion

		#region Conversion of Setting Values

		/// <summary>
		/// Converts the specified enumeration value to a string.
		/// </summary>
		/// <typeparam name="T">Enumeration type to convert.</typeparam>
		/// <param name="value">Enumeration value to convert.</param>
		/// <returns>The string representation of the specified enumeration value.</returns>
		protected static string ConvertEnumToString<T>(T value)
		{
			return value.ToString();
		}

		/// <summary>
		/// Converts the specified string to a value of the specified enumeration type.
		/// </summary>
		/// <typeparam name="T">Enumeration type to convert the string to.</typeparam>
		/// <param name="s">String to convert.</param>
		/// <returns>The converted enumeration value.</returns>
		protected static T ConvertStringToEnum<T>(string s)
		{
			return (T)Enum.Parse(typeof(T), s);
		}

		#endregion

	}
}
