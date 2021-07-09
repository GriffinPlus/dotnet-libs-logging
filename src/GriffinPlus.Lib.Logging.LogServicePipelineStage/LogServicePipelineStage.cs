///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using GriffinPlus.Lib.Logging.LogService;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A log message processing pipeline stage that forwards log message to the Griffin+ log service.
	/// </summary>
	public class LogServicePipelineStage : ProcessingPipelineStage<LogServicePipelineStage>
	{
		private          LogServiceClientChannel mChannel;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServicePipelineStage"/> class connecting to the Griffin+ log service
		/// on 'tcp://127.0.0.1:6500'.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		public LogServicePipelineStage(string name) : this(name, IPAddress.Loopback, 6500)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServicePipelineStage"/> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <param name="ipAddress">IP address of the log service.</param>
		/// <param name="port">Port of the log service.</param>
		public LogServicePipelineStage(string name, IPAddress ipAddress, int port) : this(name, new IPEndPoint(ipAddress, port))
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServicePipelineStage"/> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <param name="serviceEndpoint">IP endpoint of the log service.</param>
		public LogServicePipelineStage(string name, IPEndPoint serviceEndpoint) : base(name)
		{
			ServiceEndpoint = serviceEndpoint;
		}

		#region Service Endpoint

		/// <summary>
		/// Gets the IP endpoint of the log service.
		/// </summary>
		public IPEndPoint ServiceEndpoint { get; }

		#endregion

		#region Configuring Startup Behavior

		private TimeSpan mConnectOnStartupTimeout = TimeSpan.FromMilliseconds(1000);

		/// <summary>
		/// Gets or sets the time to wait for a connection to be established at startup (default: 1 second).
		/// Set to <see cref="TimeSpan.Zero"/> to disable connecting at startup and schedule connecting instead.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">The timeout was negative.</exception>
		public TimeSpan ConnectOnStartupTimeout
		{
			get
			{
				lock (Sync)
				{
					return mConnectOnStartupTimeout;
				}
			}

			set
			{
				if (value < TimeSpan.Zero)
					throw new ArgumentOutOfRangeException(nameof(value), "The timeout must be positive.");

				lock (Sync)
				{
					mConnectOnStartupTimeout = value;
				}
			}
		}

		#endregion

		#region Overrides

		/// <summary>
		/// Initializes the pipeline stage when the stage is attached to the logging subsystem.
		/// </summary>
		protected override void OnInitialize()
		{
			Debug.Assert(Monitor.IsEntered(Sync));

			if (mConnectOnStartupTimeout > TimeSpan.Zero)
			{
				// connecting on startup is enabled
				// => try to establish the connection to the log service
				try
				{
					using (var cts = new CancellationTokenSource(mConnectOnStartupTimeout))
					{
						Connect(cts.Token);
					}
				}
				catch (OperationCanceledException)
				{
					// establishing connection to the log service timed out
					// => start task that tries to connect periodically, if necessary
					if (mAutoReconnect) StartAutoReconnectTask();
				}
				catch (EstablishingLogServiceConnectionFailedException)
				{
					// establishing connection to the log service failed
					// => start task that tries to connect periodically, if necessary
					if (mAutoReconnect) StartAutoReconnectTask();
				}
			}
			else
			{
				// connecting on startup is disabled
				// => schedule connecting to the log service
				ThreadPool.QueueUserWorkItem(
					obj =>
					{
						try
						{
							((LogServicePipelineStage)obj).Connect();
						}
						catch
						{
							// the Connect() method will start the reconnect task,
							// if connecting fails...
						}
					},
					this);
			}
		}

		/// <summary>
		/// Shuts the pipeline stage down when the stage is detached from the logging system.
		/// </summary>
		protected override void OnShutdown()
		{
			Debug.Assert(Monitor.IsEntered(Sync));

			// cancel reconnect task, but do not wait for the task to complete
			// (could cause a dead lock in rare cases)
			if (mAutoReconnectTaskCancellationTokenSource != null)
			{
				mAutoReconnectTaskCancellationTokenSource.Cancel();
				mAutoReconnectTaskCancellationTokenSource.Dispose();
				mAutoReconnectTaskCancellationTokenSource = null;
			}

			// disconnect from the log service
			if (mChannel != null)
			{
				mChannel.ShutdownCompleted -= OnChannelCompletedShuttingDown;
				mChannel.Dispose();
				mChannel = null;
			}
		}

		/// <summary>
		/// Processes a log message synchronously.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <returns>
		/// Always <c>true</c>, so the message is passed to following stages.
		/// </returns>
		protected override bool ProcessSync(LocalLogMessage message)
		{
			Debug.Assert(Monitor.IsEntered(Sync));

			if (mChannel != null)
			{
				try
				{
					// enqueue die message for sending through the channel
					// (for performance reasons the method returns before the message has reached the service)
					mChannel.Send(message);
				}
				catch (Exception)
				{
					// just swallow...
					// => messages may get lost, but we must not block here to avoid blocking user-code...
				}
			}

			return true;
		}

		#endregion

		#region Controlling Storing Messages in the Log Service

		private bool mStoringMessagesPersistently = true;

		/// <summary>
		/// Gets or sets a value indicating whether log messages are persistently stored in the log service.
		/// </summary>
		public bool StoringMessagesPersistently
		{
			get
			{
				lock (Sync)
				{
					return mStoringMessagesPersistently;
				}
			}

			set
			{
				lock (Sync)
				{
					mStoringMessagesPersistently = value;
					if (mChannel != null) mChannel.StoringMessagesPersistently = value;
				}
			}
		}

		#endregion

		#region Handling the Connection to the Log Service

		/// <summary>
		/// Gets a value indicating whether the connection to the log service is operational.
		/// </summary>
		public bool IsOperational
		{
			get
			{
				lock (Sync)
				{
					if (mChannel == null) return false;
					return mChannel.Status == LogServiceChannelStatus.Operational;
				}
			}
		}

		/// <summary>
		/// Connects to the log service.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		/// <exception cref="InvalidOperationException">The pipeline stage is not initialized.</exception>
		/// <exception cref="EstablishingLogServiceConnectionFailedException">
		/// Establishing a connection to the log service failed, see inner exception for details.
		/// </exception>
		public void Connect(CancellationToken cancellationToken = default)
		{
			lock (Sync)
			{
				// abort, if the pipeline stage is not initialized
				EnsureAttachedToLoggingSubsystem();

				// abort, if the connection to the log service is already established
				// and clean up, if the connection is broken
				if (mChannel != null)
				{
					// abort, if the channel is already operational
					if (mChannel.Status == LogServiceChannelStatus.Operational)
						return;

					// the channel is not operational any more, release it
					Disconnect();
				}
			}

			// the connection to the log service is not operational
			// => try to establish a new connection to the log service
			LogServiceClientChannel channel = null;
			try
			{
				try
				{
					channel = LogServiceClientChannel.ConnectToServer(ServiceEndpoint.Address, ServiceEndpoint.Port, true, cancellationToken);
				}
				catch
				{
					// connecting failed
					// => retry after some time, if auto-reconnect is enabled
					lock (Sync)
					{
						if (mAutoReconnect && (IsInitialized || IsInitializing))
							StartAutoReconnectTask();
					}

					throw;
				}

				lock (Sync)
				{
					// shut the channel down, if the stage has already shut down
					EnsureAttachedToLoggingSubsystem();

					if (mChannel != null)
					{
						if (mChannel.Status == LogServiceChannelStatus.Operational)
						{
							// an operational connection has been established meanwhile
							// => shut down the freshly connected channel to avoid loosing messages in the other channel
							return;
						}

						// the existing channel is not operational
						// => shut it down
						Disconnect();
					}

					// use the new connection
					mChannel = channel;
					channel = null;
					mChannel.ShutdownCompleted += OnChannelCompletedShuttingDown;
					if (mChannel.Status == LogServiceChannelStatus.ShutdownCompleted)
						OnChannelCompletedShuttingDown(mChannel, EventArgs.Empty);
				}
			}
			finally
			{
				channel?.Dispose();
			}
		}

		/// <summary>
		/// Connects to the log service.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		/// <exception cref="InvalidOperationException">The pipeline stage is not initialized.</exception>
		/// <exception cref="EstablishingLogServiceConnectionFailedException">
		/// Establishing a connection to the log service failed, see inner exception for details.
		/// </exception>
		public async Task ConnectAsync(CancellationToken cancellationToken = default)
		{
			lock (Sync)
			{
				// abort, if the pipeline stage is not initialized
				EnsureAttachedToLoggingSubsystem();

				// abort, if the connection to the log service is already established
				// and clean up, if the connection is broken
				if (mChannel != null)
				{
					// abort, if the channel is already operational
					if (mChannel.Status == LogServiceChannelStatus.Operational)
						return;

					// the channel is not operational any more, release it
					Disconnect();
				}
			}

			// the connection to the log service is not operational
			// => try to establish a new connection to the log service
			LogServiceClientChannel channel = null;
			try
			{
				try
				{
					channel = await LogServiceClientChannel
						          .ConnectToServerAsync(ServiceEndpoint.Address, ServiceEndpoint.Port, true, cancellationToken)
						          .ConfigureAwait(false);
				}
				catch
				{
					// connecting failed
					// => retry after some time, if auto-reconnect is enabled
					lock (Sync)
					{
						if (mAutoReconnect && (IsInitialized || IsInitializing))
							StartAutoReconnectTask();
					}

					throw;
				}

				lock (Sync)
				{
					// shut the channel down, if the stage has already shut down
					EnsureAttachedToLoggingSubsystem();

					if (mChannel != null)
					{
						if (mChannel.Status == LogServiceChannelStatus.Operational)
						{
							// an operational connection has already been established
							// => shut down the freshly connected channel to avoid loosing messages in the other channel
							return;
						}

						// the existing channel is not operational
						// => shut it down
						Disconnect();
					}

					// use the new connection
					mChannel = channel;
					channel = null;
					mChannel.ShutdownCompleted += OnChannelCompletedShuttingDown;
					if (mChannel.Status == LogServiceChannelStatus.ShutdownCompleted)
						OnChannelCompletedShuttingDown(mChannel, EventArgs.Empty);
				}
			}
			finally
			{
				channel?.Dispose();
			}
		}

		/// <summary>
		/// Disconnects from the log service.
		/// </summary>
		/// <exception cref="InvalidOperationException">The pipeline stage is not initialized.</exception>
		public void Disconnect()
		{
			lock (Sync)
			{
				EnsureAttachedToLoggingSubsystem();

				if (mChannel != null)
				{
					mChannel.ShutdownCompleted -= OnChannelCompletedShuttingDown;
					mChannel.Dispose();
					mChannel = null;
				}
			}
		}

		/// <summary>
		/// Is called by the log service channel when it completes shutting down.
		/// </summary>
		/// <param name="sender">The <see cref="LogServiceClientChannel"/> that has completed shutting down.</param>
		/// <param name="e">Event arguments (not used).</param>
		private void OnChannelCompletedShuttingDown(object sender, EventArgs e)
		{
			var channel = (LogServiceClientChannel)sender;

			lock (Sync)
			{
				if (mChannel == channel)
				{
					// release old channel
					Disconnect();

					// establish a new connection to the log service, if automatic reconnecting is enabled
					// (let a worker thread do that to avoid blocking the pipeline stage lock too long)
					if (mAutoReconnect)
					{
						ThreadPool.QueueUserWorkItem(
							obj =>
							{
								try
								{
									((LogServicePipelineStage)obj).Connect();
								}
								catch
								{
									// the Connect() method will start the reconnect task,
									// if connecting fails...
								}
							},
							this);
					}
				}
			}
		}

		#endregion

		#region Reconnecting Automatically

		private bool                    mAutoReconnect                            = true;
		private TimeSpan                mAutoReconnectRetryInterval               = TimeSpan.FromSeconds(15);
		private CancellationTokenSource mAutoReconnectTaskCancellationTokenSource = null;

		/// <summary>
		/// Gets or sets a value indicating whether the connection is re-established after breaking down
		/// (most probably due to the local log service shutting down or restarting).
		/// </summary>
		public bool AutoReconnect
		{
			get
			{
				lock (Sync)
				{
					return mAutoReconnect;
				}
			}

			set
			{
				lock (Sync)
				{
					if (mAutoReconnect == value) return;
					mAutoReconnect = value;
					if (mInitialized && !IsOperational)
					{
						StartAutoReconnectTask();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the interval between two attempts to re-establish the connection to the log service.
		/// Requires <see cref="AutoReconnect"/> to be set to <c>true</c>.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">The interval was negative.</exception>
		public TimeSpan AutoReconnectRetryInterval
		{
			get
			{
				lock (Sync)
				{
					return mAutoReconnectRetryInterval;
				}
			}

			set
			{
				if (value < TimeSpan.Zero)
					throw new ArgumentOutOfRangeException(nameof(value), "The interval must be positive.");

				lock (Sync)
				{
					mAutoReconnectRetryInterval = value;
				}
			}
		}

		/// <summary>
		/// Schedules a task to connect to the log service after the <see cref="mAutoReconnectRetryInterval"/>.
		/// </summary>
		private void StartAutoReconnectTask()
		{
			lock (Sync)
			{
				// cancel the last connect operation
				// NOTE: Cannot wait for the task to complete, because this could hang up execution.
				//       The task code is properly synchronized to avoid race conditions
				if (mAutoReconnectTaskCancellationTokenSource != null)
				{
					mAutoReconnectTaskCancellationTokenSource.Cancel();
					mAutoReconnectTaskCancellationTokenSource.Dispose();
				}

				// create a new cancellation token source
				// (it is necessary to pull the token out of mAutoReconnectTaskCancellationTokenSource to ensure it is not overwritten by replacing mAutoReconnectTaskCancellationTokenSource)
				mAutoReconnectTaskCancellationTokenSource = new CancellationTokenSource();
				var cts = mAutoReconnectTaskCancellationTokenSource.Token;

				// schedule a new task to retry to connect to the log service
				Task.Delay(mAutoReconnectRetryInterval, cts)
					.ContinueWith(
						_ =>
						{
							lock (Sync)
							{
								cts.ThrowIfCancellationRequested();

								if ((mInitialized || IsInitializing) && mAutoReconnect && !IsOperational)
								{
									// shut the connection down to clean up resources
									Disconnect();

									// try to connect to the log service
									try
									{
										Connect(cts);
									}
									catch (OperationCanceledException)
									{
										// connecting was aborted
										// => just return...
									}
									catch
									{
										// connecting to the log service failed
										// => schedule a new task to retry after the specified time
										StartAutoReconnectTask();
									}
								}
							}
						},
						cts,
						TaskContinuationOptions.OnlyOnRanToCompletion,
						TaskScheduler.Current);
			}
		}

		#endregion

		#region Commands

		/// <summary>
		/// Sends a command telling the log viewer to clear its view.
		/// </summary>
		public void ClearLogViewer()
		{
			lock (Sync)
			{
				try
				{
					mChannel?.SendClearLogViewerCommand();
				}
				catch
				{
					// swallow...
				}
			}
		}

		/// <summary>
		/// Sends a command telling the log service to save a snapshot of the current log.
		/// </summary>
		public void SaveSnapshot()
		{
			lock (Sync)
			{
				try
				{
					mChannel?.SendSaveSnapshotCommand();
				}
				catch
				{
					// swallow...
				}
			}
		}

		#endregion
	}

}
