///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Flags corresponding to fields of a log message (primarily for testing purposes).
	/// </summary>
	[Flags]
	public enum LogMessageField
	{
		/// <summary>
		/// No fields.
		/// </summary>
		None = 0,

		/// <summary>
		/// The <see cref="ILogMessage.Timestamp"/> field.
		/// </summary>
		Timestamp = 1,

		/// <summary>
		/// The <see cref="ILogMessage.HighPrecisionTimestamp"/> field.
		/// </summary>
		HighPrecisionTimestamp = 1 << 1,

		/// <summary>
		/// The <see cref="ILogMessage.LogWriterName"/> field.
		/// </summary>
		LogWriterName = 1 << 2,

		/// <summary>
		/// The <see cref="ILogMessage.LogLevelName"/> field.
		/// </summary>
		LogLevelName = 1 << 3,

		/// <summary>
		/// The <see cref="ILogMessage.ApplicationName"/> field.
		/// </summary>
		ApplicationName = 1 << 4,

		/// <summary>
		/// The <see cref="ILogMessage.ProcessName"/> field.
		/// </summary>
		ProcessName = 1 << 5,

		/// <summary>
		/// The <see cref="ILogMessage.ProcessId"/> field.
		/// </summary>
		ProcessId = 1 << 6,

		/// <summary>
		/// The <see cref="ILogMessage.Text"/> field.
		/// </summary>
		Text = 1 << 7,

		/// <summary>
		/// All fields.
		/// </summary>
		All = Timestamp | HighPrecisionTimestamp | LogWriterName | LogLevelName | ApplicationName | ProcessName | ProcessId | Text,

	}
}
