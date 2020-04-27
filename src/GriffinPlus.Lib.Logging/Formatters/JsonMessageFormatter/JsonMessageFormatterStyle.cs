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
using System.Text;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Formatting styles for JSON output.
	/// </summary>
	public enum JsonMessageFormatterStyle
	{
		/// <summary>
		/// Entire JSON document on a single line without any non-significant whitespaces (best space efficiency).
		/// Example: {'A':1,'B':2}
		/// </summary>
		Compact,

		/// <summary>
		/// Entire JSON document on a single line with some whitespaces (improved readability).
		/// Example: { 'A' : 1, 'B' : 2 }
		/// </summary>
		OneLine,

		/// <summary>
		/// Entire JSON document on multiple lines with indentation for nested objects (best readability).
		/// Example:
		/// {
		///   'A' : 1,
		///   'B' : 2
		/// }
		/// </summary>
		Beautified
	}
}
