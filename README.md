# Griffin + Logging

[![Azure DevOps builds (branch)](https://img.shields.io/azure-devops/build/griffinplus/2f589a5e-e2ab-4c08-bee5-5356db2b2aeb/26/master?label=Build)](https://dev.azure.com/griffinplus/DotNET%20Libraries/_build/latest?definitionId=26&branchName=master)
[![Tests (master)](https://img.shields.io/azure-devops/tests/griffinplus/DotNET%20Libraries/26/master?label=Tests)](https://dev.azure.com/griffinplus/DotNET%20Libraries/_build/latest?definitionId=26&branchName=master)<!-- ALL-CONTRIBUTORS-BADGE:START - Do not remove or modify this section -->
[![All Contributors](https://img.shields.io/badge/all_contributors-2-orange.svg?style=flat-round)](#contributors-)
<!-- ALL-CONTRIBUTORS-BADGE:END -->

| NuGet Package                                                                                                             |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
|---------------------------------------------------------------------------------------------------------------------------|:-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [GriffinPlus.Lib.Logging](README.md)                                                                                      | [![NuGet Version](https://img.shields.io/nuget/v/GriffinPlus.Lib.Logging.svg?label=Version)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging) [![NuGet Downloads](https://img.shields.io/nuget/dt/GriffinPlus.Lib.Logging.svg?label=Downloads)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging)
| [GriffinPlus.Lib.Logging.Collections](README.md)                                                                          | [![NuGet Version](https://img.shields.io/nuget/v/GriffinPlus.Lib.Logging.Collections.svg?label=Version)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging.Collections) [![NuGet Downloads](https://img.shields.io/nuget/dt/GriffinPlus.Lib.Logging.Collections.svg?label=Downloads)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging.Collections)
| [GriffinPlus.Lib.Logging.ElasticsearchPipelineStage](src/GriffinPlus.Lib.Logging.ElasticsearchPipelineStage/README.md)    | [![NuGet Version](https://img.shields.io/nuget/v/GriffinPlus.Lib.Logging.ElasticsearchPipelineStage.svg?label=Version)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging.ElasticsearchPipelineStage) [![NuGet Downloads](https://img.shields.io/nuget/dt/GriffinPlus.Lib.Logging.ElasticsearchPipelineStage.svg?label=Downloads)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging.ElasticsearchPipelineStage)
| [GriffinPlus.Lib.Logging.LocalLogServicePipelineStage](README.md)                                                         | [![NuGet Version](https://img.shields.io/nuget/v/GriffinPlus.Lib.Logging.LocalLogServicePipelineStage.svg?label=Version)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging.LocalLogServicePipelineStage) [![NuGet Downloads](https://img.shields.io/nuget/dt/GriffinPlus.Lib.Logging.LocalLogServicePipelineStage.svg?label=Downloads)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging.LocalLogServicePipelineStage)
| [GriffinPlus.Lib.Logging.LogFile](README.md)                                                                              | [![NuGet Version](https://img.shields.io/nuget/v/GriffinPlus.Lib.Logging.LogFile.svg?label=Version)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging.LogFile) [![NuGet Downloads](https://img.shields.io/nuget/dt/GriffinPlus.Lib.Logging.LogFile.svg?label=Downloads)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging.LogFile)

## Overview

*Griffin+ Logging* is a simple, but modular extensible logging facility that focusses on applications built on the .NET framework. It addresses many issues that have arised during multiple years of developing .NET libraries and applications. *Griffin+ Logging* is part of the *Griffin+* library suite and used in other *Griffin+* projects to channelize and process log message streams.

## Supported Platforms

The library is entirely written in C# using .NET Standard 2.0.

Therefore it should work on the following platforms (or higher):
- .NET Framework 4.6.1
- .NET Core 2.0
- .NET 5.0
- Mono 5.4
- Xamarin iOS 10.14
- Xamarin Mac 3.8
- Xamarin Android 8.0
- Universal Windows Platform (UWP) 10.0.16299

The library is tested automatically on the following frameworks and operating systems:
- .NET Framework 4.6.1 (Windows Server 2019)
- .NET Core 2.1 (Windows Server 2019 and Ubuntu 20.04)
- .NET Core 3.1 (Windows Server 2019 and Ubuntu 20.04)
- .NET 5.0  (Windows Server 2019 and Ubuntu 20.04)

## Coarse Overview and Terminology

*Griffin+ Logging* consists of a couple of classes defined in the `GriffinPlus.Lib.Logging` namespace. The main pillars are the following classes:

- `Log`
- `LogWriter`
- `LogLevel`

The `Log` class is the crucial point of the logging subsystem. It provides access to the configuration of the logging subsystem that determines which messages get logged. If the code is heavily instrumented, the configuration is a good means of specifically reducing the number of messages that arise. Even if *Griffin+ Logging* is very lightweight, a large number of messages is always accompanied by a certain loss in performance. Log messages are filtered right at the beginning, even before the messages are formatted eliminating unnecessary overhead.

Furthermore the `Log` class creates log writers. A log writer - represented by the `LogWriter` class - corresponds to a certain piece of code emitting log messages. Log writers are identified by their name. Usually this is the full name of a class, but it can be an arbitrary name as well. The `LogWriter` class offers a large set of overloads for writing simple and formatted messages. Generic overloads avoid boxing of value types when passing arguments to the `Write(...)` methods, thus reducing the load on the garbage collection.

Each and every log message is associated with a log level represented by the `LogLevel` class. A log level indicates how severe the issue is the message is about. A log level has an integer id that expresses the severity: the lower the log level id the more severe the issue. The log level id is crucial when it comes to filtering by applying a log level threshold (see configuration section below). The log levels correspond to log levels known from *syslog*. Their log level id is the same as the corresponding severity level used by *syslog*, so it is easy to integrate *Griffin+ Logging* into an existing logging infrastructure. The `LogLevel` class contains a few commonly used predefined log levels and a recommendation when these log levels should be used:


| Id  | Log Level       | Description
|:---:|:----------------|:----------------------------------------------------------------------------------------------
|   0 | `Emergency`     | Absolute "panic" condition, the system is unusable.
|   1 | `Alert`         | Something bad happened, immediate attention is required.
|   2 | `Critical`      | Something bad is about to happen, immediate attention is required.
|   3 | `Error`         | Non-urgent failure in the system that needs attention.
|   4 | `Warning`       | Something will happen if it is not dealt within a timeframe.
|   5 | `Notice`        | Normal but significant condition that might need special handling.
|   6 | `Informational` | Informative but not important.
|   7 | `Debug`         | Only relevant for developers.
|   8 | `Trace`         | Only relevant for implementers.

In addition to the predefined log levels an aspect log level can be defined. Aspect log levels are primarily useful when tracking an issue that effects multiple classes. The ids of aspect log levels directly follow the predefined log levels.

Another option to split up the configuration is *tagging*. Log writers can be configured to attach tags to written log messages. In addition to just matching the name of a log writer tags provide a flexible way to group log writers, e.g. to express their membership to a subsystem. This enables writing applications that can configure logging on a per-subsystem basis without losing the original name of the log writer (which is almost always the name of the class the log writer belongs to).

## Using

### System Logger Configuration

*Griffin+ Logging* itself is a logging facility, but it cannot use itself to communicate incidents. If something goes wrong within the logging subsystem, the system logger is used to communicate issues.

On *Windows* the *Event Log* is used. An application needs to register a *log source*, so messages can be differentiated in the log. The following registry snippet adds a source for the application `MyApp.exe`. Please replace `MyApp` with the name of your application's executable (without the `.exe`). The application will also run without registering a log source, but the event log will look a bit messy.

```
Windows Registry Editor Version 5.00
[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\EventLog\Application\MyApp]
"EventMessageFile" = "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\EventLogMessages.dll"
```

If the application is running with administrative rights, it will register the log source on its own.

### Configuration

The main purpose of the *log configuration* is loading logging specific settings and providing information about which log levels should be enabled on which log writers. By default *Griffin+ Logging* comes with a configuration that allows all messages associated with log levels destined for a user's eyes to be logged, namely log level `Emergency`, `Alert`, `Critical`, `Error`, `Warning` and `Notice`. Messages associated with other log levels are blocked. There is no restriction for log writers. The default log configuration is purely in-memory.

You can tell *Griffin+ Logging* to use your own configuration by setting the `Log.Configuration` property. A configuration class only needs to implement the `ILogConfiguration` interface to work with the logging subsystem. Of course, you can implement a configuration class on your own, but in most cases one of the shipped implementations should suffice.

#### In-Memory Log Configuration

The `VolatileLogConfiguration` class provides a log configuration that lives in memory only. You can programmatically configure it to suit your needs.

Please see the example below on how to configure it.

```csharp
Log.Configuration = new VolatileLogConfiguration();
```

#### File-Backed Log Configuration

The `FileBackedConfiguration` class is a log configuration that is backed by an ini-like file. By default the file is expected to be located beside the executable with the same name as the executable, but with the extension `.gplogconf`. For example, the executable `MyApp.exe` will use the configuration file `MyApp.gplogconf`. Optionally you can choose to place the configuration file somewhere else. The configuration file can be adjusted while the application is running. It is reloaded automatically if changes are detected.

Please see the example below on how to configure it.

```csharp
Log.Configuration = new FileBackedLogConfiguration();                      // default location
Log.Configuration = new FileBackedLogConfiguration("./my-conf.gplogconf"); // custom location
```

The default configuration file contains a detailed description of the settings and looks like the following:

```
; ------------------------------------------------------------------------------
; Configuration of the Logging Subsystem
; ------------------------------------------------------------------------------
; This file configures the logging subsystem that is incorporated in the
; application concerned. The configuration is structured similar to an ini-file,
; i.e. it consists of sections and properties. A section defines a configuration
; scope while properties contain the actual settings within a section.
; ------------------------------------------------------------------------------

; ------------------------------------------------------------------------------
; Global Settings
; ------------------------------------------------------------------------------

[Settings]
ApplicationName = GriffinPlus.Lib.Logging.Demo

; ------------------------------------------------------------------------------
; Log Writer Settings
; ------------------------------------------------------------------------------
; The log writer configuration may consist of multiple [LogWriter] sections
; defining active log levels for log writers with a name matching the specified
; pattern. The pattern can match exactly one log writer or a set of log writers
; using a wildcard pattern or a .NET style regular expression. The pattern can
; be specified using the 'Name' property. If an exact match is required, the
; pattern must start with an equality sign (=). If a regular expression should
; be used, the pattern must begin with a caret (^) and end with a dollar sign ($),
; the anchors for the beginning and the end of a line. Any other pattern is
; interpreted as a wildcard pattern.
;
; Log writers can be configured to attach custom tags to written log messages.
; These log writers allow applications to split up the log writer configuration
; even further. The 'Tag' property can be used to match tagging writers. The
; 'Tag' property supports the same patterns as the 'Name' property (see above).
; If the 'Tag' property is not set, tag matching is disabled and only name
; matching is in effect.
;
; Multiple [LogWriter] sections are evaluated top-down. The first matching
; section defines the behavior of the log writer. Therefore a default settings
; section matching all log writers should be specified last.
;
; The logging module comes with a couple of predefined log levels expressing a
; wide range of severities:
; - Emergency        (most severe)
; - Alert                  .
; - Critical               .
; - Error                  .
; - Warning                .
; - Notice                 .
; - Informational          .
; - Debug                  .
; - Trace           (least severe)
;
; Furthermore aspect log levels can be used to keep log messages belonging to
; a certain subject together. This is especially useful when multiple log
; writers contribute log messages to the subject. Aspect log levels enlarge the
; list of log levels shown above and can be used just as the predefined log
; levels.
;
; The 'Level' property defines a base log level for matching log writers, i.e.
; setting 'Level' to 'Notice' tells the log writer to write log messages with
; at least log level 'Notice', e.g. 'Notice', 'Warning', 'Error', 'Critical',
; 'Alert' and 'Emergency'. Do not use aspect log levels here, since the order
; of aspect log levels is not deterministic, especially in multi-threaded
; environments.
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
Name = *
Level = Notice
```

### Log Message Processing Pipeline

*Griffin+ Logging* features a flexible *log message processing pipeline* that is fed with log messages that pass the filter defined by the *log configuration*. By default log messages are simply written to the console (*stdout*) using a tabular layout. The *log message pipeline* can be replaced by setting the `Log.ProcessingPipeline` property to a pipeline stage of your choice.

A pipeline stage class must implement the `IProcessingPipelineStage` interface. For the sake of simplicity, `ProcessingPipelineStage` is a base class that implements the common parts that rarely need to be overridden. For pipeline stages that perform I/O it is recommended to use the `AsyncProcessingPipelineStage` as base class to decouple I/O from the thread that is writing a log message. Both classes provide an `AddNextStage()` method that allows you to chain multiple pipeline stages. Pipeline stages can influence whether a log message is passed to subsequent stages by the return value of their `IProcessingPipelineStage.Process()` method. If `IProcessingPipelineStage.Process()` returns `true` the log message is passed to subsequent stages. Returning `false` stops processing at this stage.

*Griffin+ Logging* comes with the following processing pipeline stages:

- Generic Pipeline Stages
  - Implementations
    - `CallbackPipelineStage`
        - Log messages are processed by invoking specified callbacks
        - Supports synchronous processing
    - `AsyncCallbackPipelineStage`
        - Log messages are processed by invoking specified callbacks
        - Supports synchronous and asynchronous processing
    - `SplitterPipelineStage`
        - Log messages are unconditionally passed to the following stages
- Pipeline Stages emitting Text
  - Text can be influenced using a formatter that can exchanged via the `Formatter` property
    - `TableMessageFormatter` : Log messages are formatted in a tabular fashion
    - `JsonMessageFormatter` : Log messages are formatted as flat JSON documents
  - Implementations
    - `ConsoleWriterPipelineStage`
        - Log messages are printed to the console as defined by the formatter
        - Log messages can be written to *stdout* or *stderr* (depending on their log level)
        - Processing is done asynchronously
    - `FileWriterPipelineStage`
        - Log messages are written to a custom file as defined by the formatter
        - Processing is done asynchronously
- Pipeline Stages writing a queryable log file
  - Impementations
    - `LogFilePipelineStage`
      - Log Messages are written to a file using the `LogFile` class, a log file based on *SQLite*
      - The written log file can be accessed as a regular collection and filtered efficiently
      - Available via *NuGet* package `GriffinPlus.Lib.Logging.LogFile`
      - Processing is done asynchronously
- Pipeline Stages forwarding messages to other logging systems
  - Implementations
    - `ElasticsearchPipelineStage`
      - Log messages are forwarded to an Elasticsearch cluster
      - Message documents comply with the [Elasticsearch Common Schema (ECS) version 1.10](https://www.elastic.co/guide/en/ecs/current/index.html)
      - Available via *NuGet* package `GriffinPlus.Lib.Logging.ElasticsearchPipelineStage`
      - [Documentation](src/GriffinPlus.Lib.Logging.ElasticsearchPipelineStage/README.md)
    - `LocalLogServicePipelineStage` (***proprietary, Windows only***)
      - Log messages are forwarded to a service via a shared memory queue ensuring high performance
      - Messages up to a process crash are available as the stage does not buffer messages in-process
      - Available via *NuGet* package `GriffinPlus.Lib.Logging.LocalLogServicePipelineStage`
      - *At the moment there is no publicly available implementation of the service*

### Requesting a Log Writer

If you want to write a message to the log, you first have to request a `LogWriter` at the `Log` class. A log writer provides various ways of formatting log messages. It is perfectly fine to keep a single `LogWriter` instance in a static member variable as log writers are thread-safe, so you only need one instance for multiple threads. A log writer has a unique name usually identifying the piece of code that emits log messages. This is often the name of the class, although the name can be chosen freely. In this case you can simply pass the type of the corresponding class when requesting a log writer. The name will automatically be set to the full name of the specified type. A positive side effect of using a type is that the name of the log writer changes, if the name of the type changes or even the namespace the type is defined in. It is refactoring-safe.

You can obtain a `LogWriter` by calling one of the following `Log` methods:

```csharp
public static LogWriter GetWriter<T>();
public static LogWriter GetWriter(Type type);
public static LogWriter GetWriter(string name);
```

Log writers can be configured to attach custom tags to written log messages. This enables writing applications that can configure logging on a per-subsystem basis without losing the original name of the log writer (which is almost always the name of the class the log writer belongs to). To let an existing log writer add tags to written messages a new log writer with tagging behavior can be derived from the log writer using one of the following `LogWriter` methods:

```csharp
public LogWriter WithTag(string tag);
public LogWriter WithTags(params string[] tags);
```

Tags may consist of the following characters only:
- alphanumeric characters: `[a-z]`, `[A-Z]`, `[0-9]`
- extra characters: `_`, `.`, `,`, `:`, `;`, `+`, `-`, `#`
- brackets: `(`, `)`, `[`, `]`, `{`, `}`, `<`, `>`

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

// formatting with custom format provider, for more than 15 parameters
public void Write(IFormatProvider provider, LogLevel level, string format, params object[] args);
```

### Complete Example

The following example shows how *Griffin+ Logging* can be used. The source code is available in the demo project contained in the repository as well:

```csharp
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

            // Create pipeline stage that forwards to Elasticsearch using the Elasticsearch Common Schema (ECS) version 1.10.
            // The stage supports the following password-based authentication schemes:
            // - Basic authentication (with custom credentials only)
            // - Digest authentication (with custom credentials only)
            // - NTLM Authentication (with custom credentials and login user credentials)
            // - Kerberos Authentication (with custom credentials and login user credentials)
            // - Negotiate Authentication (with custom credentials and login user credentials)
            var elasticsearchStage = new ElasticsearchPipelineStage("Elasticsearch");
            elasticsearchStage.ApiBaseUrls = new[] { new Uri("http://127.0.0.1:9200/") };  // use local elasticsearch server
            elasticsearchStage.AuthenticationSchemes = AuthenticationScheme.PasswordBased; // support all password based authentication schemes
            elasticsearchStage.Username = "";                                              // username to use when authenticating (empty to use login user)
            elasticsearchStage.Password = "";                                              // password to use when authenticating (empty to use login user)
            elasticsearchStage.Domain = "";                                                // domain to use when authenticating (for schemes 'Digest', 'NTLM', 'Kerberos' and 'Negotiate')
            elasticsearchStage.BulkRequestMaxConcurrencyLevel = 5;                         // maximum number of requests on the line
            elasticsearchStage.BulkRequestMaxSize = 5 * 1024 * 1024;                       // maximum size of a bulk request
            elasticsearchStage.BulkRequestMaxMessageCount = 0;                             // maximum number of messages in a bulk request (0 = unlimited)
            elasticsearchStage.IndexName = "logs";                                         // elasticsearch index to write log messages into
            elasticsearchStage.OrganizationId = "";                                        // value of the 'organization.id' field
            elasticsearchStage.OrganizationName = "";                                      // value of the 'organization.name' field
            elasticsearchStage.SendQueueSize = 50000;                                      // maximum number of messages the stage buffers before discarding messages

            // Create splitter pipeline stage to unconditionally feed log messages into all pipelines stages
            var splitterStage = new SplitterPipelineStage("Splitter");
            splitterStage.AddNextStage(consoleStage);
            splitterStage.AddNextStage(fileStage);
            splitterStage.AddNextStage(elasticsearchStage);

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

```

### Working with Log Messages

*Griffin+ Logging* not only allows to write log messages, but also to work with these log messages. The `LogMessage` class represents a log message with all necessary properties. All properties have getters and setters, so log messages can be build up as desired. Furthermore these messages can be protected to make them read-only and assure immutability. The `LogMessage` class also implements the `INotifyPropertyChanged` interface and features calling event handlers in the thread that registered the `PropertyChanged` event. This requires the thread to have a synchronization context that marshals invocations into the registering thread. Log messages can be initialized asynchronously, if necessary. All this allows to use the `LogMessage` class as a view model in UI applications without putting a burden on the UI thread when it comes to fetching log message data from an external source.

Log messages can be stored in a collection to handle message sets. *Griffin+ Logging* comes with a couple of classes to support collections of log messages and provide a filtered view on the message set. Collection specific types are in the `GriffinPlus.Lib.Logging.Collections` namespace. Basic types can be found in the `GriffinPlus.Lib.Logging.Collections` *NuGet* package.

All collections implement the `ILogMessageCollection<TMessage>` interface which extends the following interfaces:
- `IList`
- `IList<TMessage>`
- `IReadOnlyList<TMessage>`
- `INotifyCollectionChanged`
- `INotifyPropertyChanged`
- `IDisposable`

As the interfaces indicate, log message collections are usable as regular .NET collections - with minor restrictions: Messages can be added to the end of a collection using `Add()` and `AddRange()` only. Inserting and removing single messages is not supported, only clearing the entire collection. If log message collections get to large, the `Prune()` method allows to discard old messages, either by the maximum number of messages or by age. The collections are data-bindable and can be directly used in conjunction with UIs that support it, e.g. WPF. These collections store an unfiltered message set. A *filtering accessor* provides an efficient way to a filtered message set by peeking into a set of unfiltered log messages and searching for the previous/next log messages matching the filter criteria. This technique allows to navigate through large amounts of log messages without the need to filter all messages in advance. Log message collections are available in two flavors: a purely in-memory collection and a collection that is backed by a *SQLite* database.

The in-memory implementation of the collection is available via the `LogMessageCollection` class which is also part of the `GriffinPlus.Lib.Logging.Collections` *NuGet* package. The collection can be accessed as any other .NET collection with the restrictions mentioned above. The collection always contains the unfiltered message set. If a filtered set is desired, the `LogMessageCollection` class provides two ways to accomplish that. The obvious approach is to work with another collection that provides access to the filtered message set. This collection can be created using the collection's `GetFilteredCollection()` method. This method expects the filter to apply and generates a `FilteredLogMessageCollection` that only contains messages matching the filter. The filtered collection is read-only, but changes in the unfiltered collection are also reflected in the filtered collection. Filters implement the `ILogMessageCollectionFilter` interface and are always collection specific, i. e. these filters can only be used in conjunction with the `LogMessageCollection` class. Although the `FilteredLogMessageCollection` is easy to handle, it should be used for rather small message sets only, because the collection interface requires regenerating the filtered message set as soon as the filter changes. This can consume a noticeable amount of time. A more performant way to a filtered set is using a *filtering accessor* declared by the `ILogMessageCollectionFilteringAccessor` interface. An accessor can be created using the collection's `GetFilteringAccessor()` method.

The file-backed implementation of the collection is available via the `FileBackedLogMessageCollection` class in the `GriffinPlus.Lib.Logging.LogFile` *NuGet* package. In contrast to the in-memory collection the file-backed collection stores log messages persistently using the `LogFile` class under the hood. Requested messages are loaded on demand, but a cache keeps a working set in memory to avoid performance bottlenecks when navigating through the collection. The collection can be accessed as any other .NET collection with the restrictions mentioned above. The collection always contains the unfiltered message set. If a filtered set is desired, the `FileBackedLogMessageCollection` class allows to create a *filtering accessor* using the `GetFilteringAccessor()` method.

*Griffin+ Logging* comes with a filter that should suffice most needs, the *Selectable Log Message Filter*. It allows to filter timestamps by a time interval and provides lists of selectable process ids, process names, application names, log level names and log writer names that should pass the filter. The lists of selectable items are synchronized with the log message collection the filter is attached to. A full-text search is also supported. The filters are data-bindable, so they can be directly used in an UI just as the collections.

The following implementations are available:
- `SelectableLogMessageFilter` for the `LogMessageCollection` class
- `SelectableFileBackedLogMessageFilter` for the `FileBackedLogMessageCollection` class.

### Integration of External Processes

*Griffin+ Logging* provides the `ProcessIntegration` class that assists with integrating an external process into the logging subsystem of the current process. This class configures a given `System.Diagnostics.Process` to redirect the standard output/error streams before the process is started. The redirected streams are read and logged (if desired). Furthermore the `ProcessIntegration` class provides events that are raised when the process writes to its streams. The events `OutputStreamReceivedText` and `ErrorStreamReceivedText` are raised line by line, while the events `OutputStreamReceivedMessage` and `ErrorStreamReceivedMessage` are raised as soon as a JSON formatted log message is recognized in the streams. The latter comes in handy, if the started process uses *Griffin+ Logging* as well and emits log messages using the `JsonMessageFormatter` class plugged into the `ConsoleWriterPipelineStage` class. The events are marshalled into the context of the thread registering the event, if the thread's synchronization context is initialized. Handler code can directly access elements that have thread affinity, e.g. UI elements.

The following lines are sufficient to start a process and retrieve its output:

```csharp
// set up the process to run
ProcessStartInfo startInfo = new ProcessStartInfo("my-app.exe", "<args>");
Process process = new Process { StartInfo = startInfo };

// integrate the process into the logging subsystem
ProcessIntegration integration = ProcessIntegration.IntegrateIntoLogging(process);
integration.OutputStreamReceivedText += (sender, args) => { /* your handler code */ };
integration.OutputStreamReceivedMessage += (sender, args) => { /* your handler code */ };
integration.ErrorStreamReceivedText += (sender, args) => { /* your handler code */ };
integration.ErrorStreamReceivedMessage += (sender, args) => { /* your handler code */ };

// start the process and wait for it to exit
integration.StartProcess();
process.WaitForExit();
```

By default, recognized log messages written by the started process are logged as well. This behavior can be disabled by setting the `IsLoggingMessagesEnabled` property to `false`.

## Contributors ✨

Many thanks to the following people for their contribution to this project ([emoji keys](https://allcontributors.org/docs/en/emoji-key)):

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
  <tr>
    <td align="center"><a href="https://github.com/ravenpride"><img src="https://avatars0.githubusercontent.com/u/3209384?v=4" width="100px;" alt=""/><br /><sub><b>Sascha Falk</b></sub></a><br /><a href="https://github.com/GriffinPlus/dotnet-libs-logging/commits?author=ravenpride" title="Code">💻</a> <a href="https://github.com/GriffinPlus/dotnet-libs-logging/commits?author=ravenpride" title="Documentation">📖</a> <a href="#ideas-ravenpride" title="Ideas, Planning, & Feedback">🤔</a> <a href="https://github.com/GriffinPlus/dotnet-libs-logging/commits?author=ravenpride" title="Tests">⚠️</a> </td>
    <td align="center"><a href="https://github.com/sepiel"><img src="https://avatars2.githubusercontent.com/u/42858881?v=4" width="100px;" alt=""/><br /><sub><b>Sebastian Piel</b></sub></a><br /><a href="https://github.com/GriffinPlus/dotnet-libs-logging/commits?author=sepiel" title="Code">💻</a></td>
  </tr>
</table>

<!-- markdownlint-enable -->
<!-- prettier-ignore-end -->
<!-- ALL-CONTRIBUTORS-LIST:END -->
