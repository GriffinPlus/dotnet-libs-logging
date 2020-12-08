///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Exception that is thrown, if the log file specifies a file version that is not supported
	/// (the file version corresponds to the internal database structure).
	/// </summary>
	public class FileVersionNotSupportedException : LogFileException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FileVersionNotSupportedException"/> class.
		/// </summary>
		public FileVersionNotSupportedException()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FileVersionNotSupportedException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		public FileVersionNotSupportedException(string message) : base(message)
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FileVersionNotSupportedException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		/// <param name="innerException">The original exception that led to the exception being thrown.</param>
		public FileVersionNotSupportedException(string message, Exception innerException) : base(message, innerException)
		{

		}
	}
}
