///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// The access point to the logging subsystem in the current application domain.
/// </summary>
public class Log
{
	/// <summary>
	/// Object that is used to synchronize access to shared resources in the logging subsystem.
	/// </summary>
	internal static readonly object Sync = LogGlobals.Sync;

	private static readonly int                     sProcessId                            = Process.GetCurrentProcess().Id;
	private static readonly string                  sProcessName                          = Process.GetCurrentProcess().ProcessName;
	private static readonly LocalLogMessagePool     sLogMessagePool                       = new();
	private static volatile ProcessingPipelineStage sProcessingPipeline                   = null;
	private static volatile bool                    sTerminateProcessOnUnhandledException = true;
	private static readonly LogWriter               sLog                                  = LogWriter.Get("Logging");

	/// <summary>
	/// Initializes the <see cref="Log"/> class.
	/// </summary>
	static Log()
	{
		// initialize default settings and pipeline stage
		Initialize();

		// attach to events of the logging interface
		LogLevel.NewLogLevelRegistered += ProcessLogLevelAdded;
		LogWriter.NewLogWriterRegistered += ProcessLogWriterAdded;
		LogWriter.NewLogWriterTagRegistered += ProcessLogWriterTagAdded;
		LogWriter.LogMessageWritten += ProcessLogMessageWritten;
		FailFast.TerminationRequestedWithMessage += ProcessTerminationRequested;
		FailFast.TerminationRequestedWithException += ProcessTerminationRequested;

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
	/// that is in an undetermined state. Default: <see langword="true"/>.
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
		get => Configuration.ApplicationName;
		set => Configuration.ApplicationName = value;
	}

	/// <summary>
	/// Gets all log writers that have been registered using <see cref="GetWriter{T}"/>, <see cref="GetWriter(Type)"/>, <see cref="GetWriter(string)"/>
	/// or one of their interface equivalents <see cref="LogWriter.Get{T}"/>, <see cref="LogWriter.Get(Type)"/> or <see cref="LogWriter.Get(string)"/>.
	/// The index of the log writer in the list corresponds to <see cref="LogWriter.Id"/>.
	/// </summary>
	[Obsolete("Deprecated, please use LogWriter.KnownWriters instead. Will be removed with the next major release.")]
	public static IReadOnlyList<LogWriter> KnownWriters => LogWriter.KnownWriters;

	/// <summary>
	/// Gets all log writer tags that have been registered using <see cref="LogWriter.WithTag"/> or <see cref="LogWriter.WithTags"/>.
	/// The index of the log writer tag in the list corresponds to <see cref="LogWriterTag.Id"/>.
	/// </summary>
	[Obsolete("Deprecated, please use LogWriter.KnownTags instead. Will be removed with the next major release.")]
	public static IReadOnlyList<LogWriterTag> KnownTags => LogWriter.KnownTags;

	/// <summary>
	/// Gets an id that is valid for the entire asynchronous control flow.
	/// It should be queried the first time when the asynchronous path starts.
	/// It starts with 1. When wrapping around it skips 0, so 0 can be safely used to indicate an invalid/unassigned id.
	/// </summary>
	[Obsolete("Deprecated, please use AsyncId.Current instead. Will be removed with the next major release.")]
	public static uint AsyncId => Logging.AsyncId.Current;

	/// <summary>
	/// Gets the log configuration that determines the behavior of the log.
	/// </summary>
	public static ILogConfiguration Configuration { get; private set; } = null;

	/// <summary>
	/// Gets the processing pipeline that processes any log messages written to the logging subsystem.
	/// </summary>
	public static ProcessingPipelineStage ProcessingPipeline => sProcessingPipeline;

	/// <summary>
	/// Initializes the logging subsystem with default settings.
	/// All messages with <see cref="LogLevel.Notice"/> or more severe are written to the console
	/// using a <see cref="ConsoleWriterPipelineStage"/> with default settings.
	/// </summary>
	public static void Initialize()
	{
		Initialize<VolatileLogConfiguration>(
			configuration =>
			{
				configuration.IsDefaultConfiguration = true;
			},
			null);
	}

