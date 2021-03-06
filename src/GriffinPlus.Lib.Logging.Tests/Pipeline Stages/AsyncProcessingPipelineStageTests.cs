﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Threading;

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="AsyncProcessingPipelineStage{STAGE}"/> class (not derived pipeline stages).
	/// </summary>
	public class AsyncProcessingPipelineStageTests : AsyncProcessingPipelineStageBaseTests<AsyncProcessingPipelineTestStage>
	{
		/// <summary>
		/// Creates a new instance of the pipeline stage.
		/// </summary>
		/// <returns></returns>
		protected override AsyncProcessingPipelineTestStage CreateStage(string name)
		{
			return new AsyncProcessingPipelineTestStage(name);
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
			Assert.Equal(500, stage.MessageQueueSize);
			Assert.False(stage.DiscardMessagesIfQueueFull);
			Assert.Equal(TimeSpan.FromSeconds(5), stage.ShutdownTimeout);

			// check properties of test pipeline stage
			Assert.False(stage.OnInitializeWasCalled);
			Assert.False(stage.OnShutdownWasCalled);
		}

		/// <summary>
		/// Tests whether initializing the pipeline stage using <see cref="IProcessingPipelineStage.Initialize"/> succeeds,
		/// if the stage does not have following stages. The stage should have called <see cref="AsyncProcessingPipelineStage{STAGE}.OnInitialize"/>
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
		/// if the stage has a following stage. Both stages should have called <see cref="AsyncProcessingPipelineStage{STAGE}.OnInitialize"/>
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
		/// if the stage does not have following stages. The stage should have called <see cref="AsyncProcessingPipelineStage{STAGE}.OnShutdown"/>
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
		/// if the stage has a following stage. Both stages should have called <see cref="AsyncProcessingPipelineStage{STAGE}.OnShutdown"/>
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
		/// if the stage does not have following stages. The stage should have called <see cref="AsyncProcessingPipelineStage{STAGE}.ProcessSync"/>
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
		}

		/// <summary>
		/// Tests whether calling <see cref="IProcessingPipelineStage.ProcessMessage"/> invokes
		/// <see cref="AsyncProcessingPipelineStage{STAGE}.ProcessSync"/>, if the stage has a following stage.
		/// Both stages should have called <see cref="AsyncProcessingPipelineStage{STAGE}.ProcessSync"/> after this.
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
			Assert.False(stage1.ProcessAsyncWasCalled);
			Assert.False(stage2.ProcessAsyncWasCalled);
			((IProcessingPipelineStage)stage1).ProcessMessage(message);
			Assert.True(stage1.ProcessSyncWasCalled);
			Assert.True(stage2.ProcessSyncWasCalled);
			Assert.Same(message, stage1.MessagePassedToProcessSync);
			Assert.Same(message, stage2.MessagePassedToProcessSync);

			// give the processing threads time to call ProcessAsync()
			// (just waiting for some time seems to fail on azure pipelines,
			// probably jobs are moved within the cloud causing undeterministic delays)
			for (int i = 0; i < 10; i++) Thread.Sleep(100);

			Assert.True(stage1.ProcessAsyncWasCalled);
			Assert.True(stage2.ProcessAsyncWasCalled);
			Assert.Single(stage1.MessagesPassedToProcessAsync);
			Assert.Single(stage2.MessagesPassedToProcessAsync);
			Assert.Same(message, stage1.MessagesPassedToProcessAsync.First());
			Assert.Same(message, stage2.MessagesPassedToProcessAsync.First());
		}
	}

}
