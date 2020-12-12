///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Text;

namespace GriffinPlus.Lib.Logging
{

	partial class TableMessageFormatter
	{
		/// <summary>
		/// The message text column.
		/// </summary>
		private sealed class TextColumn : ColumnBase
		{
			private string[] mBuffer;

			/// <summary>
			/// Initializes a new instance of the <see cref="TextColumn" /> class.
			/// </summary>
			/// <param name="formatter">The formatter the column belongs to.</param>
			public TextColumn(TableMessageFormatter formatter) : base(formatter, LogMessageField.Text)
			{
			}

			/// <summary>
			/// Measures the field of the message to present in the column and updates the <see cref="ColumnBase.Width" /> property.
			/// </summary>
			/// <param name="message">Message to measure to adjust the width of the column.</param>
			public override void UpdateWidth(ILogMessage message)
			{
				mBuffer = message.Text.Replace("\r", "").Split('\n');
				int length = mBuffer.Max(x => x.Length);
				Width = Math.Max(Width, length);
			}

			/// <summary>
			/// Appends output of the current column for the specified line to the specified string builder.
			/// </summary>
			/// <param name="message">Message containing the field to format.</param>
			/// <param name="builder">String builder to append the output of the current column to.</param>
			/// <param name="line">Line number to append (zero-based).</param>
			/// <returns>true, if there are more lines to process; otherwise false.</returns>
			public override bool Write(ILogMessage message, StringBuilder builder, int line)
			{
				string s = mBuffer[line];
				builder.Append(s);

				if (!IsLastColumn)
				{
					if (line == 0)
					{
						if (s.Length < Width) builder.Append(' ', Width - s.Length);
					}
					else
					{
						builder.Append(' ', Width);
					}
				}

				return line + 1 < mBuffer.Length;
			}
		}
	}

}
