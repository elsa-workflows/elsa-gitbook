---
description: >-
  Configure Elsa 3.8.0 structured log capture, Studio diagnostics, redaction,
  and durable storage.
---

# Structured Logs

Elsa's `Elsa.Diagnostics.StructuredLogs` module captures `ILogger` events from
an Elsa host and makes them available to operators through REST, SignalR, and
Elsa Studio. It is a host-diagnostics feature: it is separate from the
workflow journal, the `Elsa.Logging` workflow `Log` activity, and raw
stdout/stderr capture.

Use this module when operators need searchable application logs with workflow,
tenant, trace, and source metadata. Use [Log Persistence](../optimize/log-persistence.md)
for activity execution history, and [Distributed Tracing](distributed-tracing.md)
for spans and metrics.

## Choose the packages

The 3.8.0 release separates the server, dashboard contribution, and storage
packages:

- `Elsa.Diagnostics.StructuredLogs` captures `ILogger` events, keeps recent
  events, and exposes the REST and SignalR contracts.
- `Elsa.Diagnostics.StructuredLogs.Dashboard` contributes structured-log
  status and findings to the Elsa dashboard.
- `Elsa.Diagnostics.StructuredLogs.Persistence.Relational` contains the
  provider-neutral relational store and write buffer.
- `Elsa.Diagnostics.StructuredLogs.Persistence.Sqlite` supplies SQLite
  connection, dialect, migration, and startup-cleanup services.

The default store is in memory and is scoped to one process. Add SQLite when
recent logs must survive a restart. The release's relational package is a
provider contract; SQLite is the concrete relational provider shipped here.

## Enable server diagnostics

For a custom ASP.NET Core host, install the server package and register the
feature. `UseStructuredLogsDashboard` is optional and requires the dashboard
package.

```csharp
using Elsa.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddElsa(elsa =>
{
    elsa.UseStructuredLogs(structuredLogs =>
    {
        structuredLogs.RecentLogCapacity = 5_000;
        structuredLogs.MaxRecentLogQuerySize = 1_000;
    });

    elsa.UseStructuredLogsDashboard();
});

var app = builder.Build();

app.UseRouting();
app.UseStructuredLogs();
app.Run();
```

`UseStructuredLogs` registers the FastEndpoints assembly and structured-log
services. The application-builder call maps the SignalR hub; the REST
endpoints are discovered by the feature. Keep the call in the normal
endpoint-routing portion of the host pipeline.

The Elsa Server Web sample keeps this feature disabled by default behind a
`useStructuredLogs` switch. Enable the server feature and the corresponding
Studio module together when you want the built-in diagnostics page.

## Connect Elsa Studio

The Studio side is also modular. A custom Studio host registers the remote API
client and the dashboard widget:

```csharp
using Elsa.Studio.Diagnostics.StructuredLogs.Dashboard.Extensions;
using Elsa.Studio.Diagnostics.StructuredLogs.Extensions;

services.AddStructuredLogsModule(backendApiConfig);
services.AddStructuredLogsDashboardModule();
```

The module adds a **Structured Logs** item under the Diagnostics menu at
`diagnostics/structured-logs`. Studio requests the server's recent-log and
source endpoints, then uses the authenticated SignalR hub for live events. The
menu and dashboard widget are remote-feature-gated: if the server does not
advertise the structured-log feature, Studio does not treat the page as
available.

Studio hosts in the 3.8.0 release compose these modules explicitly. The server
must still enable the backend feature, and the Studio backend URL and
authentication configuration must point to that server.

## Inspect the server contract

All REST endpoints use the configured Elsa API prefix and require
`read:diagnostics:structured-logs`:

- `GET` or `POST /diagnostics/structured-logs/recent` returns recent events.
  The POST form accepts a `StructuredLogFilter` body.
- `GET /diagnostics/structured-logs/sources` lists known log sources and their
  health metadata.
- `GET /diagnostics/structured-logs/storage` reports whether a storage provider
  is present and how many durable writes it has dropped.

The SignalR hub is `/elsa/hubs/diagnostics/structured-logs`. It requires an
authenticated user and exposes `SubscribeAsync`, `UpdateFilterAsync`, and
`UnsubscribeAsync`. The hub's authorization attribute is separate from the
REST permission; protect the hub and its containing host with the same
authentication policy used for the rest of the Elsa API.

The recent-log filter can match:

- minimum level or an explicit level set;
- category prefix and text across messages, exceptions, properties, and
  scopes;
- tenant, workflow definition, workflow instance, trace, span, correlation,
  and source IDs; and
- timestamp range and maximum result count.

For example:

```json
{
  "MinimumLevel": "Warning",
  "WorkflowInstanceId": "instance-id",
  "From": "2026-09-03T09:00:00Z",
  "Take": 100
}
```

The in-memory store clamps `Take` to `MaxRecentLogQuerySize` (1,000 by
default). Filters are applied to the event timestamp, not the time the host
received the event.

## Understand captured events

The provider is an ordinary `ILoggerProvider`. Each captured event includes
the category, event ID and name, rendered message, message template,
exception, scopes, structured properties, timestamps, source ID, and any
available trace, span, correlation, tenant, workflow-definition, and
workflow-instance IDs.

The module excludes its own `Elsa.Diagnostics.StructuredLogs` categories by
default. Set `IncludeStructuredLogsInternalLogs` only when diagnosing the
diagnostics subsystem itself; enabling it can increase noise quickly.

