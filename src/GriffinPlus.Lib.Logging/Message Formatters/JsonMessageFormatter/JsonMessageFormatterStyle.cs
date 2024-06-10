///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging;

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
	/// 'A' : 1,
	/// 'B' : 2
	/// }
	/// </summary>
	Beautified
}
