///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Text.RegularExpressions;

namespace GriffinPlus.Lib.Logging
{

	public partial class LogWriterConfiguration
	{
		/// <summary>
		/// Interface of log writer pattern classes (must be implemented immutable).
		/// </summary>
		public interface INamePattern
		{
			/// <summary>
			/// Gets the original pattern.
			/// </summary>
			string Pattern { get; }

			/// <summary>
			/// Gets the regular expression matching the specified pattern.
			/// </summary>
			Regex Regex { get; }
		}
	}

}
