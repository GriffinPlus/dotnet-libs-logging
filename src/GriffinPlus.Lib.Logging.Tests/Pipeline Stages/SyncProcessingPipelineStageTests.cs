///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Unit tests targeting the <see cref="SyncProcessingPipelineStage"/> class (not derived pipeline stages).
/// </summary>
public class SyncProcessingPipelineStageTests : ProcessingPipelineStageBaseTests<ProcessingPipelineTestStage>
{
	/// <summary>
	/// Tests whether creating a new instance of the pipeline stage succeeds and the stage is in the expected state
	/// (only non-default stuff is checked, the rest is done by the base test class).
	/// </summary>
	[Fact]
	public void Create()
	{
		var stage = ProcessingPipelineStage.Create<ProcessingPipelineTestStage>("Stage", null);

		// check properties of the base pipeline stage
		Assert.Empty(stage.Settings);

		// check properties of test pipeline stage
		Assert.False(stage.OnInitializeWasCalled);
		Assert.False(stage.OnShutdownWasCalled);
	}

	/// <summary>
	/// Tests whether initializing the pipeline stage using <see cref="ProcessingPipelineStage.Initialize"/> succeeds,
	/// if the stage does not have following stages. The stage should have called <see cref="SyncProcessingPipelineStage.OnInitialize"/>
	/// after this.
	/// </summary>
	[Fact]
	public void Initialize_Specific_Standalone()
	{
		var stage = ProcessingPipelineStage.Create<ProcessingPipelineTestStage>("Stage", null);
		Assert.False(stage.IsInitialized);
		Assert.False(stage.OnInitializeWasCalled);
		stage.Initialize();
		Assert.True(stage.OnInitializeWasCalled);
		Assert.True(stage.IsInitialized);
	}

	/// <summary>
	/// Tests whether initializing the pipeline stage using <see cref="ProcessingPipelineStage.Initialize"/> succeeds,
	/// if the stage has a following stage. Both stages should have called <see cref="SyncProcessingPipelineStage.OnInitialize"/>
	/// after this.
	/// </summary>
	[Fact]
	public void Initialize_Specific_WithFollowingStage()
	{
		var stage1 = ProcessingPipelineStage.Create<ProcessingPipelineTestStage>("Stage1", null);
		var stage2 = stage1.AddNextStage<ProcessingPipelineTestStage>("Stage2");

		Assert.False(stage1.IsInitialized);
		Assert.False(stage2.IsInitialized);
		Assert.False(stage1.OnInitializeWasCalled);
		Assert.False(stage2.OnInitializeWasCalled);
		stage1.Initialize();
		Assert.True(stage1.OnInitializeWasCalled);
		Assert.True(stage2.OnInitializeWasCalled);
		Assert.True(stage1.IsInitialized);
		Assert.True(stage2.IsInitialized);
	}

	/// <summary>
	/// Tests whether shutting the pipeline stage down using <see cref="ProcessingPipelineStage.Shutdown"/> succeeds,
	/// if the stage does not have following stages. The stage should have called <see cref="SyncProcessingPipelineStage.OnShutdown"/>
	/// after this.
	/// </summary>
	[Fact]
	public void Shutdown_Specific_Standalone()
	{
		var stage = ProcessingPipelineStage.Create<ProcessingPipelineTestStage>("Stage", null);

		// initialize the stage
		Assert.False(stage.IsInitialized);
		Assert.False(stage.OnInitializeWasCalled);
		stage.Initialize();
		Assert.True(stage.OnInitializeWasCalled);
		Assert.True(stage.IsInitialized);

		// shut the stage down
		Assert.False(stage.OnShutdownWasCalled);
		stage.Shutdown();
		Assert.True(stage.OnShutdownWasCalled);
		Assert.False(stage.IsInitialized);
	}

