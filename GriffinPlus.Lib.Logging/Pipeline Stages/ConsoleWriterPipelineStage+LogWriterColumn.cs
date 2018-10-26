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
using System.Text;

namespace GriffinPlus.Lib.Logging
{
	partial class ConsoleWriterPipelineStage
	{
		/// <summary>
		/// The log writer column.
		/// </summary>
		class LogWriterColumn : ColumnBase
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="LogWriterColumn"/> class.
			/// </summary>
			/// <param name="stage">The pipeline stage.</param>
			public LogWriterColumn(ConsoleWriterPipelineStage stage) : base(stage)
			{

			}

			/// <summary>
			/// Measures the field of the message to present in the column and Updates the <see cref="ColumnBase.Width"/> property.
			/// </summary>
			/// <param name="message">Message to measure to adjust the width of the column.</param>
			public override void UpdateWidth(LogMessage message)
			{
				int length = message.LogWriter.Name.Length;
				Width = Math.Max(Width, length);
			}

			/// <summary>
			/// Appends output of the current column for the specified line.
			/// </summary>
			/// <param name="message">Message containing output to write.</param>
			/// <param name="builder">Stringbuilder to append the output of the current column to.</param>
			/// <param name="line">Line number to append (zero-based).</param>
			/// <returns>true, if there are more lines to process; otherwise false.</returns>
			public override bool Write(LogMessage message, StringBuilder builder, int line)
			{
				if (line == 0)
				{
					string s = message.LogWriter.Name;
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
