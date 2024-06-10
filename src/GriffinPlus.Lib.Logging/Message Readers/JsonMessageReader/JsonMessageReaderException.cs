///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Exception that is thrown when <see cref="JsonMessageReader"/> fails reading a JSON log message fails due to invalid data.
/// </summary>
public class JsonMessageReaderException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="JsonMessageReaderException"/> class.
	/// </summary>
	/// <param name="lineNumber">Line in the document where the error occurred (starts at 1).</param>
	/// <param name="position">Position in the document where the error occurred (starts at 1).</param>
	/// <param name="message">Message describing the reason why the exception is thrown.</param>
	public JsonMessageReaderException(int lineNumber, int position, string message) :
		base(message)
	{
		LineNumber = lineNumber;
		Position = position;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonMessageReaderException"/> class.
	/// </summary>
	/// <param name="lineNumber">Line in the document where the error occurred (starts at 1).</param>
	/// <param name="position">Position in the document where the error occurred (starts at 1).</param>
	/// <param name="message">Message describing the reason why the exception is thrown.</param>
	/// <param name="innerException">The original exception that led to the exception being thrown.</param>
	public JsonMessageReaderException(
		int       lineNumber,
		int       position,
		string    message,
		Exception innerException) :
		base(message, innerException)
	{
		LineNumber = lineNumber;
		Position = position;
	}

	/// <summary>
	/// Gets the number of the line where reading failed (starts at 1).
	/// </summary>
	public int LineNumber { get; }

	/// <summary>
	/// Gets the character in the line where reading failed (starts at 1).
	/// </summary>
	public int Position { get; }
}
