///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// The client side of a log service connection.
	/// </summary>
	public sealed class LogServiceClientChannel : LogServiceChannel
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceClientChannel"/> class.
		/// </summary>
		/// <param name="socket">A TCP socket representing the connection between the client and the server of the log service.</param>
		/// <param name="start">
		/// If <c>true</c> the channel immediately starts reading from the socket.
		/// If <c>false</c> the channel does not start reading the socket (call <see cref="Run"/> to make up for it).
		/// </param>
		internal LogServiceClientChannel(Socket socket, bool start) :
			base(socket)
		{
			// start channel, if it has not been shut down immediately
			if (Status == LogServiceChannelStatus.Created && start)
				Start();
		}

		/// <summary>
		/// Starts the channel, if it has been created with <c>start</c> set to <c>false</c>.
		/// </summary>
		public void Run()
		{
			Start();
		}

		#region Connecting to Server

		/// <summary>
		/// Connects to the log service server at the specified address and port.
		/// </summary>
		/// <param name="address">IP Address of the server to connect to.</param>
		/// <param name="port">Port number.</param>
		/// <param name="start">
		/// If <c>true</c> the channel immediately starts reading from the socket.
		/// If <c>false</c> the channel does not start reading the socket (call <see cref="Run"/> to make up for it).
		/// </param>
		/// <param name="cancellationToken">Cancellation token that can be signaled to abort the operation.</param>
		/// <returns>The log service client channel.</returns>
		public static LogServiceClientChannel ConnectToServer(
			IPAddress         address,
			int               port,
			bool              start             = true,
			CancellationToken cancellationToken = default)
		{
			Socket socket = null;
			try
			{
				// create a new socket to use 
				socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				// prepare event arguments for connecting asynchronously
				using (var completedEvent = new ManualResetEventSlim())
				{
					var e = new SocketAsyncEventArgs { RemoteEndPoint = new IPEndPoint(address, port) };

					// ReSharper disable once AccessToDisposedClosure
					e.Completed += (sender, args) => completedEvent.Set();

					const int maxRetryCount = 1;
					for (int retry = 0; retry <= maxRetryCount; retry++)
					{
						completedEvent.Reset();

						// connect to the server
						if (!socket.ConnectAsync(e))
							completedEvent.Set();

						// wait for the operation to complete
						completedEvent.Wait(cancellationToken);

						// try again, if the attempt to connect has timed out
						// (can occur sometimes in high load scenarios)
						if (e.SocketError == SocketError.Success || e.SocketError != SocketError.TimedOut) break;
					}

					// handle socket error, if necessary
					if (e.SocketError != SocketError.Success)
						throw new SocketException((int)e.SocketError);

					// the channel connected successfully
					var channel = new LogServiceClientChannel(e.ConnectSocket, start);
					socket = null;
					return channel;
				}
			}
			finally
			{
				socket?.Dispose();
			}
		}

		/// <summary>
		/// Connects to the log service server at the specified address and port.
		/// </summary>
		/// <param name="address">IP Address of the server to connect to.</param>
		/// <param name="port">Port number.</param>
		/// <param name="start">
		/// If <c>true</c> the channel immediately starts reading from the socket.
		/// If <c>false</c> the channel does not start reading the socket (call <see cref="Run"/> to make up for it).
		/// </param>
		/// <param name="cancellationToken">Cancellation token that can be signaled to abort the operation.</param>
		/// <returns>The log service client channel.</returns>
		public static async Task<LogServiceClientChannel> ConnectToServerAsync(
			IPAddress         address,
			int               port,
			bool              start             = true,
			CancellationToken cancellationToken = default)
		{
			Socket socket = null;
			try
			{
				// create a new socket to use 
				socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				// callback to invoke when connecting completes
				var connectTaskCompletionSource = new TaskCompletionSource<LogServiceClientChannel>();

				void ConnectCompleted(object sender, SocketAsyncEventArgs args)
				{
					if (args.SocketError != SocketError.Success)
					{
						connectTaskCompletionSource.SetException(new SocketException((int)args.SocketError));
						return;
					}

					try
					{
						var channel = new LogServiceClientChannel(args.ConnectSocket, start);
						connectTaskCompletionSource.SetResult(channel);
					}
					catch (Exception ex)
					{
						// creating the channel failed
						// => close the connected socket to avoid having it dangle around
						args.ConnectSocket.Close();
						connectTaskCompletionSource.SetException(ex);
					}
				}

				// prepare event arguments for connecting asynchronously
				var e = new SocketAsyncEventArgs { RemoteEndPoint = new IPEndPoint(address, port) };
				e.Completed += ConnectCompleted;

				// connect to the server
				const int maxRetryCount = 1;
				for (int retry = 0; retry <= maxRetryCount; retry++)
				{
					if (!socket.ConnectAsync(e))
					{
						// operation completed synchronously (very unlikely)
						ConnectCompleted(socket, e);
						return await connectTaskCompletionSource.Task.ConfigureAwait(false);
					}

					// operation is pending, the callback will be invoked on completion
					// => wait for connecting to complete
					var cancellationTask = Task.Delay(-1, cancellationToken);

					await Task
						.WhenAny(connectTaskCompletionSource.Task, cancellationTask)
						.ConfigureAwait(false);

					// retry, if the connect timed out (can easily happen in high load scenarios)
					if (connectTaskCompletionSource.Task.IsFaulted)
					{
						if (connectTaskCompletionSource.Task?.Exception?.Flatten().InnerException is SocketException sex)
						{
							if (sex.SocketErrorCode == SocketError.TimedOut && retry < maxRetryCount)
								continue;
						}
					}

					// return, if the connection has been established successfully
					if (connectTaskCompletionSource.Task.IsCompleted)
					{
						var clientChannel = await connectTaskCompletionSource
							       .Task
							       .ConfigureAwait(false);
						socket = null;
						return clientChannel;
					}

					// connecting has not completed => cancellation is pending
					Debug.Assert(cancellationToken.IsCancellationRequested);
					cancellationToken.ThrowIfCancellationRequested();
					return null; // should never occur
				}

				return null; // should never occur
			}
			finally
			{
				socket?.Dispose();
			}
		}

		#endregion

		#region Overrides

		/// <summary>
		/// Is called when the channel has been started successfully.
		/// The receiver is not started, yet.
		/// The executing thread holds the channel lock (<see cref="LogServiceChannel.Sync"/>) when called.
		/// </summary>
		protected override void OnStarted()
		{
			Debug.Assert(Monitor.IsEntered(Sync));

			// send a greeting and some information about the current process to the server
			SendGreeting();
			SendProcessInfo();
			SendApplicationInfo();
			SendPersistenceSetting(mStoringMessagesPersistently);

			// start the heartbeat task to ensure that the server does not shut the channel down due to inactivity checking
			if (HeartbeatInterval > TimeSpan.Zero)
				LogServiceClientChannelManager.RegisterHeartbeatTrigger(this);
		}

		/// <summary>
		/// Is called when the channel has completed shutting down.
		/// The executing thread holds the channel lock (<see cref="LogServiceChannel.Sync"/>) when called.
		/// </summary>
		protected override void OnShutdownCompleted()
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			base.OnShutdownCompleted();
			LogServiceClientChannelManager.UnregisterChannel(this);
		}

		/// <summary>
		/// Is called when the channel has received a complete line.
		/// </summary>
		/// <param name="line">Line to process.</param>
		protected override void OnLineReceived(ReadOnlySpan<char> line)
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			base.OnLineReceived(line);
		}

		#endregion

		#region Controlling Persistence

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
					if (mStoringMessagesPersistently != value)
					{
						mStoringMessagesPersistently = value;
						try { SendPersistenceSetting(value); }
						catch
						{
							/* swallow */
						}
					}
				}
			}
		}

		#endregion

		#region Sending Greeting and Process/Application Information

		/// <summary>
		/// Sends the greeting to the client.
		/// </summary>
		private void SendGreeting()
		{
			// determine the version of the library
			var versionAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
			string version = versionAttribute != null ? versionAttribute.InformationalVersion : "<unknown>";

			// send greeting and version information
			Send("HELLO Griffin+ .NET Log Service Client");
			Send($"INFO Log Service Library Version: {version}");
		}

		/// <summary>
		/// Sends the name of the client's process and it's id.
		/// </summary>
		private void SendProcessInfo()
		{
			string name = Process.GetCurrentProcess().ProcessName;
			int id = Process.GetCurrentProcess().Id;
			Send($"SET PROCESS_NAME {name}");
			Send($"SET PROCESS_ID {id}");
		}

		/// <summary>
		/// Sends the name of the application as specified by <see cref="Log.ApplicationName"/>.
		/// </summary>
		private void SendApplicationInfo()
		{
			string name = Log.ApplicationName;
			Send($"SET APPLICATION_NAME {name}");
		}

		/// <summary>
		/// Sends a command to the log service determining whether to store log messages from this client persistently.
		/// </summary>
		/// <param name="enable">
		/// <c>true</c> to configure the service to store messages from this client persistently;
		/// <c>false</c> to configure the service not to store messages from this client at all.
		/// </param>
		private void SendPersistenceSetting(bool enable)
		{
			string value = enable ? "1" : "0";
			Send($"SET PERSISTENCE {value}");
		}

		#endregion

		#region Heartbeat

		private static readonly TimeSpan sDefaultHeartbeatInterval = TimeSpan.FromMinutes(1);
		private                 TimeSpan mHeartbeatInterval        = sDefaultHeartbeatInterval;
		private volatile        int      mHeartbeatIntervalInTicks = (int)(sDefaultHeartbeatInterval.Ticks / 10000);

		/// <summary>
		/// Gets or sets the time between two heartbeat commands sent to the server to ensure that the channel
		/// is still operational (use <see cref="TimeSpan.Zero"/> to disable the heartbeat).
		/// Default: 1 minute.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">The heartbeat interval must not be negative.</exception>
		/// <remarks>
		/// Due to performance reasons the heartbeat will not be sent faster than every 500ms.
		/// </remarks>
		public TimeSpan HeartbeatInterval
		{
			get
			{
				lock (Sync)
				{
					return mHeartbeatInterval;
				}
			}

			set
			{
				if (value < TimeSpan.Zero)
					throw new ArgumentOutOfRangeException(nameof(value), value, "The heartbeat interval must not be negative.");

				if (value.Ticks / 10000 > int.MaxValue)
					throw new ArgumentOutOfRangeException(nameof(value), value, "The heartbeat interval is too large.");

				lock (Sync)
				{
					if (mHeartbeatInterval != value)
					{
						// set new heartbeat interval
						mHeartbeatInterval = value;
						mHeartbeatIntervalInTicks = (int)(value.Ticks / 10000);

						// start sending heartbeats periodically, if necessary
						if (mHeartbeatInterval > TimeSpan.Zero)
							LogServiceClientChannelManager.RegisterHeartbeatTrigger(this);
						else
							LogServiceClientChannelManager.UnregisterHeartbeatTrigger(this);
					}
				}
			}
		}

		/// <summary>
		/// Sends a 'HEARTBEAT' command, if it is due.
		/// </summary>
		/// <returns>Tick count of the next heartbeat.</returns>
		internal int SendHeartbeatIfDue()
		{
			// determine whether the heartbeat is due
			int currentTicks = Environment.TickCount;
			int nextRunInTicks = currentTicks + Math.Max(mHeartbeatIntervalInTicks - currentTicks + LastSendTickCount, 0);

			// send heartbeat, if it is due
			if (nextRunInTicks == currentTicks)
			{
				try
				{
					// send heartbeat
					// (updates the timestamp of the last sending operation)
					Send("HEARTBEAT");
				}
				catch (Exception)
				{
					// swallow...
				}

				// heartbeat was sent
				// => try again after the configured time...
				return currentTicks + mHeartbeatIntervalInTicks;
			}

			return nextRunInTicks;
		}

		#endregion

		#region Sending Message

		private static readonly char[] sNewLineChars         = { '\r', '\n' };
		private readonly        object mCommandBuilderSync   = new object();
		private                 char[] mCommandBuilderBuffer = new char[32 * 1024];

		/// <summary>
		/// Sends a log message to the log service.
		/// </summary>
		/// <param name="message">Log message to send.</param>
		/// <returns>
		/// <c>true</c>, if the specified message was successfully enqueued for sending;
		/// <c>false</c>, if the send queue is full.
		/// </returns>
		public bool Send(ILogMessage message)
		{
			lock (mCommandBuilderSync)
			{
				int length;

				while (true)
				{
					try
					{
						length = FormatWriteCommand(message, mCommandBuilderBuffer);
						break;
					}
					catch (InsufficientBufferSizeException)
					{
						// the resulting command does not fit into the buffer
						// => resize buffer and try again
						mCommandBuilderBuffer = new char[mCommandBuilderBuffer.Length + 4096];
					}
				}

				return Send(mCommandBuilderBuffer, 0, length, false);
			}
		}

		/// <summary>
		/// Creates a 'WRITE' command to the log service using the specified message.
		/// </summary>
		/// <param name="message">Message to format.</param>
		/// <param name="buffer">Buffer that will receive the formatted message.</param>
		/// <returns>Length of the characters 'WRITE' command in the buffer.</returns>
		/// <exception cref="InsufficientBufferSizeException">
		/// The buffer is too small and needs to be resized
		/// (due to performance optimizations the boundary checks assume the worst case, so there actually may be enough space...)
		/// </exception>
		internal static int FormatWriteCommand(ILogMessage message, char[] buffer)
		{
			int index = 0;

			// start with 'WRITE' command
			if (buffer.Length < 6) throw new InsufficientBufferSizeException();
			buffer[index++] = 'W';
			buffer[index++] = 'R';
			buffer[index++] = 'I';
			buffer[index++] = 'T';
			buffer[index++] = 'E';
			buffer[index++] = '\n';

			// append message fields
			index = FormatWriteCommand_Timestamp(buffer, index, message.Timestamp);
			index = FormatWriteCommand_HighPrecisionTimestamp(buffer, index, message.HighPrecisionTimestamp);
			index = FormatWriteCommand_LostMessageCount(buffer, index, message.LostMessageCount);
			index = FormatWriteCommand_LogWriterName(buffer, index, message.LogWriterName);
			index = FormatWriteCommand_LogLevelName(buffer, index, message.LogLevelName);
			index = FormatWriteCommand_Tags(buffer, index, message.Tags);
			index = FormatWriteCommand_Text(buffer, index, message.Text);
			return index;
		}

		/// <summary>
		/// Formats the specified timestamp into a buffer for a 'WRITE' command (incl. trailing line break).
		/// </summary>
		/// <param name="buffer">Buffer to format the timestamp into.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="timestamp">Timestamp to format.</param>
		/// <returns>The adjusted offset pointing to the position after the timestamp in the array.</returns>
		/// <exception cref="InsufficientBufferSizeException">
		/// The buffer is too small and needs to be resized
		/// (due to performance optimizations the boundary checks assume the worst case, so there actually may be enough space...)
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int FormatWriteCommand_Timestamp(char[] buffer, int offset, DateTimeOffset timestamp)
		{
			if (offset + 41 > buffer.Length)
				throw new InsufficientBufferSizeException();

			// field (11 characters)
			buffer[offset++] = 't';
			buffer[offset++] = 'i';
			buffer[offset++] = 'm';
			buffer[offset++] = 'e';
			buffer[offset++] = 's';
			buffer[offset++] = 't';
			buffer[offset++] = 'a';
			buffer[offset++] = 'm';
			buffer[offset++] = 'p';
			buffer[offset++] = ':';
			buffer[offset++] = ' ';

			var dateTime = timestamp.DateTime;
			var dateTimeOffset = timestamp.Offset;

			// date (4+1+2+1+2 = 10 characters)
			FormatAndAppendToBuffer4(buffer, offset, dateTime.Year);
			offset += 4;
			buffer[offset++] = '-';
			FormatAndAppendToBuffer2(buffer, offset, dateTime.Month);
			offset += 2;
			buffer[offset++] = '-';
			FormatAndAppendToBuffer2(buffer, offset, dateTime.Day);
			offset += 2;

			// delimiter (1 character)
			buffer[offset++] = 'T';

			// time (2+1+2+1+2+1+3 = 12 characters)
			FormatAndAppendToBuffer2(buffer, offset, dateTime.Hour);
			offset += 2;
			buffer[offset++] = ':';
			FormatAndAppendToBuffer2(buffer, offset, dateTime.Minute);
			offset += 2;
			buffer[offset++] = ':';
			FormatAndAppendToBuffer2(buffer, offset, dateTime.Second);
			offset += 2;
			buffer[offset++] = '.';
			FormatAndAppendToBuffer3(buffer, offset, dateTime.Millisecond);
			offset += 3;

			// timezone offset (1+2+1+2 = 6 characters)
			if (dateTimeOffset.Ticks < 0)
			{
				buffer[offset++] = '-';
				FormatAndAppendToBuffer2(buffer, offset, -dateTimeOffset.Hours);
				offset += 2;
				buffer[offset++] = ':';
				FormatAndAppendToBuffer2(buffer, offset, -dateTimeOffset.Minutes);
				offset += 2;
			}
			else
			{
				buffer[offset++] = '+';
				FormatAndAppendToBuffer2(buffer, offset, dateTimeOffset.Hours);
				offset += 2;
				buffer[offset++] = ':';
				FormatAndAppendToBuffer2(buffer, offset, dateTimeOffset.Minutes);
				offset += 2;
			}

			// line break (1 character)
			buffer[offset++] = '\n';

			return offset;
		}

		/// <summary>
		/// Formats the specified high-precision timestamp into a buffer for a 'WRITE' command (incl. trailing line break).
		/// </summary>
		/// <param name="buffer">Buffer to format the high-precision timestamp into.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="timestamp">High-precision timestamp to format.</param>
		/// <returns>The adjusted offset pointing to the position after the timestamp in the array.</returns>
		/// <exception cref="InsufficientBufferSizeException">
		/// The buffer is too small and needs to be resized
		/// (due to performance optimizations the boundary checks assume the worst case, so there actually may be enough space...)
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int FormatWriteCommand_HighPrecisionTimestamp(char[] buffer, int offset, long timestamp)
		{
			if (offset + 28 > buffer.Length)
				throw new InsufficientBufferSizeException();

			// field (7 characters)
			buffer[offset++] = 't';
			buffer[offset++] = 'i';
			buffer[offset++] = 'c';
			buffer[offset++] = 'k';
			buffer[offset++] = 's';
			buffer[offset++] = ':';
			buffer[offset++] = ' ';

			// value (up to 20 characters)
			offset += FormatNumber(buffer, offset, timestamp);

			// line break (1 character)
			buffer[offset++] = '\n';

			return offset;
		}

		/// <summary>
		/// Formats the specified lost message count into a buffer for a 'WRITE' command (incl. trailing line break).
		/// </summary>
		/// <param name="buffer">Buffer to format the lost message count into.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="count">Lost message count to format.</param>
		/// <returns>The adjusted offset pointing to the position after the lost message count in the array.</returns>
		/// <exception cref="InsufficientBufferSizeException">
		/// The buffer is too small and needs to be resized
		/// (due to performance optimizations the boundary checks assume the worst case, so there actually may be enough space...)
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int FormatWriteCommand_LostMessageCount(char[] buffer, int offset, int count)
		{
			if (offset + 18 > buffer.Length)
				throw new InsufficientBufferSizeException();

			if (count > 0)
			{
				// field (6 characters)
				buffer[offset++] = 'l';
				buffer[offset++] = 'o';
				buffer[offset++] = 's';
				buffer[offset++] = 't';
				buffer[offset++] = ':';
				buffer[offset++] = ' ';

				// value (up to 11 characters)
				offset += FormatNumber(buffer, offset, count);

				// line break (1 character)
				buffer[offset++] = '\n';
			}

			return offset;
		}

		/// <summary>
		/// Formats the specified log writer name into a buffer for a 'WRITE' command (incl. trailing line break).
		/// </summary>
		/// <param name="buffer">Buffer to format the log writer name into.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="name">Log writer name to format.</param>
		/// <returns>The adjusted offset pointing to the position after the log writer name in the array.</returns>
		/// <exception cref="InsufficientBufferSizeException">
		/// The buffer is too small and needs to be resized
		/// (due to performance optimizations the boundary checks assume the worst case, so there actually may be enough space...)
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int FormatWriteCommand_LogWriterName(char[] buffer, int offset, string name)
		{
			if (!string.IsNullOrWhiteSpace(name))
			{
				// the buffer should be large enough to store the string in the worst case scenario
				// (all characters have to be escaped effectively doubling the length of the string)
				if (offset + 8 + 2 * name.Length + 1 > buffer.Length)
					throw new InsufficientBufferSizeException();

				// field (8 characters)
				buffer[offset++] = 'w';
				buffer[offset++] = 'r';
				buffer[offset++] = 'i';
				buffer[offset++] = 't';
				buffer[offset++] = 'e';
				buffer[offset++] = 'r';
				buffer[offset++] = ':';
				buffer[offset++] = ' ';

				// value
				offset = EscapeNewlines(buffer, offset, name);

				// line break (1 character)
				buffer[offset++] = '\n';
			}

			return offset;
		}

		/// <summary>
		/// Formats the specified log level name into a buffer for a 'WRITE' command (incl. trailing line break).
		/// </summary>
		/// <param name="buffer">Buffer to format the log level name into.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="name">Log level name to format.</param>
		/// <returns>The adjusted offset pointing to the position after the log level name in the array.</returns>
		/// <exception cref="InsufficientBufferSizeException">
		/// The buffer is too small and needs to be resized
		/// (due to performance optimizations the boundary checks assume the worst case, so there actually may be enough space...)
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int FormatWriteCommand_LogLevelName(char[] buffer, int offset, string name)
		{
			if (!string.IsNullOrWhiteSpace(name))
			{
				// the buffer should be large enough to store the string in the worst case scenario
				// (all characters have to be escaped effectively doubling the length of the string)
				if (offset + 7 + 2 * name.Length + 1 > buffer.Length)
					throw new InsufficientBufferSizeException();

				// field (7 characters)
				buffer[offset++] = 'l';
				buffer[offset++] = 'e';
				buffer[offset++] = 'v';
				buffer[offset++] = 'e';
				buffer[offset++] = 'l';
				buffer[offset++] = ':';
				buffer[offset++] = ' ';

				// value
				offset = EscapeNewlines(buffer, offset, name);

				// line break (1 character)
				buffer[offset++] = '\n';
			}

			return offset;
		}

		/// <summary>
		/// Formats the specified tags into a buffer for a 'WRITE' command (incl. trailing line break).
		/// </summary>
		/// <param name="buffer">Buffer to format the tags into.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="tags">Tags to format.</param>
		/// <returns>The adjusted offset pointing to the position after the tags in the array.</returns>
		/// <exception cref="InsufficientBufferSizeException">
		/// The buffer is too small and needs to be resized
		/// (due to performance optimizations the boundary checks assume the worst case, so there actually may be enough space...)
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int FormatWriteCommand_Tags(char[] buffer, int offset, ITagSet tags)
		{
			if (tags != null)
			{
				// ReSharper disable once ForCanBeConvertedToForeach
				for (int i = 0; i < tags.Count; i++)
				{
					string tag = tags[i];

					if (offset + 5 + tag.Length + 1 > buffer.Length)
						throw new InsufficientBufferSizeException();

					buffer[offset++] = 't';
					buffer[offset++] = 'a';
					buffer[offset++] = 'g';
					buffer[offset++] = ':';
					buffer[offset++] = ' ';

#if NETSTANDARD2_1
					tag.AsSpan().CopyTo(new Span<char>(buffer, offset, buffer.Length - offset));
#elif NETSTANDARD2_0 || NETFRAMEWORK
					tag.CopyTo(0, buffer, offset, tag.Length);
#else
#error Unhandled Target Framework
#endif

					offset += tag.Length;
					buffer[offset++] = '\n';
				}
			}

			return offset;
		}

