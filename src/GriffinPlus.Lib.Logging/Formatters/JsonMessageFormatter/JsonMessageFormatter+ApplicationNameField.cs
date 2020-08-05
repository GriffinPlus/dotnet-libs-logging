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
		/// The application name field (immutable).
		/// </summary>
		private sealed class ApplicationNameField : FieldBase
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="ApplicationNameField"/> class.
			/// </summary>
			/// <param name="formatter">The formatter the field belongs to.</param>
			/// <param name="jsonKey">Key of the field in the JSON document.</param>
			public ApplicationNameField(JsonMessageFormatter formatter, string jsonKey) :
				base(formatter, LogMessageField.ApplicationName, jsonKey)
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
				if (message.ApplicationName != null) AppendEscapedStringToBuilder(builder, message.ApplicationName, Formatter.mEscapeSolidus);
				builder.Append('"');
			}
		}
	}
}
