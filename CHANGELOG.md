# Changelog
---

## Release v5.1.1

### Bugfixes

#### Fix sending messages to Elasticsearch data stream

The `ElasticsearchPipelineStage` used operation action `index` when sending messages to Elasticsearch. This works for regular Elasticsearch indices, but fails for data streams that need operation action `create`.

---

## Release v5.1.0

### New Features

#### Logging Interface

The logging interface (mainly the `LogWriter` class and `LogLevel` class) have been pulled out into a separate project available via NuGet package `GriffinPlus.Lib.Logging.Interface`. The interface is very stable. This allows libraries to use the interface package instead of the full-featured `GriffinPlus.Lib.Logging` package to write to the log without the need to update library packages with every release of the full-featured `GriffinPlus.Lib.Logging` package. The interface should stay stable even in case of major version releases of the logging package. Some functionality has moved, but for compatibility reasons forwards have been established. These forwards are marked as obsolete, so users can easily migrate their code to the new version without breaking anything. These forwards will be removed with the next major release.

---

## Release v5.0.1

### Bugfixes

#### Fix determining the application name

The application name was set to the name of the current application domain. It should be the name of the process by default.

---

## Release v5.0.0

**This release contains breaking changes!**

### New Features

### Bugfixes

#### Fix broken link between log configurations and log writers and pipeline stages

All configurations now provide a `Changed` event that is raised when something in the configuration changes. Event handlers are fired using the synchronization context of the thread registering the event, so it is suitable for use in conjunction with GUIs. Raising the `Changed` event can be suspended temporarily.

The `FileBackedLogConfiguration` effectively supports reloading its *.gplogconf file on changes now.

#### Fix synchronization issue when shutting down pipeline stages deriving from the `AsyncProcessingPipelineStage` class

The boolean member variable indicating that the stage is shutting down was not volatile, so it was not evaluated properly in the processing loop of the stage. This behavior deferred the shutdown of the stage.

#### Fix generating log writer name from type

The name generation now supports generic types and generic type definitions. Information about the assembly (name, version, hash) containing the type is properly pruned to create a clean name.

#### Fix `ProcessIntegration` creating zombie processes

The `ProcessIntegration` class implements `IDisposable` now. Disposing a `ProcessIntegration` object waits for the process to exit and cleans up the associated `Process` object. This should avoid generating zombie processes on Linux.

#### Fix retrying to send messages via the `ElasticsearchPipelineStage` after connection issues

#### Fix consolidating cache after pruning a `FileBackedLogMessageCollection`

#### Fix cache alignment issue after pruning in `FileBackedLogMessageCollection`

#### Fix raising overview collection changed events in `FileBackedLogMessageCollection`

#### Fix search text handling in `SelectableFileBackedLogMessageFilter` (SQL injection issue)

#### Fix 'append' mode in `FileWriterPipelineStage`

The file position was not moved to end of the file when opening in 'append' mode, so new messages have overwritten messages in an existing file.

#### Fix loosing messages in `FileWriterPipelineStage`

The stage re-opened the log file when a setting has changed. Re-opening truncated the log file, so messages were lost.

#### Fix patching assembly info

Azure pipelines did not patch the correct assembly information files, so all assemblies had version `0.0.0.0`.

### Other Changes

#### Remove support for .NET Core 2.1

Although direct support for .NET Core 2.1 has been removed, the .NET Standard 2.0 Version is still usable on .NET Core 2.1.

#### Revise base classes for pipeline stages

The `IProcessingPipelineStage` interface has been removed in favor of a common base class (`ProcessingPipelineStage`). This was necessary to better integrate pipeline stages into the logging subsystem. There is a derivation for synchronous pipeline stages (`SyncProcessingPipelineStage`) and asynchronous pipeline stages (`AsyncProcessingPipelineStage`) now.

#### Redesign initialization of logging subsystem due to configuration issues

The configuration and the processing pipeline was set up independently from each other, so pipeline stages created a temporary set of default settings to get into an operable state. As soon as pipeline stages were dropped into the logging subsystem, the configuration of the logging subsystem became active replacing the temporary stage settings. This lead to discarding settings that were changed by a stage's property.

