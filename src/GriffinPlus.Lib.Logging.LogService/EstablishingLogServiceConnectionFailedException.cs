///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Exception that is thrown when a <see cref="LogServiceClientChannel"/> fails connecting to the log service.
	/// </summary>
	public class EstablishingLogServiceConnectionFailedException : LogServiceChannelException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="EstablishingLogServiceConnectionFailedException"/> class.
		/// </summary>
		public EstablishingLogServiceConnectionFailedException()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="EstablishingLogServiceConnectionFailedException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		public EstablishingLogServiceConnectionFailedException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="EstablishingLogServiceConnectionFailedException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		/// <param name="innerException">The original exception that led to the exception being thrown.</param>
		public EstablishingLogServiceConnectionFailedException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}

}
