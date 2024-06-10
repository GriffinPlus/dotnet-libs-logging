///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Interface for a setting in a <see cref="IProcessingPipelineStageConfiguration"/>.
/// Must be implemented thread-safe.
/// </summary>
/// <typeparam name="T">Type of the setting value (can be a primitive type or string).</typeparam>
public interface IProcessingPipelineStageSetting<T> : IUntypedProcessingPipelineStageSetting
{
	/// <summary>
	/// Gets or sets the value of the setting.
	/// </summary>
	new T Value { get; set; }

	/// <summary>
	/// Gets the default value of the setting.
	/// </summary>
	new T DefaultValue { get; }
}
