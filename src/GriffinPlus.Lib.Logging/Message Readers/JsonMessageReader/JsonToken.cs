///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

// ReSharper disable NonReadonlyMemberInGetHashCode

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// JSON tokens emitted by the <see cref="JsonTokenizer"/>.
	/// </summary>
	internal struct JsonToken : IEquatable<JsonToken>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="JsonToken"/> struct.
		/// </summary>
		/// <param name="type">The token type.</param>
		/// <param name="lineNumber">Number of the line at which the token starts (starts at 1).</param>
		/// <param name="position">Position in the line at which the token starts (starts at 1).</param>
		public JsonToken(JsonTokenType type, int lineNumber = -1, int position = -1)
		{
			Type = type;
			Token = null;
			LineNumber = lineNumber;
			Position = position;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="JsonToken"/> struct.
		/// </summary>
		/// <param name="type">The token type.</param>
		/// <param name="token">The token itself (as extracted from the JSON document).</param>
		/// <param name="lineNumber">Number of the line at which the token starts (starts at 1).</param>
		/// <param name="position">Position in the line at which the token starts (starts at 1).</param>
		public JsonToken(JsonTokenType type, string token, int lineNumber = -1, int position = -1)
		{
			Type = type;
			Token = token;
			LineNumber = lineNumber;
			Position = position;
		}

		/// <summary>
		/// The token type.
		/// </summary>
		public JsonTokenType Type;

		/// <summary>
		/// The token itself (as extracted from the JSON document).
		/// </summary>
		public string Token;

		/// <summary>
		/// Gets the number of the line at which the token starts (starts at 1).
		/// </summary>
		public int LineNumber;

		/// <summary>
		/// Gets the position in the line at which the the token starts (starts at 1).
		/// </summary>
		public int Position;

		/// <summary>
		/// Checks whether the current token equals the specified one.
		/// </summary>
		/// <param name="other">Token to compare with.</param>
		/// <returns>
		/// true, if the current token equals the specified one;
		/// otherwise false.
		/// </returns>
		public bool Equals(JsonToken other)
		{
			return Type == other.Type && Token == other.Token;
		}

		/// <summary>
		/// Checks whether the current token equals the specified one.
		/// </summary>
		/// <param name="obj">Token to compare with.</param>
		/// <returns>
		/// true, if the current token equals the specified one;
		/// otherwise false.
		/// </returns>
		public override bool Equals(object obj)
		{
			return obj is JsonToken other && Equals(other);
		}

		/// <summary>
		/// Gets the hash code of the token.
		/// </summary>
		/// <returns>Hash code of the token.</returns>
		public override int GetHashCode()
		{
			unchecked
			{
				return ((int) Type * 397) ^ (Token != null ? Token.GetHashCode() : 0);
			}
		}

		/// <summary>
		/// Gets the string representation of the token.
		/// </summary>
		/// <returns>String representation of the token.</returns>
		public override string ToString()
		{
			return $"{Type} : {Token}";
		}
	}
}
