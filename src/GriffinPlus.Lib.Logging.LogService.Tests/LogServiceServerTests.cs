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
		/// Time the server waits at startup before creating the listener socket.
		/// </summary>
		public static readonly TimeSpan StartupDelay = TimeSpan.FromMilliseconds(5000);

		/// <summary>
		/// Time the server waits before actually shutting down.
		/// </summary>
		public static readonly TimeSpan ShutdownDelay = TimeSpan.FromMilliseconds(5000);

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
			}
		}

		#endregion

		#region Start/Stop[Async]()

		/// <summary>
		/// Tests starting the server using <see cref="LogServiceServer.Start"/> passing a pre-signaled cancellation token.
		/// The server should not start up in this case.
		/// </summary>
		/// <param name="backlog">Maximum length of the pending connections queue.</param>
		[Theory]
		[InlineData(1)]
		[InlineData(10)]
		public void Start_PreSignaledCancellationToken(int backlog)
		{
			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_StartupDelay = StartupDelay,
				TestMode_ShutdownDelay = ShutdownDelay
			})
			{
				// check initial server state
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server (cancellation token is signaled immediately)
				// => server does not spin up at all
				Assert.ThrowsAny<OperationCanceledException>(
					() =>
					{
						server.Start(backlog, new CancellationToken(true));
					});

				// the server should still be stopped
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);
			}
		}

		/// <summary>
		/// Tests starting the server using <see cref="LogServiceServer.StartAsync"/> passing a pre-signaled cancellation token.
		/// The server should not start up in this case.
		/// </summary>
		/// <param name="backlog">Maximum length of the pending connections queue.</param>
		[Theory]
		[InlineData(1)]
		[InlineData(10)]
		public async Task StartAsync_PreSignaledCancellationToken(int backlog)
		{
			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_StartupDelay = StartupDelay,
				TestMode_ShutdownDelay = ShutdownDelay
			})
			{
				// check initial server state
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server (cancellation token is signaled immediately)
				// => server does not spin up at all
				await Assert.ThrowsAnyAsync<OperationCanceledException>(
						async () =>
						{
							await server.StartAsync(backlog, new CancellationToken(true)).ConfigureAwait(false);
						})
					.ConfigureAwait(false);

				// the server should still be stopped
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);
			}
		}

		/// <summary>
		/// Tests stopping the server using <see cref="LogServiceServer.Stop"/> passing a pre-signaled cancellation token.
		/// The server should not shut down in this case.
		/// </summary>
		[Fact]
		public void Stop_PreSignaledCancellationToken()
		{
			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_StartupDelay = StartupDelay,
				TestMode_ShutdownDelay = ShutdownDelay
			})
			{
				// check initial server state
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				server.Start(1, CancellationToken.None);

				// the server should be running now
				Assert.Equal(LogServiceServerStatus.Running, server.Status);

				// try to stop the server with pre-signaled cancellation token
				// => server does not shut down up at all
				Assert.ThrowsAny<OperationCanceledException>(
					() =>
					{
						server.Stop(new CancellationToken(true));
					});

				// the server should still be running
				Assert.Equal(LogServiceServerStatus.Running, server.Status);

				// now shut the server down to clean up the test...
				server.Stop(CancellationToken.None);
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);
			}
		}

		/// <summary>
		/// Tests stopping the server using <see cref="LogServiceServer.StopAsync"/> passing a pre-signaled cancellation token.
		/// The server should not shut down in this case.
		/// </summary>
		[Fact]
		public async Task StopAsync_PreSignaledCancellationToken()
		{
			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_StartupDelay = StartupDelay,
				TestMode_ShutdownDelay = ShutdownDelay
			})
			{
				// check initial server state
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				await server.StartAsync(1, CancellationToken.None).ConfigureAwait(false);

				// the server should be running now
				Assert.Equal(LogServiceServerStatus.Running, server.Status);

				// try to stop the server with pre-signaled cancellation token
				// => server does not shut down up at all
				await Assert.ThrowsAnyAsync<OperationCanceledException>(
						async () =>
						{
							await server.StopAsync(new CancellationToken(true)).ConfigureAwait(false);
						})
					.ConfigureAwait(false);

				// the server should still be running
				Assert.Equal(LogServiceServerStatus.Running, server.Status);

				// now shut the server down to clean up the test...
				await server.StopAsync(CancellationToken.None).ConfigureAwait(false);
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);
			}
		}

		/// <summary>
		/// Test data for tests starting and stopping the server.
		/// </summary>
		public static IEnumerable<object[]> StartAndStop_TestData
		{
			get
			{

				foreach (int backlog in new[] { 1, 10 })
				{
					yield return new object[] { backlog, -1 };    // infinite timeout
					yield return new object[] { backlog, 500 };   // timeout too small to spin up completely
					yield return new object[] { backlog, 10000 }; // timeout large enough to spin up
				}
			}
		}

		/// <summary>
		/// Tests starting the server using <see cref="LogServiceServer.Start"/> and stopping it using <see cref="LogServiceServer.Stop"/>.
		/// </summary>
		/// <param name="backlog">Maximum length of the pending connections queue.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		[Theory]
		[MemberData(nameof(StartAndStop_TestData))]
		public void StartAndStop(int backlog, int timeout)
		{
			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_StartupDelay = StartupDelay,
				TestMode_ShutdownDelay = ShutdownDelay
			})
			{
				// check initial server state
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				using (var cts = new CancellationTokenSource(timeout))
				{
					if (timeout > 0 && timeout <= StartupDelay.TotalMilliseconds)
					{
						// cancellation token is signaled before the server is actually running
						// => waiting for it to finish times out
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

				// regardless of timeouts, the server thread should spin up
				// (its state should be WaitingForActivation as it should be blocked in the accept operation now)
				ExpectReachingStatus(server, LogServiceServerStatus.Running);

				// stop the server
				using (var cts = new CancellationTokenSource(timeout))
				{
					if (timeout > 0 && timeout <= ShutdownDelay.TotalMilliseconds)
					{
						// cancellation token is signaled before the server has completed shutting down
						// => waiting for it to finish times out
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
			}
		}

		/// <summary>
		/// Tests starting the server using <see cref="LogServiceServer.StartAsync"/> and stopping it using <see cref="LogServiceServer.StopAsync"/>
		/// </summary>
		/// <param name="backlog">Maximum length of the pending connections queue.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		[Theory]
		[MemberData(nameof(StartAndStop_TestData))]
		public async Task StartAndStopAsync(int backlog, int timeout)
		{
			using (var server = new LogServiceServer(ServerAddress, ServerPort)
			{
				TestMode_StartupDelay = StartupDelay,
				TestMode_ShutdownDelay = ShutdownDelay
			})
			{
				// check initial server state
				Assert.Equal(LogServiceServerStatus.Stopped, server.Status);

				// start the server
				using (var cts = new CancellationTokenSource(timeout))
				{
					if (timeout > 0 && timeout <= StartupDelay.TotalMilliseconds)
					{
						// cancellation token is signaled before the server is actually running
						// => waiting for it to finish times out
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

				// wait for the server thread to spin up
				ExpectReachingStatus(server, LogServiceServerStatus.Running);

				// stop the server
				using (var cts = new CancellationTokenSource(timeout))
				{
					if (timeout > 0 && timeout <= ShutdownDelay.TotalMilliseconds)
					{
						// cancellation token is signaled before the server has completed shutting down
						// => waiting for it to finish times out
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
			while (true)
			{
				if (server.Status == status) return;
				Assert.True(watch.ElapsedMilliseconds < timeout, $"The server timed out reaching status '{status}'.");
				Thread.Sleep(50);
			}
		}

		#endregion
	}

}
