---
description: >-
  Release-backed guide to observing Elsa 3.8.0 with incidents, execution logs,
  activity execution records, Studio diagnostics, and OpenTelemetry.
---

# Monitoring & Observability

Elsa 3.8.0 gives you several different observability layers. They solve
different problems, so it helps to treat them as complementary instead of
interchangeable.

Use this mapping:

- For a single workflow instance, start with incidents, the workflow journal,
  and activity execution records.
- For host application logs, use the Structured Logs or Console Logs diagnostics
  modules in Studio.
- For exported traces, metrics, and OTLP logs, use
  `Elsa.Workflows` OpenTelemetry instrumentation and your OTLP backend.
- For readiness and liveness, use health checks such as `/health/live` and
  `/health/ready` in `Elsa.Server.Web`.

If you need a dedicated tracing setup guide, see
[Distributed Tracing](distributed-tracing.md).

## Start with the workflow runtime signals

When you investigate one workflow instance, start with Elsa's own runtime
records before looking at infrastructure telemetry.

### Incidents

When an activity throws and the exception is not handled inside the workflow,
Elsa records an incident on the workflow state. In `release/3.8.0`, incidents
are stored on `WorkflowState.Incidents` and shown in Elsa Studio's workflow
instance viewer.

Use incidents when you need:

- The failing activity and node ID.
- The exception message, inner exception, and stack trace.
- A quick answer to whether the workflow faulted or continued with recorded incidents.

Related docs:

- [Incidents](incidents/README.md)
- [Incident configuration](incidents/configuration.md)

### Workflow journal

Elsa also keeps a workflow execution journal. The journal is an ordered stream
of workflow execution log records such as `Started`, `Suspended`, and
`Faulted`.

In `release/3.8.0`, the workflow APIs expose the journal through:

- `GET /workflow-instances/{id}/journal`
- `POST /workflow-instances/{id}/journal`

The `POST` variant supports filtering by activity IDs, node IDs, event names,
and excluded activity types.

Use the journal when you need:

- A timeline of what happened.
- The sequence of activity transitions.
- Fault messages without opening raw application logs first.

### Activity execution records

For per-activity inspection, Elsa stores `ActivityExecutionRecord` entries in
the runtime persistence store. These records are what power activity-level
inspection in Studio, including state snapshots, outputs, timing, retries, and
call stack navigation.

In `release/3.8.0`, the relevant APIs include:

- `GET /activity-executions/list`
- `GET /activity-execution-summaries/list`

Use activity execution records when you need:

- The last captured state of a specific activity.
- Inputs, outputs, metadata, exception state, and timestamps.
- Cross-workflow scheduling context and call stack depth.

How much input, output, and internal state Elsa stores is controlled by log
persistence settings. If activity records are too noisy or contain more data
than you want to retain, tune them with
[Log Persistence](../optimize/log-persistence.md).

## Use Studio diagnostics for host-level logs

The runtime signals above are workflow-centric. For host diagnostics,
Elsa 3.8.0 also ships separate Studio diagnostics modules.

### Structured Logs

`Elsa.Diagnostics.StructuredLogs` captures `ILogger` events, redacts sensitive
values, keeps a bounded recent buffer, exposes REST endpoints, and streams live
updates to Studio over SignalR.

Key routes and permission:

- `POST /diagnostics/structured-logs/recent`
- `GET /diagnostics/structured-logs/sources`
- `/elsa/hubs/diagnostics/structured-logs`
- `read:diagnostics:structured-logs`

Use Structured Logs when you want:

- Application logs with categories, levels, timestamps, workflow IDs,
  correlation IDs, and source metadata.
- Filtering inside Studio by workflow instance, trace ID, category, level, or
  source.
- A safer operator-facing view than raw console output.

### Console Logs

`Elsa.Diagnostics.ConsoleLogs` captures raw `stdout` and `stderr` lines from
the current process and streams them to Studio.

Key routes and permission:

- `POST /diagnostics/console-logs/recent`
- `GET /diagnostics/console-logs/sources`
- `/elsa/hubs/diagnostics/console-logs`
- `read:diagnostics:console-logs`

Use Console Logs when you want:

- Raw console output exactly as the process emitted it.
- ANSI-colored output during local troubleshooting.
- Visibility into output that did not go through `ILogger`.

Prefer Structured Logs for routine operations. Use Console Logs when you
explicitly need raw process output.

### Self-hosted Elsa Server example

