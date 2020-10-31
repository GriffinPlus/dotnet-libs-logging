///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{
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
		public TokenizingException(int lineNumber, int position, string message, Exception innerException) : base(message, innerException)
		{
			LineNumber = lineNumber;
			Position = position;
		}

		/// <summary>
		/// Gets the number of the line where the tokenizing error occurred (starts at 1).
		/// </summary>
		public int LineNumber { get; }

		/// <summary>
		/// Gets the position in the line where the tokenizing error occurred (starts at 1).
		/// </summary>
		public int Position { get; }
	}
}
