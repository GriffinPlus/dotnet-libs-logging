﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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
			var output = formatter.Format(new LogMessage());
			Assert.Equal("", output);
		}

		public static IEnumerable<object[]> FormatTestData
		{
			get
			{
				var message = GetTestMessage();

				yield return new object[] {
					LogMessageField.None,
					message,
					""
				};

				yield return new object[] {
					LogMessageField.Timestamp,
					message,
					"2000-01-01 00:00:00Z"
				};

				yield return new object[] {
					LogMessageField.HighAccuracyTimestamp,
					message,
					"123"
				};

				yield return new object[] {
					LogMessageField.LogWriterName,
					message,
					"MyWriter"
				};

				yield return new object[] {
					LogMessageField.LogLevelName,
					message,
					"MyLevel"
				};

				yield return new object[] {
					LogMessageField.ApplicationName,
					message,
					"MyApp"
				};

				yield return new object[] {
					LogMessageField.ProcessName,
					message,
					"MyProcess"
				};

				yield return new object[] {
					LogMessageField.ProcessId,
					message,
					"42"
				};

				yield return new object[] {
					LogMessageField.Text,
					message,
					"MyText"
				};

				yield return new object[] {
					LogMessageField.All,
					message,
					"2000-01-01 00:00:00Z | 123 | MyWriter | MyLevel | MyApp | MyProcess | 42 | MyText"
				};

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
			if (fields.HasFlag(LogMessageField.HighAccuracyTimestamp)) formatter.AddHighAccuracyTimestampColumn();
			if (fields.HasFlag(LogMessageField.LogWriterName)) formatter.AddLogWriterColumn();
			if (fields.HasFlag(LogMessageField.LogLevelName)) formatter.AddLogLevelColumn();
			if (fields.HasFlag(LogMessageField.ApplicationName)) formatter.AddApplicationNameColumn();
			if (fields.HasFlag(LogMessageField.ProcessName)) formatter.AddProcessNameColumn();
			if (fields.HasFlag(LogMessageField.ProcessId)) formatter.AddProcessIdColumn();
			if (fields.HasFlag(LogMessageField.Text)) formatter.AddTextColumn();

			Assert.Equal(fields, formatter.FormattedFields);

			var output = formatter.Format(message);
			Assert.Equal(expected, output);
		}

		/// <summary>
		/// Tests whether the <see cref="TableMessageFormatter.AllColumns"/> property returns the correct formatter.
		/// </summary>
		[Fact]
		public void AllColumns()
		{
			var formatter = TableMessageFormatter.AllColumns;
			var expectedFields = LogMessageField.Timestamp | LogMessageField.LogWriterName | LogMessageField.LogLevelName | LogMessageField.ApplicationName | LogMessageField.ProcessName | LogMessageField.ProcessId | LogMessageField.Text;
			Assert.Equal(expectedFields, formatter.FormattedFields);
			var message = GetTestMessage();
			var output = formatter.Format(message);
			Assert.Equal("2000-01-01 00:00:00Z | MyWriter | MyLevel | MyApp | MyProcess | 42 | MyText", output);
		}

		/// <summary>
		/// Gets a log message with test data.
		/// </summary>
		/// <returns>A log message with test data.</returns>
		private static LogMessage GetTestMessage()
		{
			return new LogMessage()
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
		}
	}
}