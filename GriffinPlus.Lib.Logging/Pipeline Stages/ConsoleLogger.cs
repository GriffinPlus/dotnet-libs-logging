///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://griffin.plus)
//
// Copyright 2018 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Globalization;
using System.Text;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message processing pipeline stage that logs messages to stdout/stderr (thread-safe).
	/// </summary>
	public class ConsoleLogger : LogMessageProcessingPipelineStage<ConsoleLogger>
	{
		private string mTimestampFormat = "u"; // conversion to UTC and output using the format yyyy-MM-dd HH:mm:ssZ.
		private string mFormatWithoutMessage;  // combined format string fot the log message without the message text
		private string mLineFormat;            // combined format string for the entire log message
		private CultureInfo mCultureInfo = CultureInfo.InvariantCulture;
		private StringBuilder mLineBuilder = new StringBuilder();
		private int mTimestampMaxLength;
		private int mLogWriterMaxLength;
		private int mLogLevelMaxLength;

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleLogger"/> class.
		/// </summary>
		public ConsoleLogger()
		{
			UpdateFormat();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleLogger"/> class by copying an existing instance.
		/// </summary>
		public ConsoleLogger(ConsoleLogger other) : base(other)
		{
			this.mTimestampFormat = other.mTimestampFormat;
			UpdateFormat();
		}

		/// <summary>
		/// Processes the specified log message and passes the log message to the next processing stages.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <remarks>
		/// Do not keep a reference to the passed log message object as it returns to a pool.
		/// Log message objects are re-used to reduce garbage collection pressure.
		/// </remarks>
		public override void Process(LogMessage message)
		{
			string text;

			lock (mSync)
			{
				// update paddings
				int timestampLength = message.Timestamp.ToString(mTimestampFormat, mCultureInfo).Length;
				int logWriterLength = message.LogWriter.Name.Length;
				int logLevelLength = message.LogLevel.Name.Length;
				if (timestampLength > mTimestampMaxLength || logWriterLength > mLogWriterMaxLength || logLevelLength > mLogLevelMaxLength)
				{
					mTimestampMaxLength = Math.Max(mTimestampMaxLength, timestampLength);
					mLogWriterMaxLength = Math.Max(mLogWriterMaxLength, logWriterLength);
					mLogLevelMaxLength = Math.Max(mLogLevelMaxLength, logLevelLength);
					UpdateFormat();
				}

				// build line to write to the console
				int indent = -1;
				mLineBuilder.Clear();
				var messageLines = message.Text.Replace("\r", "").Split('\n');
				for (int i = 0; i < messageLines.Length; i++)
				{
					if (i == 0)
					{
						mLineBuilder.AppendFormat(mCultureInfo, mLineFormat, message.Timestamp, message.LogWriter.Name, message.LogLevel.Name, messageLines[i]);
						mLineBuilder.AppendLine();
					}
					else
					{
						if (indent < 0) {
							indent = string.Format(mCultureInfo, mFormatWithoutMessage, message.Timestamp, message.LogWriter.Name, message.LogLevel.Name).Length;
						}
						mLineBuilder.Append(' ', indent);
						mLineBuilder.AppendLine(messageLines[i]);
					}
				}

				text = mLineBuilder.ToString();
			}

			// write message to the console
			if (message.LogLevel == LogLevel.Failure || message.LogLevel == LogLevel.Error)
			{
				Console.Error.Write(text);
			}
			else
			{
				Console.Out.Write(text);
			}

			// pass message to the next pipeline stages
			base.Process(message);
		}

		/// <summary>
		/// Sets the format of the timestamps written to the console
		/// (default: "u", conversion to UTC and output using the format yyyy-MM-dd HH:mm:ssZ)
		/// </summary>
		/// <param name="format">
		/// The timestamp format (see https://msdn.microsoft.com/en-us/library/bb351892(v=vs.110).aspx" for details).
		/// </param>
		/// <returns>A new pipeline stage of the same type containing the update.</returns>
		public ConsoleLogger WithTimestampFormat(string format)
		{
			if (format == null) throw new ArgumentNullException(nameof(format));
			DateTimeOffset.MinValue.ToString(format); // throws FormatException, if format is invalid

			lock (mSync)
			{
				mTimestampFormat = format;
				UpdateFormat();
			}

			return this;
		}

		/// <summary>
		/// Updates the format string that is used when printing to the console.
		/// </summary>
		private void UpdateFormat()
		{
			mFormatWithoutMessage = string.Format("{{0,-{0}:{1}}} | {{1,-{2}}} | {{2,-{3}}} | ", mTimestampMaxLength, mTimestampFormat, mLogWriterMaxLength, mLogLevelMaxLength);
			mLineFormat           = string.Format("{{0,-{0}:{1}}} | {{1,-{2}}} | {{2,-{3}}} | {{3}}", mTimestampMaxLength, mTimestampFormat, mLogWriterMaxLength, mLogLevelMaxLength);
		}
	}
}
