﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="AsyncCallbackPipelineStage"/> class.
	/// </summary>
	public class AsyncCallbackPipelineStageTests : AsyncProcessingPipelineStageBaseTests<AsyncCallbackPipelineStage>
	{
		internal class Callback
		{
			/// <summary>
			/// Gets or sets the value returned by <see cref="ProcessSyncCallback(LocalLogMessage, out bool)"/>.
			/// </summary>
			public bool ProcessSyncCallbackReturnValue { get; set; }

			/// <summary>
			/// Gets or sets the output value returned by <see cref="ProcessSyncCallback(LocalLogMessage, out bool)"/>.
			/// </summary>
			public bool ProcessSyncCallbackQueueForAsyncProcessing { get; set; }

			/// <summary>
			/// Gets a value indicating whether <see cref="ProcessSyncCallback(LocalLogMessage, out bool)"/> was called.
			/// </summary>
			public bool ProcessSyncCallbackWasCalled { get; private set; }

			/// <summary>
			/// Gets the log message passed to <see cref="ProcessSyncCallback(LocalLogMessage, out bool)"/>.
			/// </summary>
			public LocalLogMessage MessagePassedToProcessSyncCallback { get; private set; }

			public bool ProcessSyncCallback(LocalLogMessage message, out bool queueForAsyncProcessing)
			{
				queueForAsyncProcessing = ProcessSyncCallbackQueueForAsyncProcessing;

				ProcessSyncCallbackWasCalled = true;
				MessagePassedToProcessSyncCallback?.Release();
				MessagePassedToProcessSyncCallback = message;
				MessagePassedToProcessSyncCallback.AddRef();

				return ProcessSyncCallbackReturnValue;
			}

			/// <summary>
			/// Gets a value indicating whether <see cref="ProcessAsyncCallback(LocalLogMessage[], CancellationToken)"/> was called.
			/// </summary>
			public bool ProcessAsyncCallbackWasCalled { get; private set; }

			/// <summary>
			/// Gets the log message passed to <see cref="ProcessAsyncCallback(LocalLogMessage[], CancellationToken)"/>.
			/// </summary>
			public List<LocalLogMessage> MessagesPassedToProcessAsyncCallback { get; } = new List<LocalLogMessage>();

			public Task ProcessAsyncCallback(LocalLogMessage[] messages, CancellationToken cancellationToken)
			{
				ProcessAsyncCallbackWasCalled = true;

				MessagesPassedToProcessAsyncCallback.ForEach(x => x.Release());
				MessagesPassedToProcessAsyncCallback.Clear();
				MessagesPassedToProcessAsyncCallback.AddRange(messages);
				MessagesPassedToProcessAsyncCallback.ForEach(x => x.AddRef());

				return Task.CompletedTask;
			}
		}

		/// <summary>
		/// Creates a new instance of the pipeline stage.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <returns>The created stage.</returns>
		protected override AsyncCallbackPipelineStage CreateStage(string name)
		{
			var callback = new Callback();
			return new AsyncCallbackPipelineStage(name, callback.ProcessSyncCallback, callback.ProcessAsyncCallback);
		}

		/// <summary>
		/// Tests whether creating a new instance of the pipeline stage succeeds and the stage is in the expected state
		/// (only non-default stuff is checked, the rest is done by the base test class).
		/// </summary>
		[Fact]
		private void Create_WithBothCallbacks()
		{
			var callback = new Callback();
			var stage = new AsyncCallbackPipelineStage("Callback", callback.ProcessSyncCallback, callback.ProcessAsyncCallback);
			Assert.Empty(stage.Settings);
		}

		/// <summary>
		/// Tests whether creating a new instance of the pipeline stage succeeds, if only the synchronous processing callback is specified.
		/// </summary>
		[Fact]
		private void Create_WithSyncCallbackOnly()
		{
			var callback = new Callback();
			var stage = new AsyncCallbackPipelineStage("Callback", callback.ProcessSyncCallback, null);
			Assert.Empty(stage.Settings);
		}

		/// <summary>
		/// Tests whether creating a new instance of the pipeline stage succeeds, if only the asynchronous processing callback is specified.
		/// </summary>
		[Fact]
		private void Create_WithAsyncCallbackOnly()
		{
			var callback = new Callback();
			var stage = new AsyncCallbackPipelineStage("Callback", null, callback.ProcessAsyncCallback);
			Assert.Empty(stage.Settings);
		}

		/// <summary>
		/// Tests whether processing a log message succeeds, if the stage does not have following stages.
		/// </summary>
		[Theory]
		[InlineData(false, false)]
		[InlineData(false, true)]
		[InlineData(true, false)]
		[InlineData(true, true)]
		public void Process_Standalone(bool processSyncReturnValue, bool queueForAsyncProcessing)
		{
			var callback = new Callback
			{
				ProcessSyncCallbackReturnValue = processSyncReturnValue,
				ProcessSyncCallbackQueueForAsyncProcessing = queueForAsyncProcessing
			};
			var stage = new AsyncCallbackPipelineStage("Callback", callback.ProcessSyncCallback, callback.ProcessAsyncCallback);

			// initialize the stage
			Assert.False(stage.IsInitialized);
			((IProcessingPipelineStage)stage).Initialize();
			Assert.True(stage.IsInitialized);

			// process a log message
			var message = MessagePool.GetUninitializedMessage();
			Assert.False(callback.ProcessSyncCallbackWasCalled);
			((IProcessingPipelineStage)stage).ProcessMessage(message);

			// wait for the message to travel through asynchronous processing
			// (just waiting once for some time seems to fail on azure pipelines,
			// probably jobs are moved within the cloud causing undeterministic delays)
			for (int i = 0; i < 10; i++) Thread.Sleep(100);

			// check synchronous processing
			Assert.True(callback.ProcessSyncCallbackWasCalled);
			Assert.Same(message, callback.MessagePassedToProcessSyncCallback);

			// check asynchronous processing
			if (queueForAsyncProcessing)
			{
				Assert.True(callback.ProcessAsyncCallbackWasCalled);
				Assert.Single(callback.MessagesPassedToProcessAsyncCallback);
				Assert.Same(message, callback.MessagesPassedToProcessAsyncCallback.First());
			}
			else
			{
				Assert.False(callback.ProcessAsyncCallbackWasCalled);
				Assert.Empty(callback.MessagesPassedToProcessAsyncCallback);
			}

			// shut the stage down
			((IProcessingPipelineStage)stage).Shutdown();
			Assert.False(stage.IsInitialized);
		}

		/// <summary>
		/// Tests whether processing a log message succeeds, if the stage has a following stage.
		/// </summary>
		[Theory]
		[InlineData(false, false)]
		[InlineData(false, true)]
		[InlineData(true, false)]
		[InlineData(true, true)]
		public void Process_WithFollowingStage(bool processSyncReturnValue, bool queueForAsyncProcessing)
		{
			var callback1 = new Callback
			{
				ProcessSyncCallbackReturnValue = processSyncReturnValue,
				ProcessSyncCallbackQueueForAsyncProcessing = queueForAsyncProcessing
			};
			var callback2 = new Callback
			{
				ProcessSyncCallbackReturnValue = processSyncReturnValue,
				ProcessSyncCallbackQueueForAsyncProcessing = queueForAsyncProcessing
			};
			var stage1 = new AsyncCallbackPipelineStage("Callback1", callback1.ProcessSyncCallback, callback1.ProcessAsyncCallback);
			var stage2 = new AsyncCallbackPipelineStage("Callback2", callback2.ProcessSyncCallback, callback2.ProcessAsyncCallback);
			stage1.AddNextStage(stage2);

			// initialize the stages
			Assert.False(stage1.IsInitialized);
			Assert.False(stage2.IsInitialized);
			((IProcessingPipelineStage)stage1).Initialize();
			Assert.True(stage1.IsInitialized);
			Assert.True(stage2.IsInitialized);

			// process a log message
			var message = MessagePool.GetUninitializedMessage();
			Assert.False(callback1.ProcessSyncCallbackWasCalled);
			Assert.False(callback2.ProcessSyncCallbackWasCalled);
			((IProcessingPipelineStage)stage1).ProcessMessage(message);

			// wait for the message to travel through asynchronous processing
			// (just waiting for some time seems to fail on azure pipelines,
			// probably jobs are moved within the cloud causing undeterministic delays)
			for (int i = 0; i < 10; i++) Thread.Sleep(100);

			// check where the message went to
			if (processSyncReturnValue)
			{
				// check synchronous processing
				// (the message should have traveled through stage 1 and 2)
				Assert.True(callback1.ProcessSyncCallbackWasCalled);
				Assert.True(callback2.ProcessSyncCallbackWasCalled);
				Assert.Same(message, callback1.MessagePassedToProcessSyncCallback);
				Assert.Same(message, callback2.MessagePassedToProcessSyncCallback);

				// check asynchronous processing
				if (queueForAsyncProcessing)
				{
					Assert.True(callback1.ProcessAsyncCallbackWasCalled);
					Assert.True(callback2.ProcessAsyncCallbackWasCalled);
					Assert.Single(callback1.MessagesPassedToProcessAsyncCallback);
					Assert.Single(callback2.MessagesPassedToProcessAsyncCallback);
					Assert.Same(message, callback1.MessagesPassedToProcessAsyncCallback.First());
					Assert.Same(message, callback2.MessagesPassedToProcessAsyncCallback.First());
				}
				else
				{
					Assert.False(callback1.ProcessAsyncCallbackWasCalled);
					Assert.False(callback2.ProcessAsyncCallbackWasCalled);
					Assert.Empty(callback1.MessagesPassedToProcessAsyncCallback);
					Assert.Empty(callback2.MessagesPassedToProcessAsyncCallback);
				}
			}
			else
			{
				// check synchronous processing
				// (the message should have traveled through stage 1 only)
				Assert.True(callback1.ProcessSyncCallbackWasCalled);
				Assert.False(callback2.ProcessSyncCallbackWasCalled);
				Assert.Same(message, callback1.MessagePassedToProcessSyncCallback);
				Assert.Null(callback2.MessagePassedToProcessSyncCallback);

				// check asynchronous processing
				if (queueForAsyncProcessing)
				{
					Assert.True(callback1.ProcessAsyncCallbackWasCalled);
					Assert.False(callback2.ProcessAsyncCallbackWasCalled);
					Assert.Single(callback1.MessagesPassedToProcessAsyncCallback);
					Assert.Empty(callback2.MessagesPassedToProcessAsyncCallback);
					Assert.Same(message, callback1.MessagesPassedToProcessAsyncCallback.First());
				}
				else
				{
					Assert.False(callback1.ProcessAsyncCallbackWasCalled);
					Assert.False(callback2.ProcessAsyncCallbackWasCalled);
					Assert.Empty(callback1.MessagesPassedToProcessAsyncCallback);
					Assert.Empty(callback2.MessagesPassedToProcessAsyncCallback);
				}
			}

			// shut the pipeline stages down
			((IProcessingPipelineStage)stage1).Shutdown();
			Assert.False(stage1.IsInitialized);
			Assert.False(stage2.IsInitialized);
		}
	}

}
