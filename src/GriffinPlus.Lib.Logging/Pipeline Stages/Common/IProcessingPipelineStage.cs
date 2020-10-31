///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

// ReSharper disable UnusedMemberInSuper.Global

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
		/// Gets or sets processing pipeline stages that are called after the current stage has completed processing.
		/// </summary>
		IProcessingPipelineStage[] NextStages { get; }

		/// <summary>
		/// Configures the specified pipeline stage to receive log messages, when the current stage has completed running
		/// its <see cref="IProcessingPipelineStage.ProcessMessage"/> method. The method must return <c>true</c> to call the following stage.
		/// </summary>
		/// <param name="stage">The pipeline stage that should follow the current stage.</param>
		void AddNextStage(IProcessingPipelineStage stage);

		/// <summary>
		/// Removes the specified pipeline stage from the list of following pipeline stages.
		/// </summary>
		/// <param name="stage">Pipeline stage to remove.</param>
		/// <returns>
		/// true, if the specified pipeline stage was removed successfully;
		/// false, if the specified pipeline stage is not one of the following pipeline stages of the current stage.
		/// </returns>
		bool RemoveNextStage(IProcessingPipelineStage stage);

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
