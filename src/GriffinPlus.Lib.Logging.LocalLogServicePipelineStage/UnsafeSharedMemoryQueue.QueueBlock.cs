///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
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