#if NETSTANDARD2_1
		// ----------------------------------------------------------------------------------------------------------------------------
		// .NET Core 3+ and .NET 5
		// => use performant span support
		// ----------------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Formats the specified message text into a buffer for a 'WRITE' command (incl. trailing line break).
		/// </summary>
		/// <param name="buffer">Buffer to format the tags into.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="text">Text to format.</param>
		/// <returns>The adjusted offset pointing to the position after the text in the array.</returns>
		/// <exception cref="InsufficientBufferSizeException">
		/// The buffer is too small and needs to be resized
		/// (due to performance optimizations the boundary checks assume the worst case, so there actually may be enough space...)
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int FormatWriteCommand_Text(char[] buffer, int offset, string text)
		{
			// the buffer should be large enough to store the field name at this point
			// (another check taking the string into account is done below)
			if (offset + 5 + 1 > buffer.Length) // for "text: " or "text:\n"
				throw new InsufficientBufferSizeException();

			// abort, if the text is null or contains whitespaces only
			if (string.IsNullOrWhiteSpace(text))
				return offset;

			// field (5 characters)
			buffer[offset++] = 't';
			buffer[offset++] = 'e';
			buffer[offset++] = 'x';
			buffer[offset++] = 't';
			buffer[offset++] = ':';

			// handle common case that the text consists of a single line only and it is short enough to be put just after the field
			if (text.AsSpan().IndexOfAny('\r', '\n') < 0 && text.Length < MaxLineLength - 6) // -6 to take "text: " into account
			{
				buffer[offset++] = ' ';
				if (offset + text.Length + 1 > buffer.Length) throw new InsufficientBufferSizeException(); // + 1 for the line break at the end of the line
				text.AsSpan().CopyTo(new Span<char>(buffer, offset, buffer.Length - offset));
				offset += text.Length;
				buffer[offset++] = '\n';
				return offset;
			}

			// the text consists of multiple lines and/or is so long that a line splitter is needed to handle this
			buffer[offset++] = '\n';
			var span = text.AsSpan();
			char previous = (char)0;
			while (true)
			{
				// find first occurrence of a line break character
				int index = span.IndexOfAny('\r', '\n');
				if (index < 0) break;

				// get current line break character
				char current = span[index];

				// the index where the line break character was found is the line length as we're starting at index 0
				int lineLength = index;

				// process the line content (characters that are not considered line breaks)
				CopyLineIntoBuffer(span.Slice(0, lineLength), buffer, ref offset);

				// add a newline character
				// (normalizes "\r", "\n" and "\r\n" to "\n"
				if (current == '\r' || previous != '\r' || lineLength > 0)
				{
					if (offset + 1 > buffer.Length) throw new InsufficientBufferSizeException();
					buffer[offset++] = '\n';
				}

				previous = current;

				// proceed with the next line
				span = span.Slice(index + 1);
			}

			// copy the part following the last line break character
			CopyLineIntoBuffer(span, buffer, ref offset);

			// terminate the message
			if (offset + 3 > buffer.Length) throw new InsufficientBufferSizeException();
			if (buffer[offset - 1] != '\n') buffer[offset++] = '\n';
			buffer[offset++] = '.';
			buffer[offset++] = '\n';

			return offset;
		}

		/// <summary>
		/// Copies the specified line into the buffer, inserting a splitter sequence, if the line exceeds the line length.
		/// </summary>
		/// <param name="line">Line to copy into the buffer (must not contain line break characters).</param>
		/// <param name="buffer">Buffer to copy the line into.</param>
		/// <param name="offset">Offset to start copying the line into (is updated to reflect the new position).</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void CopyLineIntoBuffer(ReadOnlySpan<char> line, char[] buffer, ref int offset)
		{
			while (line.Length > 0)
			{
				// determine the part of the line to copy into the destination buffer
				// (very long lines may have to be split up)
				int charsToCopy = Math.Min(line.Length, MaxLineLength);

				// ensure that there is enough space in the buffer to receive the line,
				// a doubled first character and a line splitter sequence (if necessary)
				if (offset + charsToCopy + 1 + 3 > buffer.Length)
					throw new InsufficientBufferSizeException();

				// get the first character in the line
				char firstCharInLine = line[0];

				// double special characters
				if (firstCharInLine == '.')
				{
					buffer[offset++] = '.';
					charsToCopy--;
				}
				else if (firstCharInLine == '\\')
				{
					buffer[offset++] = '\\';
					charsToCopy--;
				}

				// copy line into the buffer
				var spanToCopy = line.Slice(0, charsToCopy);
				spanToCopy.CopyTo(new Span<char>(buffer, offset, buffer.Length - offset));
				offset += charsToCopy;
				line = line.Slice(charsToCopy);

				// insert a splitter sequence, if the line is too long
				if (line.Length > 0)
				{
					buffer[offset++] = '\n';
					buffer[offset++] = '\\';
					buffer[offset++] = '\n';
				}
			}
		}

