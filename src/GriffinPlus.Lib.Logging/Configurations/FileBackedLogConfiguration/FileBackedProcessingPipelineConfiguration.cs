///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// The processing pipeline part of the <see cref="FileBackedLogConfiguration" /> (thread-safe).
	/// </summary>
	public class FileBackedProcessingPipelineConfiguration : IProcessingPipelineConfiguration
	{
		private readonly FileBackedLogConfiguration mLogConfiguration;

		/// <summary>
		/// Initializes a new instance of the <see cref="FileBackedProcessingPipelineConfiguration"/> class.
		/// </summary>
		/// <param name="configuration">The log configuration the processing pipeline configuration belongs to.</param>
		internal FileBackedProcessingPipelineConfiguration(FileBackedLogConfiguration configuration)
		{
			mLogConfiguration = configuration;
			Stages = new FileBackedProcessingPipelineStageConfigurations(mLogConfiguration);
		}

		/// <summary>
		/// Gets the configuration of the pipeline stages.
		/// </summary>
		public FileBackedProcessingPipelineStageConfigurations Stages { get; private set; }

		/// <summary>
		/// Gets the configuration of the pipeline stages.
		/// </summary>
		IProcessingPipelineStageConfigurations IProcessingPipelineConfiguration.Stages => Stages;
	}
}
