///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable InconsistentNaming
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Configuration of a group of log writers matching specific patterns.
/// </summary>
public sealed partial class LogWriterConfiguration
{
	internal static readonly WildcardNamePattern DefaultPattern = new("*");
	internal static readonly INamePattern[]      NoPatterns     = Array.Empty<INamePattern>();
	internal                 string              mBaseLevel     = LogLevel.Notice.Name;
	internal readonly        List<INamePattern>  mNamePatterns  = new();
	internal readonly        List<INamePattern>  mTagPatterns   = new();
	internal readonly        List<string>        mIncludes      = new();
	internal readonly        List<string>        mExcludes      = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="LogWriterConfiguration"/> class (for internal use only).
	/// Please use <see cref="LogWriterConfigurationBuilder"/> instead.
	/// </summary>
	internal LogWriterConfiguration() { }

	/// <summary>
	/// Initializes a new instance of the <see cref="LogWriterConfiguration"/> class by copying another instance.
	/// </summary>
	/// <param name="other">Instance to copy.</param>
	internal LogWriterConfiguration(LogWriterConfiguration other)
	{
		mNamePatterns.AddRange(other.mNamePatterns); // the patterns are immutable
		mTagPatterns.AddRange(other.mTagPatterns);   // the patterns are immutable
		mBaseLevel = other.BaseLevel;                // immutable
		IsDefault = other.IsDefault;                 // immutable
		mIncludes.AddRange(other.mIncludes);         // the log levels are immutable
		mExcludes.AddRange(other.mExcludes);         // the log levels are immutable
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="LogWriterConfiguration"/> class.
	/// </summary>
	/// <param name="namePattern">Pattern that determines for which log writers names the settings apply.</param>
	/// <param name="tagPatterns">Pattern that determines for which message tags the settings apply.</param>
	/// <param name="baseLevel">Name of the log level a message must be associated with at minimum to get processed.</param>
	/// <param name="includes">Names of log levels (or aspects) that should be included in addition to the base level.</param>
	/// <param name="excludes">Names of log levels (or aspects) that should be excluded although covered by the base level.</param>
	internal LogWriterConfiguration(
		INamePattern              namePattern,
		IEnumerable<INamePattern> tagPatterns,
		string                    baseLevel,
		IEnumerable<string>       includes = null,
		IEnumerable<string>       excludes = null)
	{
		if (string.IsNullOrWhiteSpace(baseLevel)) throw new ArgumentException("The base level must not be null or whitespace only.", nameof(baseLevel));
		if (namePattern == null) throw new ArgumentNullException(nameof(namePattern));
		if (tagPatterns == null) throw new ArgumentNullException(nameof(tagPatterns));
		if (tagPatterns.Any(x => x == null)) throw new ArgumentException("The list of tag patterns must not contain a null reference.");

		mNamePatterns.Add(namePattern);
		mTagPatterns.AddRange(tagPatterns);
		mBaseLevel = baseLevel;

		if (includes != null)
		{
			foreach (string level in includes)
			{
				if (string.IsNullOrWhiteSpace(level))
				{
					throw new ArgumentException("The include list contains an invalid log level.");
				}

				mIncludes.Add(level.Trim());
			}
		}

		if (excludes != null)
		{
			foreach (string level in excludes)
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
	/// <param name="includes">Log levels (or aspects) to include in addition to those already enabled by the base level (may be <c>null</c>).</param>
	/// <param name="excludes">Log levels (or aspects) to exclude although covered by the base level (may be <c>null</c>).</param>
	/// <returns>The created log writer configuration.</returns>
	internal static LogWriterConfiguration FromName(
		string              name,
		string              baseLevel = "Notice",
		IEnumerable<string> includes  = null,
		IEnumerable<string> excludes  = null)
	{
		return new LogWriterConfiguration(
			new ExactNamePattern(name),
			NoPatterns,
			baseLevel,
			includes,
			excludes);
	}

	/// <summary>
	/// Creates a log writer configuration for log writer names matching the specified wildcard pattern.
	/// </summary>
	/// <param name="pattern">Wildcard pattern matching log writer names.</param>
	/// <param name="baseLevel">Base level to use.</param>
	/// <param name="includes">Log levels (or aspects) to include in addition to those already enabled by the base level (may be <c>null</c>).</param>
	/// <param name="excludes">Log levels (or aspects) to exclude although covered by the base level (may be <c>null</c>).</param>
	/// <returns>The created log writer configuration.</returns>
	internal static LogWriterConfiguration FromWildcardPattern(
		string              pattern,
		string              baseLevel = "Notice",
		IEnumerable<string> includes  = null,
		IEnumerable<string> excludes  = null)
	{
		return new LogWriterConfiguration(
			new WildcardNamePattern(pattern),
			NoPatterns,
			baseLevel,
			includes,
			excludes);
	}

	/// <summary>
	/// Creates a log writer configuration for log writer names matching the specified regex pattern.
	/// </summary>
	/// <param name="regex">Regex matching log writer names.</param>
	/// <param name="baseLevel">Base level to use.</param>
	/// <param name="includes">Log levels (or aspects) to include in addition to those already enabled by the base level (may be <c>null</c>).</param>
	/// <param name="excludes">Log levels (or aspects) to exclude although covered by the base level (may be <c>null</c>).</param>
	/// <returns>The created log writer configuration.</returns>
	internal static LogWriterConfiguration FromRegexPattern(
		string              regex,
		string              baseLevel = "Notice",
		IEnumerable<string> includes  = null,
		IEnumerable<string> excludes  = null)
	{
		return new LogWriterConfiguration(
			new RegexNamePattern(regex),
			NoPatterns,
			baseLevel,
			includes,
			excludes);
	}

	/// <summary>
	/// Gets a log writer configuration covering the default log writer that matches all log writer names using base log level 'Notice'
	/// (enabling log levels 'Emergency', 'Alert', 'Critical', 'Error', 'Warning' and 'Notice').
	/// </summary>
	public static LogWriterConfiguration Default => FromWildcardPattern("*", LogLevel.Notice);

	/// <summary>
	/// Gets a log writer configuration covering the default log writer 'Timing' writing using log level 'Notice'.
	/// </summary>
	public static LogWriterConfiguration TimingWriter => FromName("Timing", LogLevel.None, new string[] { LogLevel.Timing });

	/// <summary>
	/// Gets the list of patterns used to match the name of log writers the configuration should apply to.
	/// </summary>
	public IEnumerable<INamePattern> NamePatterns => mNamePatterns;

	/// <summary>
	/// Gets the list of patterns used to match the tags of log writers the configuration should apply to.
	/// </summary>
	public IEnumerable<INamePattern> TagPatterns => mTagPatterns;

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
	/// Gets the hash code of the object
	/// (does not take the <see cref="IsDefault"/> property into account).
	/// </summary>
	/// <returns>The hash code of the configuration.</returns>
	public override int GetHashCode()
	{
		unchecked
		{
			int hashCode = mBaseLevel.GetHashCode();
			hashCode = mNamePatterns.Aggregate(hashCode, (current, pattern) => (current * 397) ^ pattern.GetHashCode());
			hashCode = mTagPatterns.Aggregate(hashCode, (current,  pattern) => (current * 397) ^ pattern.GetHashCode());
			hashCode = mIncludes.Aggregate(hashCode, (current,     include) => (current * 397) ^ include.GetHashCode());
			return mExcludes.Aggregate(hashCode, (current, exclude) => (current * 397) ^ exclude.GetHashCode());
		}
	}

	/// <summary>
	/// Checks whether the specified object equals the current one
	/// (does not take the <see cref="IsDefault"/> property into account).
	/// </summary>
	/// <param name="other">Object to compare with.</param>
	/// <returns>
	/// <c>true</c> if the specified object equals the current one;<br/>
	/// otherwise <c>false</c>.
	/// </returns>
	public bool Equals(LogWriterConfiguration other)
	{
		return mBaseLevel == other.mBaseLevel &&
		       NamePatterns.SequenceEqual(other.NamePatterns) &&
		       TagPatterns.SequenceEqual(other.TagPatterns) &&
		       Includes.SequenceEqual(other.Includes) &&
		       Excludes.SequenceEqual(other.Excludes);
	}
}
