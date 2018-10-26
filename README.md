# Griffin+ Logging

[![Build (master)](https://img.shields.io/appveyor/ci/ravenpride/dotnet-libs-logging/master.svg?logo=appveyor)](https://ci.appveyor.com/project/ravenpride/dotnet-libs-logging/branch/master)
[![Tests (master)](https://img.shields.io/appveyor/tests/ravenpride/dotnet-libs-logging/master.svg?logo=appveyor)](https://ci.appveyor.com/project/ravenpride/dotnet-libs-logging/branch/master/tests)
[![NuGet Version](https://img.shields.io/nuget/v/GriffinPlus.Lib.Logging.svg)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging)
[![NuGet Downloads](https://img.shields.io/nuget/dt/GriffinPlus.Lib.Logging.svg)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging)

## Overview

*Griffin+ Logging* is a simple, but modular extensible logging facility that focusses on applications built on the .NET framework. It addresses many issues that have arised during multiple years of developing .NET libraries and applications. *Griffin+ Logging* is part of the *Griffin+* library suite and used in other *Griffin+* projects to channelize and process log message streams. Nevertheless *Griffin+ Logging* can be used in other projects as well as it does not have any dependencies that would clutter other projects.

## Supported Platforms

The logging subsystem is entirely written in C# using .NET Standard 2.0.

Therefore it should work on the following platforms (or higher):
- .NET Framework 4.6.1
- .NET Core 2.0
- Mono 5.4
- Xamarin iOS 10.14
- Xamarin Mac 3.8
- Xamarin Android 8.0
- Universal Windows Platform (UWP) 10.0.16299

## Coarse Overview and Terminology

*Griffin+ Logging* consists of a couple of classes defined in the `GriffinPlus.Lib.Logging` namespace. The main pillars are the following classes:

- `Log`
- `LogWriter`
- `LogLevel`

The `Log` class is the crucial point of the logging subsystem. It provides access to the configuration of the logging subsystem that determines which messages get logged. If the code is heavily instrumented, the configuration is a good means of specifically reducing the number of messages that arise. Even if *Griffin+ Logging* is very lightweight, a large number of messages is always accompanied by a certain loss in performance. Log messages are filtered right at the beginning, even before the messages are formatted eliminating unnecessary overhead.

Furthermore the `Log` class creates log writers. A log writer - represented by the `LogWriter` class - corresponds to a certain piece of code emitting log messages. Log writers are identified by their name. Usually this is the full name of a class, but it can be an arbitrary name as well. The `LogWriter` class offers a large set of overloads for writing simple and formatted messages. Generic overloads avoid boxing of value types when passing arguments to the `Write(...)` methods, thus reducing the load on the garbage collection.

Each and every log message is associated with a log level represented by the `LogLevel` class. A log level indicates how severe the issue is the message is about. A log level has an integer id that expresses the severity: the lower the log level id the more severe the issue. The log level id is crucial when it comes to filtering by applying a log level threshold (see configuration section below). The `LogLevel` class contains a few commonly used predefined log levels and a recommendation when these log levels should be used:

| Id  | Log Level   | Description
|:---:|:------------|:----------------------------------------------------------------------------------------------
|   0 | `Failure`   | The log message is about a severe error condition that threatens the system's stability.
|   1 | `Error`     | The log message is about a "normal" error condition.
|   2 | `Warning`   | The log message is not about an error condition, but something a user should keep an eye on.
|   3 | `Note`      | The log message is a note a regular user should see.
|   4 | `Developer` | A log message only developers should see.
|   5 | `Trace0`    | A log message the implementer of the code might be interested in (least detailed)
| ... |             | ...
|  24 | `Trace19`   | A log message the implementer of the code might be interested in (most detailed)

In addition to the predefined log levels an aspect log level can be defined. Aspect log levels are primarily useful when tracking an issue that effects multiple classes. The ids of aspect log levels directly follow the predefined log levels.

## Using

### Configuration

By default *Griffin+ Logging* comes with a configuration that allows all messages associated with log levels destined for a user's eyes to be logged, namely log level `Failure`, `Error`, `Warning` and `Note`. Messages associated with other log levels are blocked. There is no restriction for log writers. The default log configuration is purely in-memory, but you can quickly tell *Griffin+ Logging* to retrieve settings from a file just by adding the following line:

```csharp
Log.Configuration = new FileBackedLogConfiguration();
```

This will drop a configuration file containing the default settings into the applications's base directory. The configuration file is named as the application plus extension `.logconf`. An application named `MyApp.exe` will use `MyApp.logconf` as configuration file.

The default configuration file contains a detailed description of the settings and looks like the following:

```
; ------------------------------------------------------------------------------
; Configuration of the Logging Subsystem
; ------------------------------------------------------------------------------
; This file configures the logging subsystem that is encorporated in the
; application concerned. Each and every executable that makes use of the logging
; subsystem has its own configuration file (extension: .logconf) that is located
; beside the application's executable. The configuration is structured like an
; ini-file, i.e. it consists of sections and properties. A section defines a
; configuration scope while properties contain the actual settings within a
; section.
; ------------------------------------------------------------------------------

; ------------------------------------------------------------------------------
; Global Settings
; ------------------------------------------------------------------------------

[Settings]
ApplicationName = MyApp

; ------------------------------------------------------------------------------
; Log Writer Settings
; ------------------------------------------------------------------------------
; The log writer configuration may consist of multiple [LogWriter] sections
; defining active log levels for log writers with a name matching the specified
; pattern. The pattern can be expressed as a wildcard pattern ('WildcardPattern'
; property) or as a regular expression ('RegexPattern' property). Multiple
; [LogWriter] sections are evaluated top-down. The first matching section
; defines the behavior of the log writer. Therefore a default settings section
; matching all log writers should be specified last.
;
; The logging module comes with a couple of predefined log levels expressing a
; wide range of severities:
; - Failure          (most severe)
; - Error                  .
; - Warning                .
; - Note                   .
; - Developer              .
; - Trace0                 .
; - Trace[1..18]           .
; - Trace19          (least severe)
;
; Furthermore aspect log levels can be used to keep log messages belonging to
; a certain subject together. This is especially useful when multiple log
; writers contribute log messages to the subject. Aspect log levels enlarge the
; list of log levels shown above and can be used just as the predefined log
; levels.
;
; The 'Level' property defines a base log level for matching log writers, i.e.
; setting 'Level' to 'Note' tells the log writer to write log messages with
; at least log level 'Note', e.g. 'Note', 'Warning', 'Error' and 'Failure'.
; Do not use aspect log levels here, since the order of aspect log levels is
; not deterministic, especially in multi-threaded environments.
;
; The 'Include' property allows including certain log levels that are not
; covered by the 'Level' property. Multiple log levels can be separated by
; commas. Alternatively multiple 'Include' properties can be used.
;
; The 'Exclude' property has the opposite effect. It tells the log writer to
; keep log messages with a certain log level out of the log. Multiple log
; levels can be separated by commas. Alternatively multiple 'Exclude' properties
; can be used.
; ------------------------------------------------------------------------------

[LogWriter]
WildcardPattern = *
Level = Note
```

### Requesting a Log Writer

If you want to write a message to the log, you first have to request a `LogWriter` at the `Log` class. A log writer provides various ways of formatting log messages. It is perfectly fine to keep a single `LogWriter` instance in a static member variable as log writers are thread-safe, so you only need one instance for multiple threads. A log writer has a unique name usually identifying the piece of code that emits log messages. This is often the name of the class, although the name can be chosen freely. In this case you can simply pass the type of the corresponding class when requesting a log writer. The name will automatically be set to the full name of the specified type. A positive side effect of using a type is that the name of the log writer changes, if the name of the type changes or even the namespace the type is defined in. It is refactoring-safe.

You can obtain a `LogWriter` by calling one of the following `Log` methods:

```csharp
public static LogWriter GetWriter<T>();
public static LogWriter GetWriter(Type type);
public static LogWriter GetWriter(string name);
```

### Choosing a Log Level

Each message written to the log is associated with a certain log level, represented by the `LogLevel` class. The log level indicates the severity of the log message. *Griffin+ Logging* comes with a set of predefined log levels as described above. In addition to the predefined log levels an aspect log level can be used. Aspect log levels are primarily useful when tracking an issue that effects multiple classes. The following `LogLevel` method creates an aspect log level:

```csharp
public GetAspect(string name);
```

### Writing a Message

Once you've a `LogWriter` and a `LogLevel` you can use one of the following `LogWriter` methods to write a message:

```csharp
// without formatting
public void Write(LogLevel level, string message);

// formatting with default format provider (invariant culture), bypasses filters applied by the log configuration
public void ForceWrite(LogLevel level, string format, params object[] args);

// formatting with default format provider (invariant culture), up to 15 parameters
public void Write<T>(LogLevel level, string format, T arg);
public void Write<T0,T1>(LogLevel level, string format, T0 arg0, T1 arg1);
public void Write<T0,T1,T2>(LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2);
public void Write<T0,T1,T2,T3>(LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3);
public void Write<T0,T1,T2,T3,T4>(LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
public void Write<T0,T1,T2,T3,T4,T5>(LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
public void Write<T0,T1,T2,T3,T4,T5,T6>(LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7>(LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8>(LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8,T9>(LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10>(LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11>(LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12>(LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13>(LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14>(LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14);

// formatting with default format provider (invariant culture), for more than 15 parameters
public void Write(LogLevel level, string format, params object[] args);

// formatting with custom format provider, bypasses filters applied by the log configuration
public void ForceWrite(IFormatProvider provider, LogLevel level, string format, params object[] args);

// formatting with custom format provider, up to 15 parameters
public void Write<T>(IFormatProvider provider, LogLevel level, string format, T arg);
public void Write<T0,T1>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1);
public void Write<T0,T1,T2>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2);
public void Write<T0,T1,T2,T3>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3);
public void Write<T0,T1,T2,T3,T4>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
public void Write<T0,T1,T2,T3,T4,T5>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
public void Write<T0,T1,T2,T3,T4,T5,T6>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8,T9>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13);
public void Write<T0,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14>(IFormatProvider provider, LogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14);

// formatting with custom format provider (invariant culture), for more than 15 parameters
public void Write(IFormatProvider provider, LogLevel level, string format, params object[] args);
```

### Customization

*Griffin+ Logging* can be used as shipped, if you need a logging facility that is configurable as described above and that prints messages to *stdout* and *stderr*. If this is not what you need, you can replace the *log configuration* and the *log message processing pipeline*.

The main purpose of the *log configuration* is loading logging specific settings and providing information about which log levels should be enabled on which log writers. If you feel that is something you want to customize, simply implement the `ILogConfiguration` interface and tell the `Log` class to use your implementation via its `Configuration` property.

Probably customization of the *log message processing pipeline* is a more interesting issue. The pipeline is fed with log messages that pass the filter defined by the *log configuration*. A pipeline stage class must implement the `IProcessingPipelineStage` interface. For the sake of simplicity, `ProcessingPipelineStage` is a base class that implements the common parts that rarely need to be overridden. This class provides a `FollowedBy()` method that allows you to chain multiple pipeline stages in a fluent API fashion.

### Complete Example

The following example shows how *Griffin+ Logging* can be used. The source code is available in the demo project contained in the repository as well:

```csharp
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

			// configure the log message processing pipeline (only one stage here)
			// and arrage the columns to print
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
```

