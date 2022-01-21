///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="ProcessingPipelineStage"/> class as a base class for derived pipeline stages.
	/// </summary>
	public abstract class ProcessingPipelineStageBaseTests<TStage> where TStage : ProcessingPipelineStage
	{
		// ReSharper disable once StaticMemberInGenericType
		internal static LocalLogMessagePool MessagePool = new LocalLogMessagePool();

		/// <summary>
		/// Creates a new instance of the pipeline stage.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <returns>The created stage.</returns>
		protected abstract TStage CreateStage(string name);

		/// <summary>
		/// Tests whether creating a new stage succeeds and the stage is in the expected state
		/// (as far as the state of the base class is concerned).
		/// </summary>
		[Fact]
		public void Create_And_Check_State()
		{
			var stage = CreateStage("Stage");

			// a new stage should not be initialized (attached to the logging subsystem)
			Assert.False(stage.IsInitialized);

			// a new stage should have no following stages
			Assert.Empty(stage.NextStages);

			// the stage should be the one and only stage (no following stages)
			var stages = new HashSet<IProcessingPipelineStage>();
			stage.GetAllStages(stages);
			Assert.Single(stages);
			Assert.Same(stage, stages.First());
		}

		/// <summary>
		/// Tests adding and getting following stages using
		/// - <see cref="ProcessingPipelineStage.AddNextStage(IProcessingPipelineStage)"/>
		/// - <see cref="ProcessingPipelineStage.NextStages"/>
		/// - <see cref="ProcessingPipelineStage.GetAllStages(System.Collections.Generic.HashSet{GriffinPlus.Lib.Logging.IProcessingPipelineStage})"/>
		/// </summary>
		[Fact]
		public void Adding_And_Getting_Next_Stages()
		{
			var stage1 = CreateStage("Stage1");
			var stage2 = CreateStage("Stage2");
			var stages12 = new HashSet<TStage> { stage1, stage2 };

			// add stage 2 as follower of stage 1
			stage1.AddNextStage(stage2);

			// stage 1 should have stage 2 as following stage
			Assert.Single(stage1.NextStages);
			Assert.Same(stage2, stage1.NextStages.First());

			// stage 2 should have no following stages
			Assert.Empty(stage2.NextStages);

			// stage 1 and 2 should be returned by GetAllStages() of stage 1
			var stages1 = new HashSet<IProcessingPipelineStage>();
			stage1.GetAllStages(stages1);
			Assert.Equal(2, stages1.Count);
			Assert.Equal(stages12, stages1);

			// stage 2 should have no following stages
			Assert.Empty(stage2.NextStages);
			var stages2 = new HashSet<IProcessingPipelineStage>();
			stage2.GetAllStages(stages2);
			Assert.Single(stages2);
			Assert.Same(stage2, stages2.First());
		}

		/// <summary>
		/// Tests whether initializing the pipeline stage using <see cref="IProcessingPipelineStage.Initialize"/> succeeds,
		/// if the stage does not have following stages. The stage should be initialized after this.
		/// </summary>
		[Fact]
		public void Initialize_Standalone()
		{
			var stage = CreateStage("Stage");
			Assert.False(stage.IsInitialized);
			((IProcessingPipelineStage)stage).Initialize();
			Assert.True(stage.IsInitialized);
		}

		/// <summary>
		/// Tests whether initializing the pipeline stage using <see cref="IProcessingPipelineStage.Initialize"/> succeeds,
		/// if the stage has a following stage. Both stages should be initialized after this.
		/// </summary>
		[Fact]
		public void Initialize_WithFollowingStage()
		{
			var stage1 = CreateStage("Stage1");
			var stage2 = new ProcessingPipelineTestStage("Stage2");
			stage1.AddNextStage(stage2);
			Assert.False(stage1.IsInitialized);
			Assert.False(stage2.IsInitialized);
			((IProcessingPipelineStage)stage1).Initialize();
			Assert.True(stage1.IsInitialized);
			Assert.True(stage2.IsInitialized);
		}

		/// <summary>
		/// Test whether initializing the pipeline stage using <see cref="IProcessingPipelineStage.Initialize"/> throws an exception,
		/// if the stage is already initialized (attached to the logging subsystem).
		/// </summary>
		[Fact]
		public void Initialize_FailsIfAlreadyInitialized()
		{
			var stage = CreateStage("Stage");

			// initialize the first time
			Assert.False(stage.IsInitialized);
			((IProcessingPipelineStage)stage).Initialize();
			Assert.True(stage.IsInitialized);

			// initialize once again
			Assert.Throws<InvalidOperationException>(() => ((IProcessingPipelineStage)stage).Initialize());
		}

		/// <summary>
		/// Tests whether shutting down the pipeline stage using <see cref="IProcessingPipelineStage.Shutdown"/> succeeds,
		/// if the stage does not have following stages. The stage should not be initialized after this.
		/// </summary>
		[Fact]
		public void Shutdown_Standalone()
		{
			var stage = CreateStage("Stage");

			// initialize the stage
			Assert.False(stage.IsInitialized);
			((IProcessingPipelineStage)stage).Initialize();
			Assert.True(stage.IsInitialized);

			// shut the stage down
			((IProcessingPipelineStage)stage).Shutdown();
			Assert.False(stage.IsInitialized);
		}

		/// <summary>
		/// Tests whether shutting down the pipeline stage using <see cref="IProcessingPipelineStage.Shutdown"/> succeeds,
		/// if the stage has a following stage. Both stages should not be initialized after this.
		/// </summary>
		[Fact]
		public void Shutdown_WithFollowingStage()
		{
			var stage1 = CreateStage("Stage1");
			var stage2 = new ProcessingPipelineTestStage("Stage2");
			stage1.AddNextStage(stage2);

			// initialize the stages
			Assert.False(stage1.IsInitialized);
			Assert.False(stage2.IsInitialized);
			((IProcessingPipelineStage)stage1).Initialize();
			Assert.True(stage1.IsInitialized);
			Assert.True(stage2.IsInitialized);

			// shut the stages down
			((IProcessingPipelineStage)stage1).Shutdown();
			Assert.False(stage1.IsInitialized);
			Assert.False(stage2.IsInitialized);
		}

		/// <summary>
		/// Test whether <see cref="IProcessingPipelineStage.ProcessMessage"/> throws an exception,
		/// if the message to process is a null reference.
		/// </summary>
		[Fact]
		public void Process_FailsIfMessageIsNull()
		{
			var stage = CreateStage("Stage");
			Assert.Throws<ArgumentNullException>(() => ((IProcessingPipelineStage)stage).ProcessMessage(null));
		}

		/// <summary>
		/// Test whether <see cref="IProcessingPipelineStage.ProcessMessage"/> throws an exception,
		/// if the stage is not initialized (attached to the logging subsystem).
		/// </summary>
		[Fact]
		public void Process_FailsIfNotInitialized()
		{
			var stage = CreateStage("Stage");
			var message = MessagePool.GetUninitializedMessage();
			Assert.Throws<InvalidOperationException>(() => ((IProcessingPipelineStage)stage).ProcessMessage(message));
		}
	}

}
