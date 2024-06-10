///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Unit tests targeting the <see cref="VolatileProcessingPipelineStageConfiguration"/> class.
/// </summary>
public class VolatileProcessingPipelineStageConfigurationTests : ProcessingPipelineStageConfigurationTests_Base<VolatileProcessingPipelineStageConfiguration>
{
	/// <summary>
	/// Creates a new instance of the pipeline stage configuration to test.
	/// </summary>
	/// <param name="name">Name of the pipeline stage the configuration belongs to.</param>
	/// <param name="stageConfiguration">Receives the stage configuration to test.</param>
	/// <returns>The created configuration containing the stage configuration (must be disposed at the end of the test).</returns>
	protected override ILogConfiguration CreateConfiguration(string name, out VolatileProcessingPipelineStageConfiguration stageConfiguration)
	{
		stageConfiguration = new VolatileProcessingPipelineStageConfiguration(name, new VolatileLogConfiguration());
		return null; // the stage configuration can exist without an incorporating log configuration
	}

	/// <summary>
	/// Tests invoking the constructor.
	/// </summary>
	[Fact]
	public void Create()
	{
		var configuration = new VolatileLogConfiguration();
		var settings = new VolatileProcessingPipelineStageConfiguration("Stage", configuration);
		Assert.Same(configuration.Sync, settings.Sync);
	}
}
