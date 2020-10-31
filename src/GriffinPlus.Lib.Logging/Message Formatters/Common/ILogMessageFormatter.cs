///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Interface for log message formatters that convert a log message to a string.
	/// </summary>
	public interface ILogMessageFormatter
	{
		/// <summary>
		/// Formats the specified log message.
		/// </summary>
		/// <param name="message">Message to format.</param>
		/// <returns>The formatted log message.</returns>
		string Format(ILogMessage message);
	}

}
