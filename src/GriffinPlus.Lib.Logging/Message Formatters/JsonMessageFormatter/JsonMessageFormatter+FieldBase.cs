///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Text;

namespace GriffinPlus.Lib.Logging;

partial class JsonMessageFormatter
{
	/// <summary>
	/// Base class for field definitions
	/// (derived classes should be immutable to avoid threading issues).
	/// </summary>
	private abstract class FieldBase
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FieldBase"/> class.
		/// </summary>
		/// <param name="formatter">The formatter the field belongs to.</param>
		/// <param name="field">The formatted log message field.</param>
		/// <param name="jsonKey">Key of the field in the JSON document.</param>
		protected FieldBase(JsonMessageFormatter formatter, LogMessageField field, string jsonKey)
		{
			Formatter = formatter;
			Field = field;
			JsonKey = jsonKey;
			UpdateEscapedJsonKey();
		}

		/// <summary>
		/// Gets the formatter the field belongs to.
		/// </summary>
		protected JsonMessageFormatter Formatter { get; }

		/// <summary>
		/// Gets the log message field the field is responsible for.
		/// </summary>
		public LogMessageField Field { get; }

		/// <summary>
		/// Gets the unescaped key in the JSON document the field is associated with.
		/// </summary>
		private string JsonKey { get; }

		/// <summary>
		/// Gets the escaped key in the JSON document the field is associated with.
		/// </summary>
		public string EscapedJsonKey { get; private set; }

		/// <summary>
		/// Appends the formatted value of the current field to the specified string builder.
		/// </summary>
		/// <param name="message">Message containing the field to format.</param>
		/// <param name="builder">String builder to append the output of the current field to.</param>
		public abstract void AppendFormattedValue(ILogMessage message, StringBuilder builder);

		/// <summary>
		/// Updates the <see cref="EscapedJsonKey"/> property.
		/// </summary>
		internal void UpdateEscapedJsonKey()
		{
			EscapedJsonKey = GetEscapedString(JsonKey, Formatter.mEscapeSolidus);
		}

		/// <summary>
		/// Escapes characters in the specified string complying with the JSON specification.
		/// </summary>
		/// <param name="s">String to escape.</param>
		/// <param name="escapeSolidus">
		/// <c>true</c> to escape the solidus ('/');<br/>
		/// otherwise <c>false</c>.
		/// </param>
		/// <returns>The escaped string.</returns>
		private static string GetEscapedString(string s, bool escapeSolidus)
		{
			var builder = new StringBuilder();
			AppendEscapedStringToBuilder(builder, s, escapeSolidus);
			return builder.ToString();
		}
	}
}
