///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Field names to use when serializing/deserializing log messages to/from JSON.
/// </summary>
public class JsonMessageFieldNames
{
	/// <summary>
	/// Default field names.
	/// </summary>
	public static readonly JsonMessageFieldNames Default = new();

	/// <summary>
	/// Gets or sets the name of the field for <see cref="ILogMessage.Timestamp"/>.
	/// </summary>
	public string Timestamp { get; set; } = "Timestamp";

	/// <summary>
	/// Gets or sets the name of the field for <see cref="ILogMessage.HighPrecisionTimestamp"/>.
	/// </summary>
	public string HighPrecisionTimestamp { get; set; } = "HighPrecisionTimestamp";

	/// <summary>
	/// Gets or sets the name of the field for <see cref="ILogMessage.LogWriterName"/>.
	/// </summary>
	public string LogWriter { get; set; } = "LogWriter";

	/// <summary>
	/// Gets or sets the name of the field for <see cref="ILogMessage.LogLevelName"/>.
	/// </summary>
	public string LogLevel { get; set; } = "LogLevel";

	/// <summary>
	/// Gets or sets the name of the field for <see cref="ILogMessage.Tags"/>.
	/// </summary>
	public string Tags { get; set; } = "Tags";

	/// <summary>
	/// Gets or sets the name of the field for <see cref="ILogMessage.ApplicationName"/>.
	/// </summary>
	public string ApplicationName { get; set; } = "ApplicationName";

	/// <summary>
	/// Gets or sets the name of the field for <see cref="ILogMessage.ProcessName"/>.
	/// </summary>
	public string ProcessName { get; set; } = "ProcessName";

	/// <summary>
	/// Gets or sets the name of the field for <see cref="ILogMessage.ProcessId"/>.
	/// </summary>
	public string ProcessId { get; set; } = "ProcessId";

	/// <summary>
	/// Gets or sets the name of the field for <see cref="ILogMessage.Text"/>.
	/// </summary>
	public string Text { get; set; } = "Text";

	/// <summary>
	/// Checks whether the specified name is one of the defined field names.
	/// </summary>
	/// <param name="name">Name to check.</param>
	/// <returns>
	/// <c>true</c> if the name is a defined field name;<br/>
	/// otherwise <c>false</c>.
	/// </returns>
	public bool IsFieldName(string name)
	{
		return Timestamp == name ||
		       HighPrecisionTimestamp == name ||
		       LogWriter == name ||
		       LogLevel == name ||
		       Tags == name ||
		       ApplicationName == name ||
		       ProcessName == name ||
		       ProcessId == name ||
		       Text == name;
	}
}
