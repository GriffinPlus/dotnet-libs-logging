///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Exception that is thrown when a log service communication channel is not connected and a send operation is issued.
	/// </summary>
	public class LogServiceChannelNotConnectedException : LogServiceChannelException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceChannelNotConnectedException"/> class.
		/// </summary>
		public LogServiceChannelNotConnectedException()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceChannelNotConnectedException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		public LogServiceChannelNotConnectedException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceChannelNotConnectedException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		/// <param name="innerException">The original exception that led to the exception being thrown.</param>
		public LogServiceChannelNotConnectedException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}

}
