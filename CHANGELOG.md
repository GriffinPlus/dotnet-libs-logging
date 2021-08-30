# Changelog
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
