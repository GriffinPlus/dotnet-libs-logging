///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging;

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
	/// Tests whether creating a new instance of the pipeline stage succeeds.
	/// </summary>
	[Fact]
	private void Create()
	{
		var stage = ProcessingPipelineStage.Create<CallbackPipelineStage>("Callback", null);
		Assert.Null(stage.ProcessingCallback);
		Assert.Empty(stage.NextStages);
		Assert.Empty(stage.Settings);
	}

	/// <summary>
	/// Tests whether processing a log message succeeds, if the stage does not have any following stages.
	/// </summary>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Process_Standalone(bool processSyncReturnValue)
	{
		var callback = new Callback { ProcessSyncCallbackReturnValue = processSyncReturnValue };
		var stage = ProcessingPipelineStage.Create<CallbackPipelineStage>("Callback", null);
		stage.ProcessingCallback = callback.ProcessSyncCallback;

		// initialize the stage
		Assert.False(stage.IsInitialized);
		stage.Initialize();
		Assert.True(stage.IsInitialized);

		// process a log message
		LocalLogMessage message = MessagePool.GetUninitializedMessage();
		Assert.False(callback.ProcessSyncCallbackWasCalled);
		stage.ProcessMessage(message);
		Assert.True(callback.ProcessSyncCallbackWasCalled);
		Assert.Same(message, callback.MessagePassedToProcessSyncCallback);

		// shut the stage down
		stage.Shutdown();
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
		var stage1 = ProcessingPipelineStage.Create<CallbackPipelineStage>("Callback1", null);
		var stage2 = stage1.AddNextStage<CallbackPipelineStage>("Callback2");
		stage1.ProcessingCallback = callback1.ProcessSyncCallback;
		stage2.ProcessingCallback = callback2.ProcessSyncCallback;

		// initialize the stages
		Assert.False(stage1.IsInitialized);
		Assert.False(stage2.IsInitialized);
		stage1.Initialize();
		Assert.True(stage1.IsInitialized);
		Assert.True(stage2.IsInitialized);

		// process a log message
		LocalLogMessage message = MessagePool.GetUninitializedMessage();
		Assert.False(callback1.ProcessSyncCallbackWasCalled);
		Assert.False(callback2.ProcessSyncCallbackWasCalled);
		stage1.ProcessMessage(message);
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
		stage1.Shutdown();
		Assert.False(stage1.IsInitialized);
		Assert.False(stage2.IsInitialized);
	}
}
