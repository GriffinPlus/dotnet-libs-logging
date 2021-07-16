///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

// ReSharper disable RedundantUsingDirective
// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedParameter.Local
// ReSharper disable ConvertToConstant.Local
// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable UnusedVariable
// ReSharper disable UseObjectOrCollectionInitializer

using System;
using System.IO;
using System.Threading;

namespace GriffinPlus.Lib.Logging.Demo
{

	class MyClass1
	{
	}

	class MyClass2
	{
	}

	class MyClass3
	{
	}

	class MyClass4
	{
	}

	class MyClassA
	{
	}

	class MyClassB
	{
	}

	class Program
	{
		// Register log writers using types.
		private static readonly LogWriter sLog1 = Log.GetWriter<MyClass1>();       // actual log writer name: GriffinPlus.Lib.Logging.Demo.MyClass1
		private static readonly LogWriter sLog2 = Log.GetWriter<MyClass2>();       // actual log writer name: GriffinPlus.Lib.Logging.Demo.MyClass2
		private static readonly LogWriter sLog3 = Log.GetWriter<MyClass3>();       // actual log writer name: GriffinPlus.Lib.Logging.Demo.MyClass3
		private static readonly LogWriter sLog4 = Log.GetWriter(typeof(MyClass4)); // actual log writer name: GriffinPlus.Lib.Logging.Demo.MyClass4
		private static readonly LogWriter sLog5 = Log.GetWriter(typeof(MyClassA)); // actual log writer name: GriffinPlus.Lib.Logging.Demo.MyClassA
		private static readonly LogWriter sLog6 = Log.GetWriter(typeof(MyClassB)); // actual log writer name: GriffinPlus.Lib.Logging.Demo.MyClassB

		// Register a log writer using a custom name.
		private static readonly LogWriter sLog7 = Log.GetWriter("My Fancy Writer");

		// Create tagging log writers
		private static readonly LogWriter sLog_TagA  = sLog7.WithTag("TagA");          // same as sLog7, but tags messages with 'TagA'
		private static readonly LogWriter sLog_TagB  = sLog7.WithTag("TagB");          // same as sLog7, but tags messages with 'TagB'
		private static readonly LogWriter sLog_TagBC = sLog7.WithTags("TagB", "TagC"); // same as sLog7, but tags messages with 'TagB' and 'TagC'

