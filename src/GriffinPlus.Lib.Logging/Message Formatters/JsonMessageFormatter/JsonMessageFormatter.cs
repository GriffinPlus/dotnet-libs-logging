///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// ReSharper disable EmptyConstructor

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A log message formatter that formats log messages as JSON.
	/// All configured log message fields are put in a flat JSON object, the key of a field is customizable.
	/// </summary>
	public partial class JsonMessageFormatter : ILogMessageFormatter
	{
		private readonly List<FieldBase>           mFields          = new List<FieldBase>();
		private readonly StringBuilder             mOutputBuilder   = new StringBuilder();
		private          LogMessageField           mFormattedFields = LogMessageField.None;
		private          IFormatProvider           mFormatProvider  = CultureInfo.InvariantCulture;
		private          JsonMessageFormatterStyle mStyle           = JsonMessageFormatterStyle.OneLine;
		private          string                    mIndent          = "    ";
		private          string                    mNewline         = Environment.NewLine;
		private          bool                      mEscapeSolidus;
		private          int                       mMaxEscapedJsonKeyLength;
		private readonly object                    mSync = new object();

		/// <summary>
		/// Initializes a new instance of the <see cref="JsonMessageFormatter" /> class.
		/// </summary>
		public JsonMessageFormatter()
		{
		}

		/// <summary>
		/// Gets a formatter that writes the following fields:
		/// 'Timestamp', 'Log Writer', 'Log Level', 'Tags', 'Application Name', 'Process Name', 'Process Id', 'Text'.
		/// </summary>
		public static JsonMessageFormatter AllFields
		{
			get
			{
				var formatter = new JsonMessageFormatter();
				formatter.AddTimestampField();
				formatter.AddLogWriterField();
				formatter.AddLogLevelField();
				formatter.AddTagsField();
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
			get
			{
				lock (mSync) return mFormattedFields;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether the solidus character ('/') is escaped (default <c>false</c>).
		/// </summary>
		public bool EscapeSolidus
		{
			get
			{
				lock (mSync) return mEscapeSolidus;
			}

			set
			{
				lock (mSync)
				{
					if (mEscapeSolidus != value)
					{
						mEscapeSolidus = value;
						foreach (var field in mFields) field.UpdateEscapedJsonKey();
						UpdateMaxEscapedJsonKeyLength();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the format provider to use when formatting log messages.
		/// By default <see cref="CultureInfo.InvariantCulture" /> is used.
		/// </summary>
		public IFormatProvider FormatProvider
		{
			get
			{
				lock (mSync) return mFormatProvider;
			}

			set
			{
				lock (mSync) mFormatProvider = value ?? throw new ArgumentNullException(nameof(value));
			}
		}

		/// <summary>
		/// Gets or sets the format style of the JSON documents.
		/// </summary>
		public JsonMessageFormatterStyle Style
		{
			get
			{
				lock (mSync) return mStyle;
			}

			set
			{
				lock (mSync) mStyle = value;
			}
		}

		/// <summary>
		/// Gets or sets the string to use for indenting lines
		/// (applies only if <see cref="Style" /> is set to <see cref="JsonMessageFormatterStyle.Beautified" />).
		/// </summary>
		public string Indent
		{
			get
			{
				lock (mSync) { return mIndent; }
			}

			set
			{
				lock (mSync) { mIndent = value ?? throw new ArgumentNullException(nameof(value)); }
			}
		}

		/// <summary>
		/// Gets or sets the characters used to inject a line break.
		/// By default it is the system's standard (CRLF on windows, LF on linux).
		/// </summary>
		public string Newline
		{
			get
			{
				lock (mSync) { return mNewline; }
			}

			set
			{
				lock (mSync) { mNewline = value ?? throw new ArgumentNullException(nameof(value)); }
			}
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
							if (i + 1 < mFields.Count) mOutputBuilder.Append(",");
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
							mOutputBuilder.Append(i + 1 < mFields.Count ? ", " : " ");
						}

						mOutputBuilder.Append("}");
						break;
					}

					case JsonMessageFormatterStyle.Beautified:
					{
						mOutputBuilder.Append("{");
						mOutputBuilder.Append(mNewline);
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
							mOutputBuilder.Append(mNewline);
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
		/// <param name="jsonKey">Key of the field in the JSON document (null to use the default key).</param>
		public void AddTimestampField(string format = "u", string jsonKey = null)
		{
			if (format == null) throw new ArgumentNullException(nameof(format));
			if (jsonKey == null) jsonKey = JsonMessageFieldNames.Default.Timestamp;

			// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
			DateTimeOffset.MinValue.ToString(format); // throws FormatException, if format is invalid

			var field = new TimestampField(this, jsonKey, format);
			AppendField(field);
		}

		/// <summary>
		/// Adds the high precision timestamp field.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document (null to use the default key).</param>
		public void AddHighPrecisionTimestampField(string jsonKey = null)
		{
			if (jsonKey == null) jsonKey = JsonMessageFieldNames.Default.HighPrecisionTimestamp;
			var field = new HighPrecisionTimestampField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds a field with the name of the log writer that was used to write a log message.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document (null to use the default key).</param>
		public void AddLogWriterField(string jsonKey = null)
		{
			if (jsonKey == null) jsonKey = JsonMessageFieldNames.Default.LogWriter;
			var field = new LogWriterField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds a field with the tags the log writer attached when writing the log message.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document (null to use the default key).</param>
		public void AddTagsField(string jsonKey = null)
		{
			if (jsonKey == null) jsonKey = JsonMessageFieldNames.Default.Tags;
			var field = new TagsField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds a field with the name of the log level that was used to write a log message.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document (null to use the default key).</param>
		public void AddLogLevelField(string jsonKey = null)
		{
			if (jsonKey == null) jsonKey = JsonMessageFieldNames.Default.LogLevel;
			var field = new LogLevelField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds a field with the name of the application that has written a log message.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document (null to use the default key).</param>
		public void AddApplicationNameField(string jsonKey = null)
		{
			if (jsonKey == null) jsonKey = JsonMessageFieldNames.Default.ApplicationName;
			var field = new ApplicationNameField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds a field with the name of the process that has written a log message.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document (null to use the default key).</param>
		public void AddProcessNameField(string jsonKey = null)
		{
			if (jsonKey == null) jsonKey = JsonMessageFieldNames.Default.ProcessName;
			var field = new ProcessNameField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds a field with the id of the process that has written a log message.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document (null to use the default key).</param>
		public void AddProcessIdField(string jsonKey = null)
		{
			if (jsonKey == null) jsonKey = JsonMessageFieldNames.Default.ProcessId;
			var field = new ProcessIdField(this, jsonKey);
			AppendField(field);
		}

		/// <summary>
		/// Adds a field with the message text.
		/// </summary>
		/// <param name="jsonKey">Key of the field in the JSON document (null to use the default key).</param>
		public void AddTextField(string jsonKey = null)
		{
			if (jsonKey == null) jsonKey = JsonMessageFieldNames.Default.Text;
			var field = new TextField(this, jsonKey);
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
		/// Updates the <see cref="mMaxEscapedJsonKeyLength" /> field to reflect changes to the fields.
		/// </summary>
		private void UpdateMaxEscapedJsonKeyLength()
		{
			mMaxEscapedJsonKeyLength = 0;
			foreach (var field in mFields) mMaxEscapedJsonKeyLength = Math.Max(mMaxEscapedJsonKeyLength, field.EscapedJsonKey.Length);
		}

		/// <summary>
		/// Escapes characters in the specified string complying with the JSON specification (https://json.org).
		/// </summary>
		/// <param name="builder">String builder to append the escaped string to.</param>
		/// <param name="s">String to escape and append to the string builder.</param>
		/// <param name="escapeSolidus">true to escape the solidus ('/'), otherwise false.</param>
		internal static void AppendEscapedStringToBuilder(StringBuilder builder, string s, bool escapeSolidus)
		{
			// NOTE:
			// According to the JSON specification (https://json.org) any codepoint except the quotation mark (")
			// and the reverse solidus (\) may be used unescaped, but these code points can be escaped as well.
			// The following implementation escapes control characters that could cause issues when loading the
			// JSON file into an editor.

			foreach (char c in s)
			{
				switch (c)
				{
					case '"': // quotation mark
						builder.Append('\\');
						builder.Append('"');
						break;

					case '/': // solidus
						if (escapeSolidus) builder.Append('\\');
						builder.Append('/');
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
						if (c <= 0x1F) // control characters
						{
							builder.AppendFormat("\\u{0:X04}", (int)c);
						}
						else
						{
							builder.Append(c);
						}

						break;
				}
			}
		}
	}

}
