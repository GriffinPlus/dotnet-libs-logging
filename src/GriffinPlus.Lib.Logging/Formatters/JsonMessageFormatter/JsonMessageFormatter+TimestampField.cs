///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

using System.Text;

namespace GriffinPlus.Lib.Logging
{
	partial class JsonMessageFormatter
	{
		/// <summary>
		/// The timestamp field (immutable).
		/// </summary>
		sealed class TimestampField : FieldBase
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
