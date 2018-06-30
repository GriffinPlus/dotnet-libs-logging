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

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message processing pipeline stage that logs messages to stdout/stderr (thread-safe, since immutable).
	/// </summary>
	public class ConsoleLogger : LogMessageProcessingPipelineStage<ConsoleLogger>
	{
		private string mTimestampFormat = "u"; // conversion to UTC and output using the format yyyy-MM-dd HH:mm:ssZ.
		private string mFormat;                // combined format string for the entire log message

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
		/// Creates a copy of the current pipeline stage.
		/// </summary>
		/// <returns>A copy of the current pipeline stage.</returns>
		/// <remarks>
		/// This method must be overridden by each and every derived class to ensure that the correct type is created.
		/// The implementation should use the copy constructor to init base class members.
		/// </remarks>
		public override ConsoleLogger Dupe()
		{
			return new ConsoleLogger(this);
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
			// build line to write to the console
			var line = string.Format(mFormat, message.Timestamp, message.LogWriter.Name, message.LogLevel.Name, message.Text);

			// write message to the console
			if (message.LogLevel == LogLevel.Failure || message.LogLevel == LogLevel.Error) {
				Console.Error.WriteLine(line);
			} else {
				Console.Out.WriteLine(line);
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
			ConsoleLogger copy = Dupe();
			copy.mTimestampFormat = format;
			copy.UpdateFormat();
			return copy;
		}

		/// <summary>
		/// Updates the format string that is used when printing to the console.
		/// </summary>
		private void UpdateFormat()
		{
			mFormat = string.Format("{{0:{0}}} | {{1}} | {{2}} | {{3}}", mTimestampFormat);
		}
	}
}
