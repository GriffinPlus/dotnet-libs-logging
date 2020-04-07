﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
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
using System.Text;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message formatter that formats log messages in a tabular fashion.
	/// </summary>
	public partial class TableMessageFormatter : ILogMessageFormatter
	{
		private readonly List<ColumnBase> mColumns = new List<ColumnBase>();
		private readonly StringBuilder mOutputBuilder = new StringBuilder();
		private IFormatProvider mFormatProvider;
		private bool mDefaultColumnConfiguration = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="TableMessageFormatter"/> class.
		/// </summary>
		public TableMessageFormatter()
		{
			mColumns.Add(new TextColumn(this));
			mColumns[mColumns.Count - 1].IsLastColumn = true;
			mFormatProvider = CultureInfo.InvariantCulture;
		}

		/// <summary>
		/// Gets or sets the format provider to use when formatting log messages.
		/// By default <see cref="CultureInfo.InvariantCulture"/> is used.
		/// </summary>
		public IFormatProvider FormatProvider
		{
			get => mFormatProvider;
			set => mFormatProvider = value ?? throw new ArgumentNullException(nameof(value));
		}

		/// <summary>
		/// Formats the specified log message.
		/// </summary>
		/// <param name="message">Message to format.</param>
		/// <returns>The formatted log message.</returns>
		public string Format(ILogMessage message)
		{
			mOutputBuilder.Clear();

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
					if (i > 0) mOutputBuilder.Append(" | ");
					more |= column.Write(message, mOutputBuilder, line);
				}

				mOutputBuilder.Append(Environment.NewLine);
			}

			return mOutputBuilder.ToString();
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

			// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
			DateTimeOffset.MinValue.ToString(format); // throws FormatException, if format is invalid

			if (mDefaultColumnConfiguration)
			{
				mColumns.Clear();
				mDefaultColumnConfiguration = false;
			}

			TimestampColumn column = new TimestampColumn(this, format);
			AppendColumn(column);
		}

		/// <summary>
		/// Adds a column showing the id of the process that has written a log message.
		/// </summary>
		public void AddProcessIdColumn()
		{
			if (mDefaultColumnConfiguration)
			{
				mColumns.Clear();
				mDefaultColumnConfiguration = false;
			}

			ProcessIdColumn column = new ProcessIdColumn(this);
			AppendColumn(column);
		}

		/// <summary>
		/// Adds a column showing the name of the process that has written a log message.
		/// </summary>
		public void AddProcessNameColumn()
		{
			if (mDefaultColumnConfiguration)
			{
				mColumns.Clear();
				mDefaultColumnConfiguration = false;
			}

			ProcessNameColumn column = new ProcessNameColumn(this);
			AppendColumn(column);
		}

		/// <summary>
		/// Adds a column showing the name of the application that has written a log message.
		/// </summary>
		public void AddApplicationNameColumn()
		{
			if (mDefaultColumnConfiguration)
			{
				mColumns.Clear();
				mDefaultColumnConfiguration = false;
			}

			ApplicationNameColumn column = new ApplicationNameColumn(this);
			AppendColumn(column);
		}

		/// <summary>
		/// Adds a column showing the name of the log writer that was used to write a log message.
		/// </summary>
		public void AddLogWriterColumn()
		{
			if (mDefaultColumnConfiguration)
			{
				mColumns.Clear();
				mDefaultColumnConfiguration = false;
			}

			LogWriterColumn column = new LogWriterColumn(this);
			AppendColumn(column);
		}

		/// <summary>
		/// Adds a column showing the name of the log level that was used to write a log message.
		/// </summary>
		public void AddLogLevelColumn()
		{
			if (mDefaultColumnConfiguration)
			{
				mColumns.Clear();
				mDefaultColumnConfiguration = false;
			}

			LogLevelColumn column = new LogLevelColumn(this);
			AppendColumn(column);
		}

		/// <summary>
		/// Adds a column showing the message text.
		/// </summary>
		public void AddTextColumn()
		{
			if (mDefaultColumnConfiguration)
			{
				mColumns.Clear();
				mDefaultColumnConfiguration = false;
			}

			TextColumn column = new TextColumn(this);
			AppendColumn(column);
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
			if (index + 1 == mColumns.Count)
			{
				return;
			}

			mColumns[mColumns.Count - 1].IsLastColumn = false;
			mColumns.RemoveAt(index);
			mColumns.Add(column);
			column.IsLastColumn = true;
		}
	}
}