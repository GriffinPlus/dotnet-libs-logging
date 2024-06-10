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
	/// A name pattern matching exactly (immutable).
	/// </summary>
	public class ExactNamePattern : INamePattern
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ExactNamePattern"/> class.
		/// </summary>
		/// <param name="name">The name to match.</param>
		public ExactNamePattern(string name)
		{
			Pattern = name ?? throw new ArgumentNullException(nameof(name));
			string regex = $"^{Regex.Escape(name)}$";
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
