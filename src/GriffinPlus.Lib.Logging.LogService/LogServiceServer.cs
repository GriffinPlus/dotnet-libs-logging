///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// A server that provides access to the Griffin+ log service.
	/// </summary>
	public class LogServiceServer : IDisposable
	{
		/// <summary>
		/// Maximum time shutting down communication channels may take (in ms).
		/// </summary>
		private const int ShutdownTimeout = 5000;

		private readonly LinkedList<LogServiceServerChannel> mChannels                   = new LinkedList<LogServiceServerChannel>();
		private readonly object                              mSync                       = new object();
		private          LogServiceServerStatus              mStatus                     = LogServiceServerStatus.Stopped;
		private          TimeSpan                            mChannelInactivityTimeout   = TimeSpan.FromMinutes(5);
		private          ManualResetEventSlim                mServerThreadStartedUpEvent = null;
		private          ManualResetEventSlim                mAsyncAcceptCompletedEvent  = null;
		private          ManualResetEventSlim                mShutdownServerThreadEvent  = null;
		private          Thread                              mServerThread               = null;
		private readonly IPEndPoint                          mListenEndpoint;
		private          Socket                              mListenerSocket;
		private          int                                 mBacklog;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceServer"/> class.
		/// </summary>
		/// <param name="ipAddress">The IP address the server should listen to.</param>
		/// <param name="port">The TCP port the server should listen to.</param>
		public LogServiceServer(IPAddress ipAddress, int port)
		{
			mListenEndpoint = new IPEndPoint(ipAddress, port);
		}

		/// <summary>
		/// Disposes the server.
		/// </summary>
		public void Dispose()
		{
			Stop();
		}

		/// <summary>
		/// Gets the settings of the server.
		/// </summary>
		public LogServiceServerSettings Settings { get; } = new LogServiceServerSettings();

		#region Server Status

		/// <summary>
		/// Gets the status of the server.
		/// </summary>
		public LogServiceServerStatus Status
		{
			get
			{
				lock (mSync)
				{
					return mStatus;
				}
			}
		}

		/// <summary>
		/// Synchronously waits for the server to reach the specified status.
		/// </summary>
		/// <param name="status">Status to wait for.</param>
		/// <param name="timeout">Timeout (in ms, may be <see cref="Timeout.Infinite"/> to wait infinitely).</param>
		/// <exception cref="TimeoutException">Waiting to reach the specified status timed out.</exception>
		public void WaitForStatus(LogServiceServerStatus status, int timeout)
		{
			int countdown = timeout >= 0 ? timeout : int.MaxValue;
			const int step = 50;
			while (countdown > 0)
			{
				if (Status == status) return;
				Thread.Sleep(50);
				countdown -= step;
			}

			throw new TimeoutException($"Waiting to reach status '{status}' timed out.");
		}

		/// <summary>
		/// Synchronously waits for the server to reach the specified status.
		/// </summary>
		/// <param name="status">Status to wait for.</param>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		public void WaitForStatus(LogServiceServerStatus status, CancellationToken cancellationToken)
		{
			const int step = 50;
			while (true)
			{
				if (Status == status) return;
				cancellationToken.ThrowIfCancellationRequested();
				Thread.Sleep(step);
			}
		}

		/// <summary>
		/// Asynchronously waits for the server to reach the specified status.
		/// </summary>
		/// <param name="status">Status to wait for.</param>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		public async Task WaitForStatusAsync(LogServiceServerStatus status, CancellationToken cancellationToken)
		{
			const int step = 50;
			while (true)
			{
				if (Status == status) return;
				await Task.Delay(step, cancellationToken).ConfigureAwait(false);
			}
		}

		#endregion

		#region Unit Test Properties

		/// <summary>
		/// [[ For Tests Only]]
		/// Gets or sets a value indicating whether the server delays starting up to give unit tests
		/// a chance to check intermediate states.
		/// </summary>
		internal TimeSpan TestMode_StartupDelay { get; set; } = TimeSpan.Zero;

		/// <summary>
		/// [[ For Tests Only]]
		/// Gets or sets a value indicating whether the server delays shutting down to give unit tests
		/// a chance to check intermediate states.
		/// </summary>
		internal TimeSpan TestMode_ShutdownDelay { get; set; } = TimeSpan.Zero;

		/// <summary>
		/// [[ For Tests Only]]
		/// Gets or sets the delay to induce before starting to accept the first connection on the socket.
		/// </summary>
		internal TimeSpan TestMode_PreAcceptDelay { get; set; } = TimeSpan.Zero;

		/// <summary>
		/// [[ For Tests Only]]
		/// Gets or sets a value indicating whether the server sends received data back to allow integration tests
		/// to test streaming data over the network.
		/// </summary>
		internal bool TestMode_EchoReceivedData { get; set; } = false;

		/// <summary>
		/// [[ For Tests Only]]
		/// Gets or sets a value indicating whether the server discards received data to allow benchmarks to test
		/// the pure writing performance.
		/// </summary>
		internal bool TestMode_DiscardReceivedData { get; set; } = false;

		#endregion

		#region Starting the Server

		/// <summary>
		/// Starts the server synchronously.
		/// </summary>
		/// <param name="backlog">The maximum length of the pending connections queue.</param>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		/// <exception cref="OperationCanceledException">The operation was canceled.</exception>
		public void Start(int backlog = 50, CancellationToken cancellationToken = default)
		{
			// abort, if the cancellation token is signaled
			cancellationToken.ThrowIfCancellationRequested();

			ManualResetEventSlim serverThreadStartedUpEvent = null;

			lock (mSync)
			{
				// abort, if the server is already running
				if (mStatus == LogServiceServerStatus.Running)
					return;

				// abort, if the server is still shutting down
				if (mStatus == LogServiceServerStatus.Stopping)
					throw new InvalidOperationException("The server is still shutting down.");

				// abort, if the server is in an error state
				if (mStatus == LogServiceServerStatus.Error)
					throw new InvalidOperationException("The server is in an error state, debug the condition!");

				// the server should be in 'stopped' state now, so we can start it
				Debug.Assert(mStatus == LogServiceServerStatus.Stopped);

				// signal that the server is starting up
				mStatus = LogServiceServerStatus.Starting;
				mBacklog = backlog;

				// start the server thread for accepting and monitoring connections
				try
				{
					Debug.Assert(mServerThreadStartedUpEvent == null);
					Debug.Assert(mAsyncAcceptCompletedEvent == null);
					Debug.Assert(mShutdownServerThreadEvent == null);
					Debug.Assert(mServerThread == null);

					mServerThreadStartedUpEvent = new ManualResetEventSlim();
					mAsyncAcceptCompletedEvent = new ManualResetEventSlim();
					mShutdownServerThreadEvent = new ManualResetEventSlim();

					mServerThread = new Thread(ServerThreadProc) { Name = "Log Service Server Thread" };
					mServerThread.Start();
				}
				catch (Exception ex)
				{
					mStatus = LogServiceServerStatus.Error;
					mServerThread = null;

					if (mServerThreadStartedUpEvent != null)
					{
						mServerThreadStartedUpEvent.Dispose();
						mServerThreadStartedUpEvent = null;
					}

					if (mAsyncAcceptCompletedEvent != null)
					{
						mAsyncAcceptCompletedEvent.Dispose();
						mAsyncAcceptCompletedEvent = null;
					}

					if (mShutdownServerThreadEvent != null)
					{
						mShutdownServerThreadEvent.Dispose();
						mShutdownServerThreadEvent = null;
					}

					Debug.Fail("Starting log service thread failed unexpectedly.", ex.ToString());
					throw;
				}

				serverThreadStartedUpEvent = mServerThreadStartedUpEvent;
			}

			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				serverThreadStartedUpEvent.Wait(cancellationToken);
			}
			catch (ObjectDisposedException)
			{
				// the server thread has shut down and cleaned up the event meanwhile
				// => it must have started up before, so it is ok to ignore this condition
			}
		}

		/// <summary>
		/// Starts the server asynchronously.
		/// </summary>
		/// <param name="backlog">The maximum length of the pending connections queue.</param>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		/// <exception cref="OperationCanceledException">The operation was canceled.</exception>
		public Task StartAsync(int backlog = 50, CancellationToken cancellationToken = default)
		{
			return Task.Run(() => Start(backlog, cancellationToken), cancellationToken);
		}

		#endregion

		#region Stopping the Server

		/// <summary>
		/// Stops the server synchronously
		/// (blocks until the server has shut down or the specified cancellation token is signaled).
		/// </summary>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		/// <exception cref="OperationCanceledException">The operation was canceled.</exception>
		public void Stop(CancellationToken cancellationToken = default)
		{
			// abort, if the cancellation token is signaled
			cancellationToken.ThrowIfCancellationRequested();

			Thread serverThread;

			lock (mSync)
			{
				serverThread = mServerThread;

				// abort, if the server is in an error state
				if (mStatus == LogServiceServerStatus.Error)
					throw new InvalidOperationException("The server is in an error state, debug the condition!");

				if (mStatus == LogServiceServerStatus.Starting || mStatus == LogServiceServerStatus.Running)
				{
					mStatus = LogServiceServerStatus.Stopping;
					mShutdownServerThreadEvent.Set();
				}
			}

			// wait for the server thread to complete
			if (serverThread != null)
			{
				while (true)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (serverThread.Join(100))
						break;
				}
			}
		}

		/// <summary>
		/// Stops the server asynchronously.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
		public Task StopAsync(CancellationToken cancellationToken = default)
		{
			return Task.Run(() => Stop(cancellationToken), cancellationToken);
		}

		#endregion

		#region Channel Handling

		/// <summary>
		/// Gets established channels.
		/// </summary>
		public LogServiceServerChannel[] Channels
		{
			get
			{
				lock (mChannels)
				{
					return mChannels.ToArray();
				}
			}
		}

		/// <summary>
		/// Gets or sets the time of inactivity before a client channel is shut down (default: 5 minutes).
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">The specified time must be greater than <see cref="TimeSpan.Zero"/>.</exception>
		public TimeSpan ChannelInactivityTimeout
		{
			get
			{
				lock (mSync)
				{
					return mChannelInactivityTimeout;
				}
			}

			set
			{
				if (value <= TimeSpan.Zero)
					throw new ArgumentOutOfRangeException(nameof(value), "The time of inactivity must be greater than TimeSpan.Zero.");

				lock (mSync)
				{
					mChannelInactivityTimeout = value;
				}
			}
		}

		/// <summary>
		/// Is called by a <see cref="LogServiceServerChannel"/> after it has received some data.
		/// This moves the channel to the end of the list of monitored channels as only the first channel in the list is
		/// checked for inactivity.
		/// </summary>
		/// <param name="channel">The monitored server channel.</param>
		internal void ProcessChannelHasReceivedData(LogServiceServerChannel channel)
		{
			lock (mChannels)
			{
				if (channel.Node.List != null)
				{
					Debug.Assert(mChannels == channel.Node.List);
					mChannels.Remove(channel.Node);
					mChannels.AddLast(channel.Node);
				}
			}
		}

		/// <summary>
		/// Is called by a <see cref="LogServiceServerChannel"/> after it has completed shutting down.
		/// This removes the channel from the list of monitored channels.
		/// </summary>
		/// <param name="channel">The monitored server channel.</param>
		internal void ProcessChannelHasCompletedShuttingDown(LogServiceServerChannel channel)
		{
			lock (mChannels)
			{
				if (channel.Node.List != null)
				{
					Debug.Assert(mChannels == channel.Node.List);
					mChannels.Remove(channel.Node);
				}
			}
		}

		/// <summary>
		/// Shuts channels down that seem to be inactive.
		/// This method is periodically called by the server thread
		/// </summary>
		private void ShutdownInactiveChannels()
		{
			Debug.Assert(Monitor.IsEntered(mSync));

			List<LogServiceChannel> inactiveChannels = null;

			// check registered channels
			// (the first node in the list contains the registration of the channel that is inactive the longest time)
			lock (mChannels)
			{
				while (mChannels.First != null)
				{
					var channel = mChannels.First.Value;

					// abort, if the channel does not exceed the configured time of inactivity
					var timeSinceLastActivity = TimeSpan.FromMilliseconds(Environment.TickCount - channel.LastReceiveTickCount);
					if (timeSinceLastActivity < mChannelInactivityTimeout)
						break;

					// channel exceeds the configured time of inactivity
					// => schedule shutting it down
					if (inactiveChannels == null) inactiveChannels = new List<LogServiceChannel>();
					inactiveChannels.Add(channel);
					mChannels.RemoveFirst();
				}
			}

			// shut down all channels that have been identified to have exceeded their deadline
			if (inactiveChannels != null)
			{
				foreach (var channel in inactiveChannels)
				{
					try
					{
						channel.InitiateShutdown();
					}
					catch (Exception ex)
					{
						Debug.Fail("InitiateShutdown() failed unexpectedly.", ex.ToString());
					}
				}
			}
		}

		#endregion

		#region Server Thread

		/// <summary>
		/// Entry point of the server thread.
		/// </summary>
		/// <param name="obj">The <see cref="CancellationToken"/> that is signaled when the server is shutting down.</param>
		private void ServerThreadProc(object obj)
		{
			// delay spinning up some time (gives unit tests a chance to check intermediate states and start timeouts)
			if (TestMode_StartupDelay > TimeSpan.Zero && mShutdownServerThreadEvent.Wait(TestMode_StartupDelay))
				return;

			try
			{
				// signal that the server is running now
				// (must not monitor the passed cancellation token as this would break setting the status when shutting down!)
				lock (mSync)
				{
					if (mStatus == LogServiceServerStatus.Starting)
					{
						// the server is starting up, signal that the task has spun up
						// => start the listener and signal that the server is ready to accept connections
						mListenerSocket = new Socket(mListenEndpoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
						mListenerSocket.Bind(mListenEndpoint);
						mListenerSocket.Listen(mBacklog);
						mStatus = LogServiceServerStatus.Running;
						mServerThreadStartedUpEvent.Set();
					}
					else if (mStatus == LogServiceServerStatus.Stopping)
					{
						// the server has been stopped before it has started accepting connections
						mStatus = LogServiceServerStatus.Stopped;
						mServerThreadStartedUpEvent.Set();
						return;
					}
				}

				// wait some time before starting to accept a connection (for unit tests only)
				if (TestMode_PreAcceptDelay != TimeSpan.Zero)
					mShutdownServerThreadEvent.Wait(TestMode_PreAcceptDelay);

				// accept connections
				var e = new SocketAsyncEventArgs();
				e.Completed += ProcessAcceptCompleted;
				try
				{
					// abort, if the shutdown token is signaled
					mAsyncAcceptCompletedEvent.Set();
					if (!mShutdownServerThreadEvent.IsSet)
					{
						// accept a connection asynchronously
						// (this is usually called only once, the handler will continue accepting)
						var listenerSocket = mListenerSocket;
						mAsyncAcceptCompletedEvent.Reset();
						if (!mListenerSocket.AcceptAsync(e))
							ThreadPool.QueueUserWorkItem(_ => ProcessAcceptCompleted(listenerSocket, e));

						// accepting takes place asynchronously
						// => just wait for the server to shut down and monitor channels for inactivity meanwhile
						while (true)
						{
							lock (mSync)
							{
								ShutdownInactiveChannels();
							}

							if (mShutdownServerThreadEvent.Wait(250))
								break;
						}
					}
				}
				catch (SocketException ex)
				{
					Debug.Fail("Accepting client connection failed unexpectedly.", ex.ToString());
				}

				// close listener socket and wait for the accept call to complete
				mListenerSocket.Close();
				mListenerSocket = null;
				mAsyncAcceptCompletedEvent.Wait(CancellationToken.None);

				// initiate shutting down all server channels
				LogServiceServerChannel[] channels;
				lock (mChannels) channels = mChannels.ToArray();
				foreach (var channel in channels) channel.InitiateShutdown();

				// wait for the channels to finish shutting down
				int timeoutCountdown = ShutdownTimeout;
				const int step = 50;
				while (true)
				{
					lock (mChannels)
					{
						if (mChannels.Count == 0)
							break;
					}

					if (timeoutCountdown < 0)
					{
						Debug.Fail("Waiting for communication channels to shut down timed out unexpectedly.");
						break;
					}

					Thread.Sleep(step);
					timeoutCountdown -= step;
				}
			}
			finally
			{
				// delay shutting down some time (gives unit tests a chance to check intermediate states and stop timeouts)
				if (TestMode_ShutdownDelay > TimeSpan.Zero)
					Thread.Sleep(TestMode_ShutdownDelay);

				// signal that the server has shut down
				lock (mSync)
				{
					// the shutdown was successful, if all channels have been shut down properly
					lock (mChannels)
					{
						mStatus = mChannels.Count > 0
							          ? LogServiceServerStatus.Error
							          : LogServiceServerStatus.Stopped;
					}

					Debug.Assert(mStatus == LogServiceServerStatus.Stopped);

					mServerThreadStartedUpEvent.Dispose();
					mAsyncAcceptCompletedEvent.Dispose();
					mShutdownServerThreadEvent.Dispose();
					mServerThreadStartedUpEvent = null;
					mAsyncAcceptCompletedEvent = null;
					mShutdownServerThreadEvent = null;
					mServerThread = null;
				}
			}
		}

		/// <summary>
		/// Processes the asynchronous completion of a socket accept operation.
		/// </summary>
		/// <param name="sender">The listener socket.</param>
		/// <param name="e">Socket event arguments associated with the accept operation.</param>
		private void ProcessAcceptCompleted(object sender, SocketAsyncEventArgs e)
		{
			var listenerSocket = (Socket)sender;

			// stop accepting, if an error occurred
			if (e.SocketError != SocketError.Success)
			{
				switch (e.SocketError)
				{
					// error 995: Operation Aborted
					// (the overlapped operation was aborted due to the closure of the socket)
					// => the server is shutting down
					case SocketError.OperationAborted:
						Debug.Assert(mShutdownServerThreadEvent.IsSet);
						mAsyncAcceptCompletedEvent.Set();
						return;

					// error: 10038: Not Socket
					// (a socket operation was attempted on a non-socket)
					// => the listener socket was disposed before the actual accept operation was started
					// => the server is shutting down
					case SocketError.NotSocket:
						Debug.Assert(mShutdownServerThreadEvent.IsSet);
						mAsyncAcceptCompletedEvent.Set();
						return;
				}

				Debug.Fail($"Accepting stopped due to socket error '{e.SocketError}' (should be treated for better error handling!).");
			}

			if (e.SocketError == SocketError.Success)
			{
				// the connection was established successfully
				// => create a channel wrapping the accepted socket
				var socket = e.AcceptSocket;
				LogServiceServerChannel channel = null;
				try
				{
					channel = new LogServiceServerChannel(this, socket);

					lock (mChannels)
					{
						mChannels.AddLast(channel.Node);
					}

					socket = null;  // socket is now managed by the channel
					channel = null; // channel is now kept in the channel list
				}
				catch (Exception ex)
				{
					Debug.Fail("Creating a log service failed unexpectedly.", ex.ToString());
				}
				finally
				{
					socket?.Dispose();
					channel?.Dispose();
				}
			}

			try
			{
				// accept a connection asynchronously
				e.AcceptSocket = null;
				if (!listenerSocket.AcceptAsync(e))
					ThreadPool.QueueUserWorkItem(obj => ProcessAcceptCompleted(listenerSocket, e));
			}
			catch (ObjectDisposedException)
			{
				// the listener socket has been disposed meanwhile
				// => the server is shutting down
				mAsyncAcceptCompletedEvent.Set();
			}
			catch (SocketException ex)
			{
				e.SocketError = ex.SocketErrorCode;
				ProcessAcceptCompleted(listenerSocket, e);
			}
		}

		#endregion
	}

}
