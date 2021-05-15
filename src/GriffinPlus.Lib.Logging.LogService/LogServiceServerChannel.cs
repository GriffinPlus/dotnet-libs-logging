///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;

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
		/// Indicates whether received data is discarded (for testing purposes only).
		/// </summary>
		private bool mDiscardReceivedData;

		/// <summary>
		/// Indicates whether the channel seems to be inactive
		/// (used by <see cref="LogServiceServer"/> to delay shutting down inactive channels).
		/// </summary>
		internal volatile bool SeemsToBeInactive;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceServerChannel"/> class.
		/// </summary>
		/// <param name="server">The <see cref="LogServiceServer"/> the channel belongs to.</param>
		/// <param name="socket">A TCP socket representing the connection between the client and the server of the log service.</param>
		internal LogServiceServerChannel(LogServiceServer server, Socket socket) :
			base(socket)
		{
			mServer = server;

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

			// enable looping back or discarding received data, if configured
			mIsLoopbackEnabled = mServer.TestMode_EchoReceivedData;
			mDiscardReceivedData = mServer.TestMode_DiscardReceivedData;
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
		/// Is called when the channel has completed shutting down.
		/// (the executing thread holds the channel lock (<see cref="LogServiceChannel.Sync"/>) when called).
		/// </summary>
		protected override void OnShutdownCompleted()
		{
			mServer?.ProcessChannelHasCompletedShuttingDown(this);
		}

		/// <summary>
		/// Is called directly after some data has been received successfully.
		/// The executing thread holds the channel lock (<see cref="LogServiceChannel.Sync"/>) when called.
		/// </summary>
		protected override void OnDataReceived()
		{
			// the channels has received some data
			// => it cannot be inactive...
			SeemsToBeInactive = false;
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

			// discard data, if test requires it
			if (mDiscardReceivedData)
				return;

			// loop back data, if the server should loop back data as part of a test
			if (mIsLoopbackEnabled)
			{
				while (!Send(line, true)) { }
			}

			// TODO: Process commands here...
		}
	}

}
