///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// A builder for log message processing pipelines.
/// </summary>
public class ProcessingPipelineBuilder
{
	private readonly ILogConfiguration     mConfiguration;
	private          SplitterPipelineStage mSplitter;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProcessingPipelineBuilder"/> class.
	/// </summary>
	/// <param name="configuration">Log configuration the added pipeline stages should use.</param>
	public ProcessingPipelineBuilder(ILogConfiguration configuration)
	{
		mConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
	}

	/// <summary>
	/// Gets the created pipeline stage (may be <see langword="null"/>).
	/// </summary>
	public ProcessingPipelineStage PipelineStage { get; private set; }

	/// <summary>
	/// Adds the specified pipeline stage to the processing pipeline.
	/// If another pipeline stage was added before, a splitter is inserted to run both pipeline stages in parallel.
	/// </summary>
	/// <typeparam name="TPipelineStage">Type of the pipeline stage to add.</typeparam>
	/// <param name="name">Name of the pipeline stage to add.</param>
	/// <param name="initializer">Initializer that configures the pipeline stage.</param>
	/// <returns>The added pipeline stage.</returns>
	public TPipelineStage Add<TPipelineStage>(
		string                                             name,
		ProcessingPipelineStageInitializer<TPipelineStage> initializer)
		where TPipelineStage : ProcessingPipelineStage, new()
	{
		// create and configure the pipeline stage
		var stage = ProcessingPipelineStage.Create<TPipelineStage>(name, mConfiguration);
		initializer?.Invoke(stage);

		// link pipeline stage with previously added stages, if necessary
		if (PipelineStage != null)
		{
			if (ReferenceEquals(PipelineStage, mSplitter))
			{
				mSplitter.AddNextStage(stage);
			}
			else
			{
				mSplitter = ProcessingPipelineStage.Create<SplitterPipelineStage>("Splitter (automatically injected)", mConfiguration);
				mSplitter.AddNextStage(PipelineStage);
				mSplitter.AddNextStage(stage);
				PipelineStage = mSplitter;
			}
		}
		else
		{
			PipelineStage = stage;
		}

		return stage;
	}
}

/// <summary>
/// A method that parameterizes the processing pipeline stage when initializing the logging subsystem.
/// </summary>
/// <typeparam name="TPipelineStage">Type of the configuration to parameterize.</typeparam>
/// <param name="stage">The stage to parameterize.</param>
public delegate void ProcessingPipelineStageInitializer<in TPipelineStage>(TPipelineStage stage)
	where TPipelineStage : ProcessingPipelineStage, new();

/// <summary>
/// A method that builds a processing pipeline when initializing the logging subsystem.
/// </summary>
/// <param name="builder">Pipeline builder to use when setting up the processing pipeline.</param>
public delegate void ProcessingPipelineInitializer(ProcessingPipelineBuilder builder);
