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
	/// Interface of a log configuration (must be implemented thread-safe).
	/// </summary>
	public interface ILogConfiguration
	{
		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		string ApplicationName { get; set; }

		/// <summary>
		/// Gets the configuration of the processing pipeline.
		/// </summary>
		IProcessingPipelineConfiguration ProcessingPipeline { get; }

		/// <summary>
		/// Gets a bit mask in which each bit is associated with a log level with the same id
		/// and expresses whether the corresponding log level is active for the specified writer.
		/// </summary>
		/// <param name="writer">Log writer to get the active log level mask for.</param>
		/// <returns>The requested active log level mask.</returns>
		LogLevelBitMask GetActiveLogLevelMask(LogWriter writer);

		/// <summary>
		/// Saves the configuration.
		/// </summary>
		/// <param name="includeDefaults">
		/// true to include the default value of settings that have not been explicitly set;
		/// false to save only settings that have not been explicitly set.
		/// </param>
		void Save(bool includeDefaults = false);
	}

}
