# Griffin+ Logging

[![Azure DevOps builds (branch)](https://img.shields.io/azure-devops/build/griffinplus/2f589a5e-e2ab-4c08-bee5-5356db2b2aeb/26/master?label=Build)](https://dev.azure.com/griffinplus/DotNET%20Libraries/_build/latest?definitionId=26&branchName=master)
[![Tests (master)](https://img.shields.io/azure-devops/tests/griffinplus/DotNET%20Libraries/26/master?label=Tests)](https://dev.azure.com/griffinplus/DotNET%20Libraries/_build/latest?definitionId=26&branchName=master)
[![NuGet Version](https://img.shields.io/nuget/v/GriffinPlus.Lib.Logging.svg?label=NuGet%20Version)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging)
[![NuGet Downloads](https://img.shields.io/nuget/dt/GriffinPlus.Lib.Logging.svg?label=NuGet%20Downloads)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging)

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

The main purpose of the *log configuration* is loading logging specific settings and providing information about which log levels should be enabled on which log writers. By default *Griffin+ Logging* comes with a configuration that allows all messages associated with log levels destined for a user's eyes to be logged, namely log level `Failure`, `Error`, `Warning` and `Note`. Messages associated with other log levels are blocked. There is no restriction for log writers. The default log configuration is purely in-memory.

You can tell *Griffin+ Logging* to use your own configuration by setting the `Log.Configuration` property. A configuration class only needs to implement the `ILogConfiguration` interface to work with the logging subsystem. Of course, you can implement a configuration class on your own, but in most cases one of the shipped implementations should suffice.

#### In-Memory Log Configuration

The `VolatileLogConfiguration` class provides a log configuration that lives in memory only. You can programmatically configure it to suit your needs.

Please see the example below on how to configure log writers using the fluent API.

```csharp
Log.Configuration = new VolatileLogConfiguration();
```

#### File-Backed Log Configuration

The `FileBackedConfiguration` class is a log configuration that is backed by an ini-like file. By default the file is expected to be located beside the executable with the same name as the executable, but with the extension `.logconf`. For example, the executable `MyApp.exe` will use the configuration file `MyApp.logconf`. Optionally you can choose to place the configuration file somewhere else. The configuration file can be adjusted while the application is running. It is reloaded automatically if changes are detected.

Please see the example below on how to configure log writers using the fluent API.

```csharp
Log.Configuration = new FileBackedLogConfiguration();                    // default location
Log.Configuration = new FileBackedLogConfiguration("./my-conf.logconf"); // custom location
```

The default configuration file contains a detailed description of the settings and looks like the following:

```
; ------------------------------------------------------------------------------
; Configuration of the Logging Subsystem
; ------------------------------------------------------------------------------
; This file configures the logging subsystem that is incorporated in the
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

### Log Message Processing Pipeline

*Griffin+ Logging* features a flexible *log message processing pipeline* that is fed with log messages that pass the filter defined by the *log configuration*. By default log messages are simply written to the console (*stdout*) using a tabular layout. The *log message pipeline* can be replaced by setting the `Log.LogMessageProcessingPipeline` property to a pipeline stage of your choice.

A pipeline stage class must implement the `IProcessingPipelineStage` interface. For the sake of simplicity, `ProcessingPipelineStage` is a base class that implements the common parts that rarely need to be overridden. For pipeline stages that perform I/O it is recommended to use the `AsyncProcessingPipelineStage` as base class to decouple I/O from the thread that is writing a log message. Both classes provide a `FollowedBy()` method that allows you to chain multiple pipeline stages in a fluent API fashion.

*Griffin+ Logging* comes with the following processing pipeline stages:

- `CallbackPipelineStage`
  - A processing pipeline stage that simply invokes a specified callback to process log messages
  - Processing is done synchronously
- `ConsoleWriterPipelineStage`
  - A processing pipeline stage that writes log messages to the console (*stdout* or *stderr*, can be configured per log level)
  - Printed text can be influenced using a formatter that can be plugged in as a strategy (default: tabular layout)
  - Processing is done asynchronously
- `FileWriterPipelineStage`
  - A processing pipeline stage that writes log messages to a custom file
  - Written text can be influenced using a formatter that can be plugged in as a strategy (default: tabular layout)
  - Processing is done asynchronously

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

### Complete Example

The following example shows how *Griffin+ Logging* can be used. The source code is available in the demo project contained in the repository as well:

```csharp
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
                config.Save();
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
```

