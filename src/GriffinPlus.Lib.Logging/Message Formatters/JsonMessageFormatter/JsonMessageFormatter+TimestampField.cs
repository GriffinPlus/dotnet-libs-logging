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
		/// The timestamp field (immutable).
		/// </summary>
		private sealed class TimestampField : FieldBase
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="TimestampField"/> class.
			/// </summary>
			/// <param name="formatter">The formatter the field belongs to.</param>
			/// <param name="jsonKey">Key of the field in the JSON document.</param>
			/// <param name="format">Timestamp format to use.</param>
			public TimestampField(JsonMessageFormatter formatter, string jsonKey, string format) :
				base(formatter, LogMessageField.Timestamp, jsonKey)
			{
				TimestampFormat = format;
			}

			/// <summary>
			/// Gets or sets the timestamp format
			/// (See https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings for details)
			/// </summary>
			private string TimestampFormat { get; }

			/// <summary>
			/// Appends the formatted value of the current field to the specified string builder.
			/// </summary>
			/// <param name="message">Message containing the field to format.</param>
			/// <param name="builder">String builder to append the output of the current field to.</param>
			public override void AppendFormattedValue(ILogMessage message, StringBuilder builder)
			{
				builder.Append('"');
				AppendEscapedStringToBuilder(builder, message.Timestamp.ToString(TimestampFormat, Formatter.mFormatProvider), Formatter.mEscapeSolidus);
				builder.Append('"');
			}
		}
	}
}
