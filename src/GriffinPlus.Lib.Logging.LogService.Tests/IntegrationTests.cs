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
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
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
				Sleep(200);
				var serverChannels = server.Channels;
				Assert.Equal(clientChannels.Count, serverChannels.Length);
				Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));

				// stop the server
				server.Stop(CancellationToken.None);

				// the server should have stopped now
				ExpectReachingStatus(server, LogServiceServerStatus.Stopped);
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);

				// give clients some time to finish shutting down
				Sleep(200);

				// client connections should have shut down now
				Assert.All(clientChannels, channel => Assert.Equal(LogServiceChannelStatus.ShutdownCompleted, channel.Status));
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
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
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
				await Delay(200).ConfigureAwait(false);
				var serverChannels = server.Channels;
				Assert.Equal(clientChannels.Count, serverChannels.Length);
				Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));

				// stop the server
				await server
					.StopAsync(CancellationToken.None)
					.ConfigureAwait(false);

				// the server should have stopped now
				await ExpectReachingStatusAsync(server, LogServiceServerStatus.Stopped).ConfigureAwait(false);
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);

				// give clients some time to finish shutting down
				await Delay(200).ConfigureAwait(false);

				// client connections should have shut down now
				Assert.All(clientChannels, channel => Assert.Equal(LogServiceChannelStatus.ShutdownCompleted, channel.Status));
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

			// a delay of 1000ms before accepting a connection should be sufficient to guarantee that no connection is
			// accepted before the server shuts down
			var preAcceptDelay = TimeSpan.FromMilliseconds(1000);

			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_PreAcceptDelay = preAcceptDelay
			})
			{
				// check initial server state
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
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
				Sleep((int)(preAcceptDelay.TotalMilliseconds / 10));
				var serverChannels = server.Channels;
				Assert.Empty(serverChannels);

				// stop the server
				server.Stop(CancellationToken.None);

				// the server should have stopped now
				ExpectReachingStatus(server, LogServiceServerStatus.Stopped);
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);

				// give clients some time to finish shutting down
				Sleep(200);

				// client connections should have shut down now
				Assert.All(clientChannels, channel => Assert.Equal(LogServiceChannelStatus.ShutdownCompleted, channel.Status));
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

			// a delay of 1000ms before accepting a connection should be sufficient to guarantee that no connection is
			// accepted before the server shuts down
			var preAcceptDelay = TimeSpan.FromMilliseconds(1000);

			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_PreAcceptDelay = preAcceptDelay
			})
			{
				// check initial server state
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
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
				await Delay((int)(preAcceptDelay.TotalMilliseconds / 10)).ConfigureAwait(false);
				var serverChannels = server.Channels;
				Assert.Empty(serverChannels);

				// stop the server
				await server.StopAsync(CancellationToken.None).ConfigureAwait(false);

				// the server should have stopped now
				await ExpectReachingStatusAsync(server, LogServiceServerStatus.Stopped).ConfigureAwait(false);
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);

				// give clients some time to finish shutting down
				await Delay(200).ConfigureAwait(false);

				// client connections should have shut down now
				Assert.All(clientChannels, channel => Assert.Equal(LogServiceChannelStatus.ShutdownCompleted, channel.Status));
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
				ChannelInactivityTimeout = channelInactivityTimeout
			})
			{
				// check initial server state
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				server.Start(backlog, CancellationToken.None);
				ExpectReachingStatus(server, LogServiceServerStatus.Running);

				// establish client connections to the server
				var clientChannelConnectedTime = DateTime.UtcNow;
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
				Sleep(200);
				var serverChannels = server.Channels;
				Assert.Equal(clientChannels.Count, serverChannels.Length);
				Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));

				// the client channels should stay operational for the specified time of inactivity
				while (DateTime.UtcNow < clientChannelConnectedTime + channelInactivityTimeout)
				{
					Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));
					Thread.Sleep(50);
				}

				// the channels have reached the configured time of inactivity
				// => the server should shut them down now
				// => wait some time before checking that all channels have shut down to avoid glitches
				Sleep(500);
				Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.ShutdownCompleted, channel.Status));

				// the server should have removed all channels from the channel list
				var serverChannelsAfterShutdown = server.Channels;
				Assert.Empty(serverChannelsAfterShutdown);

				// stop the server
				server.Stop(CancellationToken.None);

				// the server should have stopped now
				ExpectReachingStatus(server, LogServiceServerStatus.Stopped);
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
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
				ChannelInactivityTimeout = channelInactivityTimeout
			})
			{
				// check initial server state
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				server.Start(backlog, CancellationToken.None);
				ExpectReachingStatus(server, LogServiceServerStatus.Running);

				// establish client connections to the server
				var clientChannelConnectedTime = DateTime.UtcNow;
				var clientChannels = new List<LogServiceClientChannel>();
				for (int i = 0; i < clientCount; i++)
				{
					var channel = LogServiceClientChannel.ConnectToServer(ServerAddress, ServerPort);
					channel.HeartbeatInterval = heartbeatInterval;
					Assert.Equal(LogServiceChannelStatus.Operational, channel.Status);
					Assert.Equal(DefaultSendQueueSize, channel.SendQueueSize);
					clientChannels.Add(channel);
				}

				// give the server some time to accept all client connections
				// => there should be as many server channels as client channels now and all channels should be operational
				Sleep(200);
				var serverChannels = server.Channels;
				Assert.Equal(clientChannels.Count, serverChannels.Length);
				Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));

				// the client channels should stay operational for three times the specified time of inactivity
				// (this shows that the heartbeat keeps the channels alive - other tests cover shutting down after the inactivity timeout)
				while (DateTime.UtcNow < clientChannelConnectedTime + TimeSpan.FromSeconds(3 * inactivityTimeoutInSeconds))
				{
					Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));
					Thread.Sleep(50);
				}

				// now configure client channels to disable the heartbeat
				var clientChannelHeartbeatDisabledTime = DateTime.UtcNow;
				foreach (var channel in clientChannels)
				{
					channel.HeartbeatInterval = TimeSpan.Zero;
				}

				// the client channels should still stay operational for the configured time of inactivity
				// (worst case: a channel was due to send a heartbeat => reduce the time the channels are expected to be alive by that time)
				while (DateTime.UtcNow < clientChannelHeartbeatDisabledTime + channelInactivityTimeout - heartbeatInterval)
				{
					Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));
					Thread.Sleep(50);
				}

				// the channels have reached the configured time of inactivity
				// => the server should shut them down now
				// => wait some time before checking that all channels have shut down to avoid glitches
				//    (worst case: a channel has sent a heartbeat just before sending heartbeats was disabled => increase the time by that time)
				Sleep(TimeSpan.FromMilliseconds(500) + heartbeatInterval);
				Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.ShutdownCompleted, channel.Status));

				// the server should have removed all channels from the channel list
				var serverChannelsAfterShutdown = server.Channels;
				Assert.Empty(serverChannelsAfterShutdown);

				// stop the server
				server.Stop(CancellationToken.None);

				// the server should have stopped now
				ExpectReachingStatus(server, LogServiceServerStatus.Stopped);
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
			}
		}

		#endregion

		#region Simulate Workload

		/// <summary>
		/// Test data for simulating a random workload.
		/// </summary>
		public static IEnumerable<object[]> SimulateWorkload_TestData
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
		[MemberData(nameof(SimulateWorkload_TestData))]
		public void SimulateWorkload(int clientCount, int iterations, int lineLength)
		{
			// all client connections must fit into the backlog to avoid connections to be refused
			int backlog = clientCount;

			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_EchoReceivedData = true
			})
			{
				// check initial server state
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
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
					Sleep(1000);

					// the server should have sent a greeting (HELLO + 2x INFO with version information) and
					// the looped back greeting of the client (HELLO + INFO with version information)
					lock (receivedLines)
					{
						Assert.Equal(5, receivedLines.Count);
						Assert.Equal("HELLO Griffin+ Log Service", receivedLines[0]);
						Assert.StartsWith("INFO Server Version: ", receivedLines[1]);
						Assert.StartsWith("INFO Log Service Library Version: ", receivedLines[2]);
						Assert.Equal("HELLO Griffin+ .NET Log Service Client", receivedLines[3]);
						Assert.StartsWith("INFO Log Service Library Version: ", receivedLines[4]);
						receivedLines.Clear();
					}

					// exchange read callback (the callback checks whether received data is as expected by comparing it to a
					// deterministic set of random characters)
					const int sendOperationCount = 3;
					var receiveRandom = new Random(seed);
					char[] expected = new char[lineLength];
					int contentRegenerationCounter = 0;
					var receiveLineCount = new StrongBox<int>(0);

					void ReceiveCallback2(LogServiceChannel _, ReadOnlySpan<char> line)
					{
						// regenerate the set of expected characters after the expected number of same lines is reached
						// (each and every send operation is called with the same set of characters)
						if (contentRegenerationCounter == 0)
						{
							for (int i = 0; i < expected.Length; i++)
							{
								expected[i] = (char)receiveRandom.Next('a', 'z');
							}
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
					var sendRandom = new Random(seed);
					char[] lineToSend = new char[lineLength];
					int sentLineCount = 0;
					for (int iteration = 0; iteration < iterations; iteration++)
					{
						// prepare line of random characters
						for (int i = 0; i < lineToSend.Length; i++) lineToSend[i] = (char)sendRandom.Next('a', 'z');
						string lineAsString = new string(lineToSend, 0, lineToSend.Length);

						while (true)
						{
							try
							{
								// send line as string
								channel.Send(lineAsString, true);
								sentLineCount++;
								break;
							}
							catch (LogServiceChannelQueueFullException)
							{
								Thread.Sleep(50);
							}
						}

						while (true)
						{
							try
							{
								// send line as array of char
								channel.Send(lineToSend, 0, lineToSend.Length, true);
								sentLineCount++;
								break;
							}
							catch (LogServiceChannelQueueFullException)
							{
								Thread.Sleep(50);
							}
						}

						while (true)
						{
							try
							{
								// send line as span
								channel.Send(new ReadOnlySpan<char>(lineToSend, 0, lineToSend.Length), true);
								sentLineCount++;
								break;
							}
							catch (LogServiceChannelQueueFullException)
							{
								Thread.Sleep(50);
							}
						}
					}

					// give the channel some time to send their data and receive their looped back data
					int timeout = 2 * 60 * 1000;
					int step = 50;
					while (true)
					{
						lock (receiveLineCount)
						{
							if (receiveLineCount.Value == sentLineCount)
								break;

							Assert.True(timeout > 0, $"Timeout while waiting for all lines to receive (expected: {sentLineCount}, actual: {receiveLineCount.Value}).");
						}

						Thread.Sleep(step);
						timeout -= step;
					}

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

				// wait until all clients have completed (they are still operational)
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

				// give clients some time to finish shutting down
				Sleep(200);

				// client connections should have shut down now
				Assert.All(clientChannels, channel => Assert.Equal(LogServiceChannelStatus.ShutdownCompleted, channel.Status));
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
		private void ExpectReachingStatus(LogServiceServer server, LogServiceServerStatus status, int timeout = 0)
		{
			const int step = 50;
			while (timeout >= 0)
			{
				if (server.Status == status) return;
				Assert.True(timeout > 0);
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
		private async Task ExpectReachingStatusAsync(LogServiceServer server, LogServiceServerStatus status, int timeout = 0)
		{
			const int step = 50;
			while (timeout >= 0)
			{
				if (server.Status == status) return;
				Assert.True(timeout > 0);
				await Task.Delay(step).ConfigureAwait(false);
				timeout -= step;
			}
		}

		/// <summary>
		/// Tests whether the specified channel completes sending within the specified time.
		/// </summary>
		/// <param name="channel">Channel to wait for to complete sending.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		private void ExpectSendingToComplete(LogServiceChannel channel, int timeout = 0)
		{
			const int step = 50;
			while (timeout >= 0)
			{
				if (channel.BytesQueuedToSend == 0) return;
				Assert.True(timeout > 0);
				Thread.Sleep(step);
				timeout -= step;
			}
		}

		/// <summary>
		/// Tests whether the specified channel completes sending within the specified time.
		/// </summary>
		/// <param name="channel">Channel to wait for to complete sending.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		private async Task ExpectSendingToCompleteAsync(LogServiceChannel channel, int timeout = 0)
		{
			const int step = 50;
			while (timeout >= 0)
			{
				if (channel.BytesQueuedToSend == 0) return;
				Assert.True(timeout > 0);
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
			TimeSpan step = TimeSpan.FromMilliseconds(50);
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
			TimeSpan step = TimeSpan.FromMilliseconds(50);
			while (time > TimeSpan.Zero)
			{
				await Task.Delay(step).ConfigureAwait(false);
				time -= step;
			}
		}

		#endregion
	}

}
