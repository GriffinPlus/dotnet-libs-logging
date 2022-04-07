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

using GriffinPlus.Lib.Logging.Elasticsearch;

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
		private static readonly LogWriter sLog1 = LogWriter.Get<MyClass1>();       // actual log writer name: GriffinPlus.Lib.Logging.Demo.MyClass1
		private static readonly LogWriter sLog2 = LogWriter.Get<MyClass2>();       // actual log writer name: GriffinPlus.Lib.Logging.Demo.MyClass2
		private static readonly LogWriter sLog3 = LogWriter.Get<MyClass3>();       // actual log writer name: GriffinPlus.Lib.Logging.Demo.MyClass3
		private static readonly LogWriter sLog4 = LogWriter.Get(typeof(MyClass4)); // actual log writer name: GriffinPlus.Lib.Logging.Demo.MyClass4
		private static readonly LogWriter sLog5 = LogWriter.Get(typeof(MyClassA)); // actual log writer name: GriffinPlus.Lib.Logging.Demo.MyClassA
		private static readonly LogWriter sLog6 = LogWriter.Get(typeof(MyClassB)); // actual log writer name: GriffinPlus.Lib.Logging.Demo.MyClassB

		// Register a log writer using a custom name.
		private static readonly LogWriter sLog7 = LogWriter.Get("My Fancy Writer");

		// Create tagging log writers
		private static readonly LogWriter sLog_TagA  = sLog7.WithTag("TagA");          // same as sLog7, but tags messages with 'TagA'
		private static readonly LogWriter sLog_TagB  = sLog7.WithTag("TagB");          // same as sLog7, but tags messages with 'TagB'
		private static readonly LogWriter sLog_TagBC = sLog7.WithTags("TagB", "TagC"); // same as sLog7, but tags messages with 'TagB' and 'TagC'

		private static void Main(string[] args)
		{
			// Initialize the logging subsystem
			bool initConfig = false;
			Log.Initialize<VolatileLogConfiguration>( // volatile configuration (programmatic configuration only, no persistence)
				// Log.Initialize<FileBackedLogConfiguration>( // file-based configuration (default location, beside the executable with file extension '.gplogconf')
				config =>
				{
					// VolatileLogConfiguration only:
					// Always initialize the configuration programmatically
					initConfig = false; // for VolatileLogConfiguration

					// FileBackedLogConfiguration only:
					// Initialize the configuration only, if the configuration file does not exist, yet
					// config.Path = "./my-conf.gplogconf"; // override the default location of the file
					// initConfig = !File.Exists(config.FullPath);

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
					}
				},
				builder =>
				{
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
					builder.Add<ConsoleWriterPipelineStage>(
						"Console",
						stage =>
						{
							stage.MessageQueueSize = 500;                                              // buffer up to 500 messages (default)
							stage.DiscardMessagesIfQueueFull = false;                                  // block if the queue is full (default)
							stage.ShutdownTimeout = TimeSpan.FromMilliseconds(5000);                   // wait up to 5000ms for the stage to shut down (default)
							stage.Formatter = tableFormatter;                                          // use specific formatter
							stage.DefaultStream = ConsoleOutputStream.Stdout;                          // print to stdout by default (default)
							stage.MapLogLevelToStream(LogLevel.Emergency, ConsoleOutputStream.Stderr); // print errors to stderr
							stage.MapLogLevelToStream(LogLevel.Alert, ConsoleOutputStream.Stderr);     // 
							stage.MapLogLevelToStream(LogLevel.Critical, ConsoleOutputStream.Stderr);  // 
							stage.MapLogLevelToStream(LogLevel.Error, ConsoleOutputStream.Stderr);     //
						});

					// Create pipeline stage for writing to a file
					builder.Add<FileWriterPipelineStage>(
						"File",
						stage =>
						{
							stage.Path = "Unnamed.log";                              // Path of the file to write to (default)
							stage.Append = false;                                    // do not append written messages to existing log file (default)
							stage.MessageQueueSize = 500;                            // buffer up to 500 messages (default)
							stage.DiscardMessagesIfQueueFull = false;                // block if the queue is full (default)
							stage.ShutdownTimeout = TimeSpan.FromMilliseconds(5000); // wait up to 5000ms for the stage to shut down (default)
							stage.Formatter = jsonFormatter;                         // use specific formatter
						});

					// Create pipeline stage that forwards to Elasticsearch using the Elasticsearch Common Schema (ECS) version 1.10.
					// The stage supports the following password-based authentication schemes:
					// - Basic authentication (with custom credentials only)
					// - Digest authentication (with custom credentials only)
					// - NTLM Authentication (with custom credentials and login user credentials)
					// - Kerberos Authentication (with custom credentials and login user credentials)
					// - Negotiate Authentication (with custom credentials and login user credentials)
					builder.Add<ElasticsearchPipelineStage>(
						"Elasticsearch",
						stage =>
						{
							stage.ApiBaseUrls = new[] { new Uri("http://127.0.0.1:9200/") };  // use local elasticsearch server
							stage.AuthenticationSchemes = AuthenticationScheme.PasswordBased; // support all password based authentication schemes
							stage.Username = "";                                              // username to use when authenticating (empty to use login user)
							stage.Password = "";                                              // password to use when authenticating (empty to use login user)
							stage.Domain = "";                                                // domain to use when authenticating (for schemes 'Digest', 'NTLM', 'Kerberos' and 'Negotiate')
							stage.BulkRequestMaxConcurrencyLevel = 5;                         // maximum number of requests on the line
							stage.BulkRequestMaxSize = 5 * 1024 * 1024;                       // maximum size of a bulk request
							stage.BulkRequestMaxMessageCount = 0;                             // maximum number of messages in a bulk request (0 = unlimited)
							stage.IndexName = "logs";                                         // elasticsearch index to write log messages into
							stage.OrganizationId = "";                                        // value of the 'organization.id' field
							stage.OrganizationName = "";                                      // value of the 'organization.name' field
							stage.SendQueueSize = 50000;                                      // maximum number of messages the stage buffers before discarding messages
						});
				});

			// Save configuration, if it was initialized programmatically (after initialization the configuration is bound to
			// the pipeline stages, so stages can persist their settings in the configuration)
			if (initConfig) Log.Configuration.Save(true);

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
