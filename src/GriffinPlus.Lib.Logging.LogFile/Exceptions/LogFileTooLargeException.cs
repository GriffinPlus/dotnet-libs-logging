///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Exception that is thrown, if the log file is too large to get handled as a regular collection that supports
	/// up to <seealso cref="int.MaxValue"/> entries.
	/// </summary>
	public class LogFileTooLargeException : LogFileException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LogFileTooLargeException"/> class.
		/// </summary>
		public LogFileTooLargeException()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogFileTooLargeException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		public LogFileTooLargeException(string message) : base(message)
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogFileTooLargeException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		/// <param name="innerException">The original exception that led to the exception being thrown.</param>
		public LogFileTooLargeException(string message, Exception innerException) : base(message, innerException)
		{

		}
	}
}
