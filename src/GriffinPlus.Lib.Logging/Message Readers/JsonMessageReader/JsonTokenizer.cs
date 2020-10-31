///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Tokenizes a JSON document.
	/// </summary>
	/// <remarks>
	/// The tokenizer removes non-significant whitespaces.
	/// </remarks>
	internal class JsonTokenizer
	{
		/// <summary>
		/// Characters creating a line break.
		/// </summary>
		private static readonly string sLineSeparators =
			"\u000A" + // line feed
			"\u000B" + // vertical tab
			"\u000C" + // form feed
			"\u000D" + // carriage return (can be followed by a line feed creating a single line break!)
			"\u0085" + // next line
			"\u2028" + // line separator
			"\u2029";  // paragraph separator

		/// <summary>
		/// Characters that have a special meaning in JSON.
		/// </summary>
		private static readonly string sWhitespaceCharacters;

		/// <summary>
		/// Characters that have a special meaning in JSON.
		/// </summary>
		private static readonly string sSpecialCharacters = "{}[]:,";

		/// <summary>
		/// Characters that terminate building an identifier.
		/// </summary>
		private static readonly string sEndOfIdentifierChars;

		/// <summary>
		/// Regular expression matching a number conforming to the JSON specification.
		/// </summary>
		private static readonly Regex sNumberRegex = new Regex(@"^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$", RegexOptions.Compiled | RegexOptions.Singleline);

		enum State
		{
			Reading,
			ReadingString,
			ReadingEscapeSequence, // escape sequence within a string
			ReadingUnicodeEscapeSequence,
			ReadingIdentifier
		}

		private State mState;
		private int mLastLineNumber;
		private int mLastPosition;
		private int mStartLineNumberOfAssembledIdentifier;
		private int mStartPositionOfAssembledIdentifier;
		private int mStartLineNumberOfAssembledEscapeSequence;
		private int mStartPositionOfAssembledEscapeSequence;
		private readonly StringBuilder mTokenBuilder = new StringBuilder();
		private readonly StringBuilder mEscapeSequenceBuilder = new StringBuilder();

		/// <summary>
		/// Initializes the <seealso cref="JsonTokenizer"/> class.
		/// </summary>
		static JsonTokenizer()
		{
			// init a string with characters that should end an identifier
			// - all whitespaces
			// - characters with a special meaning in JSON
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i <= char.MaxValue; i++) {
				char c = (char) i;
				if (char.IsWhiteSpace(c)) builder.Append(c);
			}
			sWhitespaceCharacters = builder.ToString();
			sEndOfIdentifierChars = sWhitespaceCharacters + sSpecialCharacters;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="JsonTokenizer"/> class.
		/// </summary>
		public JsonTokenizer()
		{
			Reset();
		}

		/// <summary>
		/// Gets the extracted tokens.
		/// </summary>
		public Queue<JsonToken> Tokens { get; } = new Queue<JsonToken>();

		/// <summary>
		/// Gets the current line number where tokenizing stopped.
		/// </summary>
		public int CurrentLineNumber { get; private set; }

		/// <summary>
		/// Gets the current position in the line where tokenizing stopped.
		/// </summary>
		public int CurrentPosition { get; private set; }

		/// <summary>
		/// Processes the specified JSON string, extracts tokens and stores them in <see cref="Tokens"/>
		/// (the passed data may contain incomplete JSON documents and even tokens that are completed over multiple calls).
		/// </summary>
		/// <param name="data">JSON string to process.</param>
		/// <param name="complete">
		/// true, if the passed JSON string contains complete tokens;
		/// false, if tokens may be split up over multiple calls if the method (<see cref="Flush"/> must be called explicitly at the end).
		/// </param>
		/// <returns>Number of extracted tokens.</returns>
		/// <exception cref="TokenizingException">The specified data contains an invalid token.</exception>
		public int Process(string data, bool complete = true)
		{
			int tokenCountAtStart = Tokens.Count;

			for (int i = 0; i < data.Length; i++)
			{
				char c = data[i];

				// adjust line number and position, if necessary
				mLastPosition = CurrentPosition++;
				if (sLineSeparators.IndexOf(c) >= 0)
				{
					mLastLineNumber = CurrentLineNumber++;
					CurrentPosition = 0;
				}

				// handle reading a string
				if (mState == State.ReadingString)
				{
					// handle escaped characters
					if (c == '\\')
					{
						mStartLineNumberOfAssembledEscapeSequence = CurrentLineNumber;
						mStartPositionOfAssembledEscapeSequence = CurrentPosition;
						mState = State.ReadingEscapeSequence;
						continue;
					}

					// handle end of the string
					if (c == '"')
					{
						Tokens.Enqueue(new JsonToken(JsonTokenType.String, mTokenBuilder.ToString()));
						mState = State.Reading;
						continue;
					}

					mTokenBuilder.Append(c);
					continue;
				}

				// handle reading an identifier
				if (mState == State.ReadingIdentifier)
				{
					if (sEndOfIdentifierChars.IndexOf(c) >= 0)
					{
						// detected end of the identifier
						JsonToken token = new JsonToken(JsonTokenType.Identifier, mTokenBuilder.ToString());
						TranslateIdentifier(ref token);
						Tokens.Enqueue(token);
						mState = State.Reading;

						// character must be processed once again to generate the appropriate token, if necessary
						CurrentLineNumber = mLastLineNumber;
						CurrentPosition = mLastPosition;
						i--;

						continue;
					}

					mTokenBuilder.Append(c);
					continue;
				}

				if (mState == State.ReadingEscapeSequence)
				{
					switch (c)
					{
						case 'u':
						case 'U':
							mEscapeSequenceBuilder.Clear();
							mState = State.ReadingUnicodeEscapeSequence;
							continue;
						case '"':
						case '/':
						case '\\':
							mTokenBuilder.Append(c);
							mState = State.ReadingString;
							continue;
						case 't':
							mTokenBuilder.Append('\t');
							mState = State.ReadingString;
							continue;
						case 'n':
							mTokenBuilder.Append('\n');
							mState = State.ReadingString;
							continue;
						case 'r':
							mTokenBuilder.Append('\r');
							mState = State.ReadingString;
							continue;
						case 'f':
							mTokenBuilder.Append('\f');
							mState = State.ReadingString;
							continue;
						case 'b':
							mTokenBuilder.Append('\b');
							mState = State.ReadingString;
							continue;
					}

					throw new TokenizingException(
						mStartLineNumberOfAssembledIdentifier,
						mStartPositionOfAssembledIdentifier,
						$"Invalid escape sequence at ({mStartLineNumberOfAssembledEscapeSequence},{mStartPositionOfAssembledEscapeSequence}).");
				}

				if (mState == State.ReadingUnicodeEscapeSequence)
				{
					if (c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')
					{
						mEscapeSequenceBuilder.Append(c);
						if (mEscapeSequenceBuilder.Length < 4) continue; // no enough characters to interpret the code unit
						int codeUnit = int.Parse(mEscapeSequenceBuilder.ToString(), NumberStyles.HexNumber);
						mTokenBuilder.Append((char)codeUnit);
						mState = mState = State.ReadingString;
						continue;
					}

					throw new TokenizingException(
						mStartLineNumberOfAssembledIdentifier,
						mStartPositionOfAssembledIdentifier,
						$"Invalid escape sequence at ({mStartLineNumberOfAssembledEscapeSequence},{mStartPositionOfAssembledEscapeSequence}).");
				}

				// not reading a string or an identifier
				// => whitespaces are not significant and can be skipped
				if (sWhitespaceCharacters.IndexOf(data[i]) >= 0) continue;

				// handle start of a string
				if (c == '"')
				{
					mTokenBuilder.Clear();
					mState = State.ReadingString;
					continue;
				}

				// handle characters with a specific meaning in JSON
				switch (c)
				{
					case '{':
						Tokens.Enqueue(new JsonToken(JsonTokenType.LBracket));
						continue;
					case '}':
						Tokens.Enqueue(new JsonToken(JsonTokenType.RBracket));
						continue;
					case '[':
						Tokens.Enqueue(new JsonToken(JsonTokenType.LSquareBracket));
						continue;
					case ']':
						Tokens.Enqueue(new JsonToken(JsonTokenType.RSquareBracket));
						continue;
					case ':':
						Tokens.Enqueue(new JsonToken(JsonTokenType.Colon));
						continue;
					case ',':
						Tokens.Enqueue(new JsonToken(JsonTokenType.Comma));
						continue;
				}

				// not a whitespace and not a special token
				// => must be some kind of identifier
				mTokenBuilder.Clear();
				mTokenBuilder.Append(c);
				mState = State.ReadingIdentifier;
				mStartLineNumberOfAssembledIdentifier = mLastLineNumber;
				mStartPositionOfAssembledIdentifier = mLastPosition;
			}

			if (complete) Flush();
			return Tokens.Count - tokenCountAtStart;
		}

		/// <summary>
		/// Flushes the tokenizer closing open identifiers.
		/// </summary>
		/// <returns>Number of extracted tokens (can only be 0 or 1).</returns>
		/// <exception cref="TokenizingException">Missing closing quotes of opened string.</exception>
		public int Flush()
		{
			if (mState == State.ReadingString)
			{
				// missing closing quotes of the string
				throw new TokenizingException(
					mStartLineNumberOfAssembledIdentifier,
					mStartPositionOfAssembledIdentifier,
					$"Missing closing quotes of string at ({mStartLineNumberOfAssembledIdentifier},{mStartPositionOfAssembledIdentifier}).");
			}

			if (mState == State.ReadingEscapeSequence || mState == State.ReadingUnicodeEscapeSequence)
			{
				// incomplete escape sequence
				throw new TokenizingException(
					mStartLineNumberOfAssembledIdentifier,
					mStartPositionOfAssembledIdentifier,
					$"Escape sequence at ({mStartLineNumberOfAssembledEscapeSequence},{mStartPositionOfAssembledEscapeSequence}) is incomplete.");
			}

			// close and translate identifier, if necessary
			int tokenCountAtStart = Tokens.Count;
			if (mState == State.ReadingIdentifier)
			{
				JsonToken token = new JsonToken(JsonTokenType.Identifier, mTokenBuilder.ToString());
				TranslateIdentifier(ref token);
				Tokens.Enqueue(token);
			}

			return Tokens.Count - tokenCountAtStart;
		}

		/// <summary>
		/// Resets the tokenizer.
		/// </summary>
		public void Reset()
		{
			Tokens.Clear();
			mState = State.Reading;
			CurrentLineNumber = 1;
			CurrentPosition = 0;
			mLastLineNumber = -1;
			mLastPosition = -1;
			mStartLineNumberOfAssembledIdentifier = -1;
			mStartPositionOfAssembledIdentifier = -1;
			mStartLineNumberOfAssembledEscapeSequence = -1;
			mStartPositionOfAssembledEscapeSequence = -1;
		}

		/// <summary>
		/// Tries to determine whether the specified identifier token is actually a number, a boolean or a null token.
		/// </summary>
		/// <param name="token">Token to translate.</param>
		/// <exception cref="TokenizingException">The token could not be translated, because it does not seem to be a valid number, boolean or null token.</exception>
		private void TranslateIdentifier(ref JsonToken token)
		{
			// check whether the token is one of the key words
			switch (token.Token)
			{
				case "true":
				case "false":
					token.Type = JsonTokenType.Boolean;
					return;
				case "null":
					token.Type = JsonTokenType.Null;
					return;
			}

			// check whether the token is a number conforming to the JSON specification
			if (sNumberRegex.IsMatch(token.Token))
			{
				token.Type = JsonTokenType.Number;
				return;
			}

			// current position points to end of identifier
			throw new TokenizingException(
				mStartLineNumberOfAssembledIdentifier,
				mStartPositionOfAssembledIdentifier,
				$"Unrecognized token '{token.Token}' at ({mStartLineNumberOfAssembledIdentifier},{mStartPositionOfAssembledIdentifier}).");
		}
	}
}
