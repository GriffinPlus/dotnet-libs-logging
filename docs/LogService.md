# The Griffin+ Log Service

## Overview

The *Griffin+ Log Service* is a *Windows Service* or *Linux Daemon* that can be installed on systems that run processes using *Griffin+ Logging*. These processes can add the `LogServicePipelineStage` to their log message processing pipeline in order to push log messages to the log service. The log service collects log messages from multiple processes on the system, orders them and provides a view on the merged log. This usually eases working with logs as you don't need to keep track of multiple different log files. The log service is designed to be a lightweight alternative to typical logging solutions for the cloud that usually come with high resource consumption.

The `LogServicePipelineStage` communicates with the log service via a proprietary protocol over TCP that is used to attach logging processes to the log service. Processes can use the TCP connection to directly write log messages or establish a message stream via a queue in shared memory instead. Of course, the shared memory queue works only for processes on the same system as the log service. Streaming messages via the shared memory queue provides the benefit that the thread writing a log message can directly push the message into the shared memory region. This is much more efficient than using a TCP stream and guarantees that all written messages reach the log service - even in case that the process crashes directly after writing the message. The log service is able to read all messages up to a process's death which can provide valuable information about the reason of the crash.

The log service can be configured to listen at a specific IP address and port. By default it listens to `localhost:6500`.

## The Protocol

This section describes the protocol between the `LogServicePipelineStage` and the log service. A connection starts with establishing a TCP connection from a process that wants to log to the log service.

### Greeting

As soon as the connection is established the client sends a *greeting* to the server. The *greeting* consists of a `HELLO` line to indicate to whom the server is talking. An `INFO` line with the version of the used log service library follows (informational version of the `GriffinPlus.Lib.Logging.LogService` assembly).
 
Just as the client, the log service also sends a *greeting* to indicate to whom the connecting client is talking. The *greeting* always starts with a `HELLO` line with a freely configurable name. If configured the service then sends `INFO` lines with the version of the server (file version of the entry assembly of the application) and the version of the used log service library (informational version of the `GriffinPlus.Lib.Logging.LogService` assembly).

```
Client:  HELLO Griffin+ .NET Log Service Client
         INFO Log Service Library Version: <version>
Service: HELLO Griffin+ Log Service
         INFO Server Version: <version>
         INFO Log Service Library Version: <version>
```

### Commands

The client can now send commands to communicate with the server. The server will always respond with `OK` in case of success or `NOK (<code> <message>)` in case of an error. A command id (alphanumeric string) is prepended to commands and responses. It allows the client to associate commands and responses which is important when it comes to pipelining commands (see below). The server recognizes every line starting with something looking like a command id as a command and will respond with `OK`or `NOK`.

```
Client:  [<command-id>] <COMMAND>
Service: [<command-id>] OK / [<command-id>] NOK (<code> <message>)
```

If the command id is missing or malformed, so the server does not recognize the line as a command, it sends an `ERROR` response containing some information about what went wrong. To assist with debugging this issue the server sends the line back to the client.

```
Client:  <COMMAND>
Service: ERROR Missing command id (<COMMAND>)
```

```
Client:  [<malformed-command-id>] <COMMAND>
Service: ERROR Malformed command id ([<malformed-command-id>] <COMMAND>)
```

### Client Configuration

Before starting to write messages the client can set some information about itself, namely the name and the id of its process and the name of the application, if it differs from the name of the process.

```
Client:  [<command-id>] SET PROCESS_NAME <process-name>
Service: [<command-id>] OK / [<command-id>] NOK (<code> <message>)

Client:  [<command-id>] SET PROCESS_ID <process-id>
Service: [<command-id>] OK / [<command-id>] NOK (<code> <message>)

Client:  [<command-id>] SET APPLICATION_NAME <application-name>
Service: [<command-id>] OK / [<command-id>] NOK (<code> <message>)
```

### Writing Messages

The client can write log messages using the `WRITE` command. The `text` field must always be the last field as it closes the command.

For short messages that do not contain line breaks.

```
Client:  [<command-id>] WRITE
         timestamp: <timestamp>
         ticks: <count>
         lost: <count>
         writer: <name>
         level: <name>
         tag: <name>
         text: <message>
Service: [<command-id>] OK / [<command-id>] NOK (<code> <message>)
```

For messages that contain line breaks:

```
Client:  [<command-id>] WRITE
         timestamp: <timestamp>
         ticks: <count>
         lost: <count>
         writer: <name>
         level: <name>
         tag: <name>
         text:
         <message-line-1>
         <message-line-2>
         <message-line-3>
         .
Service: [<command-id>] OK / [<command-id>] NOK (<code> <message>)
```

The following data is associated with a log message:

| Field        | Description
| :----------- | :--------------------------------------------------------------------- |
| `timestamp`  | Timestamp with timezone offset (ISO 8601)
| `ticks`      | High-precision timestamp for relative time measurements (in ns)
| `lost`       | Number of lost messages since the last successfully written message
| `writer`     | Name of the log writer
| `level`      | Name of the log level
| `tag`        | Tag associated with the message
| `text`       | The message text

All fields except the `text` field are optional, but some are strongly recommended. The fields `timestamp` and `ticks` are optional, but should be specified when writing a message. If these fields are missing, the log service will populate the fields which is usually less accurate due to the latency induced by the logging subsystem itself. The `lost` field is optional and indicates how many messages were lost since the last successfully written message. The fields `writer` and `level` are also optional and represent the name of the log writer and the log level. The `writer` field defaults to `Default` and the `level` field defaults to `Note`. The `tag` field can be specified multiple times (once for every attached log writer tag) or not at all.

The `text` field specifies the text of the message. Short messages that do not contain line breaks can just follow the `text` header. The line break at the end of the line terminates the message text. Messages spanning multiple lines have to be treated differently. The message text must start at the line following the `text` header. A line just consisting of a period (`.`) character terminates the `text` field. If the text itself contains a line that starts with a period, the period is doubled to avoid terminating the `text` field unintentionally. When writing the message text be aware that lines must not be longer than 32768 characters. In the case that long messages exceed this limit, these lines can be split by simply inserting a `\n\\\n` before the length limit is reached. This inserts a backslash on a single line that serves as a splitting indicator. The service recognizes this sequence and concatenates adjacent lines.

### Pipelining

The protocol outlined above assumes a dialog mode: the client sends a command and the server sends back a response. Then the client sends another command and so forth. This behavior is ok for low log traffic, but it is not suitable for high load scenarios as the roundtrip time strongly influences the overall speed. Therefore the log service supports writing messages in a pipelined fashion. The client can issue commands without waiting for the server to respond. The server responds as soon as it has processed a log message. This way multiple messages can be on the line at the same time.
