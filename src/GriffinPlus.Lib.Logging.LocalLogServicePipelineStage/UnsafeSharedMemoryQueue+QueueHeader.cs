///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Runtime.InteropServices;

namespace GriffinPlus.Lib.Logging;

partial class UnsafeSharedMemoryQueue
{
	/// <summary>
	/// The header of the shared memory queue.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	private unsafe struct QueueHeader
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
		public int BufferSize;

		/// <summary>
		/// Size of a queue element (including padding bytes to fill up entire cache lines).
		/// </summary>
		public int BlockSize;
	}
}
