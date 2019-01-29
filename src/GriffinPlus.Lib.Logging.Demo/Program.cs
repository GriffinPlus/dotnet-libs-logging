///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
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
using System.IO;
using System.Threading;

namespace GriffinPlus.Lib.Logging.Demo
{
	class Program
	{
		// register a log writer using a type
		// (the actual log writer name becomes: GriffinPlus.Lib.Logging.Demo.Program)
		private static LogWriter sLog1 = Log.GetWriter<Program>();
		private static LogWriter sLog2 = Log.GetWriter(typeof(Program));

		// register a log writer using a custom name
		private static LogWriter sLog3 = Log.GetWriter("My Fancy Writer");

		static void Main(string[] args)
		{
			// By default the logging subsystem is set up to use a pure in-memory configuration and a console logger
			// printing written messages to the console (stdout/stderr). In many cases you probably want to configure
			// what gets logged using a configuration file. The following example shows a simple, but complete setup
			// of the logging subsystem. A file-backed log configuration is used and it's file is placed in the
			// application's base directory named as the application plus extension '.logconf'. After that the log
			// message processing pipeline is initialized using a customized console logger.

			// set configuration
			var config = new FileBackedLogConfiguration(); // default location
			// var config = new FileBackedLogConfiguration("./my-conf.logconf"); // custom location
			Log.Configuration = config;
			
			// save configuration to disk, if it does not exist, yet
			if (!File.Exists(config.FullPath)) {
				config.Save();
			}

			// set application name (optional)
			Log.ApplicationName = "Logging Demo";

			// configure the log message processing pipeline and arrange the columns to print
			// (only one stage here, you can use FollowedBy() to append another stage to this one)
			Log.LogMessageProcessingPipeline = new ConsoleWriterPipelineStage()
				.WithTimestamp("yyyy-MM-dd HH:mm:ss.fff") // use custom timestamp format
				.WithProcessId()
				.WithProcessName()
				.WithApplicationName()
				.WithLogWriterName()
				.WithLogLevel()
				.WithText();


			// create an aspect log level
			LogLevel aspect = LogLevel.GetAspect("Demo Aspect");

			// write messages to all known log levels (predefined log levels + aspects)
			foreach (LogLevel level in LogLevel.KnownLevels)
			{
				sLog1.Write(level, "This is sLog1 writing using level '{0}'.", level.Name);
				sLog2.Write(level, "This is sLog2 writing using level '{0}'.", level.Name);
				sLog3.Write(level, "This is sLog3 writing using level '{0}'.", level.Name);
			}

			// use a timing logger to determine how long an operation takes
			// (is uses log level 'Timing' and log writer 'Timing' by default, so you need
			// to ensure that the configuration lets these messages pass).
			sLog1.Write(LogLevel.Note, "Presenting a timing logger with default settings...");
			using (TimingLogger logger = TimingLogger.Measure()) {
				Thread.Sleep(500);
			}

			// use a timing logger and customize the log writer/level it uses + associate an operation name
			// with the measurement that is printed to the log as well
			sLog1.Write(LogLevel.Note, "A timing logger with custom log level/writer and operation name...");
			using (TimingLogger logger = TimingLogger.Measure(sLog1, LogLevel.Note, "Waiting for 500ms")) {
				Thread.Sleep(500);
			}

			// now modify the configuration file in the output directory and run the demo application
			// again to see what happens!

			Console.WriteLine();
			Console.WriteLine("Press any key to continue...");
			Console.ReadKey();
		}
	}
}