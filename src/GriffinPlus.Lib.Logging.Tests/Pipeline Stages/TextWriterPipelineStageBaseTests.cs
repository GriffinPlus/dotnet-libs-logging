///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

using Xunit;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Unit tests targeting the <see cref="TextWriterPipelineStage"/> class as a base class for derived pipeline stages.
/// </summary>
public abstract class TextWriterPipelineStageBaseTests<TStage> : AsyncProcessingPipelineStageBaseTests<TStage>
	where TStage : TextWriterPipelineStage, new()
{
	/// <summary>
	/// Tests whether creating a new stage succeeds and the stage is in the expected state
	/// (as far as the state of the base class is concerned).
	/// </summary>
	public override void Create_And_Check_BaseClass_State()
	{
		// run base class creation test
		base.Create_And_Check_BaseClass_State();

		// check state introduced with the TextWriterPipelineStage class
		var stage = ProcessingPipelineStage.Create<TStage>("Stage", null);
		Assert.NotNull(stage.Formatter);
	}

	/// <summary>
	/// Tests whether setting the formatter using <see cref="TextWriterPipelineStage.Formatter"/> succeeds.
	/// </summary>
	[Fact]
	public void Formatter_SetSuccessfully()
	{
		var stage = ProcessingPipelineStage.Create<TStage>("Stage", null);
		var formatter = new TestFormatter();
		stage.Formatter = formatter;
		Assert.Same(formatter, stage.Formatter);
	}

	/// <summary>
	/// Tests whether setting the formatter using <see cref="TextWriterPipelineStage.Formatter"/> throws an exception,
	/// if a null reference is specified.
	/// </summary>
	[Fact]
	public void Formatter_FailsIfNull()
	{
		var stage = ProcessingPipelineStage.Create<TStage>("Stage", null);
		Assert.Throws<ArgumentNullException>(() => stage.Formatter = null);
	}

	/// <summary>
	/// Tests whether setting the formatter using <see cref="TextWriterPipelineStage.Formatter"/> throws an exception,
	/// if the pipeline stage is already initialized (attached to the logging subsystem).
	/// </summary>
	[Fact]
	public void Formatter_FailsIfInitialized()
	{
		var stage = ProcessingPipelineStage.Create<TStage>("Stage", null);
		var formatter = new TestFormatter();
		stage.Initialize();
		Assert.Throws<InvalidOperationException>(() => stage.Formatter = formatter);
		stage.Shutdown();
	}
}
