///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Xunit;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Unit tests targeting the <see cref="JsonTokenizer"/> class.
/// </summary>
public class JsonTokenizerTests
{
	internal static readonly string WhiteSpaceCharacters;

	/// <summary>
	/// Initializes the <see cref="JsonTokenizerTests"/> class.
	/// </summary>
	static JsonTokenizerTests()
	{
		var builder = new StringBuilder();
		for (int i = 0; i <= char.MaxValue; i++)
		{
			char c = (char)i;
			if (char.IsWhiteSpace(c)) builder.Append(c);
		}

		WhiteSpaceCharacters = builder.ToString();
	}

	/// <summary>
	/// Tests whether the creation of the tokenizer succeeds.
	/// </summary>
	[Fact]
	private void Create()
	{
		var tokenizer = new JsonTokenizer();
		Assert.Equal(1, tokenizer.CurrentLineNumber);
		Assert.Equal(1, tokenizer.CurrentPosition);
		Assert.Empty(tokenizer.Tokens);
	}

	#region Process()

	/// <summary>
	/// Test data for verifying that single JSON tokens (brackets, punctuation, strings,
	/// escaped characters, numbers, booleans, null, etc.) are parsed correctly,
	/// including optional surrounding whitespace.
	/// </summary>
	public static TheoryData<string, object> ProcessTestData_SingleTokens
	{
		get
		{
			var data = new TheoryData<string, object>();

			// Iterate over combinations of leading and trailing whitespace
			foreach (string preWhite in new[] { "", WhiteSpaceCharacters }) // preceding whitespaces
			{
				foreach (string trailWhite in new[] { "", WhiteSpaceCharacters }) // trailing whitespaces
				{
					// --- Structural characters ---
					data.Add(preWhite + "{" + trailWhite, new JsonToken(JsonTokenType.LBracket, "{"));
					data.Add(preWhite + "}" + trailWhite, new JsonToken(JsonTokenType.RBracket, "}"));
					data.Add(preWhite + "[" + trailWhite, new JsonToken(JsonTokenType.LSquareBracket, "["));
					data.Add(preWhite + "]" + trailWhite, new JsonToken(JsonTokenType.RSquareBracket, "]"));
					data.Add(preWhite + ":" + trailWhite, new JsonToken(JsonTokenType.Colon, ":"));
					data.Add(preWhite + "," + trailWhite, new JsonToken(JsonTokenType.Comma, ","));

					// --- Basic string ---
					data.Add(
						preWhite + "\"just a string\"" + trailWhite,
						new JsonToken(JsonTokenType.String, "just a string"));

					// --- Escaped characters inside strings ---
					data.Add(preWhite + "\"\\\"\"" + trailWhite, new JsonToken(JsonTokenType.String, "\"")); // escaped quotation mark
					data.Add(preWhite + "\"\\/\"" + trailWhite, new JsonToken(JsonTokenType.String, "/"));   // escaped solidus
					data.Add(preWhite + "\"\\\\\"" + trailWhite, new JsonToken(JsonTokenType.String, "\\")); // escaped backslash
					data.Add(preWhite + "\"\\t\"" + trailWhite, new JsonToken(JsonTokenType.String, "\t"));  // escaped tab
					data.Add(preWhite + "\"\\n\"" + trailWhite, new JsonToken(JsonTokenType.String, "\n"));  // escaped newline
					data.Add(preWhite + "\"\\r\"" + trailWhite, new JsonToken(JsonTokenType.String, "\r"));  // escaped carriage return
					data.Add(preWhite + "\"\\f\"" + trailWhite, new JsonToken(JsonTokenType.String, "\f"));  // escaped form feed
					data.Add(preWhite + "\"\\b\"" + trailWhite, new JsonToken(JsonTokenType.String, "\b"));  // escaped backspace

					// --- Unicode escape sequences for control characters ---
					for (int i = 0; i <= 0x1F; i++)
					{
						// lower case hex
						data.Add(
							preWhite + $"\"\\u{i:x4}\"" + trailWhite,
							new JsonToken(JsonTokenType.String, $"{(char)i}"));

						// upper case hex
						data.Add(
							preWhite + $"\"\\u{i:X4}\"" + trailWhite,
							new JsonToken(JsonTokenType.String, $"{(char)i}"));
					}

					// Escaped code unit: \uFFFF
					data.Add(
						preWhite + "\"\\uFFFF\"" + trailWhite,
						new JsonToken(JsonTokenType.String, "\uFFFF"));

					// --- Numbers ---
					data.Add(preWhite + "0" + trailWhite, new JsonToken(JsonTokenType.Number, "0"));

					foreach (string before in new[] { "1", "12", "123" }) // digits before decimal point
					{
						foreach (string after in new[] { null, "1", "12", "123" }) // digits after decimal point
						{
							string number = before;
							if (after != null)
								number += "." + after;

							// Number without exponent
							data.Add(
								preWhite + number + trailWhite,
								new JsonToken(JsonTokenType.Number, number));

							foreach (string exp in new[] { "1", "12", "123" })
							{
								foreach (string e in new[] { "e", "E" })
								{
									foreach (string expSign in new[] { "", "+", "-" })
									{
										string numberWithExponent = number + e + expSign + exp;
										data.Add(
											preWhite + numberWithExponent + trailWhite,
											new JsonToken(JsonTokenType.Number, numberWithExponent));
									}
								}
							}
						}
					}

					// --- Boolean values ---
					data.Add(preWhite + "true" + trailWhite, new JsonToken(JsonTokenType.Boolean, "true"));
					data.Add(preWhite + "false" + trailWhite, new JsonToken(JsonTokenType.Boolean, "false"));

					// --- Null literal ---
					data.Add(preWhite + "null" + trailWhite, new JsonToken(JsonTokenType.Null, "null"));
				}
			}

			return data;
		}
	}

