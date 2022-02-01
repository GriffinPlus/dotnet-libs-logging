///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using GriffinPlus.Lib.Threading;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A log message processing pipeline stage that logs messages to a file (thread-safe).
	/// </summary>
	public class FileWriterPipelineStage : TextWriterPipelineStage
	{
		private readonly StringBuilder mOutputBuilder   = new StringBuilder();
		private readonly AsyncLock     mAsyncWriterLock = new AsyncLock();
		private          FileStream    mFile;
		private          StreamWriter  mWriter;
		private          string        mOpenedFilePath;

		// defaults of settings determining the behavior of the stage
		private const bool   Default_Append = false;
		private const string Default_Path   = "Unnamed.log";

		// the settings determining the behavior of the stage
		private readonly IProcessingPipelineStageSetting<bool>   mSetting_Append;
		private readonly IProcessingPipelineStageSetting<string> mSetting_Path;

		/// <summary>
		/// Initializes a new instance of the <see cref="FileWriterPipelineStage"/> class.
		/// </summary>
		public FileWriterPipelineStage()
		{
			mSetting_Append = RegisterSetting("Append", Default_Append);
			mSetting_Path = RegisterSetting("Path", Default_Path);
		}

		/// <summary>
		/// Gets or sets a value determining whether log messages are appended to an existing log file.
		/// (<c>true</c> to append new log messages to an existing log file (default),
		/// <c>false</c> to truncate the log file before writing the first message).
		/// </summary>
		public bool Append
		{
			get => mSetting_Append.Value;
			set => mSetting_Append.Value = value;
		}

		/// <summary>
		/// Gets or sets the path of the log file (default: 'Unnamed.log').
		/// </summary>
		public string Path
		{
			get => mSetting_Path.Value;
			set => mSetting_Path.Value = value;
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
		/// Performs pipeline stage specific cleanup tasks that must run when the pipeline stage is about to be detached
		/// from the logging subsystem. This method is called from within the pipeline stage lock (<see cref="ProcessingPipelineStage.Sync"/>).
		/// </summary>
		protected internal override void OnShutdown()
		{
			base.OnShutdown();
			CloseLogFile();
		}

		/// <summary>
		/// Processes pending changes to registered setting proxies
		/// (the method is executed by the stage's processing thread, do not use <c>ConfigureAwait(false)</c> to resume
		/// execution in the processing thread when awaiting a task).
		/// </summary>
		/// <param name="settings">Settings that have changed.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		protected override Task OnSettingsChangedAsync(IUntypedProcessingPipelineStageSetting[] settings, CancellationToken cancellationToken)
		{
			TryOpenLogFile();
			return Task.CompletedTask;
		}

		/// <summary>
		/// Emits the formatted log messages (should not throw any exceptions).
		/// The method is executed by the stage's processing thread, do not use <c>ConfigureAwait(false)</c> to resume
		/// execution in the processing thread when awaiting a task.
		/// </summary>
		/// <param name="messages">The formatted log messages.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		/// <returns>Number of successfully written log messages.</returns>
		protected override async Task<int> EmitOutputAsync(FormattedMessage[] messages, CancellationToken cancellationToken)
		{
			using (await mAsyncWriterLock.LockAsync(cancellationToken))
			{
				// abort, if the log file has not been opened
				if (mWriter == null)
					return 0;

				// put all formatted messages into a single string to speed up writing them in the next step
				mOutputBuilder.Clear();
				foreach (var message in messages)
				{
					mOutputBuilder.AppendLine(message.Output);
				}

				// get the current stream position
				long position;
				try
				{
					position = mFile.Position;
				}
				catch
				{
					// swallow exceptions
					// (i/o errors should not impact the application)
					return 0;
				}

				// write to the file and flush it to ensure that all data is passed to the operating system
				try
				{
					await mWriter.WriteAsync(mOutputBuilder.ToString());
					await mWriter.FlushAsync();
				}
				catch
				{
					// writing failed
					// => try remove partially written messages to avoid generating duplicates when trying again
					try
					{
						mFile.SetLength(position);
					}
					catch
					{
						// swallow exceptions
						// (i/o errors should not impact the application)
					}

					// return that no messages have been written at all
					return 0;
				}
			}

			return messages.Length;
		}

		/// <summary>
		/// Opens the log file as specified by the <see cref="Path"/> property.
		/// If the opened file has not changed, it is not re-opened.
		/// </summary>
		/// <returns>
		/// <c>true</c> if the file was opened successfully; otherwise <c>false</c>.
		/// </returns>
		private bool TryOpenLogFile()
		{
			using (mAsyncWriterLock.Lock())
			{
				// determine the full path of the file to open
				// (always interpret relative paths relative to the application base directory, not the working directory)
				string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path));

				// abort, if the opened file has not changed
				if (mOpenedFilePath == path)
					return false;

				// the opened file has changed

				// close the currently opened log file
				try
				{
					mWriter?.Close();
				}
				catch (Exception ex)
				{
					WritePipelineError("Closing log file failed.", ex);
				}
				finally
				{
					mWriter = null;
					mFile = null;
				}

				// open/create new log file
				try
				{
					mFile = new FileStream(path, Append ? FileMode.OpenOrCreate : FileMode.Create, FileAccess.Write, FileShare.Read);
					if (Append) mFile.Position = mFile.Length;
					mWriter = new StreamWriter(mFile, Encoding.UTF8);
					mOpenedFilePath = path;
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
					mWriter?.Close();
				}
				catch (Exception ex)
				{
					WritePipelineError("Closing log file failed.", ex);
				}
				finally
				{
					mOpenedFilePath = null;
					mWriter = null;
					mFile = null;
				}
			}
		}
	}

}
