///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Exception that is thrown, if the log file does not have the expected format
	/// (most probably the file is a sqlite database file, but not a log file).
	/// </summary>
	public class InvalidLogFileFormatException : LogFileException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidLogFileFormatException"/> class.
		/// </summary>
		public InvalidLogFileFormatException()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidLogFileFormatException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		public InvalidLogFileFormatException(string message) : base(message)
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidLogFileFormatException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		/// <param name="innerException">The original exception that led to the exception being thrown.</param>
		public InvalidLogFileFormatException(string message, Exception innerException) : base(message, innerException)
		{

		}
	}
}
