///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// The client side of a log service connection.
	/// </summary>
	public sealed class LogServiceClientChannel : LogServiceChannel
	{
		private TimeSpan                mHeartbeatInterval          = TimeSpan.FromMinutes(1);
		private Task                    mHeartbeatTask              = Task.CompletedTask;
		private CancellationTokenSource mCancelHeartbeatTokenSource = null;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceClientChannel"/> class.
		/// </summary>
		/// <param name="socket">A TCP socket representing the connection between the client and the server of the log service.</param>
		/// <param name="start">
		/// If <c>true</c> the channel immediately starts reading from the socket.
		/// If <c>false</c> the channel does not start reading the socket (call <see cref="Run"/> to make up for it).
		/// </param>
		internal LogServiceClientChannel(Socket socket, bool start) :
			base(socket, CancellationToken.None)
		{
			// start channel, if it has not been shut down immediately
			if (Status == LogServiceChannelStatus.Created && start)
				Start();
		}

		/// <summary>
		/// Starts the channel, if it has been created with <c>start</c> set to <c>false</c>.
		/// </summary>
		public void Run()
		{
			Start();
		}

		/// <summary>
		/// Is called when the channel has been started successfully.
		/// The receiver is not started, yet.
		/// The executing thread holds the channel lock (<see cref="LogServiceChannel.Sync"/>) when called.
		/// </summary>
		protected override void OnStarted()
		{
			// send a greeting to the server
			SendGreeting();

			// start the heartbeat task to ensure that the server does not shut the channel down due to inactivity checking
			if (mHeartbeatInterval > TimeSpan.Zero)
			{
				// create a new cancellation token source that is signaled to shut the heartbeat task down at the end
				// and create copy of cancellation token for the task to avoid directly referencing the token source.
				Debug.Assert(mCancelHeartbeatTokenSource == null);
				mCancelHeartbeatTokenSource = new CancellationTokenSource();
				var shutdownToken = mCancelHeartbeatTokenSource.Token;

				try
				{
					Debug.Assert(mHeartbeatTask.IsCompleted);
					mHeartbeatTask = Task.Run(
						() => SendHeartbeatsAsync(shutdownToken),
						CancellationToken.None);
				}
				catch (Exception)
				{
					Debug.Fail("Starting the heartbeat task failed unexpectedly.");
					mCancelHeartbeatTokenSource.Dispose();
					mCancelHeartbeatTokenSource = null;
					throw;
				}
			}
		}

		#region Greeting

		/// <summary>
		/// Sends the greeting to the client.
		/// </summary>
		private void SendGreeting()
		{
			// determine the version of the library
			var versionAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
			string version = versionAttribute != null ? versionAttribute.InformationalVersion : "<unknown>";

			// send greeting and version information
			Send("HELLO Griffin+ .NET Log Service Client");
			Send($"INFO Log Service Library Version: {version}");
		}

		#endregion

		#region Connecting to Server

		/// <summary>
		/// Connects to the log service server at the specified address and port.
		/// </summary>
		/// <param name="address">IP Address of the server to connect to.</param>
		/// <param name="port">Port number.</param>
		/// <param name="start">
		/// If <c>true</c> the channel immediately starts reading from the socket.
		/// If <c>false</c> the channel does not start reading the socket (call <see cref="Run"/> to make up for it).
		/// </param>
		/// <param name="cancellationToken">Cancellation token that can be signaled to abort the operation.</param>
		/// <returns>The log service client channel.</returns>
		public static LogServiceClientChannel ConnectToServer(
			IPAddress         address,
			int               port,
			bool              start             = true,
			CancellationToken cancellationToken = default)
		{
			Socket socket = null;
			try
			{
				// create a new socket to use 
				socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				// prepare event arguments for connecting asynchronously
				using (var completedEvent = new ManualResetEventSlim())
				{
					var e = new SocketAsyncEventArgs { RemoteEndPoint = new IPEndPoint(address, port) };

					// ReSharper disable once AccessToDisposedClosure
					e.Completed += (sender, args) => completedEvent.Set();

					// connect to the server
					if (!socket.ConnectAsync(e))
						completedEvent.Set();

					// wait for the operation to complete
					completedEvent.Wait(cancellationToken);

					// handle socket error, if necessary
					if (e.SocketError != SocketError.Success)
						throw new SocketException((int)e.SocketError);

					// the channel connected successfully
					var channel = new LogServiceClientChannel(e.ConnectSocket, start);
					socket = null;
					return channel;
				}
			}
			finally
			{
				socket?.Dispose();
			}
		}

		/// <summary>
		/// Connects to the log service server at the specified address and port.
		/// </summary>
		/// <param name="address">IP Address of the server to connect to.</param>
		/// <param name="port">Port number.</param>
		/// <param name="start">
		/// If <c>true</c> the channel immediately starts reading from the socket.
		/// If <c>false</c> the channel does not start reading the socket (call <see cref="Run"/> to make up for it).
		/// </param>
		/// <param name="cancellationToken">Cancellation token that can be signaled to abort the operation.</param>
		/// <returns>The log service client channel.</returns>
		public static async Task<LogServiceClientChannel> ConnectToServerAsync(
			IPAddress         address,
			int               port,
			bool              start             = true,
			CancellationToken cancellationToken = default)
		{
			Socket socket = null;
			try
			{
				// create a new socket to use 
				socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				// callback to invoke when connecting completes
				var connectTaskCompletionSource = new TaskCompletionSource<LogServiceClientChannel>();

				void ConnectCompleted(object sender, SocketAsyncEventArgs args)
				{
					if (args.SocketError != SocketError.Success)
					{
						connectTaskCompletionSource.SetException(new SocketException((int)args.SocketError));
						return;
					}

					try
					{
						var channel = new LogServiceClientChannel(args.ConnectSocket, start);
						connectTaskCompletionSource.SetResult(channel);
					}
					catch (Exception ex)
					{
						// creating the channel failed
						// => close the connected socket to avoid having it dangle around
						args.ConnectSocket.Close();
						connectTaskCompletionSource.SetException(ex);
					}
				}

				// prepare event arguments for connecting asynchronously
				var e = new SocketAsyncEventArgs { RemoteEndPoint = new IPEndPoint(address, port) };
				e.Completed += ConnectCompleted;

				// connect to the server
				if (socket.ConnectAsync(e))
				{
					// operation is pending, the callback will be invoked on completion
					// => wait for connecting to complete

					var cancellationTask = Task.Delay(-1, cancellationToken);

					await Task
						.WhenAny(connectTaskCompletionSource.Task, cancellationTask)
						.ConfigureAwait(false);

					if (connectTaskCompletionSource.Task.IsCompleted)
					{
						return await connectTaskCompletionSource
							       .Task
							       .ConfigureAwait(false);
					}

					// connecting has not completed => cancellation is pending
					Debug.Assert(cancellationToken.IsCancellationRequested);
					cancellationToken.ThrowIfCancellationRequested();
					return null; // should never occur
				}

				// operation completed synchronously (very unlikely)
				ConnectCompleted(socket, e);
				return await connectTaskCompletionSource.Task.ConfigureAwait(false);
			}
			catch
			{
				socket?.Dispose();
				throw;
			}
		}

		#endregion

		#region Heartbeat

		/// <summary>
		/// Gets or sets the time between two heartbeat commands sent to the server to ensure that the channel
		/// is still operational (use <see cref="TimeSpan.Zero"/> to disable the heartbeat).
		/// Default: 1 minute.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">The heartbeat interval must not be negative.</exception>
		public TimeSpan HeartbeatInterval
		{
			get
			{
				lock (Sync)
				{
					return mHeartbeatInterval;
				}
			}

			set
			{
				if (value < TimeSpan.Zero)
					throw new ArgumentOutOfRangeException(nameof(value), value, "The heartbeat interval must not be negative.");

				lock (Sync)
				{
					if (mHeartbeatInterval != value)
					{
						// stop heartbeat task, if necessary
						if (mCancelHeartbeatTokenSource != null)
						{
							mCancelHeartbeatTokenSource.Cancel();
							mCancelHeartbeatTokenSource.Dispose();
							mCancelHeartbeatTokenSource = null;
						}

						// set new heartbeat interval
						mHeartbeatInterval = value;

						// start heartbeat task, if necessary
						if (mHeartbeatInterval > TimeSpan.Zero)
						{
							// create a new cancellation token source that is signaled to shut the heartbeat task down at the end
							// and create copy of cancellation token for the task to avoid directly referencing the token source.
							Debug.Assert(mCancelHeartbeatTokenSource == null);
							mCancelHeartbeatTokenSource = new CancellationTokenSource();
							var shutdownToken = mCancelHeartbeatTokenSource.Token;

							try
							{
								mHeartbeatTask = Task.Run(
									() => SendHeartbeatsAsync(shutdownToken),
									CancellationToken.None);
							}
							catch (Exception)
							{
								Debug.Fail("Starting the heartbeat task failed unexpectedly.");
								mCancelHeartbeatTokenSource.Dispose();
								mCancelHeartbeatTokenSource = null;
								throw;
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Checks periodically whether sending a heartbeat command (HEARTBEAT) is due and send it, if necessary.
		/// </summary>
		/// <param name="stoppingToken">Token that is signaled to stop the heartbeat task.</param>
		private async Task SendHeartbeatsAsync(CancellationToken stoppingToken)
		{
			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ShutdownToken, stoppingToken))
			{
				while (!cts.IsCancellationRequested)
				{
					try
					{
						// determine whether the heartbeat is due
						var nextRunDelay = TimeSpan.Zero;
						lock (Sync)
						{
							var timeSinceLastSend = TimeSpan.FromMilliseconds(Environment.TickCount - LastSendTickCount);
							if (timeSinceLastSend < mHeartbeatInterval)
							{
								// sending a heartbeat is not due
								// => adjust the time to wait for the next check
								nextRunDelay = mHeartbeatInterval - timeSinceLastSend;
							}
						}

						// send heartbeat, if it is due
						if (nextRunDelay == TimeSpan.Zero)
						{
							try
							{
								// send heartbeat
								// (updates the timestamp of the last sending operation)
								Send("HEARTBEAT");
							}
							catch (LogServiceChannelNotConnectedException)
							{
								// the channel has shut down
								// => stop sending heartbeats
								break;
							}
							catch (LogServiceChannelQueueFullException)
							{
								// the channel's send queue is full
								// => the connection seems to be alive, but the remote peer hangs or does not receive as fast as we're sending
								// => skip sending heartbeat this time and try again later
							}
							catch (Exception ex)
							{
								Debug.Fail("Sending heartbeat failed unexpectedly.", ex.ToString());
							}

							// adjust time to wait before sending the next heartbeat
							nextRunDelay = mHeartbeatInterval;
						}

						// wait for the next run
						await Task.Delay(nextRunDelay, cts.Token).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						break;
					}
				}
			}
		}

		#endregion

		/// <summary>
		/// Is called when the channel has received a complete line.
		/// </summary>
		/// <param name="line">Line to process.</param>
		protected override void OnLineReceived(ReadOnlySpan<char> line)
		{
			// let the base class do its work
			base.OnLineReceived(line);
		}
	}

}
