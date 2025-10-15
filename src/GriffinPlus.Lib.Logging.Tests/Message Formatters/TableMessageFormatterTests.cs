///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Globalization;

using Xunit;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Unit tests targeting the <see cref="TableMessageFormatter"/> class.
/// </summary>
public class TableMessageFormatterTests
{
	/// <summary>
	/// Tests whether the creation of the formatter succeeds.
	/// </summary>
	[Fact]
	public void Create()
	{
		var formatter = new TableMessageFormatter();
		Assert.Equal(CultureInfo.InvariantCulture, formatter.FormatProvider);
		Assert.Equal(LogMessageField.None, formatter.FormattedFields);

		// the formatter should not contain any columns at start
		// => the output should be an empty string
		string output = formatter.Format(new LogMessage());
		Assert.Equal("", output);
	}

	/// <summary>
	/// Test data for a plain text log message formatter, verifying field extraction and concatenation output.
	/// </summary>
	public static TheoryData<LogMessageField, LogMessage, string> FormatTestData
	{
		get
		{
			var data = new TheoryData<LogMessageField, LogMessage, string>();

			// Base message used for all variants (keeps local time handling as in the original)
			var message = new LogMessage
			{
				Timestamp = DateTimeOffset.Parse("2000-01-01 00:00:00Z"),
				HighPrecisionTimestamp = 123,
				LogWriterName = "MyWriter",
				Tags = new TagSet("Tag1", "Tag2"),
				LogLevelName = "MyLevel",
				ApplicationName = "MyApp",
				ProcessName = "MyProcess",
				ProcessId = 42,
				Text = "MyText"
			};

			// ------------------------------------------------------------------------
			// Individual field tests
			// ------------------------------------------------------------------------

			data.Add(LogMessageField.None, message, "");
			data.Add(LogMessageField.Timestamp, message, "2000-01-01 00:00:00Z");
			data.Add(LogMessageField.HighPrecisionTimestamp, message, "123");
			data.Add(LogMessageField.LogWriterName, message, "MyWriter");
			data.Add(LogMessageField.LogLevelName, message, "MyLevel");

			// Tags: empty
			data.Add(LogMessageField.Tags, new LogMessage(message) { Tags = new TagSet() }, "");

			// Tags: single
			message.Tags = new TagSet("Tag");
			data.Add(LogMessageField.Tags, new LogMessage(message) { Tags = new TagSet("Tag") }, "Tag");

			// Tags: multiple
			message.Tags = new TagSet("Tag1", "Tag2");
			data.Add(LogMessageField.Tags, new LogMessage(message) { Tags = new TagSet("Tag1", "Tag2") }, "Tag1, Tag2");

			// Remaining fields
			data.Add(LogMessageField.ApplicationName, message, "MyApp");
			data.Add(LogMessageField.ProcessName, message, "MyProcess");
			data.Add(LogMessageField.ProcessId, message, "42");
			data.Add(LogMessageField.Text, message, "MyText");

			// Combined case
			data.Add(
				LogMessageField.All,
				message,
				"2000-01-01 00:00:00Z | 123 | MyWriter | MyLevel | Tag1, Tag2 | MyApp | MyProcess | 42 | MyText");

			return data;
		}
	}

	/// <summary>
	/// Tests whether formatting specific fields works as expected.
	/// </summary>
	[Theory]
	[MemberData(nameof(FormatTestData))]
	public void Format(LogMessageField fields, LogMessage message, string expected)
	{
		var formatter = new TableMessageFormatter();

		if (fields.HasFlag(LogMessageField.Timestamp)) formatter.AddTimestampColumn();
		if (fields.HasFlag(LogMessageField.HighPrecisionTimestamp)) formatter.AddHighPrecisionTimestampColumn();
		if (fields.HasFlag(LogMessageField.LogWriterName)) formatter.AddLogWriterColumn();
		if (fields.HasFlag(LogMessageField.LogLevelName)) formatter.AddLogLevelColumn();
		if (fields.HasFlag(LogMessageField.Tags)) formatter.AddTagsColumn();
		if (fields.HasFlag(LogMessageField.ApplicationName)) formatter.AddApplicationNameColumn();
		if (fields.HasFlag(LogMessageField.ProcessName)) formatter.AddProcessNameColumn();
		if (fields.HasFlag(LogMessageField.ProcessId)) formatter.AddProcessIdColumn();
		if (fields.HasFlag(LogMessageField.Text)) formatter.AddTextColumn();

		Assert.Equal(fields, formatter.FormattedFields);

		string output = formatter.Format(message);
		Assert.Equal(expected, output);
	}

	/// <summary>
	/// Tests whether the <see cref="TableMessageFormatter.AllColumns"/> property returns the correct formatter.
	/// </summary>
	[Fact]
	public void AllColumns()
	{
		TableMessageFormatter formatter = TableMessageFormatter.AllColumns;
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
		string output = formatter.Format(message);
		Assert.Equal("2000-01-01 00:00:00Z | MyWriter | MyLevel | Tag1, Tag2 | MyApp | MyProcess | 42 | MyText", output);
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
