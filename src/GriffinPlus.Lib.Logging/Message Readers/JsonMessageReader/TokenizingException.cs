///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Exception that is thrown when tokenizing a document fails due to invalid data.
/// </summary>
public class TokenizingException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TokenizingException"/> class.
	/// </summary>
	/// <param name="lineNumber">Line in the document where the tokenizing error occurred (starts at 1).</param>
	/// <param name="position">Position in the document where the tokenizing error occurred (starts at 1).</param>
	/// <param name="message">Message describing the reason why the exception is thrown.</param>
	public TokenizingException(int lineNumber, int position, string message) : base(message)
	{
		LineNumber = lineNumber;
		Position = position;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TokenizingException"/> class.
	/// </summary>
	/// <param name="message">Message describing the reason why the exception is thrown.</param>
	/// <param name="lineNumber">Line in the document where the tokenizing error occurred (starts at 1).</param>
	/// <param name="position">Position in the document where the tokenizing error occurred (starts at 1).</param>
	/// <param name="innerException">The original exception that led to the exception being thrown.</param>
	public TokenizingException(
		int       lineNumber,
		int       position,
		string    message,
		Exception innerException) : base(message, innerException)
	{
		LineNumber = lineNumber;
		Position = position;
	}

	/// <summary>
	/// Gets the number of the line where the tokenizing error occurred (starts at 1).
	/// </summary>
	public int LineNumber { get; }

	/// <summary>
	/// Gets the character in the line where the tokenizing error occurred (starts at 1).
	/// </summary>
	public int Position { get; }
}
