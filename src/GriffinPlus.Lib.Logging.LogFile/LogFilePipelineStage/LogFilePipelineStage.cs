///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A pipeline stage that writes log messages to a <see cref="LogFile"/>.
	/// </summary>
	public class LogFilePipelineStage : AsyncProcessingPipelineStage
	{
		private          LogFile          mLogFile;
		private readonly string           mFilePath;
		private readonly LogFilePurpose   mPurpose;
		private readonly LogFileWriteMode mWriteMode;
		private          long             mMaximumMessageCount = -1;
		private          TimeSpan         mMaximumMessageAge   = TimeSpan.Zero;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogFilePipelineStage"/> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <param name="path">Log file to open/create.</param>
		/// <param name="purpose">
		/// Purpose of the log file determining whether the log file is primarily used for recording or for analysis
		/// (does not have any effect, if the log file exists already).
		/// </param>
		/// <param name="writeMode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
		public LogFilePipelineStage(
			string           name,
			string           path,
			LogFilePurpose   purpose,
			LogFileWriteMode writeMode) : base(name)
		{
			mFilePath = path;
			mPurpose = purpose;
			mWriteMode = writeMode;
		}

		/// <summary>
		/// Performs pipeline stage specific initialization tasks that must run when the pipeline stage is attached to the
		/// logging subsystem. This method is called from within the pipeline stage lock (<see cref="ProcessingPipelineStage.Sync"/>).
		/// </summary>
		protected override void OnInitialize()
		{
			Debug.Assert(mLogFile == null);
			mLogFile = LogFile.OpenOrCreate(mFilePath, mPurpose, mWriteMode);
		}

		/// <summary>
		/// Performs pipeline stage specific cleanup tasks that must run when the pipeline stage is about to be detached from
		/// the logging subsystem. This method is called from within the pipeline stage lock (<see cref="ProcessingPipelineStage.Sync"/>).
		/// This method must not throw exceptions.
		/// </summary>
		protected override void OnShutdown()
		{
			if (mLogFile != null)
			{
				mLogFile.Dispose();
				mLogFile = null;
			}
		}

		/// <summary>
		/// Gets or sets the maximum number of messages to keep
		/// (negative to disable removing messages by maximum message count).
		/// </summary>
		public long MaximumMessageCount
		{
			get
			{
				lock (Sync)
				{
					return mMaximumMessageCount;
				}
			}

			set
			{
				if (value < -1)
				{
					throw new ArgumentOutOfRangeException(
						nameof(value),
						"The maximum message count must be > 0 to limit the number of messages or -1 to disable the limit.");
				}

				lock (Sync)
				{
					mMaximumMessageCount = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the maximum age of messages to keep before removing them from the log file
		/// (use <see cref="TimeSpan.Zero"/> or negative time span to disable removing by age).
		/// </summary>
		public TimeSpan MaximumMessageAge
		{
			get
			{
				lock (Sync)
				{
					return mMaximumMessageAge;
				}
			}

			set
			{
				lock (Sync)
				{
					mMaximumMessageAge = value;
				}
			}
		}

		/// <summary>
		/// Processes the specified log messages asynchronously
		/// (the method is executed by the stage's processing thread, do not use <c>ConfigureAwait(false)</c> to resume
		/// execution in the processing thread when awaiting a task).
		/// </summary>
		/// <param name="messages">Messages to process.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		protected override Task ProcessAsync(LocalLogMessage[] messages, CancellationToken cancellationToken)
		{
			// write messages to the file
			try
			{
				mLogFile.Write(messages);
			}
			catch (Exception ex)
			{
				WritePipelineError($"Writing log file ({mLogFile.FilePath}) failed.", ex);
			}

			// prune log file, if necessary
			try
			{
				// get pruning parameters
				long maximumMessageCount;
				TimeSpan maximumMessageAge;
				lock (Sync)
				{
					maximumMessageCount = mMaximumMessageCount;
					maximumMessageAge = mMaximumMessageAge;
				}

				// prune messages exceeding the limits
				if (maximumMessageCount > 0 || maximumMessageAge > TimeSpan.Zero)
				{
					mLogFile.Prune(
						maximumMessageCount,
						DateTime.UtcNow - maximumMessageAge,
						false);
				}
			}
			catch (Exception ex)
			{
				WritePipelineError($"Pruning log file ({mLogFile.FilePath}) failed.", ex);
			}

			return Task.CompletedTask;
		}
	}

}
