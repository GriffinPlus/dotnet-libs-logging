# Changelog

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
