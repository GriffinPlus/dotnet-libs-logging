///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;


namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Provides a queue with fixed sized elements in shared memory.
	/// 
	/// This class creates or opens a queue in shared memory by a specified name. This queue can be
	/// written by multiple threads (even from different processes) and read by a single thread.
	/// In the case that the queue runs out of free space the queue can either wait until space is available
	/// or just return <c>null</c> indicating that the queue is full.
	///
	/// The queue uses shared memory to store a fixed amount of elements (blocks). At first all blocks
	/// are kept on a 'free block stack'. When a thread wants to write to the queue, it requests a block
	/// from that list removing the requested block from the 'free block stack'. The writer can directly
	/// write into the shared memory region eliminating the need for copy operations to put data into the queue.
	/// After finishing writing the thread puts the block (containing its data now) onto the 'used block stack'.
	/// This concept allows multiple threads to write to the queue simultaneously without blocking the entire
	/// queue while they are filling their blocks with data. Even access to the 'free block stack' and the
	/// 'used block stack' is lockless. This guarantees accessing the queue does not lead to a thread
	/// context switch at all improving the overall performance.
	///
	/// A thread that wants to read from the queue gets the entire 'used block stack', processes the data
	/// in the blocks and puts the blocks back onto the 'free block stack', so they can be used by a writer
	/// once again.
	///
	/// After creating an instance of the <see cref="UnsafeSharedMemoryQueue"/>class you must decide whether
	/// you want to create a new queue or open an existing one. Creating a new queue will work only, if a
	/// shared memory section with the specified name does not exist already.
	///
	/// To create a new queue call the <see cref="Create"/> method and specify a name for the shared memory
	/// section the queue will reside in as well as the number of blocks the queue will keep and the size of
	/// such a block. The created shared memory buffer is accessible from anyone (no security is applied).
	/// Creating a new queue is only supported on the .NET full framework!
	///
	/// To open an existing queue call the <see cref="Open"/> method and specify a name for the shared memory
	/// section. The number of blocks as well as their size is determined by the creator of the queue.
	///
	/// You can write to the queue by calling the <see cref="BeginWriting"/> method which delivers a pointer to
	/// a block within the queue structure in shared memory. Just fill your data into that block and
	/// call <see cref="EndWriting"/> telling the queue how many bytes are valid within the buffer to finish
	/// writing to the queue.
	///
	/// You can read from the queue by calling the <see cref="BeginReading"/> method which delivers a pointer
	/// to a block within the queue - just as <see cref="BeginWriting"/> does - and tells you how many bytes
	/// are valid within that block. When you're finished call <see cref="EndReading"/> to release the block
	/// and enable it to get used again.
	///
	/// After having finished work with the queue you can call the <see cref="Close"/> method to release operating
	/// system resources.
	/// </summary>
	internal sealed unsafe partial class UnsafeSharedMemoryQueue : IDisposable
	{
		/// <summary>
		/// Size of the queue's header containing administrative and user-specific data (see <see cref="QueueHeader"/>).
		/// </summary>
		private int mQueueHeaderSize;

		/// <summary>
		/// Indicates whether the queue is initialized.
		/// </summary>
		private bool mInitialized;

		/// <summary>
		/// Shared memory the queue resides in.
		/// </summary>
		private MemoryMappedFile mMemoryMappedFile;

		/// <summary>
		/// Accessor to the shared memory the queue resides in.
		/// </summary>
		private MemoryMappedViewAccessor mMemoryMappedViewAccessor;

		/// <summary>
		/// Pointer to the header of the queue in shared memory.
		/// </summary>
		private QueueHeader* mQueueHeader;

		/// <summary>
		/// Pointer to the first data block in shared memory.
		/// </summary>
		private QueueBlock* mFirstBlockInMemory;

		/// <summary>
		/// Number of blocks in the queue (capacity).
		/// </summary>
		private int mNumberOfBlocks;

		/// <summary>
		/// Size of a block in the queue (as specified when creating the queue).
		/// </summary>
		private int mBufferSize;

		/// <summary>
		/// Real size of block in the queue (including block header and padding bytes).
		/// </summary>
		private int mBlockSize;

		/// <summary>
		/// The first block in a sequence of blocks that is currently read.
		/// </summary>
		private QueueBlock* mFirstBlockUnderRead;

#if NETFRAMEWORK
		// security attributes for the shared memory queue
		private static MemoryMappedFileSecurity sMemoryMappedFileSecurity;
#endif

		/// <summary>
		/// The size of a cache line.
		/// </summary>
		private const int CacheLineSize = 64; // should be almost always correct

		/// <summary>
		/// Initializes the <see cref="UnsafeSharedMemoryQueue"/> class.
		/// </summary>
		static UnsafeSharedMemoryQueue()
		{
#if NETFRAMEWORK
			InitializeSecurityAttributes();
#endif
		}

		/// <summary>
		/// Disposes the shared memory queue releasing operating system resources.
		/// </summary>
		public void Dispose()
		{
			Close();
		}

		#region Creating/Opening and Closing

		/// <summary>
		/// Creates a new shared memory queue (only supported on the full .NET framework).
		/// </summary>
		/// <param name="name">Name of the shared memory region to create the queue in.</param>
		/// <param name="bufferSize">Size of a buffer in the shared memory queue (in bytes).</param>
		/// <param name="numberOfBlocks">Number of blocks the queue should keep.</param>
		/// <exception cref="NotSupportedException">This method is not supported on the current platform/framework.</exception>
		/// <remarks>
		/// This method creates a new queue in a shared memory region specified by the given name and the given size.
		/// </remarks>
		public void Create(string name, int bufferSize, int numberOfBlocks)
		{
#if NETFRAMEWORK
			if (name == null) throw new ArgumentNullException(nameof(name));
			if (bufferSize < 0) throw new ArgumentOutOfRangeException(nameof(bufferSize), "The block size must be positive.");
			if (numberOfBlocks < 0) throw new ArgumentOutOfRangeException(nameof(numberOfBlocks), "The number of blocks must be positive.");

			// close currently opened queue, if necessary
			Close();

			mNumberOfBlocks = numberOfBlocks;
			mBufferSize = bufferSize;
			// ensure that the block sizes are a multiple of the cache line size to avoid false sharing
			mBlockSize = (sizeof(QueueBlock) + mBufferSize + CacheLineSize - 1) & ~(CacheLineSize - 1);
			mQueueHeaderSize = (sizeof(QueueHeader) + CacheLineSize - 1) & ~(CacheLineSize - 1);
			long dataSize = (long)mNumberOfBlocks * mBlockSize;
			long totalBufferSize = mQueueHeaderSize + dataSize;

			// create the shared memory region
			string queueName = $"{name} - Shared Memory";
			mMemoryMappedFile = MemoryMappedFile.CreateNew(
				queueName,
				totalBufferSize,
				MemoryMappedFileAccess.ReadWrite,
				MemoryMappedFileOptions.None,
				sMemoryMappedFileSecurity,
				HandleInheritability.None);
			mMemoryMappedViewAccessor = mMemoryMappedFile.CreateViewAccessor();

			// get pointer to the buffer
			byte* ptr = null;
			mMemoryMappedViewAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
			mQueueHeader = (QueueHeader*) ptr;
			mFirstBlockInMemory = (QueueBlock*) ((byte*) mQueueHeader + mQueueHeaderSize);

			// init the queue
			InitQueue();

			// the queue is initialized now
			mFirstBlockUnderRead = null;
			mInitialized = true;
#else
			throw new NotSupportedException("The Created() method is only supported on the full .NET framework.");
#endif
		}

		/// <summary>
		/// Opens an existing queue in the shared memory region with the specified name.
		/// </summary>
		/// <param name="name">Name of the shared memory region to open.</param>
		public void Open(string name)
		{
			// close currently opened queue, if necessary
			Close();

			try
			{
				// open the shared memory region the queue resides in
				string queueName = $"{name} - Shared Memory";
				mMemoryMappedFile = MemoryMappedFile.OpenExisting(queueName, MemoryMappedFileRights.ReadWrite);
				mMemoryMappedViewAccessor = mMemoryMappedFile.CreateViewAccessor();

				// get pointer to the buffer in shared memory
				byte* ptr = null;
				mMemoryMappedViewAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
				mQueueHeader = (QueueHeader*)ptr;

				// check the queue's signature
				// (for an explanation why 'ALVA' see InitQueue()
				if (mQueueHeader->Signature[0] != 'A' ||
				    mQueueHeader->Signature[1] != 'L' ||
				    mQueueHeader->Signature[2] != 'V' ||
				    mQueueHeader->Signature[3] != 'A')
				{
					throw new InvalidDataException("Shared region does not start with the magic word 'ALVA'.");
				}

				// read administrative information from the queue's header
				mNumberOfBlocks = mQueueHeader->NumberOfBlocks;
				mBufferSize = mQueueHeader->BufferSize;
				mBlockSize = mQueueHeader->BlockSize;
				mQueueHeaderSize = (sizeof(QueueHeader) + CacheLineSize - 1) & ~(CacheLineSize - 1);
				mFirstBlockInMemory = (QueueBlock*)((byte*)mQueueHeader + mQueueHeaderSize);

				// the queue is initialized now!
				mFirstBlockUnderRead = null;
				mInitialized = true;
			}
			catch (Exception)
			{
				Close();
				throw;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the shared memory queue is initialized.
		/// </summary>
		public bool IsInitialized => mInitialized;

		/// <summary>
		/// Closes the shared memory queue currently opened.
		/// </summary>
		public void Close()
		{
			if (mMemoryMappedViewAccessor != null)
			{
				if (mQueueHeader != null)
				{
					mMemoryMappedViewAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
					mQueueHeader = null;
				}

				mMemoryMappedViewAccessor.Dispose();
				mMemoryMappedViewAccessor = null;
			}

			if (mMemoryMappedFile != null)
			{
				mMemoryMappedFile.Dispose();
				mMemoryMappedFile = null;
			}

			mQueueHeader = null;
			mFirstBlockInMemory = null;
			mFirstBlockUnderRead = null;
			mNumberOfBlocks = 0;
			mBufferSize = 0;
			mBlockSize = 0;
			mInitialized = false;
		}

#endregion

#region Writing

		/// <summary>
		/// Begins writing a block.
		/// </summary>
		/// <returns>
		/// Pointer to the data buffer within the retrieved block;
		/// null, if no block is available for writing.
		/// </returns>
		/// <exception cref="InvalidOperationException">The queue was not initialized using <see cref="Create"/> or <see cref="Open"/>.</exception>
		/// <remarks>
		/// This method retrieves a block from the 'free block stack' and returns a pointer to the data
		/// buffer within the block. You must either call EndWriting() to push this block onto the 'used
		/// block stack' or AbortWriting() to push it back onto the 'free block stack'.
		/// </remarks>
		public void* BeginWriting()
		{
			// abort, if the queue is not initialized
			if (!mInitialized) throw new InvalidOperationException("Queue is not initialized, call Create() or Open() to initialize it.");

			// fetch a free block
			QueueBlock* block = GetFreeBlock();
			if (block == null)
			{
				// no free block left
				return null;
			}

			return (byte*)block + sizeof(QueueBlock);
		}

		/// <summary>
		/// Ends writing a block.
		/// </summary>
		/// <param name="buffer">Pointer to the buffer of the block as returned by <see cref="BeginWriting"/>.</param>
		/// <param name="numberOfBytesWritten">Number of bytes actually written (must not exceed the block size).</param>
		/// <param name="overflowCount">Number of lost single blocks or block sequences since the last successfully transferred block.</param>
		/// <exception cref="InvalidOperationException">The queue was not initialized using <see cref="Create"/> or <see cref="Open"/>.</exception>
		public void EndWriting(void* buffer, int numberOfBytesWritten, int overflowCount)
		{
			// abort, if the buffer is null
			if (buffer == null) return;

			// abort, if the queue is not initialized
			if (!mInitialized) throw new InvalidOperationException("Queue is not initialized, call Create() or Open() to initialize it.");

			// check arguments
			if (((long)buffer & 15) != 0) throw new ArgumentException("The buffer is not aligned to a 16 byte boundary.", nameof(buffer));
			if (numberOfBytesWritten < 0) throw new ArgumentOutOfRangeException(nameof(numberOfBytesWritten), "The number of written bytes must be positive.");
			if (numberOfBytesWritten > mBufferSize) throw new ArgumentOutOfRangeException(nameof(numberOfBytesWritten), "The number of written bytes must not exceed the queue's buffer size.");
			QueueBlock* block = (QueueBlock*)((byte*)buffer - sizeof(QueueBlock));
			if (block->MagicNumber != QueueBlock.MagicNumberValue) throw new ArgumentException("The block of the buffer does not have a valid magic number.", nameof(buffer));

			// append block to the end of the 'used block stack'
			block->DataSize = numberOfBytesWritten;
			PushUsedBlock(block, overflowCount);
		}

		/// <summary>
		/// Ends writing a sequence of blocks.
		/// </summary>
		/// <param name="buffers">Array of pointers to buffers of blocks as returned by <see cref="BeginWriting"/>.</param>
		/// <param name="numberOfBytesWritten">
		/// Number of bytes written into the buffers specified in <paramref name="buffers"/> (must not exceed the block size).
		/// </param>
		/// <param name="count">Number of blocks to write.</param>
		/// <param name="overflowCount">Number of lost single blocks or block sequences since the last successfully transferred block.</param>
		/// <exception cref="InvalidOperationException">The queue was not initialized using <see cref="Create"/> or <see cref="Open"/>.</exception>
		/// <remarks>
		/// This method takes the sequence of buffers of blocks as returned by <see cref="BeginWriting"/> and pushes the blocks onto the 'used block stack'.
		/// </remarks>
		public void EndWritingSequence(void** buffers, int* numberOfBytesWritten, int count, int overflowCount)
		{
			// abort, if the buffer is null
			if (buffers == null || count <= 0)
				return;

			// abort, if the queue is not initialized
			if (!mInitialized) throw new InvalidOperationException("Queue is not initialized, call Create() or Open() to initialize it.");

			// check arguments
			for (int i = 0; i < count; i++)
			{
				if (((long)buffers[i] & 15) != 0) throw new ArgumentException($"Buffer[{i}] is not aligned to a 16 byte boundary.", nameof(buffers));
				if (numberOfBytesWritten[i] < 0) throw new ArgumentOutOfRangeException(nameof(numberOfBytesWritten), "The number of written bytes to buffer[{i}] must be positive.");
				if (numberOfBytesWritten[i] > mBufferSize) throw new ArgumentOutOfRangeException(nameof(numberOfBytesWritten), "The number of written bytes to buffer[{i}] must not exceed the queue's buffer size.");
				QueueBlock* block = (QueueBlock*)((byte*)buffers[i] - sizeof(QueueBlock));
				if (block->MagicNumber != QueueBlock.MagicNumberValue) throw new ArgumentException($"The block of buffer[{i}] does not have a valid magic number.", nameof(buffers));
			}

			// link the blocks that are about to be pushed onto the 'used block stack'
			// (the sequence is pushed onto the stack in reversed order, so the blocks can be fetched in order when reading)
			int previousIndex = -1;
			for (int i = 0; i < count; i++)
			{
				QueueBlock* block = (QueueBlock*)((byte*)buffers[i] - sizeof(QueueBlock));
				int blockIndex = (int)(((byte*)block - (byte*)mFirstBlockInMemory) / mBlockSize);
				Debug.Assert(blockIndex < mNumberOfBlocks);
				block->DataSize = numberOfBytesWritten[i];
				block->NextIndex = previousIndex;
				previousIndex = blockIndex;
			}

			// push sequence of blocks onto the 'used block stack'
			QueueBlock* firstBlock = (QueueBlock*)((byte*)buffers[count - 1] - sizeof(QueueBlock));
			QueueBlock* lastBlock = (QueueBlock*)((byte*)buffers[0] - sizeof(QueueBlock));
			PushUsedBlocks(firstBlock, lastBlock, overflowCount);
		}

		/// <summary>
		/// Aborts writing a block.
		/// </summary>
		/// <param name="buffer">Pointer to the buffer of a block as returned by <see cref="BeginWriting"/>.</param>
		/// <exception cref="InvalidOperationException">The queue was not initialized using <see cref="Create"/> or <see cref="Open"/>.</exception>
		/// <remarks>
		/// This method takes the buffer of a block as returned by <see cref="BeginWriting"/> and pushes the block onto the 'free block stack' again.
		/// </remarks>
		public void AbortWriting(void* buffer)
		{
			// abort, if the buffer is null
			if (buffer == null) return;

			// abort, if the queue is not initialized
			if (!mInitialized) throw new InvalidOperationException("Queue is not initialized, call Create() or Open() to initialize it.");

			// check arguments
			if (((long)buffer & 15) != 0) throw new ArgumentException("The buffer is not aligned to a 16 byte boundary.", nameof(buffer));

			// push block onto the 'free block stack'
			QueueBlock* block = (QueueBlock*)((byte*)buffer - sizeof(QueueBlock));
			PushFreeBlock(block);
		}

		#endregion

		#region Reading

		/// <summary>
		/// Begins reading a new block.
		/// </summary>
		/// <param name="validSize">Receives the number of valid bytes in the block.</param>
		/// <param name="overflowCount">Receives the number of lost single blocks or block sequences since the last reading attempt.</param>
		/// <param name="blocksFollowingInSequence">
		/// Receives <c>true</c>, if there are more blocks in the queue that belong to the block;
		/// otherwise false.
		/// </param>
		/// <returns>
		/// Pointer to the data buffer within the retrieved block;
		/// null if no block is available for reading.
		/// </returns>
		/// <exception cref="InvalidOperationException">The queue was not initialized using <see cref="Create"/> or <see cref="Open"/>.</exception>
		public void* BeginReading(out int validSize, out int overflowCount, out bool blocksFollowingInSequence)
		{
			// abort, if the queue is not initialized
			if (!mInitialized) throw new InvalidOperationException("Queue is not initialized, call Create() or Open() to initialize it.");

			if (mFirstBlockUnderRead != null)
			{
				// there are some blocks to read left from the last flush
				// => take one of these blocks

				// prepare stuff to return
				validSize = mFirstBlockUnderRead->DataSize;
				overflowCount = mFirstBlockUnderRead->OverflowCount;
				QueueBlock* block = mFirstBlockUnderRead;
				void* buffer = (void*)((byte)block + sizeof(QueueBlock));

				// proceed with the next block
				if (mFirstBlockUnderRead->NextIndex >= 0)
				{
					Debug.Assert(mFirstBlockUnderRead->NextIndex < mNumberOfBlocks);
					mFirstBlockUnderRead = (QueueBlock*)((byte*)mFirstBlockInMemory + (long)mFirstBlockUnderRead->NextIndex * mBlockSize);
					Debug.Assert(mFirstBlockUnderRead->MagicNumber == QueueBlock.MagicNumberValue);
				}
				else
				{
					mFirstBlockUnderRead = null;
				}

				blocksFollowingInSequence = mFirstBlockUnderRead != null;

				Debug.Assert(((long)buffer & 15) == 0);
				block->NextIndex = -1;
				return buffer;
			}

			// there are no blocks to read left from the last flush
			// => try to flush the queue, re-initialize the buffer and proceed...
			while (true)
			{
				int blockIndex = mQueueHeader->UsedStackHeaderIndex;
				if (blockIndex < 0)
				{
					// no block on the 'used block stack'
					validSize = 0;
					overflowCount = 0;
					blocksFollowingInSequence = false;
					return null;
				}

				// flush the 'used block stack'
				Debug.Assert(blockIndex < mNumberOfBlocks);
				if (Interlocked.CompareExchange(ref mQueueHeader->UsedStackHeaderIndex, -1, blockIndex) != blockIndex)
					continue;

				// reverse the sequence of blocks to restore the original order
				QueueBlock* block = (QueueBlock*)((byte*)mFirstBlockInMemory + (long)blockIndex * mBlockSize);
				int currentBlockIndex = blockIndex;
				int previousIndex = -1;
				while (true)
				{
					Debug.Assert(block->MagicNumber == QueueBlock.MagicNumberValue);
					Debug.Assert(block->NextIndex < 0 || block->NextIndex < mNumberOfBlocks);
					int nextIndex = block->NextIndex;
					block->NextIndex = previousIndex;
					previousIndex = currentBlockIndex;
					currentBlockIndex = nextIndex;
					if (currentBlockIndex < 0) break;
					block = (QueueBlock*)((byte*)mFirstBlockInMemory + (long)currentBlockIndex * mBlockSize);
				}

				Debug.Assert(previousIndex >= 0 && previousIndex < mNumberOfBlocks);
				mFirstBlockUnderRead = (QueueBlock*)((byte*)mFirstBlockInMemory + (long)previousIndex * mBlockSize);
				validSize = mFirstBlockUnderRead->DataSize;
				overflowCount = mFirstBlockUnderRead->OverflowCount;
				void* buffer = (byte*)mFirstBlockUnderRead + sizeof(QueueBlock);

				// proceed with the next block
				if (mFirstBlockUnderRead->NextIndex >= 0)
				{
					Debug.Assert(mFirstBlockUnderRead->NextIndex < mNumberOfBlocks);
					mFirstBlockUnderRead = (QueueBlock*)((byte*)mFirstBlockInMemory + (long)mFirstBlockUnderRead->NextIndex * mBlockSize);
				}
				else
				{
					mFirstBlockUnderRead = null;
				}

				blocksFollowingInSequence = mFirstBlockUnderRead != null;

				Debug.Assert(((long)buffer & 15) == 0);
				block->NextIndex = -1;
				return buffer;
			}
		}

		/// <summary>
		/// End reading a block.
		/// </summary>
		/// <param name="buffer">Pointer to the buffer of a block as returned by <see cref="BeginReading"/>.</param>
		/// <exception cref="InvalidOperationException">The queue was not initialized using <see cref="Create"/> or <see cref="Open"/>.</exception>
		public void EndReading(void* buffer)
		{
			// abort, if the buffer is null
			if (buffer == null) return;

			// abort, if the queue is not initialized
			if (!mInitialized) throw new InvalidOperationException("Queue is not initialized, call Create() or Open() to initialize it.");

			// check arguments
			if (((long)buffer & 15) != 0) throw new ArgumentException("The buffer is not aligned to a 16 byte boundary.", nameof(buffer));

			// push block onto the 'free block stack'
			QueueBlock* block = (QueueBlock*)((byte*)buffer - sizeof(QueueBlock));
			PushFreeBlock(block);
		}

		#endregion

		#region Internal Management

		/// <summary>
		/// Initializes the the queue structure in shared memory.
		/// </summary>
		private void InitQueue()
		{
			// init queue header
			mQueueHeader->Signature[0] = (byte)'A';         // Why 'ALVA'? - 'Alvarium' is latin for 'bee-hive'.
			mQueueHeader->Signature[1] = (byte)'L';         // The log messages are the bees flying through the queue at high speed.
			mQueueHeader->Signature[2] = (byte)'V';         // The rest is up to your imagination...
			mQueueHeader->Signature[3] = (byte)'A';         // Ok, we just needed a handy signature with four characters ;)
			mQueueHeader->NumberOfBlocks = mNumberOfBlocks;
			mQueueHeader->BufferSize = mBufferSize;
			mQueueHeader->BlockSize = mBlockSize;
			mQueueHeader->FreeStackHeaderIndex = 0;
			mQueueHeader->UsedStackHeaderIndex = -1;

			// init queue blocks (except last block)
			QueueBlock* block;
			QueueBlock* firstBlock = (QueueBlock*)((byte*)mQueueHeader + mQueueHeaderSize);
			for (int i = 0; i < mNumberOfBlocks; i++)
			{
				block = (QueueBlock*)((byte*)firstBlock + (long)i * mBlockSize);
				block->MagicNumber = QueueBlock.MagicNumberValue;
				block->NextIndex = i + 1;
				block->DataSize = 0;
				block->OverflowCount = 0;
			}

			// init last block
			block = (QueueBlock*)((byte*)firstBlock + (long)(mNumberOfBlocks - 1) * mBlockSize);
			block->NextIndex = -1;
		}

		/// <summary>
		/// Gets a free block.
		/// </summary>
		/// <returns>
		/// A free block;
		/// null, if no free block is available.
		/// </returns>
		private QueueBlock* GetFreeBlock()
		{
			while (true)
			{
				int blockIndex = mQueueHeader->FreeStackHeaderIndex;
				if (blockIndex < 0) return null;

				QueueBlock* block = (QueueBlock*)((byte*)mFirstBlockInMemory + (long)blockIndex * mBlockSize);
				if (Interlocked.CompareExchange(ref mQueueHeader->FreeStackHeaderIndex, block->NextIndex, blockIndex) == blockIndex)
				{
					Debug.Assert(block->MagicNumber == QueueBlock.MagicNumberValue);

					block->OverflowCount = 0;
					block->NextIndex = -1;
					return block;
				}
			}
		}

		/// <summary>
		/// Pushes a block onto the 'free block stack'.
		/// </summary>
		/// <param name="block">Block to push onto the 'free block stack'.</param>
		/// <exception cref="TimeoutException">
		/// The mutex protecting the 'free block stack' is not released within a reasonable amount of time.
		/// This is a severe error condition.
		/// </exception>
		private void PushFreeBlock(QueueBlock* block)
		{
			Debug.Assert(block->MagicNumber == QueueBlock.MagicNumberValue);
			Debug.Assert(block->NextIndex == -1);

			// block->DataSize = 0;
			// block->OverflowCount = 0;

			Debug.Assert(((byte*)block - (byte*)mFirstBlockInMemory) % mBlockSize == 0);
			int blockIndex = (int)(((byte*)block - (byte*)mFirstBlockInMemory) / mBlockSize);
			Debug.Assert(blockIndex < mNumberOfBlocks);

			while (true)
			{
				int firstIndex = block->NextIndex = mQueueHeader->FreeStackHeaderIndex;
				if (Interlocked.CompareExchange(ref mQueueHeader->FreeStackHeaderIndex, blockIndex, firstIndex) == firstIndex)
					break;
			}
		}

		/// <summary>
		/// Pushes a block onto the top of the 'used block stack'.
		/// </summary>
		/// <param name="block">Block to push onto the 'used block stack'.</param>
		/// <param name="overflowCount">Number of lost single blocks or block sequences since the last successful transferred block.</param>
		private void PushUsedBlock(QueueBlock* block, int overflowCount)
		{
			Debug.Assert(block->MagicNumber == QueueBlock.MagicNumberValue);
			Debug.Assert(block->NextIndex == -1);

			block->OverflowCount = overflowCount;

			Debug.Assert(((byte*)block - (byte*)mFirstBlockInMemory) % mBlockSize == 0);
			int blockIndex = (int)(((byte*)block - (byte*)mFirstBlockInMemory) / mBlockSize);
			Debug.Assert(blockIndex < mNumberOfBlocks);

			while (true)
			{
				int firstIndex = block->NextIndex = mQueueHeader->UsedStackHeaderIndex;
				if (Interlocked.CompareExchange(ref mQueueHeader->UsedStackHeaderIndex, blockIndex, firstIndex) == firstIndex)
					break;
			}
		}

		/// <summary>
		/// Pushes a sequence of blocks onto the top of the 'used block stack'.
		/// </summary>
		/// <param name="firstBlock">First block to push onto the 'used block stack'.</param>
		/// <param name="lastBlock">Last block to push onto the 'used block stack'.</param>
		/// <param name="overflowCount">Number of lost single blocks or block sequences since the last successful transferred block.</param>
		private void PushUsedBlocks(QueueBlock* firstBlock, QueueBlock* lastBlock, int overflowCount)
		{
			lastBlock->OverflowCount = overflowCount; // last block is the first block of the pushed sequence

			int firstBlockIndex = (int)(((byte*)firstBlock - (byte*)mFirstBlockInMemory) / mBlockSize);

			while (true)
			{
				int firstIndex = lastBlock->NextIndex = mQueueHeader->UsedStackHeaderIndex;
				if (Interlocked.CompareExchange(ref mQueueHeader->UsedStackHeaderIndex, firstBlockIndex, firstIndex) == firstIndex)
					break;
			}
		}

#if NETFRAMEWORK
		/// <summary>
		/// Initializes security attributes for the shared memory queue.
		/// </summary>
		/// <remarks>
		/// This method initializes security attributes for creating the shared memory queues.
		/// Anyone is allowed read/write (no user specific security).
		/// </remarks>
		private static void InitializeSecurityAttributes()
		{
			sMemoryMappedFileSecurity = new MemoryMappedFileSecurity();

			var rule = (AccessRule<MemoryMappedFileRights>)sMemoryMappedFileSecurity.AccessRuleFactory(
				new SecurityIdentifier(WellKnownSidType.WorldSid, null),
				(int)MemoryMappedFileRights.ReadWrite,
				false,
				InheritanceFlags.None,
				PropagationFlags.None,
				AccessControlType.Allow);

			sMemoryMappedFileSecurity.AddAccessRule(rule);
		}
#endif

#endregion
	}
}
