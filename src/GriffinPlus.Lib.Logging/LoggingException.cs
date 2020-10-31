///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Exception that is thrown when something is wrong in the logging subsystem.
	/// </summary>
	public class LoggingException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LoggingException"/> class.
		/// </summary>
		public LoggingException()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LoggingException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		public LoggingException(string message) : base(message)
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LoggingException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		/// <param name="innerException">The original exception that led to the exception being thrown.</param>
		public LoggingException(string message, Exception innerException) : base(message, innerException)
		{

		}
	}
}
