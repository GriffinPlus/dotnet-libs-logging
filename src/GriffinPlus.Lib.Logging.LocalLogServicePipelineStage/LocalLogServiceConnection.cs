///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using GriffinPlus.Lib.Threading;

// ReSharper disable RedundantCaseLabel

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// An interface to the local log service for logging clients.
/// </summary>
sealed partial class LocalLogServiceConnection
{
	private const int PipeConnectTimeout            = 1000;  // ms
	private const int QueueBlockFetchRetryDelayTime = 20;    // ms
	private const int ConnectivityCheckInterval     = 10000; // ms

	private static readonly int                     sCurrentProcessId                               = Process.GetCurrentProcess().Id;
	private readonly        object                  mSync                                           = new();
	private readonly        string                  mSinkServerPipeName                             = null;
	private readonly        string                  mGlobalQueueName                                = null;
	private readonly        string                  mLocalQueueName                                 = null;
	private readonly        UnsafeSharedMemoryQueue mSharedMemoryQueue                              = new();
	private                 bool                    mInitialized                                    = false;
	private                 TimeSpan                mAutoReconnectRetryInterval                     = TimeSpan.FromSeconds(15);
	private                 AsyncAutoResetEvent     mTriggerConnectivityMonitorEvent                = null;
	private                 Task                    mConnectivityMonitorTask                        = null;
	private                 CancellationTokenSource mConnectivityMonitorTaskCancellationTokenSource = null;
	private                 bool                    mLosslessMode                                   = false;
	private                 int                     mLostMessageCount                               = 0;
	private                 int                     mLastSentLogWriterId                            = -1;
	private                 int                     mLastSentLogLevelId                             = -1;
	private                 int                     mPeakBufferCapacity                             = 0;
	private readonly        Queue<LogEntryBlock>    mPeakBufferQueue                                = new();
	private                 bool                    mInitializationInProgress                       = false;
	private                 bool                    mShutdownInProgress                             = false;
	private                 Process                 mServiceProcess                                 = null;
	private                 Task                    mSendSettingTask                                = null;
	private                 bool                    mWriteToLogFile                                 = true;
	private                 bool                    mEstablishingConnectionInProgress               = false;

	#region Construction

	/// <summary>
	/// Initializes the <see cref="LocalLogServiceConnection"/> class.
	/// </summary>
	static unsafe LocalLogServiceConnection()
	{
		Debug.Assert(sizeof(LogEntryBlock) == 496, "Log entry block size does not match the expected size.");
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="LocalLogServiceConnection"/> class.
	/// </summary>
	/// <param name="prefix">Prefix for kernel objects created along with the connection.</param>
	public LocalLogServiceConnection(string prefix)
	{
		if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("The prefix must not be null or empty.", nameof(prefix));
		mSinkServerPipeName = $"{prefix} Log Sink Server";
		mGlobalQueueName = $"Global\\{prefix} Log Message Queue - Source Process Id: {sCurrentProcessId}";
		mLocalQueueName = $"{prefix} Log Message Queue - Source Process Id: {sCurrentProcessId}";
	}

	#endregion

	#region Auto Reconnect

	/// <summary>
	/// Gets or sets the interval between two attempts to re-establish the connection to the local log service.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The property is set and the specified interval is negative.</exception>
	public TimeSpan AutoReconnectRetryInterval
	{
		get
		{
			lock (mSync)
			{
				return mAutoReconnectRetryInterval;
			}
		}

		set
		{
			if (value < TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(value), value, "The interval must be positive.");

			lock (mSync)
			{
				mAutoReconnectRetryInterval = value;
			}
		}
	}

	#endregion

	#region Lossless Mode

	/// <summary>
	/// Gets or sets a value indicating whether the lossless mode is enabled or disabled (default: <c>false</c>).
	/// Lossless mode ensures that messages and notifications are immediately passed to the local log service.
	/// If there is no space in the shared memory queue to the local log service, the calling thread blocks until
	/// there is enough space to enqueue the message/command/notification. The connection's peak buffering is disabled
	/// in this case.
	/// 
	/// BEWARE:
	/// If the local log service hangs, the lossless mode might hang up this process as well as the local log service
	/// does not free space in the queue. Enable this mode only if you know what you are doing. Usually it is sufficient
	/// to use the shared memory queue without lossless mode and let the connection buffer extreme peaks in-process.
	/// Theoretically this can lead to message loss, but only under a very high load that spams the shared memory queue
	/// and the connection's peak buffer.
	/// </summary>
	public bool LosslessMode
	{
		get
		{
			lock (mSync)
			{
				return mLosslessMode;
			}
		}

		set
		{
			lock (mSync)
			{
				mLosslessMode = value;
			}
		}
	}

	#endregion

	#region Peak Buffering

	/// <summary>
	/// Gets or sets the capacity of the queue buffering data blocks that would have been sent to the local
	/// log service, but could not, because the shared memory queue was full. This can happen in case of severe
	/// load peaks. Peak buffering is in effect, if <see cref="LosslessMode"/> is <c>false</c>. Set the capacity
	/// to <c>0</c> to disable peak buffering messages (notifications are always buffered to avoid getting out of sync).
	/// </summary>
	/// <remarks>
	/// The actual number of blocks in the peak buffer queue can get greater than this property as notifications
	/// are always buffered and long messages can occupy multiple blocks exceeding the limit.
	/// </remarks>
	public int PeakBufferCapacity
	{
		get
		{
			lock (mSync)
			{
				return mPeakBufferCapacity;
			}
		}

		set
		{
			lock (mSync)
			{
				if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "The peak buffer capacity must be positive.");
				mPeakBufferCapacity = value;
			}
		}
	}

	#endregion

	#region Persistence

	/// <summary>
	/// Gets or sets a value indicating whether log messages are persistently stored in the local log service.
	/// </summary>
	public bool WriteToLogFile
	{
		get
		{
			lock (mSync)
			{
				return mWriteToLogFile;
			}
		}

		set
		{
			SetFieldBackedServiceSetting(
					ref mWriteToLogFile,
					value,
					() => new Request
					{
						Command = Command.SetWritingToLogFile,
						SetWritingToLogFileCommand =
						{
							ProcessId = sCurrentProcessId,
							Enable = mWriteToLogFile ? 1 : 0
						}
					},
					(request, reply) =>
					{
						// process reply returned from the local log service
						// => evaluate the result code
						//    0 = operation failed
						//    1 = operation succeeded
						if (reply.Result == 0)
						{
							// enabling/disabling the setting has failed
							Debug.WriteLine(
								request.SetWritingToLogFileCommand.Enable != 0
									? "Enabling writing messages to the log file failed."
									: "Disabling writing messages to the log file failed.");

							return false;
						}

						// enabling/disabling the setting has succeeded
						return true;
					})
				.WaitWithoutException();
		}
	}

	#endregion

	#region Initialization / Shutdown

	/// <summary>
	/// Gets a value indicating whether the connection is initialized, i.e. the connection to the local log service is set up
	/// (does not necessarily mean that the connection is established and alive).
	/// </summary>
	public bool IsInitialized
	{
		get
		{
			lock (mSync)
			{
				return mInitialized;
			}
		}
	}

