﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

using System;
using Xunit;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Unit tests targeting the <see cref="TextWriterPipelineStage{STAGE}"/> class as a base class for derived pipeline stages.
	/// </summary>
	public abstract class TextWriterPipelineStageBaseTests<STAGE> : AsyncProcessingPipelineStageBaseTests<STAGE>
		where STAGE : TextWriterPipelineStage<STAGE>
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
			var stage = CreateStage("Stage");
			Assert.NotNull(stage.Formatter);
		}

		/// <summary>
		/// Tests whether setting the formatter using <see cref="TextWriterPipelineStage{STAGE}.Formatter"/> succeeds.
		/// </summary>
		[Fact]
		public void Formatter_SetSuccessfully()
		{
			var stage = CreateStage("Stage");
			var formatter = new TestFormatter();
			stage.Formatter = formatter;
			Assert.Same(formatter, stage.Formatter);
		}

		/// <summary>
		/// Tests whether setting the formatter using <see cref="TextWriterPipelineStage{STAGE}.Formatter"/> throws an exception,
		/// if a null reference is specified.
		/// </summary>
		[Fact]
		public void Formatter_FailsIfNull()
		{
			var stage = CreateStage("Stage");
			Assert.Throws<ArgumentNullException>(() => stage.Formatter = null);
		}

		/// <summary>
		/// Tests whether setting the formatter using <see cref="TextWriterPipelineStage{STAGE}.Formatter"/> throws an exception,
		/// if the pipeline stage is already initialized (attached to the logging subsystem).
		/// </summary>
		[Fact]
		public void Formatter_FailsIfInitialized()
		{
			var stage = CreateStage("Stage");
			var formatter = new TestFormatter();
			((IProcessingPipelineStage) stage).Initialize();
			Assert.Throws<InvalidOperationException>(() => stage.Formatter = formatter);
		}

	}
}
