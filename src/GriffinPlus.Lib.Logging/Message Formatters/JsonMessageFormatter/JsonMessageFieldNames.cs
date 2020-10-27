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

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Field names to use when serializing/deserializing log messages to/from JSON.
	/// </summary>
	public class JsonMessageFieldNames
	{
		/// <summary>
		/// Default field names.
		/// </summary>
		public static readonly JsonMessageFieldNames Default = new JsonMessageFieldNames();

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
	}
}
