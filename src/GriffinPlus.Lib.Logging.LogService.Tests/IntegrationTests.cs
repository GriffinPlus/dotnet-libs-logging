///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Integration tests targeting log service components.
	/// </summary>
	[Collection("LogServiceTests")]
	public class IntegrationTests
	{
		/// <summary>
		/// The IP address the server listens to.
		/// Should always be the loopback adapter.
		/// </summary>
		public static readonly IPAddress ServerAddress = IPAddress.Loopback;

		/// <summary>
		/// The TCP port the server should listen to
		/// (use framework specific server port to allow tests for different frameworks to run in parallel).
		/// </summary>
#if NETCOREAPP2_1
		public static readonly int ServerPort = 5000;
#elif NETCOREAPP3_1
		public static readonly int ServerPort = 5001;
#elif NET5_0
		public static readonly int ServerPort = 5002;
#elif NET461
		public static readonly int ServerPort = 5003;
#else
#error Unhandled target framework
#endif

		/// <summary>
		/// The default size of the send queue of a log service channel.
		/// </summary>
		private const int DefaultSendQueueSize = 10 * 1024 * 1024;

		/// <summary>
		/// Test data for starting and stopping a server with connected clients.
		/// </summary>
		public static IEnumerable<object[]> StartAndStopServer_TestData
		{
			get
			{
				foreach (int clients in new[] { 1, 2, 10, 50 })
				{
					yield return new object[] { clients };
				}
			}
		}

		#region Start and Stop Server (All Client Connections Are Accepted)

		/// <summary>
		/// Tests starting a <see cref="LogServiceServer"/>, connecting multiple client channels to it and shutting the server down.
		/// Client channels have enough time to establish a connection to the server before the server shuts down.
		/// This test uses synchronous methods only.
		/// </summary>
		/// <param name="clientCount">Number of clients that should connect to the server.</param>
		[Theory]
		[MemberData(nameof(StartAndStopServer_TestData))]
		public void StartAndStop_AllClientConnectionsAreAccepted(int clientCount)
		{
			// all client connections must fit into the backlog to avoid connections to be refused
			int backlog = clientCount;

			using (var server = new LogServiceServer(ServerAddress, ServerPort))
			{
				// check initial server state
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				server.Start(backlog, CancellationToken.None);
				ExpectReachingStatus(server, LogServiceServerStatus.Running);

				// establish client connections to the server
				var clientChannels = new List<LogServiceClientChannel>();
				for (int i = 0; i < clientCount; i++)
				{
					var channel = LogServiceClientChannel.ConnectToServer(ServerAddress, ServerPort);
					Assert.Equal(LogServiceChannelStatus.Operational, channel.Status);
					Assert.Equal(DefaultSendQueueSize, channel.SendQueueSize);
					clientChannels.Add(channel);
				}

				// give the server some time to accept all client connections
				// => there should be as many server channels as client channels now and all channels should be operational
				var serverChannels = ExpectClientsToConnect(server, clientChannels.Count);
				Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));
				Assert.All(clientChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));

				// stop the server
				server.Stop(CancellationToken.None);

				// the server should have stopped now
				ExpectReachingStatus(server, LogServiceServerStatus.Stopped);

				// client channels should shut down now
				ExpectClientsToShutDown(clientChannels);
			}
		}

		/// <summary>
		/// Tests starting a <see cref="LogServiceServer"/>, connecting multiple client channels to it and shutting the server down.
		/// Client channels have enough time to establish a connection to the server before the server shuts down.
		/// This test uses asynchronous methods where possible.
		/// </summary>
		/// <param name="clientCount">Number of clients that should connect to the server.</param>
		[Theory]
		[MemberData(nameof(StartAndStopServer_TestData))]
		public async Task StartAndStopAsync_AllClientsCanConnect(int clientCount)
		{
			// all client connections must fit into the backlog to avoid connections to be refused
			int backlog = clientCount;

			using (var server = new LogServiceServer(ServerAddress, ServerPort))
			{
				// check initial server state
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				await server.StartAsync(backlog, CancellationToken.None).ConfigureAwait(false);
				await ExpectReachingStatusAsync(server, LogServiceServerStatus.Running).ConfigureAwait(false);

				// establish client connections to the server
				var clientChannels = new List<LogServiceClientChannel>();
				for (int i = 0; i < clientCount; i++)
				{
					var channel = await LogServiceClientChannel
						              .ConnectToServerAsync(ServerAddress, ServerPort)
						              .ConfigureAwait(false);
					Assert.Equal(LogServiceChannelStatus.Operational, channel.Status);
					Assert.Equal(DefaultSendQueueSize, channel.SendQueueSize);
					clientChannels.Add(channel);
				}

				// give the server some time to accept all client connections
				// => there should be as many server channels as client channels now and all channels should be operational
				var serverChannels = await ExpectClientsToConnectAsync(server, clientChannels.Count).ConfigureAwait(false);
				Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));
				Assert.All(clientChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));

				// stop the server
				await server
					.StopAsync(CancellationToken.None)
					.ConfigureAwait(false);

				// the server should have stopped now
				await ExpectReachingStatusAsync(server, LogServiceServerStatus.Stopped).ConfigureAwait(false);

				// client channels should shut down now
				await ExpectClientsToShutDownAsync(clientChannels).ConfigureAwait(false);
			}
		}

		#endregion

		#region Start and Stop Server (Client Connections Not Accepted Before Shutting Down)

		/// <summary>
		/// Tests starting a <see cref="LogServiceServer"/>, connecting multiple client channels to it and shutting the server down.
		/// The server delays accepting, so client channels can connect to the server socket, but the connection remains
		/// in the backlog of the listener. The server shuts down before a connection can actually be accepted.
		/// Client channels are expected to shut down due to their dead peer detection.
		/// This test uses synchronous methods only.
		/// </summary>
		/// <param name="clientCount">Number of clients that should connect to the server.</param>
		[Theory]
		[MemberData(nameof(StartAndStopServer_TestData))]
		public void StartAndStopServer_ClientConnectionsNotAcceptedBeforeShuttingDown(int clientCount)
		{
			// all client connections must fit into the backlog to avoid connections to be refused
			int backlog = clientCount;

			// a delay of 60s before accepting a connection should be sufficient to guarantee that no connection is
			// accepted before the server shuts down
			var preAcceptDelay = TimeSpan.FromSeconds(60);

			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_PreAcceptDelay = preAcceptDelay
			})
			{
				// check initial server state
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				server.Start(backlog, CancellationToken.None);
				ExpectReachingStatus(server, LogServiceServerStatus.Running);

				// establish client connections to the server
				var clientChannels = new List<LogServiceClientChannel>();
				for (int i = 0; i < clientCount; i++)
				{
					var channel = LogServiceClientChannel.ConnectToServer(ServerAddress, ServerPort);
					Assert.Equal(LogServiceChannelStatus.Operational, channel.Status);
					Assert.Equal(DefaultSendQueueSize, channel.SendQueueSize);
					clientChannels.Add(channel);
				}

				// even after some time the server should not have accepted any connections
				// => there should be as many server channels as client channels now and all channels should be operational
				Thread.Sleep(500);
				var serverChannels = server.Channels;
				Assert.Empty(serverChannels);

				// stop the server
				server.Stop(CancellationToken.None);

				// the server should have stopped now
				ExpectReachingStatus(server, LogServiceServerStatus.Stopped);

				// client channels should shut down now
				ExpectClientsToShutDown(clientChannels);
			}
		}

		/// <summary>
		/// Tests starting a <see cref="LogServiceServer"/>, connecting multiple client channels to it and shutting the server down.
		/// The server delays accepting, so client channels can connect to the server socket, but the connection remains
		/// in the backlog of the listener. The server shuts down before a connection can actually be accepted.
		/// Client channels are expected to shut down due to their dead peer detection.
		/// This test uses asynchronous methods where possible.
		/// </summary>
		/// <param name="clientCount">Number of clients that should connect to the server.</param>
		[Theory]
		[MemberData(nameof(StartAndStopServer_TestData))]
		public async Task StartAndStopServerAsync_ClientConnectionsNotAcceptedBeforeShuttingDown(int clientCount)
		{
			// all client connections must fit into the backlog to avoid connections to be refused
			int backlog = clientCount;

			// a delay of 60s before accepting a connection should be sufficient to guarantee that no connection is
			// accepted before the server shuts down
			var preAcceptDelay = TimeSpan.FromSeconds(60);

			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_PreAcceptDelay = preAcceptDelay
			})
			{
				// check initial server state
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				await server.StartAsync(backlog, CancellationToken.None).ConfigureAwait(false);
				await ExpectReachingStatusAsync(server, LogServiceServerStatus.Running).ConfigureAwait(false);

				// establish client connections to the server
				var clientChannels = new List<LogServiceClientChannel>();
				for (int i = 0; i < clientCount; i++)
				{
					var channel = await LogServiceClientChannel.ConnectToServerAsync(ServerAddress, ServerPort).ConfigureAwait(false);
					Assert.Equal(LogServiceChannelStatus.Operational, channel.Status);
					Assert.Equal(DefaultSendQueueSize, channel.SendQueueSize);
					clientChannels.Add(channel);
				}

				// even after some time the server should not have accepted any connections
				// => there should be as many server channels as client channels now and all channels should be operational
				// (there is a tiny timespan between setting the channel to 'ShutdownCompleted' and removing the channel
				// from the channel list, so wait some time before testing)
				await Delay(500).ConfigureAwait(false);
				var serverChannels = server.Channels;
				Assert.Empty(serverChannels);

				// stop the server
				await server.StopAsync(CancellationToken.None).ConfigureAwait(false);

				// the server should have stopped now
				await ExpectReachingStatusAsync(server, LogServiceServerStatus.Stopped).ConfigureAwait(false);

				// client channels should shut down now
				await ExpectClientsToShutDownAsync(clientChannels).ConfigureAwait(false);
			}
		}

		#endregion

		#region Server Should Shut Down Channels on Inactivity

		/// <summary>
		/// Tests whether <see cref="LogServiceServer"/> keeps connected inactive clients until the inactivity
		/// timeout is reached. The server should shut the channels down after the timeout.
		/// </summary>
		/// <param name="clientCount">Number of clients that should connect to the server.</param>
		/// <param name="inactivityTimeout">Time of inactivity after which client channels are shut down by the server (in seconds).</param>
		[Theory]
		[InlineData(50, 10)]
		public void ServerShouldShutDownChannelsOnInactivity(int clientCount, int inactivityTimeout)
		{
			// all client connections must fit into the backlog to avoid connections to be refused
			int backlog = clientCount;

			// let server disconnect client channels some time of inactivity
			var channelInactivityTimeout = TimeSpan.FromSeconds(inactivityTimeout);

			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				ChannelInactivityTimeout = TimeSpan.FromDays(1) // effectively disable inactivity detection at start
			})
			{
				// check initial server state
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				server.Start(backlog, CancellationToken.None);
				ExpectReachingStatus(server, LogServiceServerStatus.Running);

				// establish client connections to the server
				var clientChannels = new List<LogServiceClientChannel>();
				for (int i = 0; i < clientCount; i++)
				{
					var channel = LogServiceClientChannel.ConnectToServer(ServerAddress, ServerPort);
					channel.HeartbeatInterval = TimeSpan.Zero;
					Assert.Equal(LogServiceChannelStatus.Operational, channel.Status);
					Assert.Equal(DefaultSendQueueSize, channel.SendQueueSize);
					clientChannels.Add(channel);
				}

				// all client channels should be operational now
				Assert.All(clientChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));

				// give the server some time to accept all client connections
				// => there should be as many server channels as client channels now and all channels should be operational
				var serverChannels = ExpectClientsToConnect(server, clientChannels.Count);
				Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));

				// enable the detection of inactive channels
				server.ChannelInactivityTimeout = channelInactivityTimeout;

				// the client channels should stay operational for the specified time of inactivity
				int inactiveCount = 0;
				while (inactiveCount < serverChannels.Length)
				{
					inactiveCount = 0;
					foreach (var channel in serverChannels)
					{
						if (Environment.TickCount - channel.LastReceiveTickCount < channelInactivityTimeout.TotalMilliseconds)
						{
							Assert.Equal(LogServiceChannelStatus.Operational, channel.Status);
						}
						else
						{
							inactiveCount++;
						}
					}

					Thread.Sleep(50);
				}

				// the channels have reached the configured time of inactivity
				// => the server should shut them down now
				ExpectClientsToShutDown(clientChannels);

				// the server should have removed all channels from the channel list
				// (there is a tiny timespan between setting the channel to 'ShutdownCompleted' and removing the channel
				// from the channel list, so wait some time before testing)
				Sleep(500);
				var serverChannelsAfterShutdown = server.Channels;
				Assert.Empty(serverChannelsAfterShutdown);

				// stop the server
				server.Stop(CancellationToken.None);

				// the server should have stopped now
				ExpectReachingStatus(server, LogServiceServerStatus.Stopped);
			}
		}

		#endregion

		#region Clients Should Send Heartbeat on Inactivity

		/// <summary>
		/// Tests whether <see cref="LogServiceClientChannel"/> sends periodic heartbeats when inactive
		/// telling the server not to shut the channel down.
		/// </summary>
		/// <param name="clientCount">Number of clients that should connect to the server.</param>
		/// <param name="inactivityTimeoutInSeconds">Time of inactivity after which client channels are shut down by the server (in seconds).</param>
		/// <param name="heartbeatIntervalInSeconds">Interval between two heartbeats a client sends to the server (in seconds).</param>
		[Theory]
		[InlineData(50, 10, 1)]
		[InlineData(50, 10, 5)]
		[InlineData(50, 10, 9)]
		public void ClientsShouldSendHeartbeatOnInactivity(int clientCount, int inactivityTimeoutInSeconds, int heartbeatIntervalInSeconds)
		{
			// all client connections must fit into the backlog to avoid connections to be refused
			int backlog = clientCount;

			// let server disconnect client channels some time of inactivity
			var channelInactivityTimeout = TimeSpan.FromSeconds(inactivityTimeoutInSeconds);

			// let the client send heartbeats every x seconds
			var heartbeatInterval = TimeSpan.FromSeconds(heartbeatIntervalInSeconds);

			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				ChannelInactivityTimeout = TimeSpan.FromDays(1) // effectively disable inactivity detection at start
			})
			{
				// check initial server state
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				server.Start(backlog, CancellationToken.None);
				ExpectReachingStatus(server, LogServiceServerStatus.Running);

				// establish client connections to the server
				var clientChannels = new List<LogServiceClientChannel>();
				for (int i = 0; i < clientCount; i++)
				{
					var channel = LogServiceClientChannel.ConnectToServer(ServerAddress, ServerPort);
					channel.HeartbeatInterval = heartbeatInterval;
					Assert.Equal(LogServiceChannelStatus.Operational, channel.Status);
					Assert.Equal(DefaultSendQueueSize, channel.SendQueueSize);
					clientChannels.Add(channel);
				}

				// all client channels should be operational now
				Assert.All(clientChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));

				// give the server some time to accept all client connections
				// => there should be as many server channels as client channels now and all channels should be operational
				var serverChannels = ExpectClientsToConnect(server, clientChannels.Count);
				Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));

				// enable the detection of inactive channels
				server.ChannelInactivityTimeout = channelInactivityTimeout;

				// the client channels should stay operational for at least three times the specified time of inactivity
				// (this shows that the heartbeat keeps the channels alive - other tests cover shutting down after the inactivity timeout)
				int countdown = 3 * inactivityTimeoutInSeconds * 1000;
				int step = 50;
				while (countdown > 0)
				{
					Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));
					Thread.Sleep(step);
					countdown -= step;
				}

				// now configure client channels to disable the heartbeat
				int clientChannelHeartbeatDisabledTicks = Environment.TickCount;
				foreach (var channel in clientChannels)
				{
					channel.HeartbeatInterval = TimeSpan.Zero;
				}

				// the client channels should still stay operational for the configured time of inactivity
				// (worst case: a channel was due to send a heartbeat => reduce the time the channels are expected to be alive by that time)
				int inactiveCount = 0;
				int jitterTimeMs = 500;
				while (inactiveCount < serverChannels.Length)
				{
					inactiveCount = 0;
					foreach (var channel in serverChannels)
					{
						if (Environment.TickCount - clientChannelHeartbeatDisabledTicks < (channelInactivityTimeout - heartbeatInterval).TotalMilliseconds - jitterTimeMs)
						{
							Assert.Equal(LogServiceChannelStatus.Operational, channel.Status);
						}
						else
						{
							inactiveCount++;
						}
					}

					Thread.Sleep(50);
				}

				// the channels have reached the configured time of inactivity
				// => the server should shut them down now
				// => wait some time before checking that all channels have shut down to avoid glitches
				//    worst case: a channel has sent a heartbeat just before sending heartbeats was disabled
				//    => increase the time by that time and some time for glitches
				ExpectClientsToShutDown(clientChannels);

				// the server should have removed all channels from the channel list
				// (there is a tiny timespan between setting the channel to 'ShutdownCompleted' and removing the channel
				// from the channel list, so wait some time before testing)
				Sleep(500);
				var serverChannelsAfterShutdown = server.Channels;
				Assert.Empty(serverChannelsAfterShutdown);

				// stop the server
				server.Stop(CancellationToken.None);

				// the server should have stopped now
				ExpectReachingStatus(server, LogServiceServerStatus.Stopped);
			}
		}

		#endregion

		#region Simulate Workload (With Random Lines)

		/// <summary>
		/// Test data for simulating a random workload.
		/// </summary>
		public static IEnumerable<object[]> SimulateWorkloadWithRandomLines_TestData
		{
			get
			{
				foreach (int clients in new[] { 1, 2, 5 })
				foreach (int iterations in new[] { 1, 2, 10, 50, 100, 1000, 10000, 100000 })
				foreach (int lineLength in new[] { 1, 10, 100, 1000, 10000 })
				{
					yield return new object[] { clients, iterations, lineLength };
				}
			}
		}

		/// <summary>
		/// Tests whether a <see cref="LogServiceServer"/> can handle multiple client channels sending data.
		/// The client channels send a specific number of lines with random characters to the server and expect that
		/// the server loops them back. This test covers the network layer and basic string processing as converting
		/// UTF-8 to UTF-16 (and vice versa) as well as splitting up the stream of received data into lines.
		/// </summary>
		/// <param name="clientCount">Number of clients that should connect to the server.</param>
		/// <param name="iterations">Number of iterations (each iteration sends a random line using all send operations).</param>
		/// <param name="lineLength">Length of an exchanged line.</param>
		[Theory]
		[MemberData(nameof(SimulateWorkloadWithRandomLines_TestData))]
		public void SimulateWorkloadWithRandomLines(int clientCount, int iterations, int lineLength)
		{
			// all client connections must fit into the backlog to avoid connections to be refused
			int backlog = clientCount;

			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_EchoReceivedData = true
			})
			{
				// check initial server state
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				server.Start(backlog, CancellationToken.None);
				ExpectReachingStatus(server, LogServiceServerStatus.Running);

				// establish a client connection to the server, send random test data and test whether the server
				// loops all data back to the client
				LogServiceClientChannel ConnectToServerAndSendData(int seed)
				{
					var receivedLines = new List<string>();

					// connect to the server, but do not start reading, yet
					// (otherwise there is a good chance to miss the greeting)
					var channel = LogServiceClientChannel.ConnectToServer(
						ServerAddress,
						ServerPort,
						false,
						CancellationToken.None);

					// disable the heartbeat to prevent the channel from sending 'HEARTBEAT' commands
					// mixing up the stream of data that is looped back
					var heartbeatInterval = channel.HeartbeatInterval;
					channel.HeartbeatInterval = TimeSpan.Zero;

					// register for the 'LineReceived' event to keep track of received data
					void ReceiveCallback1(LogServiceChannel _, ReadOnlySpan<char> line)
					{
						// runs in worker thread as part of the receive operation
						lock (receivedLines)
						{
							receivedLines.Add(line.ToString());
						}
					}

					channel.LineReceived += ReceiveCallback1;

					// the 'LineReceived' is attached now, so we can't miss initial data
					// => start reading from the socket
					channel.Run();

					Assert.Equal(LogServiceChannelStatus.Operational, channel.Status);
					Assert.Equal(DefaultSendQueueSize, channel.SendQueueSize);

					// give the channels some time to send, receive and loop back data
					{
						int timeout = 60000;
						while (true)
						{
							const int step = 50;
							lock (receivedLines)
							{
								if (receivedLines.Count >= 8)
									break;
							}

							Assert.True(timeout > 0);
							Thread.Sleep(step);
							timeout -= step;
						}
					}

					// the server should have sent a greeting (HELLO + 2x INFO with version information) and
					// the looped back greeting of the client (HELLO + INFO with version information)
					lock (receivedLines)
					{
						Assert.Equal(8, receivedLines.Count);
						Assert.Equal("HELLO Griffin+ Log Service", receivedLines[0]);
						Assert.StartsWith("INFO Server Version: ", receivedLines[1]);
						Assert.StartsWith("INFO Log Service Library Version: ", receivedLines[2]);
						Assert.Equal("HELLO Griffin+ .NET Log Service Client", receivedLines[3]);
						Assert.StartsWith("INFO Log Service Library Version: ", receivedLines[4]);
						Assert.StartsWith("SET PROCESS_NAME ", receivedLines[5]);
						Assert.StartsWith("SET PROCESS_ID ", receivedLines[6]);
						Assert.StartsWith("SET APPLICATION_NAME ", receivedLines[7]);
						receivedLines.Clear();
					}

					// exchange read callback (the callback checks whether received data is as expected by comparing it to a
					// deterministic set of random characters)
					const int sendOperationCount = 3;
					char[] expected = new char[lineLength];
					int contentRegenerationCounter = 0;
					int receiveDataOffset = seed;
					var receiveLineCount = new StrongBox<int>(0);

					void ReceiveCallback2(LogServiceChannel _, ReadOnlySpan<char> line)
					{
						// regenerate the set of expected characters after the expected number of same lines is reached
						// (each and every send operation is called with the same set of characters)
						if (contentRegenerationCounter == 0)
						{
							for (int i = 0; i < expected.Length; i++) expected[i] = (char)('a' + (receiveDataOffset + i) % ('z' - 'a'));
							receiveDataOffset++;
						}

						// the received line should have the expected length
						Assert.Equal(lineLength, line.Length);

						// the characters in the line should be as expected
						Assert.True(new ReadOnlySpan<char>(expected).CompareTo(line, StringComparison.Ordinal) == 0);

						// increment number of received lines
						lock (receiveLineCount)
						{
							receiveLineCount.Value++;
						}

						// adjust content regeneration counter
						contentRegenerationCounter = (contentRegenerationCounter + 1) % sendOperationCount;
					}

					channel.LineReceived -= ReceiveCallback1;
					channel.LineReceived += ReceiveCallback2;

					// send some random lines
					char[] lineToSend = new char[lineLength];
					int sendDataOffset = seed;
					int sentLineCount = 0;
					for (int iteration = 0; iteration < iterations; iteration++)
					{
						// prepare line of random characters
						for (int i = 0; i < expected.Length; i++) lineToSend[i] = (char)('a' + (sendDataOffset + i) % ('z' - 'a'));
						sendDataOffset++;
						string lineAsString = new string(lineToSend, 0, lineToSend.Length);

						// send line as string
						while (!channel.Send(lineAsString, true)) Thread.Sleep(1);
						sentLineCount++;

						// send line as array of char
						while (!channel.Send(lineToSend, 0, lineToSend.Length, true)) Thread.Sleep(1);
						sentLineCount++;

						// send line as span
						while (!channel.Send(new ReadOnlySpan<char>(lineToSend, 0, lineToSend.Length), true)) Thread.Sleep(1);
						sentLineCount++;
					}

					// give the channel some time to send their data and receive their looped back data
					{
						int timeout = 3 * 60 * 1000;
						const int step = 50;
						while (true)
						{
							lock (receiveLineCount)
							{
								if (receiveLineCount.Value == sentLineCount)
									break;

								Assert.True(
									timeout > 0,
									$"Timeout while waiting for all lines to receive (expected: {sentLineCount}, actual: {receiveLineCount.Value}).");
							}

							Thread.Sleep(step);
							timeout -= step;
						}
					}

					// re-enable heartbeat to keep the channel alive without sending data intentionally
					channel.HeartbeatInterval = heartbeatInterval;

					return channel;
				}

				// let clients connect and send random data
				var clientTasks = new List<Task<LogServiceClientChannel>>();
				for (int i = 0; i < clientCount; i++)
				{
					int seed = i;
					clientTasks.Add(
						Task.Factory.StartNew(
							() => ConnectToServerAndSendData(seed),
							TaskCreationOptions.LongRunning));
				}

				// wait until all clients have completed
				Task.WaitAll(clientTasks.Cast<Task>().ToArray());
				var clientChannels = clientTasks.Select(task => task.Result).ToArray();
				Assert.All(clientChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));

				// there should be as many server channels as client channels now and all channels should be operational
				var serverChannels = server.Channels;
				Assert.Equal(clientChannels.Length, serverChannels.Length);
				Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));

				// stop the server
				server.Stop(CancellationToken.None);

				// the server should have stopped now
				ExpectReachingStatus(server, LogServiceServerStatus.Stopped);

				// client channels should shut down now
				ExpectClientsToShutDown(clientChannels);
			}
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Tests whether the specified server reaches a certain status within the specified time.
		/// </summary>
		/// <param name="server">Server whose <see cref="LogServiceServer.mStatus"/> should change to the specified status.</param>
		/// <param name="status">Expected status.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		private static void ExpectReachingStatus(LogServiceServer server, LogServiceServerStatus status, int timeout = 0)
		{
			const int step = 50;
			while (true)
			{
				if (server.Status == status) return;
				Assert.True(timeout > 0, $"Timeout waiting for status '{status}'.");
				Thread.Sleep(step);
				timeout -= step;
			}
		}

		/// <summary>
		/// Tests whether the specified server reaches a certain status within the specified time.
		/// </summary>
		/// <param name="server">Server whose <see cref="LogServiceServer.mStatus"/> should change to the specified status.</param>
		/// <param name="status">Expected status.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		private static async Task ExpectReachingStatusAsync(LogServiceServer server, LogServiceServerStatus status, int timeout = 0)
		{
			const int step = 50;
			while (true)
			{
				if (server.Status == status) return;
				Assert.True(timeout > 0, $"Timeout waiting for status '{status}'.");
				await Task.Delay(step).ConfigureAwait(false);
				timeout -= step;
			}
		}

		/// <summary>
		/// Tests whether the specified number of server channels is established within the specified time.
		/// </summary>
		/// <param name="server">Server that should establish connections.</param>
		/// <param name="expectedChannelCount">Expected number of channels.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		private static LogServiceServerChannel[] ExpectClientsToConnect(LogServiceServer server, int expectedChannelCount, int timeout = 180000)
		{
			const int step = 50;
			while (true)
			{
				var channels = server.Channels;
				if (channels.Length == expectedChannelCount) return channels;
				Assert.True(timeout > 0, $"Timeout waiting for all clients to connect (expected: {expectedChannelCount}, actual: {channels.Length}).");
				Thread.Sleep(step);
				timeout -= step;
			}
		}

		/// <summary>
		/// Tests whether the specified number of server channels is established within the specified time.
		/// </summary>
		/// <param name="server">Server that should establish connections.</param>
		/// <param name="expectedChannelCount">Expected number of channels.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		private static async Task<LogServiceServerChannel[]> ExpectClientsToConnectAsync(LogServiceServer server, int expectedChannelCount, int timeout = 180000)
		{
			const int step = 50;
			while (true)
			{
				var channels = server.Channels;
				if (channels.Length == expectedChannelCount) return channels;
				Assert.True(timeout > 0, $"Timeout waiting for all clients to connect (expected: {expectedChannelCount}, actual: {channels.Length}).");
				await Task.Delay(step).ConfigureAwait(false);
				timeout -= step;
			}
		}

		/// <summary>
		/// Tests whether the specified clients shut down within the specified time.
		/// </summary>
		/// <param name="channels">Channels that are expected to shut down.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		private static void ExpectClientsToShutDown(IEnumerable<LogServiceClientChannel> channels, int timeout = 180000)
		{
			const int step = 50;
			var logServiceClientChannels = channels as LogServiceClientChannel[] ?? channels.ToArray();
			while (true)
			{
				var remainingChannels = logServiceClientChannels.Where(channel => channel.Status != LogServiceChannelStatus.ShutdownCompleted).ToArray();
				if (remainingChannels.Length == 0) return;
				Assert.True(timeout > 0, $"Timeout waiting for clients to shut down (expected: {logServiceClientChannels.Count()}, actual: {remainingChannels.Length}).");
				Thread.Sleep(step);
				timeout -= step;
			}
		}

		/// <summary>
		/// Tests whether the specified clients shut down within the specified time.
		/// </summary>
		/// <param name="channels">Channels that are expected to shut down.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		private static async Task ExpectClientsToShutDownAsync(IEnumerable<LogServiceClientChannel> channels, int timeout = 180000)
		{
			const int step = 50;
			while (true)
			{
				// ReSharper disable once PossibleMultipleEnumeration
				if (channels.All(channel => channel.Status == LogServiceChannelStatus.ShutdownCompleted)) return;
				Assert.True(timeout > 0, "Timeout waiting for clients to shut down.");
				await Task.Delay(step).ConfigureAwait(false);
				timeout -= step;
			}
		}

		/// <summary>
		/// Tests whether the specified channel completes sending within the specified time.
		/// </summary>
		/// <param name="channel">Channel to wait for to complete sending.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		private static void ExpectSendingToComplete(LogServiceChannel channel, int timeout = 0)
		{
			const int step = 50;
			while (true)
			{
				if (channel.BytesQueuedToSend == 0) return;
				Assert.True(timeout > 0, "Timeout waiting for sending to complete.");
				Thread.Sleep(step);
				timeout -= step;
			}
		}

		/// <summary>
		/// Tests whether the specified channel completes sending within the specified time.
		/// </summary>
		/// <param name="channel">Channel to wait for to complete sending.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		private static async Task ExpectSendingToCompleteAsync(LogServiceChannel channel, int timeout = 0)
		{
			const int step = 50;
			while (true)
			{
				if (channel.BytesQueuedToSend == 0) return;
				Assert.True(timeout > 0, "Timeout waiting for sending to complete.");
				await Task.Delay(step).ConfigureAwait(false);
				timeout -= step;
			}
		}

		/// <summary>
		/// Sleeps the specified time.
		/// </summary>
		/// <param name="time">Time to sleep (in ms).</param>
		private static void Sleep(int time)
		{
			const int step = 50;
			while (time > 0)
			{
				Thread.Sleep(step);
				time -= step;
			}
		}

		/// <summary>
		/// Sleeps the specified time.
		/// </summary>
		/// <param name="time">Time to sleep.</param>
		private static void Sleep(TimeSpan time)
		{
			var step = TimeSpan.FromMilliseconds(50);
			while (time > TimeSpan.Zero)
			{
				Thread.Sleep(step);
				time -= step;
			}
		}

		/// <summary>
		/// Sleeps the specified time.
		/// </summary>
		/// <param name="time">Time to sleep (in ms).</param>
		private static async Task Delay(int time)
		{
			const int step = 50;
			while (time > 0)
			{
				await Task.Delay(step).ConfigureAwait(false);
				time -= step;
			}
		}

		/// <summary>
		/// Sleeps the specified time.
		/// </summary>
		/// <param name="time">Time to sleep.</param>
		private static async Task Delay(TimeSpan time)
		{
			var step = TimeSpan.FromMilliseconds(50);
			while (time > TimeSpan.Zero)
			{
				await Task.Delay(step).ConfigureAwait(false);
				time -= step;
			}
		}

		#endregion
	}

}