The `Log` class now provides an `Initialize<TConfiguration>(...)` method that creates a configuration and the pipeline stages in a single step. Stages are directly bound to this configuration. Furthermore stages now need to provide a parameterless constructor to work with the logging subsystem. The name of a pipeline stage and its settings are passed to the parameterless constructor on a side channel using the `ProcessingPipelineStage.Create<TStage>()` method. A pipeline builder mechanism assists with setting up pipeline stages during initialization and hides this magic from the user.

#### Revise pruning messages in `LogFile` / `FileBackedLogMessageCollection`

When pruning old log messages the `FileBackedLogMessageCollection` class raised the `CollectionChanged` event, but notified about a collection reset instead of a limited number of items being removed from the collection. This behavior was rather unexpected and different from the behavior of the `LogMessageCollection` class working purely in memory. The `FileBackedLogMessageCollection` behaves as the LogMessageCollection now, but the change induced reading log messages from the log file before actually removing the messages. If the user of the `FileBackedLogMessageCollection` class knows that event recipients do not need the removed messages, he can set the `ReturnDummyMessagesWhenPruning` property to `true` to pass a special collection with dummy messages to event recipients. The number of dummy messages is the same as the number of messages that has actually been removed. This way reading unneeded log messages can be avoided to improve performance.

#### Make predefined log levels customizable when deriving from the `SelectableLogMessageFilter` class

Derived filter classes can now influence which log levels are considered predefined log levels. This is especially important when interoperating with logging systems that use other predefined log levels than *Griffin+ Logging*.

#### Add option to customize resetting `SelectableLogMessageFilter` class

The `DisableFilterOnReset` property determines whether the filter is disabled when it is reset (default is false). The `UnselectItemsOnReset` property determines whether filter items are unselected when the filter is reset (default is false).

#### Let `ElasticsearchPipelineStage` add field 'ecs.version' to written events

The ECS requires writing the version field to determine the ECS version the writer complies to.

#### Add overrides for handling stage setting changes collectively

The base classes for processing pipeline stages now provide the following overridable methods that are invoked when a registered pipeline stage setting changes:

- `AsyncProcessingPipelineStage.OnSettingsChangedAsync()`
- `SyncProcessingPipelineStage.OnSettingsChanged()`

---

## Release v4.0.5

### Bugfixes

#### Fix shutting down elasticsearch pipeline stage when endpoint is not available

If no elasticsearch endpoint was available the processing thread went to sleep before trying again. In this case the pipeline stage did not shut down until the shutdown timeout elapsed causing an unnecessary delay of 30 seconds.

---

## Release v4.0.4

### Other Changes

#### Logging unhandled exceptions using the system logger

An application usually terminates when unhandled exceptions occur. Log messages buffered in stages might get lost in this case. These exceptions are now logged using the system logger and the configured pipeline stages to allow further investigation. After logging the incident the logging subsystem is shut down gracefully and the process is terminated. Terminating the process can be disabled by setting the `TerminateProcessOnUnhandledException` property of the `Log` class to `false`.

#### Report not shutting down gracefully to system log

Some pipeline stages need some time to process buffered messages, so exiting the process without shutting down gracefully can result in message loss.

#### `ElasticsearchPipelineStage` does not block process exit, if not shut down gracefully

Formerly a foreground thread was used for processing. The foreground thread could keep the process from exiting, if the pipeline stage was not shut down gracefully at the end.

---

## Release v4.0.3

### Bugfixes

#### Fix issue with cancellation on shutdown in `ElasticsearchPipelineStage`

If the `ElasticsearchPipelineStage` does not complete shutting down within 30 seconds, a cancellation token is signaled to cancel pending send operations. This could lead to an `OperationAbortedException` to be thrown before the send tasks have actually been set to completed. Disposing these incomplete tasks as part of the cleanup procedure could throw an exception as well.

#### Fix generation of registry snippet in `WindowsSystemLogger` class

The system logger now writes the correct registry snippet to register the appropriate log source to the Windows event log.

#### Fix using dedicated thread in `AsyncProcessingPipelineStage` class

The pipeline stage created a dedicated thread at startup, but lost it after awaiting the first task. The thread always continued on a worker thread. The pipeline stage now creates a dedicated thread with its own synchronization context, so execution can continue on that thread - provided that `ConfigureAwait(false)` *is not* used which allows the continuation to run on a worker thread.

