---
description: >-
  Configure Elsa 3.8.0 raw console-log capture, filtering, SignalR streaming,
  and Elsa Studio operations.
---

# Console Logs

`Elsa.Diagnostics.ConsoleLogs` is an opt-in diagnostics module for raw managed
`stdout` and `stderr` output from the Elsa backend process. It provides a
bounded recent buffer, filtered REST access, and live SignalR streaming for
authorized operators.

Use Console Logs when you need output written to `Console.Out` or
`Console.Error`, including output that did not pass through `ILogger`. Use
[Structured Logs](structured-logs.md) when you need categories, levels,
structured properties, scopes, or trace and span identifiers. Console Logs are
not a workflow journal, durable audit log, or replacement for your platform's
container and process log collection.

## What the module captures

The underlying `ConsoleLogStreaming` packages capture managed .NET console
writes as line-oriented events. In the Elsa integration, each event can also
carry:

- source identity and host metadata;
- `stdout` or `stderr` stream information;
- the workflow instance, definition, and version that were executing;
- the activity instance, activity ID, and activity node ID; and
- dropped-line or truncation information when a bounded buffer cannot retain
  every event.

The capture is process-wide and is intentionally bounded. It does not
guarantee capture of native code writing directly to file descriptors, output
from child processes that is not redirected through the current process, or
libraries that cached `Console.Out` or `Console.Error` before capture started.
Use your container or host logging system when you need authoritative process
logs or durable retention.

Partial writes are held until a newline, the configured maximum line length,
or an idle-flush timeout. Lines that exceed the maximum are emitted as one
truncated event. This means a write call is not necessarily one Studio row.

## Enable the server feature

Install the `Elsa.Diagnostics.ConsoleLogs` package that matches your Elsa
version. In an Elsa modular host, register the feature and map its live hub:

```csharp
using Elsa.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddElsa(elsa =>
{
    elsa.UseConsoleLogs(options =>
    {
        options.RecentCapacity = 5_000;
        options.MaxRecentQuerySize = 1_000;
        options.MaxLineLength = 16_384;
        options.PreserveAnsi = true;
    });
});

var app = builder.Build();

app.UseRouting();
app.UseConsoleLogs();
app.Run();
```

`UseConsoleLogs` registers the FastEndpoints feature, the capture pipeline,
workflow/activity metadata enrichment, and the REST endpoints through Elsa's
module system. The application-builder call maps the SignalR hub. Keep it in
the normal endpoint-routing part of the host pipeline.

The lower-level `AddConsoleLogsHost` service registration is not a substitute
for `UseConsoleLogs`: it provides host-level capture services but does not by
itself add Elsa's FastEndpoints assembly, workflow metadata middleware, or
Elsa endpoint mapping.

### Add dashboard findings (optional)

