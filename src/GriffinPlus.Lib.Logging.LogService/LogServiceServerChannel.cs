///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// The server side of a log service connection.
	/// </summary>
	public sealed class LogServiceServerChannel : LogServiceChannel
	{
		private readonly LogServiceServer mServer;
		private readonly Queue<string>    mLoopbackOverflowBuffer = new Queue<string>();

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
		/// Gets the list node that is used by <see cref="LogServiceServer"/> to organize channels.
		/// </summary>
		internal LinkedListNode<LogServiceServerChannel> Node { get; }

		/// <summary>
		/// Starts the channel.
		/// </summary>
		protected internal override void Start()
		{
			lock (Sync)
			{
				// let the base class start receiving
				base.Start();

				// send greeting, if the channel is operational now
				if (Status == LogServiceChannelStatus.Operational)
					SendGreeting();
			}
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
		/// Due to pipelined asynchronous receiving data may arrive out of order.
		/// The executing thread holds the channel lock (<see cref="LogServiceChannel.Sync"/>) when called.
		/// </summary>
		protected override void OnDataReceived(ReadOnlySpan<byte> data)
		{
			// ReSharper disable once InconsistentlySynchronizedField
			mServer.ProcessChannelHasReceivedData(this);
		}

		/// <summary>
		/// Is called when the channel has received a complete line.
		/// The executing thread holds the channel lock (<see cref="LogServiceChannel.Sync"/>) when called.
		/// </summary>
		/// <param name="line">Line to process.</param>
		protected override void OnLineReceived(ReadOnlySpan<char> line)
		{
			// let the base class do its work
			base.OnLineReceived(line);

			// loop back data, if the server should loop back data as part of a test
			if (mServer.TestMode_EchoReceivedData)
			{
				while (mLoopbackOverflowBuffer.Count > 0)
				{
					try
					{
						string bufferedLine = mLoopbackOverflowBuffer.Peek();
						Send(bufferedLine, true);
						mLoopbackOverflowBuffer.Dequeue();
					}
					catch (LogServiceChannelQueueFullException)
					{
						// send queue is full
						// => store line and try again later
						mLoopbackOverflowBuffer.Enqueue(line.ToString());
						return;
					}
				}

				try
				{
					Send(line, true);
				}
				catch (LogServiceChannelQueueFullException)
				{
					// send queue is full
					// => store line and try again later
					mLoopbackOverflowBuffer.Enqueue(line.ToString());
				}
			}

			// TODO: Process commands here...
		}
	}

}