	/// <summary>
	/// Tests whether shutting the pipeline stage using <see cref="ProcessingPipelineStage.Shutdown"/> succeeds,
	/// if the stage has a following stage. Both stages should have called <see cref="SyncProcessingPipelineStage.OnShutdown"/>
	/// after this.
	/// </summary>
	[Fact]
	public void Shutdown_Specific_WithFollowingStage()
	{
		var stage1 = ProcessingPipelineStage.Create<ProcessingPipelineTestStage>("Stage1", null);
		var stage2 = stage1.AddNextStage<ProcessingPipelineTestStage>("Stage2");

		// initialize the stages
		Assert.False(stage1.IsInitialized);
		Assert.False(stage2.IsInitialized);
		stage1.Initialize();
		Assert.True(stage1.IsInitialized);
		Assert.True(stage2.IsInitialized);

		// shut the stages down
		Assert.False(stage1.OnShutdownWasCalled);
		Assert.False(stage2.OnShutdownWasCalled);
		stage1.Shutdown();
		Assert.True(stage1.OnShutdownWasCalled);
		Assert.True(stage2.OnShutdownWasCalled);
		Assert.False(stage1.IsInitialized);
		Assert.False(stage2.IsInitialized);
	}

	/// <summary>
	/// Tests whether processing a log message using <see cref="ProcessingPipelineStage.ProcessMessage"/> succeeds,
	/// if the stage does not have following stages. The stage should have called <see cref="SyncProcessingPipelineStage.ProcessSync"/>
	/// after this.
	/// </summary>
	[Fact]
	public void Process_Standalone()
	{
		var stage = ProcessingPipelineStage.Create<ProcessingPipelineTestStage>("Stage", null);

		// initialize the stage
		Assert.False(stage.IsInitialized);
		Assert.False(stage.OnInitializeWasCalled);
		stage.Initialize();
		Assert.True(stage.OnInitializeWasCalled);
		Assert.True(stage.IsInitialized);

		// process a log message
		LocalLogMessage message = MessagePool.GetUninitializedMessage();
		Assert.False(stage.ProcessSyncWasCalled);
		stage.ProcessMessage(message);
		Assert.True(stage.ProcessSyncWasCalled);
		Assert.Same(message, stage.MessagePassedToProcessSync);

		// shut the stage down
		stage.Shutdown();
		Assert.False(stage.IsInitialized);
	}

	/// <summary>
	/// Tests whether calling <see cref="ProcessingPipelineStage.ProcessMessage"/> invokes
	/// <see cref="SyncProcessingPipelineStage.ProcessSync(LocalLogMessage)"/>, if the stage has a following stage.
	/// Both stages should have called <see cref="SyncProcessingPipelineStage.ProcessSync"/> after this.
	/// </summary>
	[Fact]
	public void Process_WithFollowingStage()
	{
		var stage1 = ProcessingPipelineStage.Create<ProcessingPipelineTestStage>("Stage1", null);
		var stage2 = stage1.AddNextStage<ProcessingPipelineTestStage>("Stage2");

		// initialize the stages
		Assert.False(stage1.IsInitialized);
		Assert.False(stage2.IsInitialized);
		stage1.Initialize();
		Assert.True(stage1.IsInitialized);
		Assert.True(stage2.IsInitialized);

		// process a log message
		LocalLogMessage message = MessagePool.GetUninitializedMessage();
		Assert.False(stage1.ProcessSyncWasCalled);
		Assert.False(stage2.ProcessSyncWasCalled);
		stage1.ProcessMessage(message);
		Assert.True(stage1.ProcessSyncWasCalled);
		Assert.True(stage2.ProcessSyncWasCalled);
		Assert.Same(message, stage1.MessagePassedToProcessSync);
		Assert.Same(message, stage2.MessagePassedToProcessSync);

		// shut the stages down
		stage1.Shutdown();
		Assert.False(stage1.IsInitialized);
		Assert.False(stage2.IsInitialized);
	}
}
