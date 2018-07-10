///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://griffin.plus)
//
// Copyright 2018 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// The access point to the logging subsystem in the current application domain.
	/// </summary>
	public class Log
	{
		private static readonly object sSync = new object();
		private static Dictionary<string, LogWriter> sLogWritersByName = new Dictionary<string, LogWriter>();
		private static readonly LogMessagePool sLogMessagePool = new LogMessagePool();
		private static ILogConfiguration sLogConfiguration;
		private static ILogMessageProcessingPipelineStage sLogMessageProcessingPipeline;
		private static LogWriter sLog = GetWriter<Log>();

		/// <summary>
		/// Initializes the <see cref="Log"/> class.
		/// </summary>
		static Log()
		{
			lock (sSync) // just to prevent assertions from firing...
			{
				InitDefaultConfiguration();
			}
		}

		/// <summary>
		/// Gets the object that is used to synchronize access to shared resources in the logging subsystem.
		/// </summary>
		internal static object Sync
		{
			get { return sSync; }
		}

		/// <summary>
		/// Gets or sets the log configuration that determines the behavior of the log
		/// (if set to <c>null</c> the default log configuration with its ini-style configuration file is used).
		/// </summary>
		public static ILogConfiguration Configuration
		{
			get
			{
				return sLogConfiguration;
			}

			set
			{
				lock (sSync)
				{
					if (value != null)
					{
						sLogConfiguration = value;

						// add default settings to configuration file, if necessary
						if (SetDefaultProcessingPipelineSettings(LogMessageProcessingPipeline))
						{
							try
							{
								Configuration.Save();
							}
							catch (Exception ex)
							{
								// can easily occur, if the application does not have the permission to write to its own folder
								sLog.ForceWrite(LogLevel.Developer, "Saving log configuration failed. Exception: {0}", ex);
							}
						}

						// update log writers to comply with the new configuration
						UpdateLogWriters();
					}
					else
					{
						InitDefaultConfiguration();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the log message processing pipeline that receives any log messages written to the logging subsystem.
		/// </summary>
		public static ILogMessageProcessingPipelineStage LogMessageProcessingPipeline
		{
			get
			{
				return sLogMessageProcessingPipeline;
			}

			set
			{
				lock (sSync)
				{
					// abort, if the processing pipeline has not changed
					if (sLogMessageProcessingPipeline == value) return;

					if (value != null)
					{
						if (SetDefaultProcessingPipelineSettings(value))
						{
							try
							{
								Configuration.Save();
							}
							catch (Exception ex)
							{
								// can easily occur, if the application does not have the permission to write to its own folder
								sLog.ForceWrite(LogLevel.Developer, "Saving log configuration failed. Exception: {0}", ex);
							}
						}
					}

					Thread.MemoryBarrier();
					sLogMessageProcessingPipeline = value;
				}
			}
		}

		/// <summary>
		/// Writes the specified log message using the specified log writer at the specified level.
		/// </summary>
		/// <param name="writer">Log writer to use.</param>
		/// <param name="level">Log level to use.</param>
		/// <param name="text">Text of the log message.</param>
		internal static void WriteMessage(LogWriter writer, LogLevel level, string text)
		{
			// remove preceding and trailing line breaks
			text = text.Trim('\r', '\n');

			ILogMessageProcessingPipelineStage pipeline = LogMessageProcessingPipeline;
			if (pipeline != null)
			{
				LogMessage message = null;
				try
				{
					message = sLogMessagePool.GetMessage(
						DateTimeOffset.Now,
						Stopwatch.GetTimestamp(),
						writer,
						level,
						text);

					pipeline.Process(message);
				}
				finally
				{
					if (message != null)
					{
						sLogMessagePool.ReturnMessage(message);
					}
				}
			}
		}

		/// <summary>
		/// Gets a log writer with the specified name that can be used to write to the log.
		/// </summary>
		/// <param name="name">Name of the log writer to get.</param>
		/// <returns>The requested log writer.</returns>
		public static LogWriter GetWriter(string name)
		{
			LogWriter writer;

			sLogWritersByName.TryGetValue(name, out writer);
			if (writer == null)
			{
				lock (sSync)
				{
					if (!sLogWritersByName.TryGetValue(name, out writer))
					{
						writer = new LogWriter(name);

						// set active log level mask, if the configuration is already initialized
						if (sLogConfiguration != null)
						{
							writer.ActiveLogLevelMask = sLogConfiguration.GetActiveLogLevelMask(writer);
						}

						// replace log writer collection
						Dictionary<string, LogWriter> copy = new Dictionary<string, LogWriter>(sLogWritersByName);
						copy.Add(writer.Name, writer);
						Thread.MemoryBarrier(); // ensures everything has been actually written to memory at this point
						sLogWritersByName = copy;
					}
				}
			}

			return writer;
		}

		/// <summary>
		/// Gets a log writer for the specified type that can be used to write to the log
		/// (the full name of the type becomes the name of the log writer).
		/// </summary>
		/// <param name="type">The type whose full name is to use as the log writer name.</param>
		/// <returns>The requested log writer.</returns>
		public static LogWriter GetWriter(Type type)
		{
			return GetWriter(type.FullName);
		}

		/// <summary>
		/// Gets a log writer for the specified type that can be used to write to the log
		/// (the full name of the type becomes the name of the log writer).
		/// </summary>
		/// <typeparam name="T">The type whose full name is to use as the log writer name.</typeparam>
		/// <returns>The requested log writer.</returns>
		public static LogWriter GetWriter<T>()
		{
			return GetWriter(typeof(T).FullName);
		}

		/// <summary>
		/// Initializes the default log configuration.
		/// </summary>
		internal static void InitDefaultConfiguration()
		{
			// global logging lock is hold here...
			Debug.Assert(Monitor.IsEntered(sSync));

			if (sLogConfiguration == null)
			{
				// create and init default configuration
				LogConfiguration configuration = new LogConfiguration();
				SetDefaultProcessingPipelineSettings(LogMessageProcessingPipeline);
				Thread.MemoryBarrier(); // ensures everything has been actually written to memory at this point
				sLogConfiguration = configuration;

				// update log writers appropriately
				UpdateLogWriters();
			}
		}

		/// <summary>
		/// Updates the active log level mask of all log writers.
		/// </summary>
		private static void UpdateLogWriters()
		{
			// global logging lock is hold here...
			Debug.Assert(Monitor.IsEntered(sSync));

			foreach (var kvp in sLogWritersByName)
			{
				kvp.Value.ActiveLogLevelMask = sLogConfiguration.GetActiveLogLevelMask(kvp.Value);
			}
		}

		/// <summary>
		/// Gets the default settings of the specified processing pipeline stage and all following stages and
		/// updates the current log configuration with default processing stage settings, if the corresponding
		/// processing stage settings are not defined, yet.
		/// </summary>
		/// <param name="firstStage">First stage of the processing pipeline.</param>
		/// <returns>true, if configuration was modified; otherwise false.</returns>
		private static bool SetDefaultProcessingPipelineSettings(ILogMessageProcessingPipelineStage firstStage)
		{
			// global logging lock is hold here...
			Debug.Assert(Monitor.IsEntered(Sync));

			// abort, if the first stage is not defined...
			if (firstStage == null) {
				return false;
			}

			// get all stages of the processing pipeline recursively
			HashSet<ILogMessageProcessingPipelineStage> allStages = new HashSet<ILogMessageProcessingPipelineStage>();
			firstStage.GetAllStages(allStages);

			// retrieve default settings from all stages and populate the configuration accordingly
			bool modified = false;
			foreach (var stage in allStages)
			{
				// get default settings
				IDictionary<string, string> defaultSettings = stage.GetDefaultSettings();

				// update missing settings in configuration
				bool stageSettingsModified = false;
				var ps = Configuration.GetProcessingPipelineStageSettings(stage.GetType().Name);
				Dictionary<string, string> persistentSettings = ps != null ? new Dictionary<string, string>(ps) : new Dictionary<string, string>();
				foreach (var kvp in defaultSettings.Where(x => persistentSettings.ContainsKey(x.Key)))
				{
					// add default setting to configuration
					persistentSettings.Add(kvp.Key, kvp.Value);
					stageSettingsModified = true;
					modified = true;
				}

				// update settings in configuration
				if (stageSettingsModified)
				{
					Configuration.SetProcessingPipelineStageSettings(stage.GetType().Name, persistentSettings);
				}
			}

			return modified;
		}

	}
}

