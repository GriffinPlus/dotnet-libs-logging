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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message formatter that formats log messages as JSON.
	/// All configured log message fields are put in a flat JSON object, the key of a field is customizable.
	/// </summary>
	public partial class JsonMessageFormatter : ILogMessageFormatter
	{
		private List<FieldBase> mFields = new List<FieldBase>();
		private readonly StringBuilder mOutputBuilder = new StringBuilder();
		private LogMessageField mFormattedFields = LogMessageField.None;
		private IFormatProvider mFormatProvider = CultureInfo.InvariantCulture;
		private JsonMessageFormatterStyle mStyle = JsonMessageFormatterStyle.OneLine;
		private string mIndent = "    ";
		private int mMaxEscapedJsonKeyLength = 0;
		private object mSync = new object();

		/// <summary>
		/// Initializes a new instance of the <see cref="JsonMessageFormatter"/> class.
		/// </summary>
		public JsonMessageFormatter()
		{

		}

		/// <summary>
		/// Gets a formatter that writes the following fields:
		/// 'Timestamp', 'Log Writer', 'Log Level', 'Application Name', 'Process Name', 'Process Id', 'Text'.
		/// </summary>
		public static JsonMessageFormatter AllFields
		{
			get
			{
				var formatter = new JsonMessageFormatter();
				formatter.AddTimestampField();
				formatter.AddLogWriterField();
				formatter.AddLogLevelField();
				formatter.AddApplicationNameField();
				formatter.AddProcessNameField();
				formatter.AddProcessIdField();
				formatter.AddTextField();
				return formatter;
			}
		}

		/// <summary>
		/// Gets the formatted log message fields.
		/// </summary>
		public LogMessageField FormattedFields
		{
			get { lock (mSync) return mFormattedFields; }
		}

		/// <summary>
		/// Gets or sets the format provider to use when formatting log messages.
		/// By default <see cref="CultureInfo.InvariantCulture"/> is used.
		/// </summary>
		public IFormatProvider FormatProvider
		{
			get { lock (mSync) return mFormatProvider; }
			set { lock (mSync) mFormatProvider = value ?? throw new ArgumentNullException(nameof(value)); }
		}

		/// <summary>
		/// Gets or sets the format style of the JSON documents.
		/// </summary>
		public JsonMessageFormatterStyle Style
		{
			get { lock (mSync) return mStyle; }
			set { lock (mSync) mStyle = value; }
		}

		/// <summary>
		/// Gets or sets the string to use for indenting lines
		/// (applys only if <see cref="Style"/> is set to <see cref="JsonMessageFormatterStyle.Beautified"/>).
		/// </summary>
		public string Indent
		{
			get { lock (mSync) { return mIndent; } }
			set { lock (mSync) { mIndent = value ?? throw new ArgumentNullException(nameof(value)); } }
		}

		/// <summary>
		/// Formats the specified log message.
		/// </summary>
		/// <param name="message">Message to format.</param>
		/// <returns>The formatted log message.</returns>
		public string Format(ILogMessage message)
		{
			lock (mSync)
			{
				mOutputBuilder.Clear();

				switch (mStyle)
				{
					case JsonMessageFormatterStyle.Compact:
						{
							mOutputBuilder.Append("{");
							for (int i = 0; i < mFields.Count; i++)
							{
								var field = mFields[i];
								mOutputBuilder.Append('"');
								mOutputBuilder.Append(field.EscapedJsonKey);
								mOutputBuilder.Append('"');
								mOutputBuilder.Append(":");
								field.AppendFormattedValue(message, mOutputBuilder);
								if (i+1 < mFields.Count) mOutputBuilder.Append(",");
							}
							mOutputBuilder.Append("}");
							break;
						}

					case JsonMessageFormatterStyle.OneLine:
						{
							mOutputBuilder.Append("{ ");
							for (int i = 0; i < mFields.Count; i++)
							{
								var field = mFields[i];
								mOutputBuilder.Append('"');
								mOutputBuilder.Append(field.EscapedJsonKey);
								mOutputBuilder.Append('"');
								mOutputBuilder.Append(" : ");
								field.AppendFormattedValue(message, mOutputBuilder);
								if (i + 1 < mFields.Count) mOutputBuilder.Append(", ");
								else mOutputBuilder.Append(" ");
							}
							mOutputBuilder.Append("}");
							break;
						}

					case JsonMessageFormatterStyle.Beautified:
						{
							mOutputBuilder.AppendLine("{");
							for (int i = 0; i < mFields.Count; i++)
							{
								var field = mFields[i];
								mOutputBuilder.Append(mIndent);
								mOutputBuilder.Append('"');
								mOutputBuilder.Append(field.EscapedJsonKey);
								mOutputBuilder.Append('"');
								mOutputBuilder.Append(' ', mMaxEscapedJsonKeyLength - field.EscapedJsonKey.Length);
								mOutputBuilder.Append(" : ");
								field.AppendFormattedValue(message, mOutputBuilder);
								if (i + 1 < mFields.Count) mOutputBuilder.Append(",");
								mOutputBuilder.AppendLine();
							}
							mOutputBuilder.Append("}");
							break;
						}

					default:
						throw new NotSupportedException($"The configured style ({mStyle}) is not supported.");
				}

				return mOutputBuilder.ToString();
			}
		}

		/// <summary>
		/// Adds a timestamp field and sets the format of the timestamp
		/// (default: "u", conversion to UTC and output using the format yyyy-MM-dd HH:mm:ssZ)
		/// </summary>
		/// <param name="format">
		/// The timestamp format (see https://msdn.microsoft.com/en-us/library/bb351892(v=vs.110).aspx" for details).
		/// </param>
		/// <param name="jsonKey">Key of the field in the JSON document.</param>
		public void AddTimestampField(string format = "u", string jsonKey = "Timestamp")
		{
			if (format == null) throw new ArgumentNullException(nameof(format));

			// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
			DateTimeOffset.MinValue.ToString(format); // throws FormatException, if format is invalid

			TimestampField field = new TimestampField(this, jsonKey, format);
			AppendField(field);
		}

		/// <summary>
		/// Adds the high accuracy timestamp field.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document.</param>
		public void AddHighAccuracyTimestampField(string jsonKey = "HighAccuracyTimestamp")
		{
			HighAccuracyTimestampField field = new HighAccuracyTimestampField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds a field with the id of the process that has written a log message.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document.</param>
		public void AddProcessIdField(string jsonKey = "ProcessId")
		{
			ProcessIdField field = new ProcessIdField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds a field with the name of the process that has written a log message.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document.</param>
		public void AddProcessNameField(string jsonKey = "ProcessName")
		{
			ProcessNameField field = new ProcessNameField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds a field with the name of the application that has written a log message.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document.</param>
		public void AddApplicationNameField(string jsonKey = "ApplicationName")
		{
			ApplicationNameField field = new ApplicationNameField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds a field with the name of the log writer that was used to write a log message.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document.</param>
		public void AddLogWriterField(string jsonKey = "LogWriter")
		{
			LogWriterField field = new LogWriterField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds a field with the name of the log level that was used to write a log message.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document.</param>
		public void AddLogLevelField(string jsonKey = "LogLevel")
		{
			LogLevelField field = new LogLevelField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds a field with the message text.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document.</param>
		public void AddTextField(string jsonKey = "Text")
		{
			TextField field = new TextField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds the specified field to the end of the field collection.
		/// </summary>
		/// <param name="field">Field to add.</param>
		private void AppendField(FieldBase field)
		{
			lock (mSync)
			{
				mFields.Add(field);
				mFormattedFields |= field.Field;
				mMaxEscapedJsonKeyLength = Math.Max(mMaxEscapedJsonKeyLength, field.EscapedJsonKey.Length);
			}
		}

		/// <summary>
		/// Escapes characters in the specified string complying with the JSON specification (https://json.org).
		/// </summary>
		/// <param name="builder">String builder to append the escaped string to.</param>
		/// <param name="s">String to escape and append to the string builder.</param>
		private static void AppendEscapedStringToBuilder(StringBuilder builder, string s)
		{
			// NOTE:
			// According to the JSON specification (https://json.org) any codepoint except the quotation mark (")
			// and the reverse solidus (\) may be used unescaped, but these codepoints can be escaped as well.
			// The following implementation escapes control characters that could cause issues when loading the
			// JSON file into an editor.

			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];

				switch (c)
				{
					case '"': // quotation mark
						builder.Append('\\');
						builder.Append('"');
						break;
					case '\t': // tab
						builder.Append('\\');
						builder.Append('t');
						break;
					case '\n': // line feed
						builder.Append('\\');
						builder.Append('n');
						break;
					case '\r': // carriage return
						builder.Append('\\');
						builder.Append('r');
						break;
					case '\f': // form feed
						builder.Append('\\');
						builder.Append('f');
						break;
					case '\b': // backspace
						builder.Append('\\');
						builder.Append('b');
						break;
					case '\\': // reverse solidus
						builder.Append('\\');
						builder.Append('\\');
						break;
					case '\u0085': // Next Line
						builder.Append("\\u0085");
						break;
					case '\u2028': // Line Separator
						builder.Append("\\u2028");
						break;
					case '\u2029': // Paragraph Separator
						builder.Append("\\u2029");
						break;
					default:
						builder.Append(c);
						break;
				}
			}
		}

	}
}
