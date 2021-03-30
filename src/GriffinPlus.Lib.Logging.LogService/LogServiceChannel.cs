///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using GriffinPlus.Lib.Collections;

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

		// channel management members
		private           Socket                        mSocket;
		internal readonly CancellationToken             ShutdownToken;
		private readonly  CancellationTokenRegistration mShutdownTokenRegistration;
		private           LogServiceChannelStatus       mStatus = LogServiceChannelStatus.Created;
		private           bool                          mDisposed;

		/// <summary>
		/// The channel lock (used to synchronize socket operations).
		/// </summary>
		protected readonly object Sync = new object();

		/// <summary>
		/// The processing lock (used to synchronize processing received data).
		/// </summary>
		protected readonly object ProcessingSync = new object();

		#region Initialization and Disposal

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceChannel"/> class.
		/// </summary>
		/// <param name="socket">
		/// A TCP socket representing the network connection between the client and the server of the log service.
		/// </param>
		/// <param name="shutdownToken">
		/// CancellationToken that is signaled to shut the channel down.
		/// </param>
		protected LogServiceChannel(Socket socket, CancellationToken shutdownToken)
		{
			// configure the socket
			mSocket = socket;
			mSocket.NoDelay = true;
			mSocket.ReceiveBufferSize = 64 * 1024;
			mSocket.SendBufferSize = 64 * 1024;

			// initialize receive buffers
			for (int i = 0; i < mReceiveOperations.Length; i++)
			{
				mReceiveOperations[i] = new ReceiveOperation(ReceiveBufferSize, (sender, e) => ProcessReceiveCompleted(e));
			}

			// register handler with the shutdown token (may be invoked synchronously, if the token is signaled)
			// (FinishShutdownIfAppropriate() will dispose the token registration at the end)
			ShutdownToken = shutdownToken;
			mShutdownTokenRegistration = shutdownToken.Register(InitiateShutdown);
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
		/// Disposes the channel
		/// (the executing thread holds the channel lock (<see cref="Sync"/>) when called).
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

					lock (mScheduledSendItems)
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
			lock (ProcessingSync) // needed to avoid a race condition causing received data to be processed before OnStart() has run
			{
				lock (Sync)
				{
					// abort, if the channel is not in the 'created' status
					if (mStatus != LogServiceChannelStatus.Created)
						throw new InvalidOperationException($"The channel is not in the '{LogServiceChannelStatus.Created}' state.");

					// the shutdown token was not signaled
					// => start reading
					try
					{
						// start receiving
						foreach (var operation in mReceiveOperations)
						{
							lock (operation)
							{
								// assume that the receive operation can be scheduled
								Debug.Assert(!operation.ProcessingPending);

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
								// (avoids running into a stack overflow in case of heavy traffic and invoking the handler from within the channel lock)
								if (!pending)
								{
									ThreadPool.QueueUserWorkItem(
										obj => ((LogServiceChannel)obj).ProcessReceiveCompleted(operation.EventArgs),
										this);
								}
							}
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
			}
		}

		/// <summary>
		/// Is called when the channel has been started successfully.
		/// (the executing thread holds the processing lock (<see cref="ProcessingSync"/>) when called).
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
						// the channel has been created, but the shutdown token is already signaled
						// => no operations pending
						// => just close the socket
						if (mSocket != null)
						{
							mSocket.Close();
							mSocket = null;
						}

						// clean up channel resources
						// (there should be no pending send/receive operations when the channel is in this state)
						Debug.Assert(mPendingSendOperations == 0);
						Debug.Assert(mPendingReceiveOperations == 0);
						FinishShutdownIfAppropriate();
						break;
					}

					case LogServiceChannelStatus.Connecting:
					{
						// the channel is connecting to a remote peer
						// => no send/receive operations pending, but a connect operation
						// => just close the socket to cancel connecting
						if (mSocket != null)
						{
							mSocket.Close();
							mSocket = null;
						}

						// clean up channel resources
						// (there should be no pending send/receive operations when the channel is in this state)
						Debug.Assert(mPendingSendOperations == 0);
						Debug.Assert(mPendingReceiveOperations == 0);
						FinishShutdownIfAppropriate();
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

						FinishShutdownIfAppropriate();
						break;
					}

					case LogServiceChannelStatus.ShuttingDown:
					{
						// the channel is shutting down
						// => check whether pending receive/send operations have completed meanwhile
						FinishShutdownIfAppropriate();
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
							mSocket.Close();
							mSocket = null;
						}

						// clean up as soon as all pending operations have completed
						FinishShutdownIfAppropriate();
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
				if (mSocket != null)
				{
					mSocket.Close();
					mSocket = null;
				}

				// the channel has completely shut down now
				// (keep 'malfunctional' status to indicate that an error has occurred)
				if (mStatus != LogServiceChannelStatus.Malfunctional)
					Status = LogServiceChannelStatus.ShutdownCompleted;

				// dispose token registration to avoid leaks
				// ReSharper disable once PossiblyImpureMethodCallOnReadonlyVariable
				mShutdownTokenRegistration.Dispose();

				// let derived classes perform additional cleanup
				OnShutdownCompleted();
			}
		}

		/// <summary>
		/// Is called when the channel has completed shutting down.
		/// (the executing thread holds the channel lock (<see cref="Sync"/>) when called).
		/// </summary>
		protected virtual void OnShutdownCompleted()
		{
		}

		#endregion

		#region Receiving Data / Processing

		/// <summary>
		/// Number of receive buffers.
		/// </summary>
		private const int ReceiveBufferCount = 10;

		/// <summary>
		/// Size of a receive buffer.
		/// </summary>
		private const int ReceiveBufferSize = 32 * 1024;

		private readonly Decoder            mUtf8Decoder                        = Encoding.UTF8.GetDecoder();
		private readonly ReceiveOperation[] mReceiveOperations                  = new ReceiveOperation[ReceiveBufferCount]; // synchronized via ProcessingSync
		private          int                mReceiveOperationToProcessNextIndex = 0;                                        // synchronized via ProcessingSync
		private          char[]             mIncompleteReceivedLineBuffer       = new char[100];                            // synchronized via ProcessingSync
		private          int                mIncompleteReceivedLineBufferLength = 0;                                        // synchronized via ProcessingSync
		private          int                mPendingReceiveOperations           = 0;                                        // synchronized via Sync
		private volatile int                mLastReceiveTickCount               = Environment.TickCount;                    // not synchronized, but volatile

		/// <summary>
		/// Is called when the channel has received a complete line.
		/// </summary>
		public event LineReceivedEventHandler LineReceived;

		/// <summary>
		/// Gets the number of pending send operations.
		/// </summary>
		public int PendingReceiveOperations
		{
			get
			{
				lock (Sync) return mPendingReceiveOperations;
			}
		}

		/// <summary>
		/// Gets the <see cref="Environment.TickCount"/> the channel has received some data the last time.
		/// </summary>
		internal int LastReceiveTickCount => mLastReceiveTickCount;


		/// <summary>
		/// Processes the completion of an asynchronous receive operation.
		/// </summary>
		/// <param name="e">Event arguments associated with the receive operation.</param>
		/// <param name="process">
		/// <c>true</c> to process the received buffer;
		/// <c>false</c> to signal that the buffer has been completed only.
		/// </param>
		private void ProcessReceiveCompleted(SocketAsyncEventArgs e, bool process = true)
		{
			var completedOperation = (ReceiveOperation)e.UserToken;

			// the executing thread should not hold any locks to avoid deadlocks when 
			// user-defined handlers are invoked
			Debug.Assert(!Monitor.IsEntered(Sync));
			Debug.Assert(!Monitor.IsEntered(ProcessingSync));
			Debug.Assert(!Monitor.IsEntered(mScheduledSendItems));

			try
			{
				bool shutdown;

				lock (completedOperation)
				{
					// shut down, if the remote peer has gracefully closed the connection or an error occurred
					shutdown = e.BytesTransferred == 0 || e.SocketError != SocketError.Success;

					if (!shutdown)
					{
						// the operation has completed successfully
						completedOperation.ProcessingPending = true;
					}
				}

				// shut the channel down, if necessary
				if (shutdown)
				{
					InitiateShutdown();
					return;
				}

				// update the time of the last receive operation
				mLastReceiveTickCount = Environment.TickCount;

				// let derived class perform its own work
				try
				{
					OnDataReceived();
				}
				catch (Exception ex)
				{
					Debug.Fail("OnDataReceived() failed unexpectedly.", ex.ToString());
				}

				// abort, if the received buffer should not be processed
				if (!process)
					return;

				// process as many received buffers as possible
				// (there may be other operations that completed before, but belong to one of the following buffers)
				ProcessReceivedBuffers();
			}
			finally
			{
				lock (Sync)
				{
					mPendingReceiveOperations--;
					FinishShutdownIfAppropriate();
				}
			}
		}

		/// <summary>
		/// Processes as many received buffers as possible.
		/// </summary>
		private void ProcessReceivedBuffers()
		{
			// the executing thread should not hold any locks to avoid deadlocks when 
			// user-defined handlers are invoked
			Debug.Assert(!Monitor.IsEntered(Sync));
			Debug.Assert(!Monitor.IsEntered(ProcessingSync));
			Debug.Assert(!Monitor.IsEntered(mScheduledSendItems));

			while (true)
			{
				ReceiveOperation operation;
				bool receivePending;

				lock (ProcessingSync)
				{
					operation = mReceiveOperations[mReceiveOperationToProcessNextIndex];

					char[] decodingBuffer = null;
					try
					{
						int charsUsed;
						lock (operation)
						{
							// abort, if there is no buffer to process
							if (!operation.ProcessingPending)
								break;

							// convert the received UTF-8 encoded data to UTF-16 for further processing
							decodingBuffer = sCharArrayPool.Rent(Encoding.UTF8.GetMaxCharCount(operation.EventArgs.BytesTransferred));
							mUtf8Decoder.Convert(
								operation.Buffer,
								0,
								operation.EventArgs.BytesTransferred,
								decodingBuffer,
								0,
								decodingBuffer.Length,
								true,
								out int _,
								out charsUsed,
								out bool _);

							operation.ProcessingPending = false;
						}

						// process decoded data
						ProcessReceivedCharacters(new ReadOnlySpan<char>(decodingBuffer, 0, charsUsed));
					}
					finally
					{
						// return the decoding buffer to the pool
						if (decodingBuffer != null)
							sCharArrayPool.Return(decodingBuffer);
					}

					// proceed processing the next buffer
					mReceiveOperationToProcessNextIndex = (mReceiveOperationToProcessNextIndex + 1) % mReceiveOperations.Length;

					// start receiving using the processed buffer
					lock (Sync)
					{
						try
						{
							mPendingReceiveOperations++;
							lock (operation)
							{
								Debug.Assert(!operation.ProcessingPending);
								receivePending = mSocket.ReceiveAsync(operation.EventArgs);
							}
						}
						catch (Exception)
						{
							// an error occurred, close the connection
							mPendingReceiveOperations--;
							InitiateShutdown();
							return;
						}
					}
				}

				// handle completing synchronously,
				// but do not process any data as processing is already in progress at this level
				if (!receivePending)
				{
					ProcessReceiveCompleted(operation.EventArgs, false);
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
				OnLineReceived(
					new ReadOnlySpan<char>(
						mIncompleteReceivedLineBuffer,
						0,
						mIncompleteReceivedLineBufferLength).TrimEnd('\r'));

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
				OnLineReceived(buffer.Slice(0, index).TrimEnd('\r'));
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
		/// </summary>
		protected virtual void OnDataReceived()
		{
		}

		/// <summary>
		/// Is called when the channel has received a complete line.
		/// Raises the <see cref="LineReceived"/> event.
		/// The executing thread holds the processing lock (<see cref="ProcessingSync"/>) when called.
		/// </summary>
		/// <param name="line">Line to process.</param>
		protected virtual void OnLineReceived(ReadOnlySpan<char> line)
		{
			var handler = LineReceived;
			handler?.Invoke(this, line);
		}

		#endregion

		#region Sending (Queueing Part)

		private struct ScheduledSendItem
		{
			public ScheduledSendItem(byte[] buffer)
			{
				Buffer = buffer;
				Length = 0;
			}

			public readonly byte[] Buffer;
			public          int    Length;
		}

		// all fields are synchronized using the 'mScheduledSendItems' field
		private readonly Encoder                  mUtf8Encoder         = Encoding.UTF8.GetEncoder();
		private readonly Deque<ScheduledSendItem> mScheduledSendItems  = new Deque<ScheduledSendItem>();
		private          int                      mSendQueueSize       = 10 * 1024 * 1024;
		private          int                      mBytesQueuedToSend   = 0;
		private          bool                     mIsSenderOperational = true;

		/// <summary>
		/// Gets or sets the size of the send queue (in bytes).
		/// </summary>
		public int SendQueueSize
		{
			get
			{
				lock (mScheduledSendItems)
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

				lock (mScheduledSendItems)
				{
					mSendQueueSize = value;
				}
			}
		}

		/// <summary>
		/// Gets the number of bytes that have been queued to send.
		/// </summary>
		public int BytesQueuedToSend
		{
			get
			{
				lock (mScheduledSendItems)
				{
					return mBytesQueuedToSend;
				}
			}
		}

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
		/// <exception cref="LogServiceChannelQueueFullException">The send queue is full.</exception>
		protected internal void Send(
			char[] data,
			int    index,
			int    count,
			bool   appendNewLine)
		{
			if (data == null) throw new ArgumentNullException(nameof(data));
			if (index < 0 || index >= data.Length) throw new ArgumentOutOfRangeException(nameof(index), index, "The index is out of bounds.");
			if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "The count must be positive.");
			if (index + count > data.Length) throw new ArgumentOutOfRangeException(nameof(count), count, "index + count exceeds the bounds of the buffer.");

			Send(new ReadOnlySpan<char>(data, index, count), appendNewLine);
		}

		/// <summary>
		/// Sends the specified string.
		/// </summary>
		/// <param name="line">String to send.</param>
		/// <param name="appendNewLine">
		/// <c>true</c> too append a newline character;
		/// otherwise <c>false</c>.
		/// </param>
		/// <exception cref="LogServiceChannelQueueFullException">The send queue is full.</exception>
		protected internal void Send(string line, bool appendNewLine = true)
		{
			if (line == null) throw new ArgumentNullException(nameof(line));

			Send(line.AsSpan(), appendNewLine);
		}

		/// <summary>
		/// Sends the specified characters.
		/// </summary>
		/// <param name="data">Buffer containing characters to write.</param>
		/// <param name="appendNewLine">
		/// <c>true</c> too append a newline character;
		/// otherwise <c>false</c>.
		/// </param>
		/// <exception cref="LogServiceChannelQueueFullException">The send queue is full.</exception>
		protected internal
#if NET461 || NETSTANDARD2_0
			unsafe
#endif
			void Send(ReadOnlySpan<char> data, bool appendNewLine)
		{
			if (data == null) throw new ArgumentNullException(nameof(data));

			// abort, if there is nothing to send
			if (data.Length == 0)
				return;

			ScheduledSendItem ssi = default;
			try
			{
				// get a buffer for scheduling the send operation
				ssi = GetScheduledSendItem(Encoding.UTF8.GetMaxByteCount(data.Length + 1));

#if NET461 || NETSTANDARD2_0
				bool completed;
				int bytesUsed;
				fixed (char* pData = &data.GetPinnableReference())
				fixed (byte* pBuffer = &ssi.Buffer[0])
				{
					mUtf8Encoder.Convert(
						pData,
						data.Length,
						pBuffer,
						ssi.Buffer.Length,
						true,
						out _,
						out bytesUsed,
						out completed);
				}
#elif NETSTANDARD2_1
				mUtf8Encoder.Convert(
					data,
					new Span<byte>(ssi.Buffer, 0, ssi.Buffer.Length),
					true,
					out _,
					out int bytesUsed,
					out bool completed);
#else
#error Unhandled target framework.
#endif

				// the conversion should always complete as the destination buffer is large enough
				Debug.Assert(completed);

				// append a new line character, if requested
				if (appendNewLine) ssi.Buffer[bytesUsed++] = (byte)'\n'; // also a newline in UTF-8

				// update length of the buffer and schedule buffer for sending
				ssi.Length = bytesUsed;

				// abort, if there is nothing to send
				if (ssi.Length == 0)
					return;

				bool triggerSending;
				lock (mScheduledSendItems)
				{
					// abort, if the sender is not operational, i.e. the channel is not connected
					if (!mIsSenderOperational)
						throw new LogServiceChannelNotConnectedException(GetType().FullName);

					// abort, if the send queue is full
					if (mBytesQueuedToSend + ssi.Length > mSendQueueSize)
						throw new LogServiceChannelQueueFullException("The send queue is full.");

					// trigger sending, if no data is scheduled to be sent
					triggerSending = mBytesQueuedToSend == 0;

					// schedule prepared buffer for sending
					mScheduledSendItems.AddToBack(ssi);
					mBytesQueuedToSend += ssi.Length;
					ssi = default; // avoids releasing the buffer in the finally block
				}

				// start sending, if appropriate
				if (triggerSending)
					ThreadPool.QueueUserWorkItem(obj => ((LogServiceChannel)obj).StartSending(), this);
			}
			finally
			{
				if (ssi.Buffer != null)
					ReturnScheduledSendItem(ref ssi);
			}
		}

		/// <summary>
		/// Gets a <see cref="ScheduledSendItem"/> prepared for sending a buffer.
		/// </summary>
		/// <param name="minimumBufferSize">Minimum size of the buffer to associate with the operation.</param>
		/// <returns>The requested event arguments.</returns>
		private ScheduledSendItem GetScheduledSendItem(int minimumBufferSize)
		{
			byte[] buffer = sByteArrayPool.Rent(minimumBufferSize);
			return new ScheduledSendItem(buffer);
		}

		/// <summary>
		/// Returns the specified <see cref="ScheduledSendItem"/> for sending a buffer to the pool.
		/// </summary>
		/// <param name="item">The item to return to the pool.</param>
		private void ReturnScheduledSendItem(ref ScheduledSendItem item)
		{
			sByteArrayPool.Return(item.Buffer);
		}

		#endregion

		#region Sending (Networking Part)

		private readonly Stack<SocketAsyncEventArgs> mSendSocketAsyncEventArgsStack = new Stack<SocketAsyncEventArgs>(); // synchronized via mSendSocketAsyncEventArgsStack
		private readonly int                         mMaxConcurrentSendOperations   = 1;                                 // no synchronization needed, since constant
		private volatile int                         mLastSendTickCount             = Environment.TickCount;             // not synchronized, but volatile
		private          int                         mPendingSendOperations         = 0;                                 // synchronized via Sync

		/// <summary>
		/// Gets the number of pending send operations.
		/// </summary>
		public int PendingSendOperations
		{
			get
			{
				lock (Sync) return mPendingSendOperations;
			}
		}

		/// <summary>
		/// Gets the <see cref="Environment.TickCount"/> the channel has sent some data the last time.
		/// </summary>
		internal int LastSendTickCount => mLastSendTickCount;

		/// <summary>
		/// Starts sending scheduled data.
		/// </summary>
		private void StartSending()
		{
			while (true)
			{
				SocketAsyncEventArgs e = null;
				try
				{
					bool sendPending;

					lock (Sync)
					{
						// abort, if the maximum number of concurrent send operations is reached
						if (mPendingSendOperations >= mMaxConcurrentSendOperations)
							break;

						lock (mScheduledSendItems)
						{
							// determine how many items can be sent in the next run
							// limit the total size to 80 kByte (buffer can be allocated on the small object heap avoiding large object heap issues)
							int requiredSendBufferSize = 0;
							for (int i = 0; i < mScheduledSendItems.Count; i++)
							{
								var item = mScheduledSendItems[i];
								if (requiredSendBufferSize + item.Length > 80 * 1024) break;
								requiredSendBufferSize += item.Length;
							}

							// abort, if there is nothing to send
							if (requiredSendBufferSize == 0)
								break;

							// allocate send buffer
							e = GetSendSocketEventArgs(requiredSendBufferSize);

							// copy scheduled send items into the send buffer
							int writeIndex = 0;
							while (writeIndex < requiredSendBufferSize)
							{
								var item = mScheduledSendItems[0];

								Array.Copy(
									item.Buffer,
									0,
									e.Buffer,
									writeIndex,
									item.Length);

								mScheduledSendItems.RemoveFromFront();
								ReturnScheduledSendItem(ref item);
								writeIndex += item.Length;
							}

							// update the length of the send buffer
							e.SetBuffer(e.Buffer, 0, writeIndex);

							// send the buffer
							try
							{
								mPendingSendOperations++;
								sendPending = mSocket.SendAsync(e); // throws, if the channel has been closed
							}
							catch (Exception)
							{
								// sending failed, return buffer to the pool and close the channel
								mPendingSendOperations--;
								ReturnSendSocketEventArgs(e);
								e = null; // avoids returning send buffer after disposing the channel
								InitiateShutdown();
								return;
							}
						}
					}

					// handle synchronous completion of the operation
					if (!sendPending)
					{
						ProcessSendCompleted(e, false);
					}

					// buffer has been successfully sent or scheduled to be sent
					// => avoid returning the buffer in the finally block
					e = null;
				}
				finally
				{
					if (e != null)
						ReturnSendSocketEventArgs(e);
				}
			}
		}

		/// <summary>
		/// Processes the completion of an asynchronous send operation.
		/// </summary>
		/// <param name="e">Event arguments associated with the send operation.</param>
		/// <param name="sendNext"><c>true</c> to send the next buffer, if appropriate; otherwise <c>false</c>.</param>
		private void ProcessSendCompleted(SocketAsyncEventArgs e, bool sendNext)
		{
			Debug.Assert(!Monitor.IsEntered(Sync));

			lock (Sync)
			{
				try
				{
					lock (mScheduledSendItems)
					{
						mBytesQueuedToSend -= e.Count;
						Debug.Assert(mBytesQueuedToSend >= 0);
					}

					if (e.SocketError == SocketError.Success)
					{
						// sending completed successfully
						mLastSendTickCount = Environment.TickCount;

						// return event arguments to the pool for reuse
						ReturnSendSocketEventArgs(e);
					}
					else
					{
						// sending failed
						// => close the connection and clean up
						ReturnSendSocketEventArgs(e);
						InitiateShutdown();
						return;
					}
				}
				finally
				{
					mPendingSendOperations--;
					Debug.Assert(mPendingSendOperations == 0);
					FinishShutdownIfAppropriate();
					sendNext &= mStatus == LogServiceChannelStatus.Operational;
				}
			}

			// let the derived class perform its own work
			OnSendingCompleted();

			// send the next buffer, if appropriate
			if (sendNext)
			{
				try
				{
					StartSending();
				}
				catch (Exception ex)
				{
					Debug.Fail("Sending the next buffer failed unexpectedly.", ex.ToString());
					InitiateShutdown();
				}
			}
		}

		/// <summary>
		/// Is called when the channel has completed sending a chunk of data.
		/// </summary>
		protected virtual void OnSendingCompleted()
		{
		}

		/// <summary>
		/// Gets <see cref="SocketAsyncEventArgs"/> prepared for sending a buffer.
		/// </summary>
		/// <param name="minimumBufferSize">Minimum size of the buffer to associate with the operation.</param>
		/// <returns>The requested event arguments.</returns>
		private SocketAsyncEventArgs GetSendSocketEventArgs(int minimumBufferSize)
		{
			SocketAsyncEventArgs args;
			byte[] buffer;

			lock (mSendSocketAsyncEventArgsStack)
			{
				if (mSendSocketAsyncEventArgsStack.Count > 0)
				{
					args = mSendSocketAsyncEventArgsStack.Pop();
					buffer = sByteArrayPool.Rent(minimumBufferSize);
					args.SetBuffer(buffer, 0, 0);
					return args;
				}
			}

			args = new SocketAsyncEventArgs();
			buffer = sByteArrayPool.Rent(minimumBufferSize);
			args.SetBuffer(buffer, 0, 0);
			args.Completed += (sender, e) => ProcessSendCompleted(e, true);
			return args;
		}

		/// <summary>
		/// Returns the specified <see cref="SocketAsyncEventArgs"/> for sending a buffer to the pool.
		/// </summary>
		/// <param name="e">The event arguments to return to the pool.</param>
		private void ReturnSendSocketEventArgs(SocketAsyncEventArgs e)
		{
			sByteArrayPool.Return(e.Buffer);
			e.SetBuffer(null, 0, 0);
			lock (mSendSocketAsyncEventArgsStack)
			{
				mSendSocketAsyncEventArgsStack.Push(e);
			}
		}

		#endregion
	}

}
