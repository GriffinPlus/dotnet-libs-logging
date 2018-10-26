﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message processing pipeline stage that logs messages to stdout/stderr (thread-safe).
	/// </summary>
	public partial class ConsoleWriterPipelineStage : ProcessingPipelineStage<ConsoleWriterPipelineStage>
	{
		private List<ColumnBase> mColumns = new List<ColumnBase>();
		private CultureInfo mCultureInfo = CultureInfo.InvariantCulture;
		private StringBuilder mOutputBuilder = new StringBuilder();

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleWriterPipelineStage"/> class.
		/// </summary>
		public ConsoleWriterPipelineStage()
		{
			mColumns.Add(new TextColumn(this));
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
			lock (mSync)
			{
				// update the width of the column needed to print the message
				foreach (var column in mColumns)
				{
					column.UpdateWidth(message);
				}

				// prepare output
				mOutputBuilder.Clear();
				bool more = true;
				for (int line = 0; more; line++)
				{
					more = false;
					for (int i = 0; i < mColumns.Count; i++)
					{
						var column = mColumns[i];
						if (i > 0) mOutputBuilder.Append(" | ");
						more |= column.Write(message, mOutputBuilder, line);
					}

					mOutputBuilder.Append(Environment.NewLine);
				}

				// write message to the console
				if (message.LogLevel == LogLevel.Failure || message.LogLevel == LogLevel.Error)
				{
					Console.Error.Write(mOutputBuilder.ToString());
				}
				else
				{
					Console.Out.Write(mOutputBuilder.ToString());
				}
			}

			// pass message to the next pipeline stages
			base.Process(message);
		}

		/// <summary>
		/// Enables the timestamp column and sets the format of the timestamps written to the console
		/// (default: "u", conversion to UTC and output using the format yyyy-MM-dd HH:mm:ssZ)
		/// </summary>
		/// <param name="format">
		/// The timestamp format (see https://msdn.microsoft.com/en-us/library/bb351892(v=vs.110).aspx" for details).
		/// </param>
		/// <returns>The modified pipeline stage.</returns>
		public ConsoleWriterPipelineStage WithTimestamp(string format = "u")
		{
			if (format == null) throw new ArgumentNullException(nameof(format));
			DateTimeOffset.MinValue.ToString(format); // throws FormatException, if format is invalid

			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is TimestampColumn);
				if (index < 0)
				{
					// column does not exist, yet
					TimestampColumn column = new TimestampColumn(this, format);
					mColumns.Add(column);
				}
				else
				{
					// column exists already => update and push to the end.
					TimestampColumn column = mColumns[index] as TimestampColumn;
					column.TimestampFormat = format;
					if (index + 1 != mColumns.Count)
					{
						mColumns.RemoveAt(index);
						mColumns.Add(column);
					}
				}
			}

			return this;
		}

		/// <summary>
		/// Enables the 'process id' column.
		/// </summary>
		/// <returns>The modified pipeline stage.</returns>
		public ConsoleWriterPipelineStage WithProcessId()
		{
			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is ProcessIdColumn);
				if (index < 0)
				{
					// column does not exist, yet
					ProcessIdColumn column = new ProcessIdColumn(this);
					mColumns.Add(column);
				}
				else
				{
					// column exists already => update and push to the end.
					ProcessIdColumn column = mColumns[index] as ProcessIdColumn;
					if (index + 1 != mColumns.Count)
					{
						mColumns.RemoveAt(index);
						mColumns.Add(column);
					}
				}
			}

			return this;
		}

		/// <summary>
		/// Enables the 'process name' column.
		/// </summary>
		/// <returns>The modified pipeline stage.</returns>
		public ConsoleWriterPipelineStage WithProcessName()
		{
			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is ProcessNameColumn);
				if (index < 0)
				{
					// column does not exist, yet
					ProcessNameColumn column = new ProcessNameColumn(this);
					mColumns.Add(column);
				}
				else
				{
					// column exists already => update and push to the end.
					ProcessNameColumn column = mColumns[index] as ProcessNameColumn;
					if (index + 1 != mColumns.Count)
					{
						mColumns.RemoveAt(index);
						mColumns.Add(column);
					}
				}
			}

			return this;
		}

		/// <summary>
		/// Enables the 'application name' column.
		/// </summary>
		/// <returns>The modified pipeline stage.</returns>
		public ConsoleWriterPipelineStage WithApplicationName()
		{
			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is ApplicationNameColumn);
				if (index < 0)
				{
					// column does not exist, yet
					ApplicationNameColumn column = new ApplicationNameColumn(this);
					mColumns.Add(column);
				}
				else
				{
					// column exists already => update and push to the end.
					ApplicationNameColumn column = mColumns[index] as ApplicationNameColumn;
					if (index + 1 != mColumns.Count)
					{
						mColumns.RemoveAt(index);
						mColumns.Add(column);
					}
				}
			}

			return this;
		}

		/// <summary>
		/// Enables the 'log writer' column.
		/// </summary>
		/// <returns>The modified pipeline stage.</returns>
		public ConsoleWriterPipelineStage WithLogWriterName()
		{
			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is LogWriterColumn);
				if (index < 0)
				{
					// column does not exist, yet
					LogWriterColumn column = new LogWriterColumn(this);
					mColumns.Add(column);
				}
				else
				{
					// column exists already => update and push to the end.
					LogWriterColumn column = mColumns[index] as LogWriterColumn;
					if (index + 1 != mColumns.Count)
					{
						mColumns.RemoveAt(index);
						mColumns.Add(column);
					}
				}
			}

			return this;
		}

		/// <summary>
		/// Enables the 'log level' column.
		/// </summary>
		/// <returns>The modified pipeline stage.</returns>
		public ConsoleWriterPipelineStage WithLogLevel()
		{
			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is LogLevelColumn);
				if (index < 0)
				{
					// column does not exist, yet
					LogLevelColumn column = new LogLevelColumn(this);
					mColumns.Add(column);
				}
				else
				{
					// column exists already => update and push to the end.
					LogLevelColumn column = mColumns[index] as LogLevelColumn;
					if (index + 1 != mColumns.Count)
					{
						mColumns.RemoveAt(index);
						mColumns.Add(column);
					}
				}
			}

			return this;
		}

		/// <summary>
		/// Enables the 'message text' column.
		/// </summary>
		/// <returns>The modified pipeline stage.</returns>
		public ConsoleWriterPipelineStage WithText()
		{
			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is TextColumn);
				if (index < 0)
				{
					// column does not exist, yet
					TextColumn column = new TextColumn(this);
					mColumns.Add(column);
				}
				else
				{
					// column exists already => update and push to the end.
					TextColumn column = mColumns[index] as TextColumn;
					if (index + 1 != mColumns.Count)
					{
						mColumns.RemoveAt(index);
						mColumns.Add(column);
					}
				}
			}

			return this;
		}

	}
}