### Other Changes

#### Optimization of memory usage of `ElasticsearchPipelineStage`

Replaced `MemoryStream` with `MemoryBlockStream` in `ElasticsearchPipelineStage`. The `MemoryBlockStream` rents buffers from the application's array pool. When there is nothing to do, all buffers are returned to the pool. This reduces the memory consumption in times with less log traffic. Buffers are 80 KiB in size, so they are allocated on the regular heap, not on the large object heap. Not using the large object heap is always a good idea, because objects on this heap are collected rarely and the heap is not compacted which can cause heap fragmentation issues.

#### Using dedicated processing thread in `ElasticsearchPipelineStage`

The pipeline stage used thread pool threads to do any processing. This is usually the way with the best throughput as the thread pool tries to limit the number of threads to the number of cores to minimize context switches. Queuing work for a thread pool thread is rather cheap, but the overhead to handle this was not. As a mitigation the processing thread was kept for some time to process additional work. Putting a thread pool thread asleep lets the cpu core associated with it sleep as well. This could lead to significant performance loss. Using a dedicated thread introduces additional context switches, but this seems to have less impact than putting a CPU core asleep.

---

## Release v4.0.2

### Bugfixes

- Fix unexpected disposal of content stream along with the send task of the `ElasticsearchPipelineStage`

---

## Release v4.0.1

### Bugfixes

- Remove explicit reference to System.Net.Http in `ElasticsearchPipelineStage` project
- Fix `ElasticsearchPipelineStage` shutting down too early

---

## Release v4.0.0

**This release contains some breaking changes!**

### New Features

#### Pipeline Stage for Elasticsearch

*Griffin+ Logging* ships with a new pipeline stage that forwards log messages to an *Elasticsearch* cluster. For more information, please see the [project page](src/GriffinPlus.Lib.Logging.ElasticsearchPipelineStage/README.md).

#### System Loggers

The `Log` class now provides an operating system dependent logger that directly writes into the windows event log (on windows) or to syslog (on linux). The system logger allows the logging subsystem to communicate issues that occur within the logging subsystem itself. The `ProcessingPipelineBaseStage` class supports writing pipeline-specific informational messages, warnings and errors using the system logger now. When writing errors an exception object can be passed. The exception is unwrapped, formatted and logged as well.

### Bugfixes

- Fix synchronization issue in `VolatileProcessingPipelineStageSetting` class
- Fix deadlock caused by default configuration using the pipeline stage lock

### Breaking Changes

#### Access To Pipeline Stage Settings

Pipeline stages used the `GetSetting()` method of the `ProcessingPipelineStageConfigurationBase` class to obtain a setting object for named settings. This method had undesirable side effects as it actually registered a setting to allow pipeline stages to use it. The registration added a new setting with a default value. The registration failed, if `GetSetting()` was called with different default setting values. This was intentional behavior, but confused some people, so we decided to rename `GetSetting()` to `RegisterSetting()` as it expresses what it actually does. At the same time we added methods to access settings without registering one with a default value. Now stages can use the following methods to access their settings:

- `RegisterSetting()` registers a setting with a specific name and creates a new setting with a default value, if the setting does not exist, yet. The default value does not change an existing setting value.
- `GetSetting()` gets a setting with a specific name and returns *null*, if it does not exist, yet.
- `SetSetting()` sets a setting with a specific name and creates a new setting with the specified value (but without setting a default value to circumvent clashes with `RegisterSettings()`).

#### Syslog Compliant Log Levels

*Griffin+* log levels are now compliant to *syslog* log levels and log level ids correspond to *syslog* severity codes. The former trace levels (`Trace0` to `Trace19`) have been eliminated as tags are now available to differentiate tracing, if necessary.

Old log levels:
- `Failure` (0)
- `Error` (1)
- `Warning` (2)
- `Note` (3)
- `Developer` (4)
- `Trace0` (5)
- ...
- `Trace19` (24)
- *Aspects* (25+)

New log levels:
- `Emergency` (0)
- `Alert` (1)
- `Critical` (2)
- `Error` (3)
- `Warning` (4)
- `Notice` (5)
- `Informational` (6)
- `Debug` (7)
- `Trace` (8, no syslog equivalent)
- *Aspects* (9+, no syslog equivalent)

