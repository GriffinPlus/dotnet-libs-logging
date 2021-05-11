///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Exception that is thrown when a log service communication channel cannot perform a send operation because it is not operational.
	/// </summary>
	public class LogServiceChannelNotOperationalException : LogServiceChannelException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceChannelNotOperationalException"/> class.
		/// </summary>
		public LogServiceChannelNotOperationalException()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceChannelNotOperationalException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		public LogServiceChannelNotOperationalException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceChannelNotOperationalException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		/// <param name="innerException">The original exception that led to the exception being thrown.</param>
		public LogServiceChannelNotOperationalException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}

}