Install `Elsa.Diagnostics.ConsoleLogs.Dashboard` when the Elsa dashboard should
include console-log diagnostics:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa
        .UseConsoleLogs()
        .UseConsoleLogsDashboard();
});
```

The dashboard contribution reports source count, recent `stderr` count, and
dropped-line count. It can also raise findings for unauthorized or unavailable
data, stale/disconnected sources, and dropped lines. It does not make the
recent buffer durable.

## Configure capture safely

The 3.8.0 Core source supplies these defaults before applying your feature
configuration:

| Option | Release default or behavior |
| --- | --- |
| `RecentCapacity` | `2,000` lines in the Elsa recent buffer |
| `MaxRecentQuerySize` | `250` lines per recent query unless configured otherwise |
| `PreserveAnsi` | `true`; consumers may render or strip ANSI sequences |
| `SourceId` | Machine name plus process ID, unless the underlying provider changes it |
| `SourceDisplayName` | `HOSTNAME` when available, otherwise the source ID |
| `ServiceName` | `OTEL_SERVICE_NAME`, otherwise the application domain name |

The buffer and query limits are independent. Increasing the query limit does
not make the history unlimited; the recent buffer must retain the lines first.
The Studio viewer also keeps a bounded local row buffer and reports rows it
discarded locally.

Redaction happens before recent buffering, live delivery, endpoint responses,
and provider storage. Keep secrets out of console output anyway, and add
application-specific rules when needed:

```csharp
builder.Services.AddElsa(elsa => elsa.UseConsoleLogs(options =>
{
    options.RedactionRules.Add(new()
    {
        Name = "Tenant token",
        Pattern = @"tenant-token=[^\s]+",
        Replacement = "tenant-token=[redacted]"
    });
}));
```

For modular shell configuration, the feature exposes equivalent settings for
recent capacity, subscriber capacity, maximum query size, maximum line length,
idle flush timeout, ANSI stripping, replacement text, and sensitive text
patterns. Choose limits for the expected output rate and operator workload;
subscriber queues and recent buffers are deliberately bounded.

ANSI sequences are preserved by default and can be stripped server-side with
`PreserveAnsi = false` (or the modular feature's ANSI-stripping setting). ANSI
rendering is a display concern: the Studio page has its own **ANSI colors**
toggle.

## REST and SignalR contracts

All paths below are relative to the configured Elsa API prefix. In a standard
Elsa Server deployment, that commonly means `/elsa/api` for REST requests.

### Recent lines

`POST /diagnostics/console-logs/recent` accepts a JSON filter and returns a
`RecentConsoleLogsResult` containing `items`, dropped-line summaries, and
optional source data. The filter can contain:

- `sourceId`;
- `stream`: `stdout`, `stderr`, `all`, or `null`;
- `query`: a case-insensitive text search;
- `workflowInstanceId`, `workflowDefinitionId`, and
  `workflowDefinitionVersionId`;
- `activityInstanceId`, `activityId`, and `activityNodeId`;
- `metadata`: additional exact metadata matches;
- `from` and `to` timestamps; and
- `limit`, which is clamped by the configured recent-query maximum.

For example, this requests recent standard-error lines associated with one
workflow instance:

```http
POST /elsa/api/diagnostics/console-logs/recent
Content-Type: application/json

{
  "stream": "stderr",
  "workflowInstanceId": "workflow-instance-id",
  "query": "timeout",
  "limit": 100
}
```

The endpoint returns a bad-request response for malformed JSON. Recent data is
bounded and may include dropped-line summaries; do not interpret an empty
result as proof that the process produced no output.

### Sources

`GET /diagnostics/console-logs/sources` lists known backend sources. A source
may include a stable ID, service, process, machine, pod, container, namespace,
node, last-seen time, and health state. In a multi-node deployment, select a
source explicitly when investigating one process; the source list is not a
durable inventory.

### Live SignalR streaming

The hub is mapped at:

```text
/elsa/hubs/diagnostics/console-logs
```

The hub supports these server methods:

- `SubscribeAsync(filter)` starts a pushed subscription;
- `UpdateFilterAsync(filter)` replaces the current filter; and
- `UnsubscribeAsync()` stops it.

Clients receive `ReceiveConsoleLogLineAsync`,
`ReceiveDroppedLinesAsync`, and `ReceiveSourceChangedAsync` events. A client
can also call the hub's `StreamAsync` method for a SignalR streaming result.
When a client reconnects, it should resubscribe with its latest filter.

REST and hub access both require the `read:diagnostics:console-logs`
permission. The hub also requires an authenticated SignalR connection. Apply
the same authentication, proxy, and WebSocket policy to this hub as to the
rest of the Elsa API.

## Use Elsa Studio

Install the `Elsa.Studio.Diagnostics.ConsoleLogs` package that matches your
Studio version and register it in a custom Studio host:

```csharp
using Elsa.Studio.Diagnostics.ConsoleLogs.Extensions;

services.AddConsoleLogsModule(backendApiConfig);
```

If the Studio dashboard is enabled, add the optional
`Elsa.Studio.Diagnostics.ConsoleLogs.Dashboard` package and module too:

```csharp
using Elsa.Studio.Diagnostics.ConsoleLogs.Dashboard.Extensions;