The `LocalLogServicePipelineStage` transforms the new log levels to their old pendent to avoid breaking old and new components log to the service. The log levels are mapped as follows:

- `Emergency` => `Failure`
- `Alert` => `Failure`
- `Critical` => `Failure`
- `Error` => `Error`
- `Warning` => `Warning`
- `Notice` => `Note`
- `Informational` => `Note`
- `Debug` => `Developer`
- `Trace` => `Trace0`
- Aspects are kept as they are

#### Pipeline Stage Settings

Introduced a *setting proxy* that forwards all setting accesses to the currently bound pipeline stage configuration. If the configuration is exchanged, the proxy rebinds to the new configuration automatically. This avoids breaking the link between a pipeline stage and its configuration. The solution up to now was to invoke the virtual `BindSettings()` method of the `ProcessingPipelineBaseStage` class just after the configuration has been exchanged. The first call to this method was done in the constructor of the `ProcessingPipelineBaseStage` class. This could cause severe issues as the constructor of a derived class has not run at the point the virtual method was called. The override of `BindSettings()` was working on an incompletely initialized object in this case.

Pipeline stages deriving from the `ProcessingPipelineBaseStage` class can now call `RegisterSetting()` to get a setting proxy. `BindSettings()` has been removed.

#### Custom Serialization for Pipeline Stage Settings

Pipeline stage settings supported only primitive types, enums and strings. Pipeline stage settings can now be registered with custom converters that handle the conversion from the setting's value to string and vice versa. This way even complex types can be used in pipeline settings. These types are always stored as strings in configurations.

#### All Log Configurations Implement `IDisposable`

Under Linux unit tests targeting the `FileBackedLogConfiguration` class failed sporadicly, because the user was running out of *inotify* instances needed by the `FileSystemWatcher` class. Disposing the watchers solved the issue. In real-world applications there usually is only one configuration with a single watcher, so not disposing the configuration should not be an issue.

### Other Changes

#### *Stage Initializing* State

The `ProcessingPipelineBaseStage` now provides the `IsInitializing` property derived classes can use to determine whether the stage is being initialized, but has not completed, yet. The `EnsureAttachedToLoggingSubsystem()` method ensures that the stage is already initialized or still initializing, otherwise throws an exception.

---

## Release v3.1.7

### Bugfixes

- Make exceptions thrown by SelectableFileBackedLogMessageFilter class more elaborate.

---

## Release v3.1.5

### Bugfixes

- Fix ArgumentOutOfRangeException in FileBackedLogMessageCollectionFilteringAccessor class

---

## Release v3.1.4

### Bugfixes

- Forbid using line break characters in log writer/level names (these characters break the configuration)
- Fix issue with read-only databases in `SelectableFileBackedLogMessageFilter` class
- Fix updating overview collections when creating `LogMessageCollection` with an initial message set
- Fix effect of global filter switch in `SelectableFileBackedLogMessageFilter` class
- Make Reset() method of `SelectableLogMessageFilterBase` class protected (can break consistency when not used properly in user-code)
- Fix default timestamp of SelectableLogMessageFilter class (defaults to `01-01-0001T00:00:00` now)
- Fix issue with sorting of filter items in `SelectableLogMessageFilterBase` class

---

## Release v3.1.3

### Bugfixes

- Map log level `None` and `All` to `Failure` when writing messages (these levels should be used for filtering only, not for writing!)

---

## Release v3.1.1

### Minor Changes

- Add support for underscores in log writer tags.

---

## Release v3.1.0

### New Features

- Add support for populating log files on creation to speed up creating files with an initial log message set

### Bugfixes

- Item filters of the SelectableLogMessageFilter<TMessage> implementations (in-memory, file-backed) do not remove items on Reset() any more, if AccumulateItems is set.

---

## Release v3.0.0

**This release contains some breaking changes!**

It may be necessary to adjust the setup of pipeline stages and the implementation of own pipeline stages.

These changes _do not_ effect writing log messages, so the impact should be rather low.

### New Features

