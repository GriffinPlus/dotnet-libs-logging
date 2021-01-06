///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// JSON tokens emitted by the <see cref="JsonTokenizer"/>.
	/// </summary>
	enum JsonTokenType
	{
		/// <summary>
		/// The '{' token.
		/// </summary>
		LBracket,

		/// <summary>
		/// The '}' token.
		/// </summary>
		RBracket,

		/// <summary>
		/// The '[' token.
		/// </summary>
		LSquareBracket,

		/// <summary>
		/// The ']' token.
		/// </summary>
		RSquareBracket,

		/// <summary>
		/// The ':' token.
		/// </summary>
		Colon,

		/// <summary>
		/// The ',' token.
		/// </summary>
		Comma,

		/// <summary>
		/// The string token.
		/// </summary>
		String,

		/// <summary>
		/// The number token.
		/// </summary>
		Number,

		/// <summary>
		/// A boolean value token.
		/// </summary>
		Boolean,

		/// <summary>
		/// The null token.
		/// </summary>
		Null,

		/// <summary>
		/// Identifiers are not supported by JSON, but this token type comes in handy when
		/// reading boolean values ('true, 'false') or JSON Null ('null'). The actual meaning
		/// is determined at the parsing stage. This type should not be visible outside the
		/// tokenizer as identifiers are translated to other, better fitting token types.
		/// </summary>
		Identifier
	}

}
