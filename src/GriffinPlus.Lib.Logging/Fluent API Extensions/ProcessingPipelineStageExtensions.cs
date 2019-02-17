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
	/// Fluent API extension methods for the <see cref="ProcessingPipelineStage{STAGE}"/> class.
	/// </summary>
	public static class ProcessingPipelineStageExtensions
	{
		/// <summary>
		/// Links the specified pipeline stages to the current stage.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <param name="nextStages">Pipeline stages to pass log messages to, when the current stage has completed.</param>
		/// <returns>The updated pipeline stage.</returns>
		public static STAGE FollowedBy<STAGE>(this STAGE @this, params IProcessingPipelineStage[] nextStages) where STAGE: ProcessingPipelineStage<STAGE>
		{
			@this.NextStages = nextStages;
			return @this;
		}
	}
}
