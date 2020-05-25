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

using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Interface for pipeline stage configurations (must be implemented thread-safe).
	/// </summary>
	public interface IProcessingPipelineStageConfiguration : IReadOnlyDictionary<string, IUntypedProcessingPipelineStageSetting>
	{
		/// <summary>
		/// Gets the name of the processing pipeline stage the configuration belongs to.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Gets the setting with the specified name.
		/// </summary>
		/// <typeparam name="T">Type of the setting (can be a primitive type or string).</typeparam>
		/// <param name="name">Name of the setting.</param>
		/// <param name="defaultValue">Default value of the setting.</param>
		/// <returns>The requested setting.</returns>
		IProcessingPipelineStageSetting<T> GetSetting<T>(string name, T defaultValue);
	}

}
