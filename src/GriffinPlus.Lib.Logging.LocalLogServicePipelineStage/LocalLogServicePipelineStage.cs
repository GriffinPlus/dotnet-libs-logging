///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message processing pipeline stage that forwards log message to the local log service (proprietary, windows only).
	/// </summary>
	public class LocalLogServicePipelineStage : ProcessingPipelineStage<LocalLogServicePipelineStage>
	{
		private readonly LocalLogServiceConnection mSource;

		/// <summary>
		/// Initializes a new instance of the <see cref="LocalLogServicePipelineStage"/> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <param name="prefix">
		/// Prefix for kernel objects created along with the connection
		/// (helps to create a kind of namespace to differentiate instances of the local log service)
		/// </param>
		public LocalLogServicePipelineStage(string name, string prefix = "Griffin+") : base(name)
		{
			mSource = new LocalLogServiceConnection(prefix);
		}

		/// <summary>
		/// Gets or sets a value indicating whether the connection is re-established after breaking down
		/// (most probably due to the local log service shutting down or restarting).
		/// </summary>
		public bool AutoConnect
		{
			get => mSource.AutoReconnect;
			set => mSource.AutoReconnect = value;
		}

		/// <summary>
		/// Gets or sets the interval between two attempts to re-establish the connection to the local log service.
		/// Requires <see cref="AutoConnect"/> to be set to <c>true</c>.
		/// </summary>
		public TimeSpan AutoConnectRetryInterval
		{
			get => mSource.AutoReconnectRetryInterval;
			set => mSource.AutoReconnectRetryInterval = value;
		}

		/// <summary>
		/// Gets or sets a value indicating whether the lossless mode is enabled or disabled.
		/// </summary>
		public bool LosslessMode
		{
			get => mSource.LosslessMode;
			set => mSource.LosslessMode = value;
		}

		/// <summary>
		/// Gets or sets the capacity of the queue buffering data blocks that would have been sent to the local
		/// log service, but could not, because the shared memory queue was full. This can happen in case of severe
		/// load peaks. Peak buffering is in effect, if <see cref="LosslessMode"/> is <c>false</c>. Set the capacity
		/// to 0 to disable peak buffering messages (notifications are always buffered to avoid getting out of sync).
		/// </summary>
		public int PeakBufferCapacity
		{
			get => mSource.PeakBufferCapacity;
			set => mSource.PeakBufferCapacity = value;
		}

		/// <summary>
		/// Gets or sets a value indicating whether log messages are persistently stored in the local log service.
		/// </summary>
		public bool WriteToLogFile
		{
			get => mSource.WriteToLogFile;
			set => mSource.WriteToLogFile = value;
		}

		/// <summary>
		/// Gets a value indicating whether the local log service is connected and alive.
		/// </summary>
		public bool IsConnectedAndAlive => mSource.IsLogSinkAlive();

		/// <summary>
		/// Sends a command telling the log viewer to clear its view.
		/// </summary>
		/// <returns>
		/// true, if the command was successfully sent;
		/// otherwise false.
		/// </returns>
		public bool ClearLogViewer()
		{
			return mSource.EnqueueClearLogViewerCommand();
		}

		/// <summary>
		/// Sends a command telling the local log service to save a snapshot of the current log.
		/// </summary>
		/// <returns>
		/// true, if the command was successfully sent;
		/// otherwise false.
		/// </returns>
		public bool SaveSnapshot()
		{
			return mSource.EnqueueSaveSnapshotCommand();
		}

		/// <summary>
		/// Initializes the pipeline stage when the stage is attached to the logging subsystem.
		/// </summary>
		protected override void OnInitialize()
		{
			mSource.Initialize();
		}

		/// <summary>
		/// Shuts the pipeline stage down when the stage is detached from the logging system.
		/// </summary>
		protected override void OnShutdown()
		{
			mSource.Shutdown();
		}

		/// <summary>
		/// Processes a log message synchronously.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <returns>
		/// true to continue processing (pass message to the following stages);
		/// false to stop processing.
		/// </returns>
		protected override bool ProcessSync(LocalLogMessage message)
		{
			mSource.EnqueueMessage(message);
			return true;
		}

		/// <summary>
		/// Is called when a new log level was added to the logging subsystem.
		/// </summary>
		/// <param name="level">The new log level.</param>
		protected override void OnLogLevelAdded(LogLevel level)
		{
			mSource.EnqueueLogLevelAddedNotification(level);
		}

		/// <summary>
		/// Is called when a new log writer was added to the logging subsystem.
		/// </summary>
		/// <param name="writer">The new log writer.</param>
		protected override void OnLogWriterAdded(LogWriter writer)
		{
			mSource.EnqueueLogWriterAddedNotification(writer);
		}
	}
}
