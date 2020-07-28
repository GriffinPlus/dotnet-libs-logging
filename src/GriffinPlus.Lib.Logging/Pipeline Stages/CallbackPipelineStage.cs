///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018-2019 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A processing pipeline stage that invokes a callback to process a log message (thread-safe).
	/// This is a lightweight alternative to implementing an entire custom processing pipeline stage.
	/// </summary>
	public class CallbackPipelineStage : ProcessingPipelineStage<CallbackPipelineStage>
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
		/// The message processing callback.
		/// </summary>
		private readonly ProcessingCallback mProcessingCallback;

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncCallbackPipelineStage"/> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <param name="processCallback">
		/// Callback processing a log message traveling through the pipeline (may be null).
		/// The callback is executed in the context of the thread writing the message.
		/// </param>
		/// <remarks>
		/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		public CallbackPipelineStage(string name, ProcessingCallback processCallback) : base(name)
		{
			mProcessingCallback = processCallback;
		}

		/// <summary>
		/// Processes the specified log message synchronously (is executed in the context of the thread writing the message).
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <remarks>
		/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		protected override bool ProcessSync(LocalLogMessage message)
		{
			if (mProcessingCallback != null) return mProcessingCallback(message);
			else                             return base.ProcessSync(message);
		}

	}
}
