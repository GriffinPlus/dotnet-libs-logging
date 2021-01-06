///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Base class for exceptions concerning the <see cref="LogFile"/> class.
	/// </summary>
	public class LogFileException : LoggingException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LogFileException"/> class.
		/// </summary>
		public LogFileException()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogFileException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		public LogFileException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogFileException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		/// <param name="innerException">The original exception that led to the exception being thrown.</param>
		public LogFileException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}

}
