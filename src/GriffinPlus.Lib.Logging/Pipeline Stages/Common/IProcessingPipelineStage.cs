﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018-2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Interface for stages in the log message processing pipeline (must be implemented thread-safe).
	/// </summary>
	public interface IProcessingPipelineStage
	{
		/// <summary>
		/// Gets a value indicating whether the stage is the default stage that was created by the logging subsystem at start.
		/// </summary>
		bool IsDefaultStage { get; }

		/// <summary>
		/// Gets the name of the processing pipeline stage identifying the stage throughout the entire pipeline (must be unique).
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Initializes the processing pipeline stage and all following stages.
		/// This method is called by the logging subsystem and should not be called explicitly.
		/// </summary>
		void Initialize();

		/// <summary>
		/// Gets a value indicating whether the pipeline stage is initialized, i.e. it is attached to the logging subsystem.
		/// </summary>
		bool IsInitialized { get; }

		/// <summary>
		/// Shuts the processing pipeline stage and all following stages down gracefully.
		/// This method is called by the logging subsystem and should not be called explicitly.
		/// This method must not throw exceptions.
		/// </summary>
		void Shutdown();

		/// <summary>
		/// Gets all pipeline stages following the current stage (including the current one).
		/// </summary>
		/// <param name="stages">Set to add the pipeline stages to.</param>
		void GetAllStages(HashSet<IProcessingPipelineStage> stages);

		/// <summary>
		/// Gets or sets pipeline specific settings that are backed by the log configuration.
		/// </summary>
		/// <returns>Configuration of the pipeline stage.</returns>
		IProcessingPipelineStageConfiguration Settings { get; set; }

		/// <summary>
		/// Processes that a new log level was added to the logging subsystem.
		/// </summary>
		/// <param name="level">The new log level.</param>
		void ProcessLogLevelAdded(LogLevel level);

		/// <summary>
		/// Processes that a new log writer was added to the logging subsystem.
		/// </summary>
		/// <param name="writer"></param>
		void ProcessLogWriterAdded(LogWriter writer);

		/// <summary>
		/// Processes the specified log message.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <remarks>
		/// Do not keep a reference to the passed log message object as it returns to a pool.
		/// Log message objects are re-used to reduce garbage collection pressure.
		/// </remarks>
		void ProcessMessage(LocalLogMessage message);
	}

}
