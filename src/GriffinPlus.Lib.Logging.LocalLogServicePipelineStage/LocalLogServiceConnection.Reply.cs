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
	internal partial class LocalLogServiceConnection
	{
		/// <summary>
		/// A request sent via the named pipe.
		/// </summary>
		[StructLayout(LayoutKind.Explicit)]
		public struct Reply
		{
			[FieldOffset(0)] public uint Result;
			[FieldOffset(4)] public Reply_RegisterLogSourceCommand RegisterLogSourceCommand;
			[FieldOffset(4)] public Reply_UnregisterLogSourceCommand UnregisterLogSourceCommand;
			[FieldOffset(4)] public Reply_QueryProcessIdCommand QueryProcessIdCommand;
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
}