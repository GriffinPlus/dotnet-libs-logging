﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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
	/// Configuration of a group of log writers matching specific patterns.
	/// </summary>
	public sealed partial class LogWriterConfiguration
	{
		internal static readonly WildcardLogWriterPattern sDefaultPattern = new WildcardLogWriterPattern("*");
		internal string mBaseLevel = LogLevel.Note.Name;
		internal List<ILogWriterPattern> mPatterns = new List<ILogWriterPattern>();
		internal List<string> mIncludes = new List<string>();
		internal List<string> mExcludes = new List<string>();

		/// <summary>
		/// Initializes a new instance of the <see cref="LogWriterConfiguration"/> class (for internal use only).
		/// Please use <see cref="LogWriterConfigurationBuilder"/> instead.
		/// </summary>
		internal LogWriterConfiguration()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogWriterConfiguration"/> class by copying another instance.
		/// </summary>
		/// <param name="other">Instance to copy.</param>
		internal LogWriterConfiguration(LogWriterConfiguration other)
		{
			mPatterns.AddRange(other.mPatterns);       // the patterns are immutable
			mBaseLevel = other.BaseLevel;              // immutable
			IsDefault = other.IsDefault;               // immutable
			mIncludes.AddRange(other.mIncludes);       // the log levels are immutable
			mExcludes.AddRange(other.mExcludes);       // the log levels are immutable
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogWriterConfiguration"/> class.
		/// </summary>
		/// <param name="pattern">Pattern that determines for which log writers the settings apply.</param>
		/// <param name="baseLevel">Name of the log level a message must be associated with at minimum to get processed.</param>
		/// <param name="includes">Names of log levels (or aspects) that should be included in addition to the base level.</param>
		/// <param name="excludes">Names of log levels (or aspects) that should be excluded although covered by the base level.</param>
		internal LogWriterConfiguration(
			ILogWriterPattern pattern,
			string baseLevel,
			IEnumerable<string> includes = null,
			IEnumerable<string> excludes = null)
		{
			if (string.IsNullOrWhiteSpace(baseLevel)) throw new ArgumentException("The base level must not be null or whitespace only.", nameof(baseLevel));

			mPatterns.Add(pattern ?? throw new ArgumentNullException(nameof(pattern)));
			mBaseLevel = baseLevel;

			if (includes != null)
			{
				foreach (var level in includes)
				{
					if (string.IsNullOrWhiteSpace(level)) {
						throw new ArgumentException("The include list contains an invalid log level.");
					}

					mIncludes.Add(level.Trim());
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

					mExcludes.Add(level.Trim());
				}
			}
		}

		/// <summary>
		/// Creates a log writer configuration matching exactly the specified log writer name.
		/// </summary>
		/// <param name="name">Name of the log writer to match.</param>
		/// <param name="baseLevel">Base level to use.</param>
		/// <param name="includes">Log levels (or aspects) to include in addition to those already enabled by the base level (may be null).</param>
		/// <param name="excludes">Log levels (or aspects) to exclude although covered by the base level (may be null).</param>
		/// <returns>The created log writer configuration.</returns>
		internal static LogWriterConfiguration FromName(
			string name,
			string baseLevel = "Note",
			IEnumerable<string> includes = null,
			IEnumerable<string> excludes = null)
		{
			return new LogWriterConfiguration(
				new ExactNameLogWriterPattern(name),
				baseLevel,
				includes,
				excludes);
		}

		/// <summary>
		/// Creates a log writer configuration for log writer names matching the specified wildcard pattern.
		/// </summary>
		/// <param name="pattern">Wildcard pattern matching log writer names.</param>
		/// <param name="baseLevel">Base level to use.</param>
		/// <param name="includes">Log levels (or aspects) to include in addition to those already enabled by the base level (may be null).</param>
		/// <param name="excludes">Log levels (or aspects) to exclude although covered by the base level (may be null).</param>
		/// <returns>The created log writer configuration.</returns>
		internal static LogWriterConfiguration FromWildcardPattern(
			string pattern,
			string baseLevel = "Note",
			IEnumerable<string> includes = null,
			IEnumerable<string> excludes = null)
		{
			return new LogWriterConfiguration(
				new WildcardLogWriterPattern(pattern),
				baseLevel,
				includes,
				excludes);
		}

		/// <summary>
		/// Creates a log writer configuration for log writer names matching the specified regex pattern.
		/// </summary>
		/// <param name="regex">Regex matching log writer names.</param>
		/// <param name="baseLevel">Base level to use.</param>
		/// <param name="includes">Log levels (or aspects) to include in addition to those already enabled by the base level (may be null).</param>
		/// <param name="excludes">Log levels (or aspects) to exclude although covered by the base level (may be null).</param>
		/// <returns>The created log writer configuration.</returns>
		internal static LogWriterConfiguration FromRegexPattern(
			string regex,
			string baseLevel = "Note",
			IEnumerable<string> includes = null,
			IEnumerable<string> excludes = null)
		{
			return new LogWriterConfiguration(
				new RegexLogWriterPattern(regex),
				baseLevel,
				includes,
				excludes);
		}

		/// <summary>
		/// Gets a log writer configuration covering the default log writer that matches all log writer names using base log level 'Note'
		/// (enabling log levels 'Failure', 'Error', 'Warning', 'Note').
		/// </summary>
		public static LogWriterConfiguration Default => FromWildcardPattern("*", LogLevel.Note);

		/// <summary>
		/// Gets a log writer configuration covering the default log writer 'Timing' writing using log level 'Note'.
		/// </summary>
		public static LogWriterConfiguration TimingWriter => FromName("Timing", LogLevel.None, new string[] { LogLevel.Timing });

		/// <summary>
		/// Gets the list of patterns used to match the name of log writers the configuration should apply to.
		/// </summary>
		public IEnumerable<ILogWriterPattern> Patterns => mPatterns;

		/// <summary>
		/// Gets the log level a message must be associated with at minimum to get processed.
		/// </summary>
		public string BaseLevel => mBaseLevel;

		/// <summary>
		/// Get the list of names of log levels (or aspects) to include in addition to those already enabled
		/// via <see cref="BaseLevel"/>.
		/// </summary>
		public IEnumerable<string> Includes => mIncludes;

		/// <summary>
		/// Get the list of names of log levels (or aspects) to exclude although covered by <see cref="BaseLevel"/>.
		/// </summary>
		public IEnumerable<string> Excludes => mExcludes;

		/// <summary>
		/// Gets or sets a value indicating whether this configuration is a default configuration.
		/// </summary>
		internal bool IsDefault { get; set; }

		/// <summary>
		/// Gets the hash code of the object.
		/// </summary>
		/// <returns>Hash code of the object.</returns>
		public override int GetHashCode()
		{
			// TODO: implement properly
			return base.GetHashCode();
		}

		/// <summary>
		/// Checks whether the specified object equals the current one.
		/// </summary>
		/// <param name="obj">Object to compare with.</param>
		/// <returns>true, if the specified object equals the current one; otherwise false.</returns>
		public override bool Equals(object obj)
		{
			if (obj is LogWriterConfiguration other)
			{
				if (!Patterns.SequenceEqual(other.Patterns)) return false;
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