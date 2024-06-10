///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// The configuration of the volatile processing pipeline (thread-safe).
/// </summary>
public class VolatileProcessingPipelineConfiguration : IProcessingPipelineConfiguration
{
	/// <summary>
	/// Initializes a new instance of the <see cref="VolatileProcessingPipelineConfiguration"/> class.
	/// </summary>
	/// <param name="configuration">The log configuration the processing pipeline configuration belongs to.</param>
	internal VolatileProcessingPipelineConfiguration(VolatileLogConfiguration configuration)
	{
		Stages = new VolatileProcessingPipelineStageConfigurations(configuration);
	}

	/// <summary>
	/// Gets the configurations for pipeline stages.
	/// </summary>
	public VolatileProcessingPipelineStageConfigurations Stages { get; }

	/// <summary>
	/// Gets the configurations for pipeline stages.
	/// </summary>
	IProcessingPipelineStageConfigurations IProcessingPipelineConfiguration.Stages => Stages;
}
