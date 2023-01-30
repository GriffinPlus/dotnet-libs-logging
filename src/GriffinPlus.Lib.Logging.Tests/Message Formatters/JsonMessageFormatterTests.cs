///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Xunit;

// ReSharper disable RedundantCaseLabel

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="JsonMessageFormatter"/> class.
	/// </summary>
	public class JsonMessageFormatterTests
	{
		private static readonly Dictionary<int, string> sEscapedCodePoints = new Dictionary<int, string>();
		private static readonly string                  sUnescapedString;
		private static readonly string                  sEscapedString_WithSolidus;
		private static readonly string                  sEscapedString_WithoutSolidus;

		/// <summary>
		/// Initializes the <see cref="JsonMessageFormatterTests"/> class.
		/// </summary>
		static JsonMessageFormatterTests()
		{
			// add dictionary with code points to escape
			// ---------------------------------------------------------------------------------------------
			for (int i = 0; i <= 0x1F; i++) sEscapedCodePoints[i] = $"\\u{i:X04}";
			sEscapedCodePoints[0x0008] = "\\b";     // backspace
			sEscapedCodePoints[0x0009] = "\\t";     // tab
			sEscapedCodePoints[0x000D] = "\\r";     // carriage return
			sEscapedCodePoints[0x000A] = "\\n";     // line feed
			sEscapedCodePoints[0x000C] = "\\f";     // form feed
			sEscapedCodePoints[0x0022] = "\\\"";    // quotation marks
			sEscapedCodePoints[0x002F] = "\\/";     // solidus
			sEscapedCodePoints[0x005C] = "\\\\";    // reverse solidus
			sEscapedCodePoints[0x0085] = "\\u0085"; // next line
			sEscapedCodePoints[0x0085] = "\\u0085"; // next line
			sEscapedCodePoints[0x2028] = "\\u2028"; // line separator
			sEscapedCodePoints[0x2029] = "\\u2029"; // paragraph separator

			// build strings that contains all unicode characters and their escaped equivalents
			// ---------------------------------------------------------------------------------------------
			var unescaped = new StringBuilder();
			var escaped_withSolidus = new StringBuilder();
			var escaped_withoutSolidus = new StringBuilder();
			for (int codepoint = 0; codepoint <= 0x10FFFF; codepoint++)
			{
				// add codepoint to the buffer with the input string
				if (codepoint < 0x10000)
				{
					unescaped.Append(unchecked((char)codepoint));
				}
				else
				{
					int sg1 = (codepoint - 0x10000) / 0x400 + 0xD800;
					int sg2 = codepoint % 0x400 + 0xDC00;
					unescaped.Append((char)sg1);
					unescaped.Append((char)sg2);
				}

				// handle solidus separately
				if (codepoint == 0x002F)
				{
					escaped_withSolidus.Append("\\/");
					escaped_withoutSolidus.Append('/');
					continue;
				}

				// add codepoint to buffer with the expected string
				if (sEscapedCodePoints.TryGetValue(codepoint, out string sequence))
				{
					escaped_withSolidus.Append(sequence);
					escaped_withoutSolidus.Append(sequence);
				}
				else
				{
					if (codepoint < 0x10000)
					{
						escaped_withSolidus.Append((char)codepoint);
						escaped_withoutSolidus.Append((char)codepoint);
					}
					else
					{
						int sg1 = (codepoint - 0x10000) / 0x400 + 0xD800;
						int sg2 = codepoint % 0x400 + 0xDC00;
						escaped_withSolidus.Append((char)sg1);
						escaped_withSolidus.Append((char)sg2);
						escaped_withoutSolidus.Append((char)sg1);
						escaped_withoutSolidus.Append((char)sg2);
					}
				}
			}

			sUnescapedString = unescaped.ToString();
			sEscapedString_WithSolidus = escaped_withSolidus.ToString();
			sEscapedString_WithoutSolidus = escaped_withoutSolidus.ToString();
		}


		/// <summary>
		/// Tests whether the creation of the formatter succeeds.
		/// </summary>
		[Fact]
		public void Create()
		{
			var formatter = new JsonMessageFormatter();
			Assert.Equal(CultureInfo.InvariantCulture, formatter.FormatProvider);
			Assert.Equal(LogMessageField.None, formatter.FormattedFields);
			Assert.Equal(JsonMessageFormatterStyle.OneLine, formatter.Style);
			Assert.Equal("    ", formatter.Indent);
			Assert.False(formatter.EscapeSolidus);

			// the formatter should not contain any fields at start
			// => the output should be an empty JSON document
			string output = formatter.Format(new LogMessage());
			Assert.Equal("{ }", output);
		}


		public static IEnumerable<object[]> FormatTestData
		{
			get
			{
				var message = new LogMessage
				{
					Timestamp = DateTimeOffset.Parse("2000-01-01 00:00:00Z"),
					HighPrecisionTimestamp = 123,
					LogWriterName = "MyWriter",
					LogLevelName = "MyLevel",
					Tags = new TagSet("Tag1", "Tag2"),
					ApplicationName = "MyApp",
					ProcessName = "MyProcess",
					ProcessId = 42,
					Text = "MyText"
				};

				// ------------------------------------------------------------------------
				// style: compact
				// ------------------------------------------------------------------------

				yield return new object[]
				{
					JsonMessageFormatterStyle.Compact,
					LogMessageField.None,
					message,
					null, // newline sequence is not relevant
					"{}"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.Compact,
					LogMessageField.Timestamp,
					message,
					null, // newline sequence is not relevant
					"{\"Timestamp\":\"2000-01-01 00:00:00Z\"}"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.Compact,
					LogMessageField.HighPrecisionTimestamp,
					message,
					null, // newline sequence is not relevant
					"{\"HighPrecisionTimestamp\":123}"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.Compact,
					LogMessageField.LogWriterName,
					message,
					null, // newline sequence is not relevant
					"{\"LogWriter\":\"MyWriter\"}"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.Compact,
					LogMessageField.LogLevelName,
					message,
					null, // newline sequence is not relevant
					"{\"LogLevel\":\"MyLevel\"}"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.Compact,
					LogMessageField.Tags,
					new LogMessage(message) { Tags = new TagSet() },
					null, // newline sequence is not relevant
					"{\"Tags\":[]}"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.Compact,
					LogMessageField.Tags,
					new LogMessage(message) { Tags = new TagSet("Tag") },
					null, // newline sequence is not relevant
					"{\"Tags\":[\"Tag\"]}"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.Compact,
					LogMessageField.Tags,
					new LogMessage(message) { Tags = new TagSet("Tag1", "Tag2") },
					null, // newline sequence is not relevant
					"{\"Tags\":[\"Tag1\",\"Tag2\"]}"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.Compact,
					LogMessageField.ApplicationName,
					message,
					null, // newline sequence is not relevant
					"{\"ApplicationName\":\"MyApp\"}"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.Compact,
					LogMessageField.ProcessName,
					message,
					null, // newline sequence is not relevant
					"{\"ProcessName\":\"MyProcess\"}"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.Compact,
					LogMessageField.ProcessId,
					message,
					null, // newline sequence is not relevant
					"{\"ProcessId\":42}"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.Compact,
					LogMessageField.Text,
					message,
					null, // newline sequence is not relevant
					"{\"Text\":\"MyText\"}"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.Compact,
					LogMessageField.All,
					message,
					null, // newline sequence is not relevant
					"{" +
					"\"Timestamp\":\"2000-01-01 00:00:00Z\"," +
					"\"HighPrecisionTimestamp\":123," +
					"\"LogWriter\":\"MyWriter\"," +
					"\"LogLevel\":\"MyLevel\"," +
					"\"Tags\":[\"Tag1\",\"Tag2\"]," +
					"\"ApplicationName\":\"MyApp\"," +
					"\"ProcessName\":\"MyProcess\"," +
					"\"ProcessId\":42," +
					"\"Text\":\"MyText\"" +
					"}"
				};

				// ------------------------------------------------------------------------
				// style: one line
				// ------------------------------------------------------------------------

				yield return new object[]
				{
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.None,
					message,
					null, // newline sequence is not relevant
					"{ }"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.Timestamp,
					message,
					null, // newline sequence is not relevant
					"{ \"Timestamp\" : \"2000-01-01 00:00:00Z\" }"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.HighPrecisionTimestamp,
					message,
					null, // newline sequence is not relevant
					"{ \"HighPrecisionTimestamp\" : 123 }"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.LogWriterName,
					message,
					null, // newline sequence is not relevant
					"{ \"LogWriter\" : \"MyWriter\" }"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.LogLevelName,
					message,
					null, // newline sequence is not relevant
					"{ \"LogLevel\" : \"MyLevel\" }"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.Tags,
					new LogMessage(message) { Tags = new TagSet() },
					null, // newline sequence is not relevant
					"{ \"Tags\" : [] }"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.Tags,
					new LogMessage(message) { Tags = new TagSet("Tag") },
					null, // newline sequence is not relevant
					"{ \"Tags\" : [ \"Tag\" ] }"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.Tags,
					new LogMessage(message) { Tags = new TagSet("Tag1", "Tag2") },
					null, // newline sequence is not relevant
					"{ \"Tags\" : [ \"Tag1\", \"Tag2\" ] }"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.ApplicationName,
					message,
					null, // newline sequence is not relevant
					"{ \"ApplicationName\" : \"MyApp\" }"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.ProcessName,
					message,
					null, // newline sequence is not relevant
					"{ \"ProcessName\" : \"MyProcess\" }"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.ProcessId,
					message,
					null, // newline sequence is not relevant
					"{ \"ProcessId\" : 42 }"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.Text,
					message,
					null, // newline sequence is not relevant
					"{ \"Text\" : \"MyText\" }"
				};

				yield return new object[]
				{
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.All,
					message,
					null, // newline sequence is not relevant
					"{" +
					" \"Timestamp\" : \"2000-01-01 00:00:00Z\"," +
					" \"HighPrecisionTimestamp\" : 123," +
					" \"LogWriter\" : \"MyWriter\"," +
					" \"LogLevel\" : \"MyLevel\"," +
					" \"Tags\" : [ \"Tag1\", \"Tag2\" ]," +
					" \"ApplicationName\" : \"MyApp\"," +
					" \"ProcessName\" : \"MyProcess\"," +
					" \"ProcessId\" : 42," +
					" \"Text\" : \"MyText\"" +
					" }"
				};

				// ------------------------------------------------------------------------
				// style: beautified
				// ------------------------------------------------------------------------

				foreach (string newline in new[] { "\n", "\r\n" })
				{
					yield return new object[]
					{
						JsonMessageFormatterStyle.Beautified,
						LogMessageField.None,
						message,
						newline,
						$"{{{newline}" +
						"}"
					};

					yield return new object[]
					{
						JsonMessageFormatterStyle.Beautified,
						LogMessageField.Timestamp,
						message,
						newline,
						$"{{{newline}" +
						$"    \"Timestamp\" : \"2000-01-01 00:00:00Z\"{newline}" +
						"}"
					};

					yield return new object[]
					{
						JsonMessageFormatterStyle.Beautified,
						LogMessageField.HighPrecisionTimestamp,
						message,
						newline,
						$"{{{newline}" +
						$"    \"HighPrecisionTimestamp\" : 123{newline}" +
						"}"
					};

					yield return new object[]
					{
						JsonMessageFormatterStyle.Beautified,
						LogMessageField.LogWriterName,
						message,
						newline,
						$"{{{newline}" +
						$"    \"LogWriter\" : \"MyWriter\"{newline}" +
						"}"
					};

					yield return new object[]
					{
						JsonMessageFormatterStyle.Beautified,
						LogMessageField.LogLevelName,
						message,
						newline,
						$"{{{newline}" +
						$"    \"LogLevel\" : \"MyLevel\"{newline}" +
						"}"
					};

					yield return new object[]
					{
						JsonMessageFormatterStyle.Beautified,
						LogMessageField.Tags,
						new LogMessage(message) { Tags = new TagSet() },
						newline,
						$"{{{newline}" +
						$"    \"Tags\" : []{newline}" +
						"}"
					};

					yield return new object[]
					{
						JsonMessageFormatterStyle.Beautified,
						LogMessageField.Tags,
						new LogMessage(message) { Tags = new TagSet("Tag") },
						newline,
						$"{{{newline}" +
						$"    \"Tags\" : [ \"Tag\" ]{newline}" +
						"}"
					};

					yield return new object[]
					{
						JsonMessageFormatterStyle.Beautified,
						LogMessageField.Tags,
						new LogMessage(message) { Tags = new TagSet("Tag1", "Tag2") },
						newline,
						$"{{{newline}" +
						$"    \"Tags\" : [ \"Tag1\", \"Tag2\" ]{newline}" +
						"}"
					};

					yield return new object[]
					{
						JsonMessageFormatterStyle.Beautified,
						LogMessageField.ApplicationName,
						message,
						newline,
						$"{{{newline}" +
						$"    \"ApplicationName\" : \"MyApp\"{newline}" +
						"}"
					};

					yield return new object[]
					{
						JsonMessageFormatterStyle.Beautified,
						LogMessageField.ProcessName,
						message,
						newline,
						$"{{{newline}" +
						$"    \"ProcessName\" : \"MyProcess\"{newline}" +
						"}"
					};

					yield return new object[]
					{
						JsonMessageFormatterStyle.Beautified,
						LogMessageField.ProcessId,
						message,
						newline,
						$"{{{newline}" +
						$"    \"ProcessId\" : 42{newline}" +
						"}"
					};

					yield return new object[]
					{
						JsonMessageFormatterStyle.Beautified,
						LogMessageField.Text,
						message,
						newline,
						$"{{{newline}" +
						$"    \"Text\" : \"MyText\"{newline}" +
						"}"
					};

					yield return new object[]
					{
						JsonMessageFormatterStyle.Beautified,
						LogMessageField.All,
						message,
						newline,
						$"{{{newline}" +
						$"    \"Timestamp\"              : \"2000-01-01 00:00:00Z\",{newline}" +
						$"    \"HighPrecisionTimestamp\" : 123,{newline}" +
						$"    \"LogWriter\"              : \"MyWriter\",{newline}" +
						$"    \"LogLevel\"               : \"MyLevel\",{newline}" +
						$"    \"Tags\"                   : [ \"Tag1\", \"Tag2\" ],{newline}" +
						$"    \"ApplicationName\"        : \"MyApp\",{newline}" +
						$"    \"ProcessName\"            : \"MyProcess\",{newline}" +
						$"    \"ProcessId\"              : 42,{newline}" +
						$"    \"Text\"                   : \"MyText\"{newline}" +
						"}"
					};
				}
			}
		}

		/// <summary>
		/// Tests whether formatting specific fields works as expected.
		/// </summary>
		[Theory]
		[MemberData(nameof(FormatTestData))]
		public void Format(
			JsonMessageFormatterStyle style,
			LogMessageField           fields,
			LogMessage                message,
			string                    newline,
			string                    expected)
		{
			// test set knows that the newline character sequence is not relevant, if newline == null
			// => set it explicitly to satisfy the formatter
			if (newline == null) newline = "\n";

			var formatter = new JsonMessageFormatter { Style = style, Newline = newline };

			if (fields.HasFlag(LogMessageField.Timestamp)) formatter.AddTimestampField();
			if (fields.HasFlag(LogMessageField.HighPrecisionTimestamp)) formatter.AddHighPrecisionTimestampField();
			if (fields.HasFlag(LogMessageField.LogWriterName)) formatter.AddLogWriterField();
			if (fields.HasFlag(LogMessageField.LogLevelName)) formatter.AddLogLevelField();
			if (fields.HasFlag(LogMessageField.Tags)) formatter.AddTagsField();
			if (fields.HasFlag(LogMessageField.ApplicationName)) formatter.AddApplicationNameField();
			if (fields.HasFlag(LogMessageField.ProcessName)) formatter.AddProcessNameField();
			if (fields.HasFlag(LogMessageField.ProcessId)) formatter.AddProcessIdField();
			if (fields.HasFlag(LogMessageField.Text)) formatter.AddTextField();

			Assert.Equal(fields, formatter.FormattedFields);

			string output = formatter.Format(message);
			Assert.Equal(expected, output);
		}


		/// <summary>
		/// Tests whether the <see cref="JsonMessageFormatter.AppendEscapedStringToBuilder"/> method
		/// escapes all characters properly.
		/// </summary>
		[Fact]
		public void AppendEscapedStringToBuilder()
		{
			// without escaping solidus
			var builder1 = new StringBuilder();
			JsonMessageFormatter.AppendEscapedStringToBuilder(builder1, sUnescapedString, false);
			Assert.Equal(sEscapedString_WithoutSolidus, builder1.ToString());

			// with escaping solidus
			var builder2 = new StringBuilder();
			JsonMessageFormatter.AppendEscapedStringToBuilder(builder2, sUnescapedString, true);
			Assert.Equal(sEscapedString_WithSolidus, builder2.ToString());
		}


		/// <summary>
		/// Tests whether keys are are escaped properly.
		/// </summary>
		[Theory]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.Timestamp)]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.HighPrecisionTimestamp)]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.LogWriterName)]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.LogLevelName)]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.Tags)]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.ApplicationName)]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.ProcessName)]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.ProcessId)]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.Text)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.Timestamp)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.HighPrecisionTimestamp)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.LogWriterName)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.LogLevelName)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.Tags)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.ApplicationName)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.ProcessName)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.ProcessId)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.Text)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.Timestamp)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.HighPrecisionTimestamp)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.LogWriterName)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.LogLevelName)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.Tags)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.ApplicationName)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.ProcessName)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.ProcessId)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.Text)]
		public void Format_EscapingKeys(JsonMessageFormatterStyle style, LogMessageField fields)
		{
			var message = new LogMessage();
			var formatter = new JsonMessageFormatter { Style = style, Newline = "\n" };

			if (fields.HasFlag(LogMessageField.Timestamp)) formatter.AddTimestampField("u", sUnescapedString);
			if (fields.HasFlag(LogMessageField.HighPrecisionTimestamp)) formatter.AddHighPrecisionTimestampField(sUnescapedString);
			if (fields.HasFlag(LogMessageField.LogWriterName)) formatter.AddLogWriterField(sUnescapedString);
			if (fields.HasFlag(LogMessageField.LogLevelName)) formatter.AddLogLevelField(sUnescapedString);
			if (fields.HasFlag(LogMessageField.Tags)) formatter.AddTagsField(sUnescapedString);
			if (fields.HasFlag(LogMessageField.ApplicationName)) formatter.AddApplicationNameField(sUnescapedString);
			if (fields.HasFlag(LogMessageField.ProcessName)) formatter.AddProcessNameField(sUnescapedString);
			if (fields.HasFlag(LogMessageField.ProcessId)) formatter.AddProcessIdField(sUnescapedString);
			if (fields.HasFlag(LogMessageField.Text)) formatter.AddTextField(sUnescapedString);

			Assert.Equal(fields, formatter.FormattedFields);

			// prepare regex to match output
			string pattern;
			switch (style)
			{
				case JsonMessageFormatterStyle.Compact:
					pattern = "^{\"(.+)\":.+}$";
					break;

				case JsonMessageFormatterStyle.OneLine:
					pattern = "^{ \"(.+)\" : .+ }$";
					break;

				case JsonMessageFormatterStyle.Beautified:
				default:
					pattern = "^{\n    \"(.+)\" : .+\n}$";
					break;
			}

			var regex = new Regex(pattern);

			// check whether the key has been escaped properly (without escaping the solidus)
			formatter.EscapeSolidus = false;
			string output1 = formatter.Format(message);
			Match match1 = regex.Match(output1);
			Assert.True(match1.Success);
			Assert.Equal(sEscapedString_WithoutSolidus, match1.Groups[1].Value);

			// check whether the key has been escaped properly (with escaping the solidus)
			formatter.EscapeSolidus = true;
			string output2 = formatter.Format(message);
			Match match2 = regex.Match(output2);
			Assert.True(match2.Success);
			Assert.Equal(sEscapedString_WithSolidus, match2.Groups[1].Value);
		}


		/// <summary>
		/// Tests whether values are are escaped properly.
		/// </summary>
		[Theory]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.LogWriterName)]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.LogLevelName)]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.ApplicationName)]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.ProcessName)]
		[InlineData(JsonMessageFormatterStyle.Compact, LogMessageField.Text)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.LogWriterName)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.LogLevelName)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.ApplicationName)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.ProcessName)]
		[InlineData(JsonMessageFormatterStyle.OneLine, LogMessageField.Text)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.LogWriterName)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.LogLevelName)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.ApplicationName)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.ProcessName)]
		[InlineData(JsonMessageFormatterStyle.Beautified, LogMessageField.Text)]
		public void Format_EscapingValues(JsonMessageFormatterStyle style, LogMessageField fields)
		{
			var formatter = new JsonMessageFormatter { Style = style, Newline = "\n" };

			string key = "";
			var message = new LogMessage();

			if (fields.HasFlag(LogMessageField.LogWriterName))
			{
				key = "LogWriter";
				formatter.AddLogWriterField(key);
				message.LogWriterName = sUnescapedString;
			}

			if (fields.HasFlag(LogMessageField.LogLevelName))
			{
				key = "LogLevel";
				formatter.AddLogLevelField(key);
				message.LogLevelName = sUnescapedString;
			}

			if (fields.HasFlag(LogMessageField.ApplicationName))
			{
				key = "ApplicationName";
				formatter.AddApplicationNameField(key);
				message.ApplicationName = sUnescapedString;
			}

			if (fields.HasFlag(LogMessageField.ProcessName))
			{
				key = "ProcessName";
				formatter.AddProcessNameField(key);
				message.ProcessName = sUnescapedString;
			}

			if (fields.HasFlag(LogMessageField.Text))
			{
				key = "Text";
				formatter.AddTextField(key);
				message.Text = sUnescapedString;
			}

			Assert.Equal(fields, formatter.FormattedFields);

			// prepare regex to match output
			string pattern;
			switch (style)
			{
				case JsonMessageFormatterStyle.Compact:
					pattern = "^{\"(.+)\":\"(.+)\"}$";
					break;

				case JsonMessageFormatterStyle.OneLine:
					pattern = "^{ \"(.+)\" : \"(.+)\" }$";
					break;

				case JsonMessageFormatterStyle.Beautified:
				default:
					pattern = "^{\n    \"(.+)\" : \"(.+)\"\n}$";
					break;
			}

			var regex = new Regex(pattern);

			// check whether the key has been escaped properly (without escaping the solidus)
			formatter.EscapeSolidus = false;
			string output1 = formatter.Format(message);
			Match match1 = regex.Match(output1);
			Assert.True(match1.Success);
			Assert.Equal(key, match1.Groups[1].Value);
			Assert.Equal(sEscapedString_WithoutSolidus, match1.Groups[2].Value);

			// check whether the key has been escaped properly (with escaping the solidus)
			formatter.EscapeSolidus = true;
			string output2 = formatter.Format(message);
			Match match2 = regex.Match(output2);
			Assert.True(match2.Success);
			Assert.Equal(key, match2.Groups[1].Value);
			Assert.Equal(sEscapedString_WithSolidus, match2.Groups[2].Value);
		}


		/// <summary>
		/// Tests whether the <see cref="JsonMessageFormatter.AllFields"/> property returns the correct formatter.
		/// </summary>
		[Fact]
		public void AllFields()
		{
			JsonMessageFormatter formatter = JsonMessageFormatter.AllFields;
			const LogMessageField expectedFields = LogMessageField.Timestamp |
			                                       LogMessageField.LogWriterName |
			                                       LogMessageField.LogLevelName |
			                                       LogMessageField.Tags |
			                                       LogMessageField.ApplicationName |
			                                       LogMessageField.ProcessName |
			                                       LogMessageField.ProcessId |
			                                       LogMessageField.Text;
			Assert.Equal(expectedFields, formatter.FormattedFields);
			LogMessage message = GetTestMessage();
			formatter.Style = JsonMessageFormatterStyle.OneLine;
			string output = formatter.Format(message);
			const string expected = "{" +
			                        " \"Timestamp\" : \"2000-01-01 00:00:00Z\"," +
			                        " \"LogWriter\" : \"MyWriter\"," +
			                        " \"LogLevel\" : \"MyLevel\"," +
			                        " \"Tags\" : [ \"Tag1\", \"Tag2\" ]," +
			                        " \"ApplicationName\" : \"MyApp\"," +
			                        " \"ProcessName\" : \"MyProcess\"," +
			                        " \"ProcessId\" : 42," +
			                        " \"Text\" : \"MyText\"" +
			                        " }";
			Assert.Equal(expected, output);
		}


		/// <summary>
		/// Gets a log message with test data.
		/// </summary>
		/// <returns>A log message with test data.</returns>
		private static LogMessage GetTestMessage()
		{
			return new LogMessage
			{
				Timestamp = DateTimeOffset.Parse("2000-01-01 00:00:00Z"),
				HighPrecisionTimestamp = 123,
				LogWriterName = "MyWriter",
				LogLevelName = "MyLevel",
				Tags = new TagSet("Tag1", "Tag2"),
				ApplicationName = "MyApp",
				ProcessName = "MyProcess",
				ProcessId = 42,
				Text = "MyText"
			};
		}
	}

}
