///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Exception that is thrown when a log service communication channel cannot sent data,
	/// because there is no space left in the send queue.
	/// </summary>
	public class LogServiceChannelQueueFullException : LogServiceChannelException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceChannelQueueFullException"/> class.
		/// </summary>
		public LogServiceChannelQueueFullException()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceChannelQueueFullException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		public LogServiceChannelQueueFullException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceChannelQueueFullException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		/// <param name="innerException">The original exception that led to the exception being thrown.</param>
		public LogServiceChannelQueueFullException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}

}
