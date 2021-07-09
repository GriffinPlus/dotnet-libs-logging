///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Integration tests targeting the <see cref="LogServicePipelineStage"/> class.
	/// </summary>
	[Collection("LogServiceTests")]
	public class IntegrationTests : IntegrationTestsBase
	{
		#region Simulate Workload

		/// <summary>
		/// Test data for simulating a random workload.
		/// </summary>
		public static IEnumerable<object[]> SimulateWorkloadWithPipelineStage_TestData
		{
			get
			{
				foreach (int clients in new[] { 1, 5 })
				foreach (int iterations in new[] { 1, 10, 100, 1000, 10000, 100000 })
				{
					yield return new object[] { clients, iterations };
				}
			}
		}

		/// <summary>
		/// Tests whether a <see cref="LogServiceServer"/> can handle multiple clients.
		/// Multiple <see cref="LogServicePipelineStage"/> instances are used as clients to the log service.
		/// The <see cref="LogServiceServer"/> writes the messages into a log file. ?????
		/// </summary>
		/// <param name="clientCount">Number of clients that should connect to the server.</param>
		/// <param name="messageCount">Number of messages to send.</param>
		[Theory]
		[MemberData(nameof(SimulateWorkloadWithPipelineStage_TestData))]
		public void SimulateWorkload(int clientCount, int messageCount)
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

				// establish a client connection to the server, send random test data and test whether the server
				// loops all data back to the client
				LogServicePipelineStage ConnectToServerAndSendData(int seed)
				{
					// create a pipeline stage to connect to the log service
					var stage = new LogServicePipelineStage("Log Service", ServerAddress, ServerPort);

					// initialize the pipeline stage
					((IProcessingPipelineStage)stage).Initialize();

					// send some random messages
					var random = new Random(seed);
					const int maxStringLength = 128 * 1024;
					byte[] lineToSendBytes = new byte[maxStringLength];
					char[] lineToSend = new char[maxStringLength];
					var timestampOffset = DateTime.Parse("2020-01-01T00:00:00");
					var timezoneOffset = TimeSpan.FromHours(1);
					var message = new LogMessage();
					for (int messageNumber = 0; messageNumber < messageCount; messageNumber++)
					{
						// set monotonically increasing timestamp
						var step = TimeSpan.FromMilliseconds(random.NextDouble() * 1000 + 1); // [1..1001] ms
						message.Timestamp = new DateTimeOffset(timestampOffset + step, timezoneOffset);
						timestampOffset = message.Timestamp.DateTime;

						// set usual high-precision timestamp
						message.HighPrecisionTimestamp = Log.GetHighPrecisionTimestamp();

						// set application name
						int applicationNameId = random.Next(10);
						message.ApplicationName = $"Application {applicationNameId}";

						// set process name and id
						message.ProcessId = random.Next(10000, 100050);
						message.ProcessName = $"Process {message.ProcessId}";

						// set log writer name
						int writerNameId = random.Next(20);
						message.LogWriterName = $"Writer {writerNameId}";

						// set log level name
						int levelNameId = random.Next(20);
						message.LogLevelName = $"Level {levelNameId}";

						// set tags
						int tagCount = random.Next(5);
						message.Tags = TagSet.Empty;
						for (int i = 0; i < tagCount; i++) message.Tags += $"Tag-{i}";

						// generate some random text ([a-z]+) with random length (1..maxStringLength)
						int stringLength = random.Next(1, maxStringLength);
						random.NextBytes(lineToSendBytes);
						for (int i = 0; i < lineToSend.Length; i++) lineToSend[i] = (char)('a' + lineToSendBytes[i] % ('z' - 'a'));
						message.Text = new string(lineToSend, 0, stringLength);
					}

					return stage;
				}

				// let clients connect and send random data
				var clientTasks = new List<Task<LogServicePipelineStage>>();
				for (int i = 0; i < clientCount; i++)
				{
					int seed = i;
					clientTasks.Add(Task.Run(() => ConnectToServerAndSendData(seed)));
				}

				// wait until all clients have completed (they are still operational)
				Task.WaitAll(clientTasks.Cast<Task>().ToArray());
				var clientStages = clientTasks.Select(task => task.Result).ToArray();
				Assert.All(clientStages, stage => Assert.True(stage.IsOperational, "The stage should still be operational, but it is not."));

				// there should be as many server channels as pipeline stages now and all channels should be operational
				var serverChannels = server.Channels;
				Assert.Equal(clientCount, serverChannels.Length);
				Assert.All(serverChannels, channel => Assert.Equal(LogServiceChannelStatus.Operational, channel.Status));

				// stop the server
				server.Stop(CancellationToken.None);

				// the server should have stopped now
				ExpectReachingStatus(server, LogServiceServerStatus.Stopped);

				// client channels should shut down now
				ExpectStagesToDetectServiceShutdown(clientStages);
			}
		}

		#endregion

		/// <summary>
		/// Tests whether the specified pipeline stages detect that the log service is shutting down within the specified time.
		/// </summary>
		/// <param name="stages">Stages that are expected to detect that the service is shutting down.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		private static void ExpectStagesToDetectServiceShutdown(IEnumerable<LogServicePipelineStage> stages, int timeout = 180000)
		{
			const int step = 50;
			var logServicePipelineStages = stages as LogServicePipelineStage[] ?? stages.ToArray();
			while (true)
			{
				var remainingStages = logServicePipelineStages.Where(channel => channel.IsOperational).ToArray();
				if (remainingStages.Length == 0) return;
				Assert.True(timeout > 0, $"Timeout waiting for pipeline stages to detect that the service has shut down (expected: {logServicePipelineStages.Count()}, actual: {remainingStages.Length}).");
				Thread.Sleep(step);
				timeout -= step;
			}
		}
	}

}
