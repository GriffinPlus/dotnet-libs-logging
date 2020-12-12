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
		/// The log level field (immutable).
		/// </summary>
		private sealed class LogLevelField : FieldBase
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="LogLevelField" /> class.
			/// </summary>
			/// <param name="formatter">The formatter the field belongs to.</param>
			/// <param name="jsonKey">Key of the field in the JSON document.</param>
			public LogLevelField(JsonMessageFormatter formatter, string jsonKey) :
				base(formatter, LogMessageField.LogLevelName, jsonKey)
			{
			}

			/// <summary>
			/// Appends the formatted value of the current field to the specified string builder.
			/// </summary>
			/// <param name="message">Message containing the field to format.</param>
			/// <param name="builder">String builder to append the output of the current field to.</param>
			public override void AppendFormattedValue(ILogMessage message, StringBuilder builder)
			{
				builder.Append('"');
				if (message.LogLevelName != null) AppendEscapedStringToBuilder(builder, message.LogLevelName, Formatter.mEscapeSolidus);
				builder.Append('"');
			}
		}
	}

}
