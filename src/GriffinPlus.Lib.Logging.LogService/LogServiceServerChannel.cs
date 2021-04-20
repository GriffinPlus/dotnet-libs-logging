///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using GriffinPlus.Lib.Collections;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// The server side of a log service connection.
	/// </summary>
	public sealed class LogServiceServerChannel : LogServiceChannel
	{
		/// <summary>
		/// The <see cref="LogServiceServer"/> the channel belongs to.
		/// </summary>
		private readonly LogServiceServer mServer;

		/// <summary>
		/// Indicates whether received data is looped back for testing purposes.
		/// </summary>
		private bool mIsLoopbackEnabled;

		/// <summary>
		/// List node that is used by <see cref="LogServiceServer"/> to organize channels.
		/// </summary>
		internal readonly LinkedListNode<LogServiceServerChannel> Node;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceServerChannel"/> class.
		/// </summary>
		/// <param name="server">The <see cref="LogServiceServer"/> the channel belongs to.</param>
		/// <param name="socket">A TCP socket representing the connection between the client and the server of the log service.</param>
		/// <param name="shutdownToken">CancellationToken that is signaled to shut the channel down.</param>
		internal LogServiceServerChannel(LogServiceServer server, Socket socket, CancellationToken shutdownToken) :
			base(socket, shutdownToken)
		{
			mServer = server;

			// initialize the list node the server needs to organize and monitor channels
			Node = new LinkedListNode<LogServiceServerChannel>(this);

			// start server and register the channel for monitoring,
			// if it has not been shut down immediately
			if (Status == LogServiceChannelStatus.Created)
				Start();
		}

		/// <summary>
		/// Is called when the channel has been started successfully.
		/// The receiver is not started, yet.
		/// The executing thread holds the channel lock (<see cref="LogServiceChannel.Sync"/>) when called.
		/// </summary>
		protected override void OnStarted()
		{
			SendGreeting();

			// enable looping back received data, if configured
			mIsLoopbackEnabled = mServer.TestMode_EchoReceivedData;
		}

		/// <summary>
		/// Sends the greeting to the client.
		/// </summary>
		private void SendGreeting()
		{
			// always send HELLO to greet the client and indicate to whom it is talking
			Send($"HELLO {mServer.Settings.GreetingText}");

			// send version of the application, if configured
			if (mServer.Settings.SendServerVersion)
			{
				var versionAttribute = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyFileVersionAttribute>();
				string version = versionAttribute != null ? versionAttribute.Version : "<unknown>";
				Send($"INFO Server Version: {version}");
			}

			// send version of the log service library, if configured
			if (mServer.Settings.SendLibraryVersion)
			{
				var versionAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
				string version = versionAttribute != null ? versionAttribute.InformationalVersion : "<unknown>";
				Send($"INFO Log Service Library Version: {version}");
			}
		}

		/// <summary>
		/// Is called directly after some data has been received successfully.
		/// </summary>
		protected override void OnDataReceived()
		{
			// ReSharper disable once InconsistentlySynchronizedField
			mServer.ProcessChannelHasReceivedData(this);
		}

		/// <summary>
		/// Is called when the channel has completed shutting down.
		/// (the executing thread holds the channel lock (<see cref="LogServiceChannel.Sync"/>) when called).
		/// </summary>
		protected override void OnShutdownCompleted()
		{
			mServer?.ProcessChannelHasCompletedShuttingDown(this);
		}

		/// <summary>
		/// Is called when the channel has received a complete line.
		/// </summary>
		/// <param name="line">Line to process.</param>
		protected override void OnLineReceived(ReadOnlySpan<char> line)
		{
			// let the base class do its work
			base.OnLineReceived(line);

			// loop back data, if the server should loop back data as part of a test
			if (mIsLoopbackEnabled)
			{
				LoopbackLine(line);
			}

			// TODO: Process commands here...
		}

		/// <summary>
		/// Is called when the channel has completed sending a chunk of data.
		/// </summary>
		protected override void OnSendingCompleted()
		{
			// let the base class do its work
			base.OnSendingCompleted();

			// send data that has been buffered in loopback mode
			if (mIsLoopbackEnabled)
				SendBufferedLoopbackLines();
		}

		#region Loopback Mode (for Testing)

		private struct LoopbackBuffer
		{
			public char[] Buffer;
			public int    Length;
		}

		private readonly Deque<LoopbackBuffer> mLoopbackOverflowQueue = new Deque<LoopbackBuffer>();

		/// <summary>
		/// Sends the specified line back to the remote peer (loopback test mode).
		/// </summary>
		/// <param name="line">Line to send back (does not include a new line character at the end).</param>
		private void LoopbackLine(ReadOnlySpan<char> line)
		{
			while (mLoopbackOverflowQueue.Count > 0)
			{
				var bufferedLine = mLoopbackOverflowQueue[0];
				mLoopbackOverflowQueue.RemoveFromFront();

				if (!Send(bufferedLine.Buffer, 0, bufferedLine.Length, false))
				{
					// send queue is full
					// => put the buffered line back into the queue
					mLoopbackOverflowQueue.AddToFront(bufferedLine);

					// store line to try again later
					char[] buffer = ArrayPool<char>.Shared.Rent(line.Length + 1);
					line.CopyTo(buffer.AsSpan());
					buffer[line.Length] = '\n';
					mLoopbackOverflowQueue.AddToBack(new LoopbackBuffer { Buffer = buffer, Length = line.Length + 1 });
					return;
				}

				// sending buffered line succeeded
				// => proceed with the next buffered line
				ArrayPool<char>.Shared.Return(bufferedLine.Buffer);
			}

			if (!Send(line, true))
			{
				// send queue is full
				// => store line to try again later
				char[] buffer = ArrayPool<char>.Shared.Rent(line.Length + 1);
				line.CopyTo(buffer.AsSpan());
				buffer[line.Length] = '\n';
				mLoopbackOverflowQueue.AddToBack(new LoopbackBuffer { Buffer = buffer, Length = line.Length + 1 });
			}
		}

		/// <summary>
		/// Sends buffered lines to the remote peer (loopback test mode).
		/// </summary>
		private void SendBufferedLoopbackLines()
		{
			while (mLoopbackOverflowQueue.Count > 0)
			{
				var bufferedLine = mLoopbackOverflowQueue[0];
				mLoopbackOverflowQueue.RemoveFromFront();

				if (!Send(bufferedLine.Buffer, 0, bufferedLine.Length, false))
				{
					// send queue is full
					// => store line and try again later
					mLoopbackOverflowQueue.AddToFront(bufferedLine);
					return;
				}

				// sending buffered line succeeded
				// => proceed with the next buffered line
				ArrayPool<char>.Shared.Return(bufferedLine.Buffer);
			}
		}

		#endregion
	}

}
