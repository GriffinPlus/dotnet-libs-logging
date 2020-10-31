///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Interface for the pipeline configuration (must be implemented thread-safe).
	/// </summary>
	public interface IProcessingPipelineConfiguration
	{
		/// <summary>
		/// Gets the configurations for pipeline stages.
		/// </summary>
		IProcessingPipelineStageConfigurations Stages { get; }
	}

}
