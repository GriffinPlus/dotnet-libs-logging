﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using GriffinPlus.Lib.Logging.LogService;

namespace GriffinPlus.Lib.Logging.Demo
{

	class Program
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
		public static readonly int ServerPort = 5000;

		/// <summary>
		/// The program's entry point.
		/// </summary>
		/// <param name="args"></param>
		private static void Main(string[] args)
		{
			// Benchmarks targeting specific methods
			// -----------------------------------------------------------------------------------------------------------------
			// BenchmarkRunner.Run(typeof(LogServiceClientChannelBenchmarks));

			// -----------------------------------------------------------------------------------------------------------------
			// Benchmarks for loopback performance using different Send() methods
			// (raw networking performance, no message formatting involved)
			// -----------------------------------------------------------------------------------------------------------------
			// Flow:
			// Data -> Client -> Network -> Server
			//                                 |
			// Data <- Client <- Network <-----/
			// -----------------------------------------------------------------------------------------------------------------

			Console.WriteLine();
			Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------");
			Console.WriteLine("Benchmarks for loopback performance using different Send() methods");
			Console.WriteLine("(raw networking performance, no message formatting involved)");
			Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------");

			Console.WriteLine();
			BenchmarkRawLoopback(
				"Send(string)",
				1,
				5000000,
				1000,
				(
					channel,
					lineAsArray,
					lineAsString) => channel.Send(lineAsString));

			Console.WriteLine();
			BenchmarkRawLoopback(
				"Send(char[], int, int)",
				1,
				5000000,
				1000,
				(
					channel,
					lineAsArray,
					lineAsString) => channel.Send(
					lineAsArray,
					0,
					lineAsArray.Length,
					true));

			Console.WriteLine();
			BenchmarkRawLoopback(
				"Send(ReadOnlySpan<char>, bool)",
				1,
				5000000,
				1000,
				(
					channel,
					lineAsArray,
					lineAsString) => channel.Send(
					lineAsArray.AsSpan(),
					true));

			// -----------------------------------------------------------------------------------------------------------------
			// Benchmark for pure message writing performance
			// -----------------------------------------------------------------------------------------------------------------
			// Flow:
			// Message -> Client -> Network -> Server -> Message is discarded
			// -----------------------------------------------------------------------------------------------------------------

			Console.WriteLine();
			Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------");
			Console.WriteLine("Benchmark for pure message writing performance");
			Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------");

			Console.WriteLine();
			BenchmarkWriting(
				1,
				10000000,
				10,
				100);

			Console.WriteLine();
			Console.WriteLine("Press any key to continue...");
			Console.ReadKey();
		}

		/// <summary>
		/// Creates a log service server and configures the server to loop back any received data.
		/// Then creates the specified number of clients that send the the specified amount of data to the server
		/// and wait for the data to be looped back.
		/// </summary>
		private static void BenchmarkRawLoopback(
			string                                        sendOperationName,
			int                                           clientCount,
			int                                           iterations,
			int                                           lineLength,
			Func<LogServiceChannel, char[], string, bool> sendOperation)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			Thread.Sleep(1000);

