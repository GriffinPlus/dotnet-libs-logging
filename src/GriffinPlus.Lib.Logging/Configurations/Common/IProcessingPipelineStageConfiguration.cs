///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Interface for pipeline stage configurations (must be implemented thread-safe).
	/// </summary>
	public interface IProcessingPipelineStageConfiguration : IReadOnlyDictionary<string, IUntypedProcessingPipelineStageSetting>
	{
		/// <summary>
		/// Gets the name of the processing pipeline stage the configuration belongs to.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Gets the setting with the specified name.
		/// </summary>
		/// <typeparam name="T">Type of the setting (can be a primitive type or string).</typeparam>
		/// <param name="name">Name of the setting.</param>
		/// <returns>The setting (<c>null</c> if the setting does not exist).</returns>
		IProcessingPipelineStageSetting<T> GetSetting<T>(string name);

		/// <summary>
		/// Gets the setting with the specified name
		/// (creates a new setting with the specified value, if the setting does not exist).
		/// </summary>
		/// <typeparam name="T">Type of the setting (can be a primitive type or string).</typeparam>
		/// <param name="name">Name of the setting.</param>
		/// <param name="defaultValue">Value of the setting, if the setting does not exist, yet.</param>
		/// <returns>The setting.</returns>
		IProcessingPipelineStageSetting<T> RegisterSetting<T>(string name, T defaultValue);

		/// <summary>
		/// Sets the setting with the specified name (creates a new setting, if it does not exist, yet).
		/// </summary>
		/// <typeparam name="T">Type of the setting (can be a primitive type or string).</typeparam>
		/// <param name="name">Name of the setting.</param>
		/// <param name="value">New value of the setting.</param>
		/// <returns>The setting.</returns>
		IProcessingPipelineStageSetting<T> SetSetting<T>(string name, T value);
	}

}
