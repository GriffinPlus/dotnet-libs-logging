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
	partial class UnsafeSharedMemoryQueue
	{
		/// <summary>
		/// The header of the shared memory queue.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		unsafe struct QueueHeader
		{
			/// <summary>
			/// The signature (used to recognize the shared memory queue).
			/// </summary>
			public fixed byte Signature[4];

			/// <summary>
			/// Index of the first block in the 'free block stack'.
			/// </summary>
			public int FreeStackHeaderIndex;

			/// <summary>
			/// Index of the first block in the 'used block stack'.
			/// </summary>
			public int UsedStackHeaderIndex;

			/// <summary>
			/// Maximum number of elements in the queue.
			/// </summary>
			public int NumberOfBlocks;

			/// <summary>
			/// Size of a queue element (as specified when creating the queue).
			/// </summary>
			public int BlockSize;

			/// <summary>
			/// Size of a queue element (including padding bytes to fill up entire cache lines).
			/// </summary>
			public int RealBlockSize;
		}
	}
}