			using (var server = new LogServiceServer(ServerAddress, ServerPort) { TestMode_EchoReceivedData = true })
			using (var startEvent = new ManualResetEventSlim())
			{
				// start the server
				// (all client connections must fit into the backlog to avoid connections to be refused)
				server.Start(clientCount, CancellationToken.None);

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

					// give the channels some time to send, receive and loop back data
					Thread.Sleep(1000);

					// the server should have sent a greeting (HELLO + 2x INFO with version information) and
					// the looped back greeting of the client (HELLO + INFO with version information)
					lock (receivedLines)
					{
						if (receivedLines.Count != 8)
							throw new Exception($"Expected to receive 5 lines during the greeting, but received {receivedLines.Count} line(s).");

						receivedLines.Clear();
					}

					// exchange read callback (the callback checks whether received data is as expected by comparing it to a
					// deterministic set of random characters)
					var receiveRandom = new Random(seed);
					char[] expected = new char[lineLength];
					for (int i = 0; i < expected.Length; i++) expected[i] = (char)receiveRandom.Next('a', 'z');
					var receiveLineCount = new StrongBox<int>(0);

					void ReceiveCallback2(LogServiceChannel _, ReadOnlySpan<char> line)
					{
						// the received line should have the expected length
						if (line.Length != lineLength)
							throw new Exception($"The received line does not have the expected length (expected: {lineLength}, received: {line.Length}).");

						// the characters in the line should be as expected
						if (new ReadOnlySpan<char>(expected).CompareTo(line, StringComparison.Ordinal) != 0)
							throw new Exception("The received line does not match the expected line.");

						// increment number of received lines
						lock (receiveLineCount)
						{
							receiveLineCount.Value++;
						}
					}

					channel.LineReceived -= ReceiveCallback1;
					channel.LineReceived += ReceiveCallback2;

					// wait for the benchmark to begin
					// ReSharper disable once AccessToDisposedClosure
					startEvent.Wait();

					// send some random lines
					var sendRandom = new Random(seed);
					char[] lineToSend = new char[lineLength];
					int sentLineCount = 0;
					for (int i = 0; i < lineToSend.Length; i++) lineToSend[i] = (char)sendRandom.Next('a', 'z');
					string lineAsString = new string(lineToSend, 0, lineToSend.Length);
					for (int iteration = 0; iteration < iterations; iteration++)
					{
						while (!sendOperation(channel, lineToSend, lineAsString))
						{
							Thread.Sleep(1);
						}

						sentLineCount++;
					}

					// give the channel some time to send their data and receive their looped back data
					int timeout = 3 * 60 * 1000;
					int step = 50;
					while (true)
					{
						lock (receiveLineCount)
						{
							if (receiveLineCount.Value == sentLineCount)
								break;

							if (timeout <= 0)
								throw new Exception($"Timeout while waiting for all lines to receive (expected: {sentLineCount}, actual: {receiveLineCount.Value}).");
						}

						Thread.Sleep(step);
						timeout -= step;
					}

					return channel;
				}

				// let clients connect and send random data
				var roundtripStopwatch = new Stopwatch();
				var clientTasks = new List<Task<LogServiceClientChannel>>();
				for (int i = 0; i < clientCount; i++)
				{
					int seed = i;
					clientTasks.Add(
						Task.Factory.StartNew(
							() => ConnectToServerAndSendData(seed),
							TaskCreationOptions.LongRunning));
				}

				// start the roundtrip time measurement
				// (let the channels exchange their greetings before starting the measurement)
				Thread.Sleep(1000);
				startEvent.Set();
				roundtripStopwatch.Start();

				// wait until all clients have completed (they are still operational)
				Task.WaitAll(clientTasks.Cast<Task>().ToArray());

				// stop the roundtrip time measurement
				roundtripStopwatch.Stop();

				// print benchmark result to console
				// (assumption: all chars are encoded in a single byte UTF-8 code unit)
				long totalCharCount = (long)clientCount * iterations * (lineLength + 1);
				double throughputInMegaBytePerSecond = (double)totalCharCount / (1024 * 1024) / (roundtripStopwatch.ElapsedMilliseconds / 1000.0);
				Console.WriteLine($"Loopback Benchmark using {sendOperationName}");
				Console.WriteLine($"  Clients: {clientCount} (local)");
				Console.WriteLine($"  Data: {iterations} lines x {lineLength} chars per line => {totalCharCount:#,##} chars/bytes");
				Console.WriteLine($"  Time: {roundtripStopwatch.ElapsedMilliseconds:0.##} ms");
				Console.WriteLine($"  Throughput: {throughputInMegaBytePerSecond:0.##} MByte/s");

