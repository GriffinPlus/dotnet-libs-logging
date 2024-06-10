///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Text.RegularExpressions;

namespace GriffinPlus.Lib.Logging;

public partial class LogWriterConfiguration
{
	/// <summary>
	/// A log writer pattern taking a wildcard pattern string (immutable).
	/// </summary>
	public class WildcardNamePattern : INamePattern
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="WildcardNamePattern"/> class.
		/// </summary>
		/// <param name="pattern">The wildcard pattern to use.</param>
		public WildcardNamePattern(string pattern)
		{
			Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
			Regex = RegexHelpers.FromWildcardExpression(pattern);
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
			return "Wildcard: " + Pattern;
		}
	}
}
