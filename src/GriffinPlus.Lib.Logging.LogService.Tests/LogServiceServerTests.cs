///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Unit tests targeting the <see cref="LogServiceServer"/> class.
	/// </summary>
	[Collection("LogServiceTests")]
	public class LogServiceServerTests
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

		#region Construction

		/// <summary>
		/// Tests creating a new instance using <see cref="LogServiceServer(IPAddress, int)"/>.
		/// </summary>
		[Fact]
		public void Create()
		{
			using (var server = new LogServiceServer(ServerAddress, ServerPort))
			{
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
			}
		}

		#endregion

		#region Start/Stop[Async]()

		/// <summary>
		/// Test data for tests starting and stopping the server.
		/// </summary>
		public static IEnumerable<object[]> StartAndStop_TestData
		{
			get
			{
				var startupDelay = TimeSpan.FromMilliseconds(100);
				var shutdownDelay = TimeSpan.FromMilliseconds(100);

				foreach (int backlog in new[] { 1, 10 })
				{
					yield return new object[] { backlog, startupDelay, shutdownDelay, -1 };   // infinite timeout
					yield return new object[] { backlog, startupDelay, shutdownDelay, 0 };    // no timeout
					yield return new object[] { backlog, startupDelay, shutdownDelay, 50 };   // timeout too small to spin up completely
					yield return new object[] { backlog, startupDelay, shutdownDelay, 1000 }; // timeout large enough to spin up
				}
			}
		}

		/// <summary>
		/// Tests starting the server using <see cref="LogServiceServer.Start"/> and stopping it using <see cref="LogServiceServer.Stop"/>.
		/// </summary>
		/// <param name="backlog">Maximum length of the pending connections queue.</param>
		/// <param name="startupDelay">Delay to induce when starting the server (for testing timeout behavior).</param>
		/// <param name="shutdownDelay">Delay to induce when stopping the server (for testing timeout behavior).</param>
		/// <param name="timeout">Timeout (in ms).</param>
		[Theory]
		[MemberData(nameof(StartAndStop_TestData))]
		public void StartAndStop(
			int      backlog,
			TimeSpan startupDelay,
			TimeSpan shutdownDelay,
			int      timeout)
		{
			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_StartupDelay = startupDelay,
				TestMode_ShutdownDelay = shutdownDelay
			})
			{
				// check initial server state
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				using (var cts = new CancellationTokenSource(timeout))
				{
					if (timeout == 0 || timeout > 0 && timeout <= startupDelay.TotalMilliseconds)
					{
						// cancellation token is signaled immediately
						// => server starts spinning up, but waiting for it to finish times out
						Assert.ThrowsAny<OperationCanceledException>(
							() =>
							{
								server.Start(backlog, cts.Token);
							});
					}
					else
					{
						// timeout is large enough to wait for the accepting task to spin up
						server.Start(backlog, cts.Token);
					}
				}

				// regardless of timeouts, the accepting task should spin up
				// (its state should be WaitingForActivation as it should be blocked in the accept operation now)
				ExpectReachingStatus(server, LogServiceServerStatus.Running);
				Assert.Equal(TaskStatus.WaitingForActivation, server.AcceptingTask.Status);

				// stop the server
				using (var cts = new CancellationTokenSource(timeout))
				{
					if (timeout == 0 || timeout > 0 && timeout <= shutdownDelay.TotalMilliseconds)
					{
						// cancellation token is signaled immediately
						Assert.ThrowsAny<OperationCanceledException>(
							() =>
							{
								server.Stop(cts.Token);
							});
					}
					else
					{
						// infinite timeout or sufficiently large timeout
						server.Stop(cts.Token);
					}
				}

				// regardless of timeouts the server should have stopped now
				ExpectReachingStatus(server, LogServiceServerStatus.Stopped);
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
			}
		}

		/// <summary>
		/// Tests starting the server using <see cref="LogServiceServer.StartAsync"/> and stopping it using <see cref="LogServiceServer.StopAsync"/>
		/// </summary>
		/// <param name="backlog">Maximum length of the pending connections queue.</param>
		/// <param name="startupDelay">Delay to induce when starting the server (for testing timeout behavior).</param>
		/// <param name="shutdownDelay">Delay to induce when stopping the server (for testing timeout behavior).</param>
		/// <param name="timeout">Timeout (in ms).</param>
		[Theory]
		[MemberData(nameof(StartAndStop_TestData))]
		public async Task StartAndStopAsync(
			int      backlog,
			TimeSpan startupDelay,
			TimeSpan shutdownDelay,
			int      timeout)
		{
			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_StartupDelay = startupDelay,
				TestMode_ShutdownDelay = shutdownDelay
			})
			{
				// check initial server state
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				using (var cts = new CancellationTokenSource(timeout))
				{
					if (timeout == 0 || timeout > 0 && timeout <= startupDelay.TotalMilliseconds)
					{
						// cancellation token is signaled immediately
						// => server starts spinning up, but waiting for it to finish times out
						await Assert.ThrowsAnyAsync<OperationCanceledException>(
								async () =>
								{
									await server.StartAsync(backlog, cts.Token).ConfigureAwait(false);
								})
							.ConfigureAwait(false);
					}
					else
					{
						// timeout is large enough to wait for the accepting task to spin up
						await server.StartAsync(backlog, cts.Token).ConfigureAwait(false);
					}
				}

				// wait for the accepting task to spin up
				// (its state should be WaitingForActivation as it should be blocked in the accept operation now)
				ExpectReachingStatus(server, LogServiceServerStatus.Running);
				Assert.Equal(TaskStatus.WaitingForActivation, server.AcceptingTask.Status);

				// stop the server
				using (var cts = new CancellationTokenSource(timeout))
				{
					if (timeout == 0 || timeout > 0 && timeout <= shutdownDelay.TotalMilliseconds)
					{
						// cancellation token is signaled immediately
						await Assert.ThrowsAnyAsync<OperationCanceledException>(
								async () =>
								{
									await server.StopAsync(cts.Token).ConfigureAwait(false);
								})
							.ConfigureAwait(false);
					}
					else
					{
						// infinite timeout or sufficiently large timeout
						await server.StopAsync(cts.Token).ConfigureAwait(false);
					}
				}

				// regardless of timeouts the server should have stopped now
				ExpectReachingStatus(server, LogServiceServerStatus.Stopped);
				Assert.Equal(TaskStatus.RanToCompletion, server.AcceptingTask.Status);
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
		private void ExpectReachingStatus(LogServiceServer server, LogServiceServerStatus status, int timeout = 10000)
		{
			var watch = new Stopwatch();
			watch.Start();
			do
			{
				if (server.Status == status) return;
				Assert.True(watch.ElapsedMilliseconds < timeout);
				Thread.Sleep(50);
			} while (watch.ElapsedMilliseconds < timeout);
		}

		#endregion
	}

}