Sources are tracked from the events they receive. A source becomes stale after
`SourceHeartbeatTimeout` (30 seconds by default), which helps distinguish a
quiet source from a source that has stopped reporting. Source metadata is
provider-owned and can include the host/container identity gathered by the
runtime.

## Redact before buffering or streaming

Redaction runs before an event is stored or sent to a live subscriber. The
default sensitive property-name fragments include `authorization`, `token`,
`password`, `secret`, `api-key`, `cookie`, and connection-string variants. The
default text patterns redact bearer tokens, common `name=value` secrets, and
Azure `AccountKey`/`SharedAccessKey` values.

Add application-specific names and patterns through the feature configuration:

```csharp
builder.Services.AddElsa(elsa => elsa.UseStructuredLogs(structuredLogs =>
{
    structuredLogs.SensitiveNames.Add("client-secret");
    structuredLogs.SensitiveTextPatterns.Add(
        @"(?i)private-key\s*[=:]\s*[^\s,;]+");
}));
```

Redaction is a safety net, not a substitute for avoiding secrets in log
messages and properties. Review custom patterns and test representative log
payloads before granting operators access.

## Add SQLite persistence

Register SQLite from the structured-log feature when log history should survive
process restarts:

```csharp
using Elsa.Diagnostics.StructuredLogs.Persistence.Sqlite.Extensions;
using Elsa.Extensions;

builder.Services.AddElsa(elsa =>
{
    elsa.UseStructuredLogs(structuredLogs =>
    {
        structuredLogs.UseSqliteStorage(
            "Data Source=elsa-structured-logs.db",
            sqlite =>
            {
                sqlite.RunMigrationsOnStartup = true;
                sqlite.Relational.WriteQueue.BatchSize = 100;
                sqlite.Relational.Retention.MaxAge = TimeSpan.FromDays(14);
                sqlite.Relational.Retention.CleanupOnStartup = true;
            });
    });
});
```

SQLite migrations run at startup by default. Set `RunMigrationsOnStartup` to
`false` when deployment tooling owns schema migration, and create the
`StructuredLogEvents` table before the application starts.

Relational writes pass through a bounded queue. The defaults are a capacity of
10,000 queued events, batches of 100, a one-second flush interval, and a
10-second shutdown flush timeout. When the queue is full, new writes are
dropped. A failed or timed-out flush can also increase the dropped-write
counter. Inspect `/diagnostics/structured-logs/storage` during operations and
alert on a non-zero dropped-write count.

Retention is opt-in. `MaxAge` deletes records older than the configured age;
`MaxRows` deletes rows beyond the configured maximum. The SQLite startup
service performs cleanup only when `CleanupOnStartup` is true. Choose limits
based on the log volume and the size of message, exception, scope, and property
JSON stored in each row.

The default SQLite filename is local to the process. It is suitable for a
single host or development, not as a shared multi-node log store. For a
cluster, use a relational provider that supplies its own connection factory,
SQL dialect, migration runner, and shared database topology; the 3.8.0 core
relational package does not provision those services automatically.

## Structured Logs versus the other logging features

Use the feature that matches the data you need:

- `Elsa.Diagnostics.StructuredLogs` captures host `ILogger` events for Studio
  diagnostics and optional durable storage.
- `Elsa.Logging` from `elsa-extensions` adds the workflow `Log` activity and
  routes its entries to configured sinks. It is not the `ILogger` provider
  described on this page.
- `Elsa.Diagnostics.ConsoleLogs` captures raw stdout/stderr lines. It does not
  turn those lines into structured `ILogger` events.
- The workflow journal and activity execution records describe workflow
  execution state, not the complete host application log stream.

These features can be enabled together, but they have separate packages,
permissions, buffers, and retention behavior.

## Troubleshooting checklist

1. Confirm the server package is installed and `UseStructuredLogs` is present.
2. Confirm `app.UseStructuredLogs()` runs after routing is configured.
3. Confirm the caller has `read:diagnostics:structured-logs` for REST requests
   and is authenticated for SignalR.
4. Confirm custom Studio hosts register both `AddStructuredLogsModule` and
   `AddStructuredLogsDashboardModule`.
5. Emit an `ILogger` event from the host. `Console.WriteLine` is covered by
   Console Logs, not Structured Logs.
6. Check the source list for stale sources and the storage endpoint for dropped
   durable writes.
7. If SQLite is enabled, verify the connection string, schema migration mode,
   file permissions, and whether the write queue is flushing successfully.

## Release source

This page is validated against `release/3.8.0` at the following commits:

- [Core structured-log module](https://github.com/elsa-workflows/elsa-core/tree/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.Diagnostics.StructuredLogs)
- [Core SQLite persistence](https://github.com/elsa-workflows/elsa-core/tree/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.Diagnostics.StructuredLogs.Persistence.Sqlite)
- [Core structured-log dashboard](https://github.com/elsa-workflows/elsa-core/tree/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.Diagnostics.StructuredLogs.Dashboard)
- [Studio structured-log module](https://github.com/elsa-workflows/elsa-studio/tree/a9f7b70ae36b9b81c16f327a8187df6cc77b1503/src/modules/Elsa.Studio.Diagnostics.StructuredLogs)
- [Studio structured-log dashboard](https://github.com/elsa-workflows/elsa-studio/tree/a9f7b70ae36b9b81c16f327a8187df6cc77b1503/src/modules/Elsa.Studio.Diagnostics.StructuredLogs.Dashboard)
- [`Elsa.Logging` workflow activity](https://github.com/elsa-workflows/elsa-extensions/tree/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/diagnostics/Elsa.Logging)
