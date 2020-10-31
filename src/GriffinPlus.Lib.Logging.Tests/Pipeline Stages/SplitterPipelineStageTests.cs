///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Unit tests targeting the <see cref="SplitterPipelineStage"/> class.
	/// The splitter pipeline stage is basically the functionality of the base class, so here is not that much to test.
	/// </summary>
	public class SplitterPipelineStageTests : ProcessingPipelineStageBaseTests<SplitterPipelineStage>
	{
		/// <summary>
		/// Creates a new instance of the pipeline stage.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <returns>The created stage.</returns>
		protected override SplitterPipelineStage CreateStage(string name)
		{
			return new SplitterPipelineStage(name);
		}

		/// <summary>
		/// Tests whether creating a new instance of the pipeline stage succeeds and the stage is in the expected state
		/// (only non-default stuff is checked, the rest is done by the base test class).
		/// </summary>
		[Fact]
		private void Create()
		{
			var stage = new SplitterPipelineStage("Splitter"); // do not use CreateStage() to emphasize that the constructor is tested
			Assert.Empty(stage.Settings);
		}

	}
}