	/// <summary>
	/// Tests whether tokenizing JSON tokens works as expected (single tokens).
	/// </summary>
	[Theory]
	[MemberData(nameof(ProcessTestData_SingleTokens))]
	private void Process_SingleTokens(string json, object boxedExpected)
	{
		var expected = (JsonToken)boxedExpected;
		var tokenizer = new JsonTokenizer();
		int tokenCount = tokenizer.Process(json);
		Assert.Equal(1, tokenCount);
		Assert.Equal(expected, tokenizer.Tokens.Dequeue());
	}

	/// <summary>
	/// Tests whether tokenizing combinations of token work as expected.
	/// (gets tokens generated by ProcessTestData_SingleTokens, combines them to random sets of tokens and tests them).
	/// </summary>
	[Fact]
	private void Process_TokenSets()
	{
		const int iterations = 10000;

		Tuple<string, JsonToken>[] data = ProcessTestData_SingleTokens
			.Select(x => new Tuple<string, JsonToken>((string)x[0], (JsonToken)x[1]))
			.ToArray();

		var tokenizer = new JsonTokenizer();
		var random = new Random(0);
		var json = new StringBuilder();
		var tokens = new List<JsonToken>();
		for (int run = 0; run < iterations; run++)
		{
			int tokenCount = random.Next(2, 30);

			json.Clear();
			tokens.Clear();
			for (int j = 0; j < tokenCount; j++)
			{
				int selectedTokenIndex = random.Next(0, data.Length - 1);
				json.Append(data[selectedTokenIndex].Item1);
				json.Append(WhiteSpaceCharacters[random.Next(0, WhiteSpaceCharacters.Length - 1)]);
				tokens.Add(data[selectedTokenIndex].Item2);
			}

			// --- actual test start ---
			int extractedTokenCount = tokenizer.Process(json.ToString());
			Assert.Equal(tokens.Count, extractedTokenCount);
			Assert.Equal(tokens.ToArray(), tokenizer.Tokens.ToArray());

			// --- actual test end ---

			tokenizer.Reset();
		}
	}

	#endregion
}
