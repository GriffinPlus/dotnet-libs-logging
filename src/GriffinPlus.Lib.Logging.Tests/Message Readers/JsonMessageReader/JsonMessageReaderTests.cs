///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Unit tests targeting the <see cref="JsonMessageReader"/> class.
	/// </summary>
	public class JsonMessageReaderTests
	{
		/// <summary>
		/// Tests whether the creation of the reader succeeds.
		/// </summary>
		[Fact]
		void Create()
		{
			var reader = new JsonMessageReader();
			Assert.Equal("u", reader.TimestampFormat);
			Assert.Equal("Timestamp", reader.FieldNames.Timestamp);
			Assert.Equal("HighPrecisionTimestamp", reader.FieldNames.HighPrecisionTimestamp);
			Assert.Equal("LogWriter", reader.FieldNames.LogWriter);
			Assert.Equal("LogLevel", reader.FieldNames.LogLevel);
			Assert.Equal("Tags", reader.FieldNames.Tags);
			Assert.Equal("ApplicationName", reader.FieldNames.ApplicationName);
			Assert.Equal("ProcessName", reader.FieldNames.ProcessName);
			Assert.Equal("ProcessId", reader.FieldNames.ProcessId);
			Assert.Equal("Text", reader.FieldNames.Text);
		}

		public static IEnumerable<object[]> ProcessTestData_SingleMessage
		{
			get
			{
				// use test data of the json message formatter to test reading log messages
				foreach (var data in JsonMessageFormatterTests.FormatTestData)
				{
					// JsonMessageFormatterStyle style = (JsonMessageFormatterStyle) data[0];
					LogMessageField field = (LogMessageField) data[1];
					LogMessage message = (LogMessage) data[2];
					string newline = (string) data[3]; // character sequence used to inject line breaks
					string json = (string) data[4];

					LogMessage expected = new LogMessage();
					if (field.HasFlag(LogMessageField.Timestamp)) expected.Timestamp = message.Timestamp;
					if (field.HasFlag(LogMessageField.HighPrecisionTimestamp)) expected.HighPrecisionTimestamp = message.HighPrecisionTimestamp;
					if (field.HasFlag(LogMessageField.LogWriterName)) expected.LogWriterName = message.LogWriterName;
					if (field.HasFlag(LogMessageField.LogLevelName)) expected.LogLevelName = message.LogLevelName;
					if (field.HasFlag(LogMessageField.Tags)) expected.Tags = message.Tags;
					if (field.HasFlag(LogMessageField.ApplicationName)) expected.ApplicationName = message.ApplicationName;
					if (field.HasFlag(LogMessageField.ProcessName)) expected.ProcessName = message.ProcessName;
					if (field.HasFlag(LogMessageField.ProcessId)) expected.ProcessId = message.ProcessId;
					if (field.HasFlag(LogMessageField.Text)) expected.Text = message.Text;

					yield return new object[] { json, newline, expected };
				}
			}
		}

		/// <summary>
		/// Tests processing single log messages that are passed to the reader at once.
		/// </summary>
		/// <param name="json">JSON document containing the log message to read.</param>
		/// <param name="expected">The log message that is expected to be returned.</param>
		[Theory]
		[MemberData(nameof(ProcessTestData_SingleMessage))]
		void Process_SingleMessage_AllAtOnce(string json, string _, LogMessage expected)
		{
			var reader = new JsonMessageReader();
			var messages = reader.Process(json);
			Assert.Single(messages);
			Assert.Equal(expected, messages[0]);
		}

		/// <summary>
		/// Tests processing single log messages that are passed to the reader character wise.
		/// </summary>
		/// <param name="json">JSON document containing the log message to read.</param>
		/// <param name="expected">The log message that is expected to be returned.</param>
		[Theory]
		[MemberData(nameof(ProcessTestData_SingleMessage))]
		void Process_SingleMessage_CharWise(string json, string _, LogMessage expected)
		{
			JsonMessageReader reader = new JsonMessageReader();
			ILogMessage[] messages;

			// pass all characters except the last one (that would complete the message) to the reader
			for (int i = 0; i < json.Length - 1; i++)
			{
				messages = reader.Process(json[i].ToString());
				Assert.Empty(messages);
			}

			// pass the last character into the reader completing the message
			messages = reader.Process(json[json.Length-1].ToString());
			Assert.Single(messages);
			Assert.Equal(expected, messages[0]);
		}

		/// <summary>
		/// Gets a test data set with JSON documents combined from data returned by <see cref="ProcessTestData_SingleMessage"/>.
		/// </summary>
		/// <param name="testSetCount">Number of test data sets.</param>
		/// <param name="minMessageCount">Minimum number of messages in a test data set.</param>
		/// <param name="maxMessageCount">Maximum number of messages in a test data set.</param>
		/// <param name="newline">Character sequence to use for line breaks (null to allow everything in the test set).</param>
		/// <param name="injectRandomWhiteSpaceBetweenMessages">
		/// true to inject a random whitespace between log messages;
		/// false to inject a newline character sequence only.
		/// </param>
		/// <returns>
		/// The test data sets.
		/// The tuples contain the combined JSON string and the log messages the string represents.
		/// </returns>
		public static IEnumerable<Tuple<string, ILogMessage[], HashSet<int>>> GetTestData(
			int testSetCount = 100000,
			int minMessageCount = 1,
			int maxMessageCount = 30,
			string newline = null,
			bool injectRandomWhiteSpaceBetweenMessages = true)
		{
			var data = ProcessTestData_SingleMessage
				.Where(x => newline == null || (string)x[1] == null || (string)x[1] == newline) // get only test sets with the desired line break character sequence
				.Select(x => new Tuple<string, LogMessage>((string)x[0], (LogMessage)x[2]))
				.ToArray();

			Random random = new Random(0);
			StringBuilder json = new StringBuilder();
			List<LogMessage> expectedMessages = new List<LogMessage>();
			HashSet<int> endIndexOfLogMessages = new HashSet<int>();
			for (int run = 0; run < testSetCount; run++)
			{
				json.Clear();
				expectedMessages.Clear();
				endIndexOfLogMessages.Clear();
				int messageCount = random.Next(minMessageCount, maxMessageCount);
				
				for (int j = 0; j < messageCount; j++)
				{
					int selectedMessageIndex = random.Next(0, data.Length - 1);
					json.Append(data[selectedMessageIndex].Item1);
					endIndexOfLogMessages.Add(json.Length - 1);

					if (injectRandomWhiteSpaceBetweenMessages) {
						json.Append(JsonTokenizerTests.WhiteSpaceCharacters[random.Next(0, JsonTokenizerTests.WhiteSpaceCharacters.Length - 1)]);
					} else {
						json.Append(newline ?? "\n");
					}

					expectedMessages.Add(data[selectedMessageIndex].Item2);
				}

				yield return new Tuple<string, ILogMessage[], HashSet<int>>(
					json.ToString(),
					expectedMessages.Cast<ILogMessage>().ToArray(),
					endIndexOfLogMessages);
			}
		}

		/// <summary>
		/// Tests processing multiple log messages that are passed to the reader at once.
		/// </summary>
		[Fact]
		void Process_MultipleMessages_AllAtOnce()
		{
			JsonMessageReader reader = new JsonMessageReader();

			foreach (var data in GetTestData(100000, 2, 30, null, true))
			{
				string json = data.Item1;
				ILogMessage[] expectedMessages = data.Item2;
				var messages = reader.Process(json);
				Assert.Equal(expectedMessages.Length, messages.Length);
				Assert.Equal(expectedMessages, messages);
			}

			reader.Reset();
		}

		/// <summary>
		/// Tests processing multiple log messages that are passed to the reader character wise.
		/// </summary>
		[Fact]
		void Process_MultipleMessages_CharWise()
		{
			JsonMessageReader reader = new JsonMessageReader();

			foreach (var data in GetTestData(100000, 2, 30, null, true))
			{
				string json = data.Item1;
				ILogMessage[] expectedMessages = data.Item2;
				HashSet<int> endIndexOfLogMessages = data.Item3;
				int messageNumber = 0;
				for (int i = 0; i < json.Length; i++)
				{
					var readMessages = reader.Process(json[i].ToString());

					if (endIndexOfLogMessages.Contains(i))
					{
						// message should have been read successfully at this point
						Assert.Single(readMessages);
						Assert.Equal(expectedMessages[messageNumber++], readMessages[0]);
					}
					else
					{
						// message is still incomplete
						Assert.Empty(readMessages);
					}
				}

				reader.Reset();
			}
		}

	}
}
