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
using System.IO;
using System.Reflection;
using System.Threading;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// The access point to the logging subsystem.
	/// </summary>
	public class LogSource
	{
		private static Dictionary<string, LogWriter> sLogWritersByName = new Dictionary<string, LogWriter>();
		private static readonly LogMessagePool sLogMessagePool = new LogMessagePool();
		private static ILogSourceConfiguration sLogSourceConfiguration;
		private static ILogMessageProcessingPipelineStage sLogMessageProcessingPipeline;
		private static string sApplicationName;

		/// <summary>
		/// Initializes the <see cref="LogSource"/> class.
		/// </summary>
		static LogSource()
		{
			sApplicationName = AppDomain.CurrentDomain.FriendlyName;
		}

		/// <summary>
		/// Gets the object that is used to synchronize access to shared resources in the logging subsystem.
		/// </summary>
		internal static object Sync { get; } = new object();

		/// <summary>
		/// Gets or sets the log source configuration that determines the behavior of the log source
		/// (if set to <c>null</c> the default log source configuration with its ini-style configuration file is used).
		/// </summary>
		public static ILogSourceConfiguration Configuration
		{
			get
			{
				// handle lazy initialization to avoid that the default .logconf file is created,
				// if a custom log source configuration is to be used instead
				if (sLogSourceConfiguration == null)
				{
					lock (Sync)
					{
						InitDefaultConfiguration();
					}
				}

				return sLogSourceConfiguration;
			}

			set
			{
				lock (Sync)
				{
					if (value != null)
					{
						sLogSourceConfiguration = value;
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
				if (value != null)
				{
					lock (Sync)
					{
						// get all stages of the processing pipeline recursively
						HashSet<ILogMessageProcessingPipelineStage> stages = new HashSet<ILogMessageProcessingPipelineStage>();
						value.GetAllStages(stages);

						bool modified = false;
						foreach (var stage in stages)
						{
							Dictionary<string, string> defaultSettings = new Dictionary<string, string>();
							stage.InitializeDefaultSettings(defaultSettings);
							/*
							IDictionary<string,string> persistentSettings = sLogSourceConfiguration.GetProcessingPipelineStageSettings(stage.GetType().Name);
							foreach (var kvp in defaultSettings)
							{
								if (persistentSettings.ContainsKey(kvp.Key)) {
									continue;
								}

								persistentSettings.Add(kvp.Key, kvp.Value);
								modified = true;
							}
							*/
						}

						if (modified)
						{
							//Configuration.Save();
						}
					}
				}

				sLogMessageProcessingPipeline = value;
			}
		}

		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		public static string ApplicationName
		{
			get { return sApplicationName; }
			set {
				if (value == null) throw new ArgumentNullException(nameof(value));
				if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("The application name must not consist of whitespaces only.", nameof(value));
				sApplicationName = value;
			}
		}

		/// <summary>
		/// Gets a bit mask indicating which log levels are active for the specified log writer.
		/// </summary>
		/// <param name="writer">Log writer to get the active log level mask for.</param>
		/// <returns>The active log level bit mask.</returns>
		internal static LogLevelBitMask GetActiveLogLevelMask(LogWriter writer)
		{
			ILogSourceConfiguration configuration = sLogSourceConfiguration;
			if (configuration != null)
			{

				return new LogLevelBitMask(0, false, false);
			}
			else
			{
				// configuration is not initialized, yet...
				return new LogLevelBitMask(0, false, false);
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
				lock (Sync)
				{
					if (!sLogWritersByName.TryGetValue(name, out writer))
					{
						writer = new LogWriter(name);
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
		/// Initializes the default log source configuration
		/// (ini-style configuration file located beside the entry assembly ending with the extension '.logconf').
		/// </summary>
		internal static void InitDefaultConfiguration()
		{
			// global logging lock is hold here...
			if (sLogSourceConfiguration == null)
			{
				Assembly assembly = Assembly.GetEntryAssembly();
				string path = Path.Combine(Path.GetDirectoryName(assembly.Location), Path.GetFileNameWithoutExtension(assembly.Location) + ".logconf");
				DefaultLogSourceConfiguration configuration = new DefaultLogSourceConfiguration(path);

				// save the configuration file, if it does not exist, yet
				try
				{
					if (!File.Exists(path))
					{
						configuration.Save();
					}
				}
				catch (Exception ex)
				{
					// can easily occur, if the application does not have the permission to write to its own folder
					Debug.WriteLine("Saving log source configuration failed: {0}", ex.ToString());
				}

				Thread.MemoryBarrier(); // ensures everything has been actually written to memory at this point
				sLogSourceConfiguration = configuration;

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

			foreach (var kvp in sLogWritersByName)
			{
				kvp.Value.ActiveLogLevelMask = sLogSourceConfiguration.GetActiveLogLevelMask(kvp.Value);
			}
		}
	}
}

