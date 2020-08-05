///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// An interface to the local log service for logging clients.
	/// </summary>
	internal sealed unsafe partial class LocalLogServiceConnection
	{
		private const int PipeRequestTimeout = 1000; // ms
		private const int QueueBlockFetchRetryDelayTime = 20; // ms

		private static readonly int sCurrentProcessId = Process.GetCurrentProcess().Id;
		private readonly object mSync = new object();
		private readonly string mSinkServerPipeName;
		private readonly string mGlobalQueueName;
		private readonly string mLocalQueueName;
		private readonly UnsafeSharedMemoryQueue mSharedMemoryQueue = new UnsafeSharedMemoryQueue();
		private bool mInitialized;
		private int mPeakBufferCapacity;
		private readonly Queue<LogEntryBlock> mPeakBufferQueue = new Queue<LogEntryBlock>();
		private int mLostMessageCount;
		private bool mAutoReconnect = true;
		private TimeSpan mAutoReconnectRetryInterval = TimeSpan.FromSeconds(15);
		private Task mAutoReconnectTask;
		private CancellationTokenSource mAutoReconnectTaskCancellationTokenSource;
		private bool mLosslessMode;
		private bool mWriteToLogFile = true;
		private Process mServiceProcess;

		#region Construction

		/// <summary>
		/// Initializes the <see cref="LocalLogServiceConnection"/> class.
		/// </summary>
		static LocalLogServiceConnection()
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
		/// Gets or sets a value indicating whether the connection is re-established after breaking down
		/// (most probably due to the local log service shutting down or restarting).
		/// </summary>
		public bool AutoReconnect
		{
			get
			{
				lock (mSync)
				{
					return mAutoReconnect;
				}
			}

			set
			{
				lock (mSync)
				{
					if (mAutoReconnect == value) return;
					mAutoReconnect = value;
					if (mInitialized && !IsLogSinkAlive())
					{
						StartAutoReconnectTask();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the interval between two attempts to re-establish the connection to the local log service.
		/// Requires <see cref="AutoReconnect"/> to be set to <c>true</c>.
		/// </summary>
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
				lock (mSync)
				{
					mAutoReconnectRetryInterval = value;
				}
			}
		}

		#endregion

		#region Lossless Mode

		/// <summary>
		/// Gets or sets a value indicating whether the lossless mode is enabled or disabled (default: false).
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
		/// to 0 to disable peak buffering messages (notifications are always buffered to avoid getting out of sync).
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
				if (mWriteToLogFile != value)
				{
					mWriteToLogFile = value;

					if (mServiceProcess != null)
					{
						try
						{
							// prepare request for the local log service
							Request request = new Request
							{
								Command = Command.SetWritingToLogFile,
								SetWritingToLogFileCommand =
								{
									ProcessId = sCurrentProcessId,
									Enable = mWriteToLogFile ? 1 : 0
								}
							};

							// send request
							var reply = SendRequest(request);

							// got a reply to the issued request
							// => evaluate the result code
							//    0 = operation failed
							//    1 = operation succeeded
							if (reply.Result == 0)
							{
								Debug.WriteLine(
									mWriteToLogFile
									? "Enabling writing messages to the log file failed."
									: "Disabling writing messages to the log file failed.");
							}
						}
						catch (Exception ex)
						{
							Debug.WriteLine("Sending request to enable/disable writing messages to the log file failed.");
							Debug.WriteLine(ex.ToString());
						}
					}
				}
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
		/// Initializes the connection to the local log service.
		/// </summary>
		/// <returns>
		/// true, if the connection has been established successfully or if it already was established;
		/// false, if establishing the connection failed and <see cref="AutoReconnect"/> is <c>false</c>, so the connection will not be established automatically.
		/// </returns>
		public bool Initialize()
		{
			lock (mSync)
			{
				// abort, if the connection is already up and running
				if (IsLogSinkAlive())
					return true;

				// shut the connection down to clean up resources
				ShutdownConnection();

				// try to establish a new connection
				if (InitConnection())
				{
					// connection was established successfully
					mInitialized = true;
					return true;
				}

				// connection was not established successfully
				if (mAutoReconnect)
				{
					// start task that tries to reconnect periodically, if necessary
					StartAutoReconnectTask();
					mInitialized = true;
					return true;
				}

				// the connection has not been established and the reconnect task is not scheduled
				// => the will not be established automatically
				mInitialized = false;
				return false;
			}
		}

		/// <summary>
		/// Schedules a task to connect to the local log service after the <see cref="mAutoReconnectRetryInterval"/>.
		/// </summary>
		private void StartAutoReconnectTask()
		{
			lock (mSync)
			{
				// cancel the last connect operation
				// NOTE: Cannot wait for the task to complete, because this could hang up execution.
				//       The task code is properly synchronized to avoid race conditions
				mAutoReconnectTaskCancellationTokenSource?.Cancel();

				// create a new cancellation token source
				// (it is necessary to pull the token out of mAutoReconnectTaskCancellationTokenSource to ensure it is not overwritten by replacing mAutoReconnectTaskCancellationTokenSource)
				mAutoReconnectTaskCancellationTokenSource = new CancellationTokenSource();
				CancellationToken cts = mAutoReconnectTaskCancellationTokenSource.Token;

				// schedule a new task to retry to connect to the local log service
				mAutoReconnectTask = Task
					.Delay(mAutoReconnectRetryInterval, cts)
					.ContinueWith(x =>
					{
						lock (mSync)
						{
							cts.ThrowIfCancellationRequested();

							if (mInitialized && mAutoReconnect && !IsLogSinkAlive())
							{
								// shut the connection down to clean up resources
								ShutdownConnection();

								// try to connect to the local log service
								if (!InitConnection())
								{
									// connecting to the local log service failed
									// => schedule a new task to retry after the specified time
									StartAutoReconnectTask();
								}
							}
						}
					}, cts, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);
			}
		}

		/// <summary>
		/// Initializes the connection to the local log service.
		/// </summary>
		/// <returns>
		/// true, if connecting to the local log service succeeded;
		/// otherwise false.
		/// </returns>
		private bool InitConnection()
		{
			lock (mSync)
			{
				// create an 'unregister' request (for error conditions)
				Request unregisterRequest = new Request
				{
					Command = Command.UnregisterLogSource,
					UnregisterLogSourceCommand =
					{
						ProcessId = sCurrentProcessId
					}
				};

				Debug.WriteLine("Initializing connection to the local log service.");

				// clear buffered messages / commands / notifications
				mPeakBufferQueue.Clear();
				mLostMessageCount = 0;

				///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
				// register log source with the local log service
				///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
				try
				{
					// prepare request
					Request request = new Request
					{
						Command = Command.RegisterLogSource,
						RegisterLogSourceCommand =
						{
							ProcessId = sCurrentProcessId
						}
					};

					// send request
					var reply = SendRequest(request);

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
						return false;
					}
				}
				catch (Exception ex)
				{
					// request failed
					Debug.WriteLine("Registering log source with the local log service failed.");
					Debug.WriteLine(ex.ToString());
					return false;
				}

				///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
				// get the process id of the local log service
				///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
				int serviceProcessId;
				try
				{
					serviceProcessId = QueryProcessId();
					Debug.WriteLine($"Getting process id of the local log service succeeded (process id: {serviceProcessId}).");
				}
				catch (Exception ex)
				{
					Debug.WriteLine("Getting process id of the local log service failed.");
					try
					{
						SendRequest(unregisterRequest, 0);
					}
					catch
					{
						/* swallow */
					}

					Debug.WriteLine(ex.ToString());
					return false;
				}

				///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
				// tell the local log service about the to 'write to log file' setting
				///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
				try
				{
					Request request = new Request
					{
						Command = Command.SetWritingToLogFile,
						SetWritingToLogFileCommand =
						{
							ProcessId = sCurrentProcessId,
							Enable = mWriteToLogFile ? 1 : 0
						}
					};

					// send request
					var reply = SendRequest(request);

					// evaluate the result code
					// 0 = setting succeeded
					// 1 = setting failed
					if (reply.Result == 0)
					{
						Debug.WriteLine("The local log service failed to enable writing to the log file.");
						// proceed in case of an error...
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine("Sending 'set writing to log file' request to the local log service failed.");
					Debug.WriteLine(ex.ToString());
					return false;
				}

				///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
				// open the sink's process
				///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
				Process serviceProcess = Process.GetProcessById(serviceProcessId);

				try
				{
					///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
					// open the shared memory queue the sink has created for us
					///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
					try
					{
						mSharedMemoryQueue.Open(mGlobalQueueName);
					}
					catch (Exception ex)
					{
						Debug.WriteLine("Opening the shared memory queue in the global namespace failed.");
						Debug.WriteLine(ex.ToString());

						// try to open the shared memory queue in the local namespace
						// (local log service has been started without the privilege to create global objects)
						try
						{
							mSharedMemoryQueue.Open(mLocalQueueName);
						}
						catch (Exception ex2)
						{
							Debug.WriteLine("Opening the shared memory queue in the local namespace failed.");
							Debug.WriteLine(ex2.ToString());
							return false;
						}
					}

					Debug.WriteLine("Opening the shared memory queue succeeded.");

					// tell the sink that a new session begins
					SendStartMarker();

					// tell the sink about the application name
					SendApplicationName();

					// tell the sink about log levels in use
					SendLogLevels();

					// tell the sink about source names in use
					SendSourceNames();
				}
				catch (Exception ex)
				{
					Debug.WriteLine("Connecting to the local log service failed.");
					Debug.WriteLine(ex.ToString());

					try
					{
						SendRequest(unregisterRequest, 0);
					}
					catch
					{
						/* swallow */
					}

					mSharedMemoryQueue.Close();
					serviceProcess.Dispose();
					return false;
				}

				// the log source has been initialized successfully
				Debug.WriteLine("Connection to the local log service was established successfully.");
				mServiceProcess = serviceProcess;

				return true;
			}
		}

		/// <summary>
		/// Shuts the connection to the local log service down.
		/// </summary>
		public void Shutdown()
		{
			lock (mSync)
			{
				ShutdownConnection();
				mInitialized = false;
			}
		}

		/// <summary>
		/// Shuts the connection to the local log service down.
		/// </summary>
		private void ShutdownConnection()
		{
			lock (mSync)
			{
				// cancel reconnect task, but do not wait for the task to complete
				// (could cause a dead lock in rare cases)
				mAutoReconnectTaskCancellationTokenSource?.Cancel();
				mAutoReconnectTaskCancellationTokenSource = null;

				// clear buffered messages / commands / notifications
				mPeakBufferQueue.Clear();
				mLostMessageCount = 0;

				if (mServiceProcess != null)
				{
					try
					{
						// prepare request
						Request request = new Request
						{
							Command = Command.UnregisterLogSource,
							UnregisterLogSourceCommand =
							{
								ProcessId = sCurrentProcessId
							}
						};

						// send request
						var reply = SendRequest(request);

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

					mServiceProcess.Dispose();
					mServiceProcess = null;
				}

				mSharedMemoryQueue.Close();
			}
		}

		#endregion

		#region Checking Vital Sign of the Local Log Service

		/// <summary>
		/// Checks whether the local log service is alive.
		/// </summary>
		/// <returns>
		/// true, if the local log service process is alive;
		/// otherwise false.
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
		/// <param name="timeout">Time to wait for the request to finish (in ms).</param>
		/// <returns>The received reply.</returns>
		/// <exception cref="LocalLogServiceCommunicationException">Communicating with the local log service failed.</exception>
		private Reply SendRequest(Request request, int timeout = PipeRequestTimeout)
		{
			try
			{
				using (NamedPipeClientStream pipe = new NamedPipeClientStream(".", mSinkServerPipeName, PipeDirection.InOut))
				using (MemoryReader reader = new MemoryReader(pipe))
				using (MemoryWriter writer = new MemoryWriter(pipe))
				{
					// connect to the pipe or wait until the pipe is available
					// (the local log service has a set of pipes to serve multiple clients, so waiting most likely
					// indicates that the service is not running)
					pipe.Connect(0);
					pipe.ReadMode = PipeTransmissionMode.Message;

					// TODO: configure timeout for writing and reading struct
					//       (at the moment the pipe does not support setting ReadTimeout and WriteTimeout)

					// send the request to the local log service
					writer.WriteStruct(request);

					// wait for the reply
					return reader.ReadStruct<Reply>();
				}
			}
			catch (Exception ex)
			{
				throw new LocalLogServiceCommunicationException("Sending request to the local log service failed.", ex);
			}
		}

		/// <summary>
		/// Gets the process id of the local log service.
		/// </summary>
		/// <param name="timeout">Timeout (in ms).</param>
		/// <returns>Process id of the local log service.</returns>
		/// <exception cref="LocalLogServiceCommunicationException">Communicating with the local log service failed.</exception>
		private int QueryProcessId(int timeout = PipeRequestTimeout)
		{
			Request request = new Request { Command = Command.QueryProcessId };
			var reply = SendRequest(request, timeout);
			return reply.QueryProcessIdCommand.ProcessId;
		}

		#endregion

		#region Sending Messages and Commands/Notifications to the Local Log Service (via Shared Memory Queue)

		/// <summary>
		/// Sends a start marker to the local log service.
		/// </summary>
		private void SendStartMarker()
		{
			lock (mSync)
			{
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
		}

		/// <summary>
		/// Sends the name of the application to the local log service.
		/// </summary>
		private void SendApplicationName()
		{
			lock (mSync)
			{
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
						block->SetApplicationName.ApplicationName[charsToCopy] = (char) 0;
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
		}

		/// <summary>
		/// Sends all log levels known to the logging subsystem to the local log service.
		/// </summary>
		private void SendLogLevels()
		{
			lock (mSync)
			{
				LogLevel[] levels = LogLevel.KnownLevels.ToArray();
				foreach (LogLevel level in levels)
				{
					// try to get a free block from the queue
					LogEntryBlock* block = GetLogEntryBlock();

					if (block != null)
					{
						// got a free block

						// put command into it
						block->Type = LogEntryBlockType.AddLogLevelName;
						block->AddLogLevelName.Identifier = level.Id;
						int charsToCopy = Math.Min(level.Name.Length, LogEntryBlock_AddLogLevelName.LogLevelNameSize);
						fixed (char* pLevelName = level.Name)
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
		}

		/// <summary>
		/// Sends all source names known to the logging subsystem to the local log service.
		/// </summary>
		private void SendSourceNames()
		{
			lock (mSync)
			{
				LogWriter[] writers = Log.KnownWriters.ToArray();
				foreach (LogWriter writer in writers)
				{
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
		}

		/// <summary>
		/// Enqueues the specified message for sending to the local log service.
		/// </summary>
		/// <param name="message">Message to send.</param>
		/// <returns>
		/// true, if the message was successfully enqueued;
		/// false, if the queue is full.
		/// </returns>
		public bool EnqueueMessage(LocalLogMessage message)
		{
			lock (mSync)
			{
				// try to enqueue the message into the shared memory queue
				if (EnqueueMessage(message, false))
					return true;

				// the shared memory queue is full
				// => abort, if the peak buffer queue is also full
				if (mPeakBufferQueue.Count >= mPeakBufferCapacity) {
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
		/// true to put the message into the peak buffer queue;
		/// false to put the message directly into the shared memory queue.
		/// </param>
		/// <returns>
		/// true, if the message was successfully enqueued;
		/// false, if the queue is full and lossless mode is disabled.
		/// </returns>
		private bool EnqueueMessage(LocalLogMessage message, bool defer)
		{
			lock (mSync)
			{
				var timestamp = message.Timestamp.ToUniversalTime().ToFileTime();
				var highPrecisionTimestamp = (message.HighPrecisionTimestamp + 500) / 1000; // ns => µs

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
							firstBlock->Message.Message[charsToCopy] = (char) 0;
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
									blocks[i]->MessageExtension.Message[charsToCopy] = (char) 0;
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
							mSharedMemoryQueue.EndWritingSequence((void**) blocks, bytesWritten, requiredExtensionMessages + 1, mLostMessageCount);
							mLostMessageCount = 0;
						}

						return true;
					}
				}

				// enqueuing message failed
				return false;
			}
		}

		/// <summary>
		/// Enqueues a notification that a new log level was added,
		/// so it can map the log level id to the appropriate name.
		/// </summary>
		/// <param name="level">The added log level.</param>
		/// <returns>
		/// true, if the notification was successfully enqueued;
		/// false, if the queue is full and lossless mode is disabled.
		/// </returns>
		public bool EnqueueLogLevelAddedNotification(LogLevel level)
		{
			lock (mSync)
			{
				// try to enqueue the notification into the shared memory queue
				if (EnqueueLogLevelAddedNotification(level, false))
					return true;

				// put the notification into the queue to send it later
				// (do not check the capacity of the peak buffer queue as the notifications must always be sent to the local log service)
				return EnqueueLogLevelAddedNotification(level, true);
			}
		}

		/// <summary>
		/// Enqueues a notification that a new log level was added,
		/// so it can map the log level id to the appropriate name.
		/// </summary>
		/// <param name="level">The added log level.</param>
		/// <param name="defer">
		/// true to put the notification into the peak buffer queue;
		/// false to put the notification directly into the shared memory queue.
		/// </param>
		/// <returns>
		/// true, if the notification was successfully enqueued;
		/// false, if the queue is full and lossless mode is disabled.
		/// </returns>
		private bool EnqueueLogLevelAddedNotification(LogLevel level, bool defer)
		{
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

						// prepare notification
						block->Type = LogEntryBlockType.AddLogLevelName;
						block->AddLogLevelName.Identifier = level.Id;
						int charsToCopy = Math.Min(level.Name.Length, LogEntryBlock_AddLogLevelName.LogLevelNameSize);
						fixed (char* pName = level.Name)
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
							block->AddLogLevelName.LogLevelName[charsToCopy] = (char) 0;
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

						return true;
					}
				}

				// enqueuing notification failed
				return false;
			}
		}

		/// <summary>
		/// Enqueues a notification that a new log writer was added,
		/// so it can map the log writer id to the appropriate name.
		/// </summary>
		/// <param name="writer">The added log writer.</param>
		/// <returns>
		/// true, if the notification was successfully enqueued;
		/// false, if the queue is full and lossless mode is disabled.
		/// </returns>
		public bool EnqueueLogWriterAddedNotification(LogWriter writer)
		{
			lock (mSync)
			{
				// try to enqueue the notification into the shared memory queue
				if (EnqueueLogWriterAddedNotification(writer, false))
					return true;

				// put the notification into the queue to send it later
				// (do not check the capacity of the peak buffer queue as the notifications must always be sent to the local log service)
				return EnqueueLogWriterAddedNotification(writer, true);
			}
		}

		/// <summary>
		/// Enqueues a notification that a new log writer was added,
		/// so it can map the log writer id to the appropriate name.
		/// </summary>
		/// <param name="writer">The added log writer.</param>
		/// <param name="defer">
		/// true to put the notification into the peak buffer queue;
		/// false to put the notification directly into the shared memory queue.
		/// </param>
		/// <returns>
		/// true, if the notification was successfully enqueued;
		/// false, if the queue is full and lossless mode is disabled.
		/// </returns>
		private bool EnqueueLogWriterAddedNotification(LogWriter writer, bool defer)
		{
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
							block->AddSourceName.SourceName[charsToCopy] = (char) 0;
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

						return true;
					}
				}

				// enqueuing notification failed
				return false;
			}
		}

		/// <summary>
		/// Enqueues a command telling the log viewer to clear its view.
		/// </summary>
		/// <returns>
		/// true, if the command was successfully enqueued;
		/// false, if the queue is full and lossless mode is disabled.
		/// </returns>
		public bool EnqueueClearLogViewerCommand()
		{
			lock (mSync)
			{
				// try to enqueue the command into the shared memory queue
				if (EnqueueClearLogViewerCommand(false))
					return true;

				// the shared memory queue is full
				// => abort, if the peak buffer queue is also full
				if (mPeakBufferQueue.Count >= mPeakBufferCapacity)
					return false;

				// there is space in the peak buffer queue
				// => put the command into the queue to send it later
				return EnqueueClearLogViewerCommand(true);
			}
		}

		/// <summary>
		/// Enqueues a command telling the log viewer to clear its view.
		/// </summary>
		/// <param name="defer">
		/// true to put the command into the peak buffer queue;
		/// false to put the command directly into the shared memory queue.
		/// </param>
		/// <returns>
		/// true, if the command was successfully enqueued;
		/// false, if the queue is full and lossless mode is disabled.
		/// </returns>
		private bool EnqueueClearLogViewerCommand(bool defer)
		{
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
						block->ClearLogViewer.Timestamp = Log.GetTimestamp().ToUniversalTime().ToFileTime();
						block->ClearLogViewer.HighPrecisionTimestamp = (Log.GetHighPrecisionTimestamp() + 500) / 1000; // ns => µs
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
		/// true, if the command was successfully enqueued;
		/// false, if the queue is full and lossless mode is disabled.
		/// </returns>
		public bool EnqueueSaveSnapshotCommand()
		{
			lock (mSync)
			{
				// try to enqueue the command into the shared memory queue
				if (EnqueueSaveSnapshotCommand(false))
					return true;

				// the shared memory queue is full
				// => abort, if the peak buffer queue is also full
				if (mPeakBufferQueue.Count >= mPeakBufferCapacity)
					return false;

				// there is space in the peak buffer queue
				// => put the command into the queue to send it later
				return EnqueueSaveSnapshotCommand(true);
			}
		}

		/// <summary>
		/// Enqueues a command telling the local log service to save a snapshot of the current log.
		/// </summary>
		/// <param name="defer">
		/// true to put the command into the peak buffer queue;
		/// false to put the command directly into the shared memory queue.
		/// </param>
		/// <returns>
		/// true, if the command was successfully enqueued;
		/// false, if the queue is full and lossless mode is disabled.
		/// </returns>
		private bool EnqueueSaveSnapshotCommand(bool defer)
		{
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

						// prepare command and send it
						block->Type = LogEntryBlockType.SaveSnapshot;
						block->SaveSnapshot.Timestamp = Log.GetTimestamp().ToUniversalTime().ToFileTime();
						block->SaveSnapshot.HighPrecisionTimestamp = (Log.GetHighPrecisionTimestamp() + 500) / 1000; // ns => µs
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
		}

		/// <summary>
		/// Gets a free block from the shared memory queue.
		/// </summary>
		/// <param name="sendDeferredItems">
		/// true to send deferred items, if any;
		/// false to skip sending deferred items (only for use within <see cref="GetLogEntryBlock"/>).
		/// </param>
		/// <returns>
		/// A free log entry block;
		/// null, if the queue does not contain a free block.
		/// </returns>
		/// <remarks>
		/// This method returns a free block from the log entry queue. If the queue does not contain any
		/// free blocks, it tries to get a block after a certain time (<see cref="QueueBlockFetchRetryDelayTime"/>).
		/// </remarks>
		private LogEntryBlock* GetLogEntryBlock(bool sendDeferredItems = true)
		{
			// before returning a free block to the caller all deferred blocks have to be passed to ensure that the
			// messages/commands/notifications arrive in the correct order
			if (sendDeferredItems && !SendDeferredItems())
				return null;

			while (true)
			{
				// try to get a free block from the queue
				// (suppress the overflow indication, if explicitly specified and on the first run only to avoid counting the condition multiple times)
				LogEntryBlock* pBlock = (LogEntryBlock*) mSharedMemoryQueue.BeginWriting();
				if (pBlock != null) return pBlock;

				// no free block in the queue
				// => find out what's going wrong...
				if (!IsLogSinkAlive())
				{
					// local log service has died
					// => initiate shutdown
					ShutdownConnection();
					if (mInitialized && mAutoReconnect) StartAutoReconnectTask();
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
		/// true, if all items have been sent successfully;
		/// false, if the queue is full or not initialized.
		/// </returns>
		private bool SendDeferredItems()
		{
			Debug.Assert(Monitor.IsEntered(mSync));

			// abort, if the shared memory queue is not initialized
			if (!mSharedMemoryQueue.IsInitialized)
				return false;

			while (mPeakBufferQueue.Count > 0)
			{
				var block = mPeakBufferQueue.Peek();

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
		/// true, if the message block and all extension blocks have been sent successfully;
		/// false, if the queue is full.
		/// </returns>
		private bool SendDeferredItems_Message(int extensionMessageCount)
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
			mSharedMemoryQueue.EndWritingSequence((void **)blocks, blockSizes, requiredBlockCount, mLostMessageCount);
			mLostMessageCount = 0;
			return true;
		}

		/// <summary>
		/// Tries to transfer the current block from the peak buffer queue to the shared memory queue.
		/// </summary>
		/// <returns>
		/// true, if the block has been sent successfully;
		/// false, if the queue is full.
		/// </returns>
		private bool SendDeferredItems_SingleBlock()
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
	}
}
