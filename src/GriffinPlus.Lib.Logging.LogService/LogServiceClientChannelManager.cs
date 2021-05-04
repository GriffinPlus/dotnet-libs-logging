///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading;

// ReSharper disable ForCanBeConvertedToForeach

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// A helper that assists the <see cref="LogServiceClientChannel"/> by triggering it periodically to send heartbeats.
	/// </summary>
	static class LogServiceClientChannelManager
	{
		// processing thread control
		private static readonly object               sProcessingSync        = new object();
		private static readonly ManualResetEventSlim sProcessingNeededEvent = new ManualResetEventSlim();
		private static          bool                 sProcessingInProgress  = false;

		// processing
		private static readonly List<LogServiceClientChannel> sChannelsWithHeartbeat  = new List<LogServiceClientChannel>();
		private static readonly object                        sSync                   = new object();
		private static readonly int                           sProcessingMinimumDelay = 250; // in ms

		/// <summary>
		/// Registers the specified channel for periodic heartbeat triggers.
		/// The manager thread will periodically invoke <see cref="LogServiceClientChannel.SendHeartbeatIfDue"/>.
		/// </summary>
		/// <param name="channel">Channel that should be triggered to send heartbeats.</param>
		public static void RegisterHeartbeatTrigger(LogServiceClientChannel channel)
		{
			lock (sSync)
			{
				if (sChannelsWithHeartbeat.Contains(channel)) return;
				sChannelsWithHeartbeat.Add(channel);
				TriggerManagerThread();
			}
		}

		/// <summary>
		/// Unregisters the specified channel from periodic heartbeat triggers.
		/// </summary>
		/// <param name="channel">Channel that should not be triggered to send heartbeats any more.</param>
		public static void UnregisterHeartbeatTrigger(LogServiceClientChannel channel)
		{
			lock (sSync)
			{
				sChannelsWithHeartbeat.Remove(channel);
				TriggerManagerThread();
			}
		}

		/// <summary>
		/// Unregisters the specified channel from all periodic triggers.
		/// </summary>
		/// <param name="channel"></param>
		public static void UnregisterChannel(LogServiceClientChannel channel)
		{
			lock (sSync)
			{
				sChannelsWithHeartbeat.Remove(channel);
				TriggerManagerThread();
			}
		}

		/// <summary>
		/// Triggers the manager thread.
		/// </summary>
		private static void TriggerManagerThread()
		{
			lock (sProcessingSync)
			{
				sProcessingNeededEvent.Set();
				if (!sProcessingInProgress)
				{
					ThreadPool.QueueUserWorkItem(ThreadProc);
					sProcessingInProgress = true;
				}
			}
		}

		/// <summary>
		/// The entry point of the manager thread.
		/// </summary>
		/// <param name="_"></param>
		private static void ThreadProc(object _)
		{
			var nextRunTicksList = new List<int>();

			while (true)
			{
				// determine the time to wait for the next run
				// (the channels have notified when they expect to be triggered again, take the shortest time)
				int currentTicks = Environment.TickCount;
				int timeout = int.MaxValue;
				for (int i = 0; i < nextRunTicksList.Count; i++)
				{
					timeout = Math.Min(timeout, Math.Max(nextRunTicksList[i] - currentTicks, 0));
				}

				// do not delay less than the absolute minimum to reduce unnecessary overhead and process
				// as much as possible in a single run
				timeout = timeout > sProcessingMinimumDelay ? timeout : sProcessingMinimumDelay;

				// wait to get notified or until the calculated delay elapses before running another cycle
				sProcessingNeededEvent.Wait(timeout);
				sProcessingNeededEvent.Reset();
				nextRunTicksList.Clear();

				lock (sSync)
				{
					// abort, if there is nothing left to do...
					if (sChannelsWithHeartbeat.Count == 0)
					{
						lock (sProcessingSync)
						{
							// abort, if there is nothing to process
							if (!sProcessingNeededEvent.IsSet)
							{
								sProcessingInProgress = false;
								return;
							}
						}
					}

					// trigger sending heartbeats if due
					for (int i = 0; i < sChannelsWithHeartbeat.Count; i++)
					{
						var channel = sChannelsWithHeartbeat[i];
						nextRunTicksList.Add(channel.SendHeartbeatIfDue());
					}
				}
			}
		}
	}

}
