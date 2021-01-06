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
		/// The log writer tags field (immutable).
		/// </summary>
		private sealed class TagsField : FieldBase
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="TagsField"/> class.
			/// </summary>
			/// <param name="formatter">The formatter the field belongs to.</param>
			/// <param name="jsonKey">Key of the field in the JSON document.</param>
			public TagsField(JsonMessageFormatter formatter, string jsonKey) :
				base(formatter, LogMessageField.Tags, jsonKey)
			{
			}

			/// <summary>
			/// Appends the formatted value of the current field to the specified string builder.
			/// </summary>
			/// <param name="message">Message containing the field to format.</param>
			/// <param name="builder">String builder to append the output of the current field to.</param>
			public override void AppendFormattedValue(ILogMessage message, StringBuilder builder)
			{
				builder.Append('[');

				if (message.Tags != null)
				{
					if (Formatter.mStyle == JsonMessageFormatterStyle.Compact)
					{
						int count = message.Tags.Count;
						for (int i = 0; i < count; i++)
						{
							string tag = message.Tags[i];
							builder.Append('"');
							AppendEscapedStringToBuilder(builder, tag, Formatter.mEscapeSolidus);
							builder.Append('"');
							if (i + 1 < count) builder.Append(',');
						}
					}
					else
					{
						int count = message.Tags.Count;
						for (int i = 0; i < count; i++)
						{
							string tag = message.Tags[i];
							if (i == 0) builder.Append(' ');
							builder.Append('"');
							AppendEscapedStringToBuilder(builder, tag, Formatter.mEscapeSolidus);
							builder.Append('"');
							if (i + 1 < count) builder.Append(',');
							builder.Append(' ');
						}
					}
				}

				builder.Append(']');
			}
		}
	}

}
