///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://griffin.plus)
//
// Copyright 2018 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Text.RegularExpressions;

namespace GriffinPlus.Lib.Logging
{
	public partial class LogConfigurationFile
	{
		/// <summary>
		/// Interface of log writer pattern classes.
		/// </summary>
		public interface ILogWriterPattern
		{
			/// <summary>
			/// Gets the original pattern.
			/// </summary>
			string Pattern { get; }

			/// <summary>
			/// Gets the regular expression matching the specified pattern.
			/// </summary>
			Regex Regex { get; }
		}
	}
}
