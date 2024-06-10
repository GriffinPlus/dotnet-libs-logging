///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Exception that is thrown when there is a problem communicating with the local log service.
/// </summary>
public class LocalLogServiceCommunicationException : LoggingException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LocalLogServiceCommunicationException"/> class.
	/// </summary>
	public LocalLogServiceCommunicationException() { }

	/// <summary>
	/// Initializes a new instance of the <see cref="LocalLogServiceCommunicationException"/> class.
	/// </summary>
	/// <param name="message">Message describing the reason why the exception is thrown.</param>
	public LocalLogServiceCommunicationException(string message) : base(message) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="LocalLogServiceCommunicationException"/> class.
	/// </summary>
	/// <param name="message">Message describing the reason why the exception is thrown.</param>
	/// <param name="innerException">The original exception that led to the exception being thrown.</param>
	public LocalLogServiceCommunicationException(string message, Exception innerException) : base(message, innerException) { }
}