	/// <summary>
	/// Initializes the logging subsystem.
	/// </summary>
	/// <typeparam name="TConfiguration">The type of the configuration to use.</typeparam>
	/// <param name="configurationInitializer">
	/// Initializer for the configuration (may be <see langword="null"/>).
	/// </param>
	/// <param name="processingPipelineInitializer">
	/// Initializer for the processing pipeline
	/// (may be <see langword="null"/> to set up a <see cref="ConsoleWriterPipelineStage"/> with default settings).
	/// </param>
	public static void Initialize<TConfiguration>(
		LogConfigurationInitializer<TConfiguration> configurationInitializer,
		ProcessingPipelineInitializer               processingPipelineInitializer)
		where TConfiguration : ILogConfiguration, new()
	{
		lock (Sync)
		{
			ILogConfiguration oldConfiguration = Configuration;
			ProcessingPipelineStage oldPipeline = sProcessingPipeline;

			// create the new configuration
			var configuration = new TConfiguration();

			// suspend raising the Changed event of the configuration to avoid firing the event when setting up
			// the configuration and binding stages to it
			using (configuration.SuspendChangedEvent())
			{
				// initialize the configuration
				configurationInitializer?.Invoke(configuration);

				// build and configure the processing pipeline using a builder (if specified)
				ProcessingPipelineStage stage = null;
				if (processingPipelineInitializer != null)
				{
					// pipeline builder was specified
					// => use it to set up the processing pipeline
					var stageBuilder = new ProcessingPipelineBuilder(configuration);
					processingPipelineInitializer(stageBuilder);
					stage = stageBuilder.PipelineStage;
				}

				// fall back to using a console writer pipeline stage,
				// if the pipeline builder was not specified or the builder returned an empty processing pipeline
				if (stage == null)
				{
					const string stageName = "Console";
					stage = ProcessingPipelineStage.Create<ConsoleWriterPipelineStage>(stageName, configuration);
					stage.IsDefaultStage = true;
				}

				try
				{
					// initialize the new processing pipeline
					stage.Initialize(); // can throw...
				}
				catch (Exception ex)
				{
					SystemLogger.WriteError($"Initializing log message processing pipeline failed. Exception:\n{LogWriter.UnwrapException(ex)}");
					configuration.Dispose();
					throw;
				}

				// make new configuration and processing pipeline the current one
				Configuration = configuration;
				sProcessingPipeline = stage;
			}

			// register for configuration changes
			Configuration.RegisterChangedEventHandler(OnLogConfigurationChanged, false);

			// update log writers to comply with the new configuration
			UpdateLogWriters();

			// shutdown old processing pipeline, if any
			if (oldPipeline != null)
			{
				try
				{
					oldPipeline.Shutdown();
				}
				catch (Exception ex)
				{
					SystemLogger.WriteError($"Shutting down old processing pipeline failed unexpectedly. Exception:\n{LogWriter.UnwrapException(ex)}");
				}
			}

			// shut the old configuration down
			if (oldConfiguration != null)
			{
				oldConfiguration.UnregisterChangedEventHandler(OnLogConfigurationChanged);
				oldConfiguration.Dispose();
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
			sProcessingPipeline = null;

			// shut the configuration down
			if (Configuration != null)
			{
				Configuration.UnregisterChangedEventHandler(OnLogConfigurationChanged);
				Configuration.Dispose();
				Configuration = null;
			}
		}
	}

	/// <summary>
	/// Gets the current timestamp as used by the logging subsystem.
	/// </summary>
	/// <returns>The current timestamp.</returns>
	[Obsolete("Deprecated, please use LogWriter.GetTimestamp() instead. Will be removed with the next major release.")]
	public static DateTimeOffset GetTimestamp()
	{
		return LogWriter.GetTimestamp();
	}

	/// <summary>
	/// Gets the current high precision timestamp as used by the logging subsystem (in ns).
	/// </summary>
	/// <returns>The current high precision timestamp.</returns>
	[Obsolete("Deprecated, please use LogWriter.GetHighPrecisionTimestamp() instead. Will be removed with the next major release.")]
	public static long GetHighPrecisionTimestamp()
	{
		return LogWriter.GetHighPrecisionTimestamp();
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
	/// Gets a log writer with the specified name that can be used to write to the log.
	/// </summary>
	/// <param name="name">Name of the log writer to get.</param>
	/// <returns>The requested log writer.</returns>
	/// <exception cref="ArgumentNullException">The specified name is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The specified name is invalid.</exception>
	[Obsolete("Deprecated, please use LogWriter.Get(string) instead. Will be removed with the next major release.")]
	public static LogWriter GetWriter(string name)
	{
		return LogWriter.Get(name);
	}

	/// <summary>
	/// Gets a log writer for the specified type that can be used to write to the log
	/// (the full name of the type becomes the name of the log writer).
	/// </summary>
	/// <param name="type">The type whose full name is to use as the log writer name.</param>
	/// <returns>The requested log writer.</returns>
	[Obsolete("Deprecated, please use LogWriter.Get(Type) instead. Will be removed with the next major release.")]
	public static LogWriter GetWriter(Type type)
	{
		return LogWriter.Get(type);
	}

	/// <summary>
	/// Gets a log writer for the specified type that can be used to write to the log
	/// (the full name of the type becomes the name of the log writer).
	/// </summary>
	/// <typeparam name="T">The type whose full name is to use as the log writer name.</typeparam>
	/// <returns>The requested log writer.</returns>
	[Obsolete("Deprecated, please use LogWriter.Get<T>() instead. Will be removed with the next major release.")]
	public static LogWriter GetWriter<T>()
	{
		return LogWriter.Get<T>();
	}

	/// <summary>
	/// Updates the active log level mask of all log writers.
	/// </summary>
	private static void UpdateLogWriters()
	{
		// global logging lock is hold here...
		Debug.Assert(Monitor.IsEntered(Sync));
		LogWriter.UpdateLogWriters(Configuration);
	}

	/// <summary>
	/// Is called when the log configuration changes.
	/// </summary>
	/// <param name="sender">The log configuration that has changed.</param>
	/// <param name="e">Event arguments (not used).</param>
	private static void OnLogConfigurationChanged(object sender, EventArgs e)
	{
		lock (Sync)
		{
			// update active log level mask in log writers
			UpdateLogWriters();

			// updating setting proxies in stages is not necessary, the stages will do this on their own
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
			if (sProcessingPipeline is { IsInitialized: true })
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
	/// <param name="sender">The source of the unhandled exception event (may be <see langword="null"/> on some frameworks).</param>
	/// <param name="e">An <see cref="UnhandledExceptionEventArgs"/> that contains the event data.</param>
	private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		ProcessUnhandledException(e.ExceptionObject as Exception, e.IsTerminating);
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

	/// <summary>
	/// Writes the specified log message using the specified log writer at the specified level.
	/// </summary>
	/// <param name="writer">Log writer to use.</param>
	/// <param name="level">Log level to use.</param>
	/// <param name="text">Text of the log message.</param>
	private static void ProcessLogMessageWritten(
		LogWriter writer,
		LogLevel  level,
		string    text)
	{
		if (level.Id is < 0 or int.MaxValue)
		{
			level = LogLevel.Error;
			text = "##### Message was written using log level 'None' or 'All'. Falling back to 'Error'. Don't do that!!!! #####" +
			       Environment.NewLine +
			       Environment.NewLine +
			       text;
		}

		// remove preceding and trailing line breaks
		text = text.Trim('\r', '\n');

		ProcessingPipelineStage pipeline = ProcessingPipeline;
		if (pipeline != null)
		{
			LocalLogMessage message = null;
			try
			{
				message = sLogMessagePool.GetUninitializedMessage();

				lock (Sync) // needed to avoid race conditions causing timestamps getting mixed up
				{
					long highPrecisionTimestamp = LogWriter.GetHighPrecisionTimestamp();

					message.InitWith(
						LogWriter.GetTimestamp(),
						highPrecisionTimestamp,
						writer,
						level,
						writer.Tags,
						ApplicationName,
						sProcessName,
						sProcessId,
						text);

					pipeline.ProcessMessage(message);
				}
			}
			catch (Exception ex)
			{
				// log to system log
				var builder = new StringBuilder();
				builder.AppendLine("Processing a log message failed unexpectedly.");
				builder.AppendLine();
				builder.AppendLine("Exception:");
				builder.AppendLine(LogWriter.UnwrapException(ex));
				SystemLogger.WriteError(builder.ToString());
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
	/// Is called when the <see cref="FailFast.TerminateApplication(string)"/> method is called.
	/// </summary>
	/// <param name="message">The message text describing the reason why application termination is requested.</param>
	private static void ProcessTerminationRequested(string message)
	{
		// the global logging lock should not have been acquired
		Debug.Assert(!Monitor.IsEntered(LogGlobals.Sync));

		// log to system log
		SystemLogger.WriteError(message);

		// try to put the message into the regular log as well
		sLog.ForceWrite(LogLevel.Alert, message);

		// shut the logging subsystem down to ensure that any buffered messages are processed properly
		// to avoid loosing messages that might contain information about the incident
		Shutdown();

		// terminate the application immediately (use windows error reporting, if available)
		Environment.FailFast(message);
	}

	/// <summary>
	/// Is called when the <see cref="FailFast.TerminateApplication(Exception)"/> method is called.
	/// </summary>
	/// <param name="exception">An exception describing the reason why application termination is requested.</param>
	private static void ProcessTerminationRequested(Exception exception)
	{
		// the global logging lock should not have been acquired
		Debug.Assert(!Monitor.IsEntered(LogGlobals.Sync));

		// log the exception and terminate the process (always!)
		TerminateProcessOnUnhandledException = true;
		ProcessUnhandledException(exception, isRuntimeTerminating: false);
	}

	/// <summary>
	/// Processes an unhandled/unexpected exception that occurred in the application.
	/// </summary>
	/// <param name="exception">The unhandled/unexpected exception.</param>
	/// <param name="isRuntimeTerminating">
	/// <see langword="true"/> if the runtime is terminating;<br/>
	/// otherwise, <see langword="false"/>.
	/// </param>
	private static void ProcessUnhandledException(Exception exception, bool isRuntimeTerminating)
	{
		var builder = new StringBuilder();
		builder.AppendLine("An unhandled/unexpected exception occurred!");
		builder.AppendLine();
		builder.AppendLine($"Runtime terminating: {isRuntimeTerminating}");
		builder.AppendLine();
		builder.AppendLine("Environment:");
		builder.AppendLine($"  Operating System: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? 64 : 32)} bit)");
		builder.AppendLine($"  Command Line: {Environment.CommandLine}");
		builder.AppendLine($"  User Interactive: {Environment.UserInteractive}");

		builder.AppendLine();
		builder.AppendLine("AppDomain Information:");
		builder.AppendLine($"  Is Default AppDomain: {AppDomain.CurrentDomain.IsDefaultAppDomain()}");
		builder.AppendLine($"  Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
		builder.AppendLine($"  Friendly Name: {AppDomain.CurrentDomain.FriendlyName}");
		builder.AppendLine($"  Relative Search Path: {AppDomain.CurrentDomain.RelativeSearchPath ?? "<not set>"}");
		builder.AppendLine($"  Shadow Copy Files: {AppDomain.CurrentDomain.ShadowCopyFiles}");

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
}
