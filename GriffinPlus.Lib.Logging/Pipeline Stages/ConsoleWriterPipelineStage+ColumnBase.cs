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

using System.Text;

namespace GriffinPlus.Lib.Logging
{
	partial class ConsoleWriterPipelineStage
	{
		/// <summary>
		/// Column definitions.
		/// </summary>
		abstract class ColumnBase
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="ColumnBase"/> class.
			/// </summary>
			/// <param name="stage">The pipeline stage.</param>
			public ColumnBase(ConsoleWriterPipelineStage stage)
			{
				Stage = stage;
			}

			/// <summary>
			/// The pipeline stage the column belongs to.
			/// </summary>
			public ConsoleWriterPipelineStage Stage { get; private set; }

			/// <summary>
			/// Gets or sets a value indicating whether the column is the last one.
			/// </summary>
			public bool IsLastColumn { get; set; }

			/// <summary>
			/// Gets the width of column.
			/// </summary>
			public int Width { get; set; } = 0;

			/// <summary>
			/// Measures the field of the message to present in the column and Updates the <see cref="Width"/> property.
			/// </summary>
			/// <param name="message">Message to measure to adjust the width of the column.</param>
			public abstract void UpdateWidth(LogMessage message);

			/// <summary>
			/// Appends output of the current column for the specified line.
			/// </summary>
			/// <param name="message">Message containing output to write.</param>
			/// <param name="builder">Stringbuilder to append the output of the current column to.</param>
			/// <param name="line">Line number to append (zero-based).</param>
			/// <returns>true, if there are more lines to process; otherwise false.</returns>
			public abstract bool Write(LogMessage message, StringBuilder builder, int line);
		}
	}
}
