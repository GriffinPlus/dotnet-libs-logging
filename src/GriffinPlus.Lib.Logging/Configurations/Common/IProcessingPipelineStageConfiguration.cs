///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

using GriffinPlus.Lib.Conversion;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Interface for pipeline stage configurations.
/// Must be implemented thread-safe.
/// </summary>
public interface IProcessingPipelineStageConfiguration : IReadOnlyDictionary<string, IUntypedProcessingPipelineStageSetting>
{
	/// <summary>
	/// Gets the name of the processing pipeline stage the configuration belongs to.
	/// </summary>
	string Name { get; }

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
	IProcessingPipelineStageSetting<T> RegisterSetting<T>(string name, T defaultValue);

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
	IProcessingPipelineStageSetting<T> RegisterSetting<T>(
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
	/// The setting exists already, but the specified type differs from the value type of the existing setting.
	/// </exception>
	/// <exception cref="FormatException">
	/// The <paramref name="name"/> is not a valid setting name.
	/// </exception>
	/// <exception cref="NotSupportedException">
	/// The specified type <typeparamref name="T"/> is not supported.
	/// </exception>
	IProcessingPipelineStageSetting<T> GetSetting<T>(string name);

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
	/// The setting exists already, but the specified type differs from the value type of the existing setting.
	/// </exception>
	/// <exception cref="FormatException">
	/// The <paramref name="name"/> is not a valid setting name.
	/// </exception>
	IProcessingPipelineStageSetting<T> GetSetting<T>(
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
	IProcessingPipelineStageSetting<T> SetSetting<T>(string name, T value);

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
	IProcessingPipelineStageSetting<T> SetSetting<T>(
		string                              name,
		T                                   value,
		ObjectToStringConversionDelegate<T> valueToStringConverter,
		StringToObjectConversionDelegate<T> stringToValueConverter);
}
