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

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Interface for stages in the log message processing pipeline.
	/// </summary>
	public interface ILogMessageProcessingPipelineStage
	{
		/// <summary>
		/// Processes the specified log message.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <remarks>
		/// Do not keep a reference to the passed log message object as it returns to a pool.
		/// Log message objects are re-used to reduce garbage collection pressure.
		/// </remarks>
		void Process(LogMessage message);
	}

}
