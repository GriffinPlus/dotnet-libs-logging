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

using GriffinPlus.Lib.Threading;

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

		private readonly LinkedList<LogServiceServerChannel> mChannels                    = new LinkedList<LogServiceServerChannel>();
		private readonly AsyncManualResetEvent               mAcceptingTaskStartedUpEvent = new AsyncManualResetEvent();
		private readonly AsyncLock                           mLock                        = new AsyncLock();
		private          LogServiceServerStatus              mStatus                      = LogServiceServerStatus.Stopped;
		private          CancellationTokenSource             mShutdownServerTokenSource   = null;
		private          TimeSpan                            mChannelInactivityTimeout    = TimeSpan.FromMinutes(5);
		private          Task                                mAcceptingTask               = Task.CompletedTask;
		private          Task                                mMonitoringTask              = Task.CompletedTask;
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
		/// Disposes the client.
		/// </summary>
		public void Dispose()
		{
			Stop(CancellationToken.None);
		}

		/// <summary>
		/// Gets the status of the server.
		/// </summary>
		public LogServiceServerStatus Status
		{
			get
			{
				using (mLock.Lock())
				{
					return mStatus;
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
				using (mLock.Lock())
				{
					return mChannelInactivityTimeout;
				}
			}

			set
			{
				if (value <= TimeSpan.Zero)
					throw new ArgumentOutOfRangeException(nameof(value), "The time of inactivity must be greater than TimeSpan.Zero.");

				using (mLock.Lock())
				{
					mChannelInactivityTimeout = value;
				}
			}
		}

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
		/// Gets the settings of the server.
		/// </summary>
		public LogServiceServerSettings Settings { get; } = new LogServiceServerSettings();

		/// <summary>
		/// Gets the accepting task.
		/// </summary>
		internal Task AcceptingTask
		{
			get
			{
				using (mLock.Lock())
				{
					return mAcceptingTask;
				}
			}
		}

		/// <summary>
		/// Gets the monitoring task.
		/// </summary>
		private Task MonitoringTask
		{
			get
			{
				using (mLock.Lock())
				{
					return mMonitoringTask;
				}
			}
		}

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
		/// Gets or sets the delay to induce before executing an 'accept' call on the socket.
		/// </summary>
		internal TimeSpan TestMode_PreAcceptDelay { get; set; } = TimeSpan.Zero;

		/// <summary>
		/// [[ For Tests Only]]
		/// Gets or sets a value indicating whether the server sends received data back to allow integration tests
		/// to test streaming data over the network.
		/// </summary>
		internal bool TestMode_EchoReceivedData { get; set; } = false;

		#endregion

		#region Starting the Server

		/// <summary>
		/// Starts the server synchronously.
		/// </summary>
		/// <param name="backlog">The maximum length of the pending connections queue.</param>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		public void Start(int backlog = 50, CancellationToken cancellationToken = default)
		{
			StartAsync(backlog, cancellationToken).WaitAndUnwrapException(CancellationToken.None);
		}

		/// <summary>
		/// Starts the server asynchronously.
		/// </summary>
		/// <param name="backlog">The maximum length of the pending connections queue.</param>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		public async Task StartAsync(int backlog = 50, CancellationToken cancellationToken = default)
		{
			using (await mLock.LockAsync(cancellationToken).ConfigureAwait(false))
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
				mAcceptingTaskStartedUpEvent.Reset();
				mStatus = LogServiceServerStatus.Starting;
				mBacklog = backlog;

				try
				{
					// create a new cancellation token source that is signaled to shut the monitoring task and the
					// accepting task down at the end and create copy of cancellation token for the tasks to avoid
					// directly referencing the token source.
					Debug.Assert(mShutdownServerTokenSource == null);
					mShutdownServerTokenSource = new CancellationTokenSource();
					var shutdownToken = mShutdownServerTokenSource.Token;

					// start the monitoring task
					try
					{
						mMonitoringTask = Task.Run(
							() => MonitorChannelsAsync(shutdownToken),
							CancellationToken.None); // must not be cancellable as the task switches the status!
					}
					catch (Exception)
					{
						mStatus = LogServiceServerStatus.Error;
						throw;
					}

					// start the accepting task
					try
					{
						mAcceptingTask = Task.Run(
							() => AcceptConnectionsAsync(shutdownToken),
							CancellationToken.None); // must not be cancellable as the task switches the status!
					}
					catch (Exception)
					{
						mStatus = LogServiceServerStatus.Error;
						throw;
					}
				}
				catch (Exception)
				{
					if (mShutdownServerTokenSource != null)
					{
						mShutdownServerTokenSource.Cancel();
						mShutdownServerTokenSource.Dispose();
						mShutdownServerTokenSource = null;
					}

					throw;
				}
			}

			// wait for the task to start up
			await mAcceptingTaskStartedUpEvent.WaitAsync(cancellationToken).ConfigureAwait(false);
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
			StopAsync(cancellationToken).WaitAndUnwrapException(CancellationToken.None);
		}

		/// <summary>
		/// Stops the server asynchronously.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
		public async Task StopAsync(CancellationToken cancellationToken = default)
		{
			Task acceptingTask;
			Task monitoringTask;

			using (await mLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				acceptingTask = mAcceptingTask;
				monitoringTask = mMonitoringTask;

				// abort, if the server is in an error state
				if (mStatus == LogServiceServerStatus.Error)
					throw new InvalidOperationException("The server is in an error state, debug the condition!");

				if (mStatus == LogServiceServerStatus.Starting || mStatus == LogServiceServerStatus.Running)
				{
					mStatus = LogServiceServerStatus.Stopping;

					if (mShutdownServerTokenSource != null)
					{
						mShutdownServerTokenSource.Cancel();
						mShutdownServerTokenSource.Dispose();
						mShutdownServerTokenSource = null;
					}
				}
			}

			// wait for the accepting task and the monitoring task to complete
			var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
			await Task.WhenAny(
					cancellationTask,
					Task.WhenAll(acceptingTask, monitoringTask))
				.ConfigureAwait(false);
			if (acceptingTask.IsCompleted && monitoringTask.IsCompleted) return;
			await cancellationTask.ConfigureAwait(false); // throws OperationCanceledException if the operation was cancelled
		}

		#endregion

		#region Waiting for Status

		/// <summary>
		/// Synchronously waits for the server to reach the specified status.
		/// </summary>
		/// <param name="status">Status to wait for.</param>
		/// <param name="timeout">Timeout (in ms, may be <see cref="Timeout.Infinite"/> to wait infinitely).</param>
		/// <exception cref="TimeoutException">Waiting to reach the specified status timed out.</exception>
		public void WaitForStatus(LogServiceServerStatus status, int timeout)
		{
			int startTicks = Environment.TickCount;
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

		#region Monitoring Connections

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
		/// Checks periodically whether channels have exceeded their inactivity timeout and shuts them down.
		/// </summary>
		/// <param name="shutdownToken">Token that is signaled when the server is shutting down.</param>
		private async Task MonitorChannelsAsync(CancellationToken shutdownToken)
		{
			var channelsToShutDown = new List<LogServiceChannel>();

			while (!shutdownToken.IsCancellationRequested)
			{
				try
				{
					TimeSpan nextRunDelay;

					using (await mLock.LockAsync(shutdownToken).ConfigureAwait(false))
					{
						// check registered channels
						// (the first node in the list contains the registration of the channel that is inactive the longest time)
						nextRunDelay = mChannelInactivityTimeout;
						lock (mChannels)
						{
							while (mChannels.First != null)
							{
								var channel = mChannels.First.Value;

								// abort, if the channel does not exceed the configured time of inactivity
								var timeSinceLastActivity = TimeSpan.FromMilliseconds(Environment.TickCount - channel.LastReceiveTickCount);
								if (timeSinceLastActivity < mChannelInactivityTimeout)
								{
									nextRunDelay = mChannelInactivityTimeout - timeSinceLastActivity;
									break;
								}

								// channel exceeds the configured time of inactivity
								// => schedule shutting it down
								channelsToShutDown.Add(channel);
								mChannels.RemoveFirst();
							}
						}
					}

					// shut down all channels that have been identified to have exceeded their deadline
					foreach (var channel in channelsToShutDown)
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

					channelsToShutDown.Clear();

					// wait for the next run
					await Task
						.Delay(nextRunDelay, shutdownToken)
						.ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}

		#endregion

		#region Accepting Connections

		/// <summary>
		/// Accepts connections and creates a <see cref="LogServiceServerChannel"/> for each connection to start processing.
		/// This method runs until <see cref="mShutdownServerTokenSource"/> is signaled.
		/// Then all created channels are shut down and the method returns.
		/// </summary>
		/// <param name="shutdownToken">Token that is signaled when the server is shutting down.</param>
		private async Task AcceptConnectionsAsync(CancellationToken shutdownToken)
		{
			// NOTE:
			// This method should not throw any exceptions as exceptions are not processed by the caller

			// delay spinning up some time (gives unit tests a chance to check intermediate states and start timeouts)
			if (TestMode_StartupDelay > TimeSpan.Zero)
				await Delay(TestMode_StartupDelay).ConfigureAwait(false);

			try
			{
				// signal that the server is running now
				// (must not monitor the passed cancellation token as this would break setting the status when shutting down!)
				using (await mLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
				{
					if (mStatus == LogServiceServerStatus.Starting)
					{
						// the server is starting up, signal that the task has spun up
						// => start the listener and signal that the server is ready to accept connections
						mListenerSocket = new Socket(mListenEndpoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
						mListenerSocket.Bind(mListenEndpoint);
						mListenerSocket.Listen(mBacklog);
						mStatus = LogServiceServerStatus.Running;
						mAcceptingTaskStartedUpEvent.Set();
					}
					else if (mStatus == LogServiceServerStatus.Stopping)
					{
						// the server has been stopped before it has started accepting connections
						mStatus = LogServiceServerStatus.Stopped;
						mAcceptingTaskStartedUpEvent.Set();
						return;
					}
				}

				// accept connections
				var acceptCompleted = new AsyncAutoResetEvent(false);
				var e = new SocketAsyncEventArgs();
				e.Completed += (sender, args) => acceptCompleted.Set();
				while (true)
				{
					Debug.Assert(mListenerSocket != null, nameof(mListenerSocket) + " != null");

					Socket socket = null;
					LogServiceServerChannel channel = null;
					try
					{
						// wait some time before starting to accept a connection (for unit tests only)
						if (TestMode_PreAcceptDelay != TimeSpan.Zero)
							await Delay(TestMode_PreAcceptDelay).ConfigureAwait(false);

						// abort, if the shutdown token is signaled
						if (shutdownToken.IsCancellationRequested)
						{
							if (mListenerSocket != null)
							{
								mListenerSocket.Close();
								mListenerSocket = null;
							}

							break;
						}

						// accept a connection asynchronously
						e.AcceptSocket = null;
						if (!mListenerSocket.AcceptAsync(e))
							acceptCompleted.Set();

						// wait for a new connection to be accepted or the server shutting down
						try
						{
							await acceptCompleted
								.WaitAsync(shutdownToken)
								.ConfigureAwait(false);
						}
						catch (OperationCanceledException)
						{
							// cancellation token was signaled
							// => close listener socket and wait for the accept call to complete
							mListenerSocket.Close();
							mListenerSocket = null;
							await acceptCompleted.WaitAsync(CancellationToken.None).ConfigureAwait(false);
							if (e.SocketError != SocketError.Success) break;
							socket = e.AcceptSocket;
						}

						if (e.SocketError != SocketError.Success)
							throw new SocketException((int)e.SocketError);

						// accepting a connection succeeded
						socket = e.AcceptSocket;

						// the connection was established successfully
						// => create a channel wrapping the socket
						try
						{
							channel = new LogServiceServerChannel(this, socket, shutdownToken);

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
					}
					catch (SocketException ex)
					{
						Debug.Fail("Accepting client connection failed unexpectedly.", ex.ToString());
						continue;
					}
					finally
					{
						// dispose the socket/channel, if the channel could not be started successfully
						// (otherwise there is a dangling connection)
						channel?.Dispose();
						socket?.Dispose();
					}
				}

				// connected channels should already be shutting down due to the server shutdown token
				// => wait for the channels to finish shutting down
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

					await Task.Delay(step, CancellationToken.None).ConfigureAwait(false);
					timeoutCountdown -= step;
				}
			}
			finally
			{
				// delay shutting down some time (gives unit tests a chance to check intermediate states and stop timeouts)
				if (TestMode_ShutdownDelay > TimeSpan.Zero)
					await Delay(TestMode_ShutdownDelay).ConfigureAwait(false);

				// signal that the server has shut down
				// (must not monitor the passed cancellation token as this would break setting the status when shutting down!)
				using (await mLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
				{
					// the shutdown was successful, if all channels have been shut down properly
					lock (mChannels)
					{
						mStatus = mChannels.Count > 0
							          ? LogServiceServerStatus.Error
							          : LogServiceServerStatus.Stopped;
					}

					Debug.Assert(mStatus == LogServiceServerStatus.Stopped);
				}
			}
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Sleeps the specified time.
		/// </summary>
		/// <param name="time">Time to sleep (in ms).</param>
		private static async Task Delay(int time)
		{
			const int step = 50;
			while (time > 0)
			{
				await Task.Delay(step).ConfigureAwait(false);
				time -= step;
			}
		}

		/// <summary>
		/// Sleeps the specified time.
		/// </summary>
		/// <param name="time">Time to sleep.</param>
		private static async Task Delay(TimeSpan time)
		{
			var step = TimeSpan.FromMilliseconds(50);
			while (time > TimeSpan.Zero)
			{
				await Task.Delay(step).ConfigureAwait(false);
				time -= step;
			}
		}

		#endregion
	}

}
