///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging
{

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
			stageConfiguration = new VolatileProcessingPipelineStageConfiguration(name);
			return null; // the stage configuration can exist without an incorporating log configuration
		}

		/// <summary>
		/// Tests whether invoking the constructor without a synchronization object succeeds.
		/// </summary>
		[Fact]
		public void Create_WithoutSyncObject()
		{
			var configuration = new VolatileProcessingPipelineStageConfiguration("Stage");
			Assert.NotNull(configuration.Sync);
		}

		/// <summary>
		/// Tests whether invoking the constructor taking a synchronization object to synchronize access to the configuration succeeds.
		/// </summary>
		[Fact]
		public void Create_WithSyncObject()
		{
			object sync = new object();
			var configuration = new VolatileProcessingPipelineStageConfiguration(null, sync);
			Assert.Same(sync, configuration.Sync);
		}
	}

}