		private static void Main(string[] args)
		{
			// -----------------------------------------------------------------------------------------------------------------
			// -----------------------------------------------------------------------------------------------------------------
			// Set up the volatile (in-memory) or persistent (file-backed) configuration
			// -----------------------------------------------------------------------------------------------------------------
			// -----------------------------------------------------------------------------------------------------------------

			// volatile configuration (programmatic configuration only, no persistence)
			var config = new VolatileLogConfiguration();
			bool initConfig = true; // always init programmatically

			// file-based configuration 
			// var config = new FileBackedLogConfiguration(); // default location (beside the executable with file extension '.gplogconf');
			// var config = new FileBackedLogConfiguration("./my-conf.gplogconf"); // custom location
			// bool initConfig = !File.Exists(config.FullPath); // init programmatically, if file does not exist, yet

			if (initConfig)
			{
				// Add configuration for log writers that attach 'TagA'
				// - set base log level to 'None' effectively silencing the log writer
				// - include log level 'Notice'
				// - no excluded log levels
				// - tags must contain 'TagA'
				// => enabled log levels: 'Notice'
				config.AddLogWritersByWildcard(
					"*",
					x => x
						.WithTag("TagA")
						.WithBaseLevel(LogLevel.None)
						.WithLevel(LogLevel.Notice));

				// Add configuration for log writers that attach 'TagB' and/or 'TagC'
				// - set base log level to 'None' effectively silencing the log writer
				// - include log level 'Warning'
				// - no excluded log levels
				// - tags must contain 'TagB' and/or 'TagC'
				// => enabled log levels: 'Warning'
				config.AddLogWritersByWildcard(
					"*",
					x => x
						.WithTagRegex("^Tag[BC]$")
						.WithBaseLevel(LogLevel.None)
						.WithLevel(LogLevel.Warning));

				// Add configuration for log writer 'GriffinPlus.Lib.Logging.Demo.MyClass1' only
				// - set base log level to 'Notice' => enables log level 'Emergency', 'Alert', 'Critical', 'Error', 'Warning' and 'Notice'
				// - include log level 'Trace'
				// - exclude log level 'Warning'
				// - tags are not evaluated
				// => enabled log levels: 'Emergency', 'Alert', 'Critical', 'Error', 'Notice', 'Trace'
				config.AddLogWriter<MyClass1>(
					x => x
						.WithBaseLevel(LogLevel.Notice)
						.WithLevel(LogLevel.Trace)
						.WithoutLevel("Warning"));

				// Add configuration for log writer 'GriffinPlus.Lib.Logging.Demo.MyClass2' only
				// - set base log level to 'None' effectively silencing the log writer
				// - no included/excluded log levels
				// - tags are not evaluated
				// => no enabled log levels
				config.AddLogWriter(
					typeof(MyClass2),
					x => x.WithBaseLevel(LogLevel.None));

				// Add configuration for log writer 'GriffinPlus.Lib.Logging.Demo.MyClass3' only
				// - set base log level to 'All' enabling all log levels (including aspects)
				// - exclude all log levels from 'Informational' up to 'Trace' ('Informational', 'Debug', 'Trace')
				// - tags are not evaluated
				// => enabled log levels: All log levels, but 'Informational', 'Debug', 'Trace'
				config.AddLogWriter(
					typeof(MyClass3),
					x => x
						.WithBaseLevel(LogLevel.All)
						.WithoutLevelRange(LogLevel.Informational, LogLevel.Debug));

				// Add configuration for log writers matching regex pattern
				// - pattern matches 'GriffinPlus.Lib.Logging.Demo.MyClassA' and 'GriffinPlus.Lib.Logging.Demo.MyClassB'
				// - base level defaults to 'Error' => enables log level 'Emergency', 'Alert', 'Critical', 'Error'
				// - include all log levels from 'Informational' up to 'Trace' ('Informational', 'Debug', 'Trace')
				// - no excluded log levels
				// - tags are not evaluated
				// => enabled log levels: 'Emergency', 'Alert', 'Critical', 'Error', 'Informational', 'Debug'
				config.AddLogWritersByRegex(
					"^GriffinPlus.Lib.Logging.Demo.MyClass[A-Z]$",
					x => x.WithLevelRange(LogLevel.Informational, LogLevel.Debug));

				// Add configuration for log writers matching wildcard pattern
				// - applies to 'GriffinPlus.Lib.Logging.Demo.MyClass4' only
				//   (other writers are handled by preceding steps)
				// - base level defaults to 'Error' => enables log level 'Emergency', 'Alert', 'Critical', 'Error'
				// - include log level 'Trace'
				// - no excluded log levels
				// - tags are not evaluated
				// => enabled log levels: 'Emergency', 'Alert', 'Critical', 'Error', 'Trace'
				config.AddLogWritersByWildcard(
					"GriffinPlus.Lib.Logging.Demo.MyClass*",
					x => x.WithLevel(LogLevel.Trace));

				// Add configuration for log writer 'My Fancy Writer'
				// (matches everything that was not handled explicitly before)
				// - set base log level to 'None' effectively silencing the log writer
				// - include aspect log level 'Demo Aspect'
				// - no excluded log levels
				// - tags are not evaluated
				// => enabled log levels: 'Demo Aspect'
				config.AddLogWriter(
					"My Fancy Writer",
					x => x
						.WithBaseLevel(LogLevel.None)
						.WithLevel("Demo Aspect"));

				// Add configuration for log writer 'Timing' to enable logging time measurements written by the internal
				// 'Timing' log writer (see below for time measurements)
				config.AddLogWriterTiming();

				// Add default configuration for log writers that have not been handled up to this point
				// - base level defaults  to level 'Notice'
				// - no included/excluded log levels
				// => enabled log levels: 'Emergency', 'Alert', 'Critical', 'Error', 'Warning', 'Notice'
				config.AddLogWriterDefault();

				// Set application name (optional)
				Log.ApplicationName = "Logging Demo";
			}

			// activate the configuration
			Log.Configuration = config;

			// -----------------------------------------------------------------------------------------------------------------
			// -----------------------------------------------------------------------------------------------------------------
			// Configure the log message pipeline
			// -----------------------------------------------------------------------------------------------------------------
			// -----------------------------------------------------------------------------------------------------------------

			// Create log message formatter that prints log messages in a tabular fashion
			var tableFormatter = new TableMessageFormatter();
			tableFormatter.AddTimestampColumn("yyyy-MM-dd HH:mm:ss.fff"); // use custom timestamp format
			tableFormatter.AddProcessIdColumn();
			tableFormatter.AddProcessNameColumn();
			tableFormatter.AddApplicationNameColumn();
			tableFormatter.AddLogWriterColumn();
			tableFormatter.AddLogLevelColumn();
			tableFormatter.AddTagsColumn();
			tableFormatter.AddTextColumn();

			// Create log message formatter that prints log messages as JSON
			var jsonFormatter = new JsonMessageFormatter();
			jsonFormatter.Style = JsonMessageFormatterStyle.Beautified;
			jsonFormatter.AddTimestampField("yyyy-MM-dd HH:mm:ss.fff"); // use custom timestamp format
			jsonFormatter.AddProcessIdField();
			jsonFormatter.AddProcessNameField();
			jsonFormatter.AddApplicationNameField();
			jsonFormatter.AddLogWriterField();
			jsonFormatter.AddLogLevelField();
			jsonFormatter.AddTagsField();
			jsonFormatter.AddTextField();

			// Create pipeline stage for printing to the console
			var consoleStage = new ConsoleWriterPipelineStage("Console");
			consoleStage.MessageQueueSize = 500;                                              // buffer up to 500 messages (default)
			consoleStage.DiscardMessagesIfQueueFull = false;                                  // block if the queue is full (default)
			consoleStage.ShutdownTimeout = TimeSpan.FromMilliseconds(5000);                   // wait up to 5000ms for the stage to shut down (default)
			consoleStage.Formatter = tableFormatter;                                          // use specific formatter
			consoleStage.DefaultStream = ConsoleOutputStream.Stdout;                          // print to stdout by default (default)
			consoleStage.MapLogLevelToStream(LogLevel.Emergency, ConsoleOutputStream.Stderr); // print errors to stderr
			consoleStage.MapLogLevelToStream(LogLevel.Alert, ConsoleOutputStream.Stderr);     // 
			consoleStage.MapLogLevelToStream(LogLevel.Critical, ConsoleOutputStream.Stderr);  // 
			consoleStage.MapLogLevelToStream(LogLevel.Error, ConsoleOutputStream.Stderr);     //

			// Create pipeline stage for writing to a file
			var fileStage = new FileWriterPipelineStage("File", "mylog.log", false);
			fileStage.MessageQueueSize = 500;                            // buffer up to 500 messages (default)
			fileStage.DiscardMessagesIfQueueFull = false;                // block if the queue is full (default)
			fileStage.ShutdownTimeout = TimeSpan.FromMilliseconds(5000); // wait up to 5000ms for the stage to shut down (default)
			fileStage.Formatter = jsonFormatter;                         // use specific formatter
			fileStage.AutoFlush = false;                                 // do not flush the file after writing a log message (default)

			// Create splitter pipeline stage to unconditionally feed log messages into all pipelines stages
			var splitterStage = new SplitterPipelineStage("Splitter");
			splitterStage.AddNextStage(consoleStage);
			splitterStage.AddNextStage(fileStage);

			// Activate the stages
			Log.ProcessingPipeline = splitterStage;

			// Save configuration, if it was initialized programmatically
			// (It is important that Log.ProcessingPipeline and Log.Configuration are set at this point, so the
			// pipeline stages can persist their settings in the configuration)
			if (initConfig) config.Save(true);

			// -----------------------------------------------------------------------------------------------------------------
			// -----------------------------------------------------------------------------------------------------------------
			// Use the logging subsystem
			// -----------------------------------------------------------------------------------------------------------------
			// -----------------------------------------------------------------------------------------------------------------

			// Get an aspect log level.
			var aspect = LogLevel.GetAspect("Demo Aspect");

			// Write messages to all known log levels (predefined log levels + aspects).
			foreach (var level in LogLevel.KnownLevels)
			{
				sLog1.Write(level, "This is sLog1 writing using level '{0}'.", level.Name);
				sLog2.Write(level, "This is sLog2 writing using level '{0}'.", level.Name);
				sLog3.Write(level, "This is sLog3 writing using level '{0}'.", level.Name);
				sLog4.Write(level, "This is sLog4 writing using level '{0}'.", level.Name);
				sLog5.Write(level, "This is sLog5 writing using level '{0}'.", level.Name);
				sLog6.Write(level, "This is sLog6 writing using level '{0}'.", level.Name);
				sLog7.Write(level, "This is sLog7 writing using level '{0}'.", level.Name);
				sLog_TagA.Write(level, "This is sLog_TagA writing using level '{0}'.", level.Name);
				sLog_TagB.Write(level, "This is sLog_TagB writing using level '{0}'.", level.Name);
				sLog_TagBC.Write(level, "This is sLog_TagBC writing using level '{0}'.", level.Name);
			}

			// Use a timing logger to determine how long an operation takes. It uses log level 'Timing' and log writer
			// 'Timing' by default, so you need to ensure that the configuration lets these messages pass).
			sLog1.Write(LogLevel.Notice, "Presenting a timing logger with default settings...");
			using (TimingLogger.Measure())
			{
				Thread.Sleep(500);
			}

			// Use a timing logger, customize the log writer/level it uses and associate an operation name with the
			// measurement that is printed to the log as well.
			sLog1.Write(LogLevel.Notice, "A timing logger with custom log level/writer and operation name...");
			using (TimingLogger.Measure(sLog1, LogLevel.Notice, "Waiting for 500ms"))
			{
				Thread.Sleep(500);
			}

			// -----------------------------------------------------------------------------------------------------------------
			// -----------------------------------------------------------------------------------------------------------------
			// Wait for the user to press a key to shut the logging subsystem down
			// -----------------------------------------------------------------------------------------------------------------
			// -----------------------------------------------------------------------------------------------------------------

			Thread.Sleep(1000);
			Console.WriteLine();
			Console.WriteLine("Press any key to shut the logging subsystem down...");
			Console.ReadKey();

			// -----------------------------------------------------------------------------------------------------------------
			// -----------------------------------------------------------------------------------------------------------------
			// Shut the logging subsystem down
			// -----------------------------------------------------------------------------------------------------------------
			// -----------------------------------------------------------------------------------------------------------------

			// Shut the logging subsystem down
			Log.Shutdown();
		}
	}

}
