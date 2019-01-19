///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://griffin.plus)
//
// Copyright 2018 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A processing pipeline stage that invokes a callback to process a log message (thread-safe).
	/// This is a lightweight alternative to implementing an entire custom processing pipeline stage.
	/// </summary>
	public class CallbackProcessingPipelineStage : ProcessingPipelineStage<CallbackProcessingPipelineStage>
	{
		/// <summary>
		/// A delegate that processes the specified log message.
		/// </summary>
		/// <param name="message">Log message to process.</param>
		/// <returns>
		/// true to call the following pipeline stages;
		/// false to stop processing.
		/// </returns>
		public delegate bool ProcessingCallback(LocalLogMessage message);

		/// <summary>
		/// The message processing callback (always initialized).
		/// </summary>
		private ProcessingCallback mProcessingCallback;

		/// <summary>
		/// Initializes a new instance of the <see cref="CallbackProcessingPipelineStage"/> class.
		/// </summary>
		/// <param name="callback">Callback processing a log message traveling through the pipeline.</param>
		/// <remarks>
		/// Do not keep a reference to the passed log message object as it returns to a pool.
		/// Log message objects are re-used to reduce garbage collection pressure.
		/// </remarks>
		public CallbackProcessingPipelineStage(ProcessingCallback callback)
		{
			mProcessingCallback = callback ?? throw new ArgumentNullException(nameof(callback));
		}

		/// <summary>
		/// Processes the specified log message and passes the log message to the next processing stages.
		/// </summary>
		/// <param name="message">Message to process.</param>
		public override void Process(LocalLogMessage message)
		{
			if (mProcessingCallback(message)) {
				base.Process(message);
			}
		}
	}
}
