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

using System;

// ReSharper disable UnusedMemberInSuper.Global

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Interface for a setting in a <see cref="IProcessingPipelineStageConfiguration"/> (must be implemented thread-safe).
	/// </summary>
	/// <typeparam name="T">Type of the setting value (can be a primitive type or string).</typeparam>
	public interface IProcessingPipelineStageSetting<T>
	{
		/// <summary>
		/// Gets the name of the setting.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Gets the type of the value.
		/// </summary>
		Type ValueType { get; }

		/// <summary>
		/// Gets a value indicating whether the setting has valid value (true) or just its default value (false).
		/// </summary>
		bool HasValue { get; }

		/// <summary>
		/// Gets or sets the value of the setting.
		/// </summary>
		T Value { get; set; }

		/// <summary>
		/// Gets or sets the value of the setting as a string (for serialization purposes).
		/// </summary>
		string ValueAsString { get; set; }

		/// <summary>
		/// Gets the default value of the setting.
		/// </summary>
		T DefaultValue { get; }
	}

}
