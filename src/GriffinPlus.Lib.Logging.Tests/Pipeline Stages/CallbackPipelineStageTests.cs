///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Unit tests targeting the <see cref="CallbackPipelineStage"/> class.
	/// </summary>
	public class CallbackPipelineStageTests : ProcessingPipelineStageBaseTests<CallbackPipelineStage>
	{
		internal class Callback
		{
			/// <summary>
			/// Gets or sets the value returned by <see cref="ProcessSyncCallback(LocalLogMessage)"/>.
			/// </summary>
			public bool ProcessSyncCallbackReturnValue { get; set; }

			/// <summary>
			/// Gets a value indicating whether <see cref="ProcessSyncCallback(LocalLogMessage)"/> was called.
			/// </summary>
			public bool ProcessSyncCallbackWasCalled { get; private set; }

			/// <summary>
			/// Gets the log message passed to <see cref="ProcessSyncCallback(LocalLogMessage)"/>.
			/// </summary>
			public LocalLogMessage MessagePassedToProcessSyncCallback { get; private set; }

			/// <summary>
			/// The callback that is expected to be invoked when a log message is to process.
			/// </summary>
			/// <param name="message"></param>
			/// <returns></returns>
			public bool ProcessSyncCallback(LocalLogMessage message)
			{
				ProcessSyncCallbackWasCalled = true;
				MessagePassedToProcessSyncCallback = message;
				MessagePassedToProcessSyncCallback.AddRef();
				return ProcessSyncCallbackReturnValue;
			}
		}

		/// <summary>
		/// Creates a new instance of the pipeline stage.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <returns>The created stage.</returns>
		protected override CallbackPipelineStage CreateStage(string name)
		{
			var callback = new Callback();
			return new CallbackPipelineStage(name, callback.ProcessSyncCallback);
		}

		/// <summary>
		/// Tests whether creating a new instance of the pipeline stage succeeds and the stage is in the expected state
		/// (only non-default stuff is checked, the rest is done by the base test class).
		/// </summary>
		[Fact]
		private void Create_WithCallback()
		{
			var callback = new Callback();
			var stage = new CallbackPipelineStage("Callback", callback.ProcessSyncCallback);
			Assert.Empty(stage.Settings);
		}

		/// <summary>
		/// Tests whether creating a new instance of the pipeline stage succeeds, if no callback is specified.
		/// </summary>
		[Fact]
		private void Create_WithoutCallback()
		{
			var stage = new CallbackPipelineStage("Callback", null);
			Assert.Empty(stage.Settings);
		}

		/// <summary>
		/// Tests whether processing a log message succeeds, if the stage does not have following stages.
		/// </summary>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void Process_Standalone(bool processSyncReturnValue)
		{
			var callback = new Callback { ProcessSyncCallbackReturnValue = processSyncReturnValue };
			var stage = new CallbackPipelineStage("Callback", callback.ProcessSyncCallback);

			// initialize the stage
			Assert.False(stage.IsInitialized);
			((IProcessingPipelineStage) stage).Initialize();
			Assert.True(stage.IsInitialized);

			// process a log message
			var message = MessagePool.GetUninitializedMessage();
			Assert.False(callback.ProcessSyncCallbackWasCalled);
			((IProcessingPipelineStage) stage).ProcessMessage(message);
			Assert.True(callback.ProcessSyncCallbackWasCalled);
			Assert.Same(message, callback.MessagePassedToProcessSyncCallback);

			// shut the stage down
			((IProcessingPipelineStage)stage).Shutdown();
			Assert.False(stage.IsInitialized);
		}

		/// <summary>
		/// Tests whether processing a log message succeeds, if the stage has a following stage.
		/// Both stages should have called <see cref="Callback.ProcessSyncCallback(LocalLogMessage)"/> after this.
		/// </summary>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void Process_WithFollowingStage(bool processSyncReturnValue)
		{
			var callback1 = new Callback { ProcessSyncCallbackReturnValue = processSyncReturnValue };
			var callback2 = new Callback { ProcessSyncCallbackReturnValue = processSyncReturnValue };
			var stage1 = new CallbackPipelineStage("Callback1", callback1.ProcessSyncCallback);
			var stage2 = new CallbackPipelineStage("Callback2", callback2.ProcessSyncCallback);
			stage1.AddNextStage(stage2);

			// initialize the stages
			Assert.False(stage1.IsInitialized);
			Assert.False(stage2.IsInitialized);
			((IProcessingPipelineStage) stage1).Initialize();
			Assert.True(stage1.IsInitialized);
			Assert.True(stage2.IsInitialized);

			// process a log message
			var message = MessagePool.GetUninitializedMessage();
			Assert.False(callback1.ProcessSyncCallbackWasCalled);
			Assert.False(callback2.ProcessSyncCallbackWasCalled);
			((IProcessingPipelineStage) stage1).ProcessMessage(message);
			if (processSyncReturnValue)
			{
				// the message should have traveled through stage 1 and 2
				Assert.True(callback1.ProcessSyncCallbackWasCalled);
				Assert.True(callback2.ProcessSyncCallbackWasCalled);
				Assert.Same(message, callback1.MessagePassedToProcessSyncCallback);
				Assert.Same(message, callback2.MessagePassedToProcessSyncCallback);
			}
			else
			{
				// the message should have traveled through stage 1 only
				Assert.True(callback1.ProcessSyncCallbackWasCalled);
				Assert.False(callback2.ProcessSyncCallbackWasCalled);
				Assert.Same(message, callback1.MessagePassedToProcessSyncCallback);
				Assert.Null(callback2.MessagePassedToProcessSyncCallback);
			}

			// shut the stages down
			((IProcessingPipelineStage)stage1).Shutdown();
			Assert.False(stage1.IsInitialized);
			Assert.False(stage2.IsInitialized);
		}

	}
}
