///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018-2020 Sascha Falk <sascha@falk-online.eu>
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
		private static readonly int sProcessId = Process.GetCurrentProcess().Id;
		private static readonly string sProcessName = Process.GetCurrentProcess().ProcessName;
		private static readonly LocalLogMessagePool sLogMessagePool = new LocalLogMessagePool();
		private static List<LogWriter> sLogWritersById = new List<LogWriter>();
		private static Dictionary<string, LogWriter> sLogWritersByName = new Dictionary<string, LogWriter>();
		private static ILogConfiguration sLogConfiguration;
		private static volatile IProcessingPipelineStage sLogMessageProcessingPipeline;
		private static readonly LogWriter sLog = GetWriter("Logging");
		private static long sTimerTickStart = Stopwatch.GetTimestamp();

		/// <summary>
		/// Initializes the <see cref="Log"/> class.
		/// </summary>
		static Log()
		{
			lock (sSync) // just to prevent assertions from firing...
			{
				InitDefaultConfiguration();
				LogMessageProcessingPipeline = new ConsoleWriterPipelineStage();
			}
		}

		/// <summary>
		/// Gets the object that is used to synchronize access to shared resources in the logging subsystem.
		/// </summary>
		internal static object Sync => sSync;

		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		public static string ApplicationName
		{
			get => sLogConfiguration.ApplicationName;
			set => sLogConfiguration.ApplicationName = value;
		}

		/// <summary>
		/// Gets all log writers that have been registered using <see cref="GetWriter{T}"/> or <see cref="GetWriter(string)"/>.
		/// The index of the log writer in the list corresponds to <see cref="LogWriter.Id"/>.
		/// </summary>
		public static IReadOnlyList<LogWriter> KnownWriters => sLogWritersById;

		/// <summary>
		/// Gets or sets the log configuration that determines the behavior of the log
		/// (if set to <c>null</c> the default log configuration with its ini-style configuration file is used).
		/// </summary>
		public static ILogConfiguration Configuration
		{
			get => sLogConfiguration;

			set
			{
				lock (sSync)
				{
					if (value != null)
					{
						sLogConfiguration = value;

						// add default settings to configuration, if necessary
						SetDefaultProcessingPipelineSettings(LogMessageProcessingPipeline);

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
		public static IProcessingPipelineStage LogMessageProcessingPipeline
		{
			get => sLogMessageProcessingPipeline;

			set
			{
				lock (sSync)
				{
					// abort, if the processing pipeline has not changed
					if (sLogMessageProcessingPipeline == value) return;

					if (value != null)
					{
						// initialize the new processing pipeline
						value.Initialize(); // can throw...

						// let pipeline stages set their default configuration
						SetDefaultProcessingPipelineSettings(value);
					}

					// make new processing pipeline the current one
					var oldPipeline = sLogMessageProcessingPipeline;
					sLogMessageProcessingPipeline = value;

					// shutdown old processing pipeline, if any
					if (oldPipeline != null)
					{
						try
						{
							oldPipeline.Shutdown();
						}
						catch (Exception ex)
						{
							Debug.Fail("Shutting down the old processing pipeline failed unexpectedly.", ex.ToString());
						}
					}
				}
			}
		}

		/// <summary>
		/// Shuts the logging subsystem down gracefully.
		/// </summary>
		public static void Shutdown()
		{
			lock (sSync)
			{
				// pipeline stages might have buffered messages
				// => shut them down gracefully to allow them to complete processing before exiting
				sLogMessageProcessingPipeline?.Shutdown();
			}
		}

		/// <summary>
		/// Gets the current timestamp as used by the logging subsystem.
		/// </summary>
		/// <returns>The current timestamp.</returns>
		public static DateTimeOffset GetTimestamp()
		{
			return DateTimeOffset.Now;
		}

		/// <summary>
		/// Gets the current high accuracy timestamp as used by the logging subsystem (in ns).
		/// </summary>
		/// <returns>The current high accuracy timestamp.</returns>
		public static long GetHighAccuracyTimestamp()
		{
			return unchecked((long)((decimal)(Stopwatch.GetTimestamp() - sTimerTickStart) * 1000000000L / Stopwatch.Frequency)); // in ns
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

			IProcessingPipelineStage pipeline = LogMessageProcessingPipeline;
			if (pipeline != null)
			{
				LocalLogMessage message = null;
				try
				{
					message = sLogMessagePool.GetUninitializedMessage();

					lock (sSync) // needed to avoid race conditions causing timestamps getting mixed up
					{
						long highAccuracyTimestamp = GetHighAccuracyTimestamp();

						message.Init(
							GetTimestamp(),
							highAccuracyTimestamp,
							sProcessId,
							sProcessName,
							ApplicationName,
							writer,
							level,
							text);

						pipeline.Process(message);
					}
				}
				finally
				{
					// let the message return to the pool
					// (pipeline stages may have incremented the reference counter to delay this)
					message?.Release();
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
			sLogWritersByName.TryGetValue(name, out var writer);
			if (writer == null)
			{
				lock (sSync)
				{
					if (!sLogWritersByName.TryGetValue(name, out writer))
					{
						writer = new LogWriter(name);

						// the id of the writer should correspond to the index in the list and the
						// number of elements in the dictionary.
						Debug.Assert(writer.Id == sLogWritersById.Count);
						Debug.Assert(writer.Id == sLogWritersByName.Count);

						// set active log level mask, if the configuration is already initialized
						if (sLogConfiguration != null)
						{
							writer.ActiveLogLevelMask = sLogConfiguration.GetActiveLogLevelMask(writer);
						}

						// replace log writer list
						List<LogWriter> newLogWritersById = new List<LogWriter>(sLogWritersById);
						newLogWritersById.Add(writer);
						Thread.MemoryBarrier(); // ensures everything has been actually written to memory at this point
						sLogWritersById = newLogWritersById;

						// replace log writer collection dictionary
						Dictionary<string, LogWriter> newLogWritersByName = new Dictionary<string, LogWriter>(sLogWritersByName);
						newLogWritersByName.Add(writer.Name, writer);
						Thread.MemoryBarrier(); // ensures everything has been actually written to memory at this point
						sLogWritersByName = newLogWritersByName;
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
				VolatileLogConfiguration configuration = new VolatileLogConfiguration();
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
		private static bool SetDefaultProcessingPipelineSettings(IProcessingPipelineStage firstStage)
		{
			// global logging lock is hold here...
			Debug.Assert(Monitor.IsEntered(Sync));

			// abort, if the first stage is not defined...
			if (firstStage == null) {
				return false;
			}

			// get all stages of the processing pipeline recursively
			HashSet<IProcessingPipelineStage> allStages = new HashSet<IProcessingPipelineStage>();
			firstStage.GetAllStages(allStages);

			// retrieve default settings from all stages and populate the configuration accordingly
			bool modified = false;
			foreach (var stage in allStages)
			{
				// get default settings
				IReadOnlyDictionary<string, string> defaultSettings = stage.GetDefaultSettings();

				// add pipeline stage settings that are missing in the configuration
				bool stageSettingsModified = false;
				var ps = Configuration.GetProcessingPipelineStageSettings(stage.GetType().Name);
				Dictionary<string, string> persistentSettings;
				if (ps is IDictionary<string, string> dict) {
					persistentSettings = new Dictionary<string, string>(dict);
				} else if (ps != null) {
					var copy = new Dictionary<string, string>();
					foreach (var kvp in ps) copy.Add(kvp.Key, kvp.Value);
					persistentSettings = copy;
				} else {
					persistentSettings = new Dictionary<string, string>();
				}

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

