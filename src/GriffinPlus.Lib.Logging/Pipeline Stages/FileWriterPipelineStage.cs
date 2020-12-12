///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A log message processing pipeline stage that logs messages to a file (thread-safe).
	/// </summary>
	public class FileWriterPipelineStage : TextWriterPipelineStage<FileWriterPipelineStage>
	{
		private readonly StringBuilder mOutputBuilder = new StringBuilder();
		private readonly string        mPath;
		private readonly bool          mAppend;
		private          FileStream    mFile;
		private          StreamWriter  mWriter;
		private          bool          mAutoFlush;


		/// <summary>
		/// Initializes a new instance of the <see cref="FileWriterPipelineStage" /> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <param name="path">Path of the file to write to.</param>
		/// <param name="append">
		/// true to append new messages to the specified file, if it exists already;
		/// false to truncate the file and start from scratch.
		/// </param>
		public FileWriterPipelineStage(string name, string path, bool append) : base(name)
		{
			mPath = path;
			mAppend = append;
		}

		/// <summary>
		/// Gets or sets a value indicating whether the file is flushed every time after a message is written.
		/// </summary>
		public bool AutoFlush
		{
			get
			{
				lock (Sync) return mAutoFlush;
			}

			set
			{
				lock (Sync)
				{
					EnsureNotAttachedToLoggingSubsystem();
					mAutoFlush = value;
				}
			}
		}

		/// <summary>
		/// Gets the path of the log file.
		/// </summary>
		public string Path
		{
			get
			{
				lock (Sync) return mPath;
			}
		}

		/// <summary>
		/// Performs pipeline stage specific initialization tasks that must run when the pipeline stage is attached to the
		/// logging subsystem. This method is called from within the pipeline stage lock (<see cref="ProcessingPipelineBaseStage.Sync" />).
		/// </summary>
		protected override void OnInitialize()
		{
			base.OnInitialize();

			mFile = new FileStream(mPath, mAppend ? FileMode.OpenOrCreate : FileMode.Create, FileAccess.Write, FileShare.Read);
			mWriter = new StreamWriter(mFile, Encoding.UTF8);
		}

		/// <summary>
		/// Performs pipeline stage specific cleanup tasks that must run when the pipeline stage is about to be detached
		/// from the logging subsystem. This method is called from within the pipeline stage lock (<see cref="ProcessingPipelineBaseStage.Sync" />).
		/// </summary>
		protected internal override void OnShutdown()
		{
			base.OnShutdown();

			mWriter?.Dispose();
			mWriter = null;
			mFile = null;
		}

		/// <summary>
		/// Emits the formatted log messages (should not throw any exceptions).
		/// </summary>
		/// <param name="messages">The formatted log messages.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		/// <returns>Number of successfully written log messages.</returns>
		protected override async Task<int> EmitOutputAsync(FormattedMessage[] messages, CancellationToken cancellationToken)
		{
			mOutputBuilder.Clear();
			for (int i = 0; i < messages.Length; i++)
			{
				mOutputBuilder.Append(messages[i].Output);
				mOutputBuilder.AppendLine();
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
				await mWriter.WriteAsync(mOutputBuilder.ToString()).ConfigureAwait(false);
				await mWriter.FlushAsync().ConfigureAwait(false);
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

			return messages.Length;
		}
	}

}
