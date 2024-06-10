///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Unit tests targeting the <see cref="SplitterPipelineStage"/> class.
/// The splitter pipeline stage is basically the functionality of the base class, so here is not that much to test.
/// </summary>
public class SplitterPipelineStageTests : ProcessingPipelineStageBaseTests<SplitterPipelineStage>
{
	/// <summary>
	/// Tests whether creating a new instance of the pipeline stage succeeds and the stage is in the expected state
	/// (only non-default stuff is checked, the rest is done by the base test class).
	/// </summary>
	[Fact]
	private void Create()
	{
		// do not use CreateStage() to emphasize that the constructor is tested
		var stage = ProcessingPipelineStage.Create<SplitterPipelineStage>("Splitter", null);
		Assert.Empty(stage.Settings);
		Assert.Empty(stage.NextStages);
	}
}
