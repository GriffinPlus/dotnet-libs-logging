///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

using Xunit;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Unit tests targeting the <see cref="LogServiceClientChannel"/> class.
	/// </summary>
	public class ClientLogServiceChannelTests
	{
		#region FormatWriteCommand()

		[Fact]
		public void FormatWriteCommand()
		{
			// create a fully populated log message
			// (more field specific formattings are covered by other tests below)
			var message = new LogMessage
			{
				Timestamp = DateTimeOffset.Parse("0001-01-01T01:01:01+01:00"),
				HighPrecisionTimestamp = 12345,
				LostMessageCount = 1,
				LogWriterName = "A Log Writer",
				LogLevelName = "A Log Level",
				Tags = new TagSet("Tag-1", "Tag-2"),
				Text = "Lorem ipsum dolor sit"
			};

			string expected =
				"WRITE\n" +
				"timestamp: 0001-01-01T01:01:01.000+01:00\n" +
				"ticks: 12345\n" +
				"lost: 1\n" +
				"writer: A Log Writer\n" +
				"level: A Log Level\n" +
				"tag: Tag-1\n" +
				"tag: Tag-2\n" +
				"text: Lorem ipsum dolor sit\n";

			// format the message creating a 'WRITE' command
			char[] buffer = new char[32 * 1024];
			int length = LogServiceClientChannel.FormatWriteCommand(message, buffer);
			string actual = new string(buffer, 0, length);
			Assert.Equal(expected, actual);
		}

		#endregion

		#region FormatWriteCommand_Timestamp()

		/// <summary>
		/// Test data for <see cref="FormatWriteCommand_Timestamp"/>.
		/// </summary>
		public static IEnumerable<object[]> FormatWriteCommandTestData_Timestamp
		{
			get
			{
				string pre = "timestamp: ";
				string post = "\n";

				foreach (int offset in new[] { 0, 1 })
				{
					// UTC time should always be encoded formatted using the offset syntax
					yield return new object[] { DateTimeOffset.Parse("0001-02-03T04:05:06.7+00:00"), offset, pre + "0001-02-03T04:05:06.700+00:00" + post };
					yield return new object[] { DateTimeOffset.Parse("0001-02-03T04:05:06.7Z"), offset, pre + "0001-02-03T04:05:06.700+00:00" + post };

					// milliseconds should always have 3 digits
					yield return new object[] { DateTimeOffset.Parse("0001-02-03T04:05:06.7+08:09"), offset, pre + "0001-02-03T04:05:06.700+08:09" + post };
					yield return new object[] { DateTimeOffset.Parse("0001-02-03T04:05:06.78+08:09"), offset, pre + "0001-02-03T04:05:06.780+08:09" + post };
					yield return new object[] { DateTimeOffset.Parse("0001-02-03T04:05:06.789+08:09"), offset, pre + "0001-02-03T04:05:06.789+08:09" + post };

					// minimum and maximum timestamp
					yield return new object[] { DateTimeOffset.MinValue, offset, pre + "0001-01-01T00:00:00.000+00:00" + post };
					yield return new object[] { DateTimeOffset.MaxValue, offset, pre + "9999-12-31T23:59:59.999+00:00" + post };
				}
			}
		}

		[Theory]
		[MemberData(nameof(FormatWriteCommandTestData_Timestamp))]
		public void FormatWriteCommand_Timestamp(DateTimeOffset timestamp, int offset, string expected)
		{
			char[] buffer = new char[100];
			int newOffset = LogServiceClientChannel.FormatWriteCommand_Timestamp(buffer, offset, timestamp);
			int length = newOffset - offset;
			Assert.Equal(41, length); // all timestamps should have a unified length of 41 characters (incl. field name and line break)
			string formatted = new string(buffer, offset, length);
			Assert.Equal(expected, formatted);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		public void FormatWriteCommand_Timestamp_BufferTooSmall(int offset)
		{
			// formatted timestamps should always have 41 characters, so a buffer with a length of 40 should trigger the check
			char[] buffer = new char[offset + 40];
			Assert.Throws<InsufficientBufferSizeException>(() => LogServiceClientChannel.FormatWriteCommand_Timestamp(buffer, offset, DateTime.MaxValue));
		}

		#endregion

		#region FormatWriteCommand_HighPrecisionTimestamp()

		/// <summary>
		/// Test data for <see cref="FormatWriteCommand_HighPrecisionTimestamp"/>.
		/// </summary>
		public static IEnumerable<object[]> FormatWriteCommandTestData_HighPrecisionTimestamp
		{
			get
			{
				string pre = "ticks: ";
				string post = "\n";

				foreach (int offset in new[] { 0, 1 })
				{
					yield return new object[] { long.MinValue, offset, pre + "-9223372036854775808" + post };
					yield return new object[] { -1L, offset, pre + "-1" + post };
					yield return new object[] { 0L, offset, pre + "0" + post };
					yield return new object[] { 1L, offset, pre + "1" + post };
					yield return new object[] { long.MaxValue, offset, pre + "9223372036854775807" + post };
				}
			}
		}

		[Theory]
		[MemberData(nameof(FormatWriteCommandTestData_HighPrecisionTimestamp))]
		public void FormatWriteCommand_HighPrecisionTimestamp(long ticks, int offset, string expected)
		{
			char[] buffer = new char[100];
			int newOffset = LogServiceClientChannel.FormatWriteCommand_HighPrecisionTimestamp(buffer, offset, ticks);
			int length = newOffset - offset;
			Assert.True(length <= 28); // the formatted field should have a length of 28 character at maximum
			string formatted = new string(buffer, offset, length);
			Assert.Equal(expected, formatted);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		public void FormatWriteCommand_HighPrecisionTimestamp_BufferTooSmall(int offset)
		{
			// formatted high-precision timestamps should have 28 characters at maximum, so a buffer with a length of 27 should trigger the check
			// (the check looks at the maximum possible length of the formatted result, not the actual length - the actual length may be shorter)
			char[] buffer = new char[offset + 27];
			Assert.Throws<InsufficientBufferSizeException>(() => LogServiceClientChannel.FormatWriteCommand_HighPrecisionTimestamp(buffer, offset, 0));
		}

		#endregion

		#region FormatWriteCommand_LostMessageCount()

		/// <summary>
		/// Test data for <see cref="FormatWriteCommand_LostMessageCount"/>.
		/// </summary>

		public static IEnumerable<object[]> FormatWriteCommandTestData_LostMessageCount
		{
			get
			{
				string pre = "lost: ";
				string post = "\n";

				foreach (int offset in new[] { 0, 1 })
				{
					// a negative or zero count should not emit the field at all
					yield return new object[] { int.MinValue, offset, "" };
					yield return new object[] { -1, offset, "" };
					yield return new object[] { 0, offset, "" };

					// positive counts should work as expected
					yield return new object[] { 1, offset, pre + "1" + post };
					yield return new object[] { int.MaxValue, offset, pre + "2147483647" + post };
				}
			}
		}

		[Theory]
		[MemberData(nameof(FormatWriteCommandTestData_LostMessageCount))]
		public void FormatWriteCommand_LostMessageCount(int lastMessageCount, int offset, string expected)
		{
			char[] buffer = new char[100];
			int newOffset = LogServiceClientChannel.FormatWriteCommand_LostMessageCount(buffer, offset, lastMessageCount);
			int length = newOffset - offset;
			Assert.True(length <= 18); // the formatted field should have a length of 18 character at maximum
			string formatted = new string(buffer, offset, length);
			Assert.Equal(expected, formatted);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		public void FormatWriteCommand_LostMessageCount_BufferTooSmall(int offset)
		{
			// the formatted lost message count should have 18 characters at maximum, so a buffer with a length of 18 should trigger the check
			// (the check looks at the maximum possible length of the formatted result, not the actual length - the actual length may be shorter)
			char[] buffer = new char[offset + 17];
			Assert.Throws<InsufficientBufferSizeException>(() => LogServiceClientChannel.FormatWriteCommand_LostMessageCount(buffer, offset, 0));
		}

		#endregion

		#region FormatWriteCommand_LogWriterName()

		/// <summary>
		/// Test data for <see cref="FormatWriteCommand_LogWriterName"/>.
		/// </summary>
		public static IEnumerable<object[]> FormatWriteCommandTestData_LogWriterName
		{
			get
			{
				string pre = "writer: ";
				string post = "\n";

				foreach (int offset in new[] { 0, 1 })
				{
					// no log writer name specified => field should not be emitted at all
					yield return new object[] { null, offset, "" };      // null reference
					yield return new object[] { "", offset, "" };        // empty string
					yield return new object[] { " \t\r\n", offset, "" }; // whitespace string

					// other log writer names should be emitted as expected
					yield return new object[] { "Lorem ipsum dolor", offset, pre + "Lorem ipsum dolor" + post };       // nothing special
					yield return new object[] { "Lorem\ripsum dolor", offset, pre + "Lorem\\ripsum dolor" + post };    // escaping of '\r'
					yield return new object[] { "Lorem\ripsum\rdolor", offset, pre + "Lorem\\ripsum\\rdolor" + post }; // escaping of '\r' (multiple)
					yield return new object[] { "Lorem\nipsum dolor", offset, pre + "Lorem\\nipsum dolor" + post };    // escaping of '\n'
					yield return new object[] { "Lorem\nipsum\ndolor", offset, pre + "Lorem\\nipsum\\ndolor" + post }; // escaping of '\n' (multiple)
				}
			}
		}

		[Theory]
		[MemberData(nameof(FormatWriteCommandTestData_LogWriterName))]
		public void FormatWriteCommand_LogWriterName(string name, int offset, string expected)
		{
			char[] buffer = new char[100];
			int newOffset = LogServiceClientChannel.FormatWriteCommand_LogWriterName(buffer, offset, name);
			int length = newOffset - offset;
			Assert.True(length <= 9 + 2 * (name?.Length ?? 0)); // worst case: every character is escaped
			string formatted = new string(buffer, offset, length);
			Assert.Equal(expected, formatted);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		public void FormatWriteCommand_LogWriterName_BufferTooSmall(int offset)
		{
			// worst case: every character is escaped
			// => the buffer should be at least twice as large as the string plus 9 characters for the field name and the line break.
			// => taking one character less than this size should trigger the check
			string testString = "xxx";
			char[] buffer = new char[9 + 2 * testString.Length - 1];
			Assert.Throws<InsufficientBufferSizeException>(() => LogServiceClientChannel.FormatWriteCommand_LogWriterName(buffer, offset, testString));
		}

		#endregion

		#region FormatWriteCommand_LogLevelName()

		/// <summary>
		/// Test data for <see cref="FormatWriteCommand_LogLevelName"/>.
		/// </summary>
		public static IEnumerable<object[]> FormatWriteCommandTestData_LogLevelName
		{
			get
			{
				string pre = "level: ";
				string post = "\n";

				foreach (int offset in new[] { 0, 1 })
				{
					// no log level name specified => field should not be emitted at all
					yield return new object[] { null, offset, "" };      // null reference
					yield return new object[] { "", offset, "" };        // empty string
					yield return new object[] { " \t\r\n", offset, "" }; // whitespace string

					// other log level names should be emitted as expected
					yield return new object[] { "Lorem ipsum dolor", offset, pre + "Lorem ipsum dolor" + post };       // nothing special
					yield return new object[] { "Lorem\ripsum dolor", offset, pre + "Lorem\\ripsum dolor" + post };    // escaping of '\r'
					yield return new object[] { "Lorem\ripsum\rdolor", offset, pre + "Lorem\\ripsum\\rdolor" + post }; // escaping of '\r' (multiple)
					yield return new object[] { "Lorem\nipsum dolor", offset, pre + "Lorem\\nipsum dolor" + post };    // escaping of '\n'
					yield return new object[] { "Lorem\nipsum\ndolor", offset, pre + "Lorem\\nipsum\\ndolor" + post }; // escaping of '\n' (multiple)
				}
			}
		}

		[Theory]
		[MemberData(nameof(FormatWriteCommandTestData_LogLevelName))]
		public void FormatWriteCommand_LogLevelName(string name, int offset, string expected)
		{
			char[] buffer = new char[100];
			int newOffset = LogServiceClientChannel.FormatWriteCommand_LogLevelName(buffer, offset, name);
			int length = newOffset - offset;
			Assert.True(length <= 8 + 2 * (name?.Length ?? 0)); // worst case: every character is escaped
			string formatted = new string(buffer, offset, length);
			Assert.Equal(expected, formatted);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		public void FormatWriteCommand_LogLevelName_BufferTooSmall(int offset)
		{
			// worst case: every character is escaped
			// => the buffer should be at least twice as large as the string plus 8 characters for the field name and the line break.
			// => taking one character less than this size should trigger the check
			string testString = "xxx";
			char[] buffer = new char[8 + 2 * testString.Length - 1];
			Assert.Throws<InsufficientBufferSizeException>(() => LogServiceClientChannel.FormatWriteCommand_LogLevelName(buffer, offset, testString));
		}

		#endregion

		#region FormatWriteCommand_Tags()

		/// <summary>
		/// Test data for <see cref="FormatWriteCommand_Tags"/>.
		/// </summary>
		public static IEnumerable<object[]> FormatWriteCommandTestData_Tags
		{
			get
			{
				string pre = "tag: ";
				string post = "\n";

				foreach (int offset in new[] { 0, 1 })
				{
					// no tag specified => field should not be emitted at all
					yield return new object[] { null, offset, "" };         // null reference
					yield return new object[] { TagSet.Empty, offset, "" }; // empty tag set

					// a single tag
					yield return new object[]
					{
						new TagSet("xxx"),
						offset,
						pre + "xxx" + post
					};

					// two tags
					yield return new object[]
					{
						new TagSet("xxx", "yyy"),
						offset,
						pre + "xxx" + post + pre + "yyy" + post
					};
				}
			}
		}

		[Theory]
		[MemberData(nameof(FormatWriteCommandTestData_Tags))]
		public void FormatWriteCommand_Tags(ITagSet tags, int offset, string expected)
		{
			char[] buffer = new char[100];
			int newOffset = LogServiceClientChannel.FormatWriteCommand_Tags(buffer, offset, tags);
			int length = newOffset - offset;
			string formatted = new string(buffer, offset, length);
			Assert.Equal(expected, formatted);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		public void FormatWriteCommand_Tags_BufferTooSmall_OneTag(int offset)
		{
			var tags = new TagSet("xxx");
			char[] buffer = new char[6 + tags[0].Length - 1];
			Assert.Throws<InsufficientBufferSizeException>(() => LogServiceClientChannel.FormatWriteCommand_Tags(buffer, offset, tags));
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		public void FormatWriteCommand_Tags_BufferTooSmall_TwoTags(int offset)
		{
			var tags = new TagSet("xxx", "yyy");
			char[] buffer = new char[2 * 6 + tags[0].Length + tags[1].Length - 1];
			Assert.Throws<InsufficientBufferSizeException>(() => LogServiceClientChannel.FormatWriteCommand_Tags(buffer, offset, tags));
		}

		#endregion

		#region FormatWriteCommand_Text()

		/// <summary>
		/// Test data for <see cref="FormatWriteCommand_Text"/>.
		/// </summary>
		public static IEnumerable<object[]> FormatWriteCommandTestData_Text
		{
			get
			{
				foreach (int offset in new[] { 0, 1 })
				{
					// no text specified => field should not be emitted at all
					yield return new object[] { null, offset, "" }; // null reference
					yield return new object[] { "", offset, "" };   // empty text

					// single line, nothing special
					// => the text should be put in one line with the "text: "
					yield return new object[] { "Lorem ipsum dolor si", offset, "text: " + "Lorem ipsum dolor si" + "\n" };

					// single long line (not long enough to trigger the line splitter mechanism)
					yield return new object[]
					{
						new string('x', LogServiceChannel.MaxLineLength - 7),
						offset,
						"text: " + new string('x', LogServiceChannel.MaxLineLength - 7) + "\n"
					};

					// single long line (just long enough to trigger the line splitter mechanism)
					// => line break is inserted after "text:", but the next line can fully store the line
					yield return new object[]
					{
						new string('x', LogServiceChannel.MaxLineLength - 6),
						offset,
						"text:\n" +
						new string('x', LogServiceChannel.MaxLineLength - 6) +
						"\n" +
						".\n"
					};

					// single long line (long enough to trigger the line splitter mechanism)
					// => line break is inserted after "text:", the next line can fully store the line
					//    (the line is filled up to the maximum length)
					yield return new object[]
					{
						new string('x', LogServiceChannel.MaxLineLength),
						offset,
						"text:\n" +
						new string('x', LogServiceChannel.MaxLineLength) +
						"\n" +
						".\n"
					};

					// single long line (long enough to trigger the line splitter mechanism)
					// => line break is inserted after "text:", the next line can't fully store the line
					// => the line is filled up to the maximum length, then a splitter is inserted and the remaining
					//    line is written into the following line
					yield return new object[]
					{
						new string('x', LogServiceChannel.MaxLineLength + 1),
						offset,
						"text:\n" +
						new string('x', LogServiceChannel.MaxLineLength) +
						"\n" +
						"\\\n" +
						new string('x', 1) +
						"\n" +
						".\n"
					};

					// single very long line
					// => text is distributed over multiple lines using a line splitter sequence to concat the lines
					yield return new object[]
					{
						new string('x', 3 * LogServiceChannel.MaxLineLength),
						offset,
						"text:\n" +
						new string('x', LogServiceChannel.MaxLineLength) +
						"\n" +
						"\\\n" +
						new string('x', LogServiceChannel.MaxLineLength) +
						"\n" +
						"\\\n" +
						new string('x', LogServiceChannel.MaxLineLength) +
						"\n" +
						".\n"
					};

					// single very long line
					// => text is distributed over multiple lines using a line splitter sequence to concat the lines
					yield return new object[]
					{
						new string('x', 3 * LogServiceChannel.MaxLineLength),
						offset,
						"text:\n" +
						new string('x', LogServiceChannel.MaxLineLength) +
						"\n" +
						"\\\n" +
						new string('x', LogServiceChannel.MaxLineLength) +
						"\n" +
						"\\\n" +
						new string('x', LogServiceChannel.MaxLineLength) +
						"\n" +
						".\n"
					};

					// single very long line
					// => text is distributed over multiple lines using a line splitter sequence to concat the lines
					yield return new object[]
					{
						new string('x', 3 * LogServiceChannel.MaxLineLength + 1),
						offset,
						"text:\n" +
						new string('x', LogServiceChannel.MaxLineLength) +
						"\n" +
						"\\\n" +
						new string('x', LogServiceChannel.MaxLineLength) +
						"\n" +
						"\\\n" +
						new string('x', LogServiceChannel.MaxLineLength) +
						"\n" +
						"\\\n" +
						new string('x', 1) +
						"\n" +
						".\n"
					};

					// multiple lines, nothing special
					// => the text should be put in multiple lines and line breaks should be unified
					yield return new object[]
					{
						"xxx\r" +
						"xxx yyy\r\n" +
						"xxx yyy zzz",
						offset,
						"text:\n" +
						"xxx\n" +
						"xxx yyy\n" +
						"xxx yyy zzz\n" +
						".\n"
					};
				}
			}
		}

		[Theory]
		[MemberData(nameof(FormatWriteCommandTestData_Text))]
		public void FormatWriteCommand_Text(string text, int offset, string expected)
		{
			char[] buffer = new char[128 * 1024];
			int newOffset = LogServiceClientChannel.FormatWriteCommand_Text(buffer, offset, text);
			int length = newOffset - offset;
			string formatted = new string(buffer, offset, length);
			Assert.Equal(expected, formatted);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		public void FormatWriteCommand_Tags_BufferTooSmall_SingleLine_Short(int offset)
		{
			// the text should fit on a single line together with the field name "text: " (6 characters)
			// (one character less should trigger the check)
			string text = "Lorem ipsum dolor si"; // 20 chars
			char[] buffer = new char[6 + text.Length - 1];
			Assert.Throws<InsufficientBufferSizeException>(() => LogServiceClientChannel.FormatWriteCommand_Text(buffer, offset, text));
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		public void FormatWriteCommand_Tags_BufferTooSmall_SingleLine_Long(int offset)
		{
			// The text should not fit on a single line together with the field name "text: ".
			// A line break is inserted directly after the field name.
			string text = new string('x', LogServiceChannel.MaxLineLength);
			char[] buffer = new char[
				offset +      // << test offset >>
				5 +           // "text:"
				1 +           // <newline>
				text.Length + // <text>
				1 +           // <newline>
				1 +           // "."
				1 -           // <newline>
				1             // one character less should trigger the check
			];

			Assert.Throws<InsufficientBufferSizeException>(() => LogServiceClientChannel.FormatWriteCommand_Text(buffer, offset, text));
		}

		#endregion
	}

}
