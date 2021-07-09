///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

using Xunit;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Unit tests targeting the <see cref="LogServicePipelineStage"/> class.
	/// </summary>
	[Collection("LogServiceTests")]
	public class LogServicePipelineStageTests
	{
		#region Construction

		/// <summary>
		/// Tests creating a new instance using <see cref="LogServicePipelineStage(string)"/>.
		/// </summary>
		[Fact]
		public void Create_Default()
		{
			var ipAddress = IPAddress.Parse("127.0.0.1");
			var port = 6500;
			var endpoint = new IPEndPoint(ipAddress, port);

			var stage = new LogServicePipelineStage("Test");

			Create_Common(stage, endpoint);
		}

		/// <summary>
		/// Tests creating a new instance using <see cref="LogServicePipelineStage(string, IPAddress, int)"/>.
		/// </summary>
		[Fact]
		public void Create_WithIPAddressAndPort()
		{
			var ipAddress = IPAddress.Parse("1.2.3.4");
			var port = 1234;
			var endpoint = new IPEndPoint(ipAddress, port);

			var stage = new LogServicePipelineStage("Test", ipAddress, port);

			Create_Common(stage, endpoint);
		}

		/// <summary>
		/// Tests creating a new instance using <see cref="LogServicePipelineStage(string, IPEndPoint)"/>.
		/// </summary>
		[Fact]
		public void Create_WithIPEndPoint()
		{
			var ipAddress = IPAddress.Parse("1.2.3.4");
			var port = 1234;
			var endpoint = new IPEndPoint(ipAddress, port);

			var stage = new LogServicePipelineStage("Test", endpoint);

			Create_Common(stage, endpoint);
		}

		private static void Create_Common(LogServicePipelineStage stage, IPEndPoint endpoint)
		{
			// Common stage properties
			Assert.False(stage.IsInitialized);
			Assert.False(stage.IsDefaultStage);
			Assert.Equal("Test", stage.Name);
			Assert.Empty(stage.NextStages);
			Assert.Empty(stage.Settings);

			// LogServicePipelineStage specific properties
			Assert.Equal(endpoint, stage.ServiceEndpoint);
			Assert.False(stage.IsOperational);
			Assert.True(stage.AutoReconnect);
			Assert.Equal(TimeSpan.FromSeconds(1), stage.ConnectOnStartupTimeout);
			Assert.Equal(TimeSpan.FromSeconds(15), stage.AutoReconnectRetryInterval);
			Assert.True(stage.StoringMessagesPersistently);
		}

		#endregion

		#region ConnectOnStartupTimeout

		[Theory]
		[InlineData(0)] // disable connecting at startup
		[InlineData(500)] // wait up to 500ms to connect
		public void ConnectOnStartupTimeout_Success(int milliseconds)
		{
			var timespan = TimeSpan.FromMilliseconds(milliseconds);
			var stage = new LogServicePipelineStage("Test") { ConnectOnStartupTimeout = timespan };
			Assert.Equal(timespan, stage.ConnectOnStartupTimeout);
		}

		[Theory]
		[InlineData(-1)] // negative timespan is invalid
		public void ConnectOnStartupTimeout_ArgumentOutOfRangeException(int milliseconds)
		{
			var timespan = TimeSpan.FromMilliseconds(milliseconds);
			var stage = new LogServicePipelineStage("Test");
			Assert.Throws<ArgumentOutOfRangeException>(() => stage.ConnectOnStartupTimeout = timespan);
		}

		#endregion

		#region AutoReconnect

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void AutoReconnect_Success(bool value)
		{
			var stage = new LogServicePipelineStage("Test") { AutoReconnect = value };
			Assert.Equal(value, stage.AutoReconnect);
		}

		#endregion

		#region AutoReconnectRetryInterval

		[Theory]
		[InlineData(0)]   // try to re-connect immediately
		[InlineData(500)] // try to re-connect after 500ms
		public void AutoReconnectRetryInterval_Success(int milliseconds)
		{
			var timespan = TimeSpan.FromMilliseconds(milliseconds);
			var stage = new LogServicePipelineStage("Test") { AutoReconnectRetryInterval = timespan };
			Assert.Equal(timespan, stage.AutoReconnectRetryInterval);
		}

		[Theory]
		[InlineData(-1)] // negative timespan is invalid
		public void AutoReconnectRetryInterval_ArgumentOutOfRangeException(int milliseconds)
		{
			var timespan = TimeSpan.FromMilliseconds(milliseconds);
			var stage = new LogServicePipelineStage("Test");
			Assert.Throws<ArgumentOutOfRangeException>(() => stage.AutoReconnectRetryInterval = timespan);
		}

		#endregion

		#region StoringMessagesPersistently

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void StoringMessagesPersistently_Success(bool value)
		{
			var stage = new LogServicePipelineStage("Test") { StoringMessagesPersistently = value };
			Assert.Equal(value, stage.StoringMessagesPersistently);
		}

		#endregion

		#region Initialize and Shutdown

		public static IEnumerable<object[]> InitializeAndShutdown_TestData
		{
			get
			{
				foreach (TimeSpan connectAtStartupTimeout in new[] { TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1) })
				foreach (bool autoReconnect in new[] { false, true })
				foreach (TimeSpan autoReconnectInterval in new[] { TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1) })
				foreach (bool storingMessagesPersistently in new[] { false, true }) 
				{
					yield return new object[]
					{
						connectAtStartupTimeout,
						autoReconnect,
						autoReconnectInterval,
						storingMessagesPersistently
					};
				}
			}
		}

		/// <summary>
		/// Initializes the stage with different settings, lets the stage run for some time and shuts it down at the end.
		/// There is no service running at the expected endpoint.
		/// </summary>
		/// <param name="connectAtStartupTimeout">Time to wait for connecting to the log service at startup.</param>
		/// <param name="autoReconnect">Determines whether automatic reconnecting on connection-loss is active.</param>
		/// <param name="autoReconnectInterval">The interval between two attempts to re-connect to the log service.</param>
		/// <param name="storingMessagesPersistently">Determines whether the log service should store messages persistently.</param>
		[Theory]
		[MemberData(nameof(InitializeAndShutdown_TestData))]
		public void InitializeAndShutdown_ServiceNotRunning(
			TimeSpan connectAtStartupTimeout,
			bool     autoReconnect,
			TimeSpan autoReconnectInterval,
			bool     storingMessagesPersistently)
		{
			var stage = new LogServicePipelineStage("Test", IPAddress.Loopback, 65000) // there should be no service running
			{
				ConnectOnStartupTimeout = connectAtStartupTimeout,
				StoringMessagesPersistently = storingMessagesPersistently,
				AutoReconnect = autoReconnect,
				AutoReconnectRetryInterval = autoReconnectInterval
			};

			// initialize the stage
			((IProcessingPipelineStage)stage).Initialize();
			Assert.True(stage.IsInitialized);
			Assert.False(stage.IsOperational);
			Assert.Equal(autoReconnect, stage.AutoReconnect);
			Assert.Equal(connectAtStartupTimeout, stage.ConnectOnStartupTimeout);
			Assert.Equal(autoReconnectInterval, stage.AutoReconnectRetryInterval);
			Assert.Equal(storingMessagesPersistently, stage.StoringMessagesPersistently);

			// wait for some time before shutting down
			Thread.Sleep(2000);

			// shut the stage down
			((IProcessingPipelineStage)stage).Shutdown();
			Assert.False(stage.IsInitialized);
		}

		#endregion

		#region Disconnect()

		[Fact]
		public void Disconnect_NotConnected()
		{
			var stage = new LogServicePipelineStage("Test", IPAddress.Loopback, 65000); // there should be no service running

			// initialize the stage
			((IProcessingPipelineStage)stage).Initialize();
			Assert.True(stage.IsInitialized);
			Assert.False(stage.IsOperational);

			// try to disconnect (although not connected, should not throw exceptions)
			stage.Disconnect();

			// shut the stage down
			((IProcessingPipelineStage)stage).Shutdown();
			Assert.False(stage.IsInitialized);

		}

		#endregion

	}

}
