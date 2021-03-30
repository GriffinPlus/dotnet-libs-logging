# The Griffin+ Log Service

## Overview

The *Griffin+ Log Service* is a *Windows Service* or *Linux Daemon* that can be installed on systems that run processes using *Griffin+ Logging*. These processes can add the `LogServicePipelineStage` to their log message processing pipeline in order to push log messages to the log service. The log service collects log messages from multiple processes on the system, orders them and provides a view on the merged log. This usually eases working with logs as you don't need to keep track of multiple different log files. The log service is designed to be a lightweight alternative to typical logging solutions for the cloud that usually come with high resource consumption.

The `LogServicePipelineStage` communicates with the log service via a proprietary protocol over TCP that is used to attach logging processes to the log service. Processes can use the TCP connection to directly write log messages or establish a message stream via a queue in shared memory instead. Of course, the shared memory queue works only for processes on the same system as the log service. Streaming messages via the shared memory queue provides the benefit that the thread writing a log message can directly push the message into the shared memory region. This is much more efficient than using a TCP stream and guarantees that all written messages reach the log service - even in case that the process crashes directly after writing the message. The log service is able to read all messages up to a process's death which can provide valuable information about the reason of the crash.

The log service can be configured to listen at a specific IP address and port. By default it listens to `localhost:6500`.

## The Protocol

This section describes the protocol between the `LogServicePipelineStage` and the log service.

A connection starts with a establishing a TCP connection from a process that wants to log to the log service. As soon as the connection is established the client sends a *greeting* to the server. The *greeting* consists of a `HELLO` command to indicate to whom the server is talking. An `INFO` command with the version of the used log service library follows (informational version of the `GriffinPlus.Lib.Logging.LogService` assembly).
 
Just as the client, the log service also sends a *greeting* to indicate to whom the connecting client is talking. The *greeting* always starts with a `HELLO` command with a freely configurable name. If configured the service then sends `INFO` commands with the version of the server (file version of the entry assembly of the application) and the version of the used log service library (informational version of the `GriffinPlus.Lib.Logging.LogService` assembly).

```
Client:  HELLO Griffin+ .NET Log Service Client
         INFO Log Service Library Version: <version>
Service: HELLO Griffin+ Log Service
         INFO Server Version: <version>
         INFO Log Service Library Version: <version>
```

The client can now set some information about itself, namely the name and the id of its process and the name of the application, if it differs from the name of the process. The names must be quoted, if they contain whitespaces. The Server will always respond with `OK` in case of success or `NOK (<code> <message>)` in case of an error:

```
Client:  SETPROCESS <name> <id>
Service: OK / NOK (<code> <message>)

Client:  SETAPPLICATION <name>
Service: OK / NOK (<code> <message>)
```

The client can now write log messages. The fields `writer`, `level` and `text` are mandatory, the `tag` field is optional and can be specified multiple times.

```
Client:  WRITE
         writer: <name>
         level: <name>
         tag: <name>
         text: Lorem ipsum dolor sit amet, consetetur sadipscing elitr,
         sed diam nonumy eirmod tempor invidunt ut labore et dolore
         magna aliquyam erat, sed diam voluptua. At vero eos et
         accusam et justo duo dolores et ea rebum. Stet clita kasd
         gubergren, no sea takimata sanctus est Lorem ipsum dolor
         sit amet.
         .
Service: OK / NOK (<code> <message>)
```

The name of log writers, log levels and tags can become very elongated, so a client can choose to map these names to ids and use them instead to save traffic:

```
Client: MAPWRITER <name> <id>
Service: OK / NOK (<code> <message>)

Client: MAPLEVEL <name> <id>
Service: OK / NOK (<code> <message>)

Client: MAPTAG <name> <id>
Service: OK / NOK (<code> <message>)

Client:  WRITE
         writer-id: <id>
         level-id: <id>
         tag-id: <id>
         text: Lorem ipsum dolor sit amet, consetetur sadipscing elitr,
         sed diam nonumy eirmod tempor invidunt ut labore et dolore
         magna aliquyam erat, sed diam voluptua. At vero eos et
         accusam et justo duo dolores et ea rebum. Stet clita kasd
         gubergren, no sea takimata sanctus est Lorem ipsum dolor
         sit amet.
         .
         DONE
Service: OK / NOK (<code> <message>)
```
