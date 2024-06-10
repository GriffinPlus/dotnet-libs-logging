///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging;

partial class LocalLogServiceConnection
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
