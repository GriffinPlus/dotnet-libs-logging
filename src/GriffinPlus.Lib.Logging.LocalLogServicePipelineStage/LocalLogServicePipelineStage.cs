///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading;

using GriffinPlus.Lib.Threading;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// A log message processing pipeline stage that forwards log message to the local log service (proprietary, windows only).
/// </summary>
public class LocalLogServicePipelineStage : SyncProcessingPipelineStage
{
	private LocalLogServiceConnection mConnection;
	private string                    mKernelObjectPrefix = "Griffin+";

	/// <summary>
	/// Initializes a new instance of the <see cref="LocalLogServicePipelineStage"/> class.
	/// </summary>
	public LocalLogServicePipelineStage()
	{
		mConnection = new LocalLogServiceConnection(KernelObjectPrefix);
	}

	/// <summary>
	/// Gets or sets the prefix for kernel objects created along with the connection
	/// (helps to create a kind of namespace to differentiate instances of the local log service)
	/// </summary>
	public string KernelObjectPrefix
	{
		get => mKernelObjectPrefix;
		set
		{
			if (value == null) throw new ArgumentNullException(nameof(value));

			lock (Sync)
			{
				EnsureNotAttachedToLoggingSubsystem();

				if (mKernelObjectPrefix != value)
				{
					mKernelObjectPrefix = value;

					// create a new log service connection
					mConnection?.ShutdownAsync().WaitWithoutException();
					mConnection = null;
					mConnection = new LocalLogServiceConnection(mKernelObjectPrefix);
				}
			}
		}
	}

	/// <summary>
	/// Gets or sets the interval between two attempts to re-establish the connection to the local log service.
	/// </summary>
	public TimeSpan AutoConnectRetryInterval
	{
		get => mConnection.AutoReconnectRetryInterval;
		set => mConnection.AutoReconnectRetryInterval = value;
	}

	/// <summary>
	/// Gets or sets a value indicating whether the lossless mode is enabled or disabled.
	/// </summary>
	public bool LosslessMode
	{
		get => mConnection.LosslessMode;
		set => mConnection.LosslessMode = value;
	}

	/// <summary>
	/// Gets or sets the capacity of the queue buffering data blocks that would have been sent to the local
	/// log service, but could not, because the shared memory queue was full. This can happen in case of severe
	/// load peaks. Peak buffering is in effect, if <see cref="LosslessMode"/> is <c>false</c>. Set the capacity
	/// to 0 to disable peak buffering messages (notifications are always buffered to avoid getting out of sync).
	/// </summary>
	public int PeakBufferCapacity
	{
		get => mConnection.PeakBufferCapacity;
		set => mConnection.PeakBufferCapacity = value;
	}

	/// <summary>
	/// Gets or sets a value indicating whether log messages are persistently stored in the local log service.
	/// </summary>
	public bool WriteToLogFile
	{
		get => mConnection.WriteToLogFile;
		set => mConnection.WriteToLogFile = value;
	}

	/// <summary>
	/// Gets a value indicating whether the local log service is connected and alive.
	/// </summary>
	public bool IsConnectedAndAlive => mConnection.IsLogSinkAlive();

	/// <summary>
	/// Sends a command telling the log viewer to clear its view.
	/// </summary>
	/// <returns>
	/// <c>true</c> if the command was successfully sent;<br/>
	/// otherwise <c>false</c>.
	/// </returns>
	public bool ClearLogViewer()
	{
		return mConnection.EnqueueClearLogViewerCommand();
	}

	/// <summary>
	/// Sends a command telling the local log service to save a snapshot of the current log.
	/// </summary>
	/// <returns>
	/// <c>true</c> if the command was successfully sent;<br/>
	/// otherwise <c>false</c>.
	/// </returns>
	public bool SaveSnapshot()
	{
		return mConnection.EnqueueSaveSnapshotCommand();
	}

	/// <summary>
	/// Initializes the pipeline stage when the stage is attached to the logging subsystem.
	/// </summary>
	protected override void OnInitialize()
	{
		mConnection.InitializeAsync(CancellationToken.None).WaitAndUnwrapException();
	}

	/// <summary>
	/// Shuts the pipeline stage down when the stage is detached from the logging system.
	/// </summary>
	protected override void OnShutdown()
	{
		mConnection.ShutdownAsync().WaitWithoutException();
	}

	/// <summary>
	/// Processes a log message synchronously.
	/// </summary>
	/// <param name="message">Message to process.</param>
	/// <returns>
	/// <c>true</c> to continue processing (pass message to the following stages);<br/>
	/// <c>false</c> to stop processing.
	/// </returns>
	protected override bool ProcessSync(LocalLogMessage message)
	{
		mConnection.EnqueueMessage(message);
		return true;
	}

	/// <summary>
	/// Is called when a new log level was added to the logging subsystem.
	/// </summary>
	/// <param name="level">The new log level.</param>
	protected override void OnLogLevelAdded(LogLevel level)
	{
		mConnection.EnqueueLogLevelAddedNotification(level);
	}

	/// <summary>
	/// Is called when a new log writer was added to the logging subsystem.
	/// </summary>
	/// <param name="writer">The new log writer.</param>
	protected override void OnLogWriterAdded(LogWriter writer)
	{
		mConnection.EnqueueLogWriterAddedNotification(writer);
	}
}
