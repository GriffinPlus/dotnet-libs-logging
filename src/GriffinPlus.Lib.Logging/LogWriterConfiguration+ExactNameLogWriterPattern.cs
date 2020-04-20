﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

using System.Text.RegularExpressions;

namespace GriffinPlus.Lib.Logging
{
	public partial class LogWriterConfiguration
	{
		/// <summary>
		/// A log writer pattern matching exactly (immutable).
		/// </summary>
		public class ExactNameLogWriterPattern : ILogWriterPattern
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="ExactNameLogWriterPattern"/> class.
			/// </summary>
			/// <param name="name">The name of the log writer to match.</param>
			public ExactNameLogWriterPattern(string name)
			{
				Pattern = name;
				var regex = $"^{Regex.Escape(name)}$";
				Regex = new Regex(regex, RegexOptions.Singleline); // compilation is not needed as the regex matches only once against a log writer name and is then cached
			}

			/// <summary>
			/// Gets the original pattern.
			/// </summary>
			public string Pattern { get; }

			/// <summary>
			/// Gets the regular expression matching the specified pattern.
			/// </summary>
			public Regex Regex { get; }

			/// <summary>
			/// Gets the string representation of the pattern.
			/// </summary>
			/// <returns>The string representation of the pattern.</returns>
			public override string ToString()
			{
				return "Exact: " + Pattern;
			}
		}
	}
}

