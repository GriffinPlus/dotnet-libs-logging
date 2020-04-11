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
	public partial class LogConfiguration
	{
		/// <summary>
		/// Configuration of a group of log writers matching a certain pattern.
		/// </summary>
		public class LogWriter
		{
			private static readonly WildcardLogWriterPattern sDefaultPattern = new WildcardLogWriterPattern("*");
			private ILogWriterPattern mPattern = sDefaultPattern;
			private string mBaseLevel = LogLevel.Note.Name;

			/// <summary>
			/// Initializes a new instance of the <see cref="LogWriter"/> class.
			/// </summary>
			protected internal LogWriter()
			{

			}

			/// <summary>
			/// Initializes a new instance of the <see cref="LogWriter"/> class by copying another instance.
			/// </summary>
			/// <param name="other">Instance to copy.</param>
			protected internal LogWriter(LogWriter other)
			{
				mPattern = other.mPattern;          // immutable
				BaseLevel = other.BaseLevel;        // immutable
				IsDefault = other.IsDefault;        // immutable
				Includes.AddRange(other.Includes);
				Excludes.AddRange(other.Excludes);
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="LogWriter"/> class.
			/// </summary>
			/// <param name="pattern">Pattern that determines for which log writers the settings apply.</param>
			/// <param name="baseLevel">Name of the log level a message must be associated with at minimum to get processed.</param>
			/// <param name="includes">Names of log levels (or aspects) that should be included in addition to the base level.</param>
			/// <param name="excludes">Names of log levels (or aspects) that should be excluded although covered by the base level.</param>
			protected internal LogWriter(
				ILogWriterPattern pattern,
				string baseLevel,
				IEnumerable<string> includes = null,
				IEnumerable<string> excludes = null)
			{
				if (string.IsNullOrWhiteSpace(baseLevel)) throw new ArgumentException("The base level must not be null or whitespace only.", nameof(baseLevel));

				mPattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
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
			protected internal ILogWriterPattern Pattern
			{
				get => mPattern;
				set => mPattern = value ?? throw new ArgumentNullException(nameof(value));
			}

			/// <summary>
			/// Gets or sets the log level a message must be associated with at minimum to get processed.
			/// </summary>
			public string BaseLevel
			{
				get => mBaseLevel;
				set => mBaseLevel = value ?? throw new ArgumentNullException(nameof(value));
			}

			/// <summary>
			/// Get the list of names of log levels (or aspects) to include in addition to those already enabled
			/// via <see cref="BaseLevel"/>.
			/// </summary>
			public List<string> Includes { get; } = new List<string>();

			/// <summary>
			/// Get the list of names of log levels (or aspects) to exclude although covered by <see cref="BaseLevel"/>.
			/// </summary>
			public List<string> Excludes { get; } = new List<string>();

			/// <summary>
			/// Gets or sets a value indicating whether this configuration is a default configuration.
			/// </summary>
			internal bool IsDefault { get; set; }

			/// <summary>
			/// Checks whether the specified object equals the current one.
			/// </summary>
			/// <param name="obj">Object to compare with.</param>
			/// <returns>true, if the specified object equals the current one; otherwise false.</returns>
			public override bool Equals(object obj)
			{
				if (obj is LogWriter other)
				{
					if (mPattern != other.mPattern) return false;
					if (BaseLevel != other.BaseLevel) return false;
					if (IsDefault != other.IsDefault) return false;
					if (!Includes.SequenceEqual(other.Includes)) return false;
					if (!Excludes.SequenceEqual(other.Excludes)) return false;
					return true;
				}

				return false;
			}
		}
	}
}