#elif NETSTANDARD2_0 || NETFRAMEWORK
		// ----------------------------------------------------------------------------------------------------------------------------
		// .NET Core 2 and .NET Framework
		// => use custom implementation as benchmarks have shown that using spans is very slow
		// ----------------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Formats the specified message text into a buffer for a 'WRITE' command (incl. trailing line break).
		/// </summary>
		/// <param name="buffer">Buffer to format the tags into.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="text">Text to format.</param>
		/// <returns>The adjusted offset pointing to the position after the text in the array.</returns>
		/// <exception cref="InsufficientBufferSizeException">
		/// The buffer is too small and needs to be resized
		/// (due to performance optimizations the boundary checks assume the worst case, so there actually may be enough space...)
		/// </exception>
		internal static int FormatWriteCommand_Text(char[] buffer, int offset, string text)
		{
			// the buffer should be large enough to store the field name at this point
			// (another check taking the string into account is done below)
			if (offset + 5 + 1 > buffer.Length) // for "text: " or "text:\n"
				throw new InsufficientBufferSizeException();

			// abort, if the text is null or contains whitespaces only
			if (string.IsNullOrWhiteSpace(text))
				return offset;

			// field (5 characters)
			buffer[offset++] = 't';
			buffer[offset++] = 'e';
			buffer[offset++] = 'x';
			buffer[offset++] = 't';
			buffer[offset++] = ':';

			// handle common case that the text consists of a single line only and it is short enough to be put just after the field
			if (text.IndexOfAny(sNewLineChars) < 0 && text.Length < MaxLineLength - 6) // -6 to take "text: " into account
			{
				buffer[offset++] = ' ';
				if (offset + text.Length + 1 > buffer.Length) throw new InsufficientBufferSizeException(); // + 1 for the line break at the end of the line
				text.CopyTo(0, buffer, offset, text.Length);
				offset += text.Length;
				buffer[offset++] = '\n';
				return offset;
			}

			// the text consists of multiple lines and/or is so long that a line splitter is needed to handle this
			buffer[offset++] = '\n';
			int startIndex = 0;
			char previous = (char)0;
			while (true)
			{
				// find first occurrence of a line break character
				int index = text.IndexOfAny(sNewLineChars, startIndex);
				if (index < 0) break;

				// get current line break character
				char current = text[index];

				// the index where the line break character was found is the line length as we're starting at index 0
				int lineLength = index - startIndex;

				// process the line content (characters that are not considered line breaks)
				CopyLineIntoBuffer(text, startIndex, lineLength, buffer, ref offset);

				// add a newline character
				// (normalizes "\r", "\n" and "\r\n" to "\n"
				if (current == '\r' || previous != '\r' || lineLength > 0)
				{
					if (offset + 1 > buffer.Length) throw new InsufficientBufferSizeException();
					buffer[offset++] = '\n';
				}

				previous = current;

				// proceed with the next line
				startIndex = index + 1;
			}

			// copy the part following the last line break character
			CopyLineIntoBuffer(text, startIndex, text.Length - startIndex, buffer, ref offset);

			// terminate the message
			if (offset + 3 > buffer.Length) throw new InsufficientBufferSizeException();
			if (buffer[offset - 1] != '\n') buffer[offset++] = '\n';
			buffer[offset++] = '.';
			buffer[offset++] = '\n';

			return offset;
		}

		/// <summary>
		/// Copies the specified line into the buffer, inserting a splitter sequence, if the line exceeds the line length.
		/// </summary>
		/// <param name="input">String containing the line to copy into the buffer (must not contain line break characters).</param>
		/// <param name="inputOffset">Index in the input string the line starts at.</param>
		/// <param name="inputLength">Length of the line to copy into the buffer.</param>
		/// <param name="output">Buffer to copy the line into.</param>
		/// <param name="outputOffset">Offset to start copying the line into (is updated to reflect the new position).</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void CopyLineIntoBuffer(
			string  input,
			int     inputOffset,
			int     inputLength,
			char[]  output,
			ref int outputOffset)
		{
			while (inputLength > 0)
			{
				// determine how many characters to copy into the destination buffer at once
				// (very long lines may have to be split up)
				int charsToCopy = Math.Min(inputLength, MaxLineLength);

				// ensure that there is enough space in the buffer to receive the line,
				// a doubled first character and a line splitter sequence (if necessary)
				if (outputOffset + charsToCopy + 1 + 3 > output.Length)
					throw new InsufficientBufferSizeException();

				// get first character in the line
				char firstCharInLine = input[inputOffset];

				// double special characters
				if (firstCharInLine == '.')
				{
					output[outputOffset++] = '.';
					charsToCopy--;
				}
				else if (firstCharInLine == '\\')
				{
					output[outputOffset++] = '\\';
					charsToCopy--;
				}

				// copy line into the buffer
				input.CopyTo(inputOffset, output, outputOffset, charsToCopy);
				outputOffset += charsToCopy;
				inputLength -= charsToCopy;
				inputOffset += charsToCopy;

				// insert a splitter sequence, if the line is too long
				if (inputLength > 0)
				{
					output[outputOffset++] = '\n';
					output[outputOffset++] = '\\';
					output[outputOffset++] = '\n';
				}
			}
		}

