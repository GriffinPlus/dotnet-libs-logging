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
using System.Runtime.CompilerServices;
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

		private readonly LinkedList<LogServiceServerChannel> mChannels                  = new LinkedList<LogServiceServerChannel>();
		private readonly AsyncLock                           mLock                      = new AsyncLock();
		private          LogServiceServerStatus              mStatus                    = LogServiceServerStatus.Stopped;
		private          CancellationTokenSource             mShutdownServerTokenSource = null;
		private readonly IPEndPoint                          mListenEndpoint            = null;
		private          Socket                              mListenerSocket            = null;
		private readonly object                              mSettingsSync              = new object();
		private          TimeSpan                            mChannelInactivityTimeout  = TimeSpan.FromMinutes(5);

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
				lock (mSettingsSync)
				{
					return mChannelInactivityTimeout;
				}
			}

			set
			{
				if (value <= TimeSpan.Zero)
					throw new ArgumentOutOfRangeException(nameof(value), "The time of inactivity must be greater than TimeSpan.Zero.");

				lock (mSettingsSync)
				{
					mChannelInactivityTimeout = value;
					mTriggerAcceptingAndMonitoringThreadEvent.Set();
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

		#region Unit Test Properties

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

				try
				{
					// create a new cancellation token source that is signaled to shut the monitoring task and the
					// accepting task down at the end and create copy of cancellation token for the tasks to avoid
					// directly referencing the token source.
					Debug.Assert(mShutdownServerTokenSource == null);
					mShutdownServerTokenSource = new CancellationTokenSource();

					// start the listener
					mListenerSocket = new Socket(mListenEndpoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
					mListenerSocket.Bind(mListenEndpoint);
					mListenerSocket.Listen(backlog);

					// start the accepting/monitoring thread
					mAcceptingAndMonitoringThread = new Thread(AcceptingAndMonitoringThreadProc) { Name = "Accepting and Monitoring Log Service Channels" };
					mAcceptingAndMonitoringThread.Start();

					// the server is running now
					mStatus = LogServiceServerStatus.Running;
				}
				catch (Exception)
				{
					// an unexpected exception occurred
					mStatus = LogServiceServerStatus.Error;

					if (mShutdownServerTokenSource != null)
					{
						mShutdownServerTokenSource.Cancel();
						mShutdownServerTokenSource.Dispose();
						mShutdownServerTokenSource = null;
					}

					if (mAcceptingAndMonitoringThread != null)
					{
						mAcceptingAndMonitoringThread.Join();
						mAcceptingAndMonitoringThread = null;
					}

					if (mListenerSocket != null)
					{
						mListenerSocket.Dispose();
						mListenerSocket = null;
					}

					throw;
				}
			}
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
			using (await mLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				// abort, if the server is already stopped or shutting down
				if (mStatus == LogServiceServerStatus.Stopped || mStatus == LogServiceServerStatus.Stopping)
					return;

				// abort, if the server is in an error state
				if (mStatus == LogServiceServerStatus.Error)
					throw new InvalidOperationException("The server is in an error state, debug the condition!");

				Debug.Assert(mStatus == LogServiceServerStatus.Running);

				mStatus = LogServiceServerStatus.Stopping;

				// close the listener socket
				if (mListenerSocket != null)
				{
					mListenerSocket.Dispose();
					mListenerSocket = null;
				}

				// shut the accepting/monitoring thread down
				mShutdownServerTokenSource.Cancel();
				mTriggerAcceptingAndMonitoringThreadEvent.Set();
				await Task.Run(
						() =>
						{
							while (true)
							{
								cancellationToken.ThrowIfCancellationRequested();
								if (!mAcceptingAndMonitoringThread.IsAlive) break;
								Thread.Sleep(50);
							}
						},
						CancellationToken.None)
					.ConfigureAwait(false);
				mAcceptingAndMonitoringThread = null;

				// dispose the shutdown token
				mShutdownServerTokenSource.Dispose();
				mShutdownServerTokenSource = null;

				mStatus = LogServiceServerStatus.Stopped;
			}
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
			do
			{
				if (Status == status) return;
				Thread.Sleep(50);
			} while (timeout < 0 || Environment.TickCount - startTicks < timeout);

			throw new TimeoutException($"Waiting to reach status '{status}' timed out.");
		}

		/// <summary>
		/// Synchronously waits for the server to reach the specified status.
		/// </summary>
		/// <param name="status">Status to wait for.</param>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		public void WaitForStatus(LogServiceServerStatus status, CancellationToken cancellationToken)
		{
			while (true)
			{
				if (Status == status) return;
				cancellationToken.ThrowIfCancellationRequested();
				Thread.Sleep(50);
			}
		}

		/// <summary>
		/// Asynchronously waits for the server to reach the specified status.
		/// </summary>
		/// <param name="status">Status to wait for.</param>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		public async Task WaitForStatusAsync(LogServiceServerStatus status, CancellationToken cancellationToken)
		{
			while (true)
			{
				if (Status == status) return;
				await Task.Delay(50, cancellationToken).ConfigureAwait(false);
			}
		}

		#endregion


		#region Accepting and Monitoring Connections

		private readonly ManualResetEventSlim mTriggerAcceptingAndMonitoringThreadEvent = new ManualResetEventSlim();
		private          Thread               mAcceptingAndMonitoringThread;

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
		/// Entry point of the thread responsible for accepting and monitoring connections.
		/// </summary>
		private void AcceptingAndMonitoringThreadProc()
		{
			var shutdownToken = mShutdownServerTokenSource.Token;

			// determine whether accepting is enabled right from the start (default) or whether it is delayed (test mode)
			bool isAcceptEnabled = TestMode_PreAcceptDelay == TimeSpan.Zero;
			int startupTicks = Environment.TickCount;

			var channelsToShutDown = new List<LogServiceChannel>();

			// prepare socket event args for accepting
			bool isAcceptPending = false;
			var accepted = new StrongBox<bool>();
			var acceptEventArgs = new SocketAsyncEventArgs();
			acceptEventArgs.Completed += (sender, args) =>
			{
				Volatile.Write(ref accepted.Value, true);
				mTriggerAcceptingAndMonitoringThreadEvent.Set();
			};

			int nextRunTicks = Environment.TickCount;
			while (true)
			{
				int nextRunDelay = Math.Max(nextRunTicks - Environment.TickCount, 0);
				mTriggerAcceptingAndMonitoringThreadEvent.Wait(nextRunDelay);
				mTriggerAcceptingAndMonitoringThreadEvent.Reset();

				// capture settings needed in the run
				TimeSpan channelInactivityTimeout;
				lock (mSettingsSync)
				{
					channelInactivityTimeout = mChannelInactivityTimeout;
				}

				// assume the next run is done to check for channel inactivity
				// (the value is reduced as necessary below)
				nextRunTicks = (int)(Environment.TickCount + channelInactivityTimeout.TotalMilliseconds);

				// abort, if the server is shutting down
				if (shutdownToken.IsCancellationRequested)
				{
					// connected channels should already be shutting down due to the server shutdown token
					// => wait for the channels to finish shutting down
					int startTickCount = Environment.TickCount;
					while (true)
					{
						// close socket that has been accepted, but not bound to a channel, if necessary
						if (isAcceptPending)
						{
							if (Volatile.Read(ref accepted.Value))
							{
								if (acceptEventArgs.SocketError == SocketError.Success)
								{
									acceptEventArgs.AcceptSocket.Close();
									acceptEventArgs.AcceptSocket = null;
								}

								Volatile.Write(ref accepted.Value, false);
								isAcceptPending = false;
							}
						}

						// abort, if all channels have shut down and a pending accept call has completed
						lock (mChannels)
						{
							if (mChannels.Count == 0 && !isAcceptPending)
								break;
						}

						// abort, if the time to shut down is exceeded
						if (Environment.TickCount - startTickCount > ShutdownTimeout)
						{
							Debug.Fail("Waiting for communication channels to shut down timed out unexpectedly.");
							break;
						}

						// try again after some time
						Thread.Sleep(50);
					}

					break;
				}

				// ----------------------------------------------------------------------------------------------------
				// enable accepting after some time (for tests only)
				// ----------------------------------------------------------------------------------------------------
				if (!isAcceptEnabled)
				{
					if (Environment.TickCount - startupTicks > TestMode_PreAcceptDelay.TotalMilliseconds)
					{
						isAcceptEnabled = true;
					}
					else
					{
						// pre-accept delay is not elapsed
						// => continue waiting
						nextRunTicks = (int)Math.Min(
							nextRunTicks,
							Environment.TickCount + TestMode_PreAcceptDelay.TotalMilliseconds - startupTicks);

						mTriggerAcceptingAndMonitoringThreadEvent.Set();
					}
				}

				// ----------------------------------------------------------------------------------------------------
				// accept a new connection, if possible
				// ----------------------------------------------------------------------------------------------------
				if (isAcceptEnabled)
				{
					Socket socket = null;
					LogServiceServerChannel channel = null;
					try
					{
						// accept a connection asynchronously
						if (!isAcceptPending)
						{
							acceptEventArgs.AcceptSocket = null;
							isAcceptPending = mListenerSocket.AcceptAsync(acceptEventArgs);
							if (!isAcceptPending) Volatile.Write(ref accepted.Value, true);
						}

						if (Volatile.Read(ref accepted.Value))
						{
							try
							{
								if (acceptEventArgs.SocketError != SocketError.Success)
									throw new SocketException((int)acceptEventArgs.SocketError);

								// accepting a connection succeeded
								socket = acceptEventArgs.AcceptSocket;

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
							finally
							{
								Volatile.Write(ref accepted.Value, false);
								isAcceptPending = false;
								mTriggerAcceptingAndMonitoringThreadEvent.Set();
							}
						}
					}
					catch (SocketException)
					{
						continue;
					}
					catch (ObjectDisposedException)
					{
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

				// ----------------------------------------------------------------------------------------------------
				// check whether channels have become inactive and shut them down
				// ----------------------------------------------------------------------------------------------------
				{
					// check registered channels
					// (the first node in the list contains the registration of the channel that is inactive the longest time)
					lock (mChannels)
					{
						while (mChannels.First != null)
						{
							var channel = mChannels.First.Value;

							// abort, if the channel does not exceed the configured time of inactivity
							var timeSinceLastActivity = TimeSpan.FromMilliseconds(Environment.TickCount - channel.LastReceiveTickCount);
							if (timeSinceLastActivity < channelInactivityTimeout)
							{
								nextRunTicks = (int)Math.Min(
									nextRunTicks,
									Environment.TickCount + channelInactivityTimeout.TotalMilliseconds - timeSinceLastActivity.TotalMilliseconds);

								break;
							}

							// channel exceeds the configured time of inactivity
							// => schedule shutting it down
							channelsToShutDown.Add(channel);
							mChannels.RemoveFirst();
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
				}
			}
		}

		#endregion
	}

}
