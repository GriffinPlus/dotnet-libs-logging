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
using System.Globalization;
using System.Linq;
using Xunit;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Unit tests targeting the <see cref="JsonMessageFormatter"/> class.
	/// </summary>
	public class JsonMessageFormatterTests
	{
		/// <summary>
		/// Tests whether the creation of the formatter succeeds.
		/// </summary>
		[Fact]
		public void Create()
		{
			var formatter = new JsonMessageFormatter();
			Assert.Equal(CultureInfo.InvariantCulture, formatter.FormatProvider);
			Assert.Equal("    ", formatter.Indent);
			Assert.Equal(JsonMessageFormatterStyle.OneLine, formatter.Style);

			// the formatter should not contain any fields at start
			// => the output should be an empty JSON document
			var output = formatter.Format(new LogMessage());
			Assert.Equal("{ }", output);
		}

		public static IEnumerable<object[]> FormatTestData
		{
			get
			{
				var message = new LogMessage()
				{
					Timestamp = DateTimeOffset.Parse("2000-01-01 00:00:00Z"),
					HighAccuracyTimestamp = 123,
					ProcessName = "MyProcess",
					ProcessId = 42,
					ApplicationName = "MyApp",
					LogLevelName = "MyLevel",
					LogWriterName = "MyWriter",
					Text = "MyText"
				};

				// ------------------------------------------------------------------------
				// style: compact
				// ------------------------------------------------------------------------

				yield return new object[] {
					JsonMessageFormatterStyle.Compact,
					LogMessageField.None,
					message,
					"{}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Compact,
					LogMessageField.Timestamp,
					message,
					"{\"Timestamp\":\"2000-01-01 00:00:00Z\"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Compact,
					LogMessageField.HighAccuracyTimestamp,
					message,
					"{\"HighAccuracyTimestamp\":123}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Compact,
					LogMessageField.LogLevelName,
					message,
					"{\"LogLevel\":\"MyLevel\"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Compact,
					LogMessageField.LogWriterName,
					message,
					"{\"LogWriter\":\"MyWriter\"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Compact,
					LogMessageField.ProcessId,
					message,
					"{\"ProcessId\":42}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Compact,
					LogMessageField.ProcessName,
					message,
					"{\"ProcessName\":\"MyProcess\"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Compact,
					LogMessageField.ApplicationName,
					message,
					"{\"ApplicationName\":\"MyApp\"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Compact,
					LogMessageField.Text,
					message,
					"{\"Text\":\"MyText\"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Compact,
					LogMessageField.All,
					message,
					"{" +
					"\"Timestamp\":\"2000-01-01 00:00:00Z\"," +
					"\"HighAccuracyTimestamp\":123," +
					"\"LogLevel\":\"MyLevel\"," +
					"\"LogWriter\":\"MyWriter\"," +
					"\"ProcessId\":42," +
					"\"ProcessName\":\"MyProcess\"," +
					"\"ApplicationName\":\"MyApp\"," +
					"\"Text\":\"MyText\"" +
					"}"
				};

				// ------------------------------------------------------------------------
				// style: one line
				// ------------------------------------------------------------------------

				yield return new object[] {
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.None,
					message,
					"{ }"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.Timestamp,
					message,
					"{ \"Timestamp\" : \"2000-01-01 00:00:00Z\" }"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.HighAccuracyTimestamp,
					message,
					"{ \"HighAccuracyTimestamp\" : 123 }"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.LogLevelName,
					message,
					"{ \"LogLevel\" : \"MyLevel\" }"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.LogWriterName,
					message,
					"{ \"LogWriter\" : \"MyWriter\" }"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.ProcessId,
					message,
					"{ \"ProcessId\" : 42 }"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.ProcessName,
					message,
					"{ \"ProcessName\" : \"MyProcess\" }"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.ApplicationName,
					message,
					"{ \"ApplicationName\" : \"MyApp\" }"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.Text,
					message,
					"{ \"Text\" : \"MyText\" }"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.OneLine,
					LogMessageField.All,
					message,
					"{" +
					" \"Timestamp\" : \"2000-01-01 00:00:00Z\"," +
					" \"HighAccuracyTimestamp\" : 123," +
					" \"LogLevel\" : \"MyLevel\"," +
					" \"LogWriter\" : \"MyWriter\"," +
					" \"ProcessId\" : 42," +
					" \"ProcessName\" : \"MyProcess\"," +
					" \"ApplicationName\" : \"MyApp\"," +
					" \"Text\" : \"MyText\"" +
					" }"
				};

				// ------------------------------------------------------------------------
				// style: beautified
				// ------------------------------------------------------------------------

				yield return new object[] {
					JsonMessageFormatterStyle.Beautified,
					LogMessageField.None,
					message,
					"{\r\n" +
					"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Beautified,
					LogMessageField.Timestamp,
					message,
					"{\r\n" +
					"    \"Timestamp\" : \"2000-01-01 00:00:00Z\"\r\n" +
					"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Beautified,
					LogMessageField.HighAccuracyTimestamp,
					message,
					"{\r\n" +
					"    \"HighAccuracyTimestamp\" : 123\r\n" +
					"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Beautified,
					LogMessageField.LogLevelName,
					message,
					"{\r\n" + 
					"    \"LogLevel\" : \"MyLevel\"\r\n" +
					"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Beautified,
					LogMessageField.LogWriterName,
					message,
					"{\r\n" +
					"    \"LogWriter\" : \"MyWriter\"\r\n" +
					"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Beautified,
					LogMessageField.ProcessId,
					message,
					"{\r\n" + 
					"    \"ProcessId\" : 42\r\n" +
					"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Beautified,
					LogMessageField.ProcessName,
					message,
					"{\r\n" +
					"    \"ProcessName\" : \"MyProcess\"\r\n" +
					"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Beautified,
					LogMessageField.ApplicationName,
					message,
					"{\r\n" + 
					"    \"ApplicationName\" : \"MyApp\"\r\n" +
					"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Beautified,
					LogMessageField.Text,
					message,
					"{\r\n" +
					"    \"Text\" : \"MyText\"\r\n" +
					"}"
				};

				yield return new object[] {
					JsonMessageFormatterStyle.Beautified,
					LogMessageField.All,
					message,
					"{\r\n" +
					"    \"Timestamp\"             : \"2000-01-01 00:00:00Z\",\r\n" +
					"    \"HighAccuracyTimestamp\" : 123,\r\n" +
					"    \"LogLevel\"              : \"MyLevel\",\r\n" +
					"    \"LogWriter\"             : \"MyWriter\",\r\n" +
					"    \"ProcessId\"             : 42,\r\n" +
					"    \"ProcessName\"           : \"MyProcess\",\r\n" +
					"    \"ApplicationName\"       : \"MyApp\",\r\n" +
					"    \"Text\"                  : \"MyText\"\r\n" +
					"}"
				};

			}
		}
		/// <summary>
		/// Tests whether formatting specific fields works as expected.
		/// </summary>
		[Theory]
		[MemberData(nameof(FormatTestData))]
		public void Format(JsonMessageFormatterStyle style, LogMessageField fields, LogMessage message, string expected)
		{
			var formatter = new JsonMessageFormatter();
			formatter.Style = style;

			if (fields.HasFlag(LogMessageField.Timestamp)) formatter.AddTimestampField();
			if (fields.HasFlag(LogMessageField.HighAccuracyTimestamp)) formatter.AddHighAccuracyTimestampField();
			if (fields.HasFlag(LogMessageField.LogLevelName)) formatter.AddLogLevelField();
			if (fields.HasFlag(LogMessageField.LogWriterName)) formatter.AddLogWriterField();
			if (fields.HasFlag(LogMessageField.ProcessId)) formatter.AddProcessIdField();
			if (fields.HasFlag(LogMessageField.ProcessName)) formatter.AddProcessNameField();
			if (fields.HasFlag(LogMessageField.ApplicationName)) formatter.AddApplicationNameField();
			if (fields.HasFlag(LogMessageField.Text)) formatter.AddTextField();

			var output = formatter.Format(message);
			Assert.Equal(expected, output);
		}
	}
}
