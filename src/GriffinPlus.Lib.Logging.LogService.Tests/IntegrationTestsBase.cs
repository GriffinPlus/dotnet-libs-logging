///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Base class for log service integration tests providing some common helper methods and settings.
	/// </summary>
	public abstract class IntegrationTestsBase
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
		/// Tests whether the specified server reaches a certain status within the specified time.
		/// </summary>
		/// <param name="server">Server whose <see cref="LogServiceServer.mStatus"/> should change to the specified status.</param>
		/// <param name="status">Expected status.</param>
		/// <param name="timeout">Timeout (in ms).</param>
		public static void ExpectReachingStatus(LogServiceServer server, LogServiceServerStatus status, int timeout = 0)
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
		public static async Task ExpectReachingStatusAsync(LogServiceServer server, LogServiceServerStatus status, int timeout = 0)
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
		public static LogServiceServerChannel[] ExpectClientsToConnect(LogServiceServer server, int expectedChannelCount, int timeout = 180000)
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
		public static async Task<LogServiceServerChannel[]> ExpectClientsToConnectAsync(LogServiceServer server, int expectedChannelCount, int timeout = 180000)
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
		public static void ExpectClientsToShutDown(IEnumerable<LogServiceClientChannel> channels, int timeout = 180000)
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
		public static async Task ExpectClientsToShutDownAsync(IEnumerable<LogServiceClientChannel> channels, int timeout = 180000)
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
		public static void ExpectSendingToComplete(LogServiceChannel channel, int timeout = 0)
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
		public static async Task ExpectSendingToCompleteAsync(LogServiceChannel channel, int timeout = 0)
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
		public static void Sleep(int time)
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
		public static void Sleep(TimeSpan time)
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
		public static async Task Delay(int time)
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
		public static async Task Delay(TimeSpan time)
		{
			var step = TimeSpan.FromMilliseconds(50);
			while (time > TimeSpan.Zero)
			{
				await Task.Delay(step).ConfigureAwait(false);
				time -= step;
			}
		}
	}

}
