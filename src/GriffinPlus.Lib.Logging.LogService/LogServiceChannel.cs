///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

using GriffinPlus.Lib.Io;

// ReSharper disable ForCanBeConvertedToForeach

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Base class of communication channels connecting clients and servers.
	/// </summary>
	[DebuggerDisplay("Status = {Status}")]
	public abstract partial class LogServiceChannel : IDisposable
	{
		/// <summary>
		/// Array pool to use when requesting buffers for sending via the socket.
		/// </summary>
		private static readonly ArrayPool<byte> sByteArrayPool = ArrayPool<byte>.Shared;

		/// <summary>
		/// Array pool to use when requesting buffers for converting received UTF-8 encoder data to UTF-16.
		/// </summary>
		private static readonly ArrayPool<char> sCharArrayPool = ArrayPool<char>.Shared;

		/// <summary>
		/// Maximum number of concurrent send operations.
		/// </summary>
		private const int MaxConcurrentSendOperations = 3;

		/// <summary>
		/// Maximum number of concurrent receive operations.
		/// </summary>
		private const int MaxConcurrentReceiveOperations = 5;

		/// <summary>
		/// Size of a send buffer.
		/// </summary>
		private const int SendBufferSize = 64 * 1024;

		/// <summary>
		/// Size of a receive buffer (in bytes).
		/// </summary>
		private const int ReceiveBufferSize = 64 * 1024;

		/// <summary>
		/// Maximum length of a line.
		/// </summary>
		public const int MaxLineLength = 32 * 1024;

		// channel management members
		private readonly Socket                  mSocket;
		private          LogServiceChannelStatus mStatus = LogServiceChannelStatus.Created;
		private          bool                    mDisposed;

		/// <summary>
		/// The channel lock (used to synchronize channel operations).
		/// </summary>
		protected readonly object Sync = new object();

		#region Initialization and Disposal

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceChannel"/> class.
		/// </summary>
		/// <param name="socket">
		/// A TCP socket representing the network connection between the client and the server of the log service.
		/// </param>
		protected LogServiceChannel(Socket socket)
		{
			mSocket = socket;

			// disable Nagle's algorithm to minimize latency
			mSocket.NoDelay = true;

			// set buffer sizes according to expected link characteristics
			// the buffer size should be the bandwidth-delay-product (BDP) for maximum throughput
			// => assume a link speed of 1 GBit/s and a roundtrip time of 10 ms
			double linkSpeedInBitsPerSecond = 1.0 * 1000 * 1000 * 1000; // 1 GBit/s
			double expectedRoundtripTimeInSeconds = 0.01;               // 10 ms
			if (socket.LocalEndPoint is IPEndPoint lep)
			{
				if (lep.Address.Equals(IPAddress.Loopback) || lep.Address.Equals(IPAddress.IPv6Loopback))
				{
					// the remote endpoint is on the local system
					// => assume link speed of 10 GBit/s and a roundtrip time of 0.1 ms
					linkSpeedInBitsPerSecond = 10.0 * 1000 * 1000 * 1000; // 10 GBit/s
					expectedRoundtripTimeInSeconds = 0.0001;              // 0.1 ms
				}
			}

			int bufferSize = (int)(linkSpeedInBitsPerSecond * expectedRoundtripTimeInSeconds / 8 + 0.5);
			mSocket.ReceiveBufferSize = bufferSize;
			mSocket.SendBufferSize = bufferSize;

			// initialize receive buffers
			for (int i = 0; i < mReceiveOperations.Length; i++)
			{
				mReceiveOperations[i] = new ReceiveOperation(ReceiveBufferSize, (sender, e) => ProcessReceiveCompleted(e));
			}

			// initialize send buffers
			for (int i = 0; i < mSendOperations.Length; i++)
			{
				mSendOperations[i] = new SendOperation((sender, e) => ProcessSendCompleted(e));
			}
		}

		/// <summary>
		/// Disposes the channel.
		/// The call returns immediately, before the channel has actually completed shutting down.
		/// Resources are released as soon as all concurrent processes have completed.
		/// </summary>
		public void Dispose()
		{
			lock (Sync)
			{
				if (mDisposed) return;
				Dispose(true);
				mDisposed = true;
			}
		}

		/// <summary>
		/// Disposes the channel.
		/// The executing thread holds the channel lock (<see cref="Sync"/>) when called.
		/// </summary>
		/// <param name="disposing">Not evaluated, just for the dispose pattern.</param>
		protected virtual void Dispose(bool disposing)
		{
			// trigger shutting down and let the channel clean up itself when finished
			InitiateShutdown();
		}

		#endregion

		#region Channel Status

		/// <summary>
		/// Gets the status of the channel.
		/// </summary>
		public LogServiceChannelStatus Status
		{
			get
			{
				lock (Sync)
				{
					return mStatus;
				}
			}

			private set
			{
				lock (Sync)
				{
					mStatus = value;

					lock (mSendQueueSync)
					{
						mIsSenderOperational = mStatus == LogServiceChannelStatus.Operational;
					}
				}
			}
		}

		#endregion

		#region Starting

		/// <summary>
		/// Starts the channel.
		/// </summary>
		protected internal void Start()
		{
			lock (Sync)
			{
				// abort, if the channel is not in the 'created' status
				if (mStatus != LogServiceChannelStatus.Created)
					throw new InvalidOperationException($"The channel is not in the '{LogServiceChannelStatus.Created}' state.");

				// start reading
				try
				{
					// start receiving
					foreach (var operation in mReceiveOperations)
					{
						// assume that the receive operation can be scheduled
						Debug.Assert(!operation.ReceivingCompleted);

						// start receiving
						bool pending;
						try
						{
							mPendingReceiveOperations++;
							pending = mSocket.ReceiveAsync(operation.EventArgs);
						}
						catch
						{
							// starting to receive failed
							// => no completion callback is pending!
							mPendingReceiveOperations--;
							throw;
						}

						// queue processing data, if the operation finished synchronously
						if (!pending)
							operation.ReceivingCompleted = true;
					}

					// the channel is up and running now
					Status = LogServiceChannelStatus.Operational;
				}
				catch
				{
					// starting to receive has failed
					// => channel is malfunctional, clean up after pending operations have completed
					Status = LogServiceChannelStatus.Malfunctional;
					InitiateShutdown();
					return;
				}

				// let the derived class perform its own work
				try
				{
					OnStarted();
				}
				catch (Exception ex)
				{
					Debug.Fail("OnStart() failed unexpectedly.", ex.ToString());
					Status = LogServiceChannelStatus.Malfunctional;
					InitiateShutdown();
					throw;
				}

				// start processing
				TriggerProcessing();
			}
		}

		/// <summary>
		/// Is called when the channel has been started successfully.
		/// The receiver is not started, yet.
		/// The executing thread holds the channel lock (<see cref="Sync"/>) when called.
		/// </summary>
		protected virtual void OnStarted()
		{
		}

		#endregion

		#region Shutting Down

		/// <summary>
		/// Initiates shutting the channel down
		/// (returns immediately, the shutdown is running in the background).
		/// </summary>
		public void InitiateShutdown()
		{
			lock (Sync)
			{
				switch (mStatus)
				{
					case LogServiceChannelStatus.Created:
					{
						// the channel has been created, but not started, yet
						// => no operations pending
						// => just close the socket
						mSocket?.Dispose();

						// clean up channel resources
						// (there should be no pending send/receive operations when the channel is in this state)
						Debug.Assert(mPendingSendOperations == 0);
						Debug.Assert(mPendingReceiveOperations == 0);
						Status = LogServiceChannelStatus.ShuttingDown;
						TriggerProcessing();
						break;
					}

					case LogServiceChannelStatus.Connecting:
					{
						// the channel is connecting to a remote peer
						// => no send/receive operations pending, but a connect operation
						// => just close the socket to cancel connecting
						mSocket?.Dispose();

						// clean up channel resources
						// (there should be no pending send/receive operations when the channel is in this state)
						Debug.Assert(mPendingSendOperations == 0);
						Debug.Assert(mPendingReceiveOperations == 0);
						Status = LogServiceChannelStatus.ShuttingDown;
						TriggerProcessing();
						break;
					}

					case LogServiceChannelStatus.Operational:
					{
						// the channel is connected to a remote peer
						// => send/receive operations may be pending
						// => initiate shutdown process
						Status = LogServiceChannelStatus.ShuttingDown;

						// disable sending data over the socket
						// => notifies the remote peer that we will send no more data
						// => receive operations at the remote peer complete with 0 bytes received indicating that we're shutting down
						// => remote peer shuts down sending as well
						// => read operations at our socket complete with 0 bytes indicating that the remote peer has shut down
						// => graceful shutdown is complete
						try
						{
							mSocket.Shutdown(SocketShutdown.Send);
						}
						catch (Exception)
						{
							// shutting down the socket can fail, if the client has already closed the connection
							// => ignore that as a broken connection cannot shut down gracefully...
						}

						TriggerProcessing();
						break;
					}

					case LogServiceChannelStatus.ShuttingDown:
					{
						// the channel is shutting down
						// => check whether pending receive/send operations have completed meanwhile
						TriggerProcessing();
						break;
					}

					case LogServiceChannelStatus.ShutdownCompleted:
					{
						// the channel has already shut down
						// => nothing to do here...
						break;
					}

					case LogServiceChannelStatus.Malfunctional:
					{
						// the channel is malfunctional
						// => reset the connection (do not send a FIN packet, just close it)
						if (mSocket != null)
						{
							mSocket.LingerState = new LingerOption(true, 0);
							mSocket.Dispose();
						}

						// clean up as soon as all pending operations have completed
						TriggerProcessing();
						break;
					}

					default:
						throw new NotImplementedException($"Unhandled channel status '{mStatus}'.");
				}
			}
		}

		/// <summary>
		/// Is called when shutting down to determine whether the shutdown has completed.
		/// </summary>
		private void FinishShutdownIfAppropriate()
		{
			Debug.Assert(Monitor.IsEntered(Sync));

			// clean up, if all pending operations have completed
			if (mStatus != LogServiceChannelStatus.Operational &&
			    mPendingSendOperations == 0 &&
			    mPendingReceiveOperations == 0)
			{
				// close the socket
				mSocket?.Dispose();

				// the channel has completely shut down now
				// (keep 'malfunctional' status to indicate that an error has occurred)
				if (mStatus != LogServiceChannelStatus.Malfunctional)
					Status = LogServiceChannelStatus.ShutdownCompleted;

				// let derived classes perform additional cleanup
				try
				{
					OnShutdownCompleted();
				}
				catch (Exception ex)
				{
					Debug.Fail("OnShutdownCompleted() failed unexpectedly.", ex.ToString());
				}
			}
		}

		/// <summary>
		/// Is called when the channel has completed shutting down.
		/// The executing thread holds the channel lock (<see cref="Sync"/>) when called.
		/// </summary>
		protected virtual void OnShutdownCompleted()
		{
		}

		#endregion

		#region Enqueueing for Sending

		// all fields are synchronized using mSendQueueSync
		private readonly object               mSendQueueSync       = new object();
		private readonly Encoder              mUtf8Encoder         = Encoding.UTF8.GetEncoder();
		private          int                  mSendQueueSize       = 10 * 1024 * 1024;
		private volatile int                  mLastSendTickCount   = Environment.TickCount;
		private volatile bool                 mIsSenderOperational = true;
		private          ChainableMemoryBlock mFirstSendBlock      = null;
		private          ChainableMemoryBlock mLastSendBlock       = null;

		// synchronized using interlocked operations
		private volatile int mBytesQueuedToSend = 0;

		/// <summary>
		/// Gets the <see cref="Environment.TickCount"/> the channel has sent some data the last time.
		/// </summary>
		internal int LastSendTickCount => mLastSendTickCount;

		/// <summary>
		/// Gets or sets the size of the send queue (in bytes).
		/// If the queue already contains at least this amount of data, the next send request is denied
		/// (therefore the send queue may contain a few bytes more than <see cref="SendQueueSize"/> is set to).
		/// </summary>
		public int SendQueueSize
		{
			get
			{
				lock (mSendQueueSync)
				{
					return mSendQueueSize;
				}
			}

			set
			{
				if (value < 1)
				{
					throw new ArgumentOutOfRangeException(
						nameof(value),
						value,
						"The size of the send queue must be greater than 0.");
				}

				lock (mSendQueueSync)
				{
					mSendQueueSize = value;
				}
			}
		}

		/// <summary>
		/// Gets the number of bytes that have been queued to send.
		/// </summary>
		public int BytesQueuedToSend => Interlocked.CompareExchange(ref mBytesQueuedToSend, 0, 0);

		/// <summary>
		/// Sends the specified characters.
		/// </summary>
		/// <param name="data">Buffer containing characters to write.</param>
		/// <param name="index">Index in the buffer to start at.</param>
		/// <param name="count">Number of characters to write.</param>
		/// <param name="appendNewLine">
		/// <c>true</c> too append a newline character;
		/// otherwise <c>false</c>.
		/// </param>
		/// <returns>
		/// <c>true</c>, if the specified buffer was successfully enqueued for sending;
		/// <c>false</c>, if the send queue is full.
		/// </returns>
		/// <exception cref="LogServiceChannelNotOperationalException">The log service channel is not operational.</exception>
		protected internal bool Send(
			char[] data,
			int    index,
			int    count,
			bool   appendNewLine)
		{
			if (data == null) throw new ArgumentNullException(nameof(data));
			if (index < 0 || index >= data.Length) throw new ArgumentOutOfRangeException(nameof(index), index, "The index is out of bounds.");
			if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "The count must be positive.");
			if (index + count > data.Length) throw new ArgumentOutOfRangeException(nameof(count), count, "index + count exceeds the bounds of the buffer.");

			return Send(new ReadOnlySpan<char>(data, index, count), appendNewLine);
		}

		/// <summary>
		/// Sends the specified string.
		/// </summary>
		/// <param name="line">String to send.</param>
		/// <param name="appendNewLine">
		/// <c>true</c> too append a newline character;
		/// otherwise <c>false</c>.
		/// </param>
		/// <returns>
		/// <c>true</c>, if the specified buffer was successfully enqueued for sending;
		/// <c>false</c>, if the send queue is full.
		/// </returns>
		/// <exception cref="LogServiceChannelNotOperationalException">The log service channel is not operational.</exception>
		protected internal bool Send(string line, bool appendNewLine = true)
		{
			if (line == null) throw new ArgumentNullException(nameof(line));

			return Send(line.AsSpan(), appendNewLine);
		}

		/// <summary>
		/// Sends the specified characters.
		/// </summary>
		/// <param name="data">Buffer containing characters to write.</param>
		/// <param name="appendNewLine">
		/// <c>true</c> too append a newline character;
		/// otherwise <c>false</c>.
		/// </param>
		/// <returns>
		/// <c>true</c>, if the specified buffer was successfully enqueued for sending;
		/// <c>false</c>, if the send queue is full.
		/// </returns>
		/// <exception cref="LogServiceChannelNotOperationalException">The log service channel is not operational.</exception>
		protected internal bool Send(ReadOnlySpan<char> data, bool appendNewLine)
		{
			if (data == null) throw new ArgumentNullException(nameof(data));

			// abort, if there is nothing to send
			if (data.Length == 0)
				return true;

			// abort, if the sender is not operational
			if (!mIsSenderOperational)
				throw new LogServiceChannelNotOperationalException("The channel is not operational.");

			// abort, if the send queue is full
			if (mBytesQueuedToSend > mSendQueueSize)
				return false;

			int charOffset = 0;
			bool completed = false;
			bool triggerSending = false;
			while (true)
			{
				ChainableMemoryBlock block = null;
				lock (mSendQueueSync)
				{
					AppendSendBufferIfNecessary();
					block = mLastSendBlock;
					Monitor.Enter(block);
				}

				// pull out frequently used properties to save time
				// and avoid boundary checks when changing the length in between
				byte[] blockBuffer = block.Buffer;
				int blockLength = block.Length;
				int blockCapacity = block.Capacity;

				try
				{
					if (!completed)
					{
#if NET461 || NETSTANDARD2_0
						// the Encoder class does not have support for spans, so we have to work around it
						char[] buffer = null;
						int charsUsed, bytesUsed;
						try
						{
							var spanToEncode = data.Slice(charOffset);
							buffer = ArrayPool<char>.Shared.Rent(spanToEncode.Length);
							spanToEncode.CopyTo(buffer.AsSpan());
							mUtf8Encoder.Convert(
								buffer,
								0,
								spanToEncode.Length,
								blockBuffer,
								blockLength,
								blockCapacity - blockLength,
								true,
								out charsUsed,
								out bytesUsed,
								out completed);
						}
						finally
						{
							if (buffer != null)
							{
								ArrayPool<char>.Shared.Return(buffer);
								buffer = null;
							}
						}

#elif NETSTANDARD2_1
						mUtf8Encoder.Convert(
							data.Slice(charOffset),
							new Span<byte>(blockBuffer, blockLength, blockCapacity - blockLength),
							true,
							out int charsUsed,
							out int bytesUsed,
							out completed);
#else
#error Unhandled target framework.
#endif

						// adjust length of valid bytes in the buffer
						blockLength += bytesUsed;

						// adjust the offset in the source buffer
						charOffset += charsUsed;

						// append a new line character, if requested
						bool finished = false;
						if (completed)
						{
							if (appendNewLine)
							{
								if (blockLength < blockCapacity)
								{
									blockBuffer[blockLength++] = (byte)'\n'; // also a newline in UTF-8
									bytesUsed++;
									finished = true;
								}
							}
							else
							{
								finished = true;
							}
						}

						// trigger sending, if this is the added block is the first one to send
						// (pending send operations that complete continue sending automatically)
						triggerSending |= Interlocked.Add(ref mBytesQueuedToSend, bytesUsed) == bytesUsed;

						// abort, if the specified piece of data has been enqueued entirely
						if (finished) break;
					}
					else
					{
						// append a new line character
						Debug.Assert(appendNewLine);
						if (blockLength < blockCapacity)
						{
							blockBuffer[blockLength++] = (byte)'\n'; // also a newline in UTF-8
							Interlocked.Increment(ref mBytesQueuedToSend);
							break;
						}
					}
				}
				finally
				{
					// unlock the block, so it can be sent
					block.Length = blockLength;
					Monitor.Exit(block);
				}
			}

			// update the time of the last send operation
			mLastSendTickCount = Environment.TickCount;

			// start sending, if necessary
			if (triggerSending)
				TriggerSending();

			return true;
		}

		/// <summary>
		/// Appends a new block to the send queue, if the last block is full.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AppendSendBufferIfNecessary()
		{
			if (mLastSendBlock == null || mLastSendBlock.Length == mLastSendBlock.Capacity)
			{
				var block = ChainableMemoryBlock.GetPooled(SendBufferSize, sByteArrayPool, false);
				if (mLastSendBlock == null)
				{
					mFirstSendBlock = block;
					mLastSendBlock = block;
				}
				else
				{
					mLastSendBlock.Next = block;
					mLastSendBlock = block;
				}
			}
		}

		#endregion

		#region Processing

		// all fields are synchronized using mProcessingTriggerSync
		private readonly object mProcessingTriggerSync = new object();
		private readonly ManualResetEventSlim mProcessingNeededEvent = new ManualResetEventSlim();
		private bool mProcessingThreadRunning = false;

		/// <summary>
		/// Triggers the processing thread that handles sending and receiving.
		/// </summary>
		private void TriggerProcessing()
		{
			lock (mProcessingTriggerSync)
			{
				mProcessingNeededEvent.Set();
				if (!mProcessingThreadRunning)
				{
					ThreadPool.QueueUserWorkItem(DoProcessing);
					mProcessingThreadRunning = true;
				}
			}
		}

		/// <summary>
		/// The entry point of the processing thread that handles receiving and processing.
		/// </summary>
		/// <param name="_"></param>
		private void DoProcessing(object _)
		{
			while (true)
			{
				if (!mProcessingNeededEvent.Wait(100))
				{
					// abort, if there is still nothing to process
					lock (mProcessingTriggerSync)
					{
						// abort, if there is nothing to process
						if (!mProcessingNeededEvent.IsSet)
						{
							mProcessingThreadRunning = false;
							return;
						}
					}
				}

				// there should be data to process and the processing thread will process everything
				// => there is nothing left afterwards
				mProcessingNeededEvent.Reset();

				lock (Sync)
				{
					// tell derived class, if a send operation has completed meanwhile
					// (must be done in the processing thread to call all overrides using the same thread to avoid race conditions)
					if (mSendOperationCompleted)
					{
						mSendOperationCompleted = false;

						try
						{
							OnSendingCompleted();
						}
						catch (Exception ex)
						{
							Debug.Fail("OnSendingCompleted() failed unexpectedly.", ex.ToString());
							InitiateShutdown();
						}
					}

					// process completed receive operations and start new ones, if appropriate
					ProcessCompletedReceiveOperations();

					// shut down, if a pending send operation has failed
					if (mSendOperationFailed)
						InitiateShutdown();

					// abort, if the channel is shutting down and all operations have completed
					FinishShutdownIfAppropriate();
					if (mStatus == LogServiceChannelStatus.ShutdownCompleted) break;
				}
			}
		}

		#endregion

		#region Sending

		private readonly object               mSendingTriggerSync      = new object();
		private readonly ManualResetEventSlim mSendingNeededEvent      = new ManualResetEventSlim();
		private          bool                 mSendingThreadRunning    = false;
		private readonly SendOperation[]      mSendOperations          = new SendOperation[MaxConcurrentSendOperations];
		private          int                  mSendOperationToUseIndex = 0;
		private volatile bool                 mSendOperationCompleted  = false;
		private volatile bool                 mSendOperationFailed     = false;
		private volatile int                  mPendingSendOperations   = 0;

		/// <summary>
		/// Triggers the sending thread.
		/// </summary>
		private void TriggerSending()
		{
			// abort, if a previous send operation failed
			// (in this case the following operations will fail as well)
			if (mSendOperationFailed)
				return;

			lock (mSendingTriggerSync)
			{
				mSendingNeededEvent.Set();
				if (!mSendingThreadRunning)
				{
					ThreadPool.QueueUserWorkItem(DoSending);
					mSendingThreadRunning = true;
				}
			}
		}

		/// <summary>
		/// The entry point of the sending thread.
		/// </summary>
		/// <param name="_"></param>
		private void DoSending(object _)
		{
			while (true)
			{
				if (!mSendingNeededEvent.Wait(100))
				{
					// abort, if there is still nothing to process
					lock (mSendingTriggerSync)
					{
						// abort, if there is nothing to process
						if (!mSendingNeededEvent.IsSet)
						{
							mSendingThreadRunning = false;
							return;
						}
					}
				}

				// reset the event requesting sending as we'll send as much data as possible
				// and the event is set every time a send operation completes
				mSendingNeededEvent.Reset();

				while (true)
				{
					// get available send operation object
					var operation = mSendOperations[mSendOperationToUseIndex];
					if (!operation.Available)
						break;

					// dequeue first buffer in the send queue for sending, if...
					// a) no send operation is pending or
					// b) there is a full block in the send queue
					// => optimizes the throughput while keeping the latency low, avoids sending many tiny packets
					ChainableMemoryBlock block = null;
					lock (mSendQueueSync)
					{
						if (mFirstSendBlock != null && (mPendingSendOperations < 1 || mFirstSendBlock.Length == mFirstSendBlock.Capacity))
						{
							block = mFirstSendBlock;
							mFirstSendBlock = mFirstSendBlock.Next;
							if (mFirstSendBlock == null) mLastSendBlock = null;
							block.Next = null;
						}
					}

					// abort, if there is nothing to send
					if (block == null)
						break;

					// Debug.WriteLine("Sending: {0} bytes", block.Length);

					// wait for the writer to finish filling the block, if necessary
					Monitor.Enter(block);
					Monitor.Exit(block);

					// attach the block to the send operation
					Debug.Assert(operation.Buffer == null);
					operation.Available = false;
					operation.Buffer = block;

					// proceed with the next send operation in the next run
					mSendOperationToUseIndex = (mSendOperationToUseIndex + 1) % mSendOperations.Length;

					// send the buffer
					bool sendPending;
					try
					{
						Interlocked.Increment(ref mPendingSendOperations);
						sendPending = mSocket.SendAsync(operation.EventArgs);
					}
					catch (Exception)
					{
						// sending failed, return send operation object and close the channel
						Interlocked.Decrement(ref mPendingSendOperations);
						ReleaseSendOperation(operation);
						mSendOperationFailed = true;
						mIsSenderOperational = false;
						TriggerProcessing(); // ensures that the error is processed appropriately and overrides are invoked
						break;
					}

					// handle completing synchronously
					if (!sendPending) ProcessSendCompleted(operation.EventArgs);
				}
			}
		}

		/// <summary>
		/// Processes the completion of an asynchronous send operation.
		/// </summary>
		/// <param name="e">Event arguments associated with the send operation.</param>
		private void ProcessSendCompleted(SocketAsyncEventArgs e)
		{
			var completedOperation = (SendOperation)e.UserToken;

			// reduce the number of queued bytes
			int bytesQueuedToSend = Interlocked.Add(ref mBytesQueuedToSend, -e.Count);
			Debug.Assert(bytesQueuedToSend >= 0);

			try
			{
				if (e.SocketError == SocketError.Success)
					mSendOperationCompleted = true;

				if (e.SocketError != SocketError.Success)
				{
					mSendOperationFailed = true;
					mIsSenderOperational = false;
				}

				ReleaseSendOperation(completedOperation);
			}
			finally
			{
				Interlocked.Decrement(ref mPendingSendOperations);
			}

			TriggerSending();    // ensures that the next block is sent, if necessary
			TriggerProcessing(); // ensures that OnSendCompleted() is invoked
		}

		/// <summary>
		/// Returns a <see cref="SendOperation"/> back to the list of available send operations for re-use.
		/// </summary>
		/// <param name="operation">Send operation to return.</param>
		private void ReleaseSendOperation(SendOperation operation)
		{
			if (operation.Buffer != null)
			{
				operation.Buffer.Dispose();
				operation.Buffer = null;
			}

			operation.Available = true;
		}

		#endregion

		#region Processing (Receiver Part)

		/// <summary>
		/// Size of a conversion buffer for received data (in chars).
		/// </summary>
		private static readonly int sReceivedDataConversionBufferSize = Encoding.UTF8.GetMaxCharCount(ReceiveBufferSize);

		private readonly Decoder            mUtf8Decoder                        = Encoding.UTF8.GetDecoder();
		private readonly ReceiveOperation[] mReceiveOperations                  = new ReceiveOperation[MaxConcurrentReceiveOperations];
		private          int                mReceiveOperationToProcessNextIndex = 0;
		private          char[]             mIncompleteReceivedLineBuffer       = new char[100];
		private          int                mIncompleteReceivedLineBufferLength = 0;
		private          int                mPendingReceiveOperations           = 0;
		private volatile int                mLastReceiveTickCount               = Environment.TickCount;

		/// <summary>
		/// Is called when the channel has received a complete line.
		/// </summary>
		public event LineReceivedEventHandler LineReceived;

		/// <summary>
		/// Gets the <see cref="Environment.TickCount"/> the channel has received some data the last time.
		/// </summary>
		internal int LastReceiveTickCount => mLastReceiveTickCount;

		/// <summary>
		/// Processes the completion of an asynchronous receive operation.
		/// </summary>
		/// <param name="e">Event arguments associated with the receive operation.</param>
		private void ProcessReceiveCompleted(SocketAsyncEventArgs e)
		{
			var completedOperation = (ReceiveOperation)e.UserToken;

			// mark the receive operation object for processing
			completedOperation.ReceivingCompleted = true;

			if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)
			{
				// receiving completed successfully
				mLastReceiveTickCount = Environment.TickCount;
			}

			// Debug.WriteLine("Received: {0} bytes", e.BytesTransferred);

			// trigger processing, if necessary
			TriggerProcessing();
		}

		/// <summary>
		/// Processes completed asynchronous receive operations.
		/// Must be called by the processing thread only.
		/// </summary>
		private void ProcessCompletedReceiveOperations()
		{
			Debug.Assert(Monitor.IsEntered(Sync));

			int startIndex = mReceiveOperationToProcessNextIndex;

			while (true)
			{
				// check whether the next expected buffer is ready to be processed
				var operation = mReceiveOperations[mReceiveOperationToProcessNextIndex];

				// abort, if receiving the buffer has not been completed, yet
				if (!operation.ReceivingCompleted)
					break;

				// receiving has completed
				bool success;
				try
				{
					// check whether the receive operation completed successfully, so data can be processed
					success = operation.EventArgs.BytesTransferred > 0 && operation.EventArgs.SocketError == SocketError.Success;

					// process received data, if receiving completed successfully,
					// otherwise shut the channel down
					if (success)
					{
						// process received data
						char[] decodingBuffer = null;
						try
						{
							// convert the received UTF-8 encoded data to UTF-16 for further processing
							decodingBuffer = sCharArrayPool.Rent(sReceivedDataConversionBufferSize);
							mUtf8Decoder.Convert(
								operation.Buffer,
								0,
								operation.EventArgs.BytesTransferred,
								decodingBuffer,
								0,
								decodingBuffer.Length,
								true,
								out int _,
								out int charsUsed,
								out bool _);

							// process decoded data
							ProcessReceivedCharacters(new ReadOnlySpan<char>(decodingBuffer, 0, charsUsed));
						}
						finally
						{
							// return the decoding buffer to the pool
							if (decodingBuffer != null)
								sCharArrayPool.Return(decodingBuffer);
						}
					}
					else
					{
						InitiateShutdown();
					}
				}
				finally
				{
					operation.ReceivingCompleted = false;
					mPendingReceiveOperations--;
				}

				// proceed processing the next buffer
				mReceiveOperationToProcessNextIndex = (mReceiveOperationToProcessNextIndex + 1) % mReceiveOperations.Length;

				// start receiving, if the buffer was received successfully
				if (success && mStatus == LogServiceChannelStatus.Operational)
				{
					bool receivePending;
					try
					{
						mPendingReceiveOperations++;
						Debug.Assert(!operation.ReceivingCompleted);
						receivePending = mSocket.ReceiveAsync(operation.EventArgs);
					}
					catch (Exception)
					{
						// an error occurred, close the connection
						mPendingReceiveOperations--;
						InitiateShutdown();
						break;
					}

					// handle completing synchronously
					if (!receivePending)
					{
						operation.ReceivingCompleted = true;

						// abort processing immediately, if all buffers have been processed this run
						// (helps to process send operations in between)
						if (mReceiveOperationToProcessNextIndex == startIndex)
						{
							mProcessingNeededEvent.Set();
							break;
						}
					}
				}
			}
		}

		/// <summary>
		/// Processes received data that has already been converted to UTF-16 (for internal use, not synchronized).
		/// </summary>
		/// <param name="buffer">Buffer containing characters to process.</param>
		private void ProcessReceivedCharacters(ReadOnlySpan<char> buffer)
		{
			////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
			// fill up incomplete line from last processing run, if necessary
			////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

			if (mIncompleteReceivedLineBufferLength > 0)
			{
				// determine the portion filling up the incomplete line
				int index = buffer.IndexOf('\n');
				var sliceToCopy = index >= 0 ? buffer.Slice(0, index) : buffer;

				// append the portion to the incomplete line
				if (!sliceToCopy.IsEmpty)
				{
					// resize buffer, if necessary
					if (mIncompleteReceivedLineBufferLength + sliceToCopy.Length > mIncompleteReceivedLineBuffer.Length)
					{
						char[] newBuffer = new char[mIncompleteReceivedLineBufferLength + sliceToCopy.Length];
						Array.Copy(mIncompleteReceivedLineBuffer, newBuffer, mIncompleteReceivedLineBufferLength);
						mIncompleteReceivedLineBuffer = newBuffer;
					}

					// append characters to the buffer storing the incomplete line
					sliceToCopy.CopyTo(
						new Span<char>(
							mIncompleteReceivedLineBuffer,
							mIncompleteReceivedLineBufferLength,
							sliceToCopy.Length));

					mIncompleteReceivedLineBufferLength += sliceToCopy.Length;
				}

				// abort, if the buffer has been processed entirely
				if (index < 0)
					return;

				// a newline character was found
				// => the line is complete now, process it!
				try
				{
					OnLineReceived(
						new ReadOnlySpan<char>(
							mIncompleteReceivedLineBuffer,
							0,
							mIncompleteReceivedLineBufferLength).TrimEnd('\r'));
				}
				catch (Exception ex)
				{
					Debug.Fail("OnLineReceived() failed unexpectedly.", ex.ToString());
				}

				// the incomplete line buffer is empty now
				mIncompleteReceivedLineBufferLength = 0;

				// adjust the remaining buffer
				buffer = buffer.Slice(index + 1);
			}

			////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
			// process complete lines just from the buffer
			////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

			while (true)
			{
				int index = buffer.IndexOf('\n');
				if (index < 0) break;

				try
				{
					OnLineReceived(buffer.Slice(0, index).TrimEnd('\r'));
				}
				catch (Exception ex)
				{
					Debug.Fail("OnLineReceived() failed unexpectedly.", ex.ToString());
				}

				buffer = buffer.Slice(index + 1);
			}

			////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
			// copy remaining characters into the line buffer to process them in the next run
			////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

			if (!buffer.IsEmpty)
			{
				// resize line buffer, if necessary
				Debug.Assert(mIncompleteReceivedLineBufferLength == 0);
				if (buffer.Length > mIncompleteReceivedLineBuffer.Length)
					mIncompleteReceivedLineBuffer = new char[buffer.Length];

				buffer.CopyTo(new Span<char>(mIncompleteReceivedLineBuffer));
				mIncompleteReceivedLineBufferLength = buffer.Length;
			}
		}

		/// <summary>
		/// Is called directly after some data has been received successfully.
		/// The executing thread holds the channel lock (<see cref="Sync"/>) when called.
		/// </summary>
		protected virtual void OnDataReceived()
		{
		}

		/// <summary>
		/// Is called when the channel has received a complete line.
		/// Raises the <see cref="LineReceived"/> event.
		/// The executing thread holds the channel lock (<see cref="Sync"/>) when called.
		/// </summary>
		/// <param name="line">Line to process.</param>
		protected virtual void OnLineReceived(ReadOnlySpan<char> line)
		{
			var handler = LineReceived;
			handler?.Invoke(this, line);
		}

		/// <summary>
		/// Is called when the channel has completed sending a chunk of data.
		/// The executing thread holds the channel lock (<see cref="Sync"/>) when called.
		/// </summary>
		protected virtual void OnSendingCompleted()
		{
		}

		#endregion
	}

}
