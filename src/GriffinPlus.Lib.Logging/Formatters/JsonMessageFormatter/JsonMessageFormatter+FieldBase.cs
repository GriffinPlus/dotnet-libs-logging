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
using System.Web;

namespace GriffinPlus.Lib.Logging
{
	partial class JsonMessageFormatter
	{
		/// <summary>
		/// Base class for field definitions
		/// (derived classes should be immutable to avoid threading issues).
		/// </summary>
		abstract class FieldBase
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="FieldBase"/> class.
			/// </summary>
			/// <param name="formatter">The formatter the field belongs to.</param>
			/// <param name="jsonKey">Key of the field in the JSON document.</param>
			protected FieldBase(JsonMessageFormatter formatter, string jsonKey)
			{
				Formatter = formatter;
				JsonKey = jsonKey;
				EscapedJsonKey = GetEscapedString(jsonKey);
			}

			/// <summary>
			/// Gets the formatter the field belongs to.
			/// </summary>
			protected JsonMessageFormatter Formatter { get; }

			/// <summary>
			/// Gets the unescaped key in the JSON document the field is associated with.
			/// </summary>
			public string JsonKey { get; }

			/// <summary>
			/// Gets the escaped key in the JSON document the field is associated with.
			/// </summary>
			public string EscapedJsonKey { get; }

			/// <summary>
			/// Appends the formatted value of the current field to the specified string builder.
			/// </summary>
			/// <param name="message">Message containing the field to format.</param>
			/// <param name="builder">String builder to append the output of the current field to.</param>
			public abstract void AppendFormattedValue(ILogMessage message, StringBuilder builder);

			/// <summary>
			/// Escapes characters in the specified string complying with the JSON specification.
			/// </summary>
			/// <param name="s">String to escape.</param>
			/// <returns>The escaped string.</returns>
			private static string GetEscapedString(string s)
			{
				StringBuilder builder = new StringBuilder();
				AppendEscapedStringToBuilder(builder, s);
				return builder.ToString();
			}

		}
	}
}