				// stop the server
				server.Stop(CancellationToken.None);
			}
		}

		/// <summary>
		/// Creates a log service server and configures the server to discard any received data.
		/// Then creates the specified number of clients that send the the specified amount of data to the server.
		/// </summary>
		private static void BenchmarkWriting(
			int clientCount,
			int messageCount,
			int lineCount,
			int lineLength)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			Thread.Sleep(1000);

			using (var server = new LogServiceServer(ServerAddress, ServerPort) { TestMode_DiscardReceivedData = true })
			{
				// start the server
				// (all client connections must fit into the backlog to avoid connections to be refused)
				server.Start(clientCount, CancellationToken.None);

				// establish a client connection to the server, send random test data and test whether the server
				// loops all data back to the client
				LogServiceClientChannel ConnectToServerAndSendData(int seed)
				{
					// connect to the server, but do not start reading, yet
					// (otherwise there is a good chance to miss the greeting)
					var channel = LogServiceClientChannel.ConnectToServer(ServerAddress, ServerPort);

					// disable the heartbeat to prevent the channel from sending 'HEARTBEAT' commands
					// mixing up the stream of data that is looped back
					channel.HeartbeatInterval = TimeSpan.Zero;

					// give the channels some time to send, receive and loop back data
					Thread.Sleep(1000);

					// prepare message to send
					var textBuilder = new StringBuilder();
					for (int i = 0; i < lineCount; i++)
					{
						textBuilder.Append(new string('x', lineLength));
						textBuilder.Append('\n');
					}

					var message = new LogMessage
					{
						Timestamp = DateTimeOffset.Now,
						HighPrecisionTimestamp = Log.GetHighPrecisionTimestamp(),
						ApplicationName = "My Application",
						ProcessName = Process.GetCurrentProcess().ProcessName,
						ProcessId = Process.GetCurrentProcess().Id,
						LogWriterName = "My Log Writer",
						LogLevelName = "Note",
						Tags = new TagSet("Tag-1", "Tag-2"),
						LostMessageCount = 1,
						Text = textBuilder.ToString()
					};

					// send some messages
					for (int i = 0; i < messageCount; i++)
					{
						while (!channel.Send(message))
						{
							Thread.Sleep(1);
						}
					}

					// give the channel some time to send their data and receive their looped back data
					int timeout = 3 * 60 * 1000;
					int step = 50;
					while (true)
					{
						if (channel.BytesQueuedToSend == 0)
							break;

						if (timeout <= 0)
							throw new Exception("Timeout while waiting for sending to complete.");

						Thread.Sleep(step);
						timeout -= step;
					}

					return channel;
				}

				// let clients connect and send messages
				var roundtripStopwatch = new Stopwatch();
				var clientTasks = new List<Task<LogServiceClientChannel>>();
				for (int i = 0; i < clientCount; i++)
				{
					int seed = i;
					clientTasks.Add(
						Task.Factory.StartNew(
							() => ConnectToServerAndSendData(seed),
							TaskCreationOptions.LongRunning));
				}

				// start the roundtrip time measurement
				roundtripStopwatch.Start();

				// wait until all clients have completed (they are still operational)
				Task.WaitAll(clientTasks.Cast<Task>().ToArray());

				// stop the roundtrip time measurement
				roundtripStopwatch.Stop();

				// print benchmark result to console
				// (only the message text counts for the benchmark)
				// (assumption: all chars are encoded in a single byte UTF-8 code unit)
				long totalCharCount = (long)clientCount * messageCount * lineCount * (lineLength + 1);
				double throughputInMessagesPerSecond = messageCount / (roundtripStopwatch.ElapsedMilliseconds / 1000.0);
				double throughputInMegaBytePerSecond = (double)totalCharCount / (1024 * 1024) / (roundtripStopwatch.ElapsedMilliseconds / 1000.0);
				Console.WriteLine("Writing Benchmark using");
				Console.WriteLine($"  Clients:                                 {clientCount} (local)");
				Console.WriteLine($"  Time:                                    {roundtripStopwatch.ElapsedMilliseconds:0.##} ms");
				Console.WriteLine($"  Data (message text only):                {messageCount} messages x {lineCount} lines x {lineLength} chars per line => {totalCharCount:#,##} chars/bytes");
				Console.WriteLine($"  Throughput (Messages/s):                 {throughputInMessagesPerSecond:0.##} Messages/s");
				Console.WriteLine($"  Throughput (MByte/s, message text only): {throughputInMegaBytePerSecond:0.##} MByte/s");

				// stop the server
				server.Stop(CancellationToken.None);
			}
		}
	}

}
