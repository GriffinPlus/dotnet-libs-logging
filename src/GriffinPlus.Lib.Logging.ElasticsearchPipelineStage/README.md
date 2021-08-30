# Elasticsearch Pipeline Stage for Griffin+ Logging

[![NuGet Version](https://img.shields.io/nuget/v/GriffinPlus.Lib.Logging.ElasticsearchPipelineStage.svg?label=Version)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging.ElasticsearchPipelineStage) [![NuGet Downloads](https://img.shields.io/nuget/dt/GriffinPlus.Lib.Logging.ElasticsearchPipelineStage.svg?label=Downloads)](https://www.nuget.org/packages/GriffinPlus.Lib.Logging.ElasticsearchPipelineStage)

This project is part of the [Griffin+ Logging Suite](../../README.md).

## Overview

The *Elasticsearch Pipeline Stage* is a pipeline stage that can be plugged into *Griffin+ Logging* to forward messages to an *Elasticsearch* cluster. *Elasticsearch* is the de-facto standard logging system when it comes to logging in cloud applications, but there is also an increasing number of applications that use *Elasticsearch* locally to get rid of textual log files that are hard to evaluate.

The *Elasticsearch Pipeline Stage* sends messages to *Elasticsearch* complying with the [Elasticsearch Common Schema (ECS) version 1.10](https://www.elastic.co/guide/en/ecs/1.10/index.html). This ensures that the log output integrates seamlessly with logs from other applications complying with the schema as well. To integrate even better *Griffin+ Logging* has been redesigned to align with *syslog* severity levels and log level names.

## Using

### Step 1: Installation

Add NuGet package [`GriffinPlus.Lib.Logging.ElasticsearchPipelineStage`](https://www.nuget.org/packages/GriffinPlus.Lib.Logging.ElasticsearchPipelineStage) to your project.

### Step 2: Integration

Add a `using` directive to include the namespace.

```csharp
using GriffinPlus.Lib.Logging.Elasticsearch;
```

Create an instance of the pipeline stage, configure it to your needs and let the logging subsystem use it. The example below shows the defaults. You can skip setting the properties, if the defaults satisfy your needs. The default settings are suitable to access a locally installed *Elasticsearch* server which is:

- listening to the default port (9200)
- using no authentication or NTLM, Kerberos or Negotiate with login user credentials

```csharp
// initialize the pipeline stage (with default values, for illustration purposes only)
var stage = new ElasticsearchPipelineStage("Elasticsearch");
stage.ApiBaseUrls = new[] { new Uri("http://127.0.0.1:9200/") };  // use local elasticsearch server
stage.AuthenticationSchemes = AuthenticationScheme.PasswordBased; // support all password based authentication schemes
stage.Username = "";                                              // username to use when authenticating (empty to use login user)
stage.Password = "";                                              // password to use when authenticating (empty to use login user)
stage.Domain = "";                                                // domain to use when authenticating (for schemes 'Digest', 'NTLM', 'Kerberos' and 'Negotiate')
stage.BulkRequestMaxConcurrencyLevel = 5;                         // maximum number of requests on the line
stage.BulkRequestMaxSize = 5 * 1024 * 1024;                       // maximum size of a bulk request
stage.BulkRequestMaxMessageCount = 0;                             // maximum number of messages in a bulk request (0 = unlimited)
stage.IndexName = "logs";                                         // elasticsearch index to write log messages into
stage.OrganizationId = "";                                        // value of the 'organization.id' field
stage.OrganizationName = "";                                      // value of the 'organization.name' field
stage.SendQueueSize = 50000;                                      // maximum number of messages the stage buffers before discarding messages

// let the logging subsystem use the pipeline stage
Log.ProcessingPipeline = stage;
```

The pipeline stage allows to configure multiple *Elasticsearch* endpoints for redundancy. The stage will try the first configured endpoint and use it until it fails. Then it tries to use the next endpoint in the list until it fails and so on. An endpoint that failed is not used for 30 seconds.

When it comes to authenticating the pipeline stage supports the authentication schemes `Basic`, `Digest`, `Ntlm`, `Kerberos` and `Negotiate`. The required schemes can be selected by or'ing the schemes, because `AuthenticationScheme` is a flag enumeration. The value `PasswordBased` is predefined to sum up all these authentication schemes in a single value. To use custom credentials you need to set the `Username` and the `Password` property. If any of both is empty the stage will use the credentials of the login user. Furthermore for the authentication schemes `Digest`, `Ntlm`, `Kerberos` and `Negotiate` you need to initialize the `Domain` property appropriately. Due to security reasons using login credentials is only supported for the authentication schemes `Ntlm`, `Kerberos` and `Negotiate`.

By default the stage limits the size of a bulk request to 5 Mebibytes. This can be overridden using the `BulkRequestMaxSize` property. The request size must be in the range between 10 KiB and 100 MiB. Invalid sizes are adjusted to the nearest valid size to avoid malfunctions. There is also an option to limit the number of messages in a request using the `BulkRequestMaxMessageCount` property. The default is `0` which means that the number of messages in a request is not limited. As long as there are messages available there is at least one request on the line. Under high-load the stage sends additional requests up the maximum number defined by the `BulkRequestMaxConcurrencyLevel` property. By default the stage allows to send up to 5 bulk requests in parallel. These requests must be filled entirely to be sent. If there are less messages to send the stage waits for the last request to complete, then sends the next one. This behavior spares bulk request slots on the *Elasticsearch* server.

Via the `IndexName` property the pipeline stage allows to choose the name of the *Elasticsearch* index messages should be sent to. By default the name of the index is `logs`.

When sending a message to *Elasticsearch*, the stage allows to set the ECS fields `organization.id` and `organization.name` in the document to help to distinguish messages written by different organizations. By default these properties are empty strings, i.e. the fields are not sent along with the message.

At last the `SendQueueSize` property allows to adjust the number of messages the stage can buffer, before it discards messages. If an application is expected to generate bursts of messages or in case of varying network throughput, it can be a good idea to enlarge the send queue. The default queue size is 50000 messages.

It is possible to configure these settings without recompiling the application. The pipeline stage utilizes the *Log Configuration System* for this. *Griffin+ Logging* ships with the `FileBackedLogConfiguration` class that allows to store settings in an *ini-like* configuration file. For more information about log configurations please see the top-level [documentation](../../README.md) of *Griffin+ Logging*. The following section can be put into this log configuration file to configure the settings of the stage.

```ini
; ------------------------------------------------------------------------------
; Processing Pipeline Stage Settings
; ------------------------------------------------------------------------------

[ProcessingPipelineStage:Elasticsearch]
Server.ApiBaseUrls = http://127.0.0.1:9200/
Server.Authentication.Schemes = PasswordBased
Server.Authentication.Username = 
Server.Authentication.Password = 
Server.Authentication.Domain = 
Server.BulkRequest.MaxConcurrencyLevel = 5
Server.BulkRequest.MaxSize = 5242880
Server.BulkRequest.MaxMessageCount = 1000
Server.IndexName = logs
Data.Organization.Id = 
Data.Organization.Name = 
Stage.SendQueueSize = 50000
```

The [demo project](../GriffinPlus.Lib.Logging.Demo) shows how to set up and integrate the pipeline stage. If the configuration file does not exist, the demo project saves a new one with the settings that have been configured programmatically. This way you can set your own defaults and write a configuration file using these settings. An existing log configuration file is loaded, but not modified. This enables an application to generate a missing log configuration file with sensible defaults on the first run. Afterwards the user can modify the configuration file as needed.

## Message Fields

Log messages are written to *Elasticsearch* using a JSON document with the following mapping:

| JSON Field          | Description
|:--------------------|:-------------------------------------------------------------------------------------------------------------------------------------------------------
| `@timestamp`        | Date/time (UTC) when the event originated ([ECS field](https://www.elastic.co/guide/en/ecs/1.10/ecs-base.html#field-timestamp))
| `tags`              | Tags associated with the log message ([ECS field](https://www.elastic.co/guide/en/ecs/1.10/ecs-base.html#field-tags))
| `message`           | Text of the log message ([ECS field](https://www.elastic.co/guide/en/ecs/1.10/ecs-base.html#field-message))
| `event.timezone`    | Timezone offset ([ECS field](https://www.elastic.co/guide/en/ecs/1.10/ecs-event.html#field-event-timezone), format: `-hh:mm` or `+hh:mm`)
| `event.severity`    | Numeric severity of the event ([ECS field](https://www.elastic.co/guide/en/ecs/1.10/ecs-event.html#field-event-severity), see below)
| `host.hostname`     | Name of the host ([ECS field](https://www.elastic.co/guide/en/ecs/1.10/ecs-host.html#field-host-hostname))
| `host.TicksNs`      | Host-specific tick counter used to calculate time differences (in ns, custom field)
| `log.level`         | Log level associated with the event ([ECS field](https://www.elastic.co/guide/en/ecs/1.10/ecs-log.html#field-log-level), see below)
| `log.logger`        | Name of the log writer ([ECS field](https://www.elastic.co/guide/en/ecs/1.10/ecs-log.html#field-log-logger))
| `organization.id`   | Unique id of the organization ([ECS field](https://www.elastic.co/guide/en/ecs/1.10/ecs-organization.html#field-organization-id))
| `organization.name` | Name of the organization ([ECS field](https://www.elastic.co/guide/en/ecs/1.10/ecs-organization.html#field-organization-name))
| `process.name`      | Name of the process ([ECS field](https://www.elastic.co/guide/en/ecs/1.10/ecs-process.html#field-process-name))
| `process.pid`       | Id of the process ([ECS field](https://www.elastic.co/guide/en/ecs/1.10/ecs-process.html#field-process-pid))
| `process.title`     | Name of the application ([ECS field](https://www.elastic.co/guide/en/ecs/1.10/ecs-process.html#field-process-title))

The following table shows how *Griffin+ Log Levels* are mapped to the `event.severity` field and the `log.level` field. The value of `event.severity` aligns with *syslog* severity codes to ease writing queries over accumulated logs.

| Griffin+ Log Level  | `event.severity` | `log.level`     | Description
|:--------------------|:----------------:|:----------------|:--------------------------------------------------------------------------------
| `Emergency`         |        0         | `emerg`         | Absolute "panic" condition: the system is unusable
| `Alert`             |        1         | `alert`         | Something bad happened: immediate attention is required
| `Critical`          |        2         | `crit`          | Something bad is about to happen: immediate attention is required
| `Error`             |        3         | `error`         | Non-urgent failure in the system that needs attention
| `Warning`           |        4         | `warn`          | Something will happen if it is not dealt within a timeframe
| `Notice`            |        5         | `notice`        | Normal but significant condition that might need special handling
| `Informational`     |        6         | `info`          | Informative but not important
| `Debug`             |        7         | `debug`         | Only relevant for developers
| `Trace`             |        8         | `trace`         | Only relevant for implementers
| Aspect Levels       |        9         | `<aspect name>` | All aspect log levels

## Error Reporting

If errors occur setting up the pipeline stage, the system logger (`Log.SystemLogger`) is used to communicate these errors. On *Windows* systems these messages are logged to the *Windows Event Log*. On *Linux* the system's *syslog* daemon is used. So keep an eye on these logs when setting up the pipeline stage.
