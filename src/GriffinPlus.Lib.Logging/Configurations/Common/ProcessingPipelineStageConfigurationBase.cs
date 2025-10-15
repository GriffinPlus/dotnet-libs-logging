///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using GriffinPlus.Lib.Conversion;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Base class for pipeline stage configurations (thread-safe).
/// </summary>
public abstract class ProcessingPipelineStageConfigurationBase : IProcessingPipelineStageConfiguration
{
	/// <summary>
	/// Regex matching valid setting names.
	/// </summary>
	private static readonly Regex sValidSettingNameRegex = new(@"^[a-zA-Z0-9\[\]\.]+$", RegexOptions.Compiled);

	/// <summary>
	/// Initializes a new instance of the <see cref="ProcessingPipelineStageConfigurationBase"/> class.
	/// </summary>
	/// <param name="sync">
	/// The configuration lock used to synchronize access to the configuration (<see langword="null"/> to create a new lock).
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
	/// Gets the object used to synchronize access to the pipeline stage (and its settings).
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
	/// The argument <paramref name="name"/> is <see langword="null"/>.
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

		// get global converter (handles enum types as well)
		IConverter converter = Converters.GetGlobalConverter(typeof(T));

		// register the setting
		lock (Sync)
		{
			return RegisterSetting(
				name,
				defaultValue,
				(ObjectToStringConversionDelegate<T>)converter.ObjectToStringConversion,
				(StringToObjectConversionDelegate<T>)converter.StringToObjectConversion);
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
	/// The argument <paramref name="name"/>, <paramref name="valueToStringConverter"/> and/or <paramref name="stringToValueConverter"/> is
	/// <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// The setting exists already, but the specified type differs from the value type of the existing setting.
	/// </exception>
	/// <exception cref="FormatException">
	/// The <paramref name="name"/> is not a valid setting name.
	/// </exception>
	public abstract IProcessingPipelineStageSetting<T> RegisterSetting<T>(
		string                              name,
		T                                   defaultValue,
		ObjectToStringConversionDelegate<T> valueToStringConverter,
		StringToObjectConversionDelegate<T> stringToValueConverter);

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
	/// <returns>The setting (<see langword="null"/> if the setting does not exist).</returns>
	/// <exception cref="ArgumentNullException">
	/// The argument <paramref name="name"/> is <see langword="null"/>.
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
		// ensure that the specified name is well-formed and the setting value type is supported
		CheckSettingName(name);
		CheckSettingTypeIsSupported(typeof(T));

		// get global converter (handles enum types as well)
		IConverter converter = Converters.GetGlobalConverter(typeof(T));

		// get the setting
		lock (Sync)
		{
			return GetSetting(
				name,
				(ObjectToStringConversionDelegate<T>)converter.ObjectToStringConversion,
				(StringToObjectConversionDelegate<T>)converter.StringToObjectConversion);
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
	/// <returns>The setting (<see langword="null"/> if the setting does not exist).</returns>
	/// <exception cref="ArgumentNullException">
	/// The argument <paramref name="name"/>, <paramref name="valueToStringConverter"/> and/or <paramref name="stringToValueConverter"/> is
	/// <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// The setting exists, but the specified type differs from the value type of the existing setting.
	/// </exception>
	/// <exception cref="FormatException">
	/// The <paramref name="name"/> is not a valid setting name.
	/// </exception>
	public abstract IProcessingPipelineStageSetting<T> GetSetting<T>(
		string                              name,
		ObjectToStringConversionDelegate<T> valueToStringConverter,
		StringToObjectConversionDelegate<T> stringToValueConverter);

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
	/// The argument <paramref name="name"/> is <see langword="null"/>.
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

		// get global converter (handles enum types as well)
		IConverter converter = Converters.GetGlobalConverter(typeof(T));

		lock (Sync)
		{
			return SetSetting(
				name,
				value,
				(ObjectToStringConversionDelegate<T>)converter.ObjectToStringConversion,
				(StringToObjectConversionDelegate<T>)converter.StringToObjectConversion);
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
	/// The argument <paramref name="name"/>, <paramref name="valueToStringConverter"/> and/or <paramref name="stringToValueConverter"/> is
	/// <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// The setting exists already, but the specified type differs from the value type of the existing setting.
	/// </exception>
	/// <exception cref="FormatException">
	/// The <paramref name="name"/> is not a valid setting name.
	/// </exception>
	public abstract IProcessingPipelineStageSetting<T> SetSetting<T>(
		string                              name,
		T                                   value,
		ObjectToStringConversionDelegate<T> valueToStringConverter,
		StringToObjectConversionDelegate<T> stringToValueConverter);

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
	/// <returns>
	/// <see langword="true"/> if the setting with the specified name exists in the configuration;<br/>
	/// otherwise, <see langword="false"/>.
	/// </returns>
	public abstract bool ContainsKey(string key);

	/// <summary>
	/// Tries to get the setting with the specified name.
	/// </summary>
	/// <param name="key">Name of the setting to get.</param>
	/// <param name="value">Receives the setting, if it exists.</param>
	/// <returns>
	/// <see langword="true"/> if the requested setting was successfully returned;<br/>
	/// otherwise, <see langword="false"/>.
	/// </returns>
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
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
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
		if (Converters.GlobalConverters.All(x => x.Type != type))
			throw new NotSupportedException($"The specified setting type ({type.FullName}) is not supported.");
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
