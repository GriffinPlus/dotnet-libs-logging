﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="ProcessingPipelineStage{STAGE}"/> class (not derived pipeline stages).
	/// </summary>
	public class ProcessingPipelineStageTests : ProcessingPipelineStageBaseTests<ProcessingPipelineTestStage>
	{
		/// <summary>
		/// Creates a new instance of the pipeline stage.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <returns>The created stage.</returns>
		protected override ProcessingPipelineTestStage CreateStage(string name)
		{
			return new ProcessingPipelineTestStage(name);
		}

		/// <summary>
		/// Tests whether creating a new instance of the pipeline stage succeeds and the stage is in the expected state
		/// (only non-default stuff is checked, the rest is done by the base test class).
		/// </summary>
		[Fact]
		public void Create()
		{
			var stage = CreateStage("Stage");

			// check properties of the base pipeline stage
			Assert.Empty(stage.Settings);

			// check properties of test pipeline stage
			Assert.False(stage.OnInitializeWasCalled);
			Assert.False(stage.OnShutdownWasCalled);
		}

		/// <summary>
		/// Tests whether initializing the pipeline stage using <see cref="IProcessingPipelineStage.Initialize"/> succeeds,
		/// if the stage does not have following stages. The stage should have called <see cref="ProcessingPipelineStage{STAGE}.OnInitialize"/>
		/// after this.
		/// </summary>
		[Fact]
		public void Initialize_Specific_Standalone()
		{
			var stage = CreateStage("Stage");
			Assert.False(stage.IsInitialized);
			Assert.False(stage.OnInitializeWasCalled);
			((IProcessingPipelineStage)stage).Initialize();
			Assert.True(stage.OnInitializeWasCalled);
			Assert.True(stage.IsInitialized);
		}

		/// <summary>
		/// Tests whether initializing the pipeline stage using <see cref="IProcessingPipelineStage.Initialize"/> succeeds,
		/// if the stage has a following stage. Both stages should have called <see cref="ProcessingPipelineStage{STAGE}.OnInitialize"/>
		/// after this.
		/// </summary>
		[Fact]
		public void Initialize_Specific_WithFollowingStage()
		{
			var stage1 = CreateStage("Stage1");
			var stage2 = CreateStage("Stage2");
			stage1.AddNextStage(stage2);
			Assert.False(stage1.IsInitialized);
			Assert.False(stage2.IsInitialized);
			Assert.False(stage1.OnInitializeWasCalled);
			Assert.False(stage2.OnInitializeWasCalled);
			((IProcessingPipelineStage)stage1).Initialize();
			Assert.True(stage1.OnInitializeWasCalled);
			Assert.True(stage2.OnInitializeWasCalled);
			Assert.True(stage1.IsInitialized);
			Assert.True(stage2.IsInitialized);
		}

		/// <summary>
		/// Tests whether shutting the pipeline stage down using <see cref="IProcessingPipelineStage.Shutdown"/> succeeds,
		/// if the stage does not have following stages. The stage should have called <see cref="ProcessingPipelineStage{STAGE}.OnShutdown"/>
		/// after this.
		/// </summary>
		[Fact]
		public void Shutdown_Specific_Standalone()
		{
			var stage = CreateStage("Stage");

			// initialize the stage
			Assert.False(stage.IsInitialized);
			Assert.False(stage.OnInitializeWasCalled);
			((IProcessingPipelineStage)stage).Initialize();
			Assert.True(stage.OnInitializeWasCalled);
			Assert.True(stage.IsInitialized);

			// shut the stage down
			Assert.False(stage.OnShutdownWasCalled);
			((IProcessingPipelineStage)stage).Shutdown();
			Assert.True(stage.OnShutdownWasCalled);
			Assert.False(stage.IsInitialized);
		}

		/// <summary>
		/// Tests whether shutting the pipeline stage using <see cref="IProcessingPipelineStage.Shutdown"/> succeeds,
		/// if the stage has a following stage. Both stages should have called <see cref="ProcessingPipelineStage{STAGE}.OnShutdown"/>
		/// after this.
		/// </summary>
		[Fact]
		public void Shutdown_Specific_WithFollowingStage()
		{
			var stage1 = CreateStage("Stage1");
			var stage2 = CreateStage("Stage2");
			stage1.AddNextStage(stage2);

			// initialize the stages
			Assert.False(stage1.IsInitialized);
			Assert.False(stage2.IsInitialized);
			((IProcessingPipelineStage)stage1).Initialize();
			Assert.True(stage1.IsInitialized);
			Assert.True(stage2.IsInitialized);

			// shut the stages down
			Assert.False(stage1.OnShutdownWasCalled);
			Assert.False(stage2.OnShutdownWasCalled);
			((IProcessingPipelineStage)stage1).Shutdown();
			Assert.True(stage1.OnShutdownWasCalled);
			Assert.True(stage2.OnShutdownWasCalled);
			Assert.False(stage1.IsInitialized);
			Assert.False(stage2.IsInitialized);
		}

		/// <summary>
		/// Tests whether processing a log message using <see cref="IProcessingPipelineStage.ProcessMessage"/> succeeds,
		/// if the stage does not have following stages. The stage should have called <see cref="ProcessingPipelineStage{STAGE}.ProcessSync"/>
		/// after this.
		/// </summary>
		[Fact]
		public void Process_Standalone()
		{
			var stage = CreateStage("Stage");

			// initialize the stage
			Assert.False(stage.IsInitialized);
			Assert.False(stage.OnInitializeWasCalled);
			((IProcessingPipelineStage)stage).Initialize();
			Assert.True(stage.OnInitializeWasCalled);
			Assert.True(stage.IsInitialized);

			// process a log message
			var message = MessagePool.GetUninitializedMessage();
			Assert.False(stage.ProcessSyncWasCalled);
			((IProcessingPipelineStage)stage).ProcessMessage(message);
			Assert.True(stage.ProcessSyncWasCalled);
			Assert.Same(message, stage.MessagePassedToProcessSync);

			// shut the stage down
			((IProcessingPipelineStage)stage).Shutdown();
			Assert.False(stage.IsInitialized);
		}

		/// <summary>
		/// Tests whether calling <see cref="IProcessingPipelineStage.ProcessMessage"/> invokes
		/// <see cref="ProcessingPipelineStage{STAGE}.ProcessSync(LocalLogMessage)"/>, if the stage has a following stage.
		/// Both stages should have called <see cref="ProcessingPipelineStage{STAGE}.ProcessSync"/> after this.
		/// </summary>
		[Fact]
		public void Process_WithFollowingStage()
		{
			var stage1 = CreateStage("Stage1");
			var stage2 = CreateStage("Stage2");
			stage1.AddNextStage(stage2);

			// initialize the stages
			Assert.False(stage1.IsInitialized);
			Assert.False(stage2.IsInitialized);
			((IProcessingPipelineStage)stage1).Initialize();
			Assert.True(stage1.IsInitialized);
			Assert.True(stage2.IsInitialized);

			// process a log message
			var message = MessagePool.GetUninitializedMessage();
			Assert.False(stage1.ProcessSyncWasCalled);
			Assert.False(stage2.ProcessSyncWasCalled);
			((IProcessingPipelineStage)stage1).ProcessMessage(message);
			Assert.True(stage1.ProcessSyncWasCalled);
			Assert.True(stage2.ProcessSyncWasCalled);
			Assert.Same(message, stage1.MessagePassedToProcessSync);
			Assert.Same(message, stage2.MessagePassedToProcessSync);

			// shut the stages down
			((IProcessingPipelineStage)stage1).Shutdown();
			Assert.False(stage1.IsInitialized);
			Assert.False(stage2.IsInitialized);
		}
	}

}
