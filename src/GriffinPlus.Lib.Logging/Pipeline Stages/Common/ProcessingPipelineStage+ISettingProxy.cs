///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging;

partial class ProcessingPipelineStage
{
	/// <summary>
	/// Typed interface for a setting proxy (must be implemented thread-safe).
	/// </summary>
	/// <typeparam name="T">Type of the setting value (can be a primitive type or string).</typeparam>
	internal interface ISettingProxy<T> : IProcessingPipelineStageSetting<T>, IUntypedSettingProxy;
}
