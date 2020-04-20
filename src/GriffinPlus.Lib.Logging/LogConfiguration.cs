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

using System;
using System.Collections.Generic;
using System.Linq;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// The base class for log configurations.
	/// </summary>
	public abstract partial class LogConfiguration : ILogConfiguration
	{
		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		public abstract string ApplicationName { get; set; }

		/// <summary>
		/// Gets a bit mask in which each bit is associated with a log level with the same id
		/// and expresses whether the corresponding log level is active for the specified writer.
		/// </summary>
		/// <param name="writer">Log writer to get the active log level mask for.</param>
		/// <returns>The requested active log level mask.</returns>
		public abstract LogLevelBitMask GetActiveLogLevelMask(LogWriter writer);

		/// <summary>
		/// Gets the current log writer settings.
		/// </summary>
		/// <returns>A copy of the internal log writer settings.</returns>
		public abstract IEnumerable<LogWriterConfiguration> GetLogWriterSettings();

		/// <summary>
		/// Sets the log writer settings to use.
		/// </summary>
		/// <param name="settings">Settings to use.</param>
		public abstract void SetLogWriterSettings(IEnumerable<LogWriterConfiguration> settings);

		/// <summary>
		/// Sets the log writer settings to use.
		/// </summary>
		/// <param name="settings">Settings to use.</param>
		public abstract void SetLogWriterSettings(params LogWriterConfiguration[] settings);

		/// <summary>
		/// Gets the settings for pipeline stages by their name.
		/// </summary>
		/// <returns>The requested settings.</returns>
		public abstract IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetProcessingPipelineStageSettings();

		/// <summary>
		/// Gets the settings for the pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the pipeline stage to get the settings for.</param>
		/// <returns>
		/// The requested settings;
		/// null, if the settings do not exist.
		/// </returns>
		public abstract IReadOnlyDictionary<string, string> GetProcessingPipelineStageSettings(string name);

		/// <summary>
		/// Sets the settings for the pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the pipeline stage to set the settings for.</param>
		/// <param name="settings">Settings to set.</param>
		public abstract void SetProcessingPipelineStageSettings(string name, IReadOnlyDictionary<string, string> settings);

	}
}
