///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Pipeline stage that only provides functionality of the <see cref="AsyncProcessingPipelineStage{STAGE}"/> class.
	/// It is used for testing purposes only.
	/// </summary>
	public class AsyncProcessingPipelineTestStage : AsyncProcessingPipelineStage<AsyncProcessingPipelineTestStage>
	{
		/// <summary>
		/// Gets a value indicating whether <see cref="OnInitialize"/> was called.
		/// </summary>
		public bool OnInitializeWasCalled { get; private set; }

		/// <summary>
		/// Gets a value indicating whether <see cref="OnShutdown"/> was called.
		/// </summary>
		public bool OnShutdownWasCalled { get; private set; }

		/// <summary>
		/// Gets a value indicating whether <see cref="ProcessSync(LocalLogMessage, out bool)"/> was called.
		/// </summary>
		public bool ProcessSyncWasCalled { get; private set; }

		/// <summary>
		/// Gets the message that was passed to <see cref="AsyncProcessingPipelineStage{STAGE}.ProcessSync(LocalLogMessage, out bool)" />
		/// with the last call.
		/// </summary>
		public LocalLogMessage MessagePassedToProcessSync { get; private set; }

		/// <summary>
		/// Gets the messages that were passed to <see cref="AsyncProcessingPipelineStage{STAGE}.ProcessAsync(LocalLogMessage[], CancellationToken)" />
		/// with the last call.
		/// </summary>
		public List<LocalLogMessage> MessagesPassedToProcessAsync { get; } = new List<LocalLogMessage>();

		/// <summary>
		/// Gets a value indicating whether <see cref="ProcessAsync(LocalLogMessage[], CancellationToken)"/> was called.
		/// </summary>
		public bool ProcessAsyncWasCalled { get; private set; }

		protected override void OnInitialize()
		{
			OnInitializeWasCalled = true;
			base.OnInitialize();
		}

		protected internal override void OnShutdown()
		{
			OnShutdownWasCalled = true;
			base.OnShutdown();
		}

		protected override bool ProcessSync(LocalLogMessage message, out bool queueForAsyncProcessing)
		{
			ProcessSyncWasCalled = true;
			MessagePassedToProcessSync?.Release();
			MessagePassedToProcessSync = message;
			MessagePassedToProcessSync.AddRef();
			return base.ProcessSync(message, out queueForAsyncProcessing);
		}

		protected override Task ProcessAsync(LocalLogMessage[] messages, CancellationToken cancellationToken)
		{
			ProcessAsyncWasCalled = true;

			MessagesPassedToProcessAsync.ForEach(x => x.Release());
			MessagesPassedToProcessAsync.Clear();
			MessagesPassedToProcessAsync.AddRange(messages);
			MessagesPassedToProcessAsync.ForEach(x => x.AddRef());

			return base.ProcessAsync(messages, cancellationToken);
		}

	}
}
