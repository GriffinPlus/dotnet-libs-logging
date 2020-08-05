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
	internal partial class LocalLogServiceConnection
	{
		/// <summary>
		/// Types a log entry block in a the shared memory queue can contain.
		/// </summary>
		internal enum LogEntryBlockType
		{
			/// <summary>
			/// A log message.
			/// </summary>
			Message = 0,

			/// <summary>
			/// A log message extension (for messages with long text).
			/// </summary>
			MessageExtension = 1,

			/// <summary>
			/// A message notifying of a new log writer.
			/// </summary>
			AddSourceName = 2,

			/// <summary>
			/// A message notifying of a new log level.
			/// </summary>
			AddLogLevelName = 3,

			/// <summary>
			/// A message notifying that the application name of the process was changed.
			/// </summary>
			SetApplicationName = 4,

			/// <summary>
			/// A start marker indicating that the start of the message stream.
			/// </summary>
			StartMarker = 5,

			/// <summary>
			/// A command telling the log viewer to clear the view.
			/// </summary>
			ClearLogViewer = 6,

			/// <summary>
			/// A command telling the local log service to save a snapshot of the log file.
			/// </summary>
			SaveSnapshot = 7
		}
	}
}
