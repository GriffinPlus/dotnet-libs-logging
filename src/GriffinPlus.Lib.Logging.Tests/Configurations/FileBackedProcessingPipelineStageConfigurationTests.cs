///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="FileBackedProcessingPipelineStageConfiguration"/> class.
	/// </summary>
	public class FileBackedProcessingPipelineStageConfigurationTests : ProcessingPipelineStageConfigurationTests_Base<FileBackedProcessingPipelineStageConfiguration>
	{
		/// <summary>
		/// Creates a new instance of the pipeline stage configuration to test.
		/// </summary>
		/// <param name="name">Name of the pipeline stage the configuration belongs to.</param>
		/// <param name="stageConfiguration">Receives the stage configuration to test.</param>
		/// <returns>The created configuration containing the stage configuration (must be disposed at the end of the test).</returns>
		protected override ILogConfiguration CreateConfiguration(string name, out FileBackedProcessingPipelineStageConfiguration stageConfiguration)
		{
			// the file-backed pipeline stage configuration can exist only within the file-backed log configuration
			// (use specific file name to avoid sharing violation when running other tests that use the default constructor of the configuration as well)
			var logConfiguration = new FileBackedLogConfiguration();
			logConfiguration.Path = "FileBackedProcessingPipelineStageConfigurationTests.gplogconf";
			stageConfiguration = new FileBackedProcessingPipelineStageConfiguration(logConfiguration, name);
			return logConfiguration;
		}

		/// <summary>
		/// Tests whether invoking the constructor without a synchronization object succeeds.
		/// </summary>
		[Fact]
		public void Create()
		{
			// use specific file name to avoid sharing violation when running other tests that use the default constructor of the configuration as well
			using (var logConfiguration = new FileBackedLogConfiguration())
			{
				logConfiguration.Path = "FileBackedProcessingPipelineStageConfigurationTests.gplogconf";
				var stageConfiguration = new FileBackedProcessingPipelineStageConfiguration(logConfiguration, "Stage");
				Assert.NotNull(stageConfiguration.Sync);
			}
		}
	}

}
