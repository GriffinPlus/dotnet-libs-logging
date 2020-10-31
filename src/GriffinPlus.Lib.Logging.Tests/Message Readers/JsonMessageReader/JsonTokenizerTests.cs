///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/GriffinPlus/dotnet-libs-logging)
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Unit tests targeting the <see cref="JsonTokenizer"/> class.
	/// </summary>
	public class JsonTokenizerTests
	{
		private static readonly string sWhiteSpaceCharacters;

		/// <summary>
		/// Initializes the <see cref="JsonTokenizerTests"/> class.
		/// </summary>
		static JsonTokenizerTests()
		{
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i <= char.MaxValue; i++)
			{
				char c = (char)i;
				if (char.IsWhiteSpace(c)) builder.Append(c);
			}

			sWhiteSpaceCharacters = builder.ToString();
		}

		/// <summary>
		/// Tests whether the creation of the tokenizer succeeds.
		/// </summary>
		[Fact]
		void Create()
		{
			var tokenizer = new JsonTokenizer();
			Assert.Equal(1, tokenizer.CurrentLineNumber);
			Assert.Equal(0, tokenizer.CurrentPosition);
			Assert.Empty(tokenizer.Tokens);
		}

		#region Process()

		public static IEnumerable<object[]> ProcessTestData_SingleTokens
		{
			get
			{
				foreach (var preWhite in new[] { "", sWhiteSpaceCharacters }) // preceding whitespaces
				{
					foreach (var trailWhite in new[] { "", sWhiteSpaceCharacters }) // trailing whitespaces
					{
						var input = preWhite + "{" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.LBracket)
						};

						input = preWhite + "}" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.RBracket)
						};

						input = preWhite + "[" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.LSquareBracket)
						};

						input = preWhite + "]" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.RSquareBracket)
						};

						input = preWhite + ":" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.Colon)
						};

						input = preWhite + "," + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.Comma)
						};

						input = preWhite + "\"just a string\"" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.String, "just a string")
						};

						// escaped quotation mark ('"')
						input = preWhite + "\"\\\"\"" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.String, "\"")
						};

						// escaped solidus ('/')
						input = preWhite + "\"\\/\"" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.String, "/")
						};

						// escaped back space ('\')
						input = preWhite + "\"\\\\\"" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.String, "\\")
						};

						// escaped tab ('\t')
						input = preWhite + "\"\\t\"" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.String, "\t")
						};

						// escaped newline ('\n')
						input = preWhite + "\"\\n\"" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.String, "\n")
						};

						// escaped carriage return ('\r')
						input = preWhite + "\"\\r\"" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.String, "\r")
						};

						// escaped form feed ('\f')
						input = preWhite + "\"\\f\"" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.String, "\f")
						};

						// escaped backspace ('\b')
						input = preWhite + "\"\\b\"" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.String, "\b")
						};

						// control characters
						for (int i = 0; i <= 0x1F; i++)
						{
							// hex value in lower case letters
							input = preWhite + $"\"\\u{i:x4}\"" + trailWhite;
							yield return new object[]
							{
								input,
								new JsonToken(JsonTokenType.String, $"{(char)i}")
							};

							// hex value in upper case letters
							input = preWhite + $"\"\\u{i:X4}\"" + trailWhite;
							yield return new object[]
							{
								input,
								new JsonToken(JsonTokenType.String, $"{(char)i}")
							};
						}

						// great escaped code unit
						input = preWhite + "\"\\uFFFF\"" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.String, "\uFFFF")
						};

						// number just zero
						input = preWhite + "0" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.Number, "0")
						};

						// various number formats
						foreach (var before in new[] { "1", "12", "123" }) // digits before decimal point
						{
							foreach (var after in new[] { null, "1", "12", "123" }) // digits after decimal point
							{
								string number = before;
								if (after != null) number += "." + after;

								// number without exponent
								input = preWhite + number + trailWhite;
								yield return new object[]
								{
									input,
									new JsonToken(JsonTokenType.Number, number)
								};

								foreach (var exp in new[] { "1", "12", "123" })
								{
									if (exp != null)
									{
										foreach (var e in new[] { "e", "E" })
										{
											foreach (var expSign in new[] { "", "+", "-" })
											{
												var numberWithExponent = number + e + expSign + exp;
												input = preWhite + numberWithExponent + trailWhite;
												yield return new object[]
												{
													input,
													new JsonToken(JsonTokenType.Number, numberWithExponent)
												};
											}
										}
									}
									else
									{
										input = preWhite + number + trailWhite;
										yield return new object[]
										{
											input,
											new JsonToken(JsonTokenType.Number, number)
										};
									}
								}
							}
						}

						// boolean value: true
						input = preWhite + "true" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.Boolean, "true")
						};

						// boolean value: false
						input = preWhite + "false" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.Boolean, "false")
						};

						// json null
						input = preWhite + "null" + trailWhite;
						yield return new object[]
						{
							input,
							new JsonToken(JsonTokenType.Null, "null")
						};
					}
				}
			}
		}

		/// <summary>
		/// Tests whether tokenizing JSON tokens works as expected (single tokens).
		/// </summary>
		[Theory]
		[MemberData(nameof(ProcessTestData_SingleTokens))]
		void Process_SingleTokens(string json, JsonToken expected)
		{
			var tokenizer = new JsonTokenizer();
			int tokenCount = tokenizer.Process(json, true);
			Assert.Equal(1, tokenCount);
			Assert.Equal(expected, tokenizer.Tokens.Dequeue());
		}

		/// <summary>
		/// Tests whether tokenizing combinations of token work as expected.
		/// (gets tokens generated by ProcessTestData_SingleTokens, combines them to random sets of tokens and tests them).
		/// </summary>
		[Fact]
		void Process_TokenSets()
		{
			const int iterations = 100000;

			var data = ProcessTestData_SingleTokens
				.Select(x => new Tuple<string, JsonToken>((string)x[0], (JsonToken)x[1]))
				.ToArray();

			var tokenizer = new JsonTokenizer();
			Random random = new Random(0);
			StringBuilder json = new StringBuilder();
			List<JsonToken> tokens = new List<JsonToken>();
			for (int run = 0; run < iterations; run++)
			{
				int tokenCount = random.Next(2, 30);

				json.Clear();
				tokens.Clear();
				for (int j = 0; j < tokenCount; j++)
				{
					int selectedTokenIndex = random.Next(0, data.Length - 1);
					json.Append(data[selectedTokenIndex].Item1);
					json.Append(sWhiteSpaceCharacters[random.Next(0, sWhiteSpaceCharacters.Length - 1)]);
					tokens.Add(data[selectedTokenIndex].Item2);
				}

				// --- actual test start ---
				int extractedTokenCount = tokenizer.Process(json.ToString(), true);
				Assert.Equal(tokens.Count, extractedTokenCount);
				Assert.Equal(tokens.ToArray(), tokenizer.Tokens.ToArray());
				// --- actual test end ---

				tokenizer.Reset();
			}
		}

		#endregion

	}
}
