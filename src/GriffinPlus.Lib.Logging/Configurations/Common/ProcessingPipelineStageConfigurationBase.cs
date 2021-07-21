///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
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
			ValueFromStringConverters[typeof(sbyte)] = new Func<string, sbyte>(s => Convert.ToSByte(s, culture));
			ValueToStringConverters[typeof(sbyte)] = new Func<sbyte, string>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(short)] = new Func<string, short>(s => Convert.ToInt16(s, culture));
			ValueToStringConverters[typeof(short)] = new Func<short, string>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(int)] = new Func<string, int>(s => Convert.ToInt32(s, culture));
			ValueToStringConverters[typeof(int)] = new Func<int, string>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(long)] = new Func<string, long>(s => Convert.ToInt64(s, culture));
			ValueToStringConverters[typeof(long)] = new Func<long, string>(v => Convert.ToString(v, culture));

			// unsigned integers
			ValueFromStringConverters[typeof(byte)] = new Func<string, byte>(s => Convert.ToByte(s, culture));
			ValueToStringConverters[typeof(byte)] = new Func<byte, string>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(ushort)] = new Func<string, ushort>(s => Convert.ToUInt16(s, culture));
			ValueToStringConverters[typeof(ushort)] = new Func<ushort, string>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(uint)] = new Func<string, uint>(s => Convert.ToUInt32(s, culture));
			ValueToStringConverters[typeof(uint)] = new Func<uint, string>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(ulong)] = new Func<string, ulong>(s => Convert.ToUInt64(s, culture));
			ValueToStringConverters[typeof(ulong)] = new Func<ulong, string>(v => Convert.ToString(v, culture));

			// floating point numbers
			ValueFromStringConverters[typeof(float)] = new Func<string, float>(s => Convert.ToSingle(s, culture));
			ValueToStringConverters[typeof(float)] = new Func<float, string>(v => Convert.ToString(v, culture));
			ValueFromStringConverters[typeof(double)] = new Func<string, double>(s => Convert.ToDouble(s, culture));
			ValueToStringConverters[typeof(double)] = new Func<double, string>(v => Convert.ToString(v, culture));

			// decimal numbers
			ValueFromStringConverters[typeof(decimal)] = new Func<string, decimal>(s => Convert.ToDecimal(s, culture));
			ValueToStringConverters[typeof(decimal)] = new Func<decimal, string>(v => Convert.ToString(v, culture));

			// strings
			ValueFromStringConverters[typeof(string)] = new Func<string, string>(s => s);
			ValueToStringConverters[typeof(string)] = new Func<string, string>(v => v);

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
		/// Registers the setting with the specified name (supports primitive types, enums and string).
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
		/// <returns>The setting.</returns>
		/// <exception cref="ArgumentNullException">
		/// The argument <paramref name="name"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// The setting exists already, but the specified type differs from the value type of the existing setting.
		/// </exception>
		/// <exception cref="FormatException">
		/// The <paramref name="name"/> is not a valid setting name.
		/// </exception>
		/// <exception cref="NotSupportedException">
		/// The specified type <typeparamref name="T"/> is not supported.
		/// </exception>
		public virtual IProcessingPipelineStageSetting<T> RegisterSetting<T>(string name, T defaultValue)
		{
			// ensure that the specified name is well-formed and the setting value type is supported
			CheckSettingName(name);
			CheckSettingTypeIsSupported(typeof(T));

			Func<string, T> stringToValueConverter;
			Func<T, string> valueToStringConverter;

			if (typeof(T).IsEnum)
			{
				stringToValueConverter = ConvertStringToEnum<T>;
				valueToStringConverter = ConvertEnumToString;
			}
			else
			{
				stringToValueConverter = (Func<string, T>)ValueFromStringConverters[typeof(T)];
				valueToStringConverter = (Func<T, string>)ValueToStringConverters[typeof(T)];
			}

			lock (Sync)
			{
				return RegisterSetting(
					name,
					defaultValue,
					valueToStringConverter,
					stringToValueConverter);
			}
		}

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
		public abstract IProcessingPipelineStageSetting<T> RegisterSetting<T>(
			string          name,
			T               defaultValue,
			Func<T, string> valueToStringConverter,
			Func<string, T> stringToValueConverter);

		/// <summary>
		/// Gets the setting with the specified name (supports primitive types, enums and string).
		/// </summary>
		/// <typeparam name="T">Type of the setting.</typeparam>
		/// <param name="name">
		/// Name of the setting. The following characters are allowed:
		/// - alphanumeric characters ( a-z, A-Z, 0-9 )
		/// - square brackets ( [] )
		/// - Period (.)
		/// </param>
		/// <returns>The setting (<c>null</c> if the setting does not exist).</returns>
		/// <exception cref="ArgumentNullException">
		/// The argument <paramref name="name"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// The setting exists, but the specified type differs from the value type of the existing setting.
		/// </exception>
		/// <exception cref="FormatException">
		/// The <paramref name="name"/> is not a valid setting name.
		/// </exception>
		/// <exception cref="NotSupportedException">
		/// The specified type <typeparamref name="T"/> is not supported.
		/// </exception>
		public virtual IProcessingPipelineStageSetting<T> GetSetting<T>(string name)
		{
			Func<string, T> stringToValueConverter;
			Func<T, string> valueToStringConverter;

			if (typeof(T).IsEnum)
			{
				stringToValueConverter = ConvertStringToEnum<T>;
				valueToStringConverter = ConvertEnumToString;
			}
			else
			{
				stringToValueConverter = (Func<string, T>)ValueFromStringConverters[typeof(T)];
				valueToStringConverter = (Func<T, string>)ValueToStringConverters[typeof(T)];
			}

			lock (Sync)
			{
				return GetSetting(
					name,
					valueToStringConverter,
					stringToValueConverter);
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
		public abstract IProcessingPipelineStageSetting<T> GetSetting<T>(
			string          name,
			Func<T, string> valueToStringConverter,
			Func<string, T> stringToValueConverter);

		/// <summary>
		/// Sets the setting with the specified name (supports primitive types, enums and string).
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
		/// <returns>The setting.</returns>
		/// <exception cref="ArgumentNullException">
		/// The argument <paramref name="name"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// The setting exists already, but the specified type differs from the value type of the existing setting.
		/// </exception>
		/// <exception cref="FormatException">
		/// The specified <paramref name="name"/> is not a valid setting name.
		/// </exception>
		/// <exception cref="NotSupportedException">
		/// The specified type <typeparamref name="T"/> is not supported.
		/// </exception>
		public virtual IProcessingPipelineStageSetting<T> SetSetting<T>(string name, T value)
		{
			// ensure that the specified name is well-formed and the setting value type is supported
			CheckSettingName(name);
			CheckSettingTypeIsSupported(typeof(T));

			Func<string, T> stringToValueConverter;
			Func<T, string> valueToStringConverter;

			if (typeof(T).IsEnum)
			{
				stringToValueConverter = ConvertStringToEnum<T>;
				valueToStringConverter = ConvertEnumToString;
			}
			else
			{
				stringToValueConverter = (Func<string, T>)ValueFromStringConverters[typeof(T)];
				valueToStringConverter = (Func<T, string>)ValueToStringConverters[typeof(T)];
			}

			lock (Sync)
			{
				return SetSetting(
					name,
					value,
					valueToStringConverter,
					stringToValueConverter);
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
		public abstract IProcessingPipelineStageSetting<T> SetSetting<T>(
			string          name,
			T               value,
			Func<T, string> valueToStringConverter,
			Func<string, T> stringToValueConverter);

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
