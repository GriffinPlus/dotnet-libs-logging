///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Runtime.InteropServices;

namespace GriffinPlus.Lib.Logging;

partial class LocalLogServiceConnection
{
	/// <summary>
	/// A log entry block within the shared memory queue (496 bytes in size).
	/// </summary>
	/// <remarks>
	/// This structure is designed to produce 512 byte blocks when put into a block of the <see cref="UnsafeSharedMemoryQueue"/>.
	/// </remarks>
	[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
	private struct LogEntryBlock
	{
		/// <summary>
		/// Type specifying the kind of log block entry (command/message).
		/// </summary>
		[FieldOffset(0)] public LogEntryBlockType Type;

		/// <summary>
		/// Reserved for extensions.
		/// </summary>
		[FieldOffset(4)] public uint Reserved;

		/// <summary>
		/// A message indicating the start of a logging session.
		/// </summary>
		[FieldOffset(8)] public LogEntryBlock_StartMarker StartMarker;

		/// <summary>
		/// A message setting the application name of the logging process.
		/// </summary>
		[FieldOffset(8)] public LogEntryBlock_SetApplicationName SetApplicationName;

		/// <summary>
		/// A message setting the name of a log writer.
		/// </summary>
		[FieldOffset(8)] public LogEntryBlock_AddSourceName AddSourceName;

		/// <summary>
		/// A message setting the name of a log level.
		/// </summary>
		[FieldOffset(8)] public LogEntryBlock_AddLogLevelName AddLogLevelName;

		/// <summary>
		/// A message triggering clearing the log viewer.
		/// </summary>
		[FieldOffset(8)] public LogEntryBlock_ClearLogViewer ClearLogViewer;

		/// <summary>
		/// A message triggering saving a snapshot in the local log service.
		/// </summary>
		[FieldOffset(8)] public LogEntryBlock_SaveSnapshot SaveSnapshot;

		/// <summary>
		/// A log message.
		/// </summary>
		[FieldOffset(8)] public LogEntryBlock_Message Message;

		/// <summary>
		/// A message extending a preceding log message (for long messages).
		/// </summary>
		[FieldOffset(8)] public LogEntryBlock_MessageExtension MessageExtension;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct LogEntryBlock_StartMarker
	{
		/// <summary>
		/// The maximum number of log levels the logging subsystem can have (-1 for unlimited).
		/// </summary>
		public int MaxLogLevelCount;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private unsafe struct LogEntryBlock_SetApplicationName
	{
		public const int ApplicationNameSize = 244;

		/// <summary>
		/// Name of the application.
		/// </summary>
		public fixed char ApplicationName[ApplicationNameSize];
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private unsafe struct LogEntryBlock_AddSourceName
	{
		public const int SourceNameSize = 242;

		/// <summary>
		/// Identifier of the log source.
		/// </summary>
		public int Identifier;

		/// <summary>
		/// Name of the source that belongs to the specified identifier.
		/// </summary>
		public fixed char SourceName[SourceNameSize];
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private unsafe struct LogEntryBlock_AddLogLevelName
	{
		public const int LogLevelNameSize = 242;

		/// <summary>
		/// Identifier of the log level.
		/// </summary>
		public int Identifier;

		/// <summary>
		/// Name of the log level that belongs to the specified identifier.
		/// </summary>
		public fixed char LogLevelName[LogLevelNameSize];
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct LogEntryBlock_ClearLogViewer
	{
		/// <summary>
		/// Time the message was put into the queue.
		/// </summary>
		public long Timestamp;

		/// <summary>
		/// Id of the process writing the log entry.
		/// </summary>
		public int ProcessId;

		/// <summary>
		/// High precision timestamp (in microseconds).
		/// </summary>
		public long HighPrecisionTimestamp;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct LogEntryBlock_SaveSnapshot
	{
		/// <summary>
		/// Time the message was put into the queue.
		/// </summary>
		public long Timestamp;

		/// <summary>
		/// Id of the process writing the log entry.
		/// </summary>
		public int ProcessId;

		/// <summary>
		/// High precision timestamp (in microseconds).
		/// </summary>
		public long HighPrecisionTimestamp;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private unsafe struct LogEntryBlock_Message
	{
		public const int MessageSize = 224;

		/// <summary>
		/// Time the message was put into the queue.
		/// </summary>
		public long Timestamp;

		/// <summary>
		/// High precision timestamp (in microseconds).
		/// </summary>
		public long HighPrecisionTimestamp;

		/// <summary>
		/// Name identifier of the source writing the log entry.
		/// </summary>
		public int SourceNameId;

		/// <summary>
		/// Name identifier of the log level determining the importance of the message.
		/// </summary>
		public int LogLevelNameId;

		/// <summary>
		/// Id of the process writing the log entry.
		/// </summary>
		public int ProcessId;

		/// <summary>
		/// Number of extension messages following this log entry block.
		/// </summary>
		public int MessageExtensionCount;

		/// <summary>
		/// Log message text.
		/// </summary>
		public fixed char Message[MessageSize];
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private unsafe struct LogEntryBlock_MessageExtension
	{
		public const int MessageSize = 244;

		/// <summary>
		/// Log message text (extending the previously sent message).
		/// </summary>
		public fixed char Message[MessageSize];
	}
}
