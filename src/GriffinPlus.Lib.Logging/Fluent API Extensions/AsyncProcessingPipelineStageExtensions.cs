///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2019 Sascha Falk <sascha@falk-online.eu>
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
	/// Fluent API extension methods for the <see cref="AsyncProcessingPipelineStage{STAGE}"/> class.
	/// </summary>
	public static class AsyncProcessingPipelineStageExtensions
	{
		/// <summary>
		/// Configures the queue buffering messages to be processed asynchronously.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <param name="queueSize">Capacity of the queue buffering messages to be processed asynchronously.</param>
		/// <param name="discardMessageIfQueueFull">
		/// <c>true</c> to discard a message, if the message queue is full;
		/// <c>false</c> to block the thread writing a message until the message is in the queue.
		/// </param>
		/// <returns>The modified pipeline stage.</returns>
		public static STAGE WithQueue<STAGE>(this STAGE @this, int queueSize, bool discardMessageIfQueueFull) where STAGE: AsyncProcessingPipelineStage<STAGE>
		{
			@this.ConfigureQueue(queueSize, discardMessageIfQueueFull);
			return @this;
		}

		/// <summary>
		/// Sets the shutdown timeout of the asynchronous processing thread (in ms).
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		/// <returns>The modified pipeline stage.</returns>
		public static STAGE WithShutdownTimeout<STAGE>(this STAGE @this, int timeout) where STAGE: AsyncProcessingPipelineStage<STAGE>
		{
			@this.ShutdownTimeout = timeout;
			return @this;
		}
	}
}
