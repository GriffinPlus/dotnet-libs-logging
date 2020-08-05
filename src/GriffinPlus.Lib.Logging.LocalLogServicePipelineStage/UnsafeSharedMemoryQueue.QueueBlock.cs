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
	internal partial class UnsafeSharedMemoryQueue
	{
		/// <summary>
		/// A block of data in the shared memory queue.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		private struct QueueBlock
		{
			public const uint MagicNumberValue = 0x11223344;

			/// <summary>
			/// Magic number identifying a queue block.
			/// </summary>
			public uint MagicNumber;

			/// <summary>
			/// Singly linked list item (used for linking multiple blocks).
			/// </summary>
			public int NextIndex;

			/// <summary>
			/// Number of valid bytes in the following payload.
			/// </summary>
			public int DataSize;

			/// <summary>
			/// Number of lost blocks before this block due to an overflow condition.
			/// </summary>
			public int OverflowCount;
		}
	}
}
