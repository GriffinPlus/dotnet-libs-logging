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
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message processing pipeline stage that logs messages in a tabular format (thread-safe).
	/// </summary>
	public abstract partial class TextWriterPipelineStage<STAGE> : ProcessingPipelineStage<STAGE>
		where STAGE: TextWriterPipelineStage<STAGE>
	{
		private List<ColumnBase> mColumns = new List<ColumnBase>();
		private IFormatProvider mFormatProvider = CultureInfo.InvariantCulture;
		private StringBuilder mOutputBuilder = new StringBuilder();

		/// <summary>
		/// Initializes a new instance of the <see cref="TextWriterPipelineStage{T}"/> class.
		/// </summary>
		public TextWriterPipelineStage()
		{
			mColumns.Add(new TextColumn(this as STAGE));
			mColumns[mColumns.Count - 1].IsLastColumn = true;
		}

		/// <summary>
		/// Processes the specified log message and passes the log message to the next processing stages.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <remarks>
		/// Do not keep a reference to the passed log message object as it returns to a pool.
		/// Log message objects are re-used to reduce garbage collection pressure.
		/// </remarks>
		public override void Process(LocalLogMessage message)
		{
			lock (mSync)
			{
				mOutputBuilder.Clear();
				FormatOutput(message, mOutputBuilder);
				EmitOutput(message, mOutputBuilder);
			}

			// pass message to the next pipeline stages
			base.Process(message);
		}

		/// <summary>
		/// Formats the specified log message before emitting it.
		/// This method is called from within a locked block.
		/// </summary>
		/// <param name="message">Message to format.</param>
		/// <param name="output">String builder receiving the formatted message.</param>
		protected virtual void FormatOutput(LocalLogMessage message, StringBuilder output)
		{
			// update the width of the column needed to print the message
			foreach (var column in mColumns)
			{
				column.UpdateWidth(message);
			}

			// prepare output
			bool more = true;
			for (int line = 0; more; line++)
			{
				more = false;
				for (int i = 0; i < mColumns.Count; i++)
				{
					var column = mColumns[i];
					if (i > 0) output.Append(" | ");
					more |= column.Write(message, output, line);
				}

				output.Append(Environment.NewLine);
			}
		}

		/// <summary>
		/// Emits the formatted log message.
		/// This method is called from within a locked block.
		/// </summary>
		/// <param name="message">The current log message.</param>
		/// <param name="output">The formatted output of the current log message.</param>
		protected abstract void EmitOutput(LocalLogMessage message, StringBuilder output);

		/// <summary>
		/// Gets or sets the format provider to use when formatting log messages.
		/// By default <see cref="CultureInfo.InvariantCulture"/> is used.
		/// </summary>
		public IFormatProvider FormatProvider
		{
			get { lock (mSync) return mFormatProvider;  }
			set { lock (mSync) mFormatProvider = value; }
		}

		/// <summary>
		/// Enables the timestamp column and sets the format of the timestamps written to the console
		/// (default: "u", conversion to UTC and output using the format yyyy-MM-dd HH:mm:ssZ)
		/// </summary>
		/// <param name="format">
		/// The timestamp format (see https://msdn.microsoft.com/en-us/library/bb351892(v=vs.110).aspx" for details).
		/// </param>
		/// <returns>The modified pipeline stage.</returns>
		public STAGE WithTimestamp(string format = "u")
		{
			if (format == null) throw new ArgumentNullException(nameof(format));
			DateTimeOffset.MinValue.ToString(format); // throws FormatException, if format is invalid

			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is TimestampColumn);
				if (index < 0)
				{
					// column does not exist, yet
					// => append it...
					TimestampColumn column = new TimestampColumn(this as STAGE, format);
					AppendColumn(column);
				}
				else
				{
					// column exists already
					// => update and push to the end...
					TimestampColumn column = mColumns[index] as TimestampColumn;
					column.TimestampFormat = format;
					MoveColumnToEnd(index);
				}
			}

			return this as STAGE;
		}

		/// <summary>
		/// Enables the 'process id' column.
		/// </summary>
		/// <returns>The modified pipeline stage.</returns>
		public STAGE WithProcessId()
		{
			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is ProcessIdColumn);
				if (index < 0)
				{
					// column does not exist, yet
					// => append it...
					ProcessIdColumn column = new ProcessIdColumn(this as STAGE);
					AppendColumn(column);
				}
				else
				{
					// column exists already
					// => update and push to the end...
					MoveColumnToEnd(index);
				}
			}

			return this as STAGE;
		}

		/// <summary>
		/// Enables the 'process name' column.
		/// </summary>
		/// <returns>The modified pipeline stage.</returns>
		public STAGE WithProcessName()
		{
			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is ProcessNameColumn);
				if (index < 0)
				{
					// column does not exist, yet
					// => append it...
					ProcessNameColumn column = new ProcessNameColumn(this as STAGE);
					AppendColumn(column);
				}
				else
				{
					// column exists already
					// => update and push to the end...
					MoveColumnToEnd(index);
				}
			}

			return this as STAGE;
		}

		/// <summary>
		/// Enables the 'application name' column.
		/// </summary>
		/// <returns>The modified pipeline stage.</returns>
		public STAGE WithApplicationName()
		{
			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is ApplicationNameColumn);
				if (index < 0)
				{
					// column does not exist, yet
					// => append it...
					ApplicationNameColumn column = new ApplicationNameColumn(this as STAGE);
					AppendColumn(column);
				}
				else
				{
					// column exists already
					// => update and push to the end...
					MoveColumnToEnd(index);
				}
			}

			return this as STAGE;
		}

		/// <summary>
		/// Enables the 'log writer' column.
		/// </summary>
		/// <returns>The modified pipeline stage.</returns>
		public STAGE WithLogWriterName()
		{
			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is LogWriterColumn);
				if (index < 0)
				{
					// column does not exist, yet
					// => append it...
					LogWriterColumn column = new LogWriterColumn(this as STAGE);
					AppendColumn(column);
				}
				else
				{
					// column exists already
					// => update and push to the end...
					MoveColumnToEnd(index);
				}
			}

			return this as STAGE;
		}

		/// <summary>
		/// Enables the 'log level' column.
		/// </summary>
		/// <returns>The modified pipeline stage.</returns>
		public STAGE WithLogLevel()
		{
			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is LogLevelColumn);
				if (index < 0)
				{
					// column does not exist, yet
					// => append it...
					LogLevelColumn column = new LogLevelColumn(this as STAGE);
					AppendColumn(column);
				}
				else
				{
					// column exists already
					// => update and push to the end...
					MoveColumnToEnd(index);
				}
			}

			return this as STAGE;
		}

		/// <summary>
		/// Enables the 'message text' column.
		/// </summary>
		/// <returns>The modified pipeline stage.</returns>
		public STAGE WithText()
		{
			lock (mSync)
			{
				int index = mColumns.FindIndex(x => x is TextColumn);
				if (index < 0)
				{
					// column does not exist, yet
					// => append it...
					TextColumn column = new TextColumn(this as STAGE);
					AppendColumn(column);
				}
				else
				{
					// column exists already
					// => update and push to the end...
					MoveColumnToEnd(index);
				}
			}

			return this as STAGE;
		}

		/// <summary>
		/// Adds the specified column to the end of the column collection.
		/// </summary>
		/// <param name="column">Column to add.</param>
		private void AppendColumn(ColumnBase column)
		{
			mColumns[mColumns.Count - 1].IsLastColumn = false;
			mColumns.Add(column);
			column.IsLastColumn = true;
		}

		/// <summary>
		/// Moves the column at the specified index to the end of the column collection.
		/// </summary>
		/// <param name="index">Index of the column to move.</param>
		private void MoveColumnToEnd(int index)
		{
			ColumnBase column = mColumns[index];

			// abort, if the column is already the last column
			if (index + 1 == mColumns.Count) {
				return;
			}

			mColumns[mColumns.Count - 1].IsLastColumn = false;
			mColumns.RemoveAt(index);
			mColumns.Add(column);
			column.IsLastColumn = true;
		}

	}
}
