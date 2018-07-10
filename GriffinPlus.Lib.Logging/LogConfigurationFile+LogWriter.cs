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

using System;
using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging
{
	public partial class LogConfigurationFile
	{
		/// <summary>
		/// Configuration of a group of log writers matching a certain pattern.
		/// </summary>
		public class LogWriter
		{
			private static readonly WildcardLogWriterPattern sDefaultPattern = new WildcardLogWriterPattern("*");
			private ILogWriterPattern mPattern = sDefaultPattern;

			/// <summary>
			/// Initializes a new instance of the <see cref="LogWriter"/> class.
			/// </summary>
			internal LogWriter()
			{

			}

			/// <summary>
			/// Initializes a new instance of the <see cref="LogWriter"/> class.
			/// </summary>
			/// <param name="pattern">Pattern that determines for which log writers the settings apply.</param>
			/// <param name="baseLevel">Name of the log level a message must be associated with at minimum to get processed.</param>
			/// <param name="includes">Names of log levels (or aspects) that should be included in addition to the base level.</param>
			/// <param name="excludes">Names of log levels (or aspects) that should be excluded although covered by the base level.</param>
			public LogWriter(
				ILogWriterPattern pattern,
				string baseLevel,
				IEnumerable<string> includes = null,
				IEnumerable<string> excludes = null)
			{
				if (pattern == null) throw new ArgumentNullException(nameof(pattern));
				if (string.IsNullOrWhiteSpace(baseLevel)) throw new ArgumentException("The base level must not be null or whitespace only.", nameof(baseLevel));

				mPattern = pattern;
				BaseLevel = baseLevel;

				if (includes != null)
				{
					foreach (var level in includes)
					{
						if (string.IsNullOrWhiteSpace(level)) {
							throw new ArgumentException("The include list contains an invalid log level.");
						}

						Includes.Add(level.Trim());
					}
				}

				if (excludes != null)
				{
					foreach (var level in excludes)
					{
						if (string.IsNullOrWhiteSpace(level))
						{
							throw new ArgumentException("The exclude list contains an invalid log level.");
						}

						Excludes.Add(level.Trim());
					}
				}
			}

			/// <summary>
			/// Gets or sets the pattern used to match log writers.
			/// </summary>
			public ILogWriterPattern Pattern
			{
				get
				{
					return mPattern;
				}

				set
				{
					if (value == null) throw new ArgumentNullException(nameof(value));
					mPattern = value;
				}
			}

			/// <summary>
			/// Gets or sets the log level a message must be associated with at minimum to get processed.
			/// </summary>
			public string BaseLevel { get; set; } = "Note";

			/// <summary>
			/// Get the list of names of log levels (or aspects) to include in addition to those already
			/// enabled via <see cref="BaseLevel"/>.
			/// </summary>
			public List<string> Includes { get; } = new List<string>();

			/// <summary>
			/// Get the list of names of log levels (or aspects) to exclude although enabled
			/// via <see cref="BaseLevel"/>.
			/// </summary>
			public List<string> Excludes { get; } = new List<string>();
		}
	}
}
