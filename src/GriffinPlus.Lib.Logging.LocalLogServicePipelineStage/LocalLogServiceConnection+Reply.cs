///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Runtime.InteropServices;

namespace GriffinPlus.Lib.Logging;

partial class LocalLogServiceConnection
{
	/// <summary>
	/// A request sent via the named pipe.
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	public struct Reply
	{
		[FieldOffset(0)] public uint                             Result;
		[FieldOffset(4)] public Reply_RegisterLogSourceCommand   RegisterLogSourceCommand;
		[FieldOffset(4)] public Reply_UnregisterLogSourceCommand UnregisterLogSourceCommand;
		[FieldOffset(4)] public Reply_QueryProcessIdCommand      QueryProcessIdCommand;
		[FieldOffset(4)] public Reply_SetWritingToLogFileCommand SetWritingToLogFileCommand;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Reply_RegisterLogSourceCommand
	{
		// empty
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Reply_UnregisterLogSourceCommand
	{
		// empty
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Reply_QueryProcessIdCommand
	{
		/// <summary>
		/// The id of the process.
		/// </summary>
		public int ProcessId;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Reply_SetWritingToLogFileCommand
	{
		// empty
	}
}
