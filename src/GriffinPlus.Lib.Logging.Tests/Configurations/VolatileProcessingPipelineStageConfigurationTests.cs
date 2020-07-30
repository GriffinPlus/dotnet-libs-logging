///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

using Xunit;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Unit tests targeting the <see cref="VolatileProcessingPipelineStageConfiguration"/> class.
	/// </summary>
	public class VolatileProcessingPipelineStageConfigurationTests : ProcessingPipelineStageConfigurationTests_Base<VolatileProcessingPipelineStageConfiguration>
	{
		/// <summary>
		/// Creates a new instance of the pipeline stage configuration to test.
		/// </summary>
		/// <param name="name">Name of the pipeline stage the configuration belongs to.</param>
		/// <returns>The created pipeline stage configuration.</returns>
		protected override VolatileProcessingPipelineStageConfiguration CreateConfiguration(string name)
		{
			return new VolatileProcessingPipelineStageConfiguration(name, null);
		}

		/// <summary>
		/// Tests whether invoking the constructor without a synchronization object succeeds.
		/// </summary>
		[Fact]
		public void Create_WithoutSyncObject()
		{
			var configuration = new VolatileProcessingPipelineStageConfiguration("Stage", null);
			Assert.NotNull(configuration.Sync);
		}

		/// <summary>
		/// Tests whether invoking the constructor taking a synchronization object to synchronize access to the configuration succeeds.
		/// </summary>
		[Fact]
		public void Create_WithSyncObject()
		{
			var sync = new object();
			var configuration = new VolatileProcessingPipelineStageConfiguration(null, sync);
			Assert.Same(sync, configuration.Sync);
		}

	}
}
