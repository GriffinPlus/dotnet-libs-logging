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

using System.Runtime.InteropServices;

namespace GriffinPlus.Lib.Logging
{
	partial class LocalLogServiceConnection
	{
		/// <summary>
		/// A request sent via the named pipe.
		/// </summary>
		[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
		public struct Request
		{
			[FieldOffset(0)] public Command Command;
			[FieldOffset(4)] public Request_RegisterLogSourceCommand RegisterLogSourceCommand;
			[FieldOffset(4)] public Request_UnregisterLogSourceCommand UnregisterLogSourceCommand;
			[FieldOffset(4)] public Request_QueryProcessIdCommand QueryProcessIdCommand;
			[FieldOffset(4)] public Request_SetWritingToLogFileCommand SetWritingToLogFileCommand;
		}

		public enum Command
		{
			RegisterLogSource = 1,
			UnregisterLogSource = 2,
			QueryProcessId = 3,
			SetWritingToLogFile = 4
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Request_RegisterLogSourceCommand
		{
			/// <summary>
			/// The id of the process registering itself.
			/// </summary>
			public int ProcessId;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Request_UnregisterLogSourceCommand
		{
			/// <summary>
			/// The id of the process unregistering itself.
			/// </summary>
			public int ProcessId;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Request_QueryProcessIdCommand
		{
			// empty
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Request_SetWritingToLogFileCommand
		{
			/// <summary>
			/// The id of the process setting whether the local log service should write its messages to the log file.
			/// </summary>
			public int ProcessId;

			/// <summary>
			/// Indicates whether messages from the process are written to the log file
			/// (0 = messages are not written to the log file, 1 = messages are written to the log file).
			/// </summary>
			public int Enable; // not 'bool', it only has 8 bit, but 32 bit are expected (Win32 BOOL)
		}

	}
}