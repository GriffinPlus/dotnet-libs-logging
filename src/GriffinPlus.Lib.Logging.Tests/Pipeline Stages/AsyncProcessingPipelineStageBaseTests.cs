///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Unit tests targeting the <see cref="AsyncProcessingPipelineStage"/> class as a base class for derived pipeline stages.
/// </summary>
public abstract class AsyncProcessingPipelineStageBaseTests<TStage> where TStage : AsyncProcessingPipelineStage, new()
{
	// ReSharper disable once StaticMemberInGenericType
	internal static LocalLogMessagePool MessagePool = new();

	/// <summary>
	/// Tests whether creating a new stage succeeds and the stage is in the expected state
	/// (as far as the state of the base class is concerned).
	/// </summary>
	[Fact]
	public virtual void Create_And_Check_BaseClass_State()
	{
		var stage = ProcessingPipelineStage.Create<TStage>("Stage", null);

		// a new stage should not be initialized (attached to the logging subsystem)
		Assert.False(stage.IsInitialized);

		// a new stage should have no following stages
		Assert.Empty(stage.NextStages);

		// the stage should be the one and only stage (no following stages)
		var stages = new HashSet<ProcessingPipelineStage>();
		stage.GetAllStages(stages);
		Assert.Single(stages);
		Assert.Same(stage, stages.First());
	}

	/// <summary>
	/// Tests adding and getting following stages using
	/// - <see cref="AsyncProcessingPipelineStage.AddNextStage(ProcessingPipelineStage)"/>
	/// - <see cref="AsyncProcessingPipelineStage.NextStages"/>
	/// - <see cref="AsyncProcessingPipelineStage.GetAllStages(System.Collections.Generic.HashSet{GriffinPlus.Lib.Logging.ProcessingPipelineStage})"/>
	/// </summary>
	[Fact]
	public void Adding_And_Getting_Next_Stages()
	{
		// create stage 1 and add stage 2 following stage 1
		var stage1 = ProcessingPipelineStage.Create<TStage>("Stage1", null);
		var stage2 = stage1.AddNextStage<TStage>("Stage2");
		var stages12 = new HashSet<ProcessingPipelineStage> { stage1, stage2 };

		// stage 1 should have stage 2 as following stage
		Assert.Single(stage1.NextStages);
		Assert.Same(stage2, stage1.NextStages.First());

		// stage 2 should have no following stages
		Assert.Empty(stage2.NextStages);

		// stage 1 and 2 should be returned by GetAllStages() of stage 1
		var stages1 = new HashSet<ProcessingPipelineStage>();
		stage1.GetAllStages(stages1);
		Assert.Equal(2, stages1.Count);
		Assert.Equal(stages12, stages1);

		// stage 2 should have no following stages
		Assert.Empty(stage2.NextStages);
		var stages2 = new HashSet<ProcessingPipelineStage>();
		stage2.GetAllStages(stages2);
		Assert.Single(stages2);
		Assert.Same(stage2, stages2.First());
	}

	/// <summary>
	/// Tests whether initializing the pipeline stage using <see cref="ProcessingPipelineStage.Initialize"/> succeeds,
	/// if the stage does not have any following stages. The stage should be initialized after this.
	/// </summary>
	[Fact]
	public void Initialize_Standalone()
	{
		var stage = ProcessingPipelineStage.Create<TStage>("Stage", null);
		Assert.False(stage.IsInitialized);
		stage.Initialize();
		Assert.True(stage.IsInitialized);
	}

	/// <summary>
	/// Tests whether initializing the pipeline stage using <see cref="ProcessingPipelineStage.Initialize"/> succeeds,
	/// if the stage has a following stage. Both stages should be initialized after this.
	/// </summary>
	[Fact]
	public void Initialize_WithFollowingStage()
	{
		var stage1 = ProcessingPipelineStage.Create<TStage>("Stage1", null);
		var stage2 = stage1.AddNextStage<AsyncProcessingPipelineTestStage>("Stage2", null);
		Assert.False(stage1.IsInitialized);
		Assert.False(stage2.IsInitialized);
		stage1.Initialize();
		Assert.True(stage1.IsInitialized);
		Assert.True(stage2.IsInitialized);
	}

	/// <summary>
	/// Test whether initializing the pipeline stage using <see cref="ProcessingPipelineStage.Initialize"/> throws an exception,
	/// if the stage is already initialized (attached to the logging subsystem).
	/// </summary>
	[Fact]
	public void Initialize_FailsIfAlreadyInitialized()
	{
		var stage = ProcessingPipelineStage.Create<TStage>("Stage", null);

		// initialize the first time
		Assert.False(stage.IsInitialized);
		stage.Initialize();
		Assert.True(stage.IsInitialized);

		// initialize once again
		Assert.Throws<InvalidOperationException>(() => stage.Initialize());
	}

	/// <summary>
	/// Tests whether shutting down the pipeline stage using <see cref="ProcessingPipelineStage.Shutdown"/> succeeds,
	/// if the stage does not have any following stages. The stage should not be initialized after this.
	/// </summary>
	[Fact]
	public void Shutdown_Standalone()
	{
		var stage = ProcessingPipelineStage.Create<TStage>("Stage", null);

		// initialize the stage
		Assert.False(stage.IsInitialized);
		stage.Initialize();
		Assert.True(stage.IsInitialized);

		// shut the stage down
		stage.Shutdown();
		Assert.False(stage.IsInitialized);
	}

	/// <summary>
	/// Tests whether shutting down the pipeline stage using <see cref="ProcessingPipelineStage.Shutdown"/> succeeds,
	/// if the stage has a following stage. Both stages should not be initialized after this.
	/// </summary>
	[Fact]
	public void Shutdown_WithFollowingStage()
	{
		var stage1 = ProcessingPipelineStage.Create<TStage>("Stage1", null);
		var stage2 = stage1.AddNextStage<AsyncProcessingPipelineTestStage>("Stage2");

		// initialize the stages
		Assert.False(stage1.IsInitialized);
		Assert.False(stage2.IsInitialized);
		stage1.Initialize();
		Assert.True(stage1.IsInitialized);
		Assert.True(stage2.IsInitialized);

		// shut the stages down
		stage1.Shutdown();
		Assert.False(stage1.IsInitialized);
		Assert.False(stage2.IsInitialized);
	}

	/// <summary>
	/// Test whether <see cref="ProcessingPipelineStage.ProcessMessage"/> throws an exception,
	/// if the message to process is a null reference.
	/// </summary>
	[Fact]
	public void Process_FailsIfMessageIsNull()
	{
		var stage = ProcessingPipelineStage.Create<TStage>("Stage", null);
		Assert.Throws<ArgumentNullException>(() => stage.ProcessMessage(null));
	}

	/// <summary>
	/// Test whether <see cref="ProcessingPipelineStage.ProcessMessage"/> throws an exception,
	/// if the stage is not initialized (attached to the logging subsystem).
	/// </summary>
	[Fact]
	public void Process_FailsIfNotInitialized()
	{
		var stage = ProcessingPipelineStage.Create<TStage>("Stage", null);
		LocalLogMessage message = MessagePool.GetUninitializedMessage();
		Assert.Throws<InvalidOperationException>(() => stage.ProcessMessage(message));
	}
}