#else
#error Unhandled Target Framework
#endif

		/// <summary>
		/// Escapes newline characters in the specified string and puts the result into a buffer.
		/// The buffer must have space for at least twice the length of the specified string.
		/// </summary>
		/// <param name="buffer">Buffer to put the escaped string into.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="s">String to escape.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int EscapeNewlines(char[] buffer, int offset, string s)
		{
#if NETSTANDARD2_1
			// ----------------------------------------------------------------------------------------------------------------------------
			// .NET Core 3+ and .NET 5
			// => use performant span support
			// ----------------------------------------------------------------------------------------------------------------------------
			var span = s.AsSpan();
			while (true)
			{
				int index = span.IndexOfAny(sNewLineChars);
				if (index < 0) break;

				// extract the line from the string
				var spanToCopy = span.Slice(0, index);

				// ensure that there is enough space in the buffer to store the line and the escaped line break characters
				if (offset + spanToCopy.Length + 2 > buffer.Length) throw new InsufficientBufferSizeException();

				// copy line to the buffer
				spanToCopy.CopyTo(new Span<char>(buffer, offset, buffer.Length - offset));
				offset += spanToCopy.Length;

				// escape line break character
				char current = span[index];
				buffer[offset++] = '\\';
				if (current == '\r') buffer[offset++] = 'r';
				else if (current == '\n') buffer[offset++] = 'n';

				// proceed with the next line in the string
				span = span.Slice(index + 1);
			}

			// copy the line after the last line break
			if (offset + span.Length > buffer.Length) throw new InsufficientBufferSizeException();
			span.CopyTo(new Span<char>(buffer, offset, buffer.Length - offset));
			offset += span.Length;
			return offset;

#elif NETSTANDARD2_0 || NETFRAMEWORK
			// ----------------------------------------------------------------------------------------------------------------------------
			// .NET Core 2 and .NET Framework
			// => use custom implementation as benchmarks have shown that using spans is very slow
			// ----------------------------------------------------------------------------------------------------------------------------

			if (offset + 2 * s.Length > buffer.Length)
				throw new InsufficientBufferSizeException();

			foreach (char current in s)
			{
				if (current == '\r')
				{
					buffer[offset++] = '\\';
					buffer[offset++] = 'r';
					continue;
				}

				if (current == '\n')
				{
					buffer[offset++] = '\\';
					buffer[offset++] = 'n';
					continue;
				}

				// just append other characters
				buffer[offset++] = current;
			}

			return offset;
#else
#error Unhandled Target Framework
#endif
		}

		/// <summary>
		/// Formats the specified number into a buffer.
		/// The buffer must have space for at least 11 characters (10 digits + 1 sign).
		/// </summary>
		/// <param name="buffer">Buffer receiving the formatted number.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="number">Number to format.</param>
		/// <returns>Length of the formatted string.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int FormatNumber(char[] buffer, int offset, int number)
		{
			// abort if the number is zero
			// (number becoming zero serves as the termination condition below)
			if (number == 0)
			{
				buffer[offset] = '0';
				return 1;
			}

			// check whether the integer is negative and make it positive for the conversion
			bool isNegative = number < 0;

			// convert absolute value of the integer
			Span<char> scratchBuffer = stackalloc char[11]; // 10 digits + 1 sign
			int index = scratchBuffer.Length;
			uint value = (uint)number;
			if (isNegative) value = ~value + 1;
			while (value != 0)
			{
				uint remainder = value % 10;
				scratchBuffer[--index] = (char)('0' + remainder);
				value = value / 10;
			}

			// if number is negative, add '-'
			if (isNegative) scratchBuffer[--index] = '-';

			// copy local buffer into the output buffer
			int length = scratchBuffer.Length - index;
			scratchBuffer.Slice(index).CopyTo(new Span<char>(buffer, offset, length));
			return length;
		}

		/// <summary>
		/// Formats the specified number into a buffer.
		/// The buffer must have space for at least 20 characters (19 digits + 1 sign).
		/// </summary>
		/// <param name="buffer">Buffer receiving the formatted number.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="number">Number to format.</param>
		/// <returns>Length of the formatted string.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int FormatNumber(char[] buffer, int offset, long number)
		{
			// abort if the number is zero
			// (number becoming zero serves as the termination condition below)
			if (number == 0)
			{
				buffer[offset] = '0';
				return 1;
			}

			// check whether the integer is negative and make it positive for the conversion
			bool isNegative = number < 0;

			// convert absolute value of the integer
			Span<char> scratchBuffer = stackalloc char[20]; // 19 digits + 1 sign
			int index = scratchBuffer.Length;
			ulong value = (ulong)number;
			if (isNegative) value = ~value + 1;
			while (value != 0)
			{
				ulong remainder = value % 10;
				scratchBuffer[--index] = (char)('0' + remainder);
				value = value / 10;
			}

			// if number is negative, add '-'
			if (isNegative) scratchBuffer[--index] = '-';

			// copy local buffer into the output buffer
			int length = scratchBuffer.Length - index;
			scratchBuffer.Slice(index).CopyTo(new Span<char>(buffer, offset, length));
			return length;
		}

		/// <summary>
		/// Formats the specified number into a buffer
		/// (for numbers that do not exceed two digits, the result will always have two digits).
		/// </summary>
		/// <param name="buffer">Buffer receiving the formatted number.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="number">Number to format.</param>
		/// <returns>Length of the formatted string.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void FormatAndAppendToBuffer2(char[] buffer, int offset, int number)
		{
			buffer[offset + 1] = (char)('0' + number % 10);
			number /= 10;
			buffer[offset] = (char)('0' + number % 10);
		}

		/// <summary>
		/// Formats the specified number into a buffer
		/// (for numbers that do not exceed three digits, the result will always have three digits).
		/// </summary>
		/// <param name="buffer">Buffer receiving the formatted number.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="number">Number to format.</param>
		/// <returns>Length of the formatted string.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void FormatAndAppendToBuffer3(char[] buffer, int offset, int number)
		{
			buffer[offset + 2] = (char)('0' + number % 10);
			number /= 10;
			buffer[offset + 1] = (char)('0' + number % 10);
			number /= 10;
			buffer[offset] = (char)('0' + number % 10);
		}

		/// <summary>
		/// Formats the specified number into a buffer
		/// (for numbers that do not exceed four digits, the result will always have four digits).
		/// </summary>
		/// <param name="buffer">Buffer receiving the formatted number.</param>
		/// <param name="offset">Offset in the buffer to start at.</param>
		/// <param name="number">Number to format.</param>
		/// <returns>Length of the formatted string.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void FormatAndAppendToBuffer4(char[] buffer, int offset, int number)
		{
			buffer[offset + 3] = (char)('0' + number % 10);
			number /= 10;
			buffer[offset + 2] = (char)('0' + number % 10);
			number /= 10;
			buffer[offset + 1] = (char)('0' + number % 10);
			number /= 10;
			buffer[offset] = (char)('0' + number % 10);
		}

		#endregion
	}

}
