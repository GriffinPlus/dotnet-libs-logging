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
using System.IO;
using System.Threading;

namespace GriffinPlus.Lib.Logging.Demo
{

	class MyClass1 { }
	class MyClass2 { }
	class MyClass3 { }
	class MyClass4 { }
	class MyClassA { }
	class MyClassB { }

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

		static void Main(string[] args)
		{
			// By default the logging subsystem is set up to use a pure in-memory configuration and a console logger
			// printing written messages to the console (stdout). In many cases you probably want to configure
			// what gets logged using a configuration file. The following example shows a simple, but complete setup
			// of the logging subsystem.

			// Create a volatile (in-memory) or persistent (file-backed) configuration
			LogConfiguration config = new VolatileLogConfiguration();
			// LogConfiguration config = new FileBackedLogConfiguration();                    // default location (beside the executable with file extension '.logconf');
			// LogConfiguration config = new FileBackedLogConfiguration("./my-conf.logconf"); // custom location

			// Adjust configuration to your needs.
			// This works for volatile and persistent configurations, but is usually only needed for volatile configurations.
			config = config

				// Add configuration for log writer 'GriffinPlus.Lib.Logging.Demo.MyClass1' only
				// - set base log level to 'Note' => enables log level 'Failure', 'Error', 'Warning' and 'Note'
				// - include log level 'Trace0'
				// - exclude log level 'Warning'
				// => enabled log levels: 'Failure', 'Error', 'Note', 'Trace0'
				.WithLogWriter<MyClass1>(x => x
					.WithBaseLevel(LogLevel.Note)
					.WithLevel(LogLevel.Trace0)
					.WithoutLevel("Warning"))

				// Add configuration for log writer 'GriffinPlus.Lib.Logging.Demo.MyClass2' only
				// - set base log level to 'None' effectively silencing the log writer
				// - no included/excluded log levels
				// => no enabled log levels
				.WithLogWriter(typeof(MyClass2), x=> x
					.WithBaseLevel(LogLevel.None))

				// Add configuration for log writer 'GriffinPlus.Lib.Logging.Demo.MyClass3' only
				// - set base log level to 'All' enabling all log levels (including aspects)
				// - exclude all log levels from 'Trace10' up to 'Trace19'
				// => enabled log levels: All log levels, but 'Trace[10-19]'
				.WithLogWriter(typeof(MyClass3), x => x
					.WithBaseLevel(LogLevel.All)
					.WithoutLevelRange(LogLevel.Trace10, LogLevel.Trace19))

				// Add configuration for log writers matching regex pattern
				// - pattern matches 'GriffinPlus.Lib.Logging.Demo.MyClassA' and 'GriffinPlus.Lib.Logging.Demo.MyClassB'
				// - base level defaults to 'Note' => enables log level 'Failure', 'Error', 'Warning' and 'Note'
				// - include all log levels from 'Trace10' up to 'Trace15'
				// - no excluded log levels
				// => enabled log levels: 'Failure', 'Error', 'Warning', 'Note', 'Trace0'
				.WithLogWritersByRegex("^GriffinPlus.Lib.Logging.Demo.MyClass[A-Z]$", x => x
					.WithLevelRange(LogLevel.Trace10, LogLevel.Trace15))

				// Add configuration for log writers matching wildcard pattern
				// - applys to 'GriffinPlus.Lib.Logging.Demo.MyClass4' only
				//   (other writers are handled by preceding steps)
				// - base level defaults to 'Note' => enables log level 'Failure', 'Error', 'Warning' and 'Note'
				// - include log level 'Trace15'
				// - no excluded log levels
				// => enabled log levels: 'Failure', 'Error', 'Warning', 'Note', 'Trace15'
				.WithLogWritersByWildcard("GriffinPlus.Lib.Logging.Demo.MyClass*", x => x
					.WithLevel(LogLevel.Trace15))

				// Add configuration for log writer 'My Fancy Writer'
				// - base level defaults to level 'Note' => enables log level 'Failure', 'Error', 'Warning' and 'Note'
				// - include aspect log level 'Demo Aspect'
				// - no excluded log levels
				// => enabled log levels: 'Failure', 'Error', 'Warning', 'Note', 'Demo Aspect'
				.WithLogWriter("My Fancy Writer", x => x
					.WithLevel("Demo Aspect"))

				// Add configuration for log writer 'Timing' to enable logging time measurements written by the internal
				// 'Timing' log writer (see below for time measurements)
				.WithLogWriterTiming()

				// Add default configuration for log writers that have not been handled up to this point
				// - base level defaults  to level 'Note'
				// - no included/excluded log levels
				// => enabled log levels: 'Failure', 'Error', 'Warning', 'Note'
				.WithLogWriterDefault();

			// Save file backed configuration file to disk, if it does not exist, yet
			if (config is FileBackedLogConfiguration fbc && !File.Exists(fbc.FullPath)) {
				fbc.Save();
			}

			Log.Configuration = config;

			// Set application name (optional)
			Log.ApplicationName = "Logging Demo";

			// Configure the log message processing pipeline and arrange the columns to print.
			Log.LogMessageProcessingPipeline = new ConsoleWriterPipelineStage()
				.WithQueue(500, false)                             // buffer up to 500 messages and block, if the queue is full (default)
				.WithFormatter(new TableMessageFormatter()
					.WithTimestamp("yyyy-MM-dd HH:mm:ss.fff")      // use custom timestamp format
					.WithProcessId()
					.WithProcessName()
					.WithApplicationName()
					.WithLogWriter()
					.WithLogLevel()
					.WithText()
				).FollowedBy(new FileWriterPipelineStage("mylog.log", false)
					.WithFormatter(new TableMessageFormatter()
						.WithTimestamp()
						.WithText()));

			// Get an aspect log level.
			LogLevel aspect = LogLevel.GetAspect("Demo Aspect");

			// Write messages to all known log levels (predefined log levels + aspects).
			foreach (LogLevel level in LogLevel.KnownLevels)
			{
				sLog1.Write(level, "This is sLog1 writing using level '{0}'.", level.Name);
				sLog2.Write(level, "This is sLog2 writing using level '{0}'.", level.Name);
				sLog3.Write(level, "This is sLog3 writing using level '{0}'.", level.Name);
				sLog4.Write(level, "This is sLog4 writing using level '{0}'.", level.Name);
				sLog5.Write(level, "This is sLog5 writing using level '{0}'.", level.Name);
				sLog6.Write(level, "This is sLog6 writing using level '{0}'.", level.Name);
				sLog7.Write(level, "This is sLog7 writing using level '{0}'.", level.Name);
			}

			// Use a timing logger to determine how long an operation takes. It uses log level 'Timing' and log writer
			// 'Timing' by default, so you need to ensure that the configuration lets these messages pass).
			sLog1.Write(LogLevel.Note, "Presenting a timing logger with default settings...");
			using (TimingLogger.Measure()) {
				Thread.Sleep(500);
			}

			// Use a timing logger, customize the log writer/level it uses and associate an operation name with the
			// measurement that is printed to the log as well.
			sLog1.Write(LogLevel.Note, "A timing logger with custom log level/writer and operation name...");
			using (TimingLogger.Measure(sLog1, LogLevel.Note, "Waiting for 500ms")) {
				Thread.Sleep(500);
			}

			// Shut the logging subsystem down
			Log.Shutdown();

			Console.WriteLine();
			Console.WriteLine("Press any key to continue...");
			Console.ReadKey();
		}
	}
}
