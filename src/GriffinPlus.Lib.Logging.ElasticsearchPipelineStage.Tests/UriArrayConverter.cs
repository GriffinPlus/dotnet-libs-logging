///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Globalization;
using System.Linq;

namespace GriffinPlus.Lib.Logging.Elasticsearch.Tests;

/// <summary>
/// Converts arrays of <see cref="Uri"/> to and from the string representation used in the log configuration.
/// </summary>
static class UriArrayConverter
{
	/// <summary>
	/// Converts an array of <see cref="Uri"/> to a string as used in the configuration.
	/// </summary>
	/// <param name="uris">Array of <see cref="Uri"/> to convert to a string.</param>
	/// <param name="provider">Format provider to use (<see langword="null"/> to use <see cref="CultureInfo.InvariantCulture"/>).</param>
	/// <returns>The formatted array of <see cref="Uri"/>.</returns>
	public static string Format(Uri[] uris, IFormatProvider provider = null)
	{
		return uris != null ? string.Join("; ", uris.Select(x => x.ToString())) : "";
	}

	/// <summary>
	/// Converts a string to an array of <see cref="Uri"/>.
	/// The string is expected to contain the uris separated by semicolons.
	/// </summary>
	/// <param name="s">String to convert to an array of <see cref="Uri"/>.</param>
	/// <param name="provider">Format provider to use (<see langword="null"/> to use <see cref="CultureInfo.InvariantCulture"/>).</param>
	/// <returns>An array of <see cref="Uri"/> corresponding to the specified string.</returns>
	public static Uri[] Parse(string s, IFormatProvider provider = null)
	{
		return (
			       from endpointToken in s.Trim().Split(';')
			       select endpointToken.Trim() into apiEndpointString
			       where apiEndpointString.Length > 0
			       select new Uri(apiEndpointString)).ToArray();
	}
}
