﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Text;

namespace GriffinPlus.Lib.Logging
{

	partial class TableMessageFormatter
	{
		/// <summary>
		/// The log level column.
		/// </summary>
		private sealed class LogLevelColumn : ColumnBase
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="LogLevelColumn"/> class.
			/// </summary>
			/// <param name="formatter">The formatter the column belongs to.</param>
			public LogLevelColumn(TableMessageFormatter formatter) : base(formatter, LogMessageField.LogLevelName) { }

			/// <summary>
			/// Measures the field of the message to present in the column and updates the <see cref="ColumnBase.Width"/> property.
			/// </summary>
			/// <param name="message">Message to measure to adjust the width of the column.</param>
			public override void UpdateWidth(ILogMessage message)
			{
				int length = message.LogLevelName.Length;
				Width = Math.Max(Width, length);
			}

			/// <summary>
			/// Appends output of the current column for the specified line to the specified string builder.
			/// </summary>
			/// <param name="message">Message containing the field to format.</param>
			/// <param name="builder">String builder to append the output of the current column to.</param>
			/// <param name="line">Line number to append (zero-based).</param>
			/// <returns>
			/// <c>true</c> if there are more lines to process;<br/>
			/// otherwise <c>false</c>.
			/// </returns>
			public override bool Write(ILogMessage message, StringBuilder builder, int line)
			{
				if (line == 0)
				{
					string s = message.LogLevelName;
					builder.Append(s);
					if (!IsLastColumn && s.Length < Width) builder.Append(' ', Width - s.Length);
				}
				else
				{
					if (!IsLastColumn) builder.Append(' ', Width);
				}


				return false; // last line
			}
		}
	}

}
