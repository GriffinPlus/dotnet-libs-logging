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
		public JsonToken(JsonTokenType type)
		{
			Type = type;
			Token = null;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="JsonToken"/> struct.
		/// </summary>
		/// <param name="type">The token type.</param>
		/// <param name="token">The token itself (as extracted from the JSON document).</param>
		public JsonToken(JsonTokenType type, string token)
		{
			Type = type;
			Token = token;
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
