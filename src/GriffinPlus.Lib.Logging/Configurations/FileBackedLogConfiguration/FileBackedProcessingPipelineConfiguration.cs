///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// The processing pipeline part of the <see cref="FileBackedLogConfiguration"/> (thread-safe).
/// </summary>
public class FileBackedProcessingPipelineConfiguration : IProcessingPipelineConfiguration
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FileBackedProcessingPipelineConfiguration"/> class.
	/// </summary>
	/// <param name="configuration">The log configuration the processing pipeline configuration belongs to.</param>
	internal FileBackedProcessingPipelineConfiguration(FileBackedLogConfiguration configuration)
	{
		Stages = new FileBackedProcessingPipelineStageConfigurations(configuration);
	}

	/// <summary>
	/// Gets the configuration of the pipeline stages.
	/// </summary>
	public FileBackedProcessingPipelineStageConfigurations Stages { get; }

	/// <summary>
	/// Gets the configuration of the pipeline stages.
	/// </summary>
	IProcessingPipelineStageConfigurations IProcessingPipelineConfiguration.Stages => Stages;
}
