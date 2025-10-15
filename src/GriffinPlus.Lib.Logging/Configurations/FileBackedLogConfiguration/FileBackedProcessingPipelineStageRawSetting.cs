///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// A raw setting in a <see cref="FileBackedProcessingPipelineStageConfiguration"/>.
/// </summary>
public class FileBackedProcessingPipelineStageRawSetting
{
	private bool   mHasDefaultValue;
	private string mDefaultValue;

	/// <summary>
	/// Initializes a new instance of the <see cref="FileBackedProcessingPipelineStageRawSetting"/> class
	/// without a default value.
	/// </summary>
	/// <param name="configuration">The configuration the setting belongs to.</param>
	/// <param name="name">Name of the setting.</param>
	internal FileBackedProcessingPipelineStageRawSetting(
		FileBackedProcessingPipelineStageConfiguration configuration,
		string                                         name)
	{
		StageConfiguration = configuration;
		Name = name;
		mHasDefaultValue = false;
		mDefaultValue = null;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FileBackedProcessingPipelineStageRawSetting"/> class
	/// with a default value.
	/// </summary>
	/// <param name="configuration">The configuration the setting belongs to.</param>
	/// <param name="name">Name of the setting.</param>
	/// <param name="defaultValue">The default value of the setting.</param>
	internal FileBackedProcessingPipelineStageRawSetting(
		FileBackedProcessingPipelineStageConfiguration configuration,
		string                                         name,
		string                                         defaultValue)
	{
		StageConfiguration = configuration;
		Name = name;
		mHasDefaultValue = true;
		mDefaultValue = defaultValue;
	}

	/// <summary>
	/// Gets the configuration the setting belongs to.
	/// </summary>
	public FileBackedProcessingPipelineStageConfiguration StageConfiguration { get; }

	/// <summary>
	/// Gets the name of the setting.
	/// </summary>
	public string Name { get; } // immutable => no sync necessary

	/// <summary>
	/// Gets a value indicating whether the setting has valid value (<see langword="true"/>) or just its default value (<see langword="false"/>).
	/// </summary>
	public bool HasValue
	{
		get
		{
			lock (StageConfiguration.Sync)
			{
				if (StageConfiguration.LogConfiguration.File.ProcessingPipelineStageSettings.TryGetValue(StageConfiguration.Name, out IDictionary<string, string> settings))
				{
					if (settings.TryGetValue(Name, out string _))
					{
						return true;
					}
				}

				return false;
			}
		}
	}

	/// <summary>
	/// Gets or sets the value of the setting.
	/// </summary>
	public string Value
	{
		get
		{
			lock (StageConfiguration.Sync)
			{
				if (StageConfiguration.LogConfiguration.File.ProcessingPipelineStageSettings.TryGetValue(StageConfiguration.Name, out IDictionary<string, string> settings))
				{
					if (settings.TryGetValue(Name, out string value))
					{
						return value;
					}
				}

				return DefaultValue;
			}
		}

		set
		{
			lock (StageConfiguration.Sync)
			{
				if (!StageConfiguration.LogConfiguration.File.ProcessingPipelineStageSettings.TryGetValue(StageConfiguration.Name, out IDictionary<string, string> settings))
				{
					settings = new Dictionary<string, string>();
					StageConfiguration.LogConfiguration.File.ProcessingPipelineStageSettings.Add(StageConfiguration.Name, settings);
				}

				settings[Name] = value;
			}
		}
	}

	/// <summary>
	/// Gets a value indicating whether the setting has valid default value.
	/// </summary>
	internal bool HasDefaultValue
	{
		get
		{
			lock (StageConfiguration.Sync) return mHasDefaultValue;
		}
	}

	/// <summary>
	/// Gets the default value of the setting.
	/// </summary>
	/// <exception cref="InvalidOperationException">The item does not have a default value.</exception>
	public string DefaultValue
	{
		get
		{
			lock (StageConfiguration.Sync)
			{
				if (!mHasDefaultValue) throw new InvalidOperationException("The item does not have a default value.");
				return mDefaultValue;
			}
		}

		internal set
		{
			lock (StageConfiguration.Sync)
			{
				mDefaultValue = value;
				mHasDefaultValue = true;
			}
		}
	}

	/// <summary>
	/// Gets the string representation of the setting.
	/// </summary>
	/// <returns>String representation of the setting.</returns>
	public override string ToString()
	{
		lock (StageConfiguration.Sync)
		{
			return HasValue
				       ? $"Name: '{Name}', Value: '{Value}'"
				       : HasDefaultValue
					       ? $"Name: '{Name}', Value: <no value> (defaults to: '{DefaultValue}'"
					       : $"Name: '{Name}', Value: <no value> (defaults to: <no value>)";
		}
	}
}
