# Changelog

---

## Upcoming Changes

### New Features

### Extensions

### Bugfixes

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
