///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="FileBackedProcessingPipelineStageConfiguration" /> class.
	/// </summary>
	public class FileBackedProcessingPipelineStageConfigurationTests : ProcessingPipelineStageConfigurationTests_Base<FileBackedProcessingPipelineStageConfiguration>
	{
		/// <summary>
		/// Creates a new instance of the pipeline stage configuration to test.
		/// </summary>
		/// <param name="name">Name of the pipeline stage the configuration belongs to.</param>
		/// <returns>The created pipeline stage configuration.</returns>
		protected override FileBackedProcessingPipelineStageConfiguration CreateConfiguration(string name)
		{
			// the file-backed pipeline stage configuration can exist only within the file-backed log configuration
			var logConfiguration = new FileBackedLogConfiguration();
			return new FileBackedProcessingPipelineStageConfiguration(logConfiguration, name);
		}

		/// <summary>
		/// Tests whether invoking the constructor without a synchronization object succeeds.
		/// </summary>
		[Fact]
		public void Create()
		{
			var logConfiguration = new FileBackedLogConfiguration();
			var stageConfiguration = new FileBackedProcessingPipelineStageConfiguration(logConfiguration, "Stage");
			Assert.NotNull(stageConfiguration.Sync);
		}
	}

}