	/// <summary>
	/// Gets a value indicating whether the connection to the local log service is established.
	/// </summary>
	public bool IsEstablished
	{
		get
		{
			lock (mSync)
			{
				return mServiceProcess != null;
			}
		}
	}

	/// <summary>
	/// Initializes the connection to the local log service asynchronously.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token that can be signaled to abort the operation.</param>
	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		Debug.Assert(!Monitor.IsEntered(mSync));

		lock (mSync)
		{
			// abort if the initialization is already in progress
			if (mInitializationInProgress)
				throw new InvalidOperationException("Initialization is already in progress.");

			// abort if the shutdown is already in progress
			if (mShutdownInProgress)
				throw new InvalidOperationException("Shutdown is already in progress.");

			mInitializationInProgress = true;
		}

		try
		{
			// abort, if the connection is already up and running
			if (IsLogSinkAlive())
				return;

			// shut the connection down to clean up resources
			ShutdownConnection();

			// try to establish a new connection
			await EstablishConnectionAsync(cancellationToken).ConfigureAwait(false);

			// update the connection state
			lock (mSync)
			{
				Debug.Assert(!mInitialized);
				mInitialized = true;

				// start task that checks the connectivity and tries to reconnect periodically if necessary
				mConnectivityMonitorTaskCancellationTokenSource = new CancellationTokenSource();
				mTriggerConnectivityMonitorEvent = new AsyncAutoResetEvent(false);
				mConnectivityMonitorTask = RunConnectivityMonitor();
			}
		}
		finally
		{
			lock (mSync)
			{
				mInitializationInProgress = false;
			}
		}
	}

	/// <summary>
	/// Establishes the connection to the local log service.
	/// </summary>
	/// <returns>
	/// <param name="cancellationToken">Cancellation token that may be signaled to abort the operation.</param>
	/// <c>true</c> if connecting to the local log service succeeded;<br/>
	/// otherwise <c>false</c>.
	/// </returns>
	private async Task EstablishConnectionAsync(CancellationToken cancellationToken)
	{
		Debug.Assert(!Monitor.IsEntered(mSync));

		// ReSharper disable once LocalFunctionHidesMethod
		async Task ShutdownAsync(bool unregister)
		{
			if (unregister)
			{
				try
				{
					// create an 'unregister' request (for error conditions)
					var unregisterRequest = new Request
					{
						Command = Command.UnregisterLogSource,
						UnregisterLogSourceCommand = { ProcessId = sCurrentProcessId }
					};

					await SendRequestAsync(unregisterRequest, 0, CancellationToken.None).ConfigureAwait(false);
				}
				catch
				{
					/* swallow */
				}
			}

			lock (mSync)
			{
				mEstablishingConnectionInProgress = false;
				mSharedMemoryQueue.Close();
				mServiceProcess = null;
			}
		}

		Process serviceProcess = null;

		try
		{
			lock (mSync)
			{
				// abort, if the connection is already established
				if (mServiceProcess != null)
					throw new InvalidOperationException("The connection is already established, shut it down before connecting again.");

				// reset state
				mPeakBufferQueue.Clear();
				mLastSentLogWriterId = -1;
				mLastSentLogLevelId = -1;
				mLostMessageCount = 0;

				// remember that the establishing the connection is in progress
				mEstablishingConnectionInProgress = true;
			}

			Debug.WriteLine("Initializing connection to the local log service...");

			///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
			// register log source with the local log service
			///////////////////////////////////////////////////////////////////////////////////////////////////////////////////

			Debug.WriteLine("Registering log source with the local log service...");

			Request request;
			Reply reply;

			try
			{
				// prepare request
				request = new Request
				{
					Command = Command.RegisterLogSource,
					RegisterLogSourceCommand = { ProcessId = sCurrentProcessId }
				};

				// send request
				reply = await SendRequestAsync(request, PipeConnectTimeout, cancellationToken).ConfigureAwait(false);

				// evaluate the result code
				// 0 = registering log source failed
				// 1 = registering log source succeeded
				if (reply.Result != 0)
				{
					Debug.WriteLine("Registering log source with the local log service succeeded.");
				}
				else
				{
					Debug.WriteLine("Registering log source with the local log service failed.");
					await ShutdownAsync(false).ConfigureAwait(false);
					return;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Connecting to the local log service failed.");
				Debug.WriteLine(ex.ToString());
				await ShutdownAsync(false).ConfigureAwait(false);
				return;
			}

			///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
			// get the process id of the local log service
			///////////////////////////////////////////////////////////////////////////////////////////////////////////////////

			Debug.WriteLine("Getting process id of the local log service...");
			int serviceProcessId = await QueryProcessIdAsync(PipeConnectTimeout, cancellationToken).ConfigureAwait(false);
			Debug.WriteLine($"Getting process id of the local log service succeeded (process id: {serviceProcessId}).");

			///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
			// tell the local log service about the to 'write to log file' setting
			///////////////////////////////////////////////////////////////////////////////////////////////////////////////////

			Debug.WriteLine("Sending 'set writing to log file' request to the local log service...");
			bool writeToLogFile;
			lock (mSync) writeToLogFile = mWriteToLogFile;

			request = new Request
			{
				Command = Command.SetWritingToLogFile,
				SetWritingToLogFileCommand = { ProcessId = sCurrentProcessId, Enable = writeToLogFile ? 1 : 0 }
			};

			// send request
			reply = await SendRequestAsync(request, PipeConnectTimeout, cancellationToken).ConfigureAwait(false);

			// evaluate the result code
			// 0 = setting succeeded
			// 1 = setting failed
			if (reply.Result == 0)
			{
				Debug.WriteLine("The local log service failed to enable writing to the log file.");
				// proceed in case of an error...
			}

			///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
			// open the sink's process
			///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
			serviceProcess = Process.GetProcessById(serviceProcessId);

			///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
			// open the shared memory queue the sink has created for us
			///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
			try
			{
				Debug.WriteLine("Opening the shared memory queue to the local log service in the global namespace...");
				mSharedMemoryQueue.Open(mGlobalQueueName);
			}
			catch (Exception)
			{
				Debug.WriteLine("Opening the shared memory queue to the local log service in the global namespace failed.");
				Debug.WriteLine("Opening the shared memory queue to the local log service in the local namespace...");

				// try to open the shared memory queue in the local namespace
				// (local log service has been started without the privilege to create global objects)
				mSharedMemoryQueue.Open(mLocalQueueName);
			}

			Debug.WriteLine("Opening the shared memory queue to the local log service succeeded.");

			// tell the sink that a new session begins
			SendStartMarker();

			// tell the sink about the application name
			SendApplicationName();

			// tell the sink about log levels in use
			SendLogLevels();

			// tell the sink about source names in use
			SendSourceNames();

			// the log source has been initialized successfully
			Debug.WriteLine("Connection to the local log service has been established successfully.");
			lock (mSync)
			{
				mEstablishingConnectionInProgress = false;
				mServiceProcess = serviceProcess;
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Connecting to the local log service failed.");
			Debug.WriteLine(ex.ToString());
			serviceProcess?.Dispose();
			await ShutdownAsync(true).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Starts the connectivity monitoring task.
	/// </summary>
	private async Task RunConnectivityMonitor()
	{
		while (true)
		{
			try
			{
				TimeSpan timeout;
				CancellationToken connectivityMonitorCancellationToken;
				AsyncAutoResetEvent triggerConnectivityMonitorEvent;
				lock (mSync)
				{
					timeout = mServiceProcess != null ? TimeSpan.FromMilliseconds(ConnectivityCheckInterval) : mAutoReconnectRetryInterval;
					connectivityMonitorCancellationToken = mConnectivityMonitorTaskCancellationTokenSource.Token;
					triggerConnectivityMonitorEvent = mTriggerConnectivityMonitorEvent;
				}

				using (var cancellationTokenSource = new CancellationTokenSource())
				using (var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, connectivityMonitorCancellationToken))
				{
					Task triggerTask = triggerConnectivityMonitorEvent.WaitAsync(linkedCancellationTokenSource.Token);
					Task delayTask = Task.Delay(timeout, linkedCancellationTokenSource.Token);
					await Task.WhenAny(triggerTask, delayTask).ConfigureAwait(false); // does not throw!
					if (triggerTask.Status == TaskStatus.RanToCompletion || delayTask.Status == TaskStatus.RanToCompletion)
						cancellationTokenSource.Cancel();

					// wait for all tasks to complete
					try
					{
						await Task.WhenAll(triggerTask, delayTask).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						if (triggerTask.Status != TaskStatus.RanToCompletion && delayTask.Status != TaskStatus.RanToCompletion)
							return;
					}

					// try to to connect to the local log service if the service process seems to be dead
					if (!IsLogSinkAlive())
					{
						// shut the connection down to clean up resources
						ShutdownConnection();

						// try to connect to the local log service
						try
						{
							await EstablishConnectionAsync(connectivityMonitorCancellationToken).ConfigureAwait(false);
						}
						catch (OperationCanceledException)
						{
							return;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("The connectivity monitoring task threw an unhandled exception (catch and continue)...");
				Debug.WriteLine(ex.ToString());
			}
		}
	}

	/// <summary>
	/// Shuts the connection to the local log service down.
	/// </summary>
	public async Task ShutdownAsync()
	{
		Debug.Assert(!Monitor.IsEntered(mSync));

		Task connectivityMonitorTask;
		lock (mSync)
		{
			// abort if the shutdown is already in progress
			if (mShutdownInProgress)
				throw new InvalidOperationException("Shutdown is already in progress.");

			// abort if the initialization is already in progress
			if (mInitializationInProgress)
				throw new InvalidOperationException("Initialization is already in progress.");

			// cancel connectivity monitoring task
			mConnectivityMonitorTaskCancellationTokenSource?.Cancel();
			connectivityMonitorTask = mConnectivityMonitorTask;

			// signal that the shutdown is in progress
			mShutdownInProgress = true;
		}

		try
		{
			// wait for the connectivity monitoring task to complete
			if (connectivityMonitorTask != null)
				await connectivityMonitorTask.ConfigureAwait(false);

			// dispose the cancellation token source aborting the connectivity monitor task
			lock (mSync)
			{
				mConnectivityMonitorTaskCancellationTokenSource?.Dispose();
				mConnectivityMonitorTaskCancellationTokenSource = null;
				mConnectivityMonitorTask = null;
			}

			// shut the connection down
			ShutdownConnection();
		}
		finally
		{
			lock (mSync)
			{
				mShutdownInProgress = false; // shutdown completed
				mInitialized = false;        // connection is not initialized any more
			}
		}
	}

	/// <summary>
	/// Shuts the connection to the local log service down.
	/// </summary>
	private void ShutdownConnection()
	{
		lock (mSync)
		{
			if (mServiceProcess != null)
			{
				// unregister from the local log service
				try
				{
					// prepare request
					var request = new Request
					{
						Command = Command.UnregisterLogSource,
						UnregisterLogSourceCommand = { ProcessId = sCurrentProcessId }
					};

					// send request
					Reply reply = SendRequest(request, 0);

					// evaluate the result code
					// 0 = setting succeeded
					// 1 = setting failed
					Debug.WriteLine(
						reply.Result != 0
							? "Unregistering log source succeeded."
							: "Unregistering log source failed.");
				}
				catch (Exception ex)
				{
					Debug.WriteLine("Sending request to unregister the log source failed.");
					Debug.WriteLine(ex.ToString());
				}
			}

			// clear buffered messages / commands / notifications
			mPeakBufferQueue.Clear();
			mLastSentLogWriterId = -1;
			mLastSentLogLevelId = -1;
			mLostMessageCount = 0;

			// close the shared memory queue to the local log service
			mSharedMemoryQueue.Close();

			// close the handle of the local log service process
			if (mServiceProcess != null)
			{
				mServiceProcess.Dispose();
				mServiceProcess = null;
			}
		}
	}

	#endregion

	#region Checking Vital Sign of the Local Log Service

	/// <summary>
	/// Checks whether the local log service is alive.
	/// </summary>
	/// <returns>
	/// <c>true</c> if the local log service process is alive;<br/>
	/// otherwise <c>false</c>.
	/// </returns>
	public bool IsLogSinkAlive()
	{
		lock (mSync)
		{
			if (mServiceProcess == null)
			{
				// sink process not opened
				// => we cannot determine whether it is alive or not
				// => assume it is not alive
				return false;
			}

			return !mServiceProcess.WaitForExit(0);
		}
	}

	#endregion

	#region Sending Requests to the Local Log Service (via Named Pipe)

	/// <summary>
	/// Sends the specified request to the local log service.
	/// </summary>
	/// <param name="request">Request to send.</param>
	/// <param name="connectTimeout">Time to wait for the request to finish (in ms).</param>
	/// <returns>The received reply.</returns>
	/// <exception cref="LocalLogServiceCommunicationException">Communicating with the local log service failed.</exception>
	private Reply SendRequest(Request request, int connectTimeout)
	{
		try
		{
			using var pipe = new NamedPipeClientStream(".", mSinkServerPipeName, PipeDirection.InOut);
			using var reader = new MemoryReader(pipe);
			using var writer = new MemoryWriter(pipe);

			// connect to the pipe or wait until the pipe is available
			// (the local log service has a set of pipes to serve multiple clients, so waiting most likely
			// indicates that the service is not running)
			pipe.Connect(connectTimeout);
			pipe.ReadMode = PipeTransmissionMode.Message;

			// TODO: configure timeout for writing and reading struct
			//       (at the moment the pipe does not support setting ReadTimeout and WriteTimeout)

			// send the request to the local log service
			writer.WriteStruct(request);

			// wait for the reply
			return reader.ReadStruct<Reply>();
		}
		catch (Exception ex)
		{
			throw new LocalLogServiceCommunicationException("Sending request to the local log service failed.", ex);
		}
	}

	/// <summary>
	/// Sends the specified request to the local log service.
	/// </summary>
	/// <param name="request">Request to send.</param>
	/// <param name="connectTimeout">Time to wait for the request to finish (in ms).</param>
	/// <param name="cancellationToken">Cancellation token that can be signaled to abort the operation.</param>
	/// <returns>The received reply.</returns>
	/// <exception cref="LocalLogServiceCommunicationException">Communicating with the local log service failed.</exception>
	private async Task<Reply> SendRequestAsync(Request request, int connectTimeout, CancellationToken cancellationToken)
	{
		try
		{
			using var pipe = new NamedPipeClientStream(".", mSinkServerPipeName, PipeDirection.InOut);
			using var reader = new MemoryReader(pipe);
			using var writer = new MemoryWriter(pipe);

			// connect to the pipe or wait until the pipe is available
			// (the local log service has a set of pipes to serve multiple clients, so waiting most likely
			// indicates that the service is not running)
			await pipe.ConnectAsync(connectTimeout, cancellationToken).ConfigureAwait(false);
			pipe.ReadMode = PipeTransmissionMode.Message;

			// TODO: configure timeout for writing and reading struct
			//       (at the moment the pipe does not support setting ReadTimeout and WriteTimeout)

			// send the request to the local log service
			writer.WriteStruct(request);

			// wait for the reply
			return reader.ReadStruct<Reply>();
		}
		catch (Exception ex)
		{
			throw new LocalLogServiceCommunicationException("Sending request to the local log service failed.", ex);
		}
	}

	/// <summary>
	/// Gets the process id of the local log service.
	/// </summary>
	/// <param name="connectTimeout">Timeout (in ms).</param>
	/// <param name="cancellationToken">Cancellation token that can be signaled to abort the operation.</param>
	/// <returns>Process id of the local log service.</returns>
	/// <exception cref="LocalLogServiceCommunicationException">Communicating with the local log service failed.</exception>
	private async Task<int> QueryProcessIdAsync(int connectTimeout, CancellationToken cancellationToken)
	{
		var request = new Request { Command = Command.QueryProcessId };
		Reply reply = await SendRequestAsync(request, connectTimeout, cancellationToken).ConfigureAwait(false);
		return reply.QueryProcessIdCommand.ProcessId;
	}

	/// <summary>
	/// Updates the value of a field and sends a request to set the corresponding setting in the local log service.
	/// </summary>
	/// <typeparam name="TField">Type of the field to update</typeparam>
	/// <param name="field"></param>
	/// <param name="value"></param>
	/// <param name="requestFactory"></param>
	/// <param name="replyProcessing"></param>
	/// <returns>Task identifying the send operation.</returns>
	private Task SetFieldBackedServiceSetting<TField>(
		ref TField                 field,
		TField                     value,
		Func<Request>              requestFactory,
		Func<Request, Reply, bool> replyProcessing)
	{
		Debug.Assert(
			!Monitor.IsEntered(mSync),
			"The lock should not have been acquired here. Otherwise the logic below will not work!");

		while (true)
		{
			lock (mSync)
			{
				if (mSendSettingTask == null && !mEstablishingConnectionInProgress)
				{
					if (!Equals(field, value))
					{
						field = value;

						if (mServiceProcess != null)
						{
							// the connection to the local log service is established
							// => create request
							Request request = requestFactory();

							// send the request asynchronously
							Task<bool> task = SendRequestAsync(request, PipeConnectTimeout, CancellationToken.None)
								.ContinueWith(
									sendTask =>
									{
										try
										{
											// abort, if sending the request threw an exception
											if (sendTask.IsFaulted)
											{
												Debug.WriteLine("Sending request to the local log service failed.");
												if (sendTask.Exception != null)
												{
													Debug.WriteLine("Exceptions:");
													foreach (Exception exception in sendTask.Exception.Flatten().InnerExceptions)
													{
														Debug.WriteLine("{0}: {1}", exception.GetType().Name, exception.Message);
													}
												}

												return false;
											}

											// abort, if sending the request has been canceled
											if (sendTask.IsCanceled)
												return false;

											// the request was successfully sent to the local log service
											// => retrieve the reply
											Reply reply = sendTask.Result;

											// process the reply
											return replyProcessing(request, reply);
										}
										finally
										{
											// the send task has been completed
											lock (mSync)
											{
												Debug.Assert(mSendSettingTask != null);
												Debug.Assert(mSendSettingTask.IsCompleted);
												mSendSettingTask = null;
											}
										}
									},
									CancellationToken.None); // continuation must always run...

							return task;
						}
					}

					break;
				}
			}

			// another task sending the setting to the local log service is already running
			// => wait some time and try again
			Thread.Sleep(50);
		}

		return Task.CompletedTask;
	}

	#endregion

	#region Sending Messages and Commands/Notifications to the Local Log Service (via Shared Memory Queue)

	/// <summary>
	/// Sends a start marker to the local log service.
	/// </summary>
	private unsafe void SendStartMarker()
	{
		Debug.Assert(Monitor.IsEntered(mSync));

		// try to get a free block from the queue
		LogEntryBlock* block = GetLogEntryBlock();

		if (block != null)
		{
			// got a free block
			// => put command into it and return
			block->Type = LogEntryBlockType.StartMarker;
			block->StartMarker.MaxLogLevelCount = -1; // unlimited
			mSharedMemoryQueue.EndWriting(block, sizeof(LogEntryBlock), mLostMessageCount);
			mLostMessageCount = 0;
		}
		else
		{
			// no free block in the queue
			// => lost connection to the local log service
			throw new LocalLogServiceCommunicationException("No free block available, sending start marker failed.");
		}
	}

	/// <summary>
	/// Sends the name of the application to the local log service.
	/// </summary>
	private unsafe void SendApplicationName()
	{
		Debug.Assert(Monitor.IsEntered(mSync));

		// try to get a free block from the queue
		LogEntryBlock* block = GetLogEntryBlock();

		if (block != null)
		{
			// got a free block

			// put command into it
			block->Type = LogEntryBlockType.SetApplicationName;
			int charsToCopy = Math.Min(Log.ApplicationName.Length, LogEntryBlock_SetApplicationName.ApplicationNameSize);
			fixed (char* pApplicationName = Log.ApplicationName)
			{
				Buffer.MemoryCopy(
					pApplicationName,
					block->SetApplicationName.ApplicationName,
					LogEntryBlock_SetApplicationName.ApplicationNameSize * sizeof(char),
					charsToCopy * sizeof(char));
			}

			// terminate the string, if necessary
			if (charsToCopy < LogEntryBlock_SetApplicationName.ApplicationNameSize)
			{
				block->SetApplicationName.ApplicationName[charsToCopy] = (char)0;
			}

			// write the block
			mSharedMemoryQueue.EndWriting(block, sizeof(LogEntryBlock), mLostMessageCount);
			mLostMessageCount = 0;
		}
		else
		{
			// no free block in the queue
			// => lost connection to the local log service
			throw new LocalLogServiceCommunicationException("No free block available, sending application name failed.");
		}
	}

	/// <summary>
	/// Sends all log levels known to the logging subsystem to the local log service.
	/// </summary>
	private unsafe void SendLogLevels()
	{
		Debug.Assert(Monitor.IsEntered(mSync));

		LogLevel[] levels = LogLevel.KnownLevels.ToArray();
		foreach (LogLevel level in levels)
		{
			Debug.Assert(level.Id == mLastSentLogLevelId + 1);

			// try to get a free block from the queue
			LogEntryBlock* block = GetLogEntryBlock();

			if (block != null)
			{
				// got a free block

				// put command into it
				block->Type = LogEntryBlockType.AddLogLevelName;
				block->AddLogLevelName.Identifier = level.Id;
				string logLevelName = MapLogLevel(level);
				int charsToCopy = Math.Min(logLevelName.Length, LogEntryBlock_AddLogLevelName.LogLevelNameSize);
				fixed (char* pLevelName = logLevelName)
				{
					Buffer.MemoryCopy(
						pLevelName,
						block->AddLogLevelName.LogLevelName,
						LogEntryBlock_AddLogLevelName.LogLevelNameSize * sizeof(char),
						charsToCopy * sizeof(char));
				}

				// terminate the string, if necessary
				if (charsToCopy < LogEntryBlock_AddLogLevelName.LogLevelNameSize)
				{
					block->AddLogLevelName.LogLevelName[charsToCopy] = (char)0;
				}

				// write the block
				mSharedMemoryQueue.EndWriting(block, sizeof(LogEntryBlock), mLostMessageCount);
				mLastSentLogLevelId = level.Id;
				mLostMessageCount = 0;
			}
			else
			{
				// no free block in the queue
				// => lost connection to the local log service
				throw new LocalLogServiceCommunicationException("No free block available, sending log level failed.");
			}
		}
	}

	/// <summary>
	/// Sends all source names known to the logging subsystem to the local log service.
	/// </summary>
	private unsafe void SendSourceNames()
	{
		Debug.Assert(Monitor.IsEntered(mSync));

		LogWriter[] writers = LogWriter.KnownWriters.ToArray();
		foreach (LogWriter writer in writers)
		{
			Debug.Assert(writer.Id == mLastSentLogWriterId + 1);

			// try to get a free block from the queue
			LogEntryBlock* block = GetLogEntryBlock();

			if (block != null)
			{
				// got a free block

				// put command into it
				block->Type = LogEntryBlockType.AddSourceName;
				block->AddSourceName.Identifier = writer.Id;
				int charsToCopy = Math.Min(writer.Name.Length, LogEntryBlock_AddSourceName.SourceNameSize);
				fixed (char* pWriterName = writer.Name)
				{
					Buffer.MemoryCopy(
						pWriterName,
						block->AddSourceName.SourceName,
						LogEntryBlock_AddSourceName.SourceNameSize * sizeof(char),
						charsToCopy * sizeof(char));
				}

				// terminate the string, if necessary
				if (charsToCopy < LogEntryBlock_AddSourceName.SourceNameSize)
				{
					block->AddSourceName.SourceName[charsToCopy] = (char)0;
				}

				// write the block
				mSharedMemoryQueue.EndWriting(block, sizeof(LogEntryBlock), mLostMessageCount);
				mLastSentLogWriterId = writer.Id;
				mLostMessageCount = 0;
			}
			else
			{
				// no free block in the queue
				// => lost connection to the local log service
				throw new LocalLogServiceCommunicationException("No free block available, sending log level failed.");
			}
		}
	}

	/// <summary>
	/// Enqueues the specified message for sending to the local log service.
	/// </summary>
	/// <param name="message">Message to send.</param>
	/// <returns>
	/// <c>true</c> if the message was successfully enqueued;<br/>
	/// <c>false</c> if the queue is full.
	/// </returns>
	public bool EnqueueMessage(LocalLogMessage message)
	{
		Debug.Assert(!Monitor.IsEntered(mSync));

		lock (mSync)
		{
			// abort, if the connection is down
			if (mServiceProcess == null)
				return false;

			// try to enqueue the message into the shared memory queue
			if (EnqueueMessage(message, false))
				return true;

			// the shared memory queue is full
			// => abort, if the peak buffer queue is also full
			if (mPeakBufferQueue.Count >= mPeakBufferCapacity)
			{
				mLostMessageCount++;
				return false;
			}

			// there is space in the peak buffer queue
			// => put the message into the queue to send it later
			return EnqueueMessage(message, true);
		}
	}

	/// <summary>
	/// Enqueues the specified message for sending to the local log service.
	/// </summary>
	/// <param name="message">Message to send.</param>
	/// <param name="defer">
	/// <c>true</c> to put the message into the peak buffer queue;<br/>
	/// <c>false</c> to put the message directly into the shared memory queue.
	/// </param>
	/// <returns>
	/// <c>true</c> if the message was successfully enqueued;<br/>
	/// <c>false</c> if the queue is full and lossless mode is disabled.
	/// </returns>
	private unsafe bool EnqueueMessage(LocalLogMessage message, bool defer)
	{
		Debug.Assert(Monitor.IsEntered(mSync));

		long timestamp = message.Timestamp.ToUniversalTime().ToFileTime();
		long highPrecisionTimestamp = (message.HighPrecisionTimestamp + 500) / 1000; // ns => µs

		if (defer || mServiceProcess != null)
		{
			// get an empty block
			LogEntryBlock* firstBlock;
			if (defer)
			{
				LogEntryBlock* b = stackalloc LogEntryBlock[1];
				firstBlock = b;
			}
			else
			{
				firstBlock = GetLogEntryBlock();
			}

			if (firstBlock != null)
			{
				// write the first part of the message (and in most cases the only one...)
				firstBlock->Type = LogEntryBlockType.Message;
				firstBlock->Reserved = 0;
				firstBlock->Message.Timestamp = timestamp;
				firstBlock->Message.HighPrecisionTimestamp = highPrecisionTimestamp;
				firstBlock->Message.LogLevelNameId = message.LogLevel.Id;
				firstBlock->Message.SourceNameId = message.LogWriter.Id;
				firstBlock->Message.ProcessId = sCurrentProcessId;
				firstBlock->Message.MessageExtensionCount = 0;
				int charsToCopy = Math.Min(message.Text.Length, LogEntryBlock_Message.MessageSize);

				fixed (char* pMessage = message.Text)
				{
					Buffer.MemoryCopy(
						pMessage,
						firstBlock->Message.Message,
						LogEntryBlock_Message.MessageSize * sizeof(char),
						charsToCopy * sizeof(char));
				}

				// terminate the message, if it is shorter than the buffer
				if (charsToCopy < LogEntryBlock_Message.MessageSize)
				{
					firstBlock->Message.Message[charsToCopy] = (char)0;
				}

				if (message.Text.Length <= LogEntryBlock_Message.MessageSize)
				{
					// message fits into a single buffer
					// => enqueue block
					if (defer)
					{
						mPeakBufferQueue.Enqueue(*firstBlock);
					}
					else
					{
						mSharedMemoryQueue.EndWriting(firstBlock, sizeof(LogEntryBlock), mLostMessageCount);
						mLostMessageCount = 0;
					}

					return true;
				}

				// message does not fit into a single buffer

				// determine the amount of space needed
				int requiredLength = message.Text.Length;
				const int maxExtensionMessageLength = LogEntryBlock_MessageExtension.MessageSize;
				int requiredExtensionMessages = (requiredLength - LogEntryBlock_Message.MessageSize + maxExtensionMessageLength - 1) / maxExtensionMessageLength;
				Debug.Assert(requiredExtensionMessages > 0);

				// update the first log entry block to store the correct amount of following messages
				// extending the first one
				firstBlock->Message.MessageExtensionCount = requiredExtensionMessages;

				// get enough blocks to store the resulting message
				LogEntryBlock** blocks = stackalloc LogEntryBlock*[requiredExtensionMessages + 1];
				int* bytesWritten = stackalloc int[requiredExtensionMessages + 1];
				blocks[0] = firstBlock;
				bytesWritten[0] = sizeof(LogEntryBlock);

				if (defer)
				{
					LogEntryBlock* b2 = stackalloc LogEntryBlock[requiredExtensionMessages];
					for (int i = 1; i <= requiredExtensionMessages; i++) blocks[i] = &b2[i - 1];
				}
				else
				{
					for (int i = 1; i <= requiredExtensionMessages; i++)
					{
						blocks[i] = GetLogEntryBlock();

						if (blocks[i] == null)
						{
							// no free block left
							// => release already allocated blocks and abort...
							if (mSharedMemoryQueue.IsInitialized) // GetLogEntryBlock() might have shut the queue down...
							{
								for (int j = 0; j < i; j++)
								{
									mSharedMemoryQueue.AbortWriting(blocks[j]);
								}
							}

							return false;
						}
					}
				}

				// initialize the log entry blocks appropriately
				int offset = LogEntryBlock_Message.MessageSize;
				int charsRemaining = requiredLength - LogEntryBlock_Message.MessageSize;
				fixed (char* pMessage = message.Text)
				{
					for (int i = 1; i <= requiredExtensionMessages; i++)
					{
						Debug.Assert(charsRemaining > 0);
						bytesWritten[i] = sizeof(LogEntryBlock);
						blocks[i]->Type = LogEntryBlockType.MessageExtension;
						blocks[i]->Reserved = 0;

						charsToCopy = Math.Min(LogEntryBlock_MessageExtension.MessageSize, charsRemaining);

						Buffer.MemoryCopy(
							pMessage + offset,
							blocks[i]->MessageExtension.Message,
							LogEntryBlock_MessageExtension.MessageSize * sizeof(char),
							charsToCopy * sizeof(char));

						if (charsToCopy < LogEntryBlock_MessageExtension.MessageSize)
						{
							blocks[i]->MessageExtension.Message[charsToCopy] = (char)0;
						}

						offset += charsToCopy;
						charsRemaining -= charsToCopy;
					}
				}

				// enqueue sequence of log entry blocks
				if (defer)
				{
					for (int i = 0; i < requiredExtensionMessages + 1; i++)
					{
						mPeakBufferQueue.Enqueue(*blocks[i]);
					}
				}
				else
				{
					mSharedMemoryQueue.EndWritingSequence((void**)blocks, bytesWritten, requiredExtensionMessages + 1, mLostMessageCount);
					mLostMessageCount = 0;
				}

				return true;
			}
		}

		// enqueuing message failed
		return false;
	}

	/// <summary>
	/// Enqueues a notification that a new log level was added,
	/// so it can map the log level id to the appropriate name.
	/// </summary>
	/// <param name="level">The added log level.</param>
	/// <returns>
	/// <c>true</c> if the notification was successfully enqueued;<br/>
	/// <c>false</c> if the queue is full and lossless mode is disabled.
	/// </returns>
	public bool EnqueueLogLevelAddedNotification(LogLevel level)
	{
		lock (mSync)
		{
			// abort, if the connection is down
			if (mServiceProcess == null)
				return false;

			// try to enqueue the notification into the shared memory queue
			// or put the notification into the queue to send it later, if it cannot be enqueued immediately
			// (do not check the capacity of the peak buffer queue as the notifications must always be sent to the local log service)
			return EnqueueLogLevelAddedNotification(level, false) ||
			       EnqueueLogLevelAddedNotification(level, true);
		}
	}

	/// <summary>
	/// Enqueues a notification that a new log level was added,
	/// so it can map the log level id to the appropriate name.
	/// </summary>
	/// <param name="level">The added log level.</param>
	/// <param name="defer">
	/// <c>true</c> to put the notification into the peak buffer queue;<br/>
	/// <c>false</c> to put the notification directly into the shared memory queue.
	/// </param>
	/// <returns>
	/// <c>true</c> if the notification was successfully enqueued;<br/>
	/// <c>false</c> if the queue is full and lossless mode is disabled.
	/// </returns>
	private unsafe bool EnqueueLogLevelAddedNotification(LogLevel level, bool defer)
	{
		Debug.Assert(Monitor.IsEntered(mSync));
		Debug.Assert(level.Id == mLastSentLogLevelId + 1);

		if (defer || mServiceProcess != null)
		{
			// try to get a free block
			LogEntryBlock* block;
			if (defer)
			{
				LogEntryBlock* b = stackalloc LogEntryBlock[1];
				block = b;
			}
			else
			{
				block = GetLogEntryBlock();
			}

			if (block != null)
			{
				// got a free block

				// prepare notification
				block->Type = LogEntryBlockType.AddLogLevelName;
				block->AddLogLevelName.Identifier = level.Id;
				string logLevelName = MapLogLevel(level);
				int charsToCopy = Math.Min(logLevelName.Length, LogEntryBlock_AddLogLevelName.LogLevelNameSize);
				fixed (char* pName = logLevelName)
				{
					Buffer.MemoryCopy(
						pName,
						block->AddLogLevelName.LogLevelName,
						LogEntryBlock_AddLogLevelName.LogLevelNameSize * sizeof(char),
						charsToCopy * sizeof(char));
				}

				// terminate the message, if it is shorter than the buffer
				if (charsToCopy < LogEntryBlock_AddLogLevelName.LogLevelNameSize)
				{
					block->AddLogLevelName.LogLevelName[charsToCopy] = (char)0;
				}

				// enqueue notification
				if (defer)
				{
					mPeakBufferQueue.Enqueue(*block);
				}
				else
				{
					mSharedMemoryQueue.EndWriting(block, sizeof(LogEntryBlock), mLostMessageCount);
					mLostMessageCount = 0;
				}

				mLastSentLogLevelId = level.Id;
				return true;
			}
		}

		// enqueuing notification failed
		return false;
	}

	/// <summary>
	/// Enqueues a notification that a new log writer was added,
	/// so it can map the log writer id to the appropriate name.
	/// </summary>
	/// <param name="writer">The added log writer.</param>
	/// <returns>
	/// <c>true</c> if the notification was successfully enqueued;<br/>
	/// <c>false</c> if the queue is full and lossless mode is disabled.
	/// </returns>
	public bool EnqueueLogWriterAddedNotification(LogWriter writer)
	{
		lock (mSync)
		{
			// abort, if the connection is down
			if (mServiceProcess == null)
				return false;

			// try to enqueue the notification into the shared memory queue
			// or put the notification into the queue to send it later, if it cannot be enqueued immediately
			// (do not check the capacity of the peak buffer queue as the notifications must always be sent to the local log service)
			return EnqueueLogWriterAddedNotification(writer, false) ||
			       EnqueueLogWriterAddedNotification(writer, true);
		}
	}

	/// <summary>
	/// Enqueues a notification that a new log writer was added,
	/// so it can map the log writer id to the appropriate name.
	/// </summary>
	/// <param name="writer">The added log writer.</param>
	/// <param name="defer">
	/// <c>true</c> to put the notification into the peak buffer queue;<br/>
	/// <c>false</c> to put the notification directly into the shared memory queue.
	/// </param>
	/// <returns>
	/// <c>true</c> if the notification was successfully enqueued;<br/>
	/// <c>false</c> if the queue is full and lossless mode is disabled.
	/// </returns>
	private unsafe bool EnqueueLogWriterAddedNotification(LogWriter writer, bool defer)
	{
		Debug.Assert(Monitor.IsEntered(mSync));
		Debug.Assert(writer.Id == mLastSentLogWriterId + 1);

		if (defer || mServiceProcess != null)
		{
			// try to get a free block
			LogEntryBlock* block;
			if (defer)
			{
				LogEntryBlock* b = stackalloc LogEntryBlock[1];
				block = b;
			}
			else
			{
				block = GetLogEntryBlock();
			}

			if (block != null)
			{
				// got a free block

				// prepare notification
				block->Type = LogEntryBlockType.AddSourceName;
				block->AddSourceName.Identifier = writer.Id;
				int charsToCopy = Math.Min(writer.Name.Length, LogEntryBlock_AddSourceName.SourceNameSize);
				fixed (char* pName = writer.Name)
				{
					Buffer.MemoryCopy(
						pName,
						block->AddSourceName.SourceName,
						LogEntryBlock_AddSourceName.SourceNameSize * sizeof(char),
						charsToCopy * sizeof(char));
				}

				// terminate the message, if it is shorter than the buffer
				if (charsToCopy < LogEntryBlock_AddSourceName.SourceNameSize)
				{
					block->AddSourceName.SourceName[charsToCopy] = (char)0;
				}

				// enqueue notification
				if (defer)
				{
					mPeakBufferQueue.Enqueue(*block);
				}
				else
				{
					mSharedMemoryQueue.EndWriting(block, sizeof(LogEntryBlock), mLostMessageCount);
					mLostMessageCount = 0;
				}

				mLastSentLogWriterId = writer.Id;
				return true;
			}
		}

		// enqueuing notification failed
		return false;
	}

	/// <summary>
	/// Enqueues a command telling the log viewer to clear its view.
	/// </summary>
	/// <returns>
	/// <c>true</c> if the command was successfully enqueued;<br/>
	/// <c>false</c> if the queue is full and lossless mode is disabled.
	/// </returns>
	public bool EnqueueClearLogViewerCommand()
	{
		Debug.Assert(!Monitor.IsEntered(mSync));

		lock (mSync)
		{
			// try to enqueue the command into the shared memory queue
			if (EnqueueClearLogViewerCommand(false))
				return true;

			// the shared memory queue is full
			// => put the command into the queue to send it later, if there is some space in the peak buffer queue
			return mPeakBufferQueue.Count < mPeakBufferCapacity &&
			       EnqueueClearLogViewerCommand(true);
		}
	}

	/// <summary>
	/// Enqueues a command telling the log viewer to clear its view.
	/// </summary>
	/// <param name="defer">
	/// <c>true</c> to put the command into the peak buffer queue;<br/>
	/// <c>false</c> to put the command directly into the shared memory queue.
	/// </param>
	/// <returns>
	/// <c>true</c> if the command was successfully enqueued;<br/>
	/// <c>false</c> if the queue is full and lossless mode is disabled.
	/// </returns>
	private unsafe bool EnqueueClearLogViewerCommand(bool defer)
	{
		Debug.Assert(!Monitor.IsEntered(mSync));

		lock (mSync)
		{
			if (defer || mServiceProcess != null)
			{
				// try to get a free block
				LogEntryBlock* block;
				if (defer)
				{
					LogEntryBlock* b = stackalloc LogEntryBlock[1];
					block = b;
				}
				else
				{
					block = GetLogEntryBlock();
				}

				if (block != null)
				{
					// got a free block
					// => put command into it
					block->Type = LogEntryBlockType.ClearLogViewer;
					block->ClearLogViewer.Timestamp = LogWriter.GetTimestamp().ToUniversalTime().ToFileTime();
					block->ClearLogViewer.HighPrecisionTimestamp = (LogWriter.GetHighPrecisionTimestamp() + 500) / 1000; // ns => µs
					block->ClearLogViewer.ProcessId = sCurrentProcessId;

					// enqueue command
					if (defer)
					{
						mPeakBufferQueue.Enqueue(*block);
					}
					else
					{
						mSharedMemoryQueue.EndWriting(block, sizeof(LogEntryBlock), mLostMessageCount);
						mLostMessageCount = 0;
					}

					return true;
				}
			}

			// enqueuing command failed
			return false;
		}
	}

	/// <summary>
	/// Enqueues a command telling the local log service to save a snapshot of the current log.
	/// </summary>
	/// <returns>
	/// <c>true</c> if the command was successfully enqueued;<br/>
	/// <c>false</c> if the queue is full and lossless mode is disabled.
	/// </returns>
	public bool EnqueueSaveSnapshotCommand()
	{
		Debug.Assert(!Monitor.IsEntered(mSync));

		lock (mSync)
		{
			// try to enqueue the command into the shared memory queue
			if (EnqueueSaveSnapshotCommand(false))
				return true;

			// the shared memory queue is full
			// => put the command into the queue to send it later, if there is some space in the peak buffer queue
			return mPeakBufferQueue.Count < mPeakBufferCapacity &&
			       EnqueueSaveSnapshotCommand(true);
		}
	}

	/// <summary>
	/// Enqueues a command telling the local log service to save a snapshot of the current log.
	/// </summary>
	/// <param name="defer">
	/// <c>true</c> to put the command into the peak buffer queue;<br/>
	/// <c>false</c> to put the command directly into the shared memory queue.
	/// </param>
	/// <returns>
	/// <c>true</c> if the command was successfully enqueued;<br/>
	/// <c>false</c> if the queue is full and lossless mode is disabled.
	/// </returns>
	private unsafe bool EnqueueSaveSnapshotCommand(bool defer)
	{
		Debug.Assert(Monitor.IsEntered(mSync));

		if (defer || mServiceProcess != null)
		{
			// try to get a free block
			LogEntryBlock* block;
			if (defer)
			{
				LogEntryBlock* b = stackalloc LogEntryBlock[1];
				block = b;
			}
			else
			{
				block = GetLogEntryBlock();
			}

			if (block != null)
			{
				// got a free block

				// prepare command and send it
				block->Type = LogEntryBlockType.SaveSnapshot;
				block->SaveSnapshot.Timestamp = LogWriter.GetTimestamp().ToUniversalTime().ToFileTime();
				block->SaveSnapshot.HighPrecisionTimestamp = (LogWriter.GetHighPrecisionTimestamp() + 500) / 1000; // ns => µs
				block->SaveSnapshot.ProcessId = sCurrentProcessId;

				// enqueue command
				if (defer)
				{
					mPeakBufferQueue.Enqueue(*block);
				}
				else
				{
					mSharedMemoryQueue.EndWriting(block, sizeof(LogEntryBlock), mLostMessageCount);
					mLostMessageCount = 0;
				}

				return true;
			}
		}

		// enqueuing command failed
		return false;
	}

	/// <summary>
	/// Gets a free block from the shared memory queue.
	/// </summary>
	/// <param name="sendDeferredItems">
	/// <c>true</c> to send deferred items, if any;<br/>
	/// <c>false</c> to skip sending deferred items (only for use within <see cref="GetLogEntryBlock"/>).
	/// </param>
	/// <returns>
	/// A free log entry block;<br/>
	/// <c>null</c> if the queue does not contain a free block.
	/// </returns>
	/// <remarks>
	/// This method returns a free block from the log entry queue. If the queue does not contain any
	/// free blocks, it tries to get a block after a certain time (<see cref="QueueBlockFetchRetryDelayTime"/>).
	/// </remarks>
	private unsafe LogEntryBlock* GetLogEntryBlock(bool sendDeferredItems = true)
	{
		Debug.Assert(Monitor.IsEntered(mSync));

		// before returning a free block to the caller all deferred blocks have to be passed to ensure that the
		// messages/commands/notifications arrive in the correct order
		if (sendDeferredItems && !SendDeferredItems())
			return null;

		while (true)
		{
			// try to get a free block from the queue
			// (suppress the overflow indication, if explicitly specified and on the first run only to avoid counting the condition multiple times)
			var pBlock = (LogEntryBlock*)mSharedMemoryQueue.BeginWriting();
			if (pBlock != null) return pBlock;

			// no free block in the queue
			// => find out what's going wrong...
			if (!IsLogSinkAlive())
			{
				// local log service has died
				// => initiate shutting down asynchronously and trigger to reconnect
				mTriggerConnectivityMonitorEvent.Set();
				return null;
			}

			// abort, if lossless mode is not enabled
			if (!mLosslessMode) return null;

			// lossless mode is enabled
			// => wait some time before retrying
			Thread.Sleep(QueueBlockFetchRetryDelayTime);
		}
	}

	/// <summary>
	/// Tries to transfer all items from the peak buffer queue to the shared memory queue.
	/// </summary>
	/// <returns>
	/// <c>true</c> if all items have been sent successfully;<br/>
	/// <c>false</c> if the queue is full or not initialized.
	/// </returns>
	private bool SendDeferredItems()
	{
		Debug.Assert(Monitor.IsEntered(mSync));

		// abort, if the shared memory queue is not initialized
		if (!mSharedMemoryQueue.IsInitialized)
			return false;

		while (mPeakBufferQueue.Count > 0)
		{
			LogEntryBlock block = mPeakBufferQueue.Peek();

			switch (block.Type)
			{
				// item is a message that can consist of multiple blocks
				case LogEntryBlockType.Message:
					if (!SendDeferredItems_Message(block.Message.MessageExtensionCount)) return false;
					break;

				// item can consist of a single block only
				case LogEntryBlockType.AddSourceName:
				case LogEntryBlockType.AddLogLevelName:
				case LogEntryBlockType.SetApplicationName:
				case LogEntryBlockType.StartMarker:
				case LogEntryBlockType.ClearLogViewer:
				case LogEntryBlockType.SaveSnapshot:
					if (!SendDeferredItems_SingleBlock()) return false;
					break;

				case LogEntryBlockType.MessageExtension:
				default:
					Debug.Fail($"Item of type '{block.Type}' is not expected.");
					break;
			}
		}

		return true;
	}

	/// <summary>
	/// Tries to transfer the current message block incl. its extension blocks from the peak buffer queue
	/// to the shared memory queue.
	/// </summary>
	/// <param name="extensionMessageCount">Number of extension message blocks following the message block..</param>
	/// <returns>
	/// <c>true</c> if the message block and all extension blocks have been sent successfully;<br/>
	/// <c>false</c> if the queue is full.
	/// </returns>
	private unsafe bool SendDeferredItems_Message(int extensionMessageCount)
	{
		// allocate enough blocks for the message block incl. its extension blocks
		int requiredBlockCount = extensionMessageCount + 1;
		LogEntryBlock** blocks = stackalloc LogEntryBlock*[requiredBlockCount];
		int* blockSizes = stackalloc int[requiredBlockCount];
		for (int i = 0; i < requiredBlockCount; i++)
		{
			blockSizes[i] = sizeof(LogEntryBlock);
			blocks[i] = GetLogEntryBlock(false);
			if (blocks[i] == null)
			{
				if (mSharedMemoryQueue.IsInitialized) // GetLogEntryBlock() might have shut the queue down...
				{
					for (int j = 0; j < i; j++)
					{
						mSharedMemoryQueue.AbortWriting(blocks[j]);
					}
				}

				return false;
			}
		}

		// populate blocks
		for (int i = 0; i < requiredBlockCount; i++)
		{
			// there should be enough blocks in the queue, otherwise the enqueuing operation is broken...
			LogEntryBlock block = mPeakBufferQueue.Dequeue();
			Buffer.MemoryCopy(&block, blocks[i], sizeof(LogEntryBlock), sizeof(LogEntryBlock));
		}

		// enqueue blocks
		mSharedMemoryQueue.EndWritingSequence((void**)blocks, blockSizes, requiredBlockCount, mLostMessageCount);
		mLostMessageCount = 0;
		return true;
	}

	/// <summary>
	/// Tries to transfer the current block from the peak buffer queue to the shared memory queue.
	/// </summary>
	/// <returns>
	/// <c>true</c> if the block has been sent successfully;<br/>
	/// <c>false</c> if the queue is full.
	/// </returns>
	private unsafe bool SendDeferredItems_SingleBlock()
	{
		LogEntryBlock* pBlock = GetLogEntryBlock(false);
		if (pBlock == null) return false;
		LogEntryBlock block = mPeakBufferQueue.Dequeue();
		Buffer.MemoryCopy(&block, pBlock, sizeof(LogEntryBlock), sizeof(LogEntryBlock));
		mSharedMemoryQueue.EndWriting(pBlock, sizeof(LogEntryBlock), mLostMessageCount);
		mLostMessageCount = 0;
		return true;
	}

	#endregion

	#region Mapping Log Levels

	/// <summary>
	/// Maps the specified log level to the log level name to send to the Local Log Service.
	/// </summary>
	/// <param name="level">Log level to map.</param>
	/// <returns>Name of the log level to send to the Local Log Service.</returns>
	private static string MapLogLevel(LogLevel level)
	{
		switch (level.Id)
		{
			case 0:
				Debug.Assert(level == LogLevel.Emergency);
				return "Failure";

			case 1:
				Debug.Assert(level == LogLevel.Alert);
				return "Failure";

			case 2:
				Debug.Assert(level == LogLevel.Critical);
				return "Failure";

			case 3:
				Debug.Assert(level == LogLevel.Error);
				return "Error";

			case 4:
				Debug.Assert(level == LogLevel.Warning);
				return "Warning";

			case 5:
				Debug.Assert(level == LogLevel.Notice);
				return "Note";

			case 6:
				Debug.Assert(level == LogLevel.Informational);
				return "Note";

			case 7:
				Debug.Assert(level == LogLevel.Debug);
				return "Developer";

			case 8:
				Debug.Assert(level == LogLevel.Trace);
				return "Trace0";

			default:
				// aspect log levels
				return level.Name;
		}
	}

	#endregion
}
