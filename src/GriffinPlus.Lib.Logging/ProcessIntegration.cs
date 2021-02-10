///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using GriffinPlus.Lib.Events;

// ReSharper disable ForCanBeConvertedToForeach

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A class that assists with redirecting output/error streams into the Griffin+ logging subsystem.
	/// </summary>
	public partial class ProcessIntegration
	{
		private readonly JsonMessageReader mOutputMessageReader = new JsonMessageReader();
		private readonly JsonMessageReader mErrorMessageReader  = new JsonMessageReader();
		private          bool              mSyncingOutputMessageReader;
		private          bool              mSyncingErrorMessageReader;

		/// <summary>
		/// Occurs when the integrated process has written a line to its output stream.
		/// If the thread registering the event has a synchronization context, the event handler is invoked in the context of that thread.
		/// The event handler receives an event argument with <see cref="LineReceivedEventArgs.Line"/> set to <c>null</c> at the end of the stream
		/// (when the process exits).
		/// </summary>
		public event EventHandler<LineReceivedEventArgs> OutputStreamReceivedText
		{
			add => EventManager<LineReceivedEventArgs>.RegisterEventHandler(
				this,
				nameof(OutputStreamReceivedText),
				value,
				SynchronizationContext.Current,
				false);

			remove => EventManager<LineReceivedEventArgs>.UnregisterEventHandler(
				this,
				nameof(OutputStreamReceivedText),
				value);
		}

		/// <summary>
		/// Occurs when the integrated process has completed writing a JSON log message to its output stream.
		/// If the thread registering the event has a synchronization context, the event handler is invoked in the context of that thread.
		/// The event handler receives an event argument with <see cref="MessageReceivedEventArgs.Message"/> set to <c>null</c> at the end of the stream
		/// (when the process exits).
		/// </summary>
		public event EventHandler<MessageReceivedEventArgs> OutputStreamReceivedMessage
		{
			add => EventManager<MessageReceivedEventArgs>.RegisterEventHandler(
				this,
				nameof(OutputStreamReceivedMessage),
				value,
				SynchronizationContext.Current,
				false);

			remove => EventManager<MessageReceivedEventArgs>.UnregisterEventHandler(
				this,
				nameof(OutputStreamReceivedMessage),
				value);
		}

		/// <summary>
		/// Occurs when the integrated process has written a line to its error stream.
		/// If the thread registering the event has a synchronization context, the event handler is invoked in the context of that thread.
		/// The event handler receives an event argument with <see cref="LineReceivedEventArgs.Line"/> set to <c>null</c> at the end of the stream
		/// (when the process exits).
		/// </summary>
		public event EventHandler<LineReceivedEventArgs> ErrorStreamReceivedText
		{
			add => EventManager<LineReceivedEventArgs>.RegisterEventHandler(
				this,
				nameof(ErrorStreamReceivedText),
				value,
				SynchronizationContext.Current,
				false);

			remove => EventManager<LineReceivedEventArgs>.UnregisterEventHandler(
				this,
				nameof(ErrorStreamReceivedText),
				value);
		}

		/// <summary>
		/// Occurs when the integrated process has completed writing a JSON log message to its error stream.
		/// If the thread registering the event has a synchronization context, the event handler is invoked in the context of that thread.
		/// The event handler receives an event argument with <see cref="MessageReceivedEventArgs.Message"/> set to <c>null</c> at the end of the stream
		/// (when the process exits).
		/// </summary>
		public event EventHandler<MessageReceivedEventArgs> ErrorStreamReceivedMessage
		{
			add => EventManager<MessageReceivedEventArgs>.RegisterEventHandler(
				this,
				nameof(ErrorStreamReceivedMessage),
				value,
				SynchronizationContext.Current,
				false);

			remove => EventManager<MessageReceivedEventArgs>.UnregisterEventHandler(
				this,
				nameof(ErrorStreamReceivedMessage),
				value);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessIntegration"/> class.
		/// </summary>
		/// <param name="process">The process.</param>
		/// <param name="logWriter">Log writer to use when logging received messages (may be null).</param>
		/// <exception cref="ArgumentNullException">The specified process is null.</exception>
		private ProcessIntegration(Process process, LogWriter logWriter)
		{
			Process = process ?? throw new ArgumentNullException(nameof(process));
			Process.StartInfo.UseShellExecute = false; // must be false to use i/o redirection
			Process.StartInfo.RedirectStandardInput = true;
			Process.StartInfo.RedirectStandardOutput = true;
			Process.StartInfo.RedirectStandardError = true;
			Process.ErrorDataReceived += ProcessErrorDataReceived;
			Process.OutputDataReceived += ProcessOutputDataReceived;

			if (logWriter != null)
			{
				LogWriter = logWriter;
			}
			else
			{
				string filename = process.StartInfo.FileName;
				string name = Path.GetFileName(filename);
				LogWriter = Log.GetWriter($"External Process ({name})");
			}
		}

		#region Integrating and Starting Process

		/// <summary>
		/// Gets the process.
		/// </summary>
		public Process Process { get; }

		/// <summary>
		/// Gets the log writer that is used to log received messages.
		/// </summary>
		public LogWriter LogWriter { get; }

		/// <summary>
		/// Gets or sets a value indicating whether log messages received via the standard output stream are logged
		/// (requires the started process to emit JSON formatted log messages).
		/// </summary>
		public bool IsLoggingMessagesEnabled { get; set; } = true;

		/// <summary>
		/// Configures the specified process to redirect its output/error streams to the logging subsystem.
		/// You can attach event handlers to <see cref="OutputStreamReceivedText"/> and <see cref="ErrorStreamReceivedText"/> to
		/// get notified as soon as the integrated process writes a line to its output/error stream. The process
		/// must be started using <see cref="StartProcess"/> to kick off reading from its output/error stream.
		/// </summary>
		/// <param name="process">Process to configure.</param>
		/// <param name="logWriter">Log writer to use when logging received messages (may be null).</param>
		public static ProcessIntegration IntegrateIntoLogging(Process process, LogWriter logWriter = null)
		{
			return new ProcessIntegration(process, logWriter);
		}

		/// <summary>
		/// Starts the integrated process and reading from its output/error streams.
		/// </summary>
		public void StartProcess()
		{
			Process.Start();
			Process.BeginOutputReadLine();
			Process.BeginErrorReadLine();
		}

		/// <summary>
		/// Waits for the process to exit.
		/// </summary>
		public void WaitForExit()
		{
			Process.WaitForExit();
		}

		/// <summary>
		/// Waits for the process to exit or the specified timeout.
		/// </summary>
		/// <param name="milliseconds">
		/// The amount of time, in milliseconds, to wait for the associated process to exit.
		/// The maximum is the largest possible value of a 32-bit integer, which represents infinity to the operating system.
		/// </param>
		/// <returns>
		/// <c>true</c> if the associated process has exited;
		/// otherwise, <c>false</c>.
		/// </returns>
		public bool WaitForExit(int milliseconds)
		{
			return Process.WaitForExit(milliseconds);
		}

		/// <summary>
		/// Waits asynchronously for the process to exit.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel waiting.</param>
		public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
		{
			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			void ProcessExited(object sender, EventArgs e)
			{
				tcs.TrySetResult(true);
			}

			try
			{
				Process.EnableRaisingEvents = true;
				Process.Exited += ProcessExited;

				if (Process.HasExited)
				{
					return;
				}

				using (cancellationToken.Register(() => tcs.TrySetCanceled()))
				{
					await tcs.Task.ConfigureAwait(false);
				}
			}
			finally
			{
				Process.Exited -= ProcessExited;
			}
		}

		#endregion

		#region Processing Stream Data

		/// <summary>
		/// Is called when a line was written to the standard output stream of the integrated process
		/// (this method is invoked by a worker thread).
		/// </summary>
		/// <param name="sender">The process that has written the line.</param>
		/// <param name="e">Event arguments containing the written line.</param>
		private void ProcessOutputDataReceived(object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				// notify event recipients interested in pure text
				OnOutputStreamReceivedText(e.Data);

				if (IsLoggingMessagesEnabled || EventManager<MessageReceivedEventArgs>.IsHandlerRegistered(this, nameof(OutputStreamReceivedMessage)))
				{
					try
					{
						var messages = mOutputMessageReader.Process(e.Data);
						for (int i = 0; i < messages.Length; i++)
						{
							var message = messages[i];
							if (IsLoggingMessagesEnabled) LogMessage(message, "output");
							OnOutputStreamReceivedMessage(message);
						}

						mSyncingOutputMessageReader = false;
					}
					catch (JsonMessageReaderException ex)
					{
						// invalid json or log message not in the expected format
						// => reset reader and emit surrogate message
						mOutputMessageReader.Reset();
						if (!mSyncingOutputMessageReader)
						{
							mSyncingOutputMessageReader = true;
							var message = new LogMessage { Text = $"Reading JSON formatted log message failed, trying to sync to the stream.\nException: {ex}" };
							OnOutputStreamReceivedMessage(message);
						}
					}
					catch (Exception ex)
					{
						// unexpected error
						var message = new LogMessage { Text = $"Unhandled exception reading JSON log message stream.\nException: {ex})." };
						OnOutputStreamReceivedMessage(message);
					}
				}
			}
			else
			{
				// the process has exited
				// => notify event recipients
				OnOutputStreamReceivedText(null);
				OnOutputStreamReceivedMessage(null);
			}
		}

		/// <summary>
		/// Is called when a line was written to the standard error stream of the integrated process
		/// (this method is invoked by a worker thread).
		/// </summary>
		/// <param name="sender">The process that has written the line.</param>
		/// <param name="e">Event arguments containing the written line.</param>
		private void ProcessErrorDataReceived(object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				// notify event recipients interested in pure text
				OnErrorStreamReceivedText(e.Data);

				if (IsLoggingMessagesEnabled || EventManager<MessageReceivedEventArgs>.IsHandlerRegistered(this, nameof(ErrorStreamReceivedMessage)))
				{
					try
					{
						var messages = mErrorMessageReader.Process(e.Data);
						for (int i = 0; i < messages.Length; i++)
						{
							var message = messages[i];
							if (IsLoggingMessagesEnabled) LogMessage(message, "error");
							OnErrorStreamReceivedMessage(message);
						}

						mSyncingErrorMessageReader = false;
					}
					catch (JsonMessageReaderException ex)
					{
						// invalid json or log message not in the expected format
						// => reset reader and emit surrogate message
						mErrorMessageReader.Reset();
						if (!mSyncingErrorMessageReader)
						{
							mSyncingErrorMessageReader = true;
							var message = new LogMessage { Text = $"Reading JSON formatted log message failed, trying to sync to the stream.\nException: {ex}" };
							OnErrorStreamReceivedMessage(message);
						}
					}
					catch (Exception ex)
					{
						// unexpected error
						var message = new LogMessage { Text = $"Unhandled exception reading JSON log message stream.\nException: {ex})." };
						OnErrorStreamReceivedMessage(message);
					}
				}
			}
			else
			{
				// the process has exited
				// => notify event recipients
				OnErrorStreamReceivedText(null);
				OnErrorStreamReceivedMessage(null);
			}
		}

		/// <summary>
		/// Formats the specified log message and logs it using the logging subsystem of the current process.
		/// The log level of the written message is the same as the log level specified in the message.
		/// </summary>
		/// <param name="message">Message to log.</param>
		/// <param name="stream">Name of the stream the message was read from.</param>
		private void LogMessage(ILogMessage message, string stream)
		{
			var builder = new StringBuilder();

			builder.AppendLine($"--- The process emitted the following log message via its standard {stream} stream ---");

			builder.AppendLine($"Timestamp:   {message.Timestamp:u}");

			if (message.LogWriterName != null)
			{
				builder.AppendLine($"Log Writer:  {message.LogWriterName}");
			}

			if (message.LogLevelName != null)
			{
				builder.AppendLine($"Log Level:   {message.LogLevelName}");
			}

			if (message.Tags != null && message.Tags.Count > 0)
			{
				builder.AppendLine($"Tags:        {string.Join(", ", message.Tags)}");
			}

			if (message.ApplicationName != null)
			{
				builder.AppendLine($"Application: {message.ApplicationName}");
			}

			if (message.ProcessName != null)
			{
				builder.AppendLine($"Process:     {message.ProcessName} (id: {message.ProcessId})");
			}

			// write the received log message to our own log using the same log level
			var level = LogLevel.GetAspect(message.LogLevelName ?? "Note"); // log message as 'Note', if not specified explicitly
			LogWriter.Write(level, builder.ToString());
		}

		#endregion

		#region Event Raiser

		/// <summary>
		/// Raises the <see cref="OutputStreamReceivedText"/> event.
		/// </summary>
		/// <param name="line">Line emitted by the output stream of the integrated process.</param>
		private void OnOutputStreamReceivedText(string line)
		{
			if (EventManager<LineReceivedEventArgs>.IsHandlerRegistered(this, nameof(OutputStreamReceivedText)))
			{
				EventManager<LineReceivedEventArgs>.FireEvent(this, nameof(OutputStreamReceivedText), this, new LineReceivedEventArgs(line));
			}
		}

		/// <summary>
		/// Raises the <see cref="OutputStreamReceivedMessage"/> event.
		/// </summary>
		/// <param name="message">Log message emitted by the output stream of the integrated process.</param>
		private void OnOutputStreamReceivedMessage(ILogMessage message)
		{
			if (EventManager<MessageReceivedEventArgs>.IsHandlerRegistered(this, nameof(OutputStreamReceivedMessage)))
			{
				EventManager<MessageReceivedEventArgs>.FireEvent(
					this,
					nameof(OutputStreamReceivedMessage),
					this,
					new MessageReceivedEventArgs(message));
			}
		}

		/// <summary>
		/// Raises the <see cref="ErrorStreamReceivedText"/> event.
		/// </summary>
		/// <param name="line">Line emitted by the error stream of the integrated process.</param>
		private void OnErrorStreamReceivedText(string line)
		{
			if (EventManager<LineReceivedEventArgs>.IsHandlerRegistered(this, nameof(ErrorStreamReceivedText)))
			{
				EventManager<LineReceivedEventArgs>.FireEvent(this, nameof(ErrorStreamReceivedText), this, new LineReceivedEventArgs(line));
			}
		}

		/// <summary>
		/// Raises the <see cref="ErrorStreamReceivedMessage"/> event.
		/// </summary>
		/// <param name="message">Log message emitted by the error stream of the integrated process.</param>
		private void OnErrorStreamReceivedMessage(ILogMessage message)
		{
			if (EventManager<MessageReceivedEventArgs>.IsHandlerRegistered(this, nameof(ErrorStreamReceivedMessage)))
			{
				EventManager<MessageReceivedEventArgs>.FireEvent(
					this,
					nameof(ErrorStreamReceivedMessage),
					this,
					new MessageReceivedEventArgs(message));
			}
		}

		#endregion
	}

}
