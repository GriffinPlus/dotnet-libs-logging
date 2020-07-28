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
	/// A processing pipeline stage that splits writing a log message up and calls multiple other stages unconditionally (thread-safe).
	/// </summary>
	/// <remarks>
	/// This is basically the core functionality of the base class.
	/// </remarks>
	public class SplitterPipelineStage : ProcessingPipelineStage<SplitterPipelineStage>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SplitterPipelineStage"/> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		public SplitterPipelineStage(string name) : base(name)
		{

		}

	}
}
