///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Text;

namespace GriffinPlus.Lib.Logging
{

	partial class JsonMessageFormatter
	{
		/// <summary>
		/// The process id field (immutable).
		/// </summary>
		private sealed class ProcessIdField : FieldBase
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="ProcessIdField" /> class.
			/// </summary>
			/// <param name="formatter">The formatter the column belongs to.</param>
			/// <param name="jsonKey">Key of the field in the JSON document.</param>
			public ProcessIdField(JsonMessageFormatter formatter, string jsonKey) :
				base(formatter, LogMessageField.ProcessId, jsonKey)
			{
			}

			/// <summary>
			/// Appends the formatted value of the current field to the specified string builder.
			/// </summary>
			/// <param name="message">Message containing the field to format.</param>
			/// <param name="builder">String builder to append the output of the current field to.</param>
			public override void AppendFormattedValue(ILogMessage message, StringBuilder builder)
			{
				builder.Append(message.ProcessId);
			}
		}
	}

}