services.AddConsoleLogsDashboardModule();
```

The server advertises the remote feature as
`Elsa.Diagnostics.ConsoleLogs.ShellFeatures.ConsoleLogs`. When that feature is
available, Studio adds **Console** under **Diagnostics** at
`/diagnostics/console`. REST calls use the configured backend API client, and
the SignalR connection uses the same authenticated connection options.

On activation, Studio loads sources and recent rows, then starts the live
subscription. A missing backend feature is shown as unavailable; an HTTP 401
or 403 is shown as unauthorized. The Console menu is remote-feature-gated, so
the server must enable the backend feature even when the Studio module is
installed.

The page provides:

- source and stream controls; text and time-range filters can also be supplied
  through the supported URL/API state;
- pause/resume, clear-local-view, reconnect, copy-visible, and export-visible
  actions;
- follow-tail, line wrapping, compact density, and ANSI-color display
  toggles; and
- connection status plus counts for upstream dropped lines, locally discarded
  rows, paused pending lines, and rows discarded during a refresh.

**Clear** only clears the current Studio view. It does not delete the backend
buffer or affect other operators. **Pause** stops new lines from moving into
the visible rows but keeps a bounded pending queue; resuming flushes that
queue. Recent backfill and live updates use the same filter, and a text filter
is sent to the server rather than being only a local highlight.

The page stores its view state in URL query parameters, making a focused view
shareable. The supported keys are `source`, `stream`, `text`, `from`, `to`,
`wrap`, `compact`, `ansi`, and `follow`.

### Inspect output for a workflow

The Studio module contributes a **Console** tab to workflow-instance views. A
workflow-scoped tab filters by workflow instance ID. The activity-scoped tab
also filters by activity instance, activity ID, and activity node ID when an
activity is selected. This is useful for correlating `Console.WriteLine` calls
with a workflow execution, but it does not turn raw output into a structured
activity log.

## Choose the right diagnostic surface

| Need | Use |
| --- | --- |
| Raw `Console.WriteLine`/`Console.Error.WriteLine` output | Console Logs |
| Categories, levels, scopes, properties, and trace context | [Structured Logs](structured-logs.md) |
| Workflow state transitions and fault details | [Workflow journal](workflow-state-and-journal.md) and [Incidents](incidents/README.md) |
| Activity inputs, outputs, retries, and execution timing | [Log Persistence](../optimize/log-persistence.md) and activity execution records |
| Durable, authoritative process or container history | Platform/container logging |

Console Logs and Structured Logs can be enabled together, but they have
separate packages, permissions, buffers, and data models.

## Troubleshooting checklist

1. Confirm `Elsa.Diagnostics.ConsoleLogs` is installed and `UseConsoleLogs()`
   is registered in the server host.
2. Confirm `app.UseConsoleLogs()` runs in the endpoint-routing pipeline so the
   SignalR hub is mapped.
3. Confirm the caller is authenticated and has
   `read:diagnostics:console-logs`.
4. Confirm the Studio module points to the same backend and that the backend
   advertises `Elsa.Diagnostics.ConsoleLogs.ShellFeatures.ConsoleLogs`.
5. If recent lines are empty, check the source selector, stream, time, and
   text filters, then emit a managed `Console.WriteLine` and
   `Console.Error.WriteLine` after capture has started.
6. If live output is unavailable, check the hub path, reverse-proxy WebSocket
   support, and the browser's authenticated connection. Use **Reconnect**
   after correcting the transport.
7. If lines are missing, inspect dropped-line indicators and reduce output
   pressure or increase the bounded capacities carefully.
8. If ANSI colors are absent, remember that redirected console output often
   has colors disabled by the .NET console formatter; enable the formatter's
   color behavior or use Studio's plain-text mode when color is not required.
9. For durable or complete process logs, inspect the platform/container log
   collector instead of relying on the in-memory Console Logs buffer.

## Release source

This page is validated against the requested `release/3.8.0` branch:

- [Core Console Logs module at 8191ae3](https://github.com/elsa-workflows/elsa-core/tree/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Diagnostics.ConsoleLogs)
- [Core Console Logs dashboard at 8191ae3](https://github.com/elsa-workflows/elsa-core/tree/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Diagnostics.ConsoleLogs.Dashboard)
- [Core Console Logs design notes at 8191ae3](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/doc/wiki/diagnostics-console-logs.md)
- [Studio Console Logs module at 8539524](https://github.com/elsa-workflows/elsa-studio/tree/85395246140c6b192a2457992da8704b0cdec3d0/src/modules/Elsa.Studio.Diagnostics.ConsoleLogs)
- [Studio Console Logs dashboard at 8539524](https://github.com/elsa-workflows/elsa-studio/tree/85395246140c6b192a2457992da8704b0cdec3d0/src/modules/Elsa.Studio.Diagnostics.ConsoleLogs.Dashboard)

The `release/3.8.0` Extensions source at `66861ae` has no direct Console Logs
implementation; the feature is provided by Core and consumed by the Studio
module.