- Added a log file based on an *SQLite* database ([LogFile](https://github.com/GriffinPlus/dotnet-libs-logging/blob/master/src/GriffinPlus.Lib.Logging.LogFile/LogFile/LogFile.cs) class)
  - The file can be set up for *recording* and for *analysis* to fit scenarios where write speed is more important than the ability to query data - an vice versa.
  - The file can operate in two modes to weigh robustness against speed
    - Robust Mode: The database uses a WAL (Write Ahead Log) when writing to ensure data consistency 
    - Fast Mode: The database works without journaling and does not sync to disk to speed up operation
- Added log message collections with filtering accessors and data-binding capabilities
  - [LogFileCollection](https://github.com/GriffinPlus/dotnet-libs-logging/blob/master/src/GriffinPlus.Lib.Logging.Collections/LogMessageCollection%20(In-Memory)/LogMessageCollection.cs) class (in-memory)
  - [FileBackedLogFileCollection](https://github.com/GriffinPlus/dotnet-libs-logging/blob/master/src/GriffinPlus.Lib.Logging.LogFile/FileBackedLogMessageCollection/FileBackedLogMessageCollection.cs) class (backed by the [LogFile](https://github.com/GriffinPlus/dotnet-libs-logging/blob/master/src/GriffinPlus.Lib.Logging.LogFile/LogFile/LogFile.cs) class)
- Added *Selectable Log Message Filter* with data-binding support for both collection types to filter log messages...
  - ... by time span
  - ... by selecting process ids, process names, application names, log writer names and log level names a log message must match
  - ... by full-text search in the message text
- The [LogMessage](https://github.com/GriffinPlus/dotnet-libs-logging/blob/master/src/GriffinPlus.Lib.Logging/LogMessage.cs) class now supports data-binding, asynchronous initialization and write protection

### Other Changes

- Changed the default file extension for log configuration files from `.logconf` to `.gplogconf` to circumvent a name clash with another logging subsystem

---

## Release v2.3.0

### Extensions

- The [ProcessIntegration](https://github.com/GriffinPlus/dotnet-libs-logging/blob/master/src/GriffinPlus.Lib.Logging/ProcessIntegration.cs) class now supports waiting for the process to exit (synchronously and asynchronously).

### Bugfixes

- Fixed task setup in [AsyncProcessingPipelineStage](https://github.com/GriffinPlus/dotnet-libs-logging/blob/master/src/GriffinPlus.Lib.Logging/Pipeline%20Stages/Common/AsyncProcessingPipelineStage.cs) class.

---

## Release v2.2.0

### New Features

- Log Writers can be configured to attach tags to written log messages (tags can be used when filtering log messages).
- Support for reading JSON formatted log messages (see [JsonMessageReader](https://github.com/GriffinPlus/dotnet-libs-logging/blob/master/src/GriffinPlus.Lib.Logging/Message%20Readers/JsonMessageReader/JsonMessageReader.cs) class)
- Support for integrating external processes into the logging subsystem (see [ProcessIntegration](https://github.com/GriffinPlus/dotnet-libs-logging/blob/master/src/GriffinPlus.Lib.Logging/ProcessIntegration.cs) class).

### Extensions

- The [JsonMessageFormatter](https://github.com/GriffinPlus/dotnet-libs-logging/blob/master/src/GriffinPlus.Lib.Logging/Message%20Formatters/JsonMessageFormatter/JsonMessageFormatter.cs) class supports setting the newline character sequence now.
- Option to replace used streams in the [ConsoleWriterPipelineStage](https://github.com/GriffinPlus/dotnet-libs-logging/blob/master/src/GriffinPlus.Lib.Logging/Pipeline%20Stages/ConsoleWriterPipelineStage.cs) class.

---

## Release v2.1.10

### Bugfixes

- Fixed crash in [ProcessingPipelineBaseStage.RemoveNextStage()](https://github.com/GriffinPlus/dotnet-libs-logging/blob/master/src/GriffinPlus.Lib.Logging/Pipeline%20Stages/Common/ProcessingPipelineBaseStage.cs) class (effected all derived pipeline stages).

---

## Release v2.1.9

### Bugfixes

- Let the [TextWriterPipelineStage](https://github.com/GriffinPlus/dotnet-libs-logging/blob/master/src/GriffinPlus.Lib.Logging/Pipeline%20Stages/Common/TextWriterPipelineStage.cs) emit all log messages fields by default.

---
