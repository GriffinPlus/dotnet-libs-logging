///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018-2019 Sascha Falk <sascha@falk-online.eu>
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
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message processing pipeline stage that logs messages in a tabular format (thread-safe).
	/// </summary>
	public abstract partial class TextWriterPipelineStage<STAGE> : AsyncProcessingPipelineStage<STAGE>
		where STAGE: TextWriterPipelineStage<STAGE>
	{
		private List<ColumnBase> mColumns = new List<ColumnBase>();
		private IFormatProvider mFormatProvider = CultureInfo.InvariantCulture;
		private StringBuilder mOutputBuilder = new StringBuilder();
		private bool mDefaultColumnConfiguration = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="TextWriterPipelineStage{STAGE}"/> class.
		/// </summary>
		public TextWriterPipelineStage()
		{
			mColumns.Add(new TextColumn(this as STAGE));
			mColumns[mColumns.Count - 1].IsLastColumn = true;
		}

		/// <summary>
		/// Processes the specified log messages asynchronously (in the context of the asynchronous process thread of the pipeline stage).
		/// </summary>
		/// <param name="messages">Messages to process.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		/// <remarks>
		/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		protected override async Task ProcessAsync(LocalLogMessage[] messages, CancellationToken cancellationToken)
		{
			for (int i = 0; i < messages.Length; i++) {
				mOutputBuilder.Clear();
				FormatOutput(messages[i], mOutputBuilder);
				await EmitOutputAsync(messages[i], mOutputBuilder, cancellationToken);
			}
		}

		/// <summary>
		/// Formats the specified log message before emitting it.
		/// This method is called from within the pipeline stage lock.
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
		/// This method is called from within the pipeline stage lock (<see cref="AsyncProcessingPipelineStage{T}.Sync"/>).
		/// </summary>
		/// <param name="message">The current log message.</param>
		/// <param name="output">The formatted output of the current log message.</param>
		/// <param name="cancellationToken">Cancellation token that is signalled when the pipeline stage is shutting down.</param>
		protected abstract Task EmitOutputAsync(LocalLogMessage message, StringBuilder output, CancellationToken cancellationToken);

		/// <summary>
		/// Gets or sets the format provider to use when formatting log messages.
		/// By default <see cref="CultureInfo.InvariantCulture"/> is used.
		/// </summary>
		public IFormatProvider FormatProvider
		{
			get
			{
				lock (Sync) return mFormatProvider;
			}

			set
			{
				lock (Sync)
				{
					EnsureNotAttachedToLoggingSubsystem();
					mFormatProvider = value;
				}
			}
		}

		/// <summary>
		/// Adds a timestamp column and sets the format of the timestamps written to the console
		/// (default: "u", conversion to UTC and output using the format yyyy-MM-dd HH:mm:ssZ)
		/// </summary>
		/// <param name="format">
		/// The timestamp format (see https://msdn.microsoft.com/en-us/library/bb351892(v=vs.110).aspx" for details).
		/// </param>
		public void AddTimestampColumn(string format = "u")
		{
			if (format == null) throw new ArgumentNullException(nameof(format));
			DateTimeOffset.MinValue.ToString(format); // throws FormatException, if format is invalid

			lock (Sync)
			{
				EnsureNotAttachedToLoggingSubsystem();

				if (mDefaultColumnConfiguration) {
					mColumns.Clear();
					mDefaultColumnConfiguration = false;
				}

				TimestampColumn column = new TimestampColumn(this as STAGE, format);
				AppendColumn(column);
			}
		}

		/// <summary>
		/// Adds a column showing the id of the process that has written a log message.
		/// </summary>
		public void AddProcessIdColumn()
		{
			lock (Sync)
			{
				EnsureNotAttachedToLoggingSubsystem();

				if (mDefaultColumnConfiguration) {
					mColumns.Clear();
					mDefaultColumnConfiguration = false;
				}

				ProcessIdColumn column = new ProcessIdColumn(this as STAGE);
				AppendColumn(column);
			}
		}

		/// <summary>
		/// Adds a column showing the name of the process that has written a log message.
		/// </summary>
		public void AddProcessNameColumn()
		{
			lock (Sync)
			{
				EnsureNotAttachedToLoggingSubsystem();

				if (mDefaultColumnConfiguration) {
					mColumns.Clear();
					mDefaultColumnConfiguration = false;
				}

				ProcessNameColumn column = new ProcessNameColumn(this as STAGE);
				AppendColumn(column);
			}
		}

		/// <summary>
		/// Adds a column showing the name of the application that has written a log message.
		/// </summary>
		public void AddApplicationNameColumn()
		{
			lock (Sync)
			{
				EnsureNotAttachedToLoggingSubsystem();

				if (mDefaultColumnConfiguration) {
					mColumns.Clear();
					mDefaultColumnConfiguration = false;
				}

				ApplicationNameColumn column = new ApplicationNameColumn(this as STAGE);
				AppendColumn(column);
			}
		}

		/// <summary>
		/// Adds a column showing the name of the log writer that was used to write a log message.
		/// </summary>
		public void AddLogWriterColumn()
		{
			lock (Sync)
			{
				EnsureNotAttachedToLoggingSubsystem();

				if (mDefaultColumnConfiguration) {
					mColumns.Clear();
					mDefaultColumnConfiguration = false;
				}

				LogWriterColumn column = new LogWriterColumn(this as STAGE);
				AppendColumn(column);
			}
		}

		/// <summary>
		/// Adds a column showing the name of the log level that was used to write a log message.
		/// </summary>
		public void AddLogLevelColumn()
		{
			lock (Sync)
			{
				EnsureNotAttachedToLoggingSubsystem();

				if (mDefaultColumnConfiguration) {
					mColumns.Clear();
					mDefaultColumnConfiguration = false;
				}

				LogLevelColumn column = new LogLevelColumn(this as STAGE);
				AppendColumn(column);
			}
		}

		/// <summary>
		/// Adds a column showing the message text.
		/// </summary>
		public void AddTextColumn()
		{
			lock (Sync)
			{
				EnsureNotAttachedToLoggingSubsystem();

				if (mDefaultColumnConfiguration) {
					mColumns.Clear();
					mDefaultColumnConfiguration = false;
				}

				TextColumn column = new TextColumn(this as STAGE);
				AppendColumn(column);
			}
		}

		/// <summary>
		/// Adds the specified column to the end of the column collection.
		/// </summary>
		/// <param name="column">Column to add.</param>
		private void AppendColumn(ColumnBase column)
		{
			if (mColumns.Count > 0) mColumns[mColumns.Count - 1].IsLastColumn = false;
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
