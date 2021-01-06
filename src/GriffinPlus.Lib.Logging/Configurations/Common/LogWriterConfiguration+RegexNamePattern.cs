///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Text.RegularExpressions;

namespace GriffinPlus.Lib.Logging
{

	public partial class LogWriterConfiguration
	{
		/// <summary>
		/// A .NET regular expression pattern (immutable).
		/// </summary>
		public class RegexNamePattern : INamePattern
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="RegexNamePattern"/> class.
			/// </summary>
			/// <param name="pattern">The regular expression to use (must start with the anchor '^' and end with '$').</param>
			/// <exception cref="FormatException">The specified pattern does not start with '^' and end with '$'.</exception>
			public RegexNamePattern(string pattern)
			{
				if (pattern == null) throw new ArgumentNullException(nameof(pattern));
				if (!pattern.StartsWith("^") || !pattern.EndsWith("$")) throw new FormatException($"The specified pattern ({pattern}) does not start with '^' and end with '$'.");
				Pattern = pattern;
				Regex = new Regex(pattern, RegexOptions.Singleline); // compilation is not needed as the regex matches only once against a log writer name and is then cached
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
				return "Regex: " + Pattern;
			}
		}
	}

}
