///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Text;

namespace GriffinPlus.Lib.Logging
{
	partial class TableMessageFormatter
	{
		/// <summary>
		/// Base class for column definitions.
		/// </summary>
		private abstract class ColumnBase
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="ColumnBase"/> class.
			/// </summary>
			/// <param name="formatter">The formatter the column belongs to.</param>
			/// <param name="field">The formatted log message field.</param>
			protected ColumnBase(TableMessageFormatter formatter, LogMessageField field)
			{
				Formatter = formatter;
				Field = field;
			}

			/// <summary>
			/// Gets the formatter the column belongs to.
			/// </summary>
			protected TableMessageFormatter Formatter { get; }

			/// <summary>
			/// Gets the log message field the column is responsible for.
			/// </summary>
			public LogMessageField Field { get; }

			/// <summary>
			/// Gets or sets a value indicating whether the column is the last one.
			/// </summary>
			public bool IsLastColumn { get; set; }

			/// <summary>
			/// Gets the width of column.
			/// </summary>
			protected int Width { get; set; }

			/// <summary>
			/// Measures the field of the message to present in the column and updates the <see cref="Width"/> property.
			/// </summary>
			/// <param name="message">Message to measure to adjust the width of the column.</param>
			public abstract void UpdateWidth(ILogMessage message);

			/// <summary>
			/// Appends output of the current column for the specified line to the specified string builder.
			/// </summary>
			/// <param name="message">Message containing the field to format.</param>
			/// <param name="builder">String builder to append the output of the current column to.</param>
			/// <param name="line">Line number to append (zero-based).</param>
			/// <returns>true, if there are more lines to process; otherwise false.</returns>
			public abstract bool Write(ILogMessage message, StringBuilder builder, int line);
		}
	}
}