If you host Elsa yourself instead of using the modular sample host, enable the
diagnostics modules explicitly:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa
        .UseStructuredLogs()
        .UseStructuredLogsDashboard()
        .UseConsoleLogs()
        .UseConsoleLogsDashboard();
});

var app = builder.Build();

app.UseStructuredLogs();
app.UseConsoleLogs();
```

## Export traces and metrics with OpenTelemetry

In `release/3.8.0`, workflow tracing and metrics come from `Elsa.Workflows`
itself. The core instrumentation publishes:

- `ActivitySource`: `Elsa.Workflows`
- `Meter`: `Elsa.Workflows`

This is the release-backed way to export workflow traces and metrics to
OTLP-compatible tooling.

```csharp
using Elsa.Workflows.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(WorkflowInstrumentation.ActivitySourceName)
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(WorkflowInstrumentation.MeterName)
        .AddOtlpExporter());
```

The built-in workflow instrumentation includes workflow and activity spans plus
counters and histograms such as workflow started, completed, faulted, and
activity duration.

## Studio OpenTelemetry diagnostics are collector-side

Elsa 3.8.0 also includes `Elsa.Diagnostics.OpenTelemetry`, but it serves a
different purpose from `Elsa.Workflows` instrumentation.

- `Elsa.Workflows` produces traces and metrics.
- `Elsa.Diagnostics.OpenTelemetry` ingests, stores, queries, and streams OTLP
  telemetry for Studio.

The diagnostics collector exposes:

- `POST /elsa/otlp/v1/traces`
- `POST /elsa/otlp/v1/metrics`
- `POST /elsa/otlp/v1/logs`
- `POST /diagnostics/opentelemetry/resources/search`
- `POST /diagnostics/opentelemetry/traces/search`
- `GET /diagnostics/opentelemetry/traces/{traceId}`
- `POST /diagnostics/opentelemetry/metrics/search`
- `POST /diagnostics/opentelemetry/logs/search`
- `GET /diagnostics/opentelemetry/storage`
- `GET /diagnostics/opentelemetry/collector-configuration`
- `/elsa/hubs/diagnostics/opentelemetry`

Read access requires `read:diagnostics:opentelemetry`. OTLP ingestion can also
be protected with an API key through
`OpenTelemetryDiagnosticsOptions.ApiKey`, using the `x-otlp-api-key` header by
default.

If you use the modular sample host, `Elsa.ModularServer.Web` already shows the
intended setup for this release:

- it exports logs, traces, and metrics with OpenTelemetry;
- it uses `WorkflowInstrumentation.ActivitySourceName` and
  `WorkflowInstrumentation.MeterName`;
- it enables the `StructuredLogs`, `ConsoleLogs`, and `OpenTelemetry` shell
  features in configuration.

## Avoid the stale `Elsa.OpenTelemetry` guidance

If you encounter older documentation or examples that tell you to use
`Elsa.OpenTelemetry` from `elsa-extensions`, treat that guidance as outdated
for `release/3.8.0`.

For this release branch:

- use `Elsa.Workflows` instrumentation for emitting traces and metrics;
- use `Elsa.Diagnostics.OpenTelemetry` for collector and Studio diagnostics
  scenarios;
- use `Elsa.Diagnostics.StructuredLogs` and `Elsa.Diagnostics.ConsoleLogs` for
  host log inspection in Studio.

## Recommended operator flow

When a production issue is reported, this order usually produces the fastest
answer:

1. Open the workflow instance in Studio and check **Status**,
   **Sub status**, and **Incidents**.
2. Inspect the workflow journal to see where execution started, suspended,
   resumed, or faulted.
3. Open the relevant activity execution record and call stack for the failing
   branch.
4. If you still need host context, open **Structured Logs** filtered by
   workflow instance ID.
5. If the problem spans multiple services, inspect exported OTLP traces and metrics.
6. If the host may be unhealthy, verify `/health/ready` before retrying or scaling.

For the instance and journal route shapes, filters, permission split, and
activity call-stack workflow, see [Investigate a Workflow
Instance](workflow-state-and-journal.md).

## Related guides

- [Distributed Tracing](distributed-tracing.md)
- [Incidents](incidents/README.md)
- [Log Persistence](../optimize/log-persistence.md)
- [Performance & Scaling Guide](../guides/performance/README.md)
- [Troubleshooting Guide](../guides/troubleshooting/README.md)
