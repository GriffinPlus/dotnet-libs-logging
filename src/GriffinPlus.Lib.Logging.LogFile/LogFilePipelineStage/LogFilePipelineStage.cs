///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading;
using System.Threading.Tasks;

using GriffinPlus.Lib.Threading;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A pipeline stage that writes log messages to a <see cref="LogFile"/>.
	/// </summary>
	public class LogFilePipelineStage : AsyncProcessingPipelineStage
	{
		private readonly AsyncLock mAsyncWriterLock = new AsyncLock();
		private          LogFile   mLogFile;

		// defaults of settings determining the behavior of the stage
		private static readonly string           sDefault_Path                = "Unnamed.log";
		private static readonly LogFilePurpose   sDefault_Purpose             = LogFilePurpose.Recording;
		private static readonly LogFileWriteMode sDefault_WriteMode           = LogFileWriteMode.Robust;
		private static readonly long             sDefault_MaximumMessageCount = -1;
		private static readonly TimeSpan         sDefault_MaximumMessageAge   = TimeSpan.Zero;

		// the settings determining the behavior of the stage
		private readonly IProcessingPipelineStageSetting<string>           mSetting_Path;
		private readonly IProcessingPipelineStageSetting<LogFilePurpose>   mSetting_Purpose;
		private readonly IProcessingPipelineStageSetting<LogFileWriteMode> mSetting_WriteMode;
		private readonly IProcessingPipelineStageSetting<long>             mSetting_MaximumMessageCount;
		private readonly IProcessingPipelineStageSetting<TimeSpan>         mSetting_MaximumMessageAge;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogFilePipelineStage"/> class.
		/// </summary>
		public LogFilePipelineStage()
		{
			mSetting_Path = RegisterSetting("Path", sDefault_Path);
			mSetting_Path.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_Purpose = RegisterSetting("Purpose", sDefault_Purpose);
			mSetting_Purpose.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_WriteMode = RegisterSetting("WriteMode", sDefault_WriteMode);
			mSetting_WriteMode.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_MaximumMessageCount = RegisterSetting("MaximumMessageCount", sDefault_MaximumMessageCount);
			mSetting_MaximumMessageCount.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_MaximumMessageAge = RegisterSetting("MaximumMessageAge", sDefault_MaximumMessageAge);
			mSetting_MaximumMessageAge.RegisterSettingChangedEventHandler(OnSettingChanged, false);
		}

		/// <summary>
		/// Gets or sets the path of the log file to open/create.
		/// </summary>
		public string Path
		{
			get => mSetting_Path.Value;
			set => mSetting_Path.Value = value;
		}

		/// <summary>
		/// Gets or sets the purpose of the log file determining whether the log file is primarily used for recording (default) or for analysis
		/// (does not have any effect, if the log file exists already).
		/// </summary>
		public LogFilePurpose Purpose
		{
			get => mSetting_Purpose.Value;
			set => mSetting_Purpose.Value = value;
		}

		/// <summary>
		/// Gets or sets the write mode determining whether to open the log file in 'robust' (default) or 'fast' mode.
		/// </summary>
		public LogFileWriteMode WriteMode
		{
			get => mSetting_WriteMode.Value;
			set => mSetting_WriteMode.Value = value;
		}

		/// <summary>
		/// Gets or sets the maximum number of messages to keep
		/// (negative to disable removing messages by maximum message count).
		/// </summary>
		public long MaximumMessageCount
		{
			get => mSetting_MaximumMessageCount.Value;
			set => mSetting_MaximumMessageCount.Value = value;
		}

		/// <summary>
		/// Gets or sets the maximum age of messages to keep before removing them from the log file
		/// (use <see cref="TimeSpan.Zero"/> or negative time span to disable removing by age).
		/// </summary>
		public TimeSpan MaximumMessageAge
		{
			get => mSetting_MaximumMessageAge.Value;
			set => mSetting_MaximumMessageAge.Value = value;
		}

		/// <summary>
		/// Performs pipeline stage specific initialization tasks that must run when the pipeline stage is attached to the
		/// logging subsystem. This method is called from within the pipeline stage lock (<see cref="ProcessingPipelineStage.Sync"/>).
		/// </summary>
		protected override void OnInitialize()
		{
			base.OnInitialize();
			TryOpenLogFile();
		}

		/// <summary>
		/// Performs pipeline stage specific cleanup tasks that must run when the pipeline stage is about to be detached from
		/// the logging subsystem. This method is called from within the pipeline stage lock (<see cref="ProcessingPipelineStage.Sync"/>).
		/// This method must not throw exceptions.
		/// </summary>
		protected override void OnShutdown()
		{
			base.OnShutdown();
			CloseLogFile();
		}

		/// <summary>
		/// Processes the specified log messages asynchronously
		/// (the method is executed by the stage's processing thread, do not use <c>ConfigureAwait(false)</c> to resume
		/// execution in the processing thread when awaiting a task).
		/// </summary>
		/// <param name="messages">Messages to process.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		protected override async Task ProcessAsync(LocalLogMessage[] messages, CancellationToken cancellationToken)
		{
			using (await mAsyncWriterLock.LockAsync(cancellationToken))
			{
				// abort, if the log file is not opened
				if (mLogFile == null)
					return;

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
					// prune messages exceeding the limits
					long maximumMessageCount = MaximumMessageCount;
					TimeSpan maximumMessageAge = MaximumMessageAge;
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
			}
		}

		/// <summary>
		/// Opens the log file as specified by the <see cref="Path"/> property.
		/// </summary>
		/// <returns>
		/// <c>true</c> if the file was opened successfully; otherwise <c>false</c>.
		/// </returns>
		private bool TryOpenLogFile()
		{
			using (mAsyncWriterLock.Lock())
			{
				// close the currently opened log file
				try
				{
					mLogFile?.Dispose();
				}
				catch (Exception ex)
				{
					WritePipelineError("Closing log file failed.", ex);
				}
				finally
				{
					mLogFile = null;
				}

				// open/create new log file
				// (always interpret relative paths relative to the application base directory, not the working directory)
				try
				{
					string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path));
					mLogFile = LogFile.OpenOrCreate(path, Purpose, WriteMode);
					return true;
				}
				catch (Exception ex)
				{
					WritePipelineError($"Opening log file ({Path}) failed.", ex);
				}
			}

			return false;
		}

		/// <summary>
		/// Closes the opened log file.
		/// </summary>
		private void CloseLogFile()
		{
			using (mAsyncWriterLock.Lock())
			{
				try
				{
					mLogFile?.Dispose();
				}
				catch (Exception ex)
				{
					WritePipelineError("Closing log file failed.", ex);
				}

				mLogFile = null;
			}
		}

		/// <summary>
		/// Is called by a worker thread when the configuration changes.
		/// </summary>
		private void OnSettingChanged(object sender, SettingChangedEventArgs e)
		{
			TryOpenLogFile();
		}
	}

}
