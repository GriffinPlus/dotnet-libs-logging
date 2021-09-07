///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// The access point to the logging subsystem in the current application domain.
	/// </summary>
	public class Log
	{
		/// <summary>
		/// Object that is used to synchronize access to shared resources in the logging subsystem.
		/// </summary>
		internal static readonly object Sync = new object();

		private static readonly int                              sProcessId                            = Process.GetCurrentProcess().Id;
		private static readonly string                           sProcessName                          = Process.GetCurrentProcess().ProcessName;
		private static readonly LocalLogMessagePool              sLogMessagePool                       = new LocalLogMessagePool();
		private static          List<LogWriter>                  sLogWritersById                       = new List<LogWriter>();
		private static          Dictionary<string, LogWriter>    sLogWritersByName                     = new Dictionary<string, LogWriter>();
		private static          List<LogWriterTag>               sLogWriterTagsById                    = new List<LogWriterTag>();
		private static          Dictionary<string, LogWriterTag> sLogWriterTagsByName                  = new Dictionary<string, LogWriterTag>();
		private static          ILogConfiguration                sLogConfiguration                     = null;
		private static volatile IProcessingPipelineStage         sProcessingPipeline                   = null;
		private static volatile bool                             sTerminateProcessOnUnhandledException = true;
		private static readonly AsyncLocal<uint>                 sAsyncId                              = new AsyncLocal<uint>();
		private static          int                              sAsyncIdCounter                       = 0;
		private static readonly LogWriter                        sLog                                  = GetWriter("Logging");

		/// <summary>
		/// Initializes the <see cref="Log"/> class.
		/// </summary>
		static Log()
		{
			lock (Sync) // just to prevent assertions from firing...
			{
				InitDefaultConfiguration();
				ProcessingPipeline = new ConsoleWriterPipelineStage("Console") { IsDefaultStage = true };
			}

			// register handler for unhandled exceptions and process exit
			AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
			AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
		}

		/// <summary>
		/// Gets a logger that directly writes into the log of the operating system
		/// (windows event log on windows, syslog on linux).
		/// </summary>
		public static ISystemLogger SystemLogger { get; } = SystemLoggerFactory.Create();

		/// <summary>
		/// Gets or sets a value indicating whether the logging subsystem terminates the process after an unhandled exception
		/// is caught and logged. This is usually the best way to deal with such a condition to avoid executing a program
		/// that is in an undetermined state. Default: <c>true</c>.
		/// </summary>
		public static bool TerminateProcessOnUnhandledException
		{
			get => sTerminateProcessOnUnhandledException;
			set => sTerminateProcessOnUnhandledException = value;
		}

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
		/// Gets all log writer tags that have been registered using <see cref="LogWriter.WithTag"/> or <see cref="LogWriter.WithTags"/>.
		/// The index of the log writer tag in the list corresponds to <see cref="LogWriterTag.Id"/>.
		/// </summary>
		public static IReadOnlyList<LogWriterTag> KnownTags => sLogWriterTagsById;

		/// <summary>
		/// Gets an id that is valid for the entire asynchronous control flow.
		/// It should be queried the first time where the asynchronous path starts.
		/// </summary>
		public static uint AsyncId
		{
			get
			{
				unchecked
				{
					uint id = sAsyncId.Value;

					if (id == 0)
					{
						id = (uint)Interlocked.Increment(ref sAsyncIdCounter);
						if (id == 0) id = (uint)Interlocked.Increment(ref sAsyncIdCounter); // handles overflow
						sAsyncId.Value = id;
					}

					return id;
				}
			}
		}

		/// <summary>
		/// Gets or sets the log configuration that determines the behavior of the log
		/// (if set to <c>null</c> a volatile log configuration is used).
		/// </summary>
		public static ILogConfiguration Configuration
		{
			get => sLogConfiguration;

			set
			{
				lock (Sync)
				{
					// abort, if the configuration has not changed
					if (value == sLogConfiguration) return;

					// unlink pipeline stages and the configuration
					UnlinkPipelineStagesFromConfiguration(ProcessingPipeline);

					if (value != null)
					{
						sLogConfiguration = value;
					}
					else
					{
						InitDefaultConfiguration();
					}

					// update log writers to comply with the new configuration
					UpdateLogWriters();

					// link pipeline stages to the configuration, so configuration changes effect the pipeline stages
					// and programmatic changes to pipeline stages effect the configuration
					LinkPipelineStagesToConfiguration(ProcessingPipeline, sLogConfiguration);
				}
			}
		}

		/// <summary>
		/// Gets or sets the log message processing pipeline that receives any log messages written to the logging subsystem.
		/// </summary>
		public static IProcessingPipelineStage ProcessingPipeline
		{
			get => sProcessingPipeline;

			set
			{
				lock (Sync)
				{
					// abort, if the processing pipeline has not changed
					if (sProcessingPipeline == value) return;

					if (value != null)
					{
						// link pipeline stages to the configuration, so configuration changes effect the pipeline stages
						// and programmatic changes to pipeline stages effect the configuration
						LinkPipelineStagesToConfiguration(value, sLogConfiguration);

						// initialize the new processing pipeline
						value.Initialize(); // can throw...
					}

					// make new processing pipeline the current one
					var oldPipeline = sProcessingPipeline;
					sProcessingPipeline = value;

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

						// unlink old pipeline stages and the configuration
						UnlinkPipelineStagesFromConfiguration(oldPipeline);
					}
				}
			}
		}

		/// <summary>
		/// Shuts the logging subsystem down gracefully.
		/// </summary>
		public static void Shutdown()
		{
			lock (Sync)
			{
				// pipeline stages might have buffered messages
				// => shut them down gracefully to allow them to complete processing before exiting
				sProcessingPipeline?.Shutdown();
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
		/// Gets the current high precision timestamp as used by the logging subsystem (in ns).
		/// </summary>
		/// <returns>The current high precision timestamp.</returns>
		public static long GetHighPrecisionTimestamp()
		{
			return (long)((decimal)Stopwatch.GetTimestamp() * 1000000000L / Stopwatch.Frequency); // in ns
		}

		/// <summary>
		/// Writes a message using an internal log writer bypassing configured source filters
		/// (should be used by components of the logging subsystem only to report important conditions).
		/// </summary>
		/// <param name="level">Log level to use.</param>
		/// <param name="text">Text of the message to write.</param>
		public void WriteLoggingMessage(LogLevel level, string text)
		{
			sLog.ForceWrite(level, text);
		}

		/// <summary>
		/// Writes the specified log message using the specified log writer at the specified level.
		/// </summary>
		/// <param name="writer">Log writer to use.</param>
		/// <param name="level">Log level to use.</param>
		/// <param name="tags">Tags attached to the message.</param>
		/// <param name="text">Text of the log message.</param>
		internal static void WriteMessage(
			LogWriter       writer,
			LogLevel        level,
			LogWriterTagSet tags,
			string          text)
		{
			if (level.Id < 0 || level.Id == int.MaxValue)
			{
				level = LogLevel.Error;
				text = "##### Message was written using log level 'None' or 'All'. Falling back to 'Error'. Don't do that!!!! #####" +
				       Environment.NewLine +
				       Environment.NewLine +
				       text;
			}

			// remove preceding and trailing line breaks
			text = text.Trim('\r', '\n');

			var pipeline = ProcessingPipeline;
			if (pipeline != null)
			{
				LocalLogMessage message = null;
				try
				{
					message = sLogMessagePool.GetUninitializedMessage();

					lock (Sync) // needed to avoid race conditions causing timestamps getting mixed up
					{
						long highPrecisionTimestamp = GetHighPrecisionTimestamp();

						message.InitWith(
							GetTimestamp(),
							highPrecisionTimestamp,
							writer,
							level,
							tags,
							ApplicationName,
							sProcessName,
							sProcessId,
							text);

						pipeline.ProcessMessage(message);
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
		/// <exception cref="ArgumentNullException">The specified name is <c>null</c>.</exception>
		/// <exception cref="ArgumentException">The specified name is invalid.</exception>
		public static LogWriter GetWriter(string name)
		{
			LogWriter.CheckName(name);

			sLogWritersByName.TryGetValue(name, out var writer);
			if (writer == null)
			{
				lock (Sync)
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
						var newLogWritersById = new List<LogWriter>(sLogWritersById) { writer };
						Thread.MemoryBarrier(); // ensures everything has been actually written to memory at this point
						sLogWritersById = newLogWritersById;

						// replace log writer collection dictionary
						var newLogWritersByName = new Dictionary<string, LogWriter>(sLogWritersByName) { { writer.Name, writer } };
						Thread.MemoryBarrier(); // ensures everything has been actually written to memory at this point
						sLogWritersByName = newLogWritersByName;

						// notify about the new log writer
						ProcessLogWriterAdded(writer);
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
		/// Gets a log writer tag with the specified name (for internal use only).
		/// </summary>
		/// <param name="name">Name of the log writer to get.</param>
		/// <returns>The requested log writer.</returns>
		internal static LogWriterTag GetWriterTag(string name)
		{
			sLogWriterTagsByName.TryGetValue(name, out var tag);
			if (tag == null)
			{
				lock (Sync)
				{
					if (!sLogWriterTagsByName.TryGetValue(name, out tag))
					{
						tag = new LogWriterTag(name);

						// the id of the writer tag should correspond to the index in the list and the
						// number of elements in the dictionary.
						Debug.Assert(tag.Id == sLogWriterTagsById.Count);
						Debug.Assert(tag.Id == sLogWriterTagsByName.Count);

						// replace log writer tag list
						var newLogWriterTagsById = new List<LogWriterTag>(sLogWriterTagsById) { tag };
						Thread.MemoryBarrier(); // ensures everything has been actually written to memory at this point
						sLogWriterTagsById = newLogWriterTagsById;

						// replace log writer tag collection dictionary
						var newLogWriterTagsByName = new Dictionary<string, LogWriterTag>(sLogWriterTagsByName) { { tag.Name, tag } };
						Thread.MemoryBarrier(); // ensures everything has been actually written to memory at this point
						sLogWriterTagsByName = newLogWriterTagsByName;

						// notify about the new log writer tag
						ProcessLogWriterTagAdded(tag);
					}
				}
			}

			return tag;
		}

		/// <summary>
		/// Initializes the default log configuration.
		/// </summary>
		internal static void InitDefaultConfiguration()
		{
			// global logging lock is hold here...
			Debug.Assert(Monitor.IsEntered(Sync));

			if (sLogConfiguration == null)
			{
				// create and init default configuration
				var configuration = new VolatileLogConfiguration { IsDefaultConfiguration = true };
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
			Debug.Assert(Monitor.IsEntered(Sync));
			foreach (var writer in sLogWritersById) writer.Update(sLogConfiguration);
		}

		/// <summary>
		/// Links the specified processing pipeline stage and its following stages with to the specified configuration.
		/// This enables the stages to persist their settings.
		/// </summary>
		/// <param name="firstStage">First stage of the processing pipeline.</param>
		/// <param name="configuration">The configuration to set.</param>
		private static void LinkPipelineStagesToConfiguration(
			IProcessingPipelineStage firstStage,
			ILogConfiguration        configuration)
		{
			// global logging lock is hold here...
			Debug.Assert(Monitor.IsEntered(Sync));

			// abort, if the first stage is not defined...
			if (firstStage == null) return;

			// get all stages of the processing pipeline recursively
			var allStages = new HashSet<IProcessingPipelineStage>();
			firstStage.GetAllStages(allStages);

			// link configuration to all stages
			foreach (var stage in allStages)
			{
				var stageConfiguration = configuration.ProcessingPipeline.Stages.FirstOrDefault(x => x.Name == stage.Name) ??
				                         configuration.ProcessingPipeline.Stages.AddNew(stage.Name);
				stage.Settings = stageConfiguration;
			}
		}

		/// <summary>
		/// Unlinks the specified processing pipeline stage and its following stages from the specified configuration.
		/// </summary>
		/// <param name="firstStage">First stage of the processing pipeline.</param>
		private static void UnlinkPipelineStagesFromConfiguration(IProcessingPipelineStage firstStage)
		{
			// global logging lock is hold here...
			Debug.Assert(Monitor.IsEntered(Sync));

			// abort, if the first stage is not defined...
			if (firstStage == null) return;

			// get all stages of the processing pipeline recursively
			var allStages = new HashSet<IProcessingPipelineStage>();
			firstStage.GetAllStages(allStages);

			// unlink configuration from all stages
			foreach (var stage in allStages)
			{
				stage.Settings = null;
			}
		}

		/// <summary>
		/// Is called when the process exits.
		/// </summary>
		/// <param name="sender">The AppDomain that exited.</param>
		/// <param name="e">Not used.</param>
		private static void OnProcessExit(object sender, EventArgs e)
		{
			lock (Sync)
			{
				if (sProcessingPipeline != null && sProcessingPipeline.IsInitialized)
				{
					// report the incident using the system logger
					var builder = new StringBuilder();
					builder.AppendLine("The logging subsystem has not been shut down properly before the application exited.");
					builder.AppendLine("You should call Log.Shutdown() to shutdown the logging subsystem gracefully to avoid loosing messages.");
					SystemLogger.WriteError(builder.ToString());
				}
			}
		}

		/// <summary>
		/// Is called when an exception is not caught.
		/// </summary>
		/// <param name="sender">The source of the unhandled exception event (may be null on some frameworks).</param>
		/// <param name="e">An <see cref="UnhandledExceptionEventArgs"/> that contains the event data.</param>
		private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			var exception = e.ExceptionObject as Exception;

			var builder = new StringBuilder();
			builder.AppendLine("An unhandled exception occurred!");
			builder.AppendLine();
			builder.AppendLine($"Runtime terminating: {e.IsTerminating}");
			builder.AppendLine();
			builder.AppendLine("Environment:");
			builder.AppendLine($"  Operating System: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? 64 : 32)} bit)");
			builder.AppendLine($"  Command Line: {Environment.CommandLine}");
			builder.AppendLine($"  User Interactive: {Environment.UserInteractive}");

			if (sender is AppDomain appDomain)
			{
				builder.AppendLine();
				builder.AppendLine("AppDomain Information:");
				builder.AppendLine($"  Is Default AppDomain: {appDomain.IsDefaultAppDomain()}");
				builder.AppendLine($"  Base Directory: {appDomain.BaseDirectory}");
				builder.AppendLine($"  Friendly Name: {appDomain.FriendlyName}");
				builder.AppendLine($"  Relative Search Path: {appDomain.RelativeSearchPath ?? "<not set>"}");
				builder.AppendLine($"  Shadow Copy Files: {appDomain.ShadowCopyFiles}");
			}

			if (exception != null)
			{
				builder.AppendLine();
				builder.AppendLine("Exception:");
				builder.AppendLine(LogWriter.UnwrapException(exception));
			}

			// log to system log
			SystemLogger.WriteError(builder.ToString());

			// try to put the message into the regular log as well
			sLog.ForceWrite(LogLevel.Alert, builder.ToString());

			// terminate the application immediately
			if (TerminateProcessOnUnhandledException)
			{
				// shut the logging subsystem down to ensure that any buffered messages are processed properly
				// to avoid loosing messages that might contain information about the incident
				Shutdown();

				// terminate the application immediately (use windows error reporting, if available)
				Environment.FailFast("An unhandled exception occurred.", exception);
			}
		}

		/// <summary>
		/// Is called when a new log level is added to the logging subsystem.
		/// It notifies other components about the new log level.
		/// </summary>
		/// <param name="level">The new log level.</param>
		internal static void ProcessLogLevelAdded(LogLevel level)
		{
			// the global logging lock should have been acquired
			Debug.Assert(Monitor.IsEntered(Sync));

			// notify log message processing pipeline stages
			ProcessingPipeline?.ProcessLogLevelAdded(level);
		}

		/// <summary>
		/// Is called when a new log writer is added to the logging subsystem.
		/// It notifies other components about the new log writer.
		/// </summary>
		/// <param name="writer">The new log writer.</param>
		internal static void ProcessLogWriterAdded(LogWriter writer)
		{
			// the global logging lock should have been acquired
			Debug.Assert(Monitor.IsEntered(Sync));

			// notify log message processing pipeline stages
			ProcessingPipeline?.ProcessLogWriterAdded(writer);
		}

		/// <summary>
		/// Is called when a new log writer tag is added to the logging subsystem.
		/// It notifies other components about the new tag.
		/// </summary>
		/// <param name="tag">The new log writer tag.</param>
		internal static void ProcessLogWriterTagAdded(LogWriterTag tag)
		{
			// the global logging lock should have been acquired
			Debug.Assert(Monitor.IsEntered(Sync));

			// notify log message processing pipeline stages
			ProcessingPipeline?.ProcessLogWriterTagAdded(tag);
		}
	}

}
