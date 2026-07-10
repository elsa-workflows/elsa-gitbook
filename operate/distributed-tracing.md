---
description: >-
  Release-backed guide to tracing Elsa 3.8.0 workflows with
  Elsa.Workflows instrumentation and the OpenTelemetry diagnostics collector.
---

# Distributed Tracing

In `release/3.8.0`, Elsa has two distinct OpenTelemetry stories:

- `Elsa.Workflows` emits workflow and activity spans plus workflow metrics.
- `Elsa.Diagnostics.OpenTelemetry` ingests OTLP data so Elsa Studio can query
  and stream traces, metrics, and logs.

Treat those as complementary parts of one tracing setup:

1. your Elsa host emits telemetry;
2. your OTLP backend stores and correlates it;
3. Elsa's diagnostics collector can optionally receive the same OTLP traffic
   for Studio diagnostics.

## What Elsa emits directly

The release-backed instrumentation surface is `Elsa.Workflows`.

- `ActivitySource`: `Elsa.Workflows`
- `Meter`: `Elsa.Workflows`

`WorkflowInstrumentation` in `elsa-core` emits:

- workflow spans with operation name `workflow.execute`;
- activity spans with operation name `activity.execute`;
- counters for `elsa.workflow.started`, `elsa.workflow.completed`, and
  `elsa.workflow.faulted`;
- a histogram for `elsa.activity.duration`.

Key workflow span tags include:

- `workflow.instance.id`
- `workflow.definition.id`
- `workflow.definition.version`
- `workflow.definition.version.id`
- `workflow.status`
- `workflow.substatus`
- `workflow.name`
- `workflow.parent.instance.id`
- `workflow.correlation.id`
- `elsa.tenant.id`

Key activity span tags include:

- `workflow.activity.id`
- `workflow.activity.name`
- `workflow.activity.type`
- `workflow.activity.version`
- `workflow.activity.execution.id`
- `workflow.activity.status`
- `workflow.activity.parent.execution.id`
- `workflow.activity.scheduled.by.execution.id`
- `workflow.activity.outcome`

When execution faults, the instrumentation marks the span as error and adds a
standard `exception.type` tag.

## Basic exporter setup

To export Elsa workflow spans and metrics from your host, subscribe to the
`Elsa.Workflows` source and meter:

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

Use this path when you want traces in systems such as Jaeger, Grafana Tempo,
Honeycomb, Datadog, or any other OTLP-compatible backend.

## What to expect in traces

For the built-in `Elsa.Workflows` instrumentation:

- one workflow execution cycle creates one workflow span;
- each executed activity creates an activity span;
- faulted executions set the span status to error;
- cancellations are treated separately from faults;
- parent workflow instance IDs and correlation IDs flow into span tags when
  present.

This makes `CorrelationId`, parent-child workflow dispatch, and activity
timings directly queryable in your tracing backend.

## Elsa Studio collector and trace viewer

`Elsa.Diagnostics.OpenTelemetry` does not replace the `Elsa.Workflows`
instrumentation. It adds a diagnostics collector and Studio-facing query
surface.

In `release/3.8.0`, the collector maps these HTTP/protobuf ingestion routes by
default:

- `POST /elsa/otlp/v1/traces`
- `POST /elsa/otlp/v1/metrics`
- `POST /elsa/otlp/v1/logs`

It also exposes Studio-facing read endpoints:

- `POST /diagnostics/opentelemetry/resources/search`
- `POST /diagnostics/opentelemetry/traces/search`
- `GET /diagnostics/opentelemetry/traces/{traceId}`
- `POST /diagnostics/opentelemetry/metrics/search`
- `POST /diagnostics/opentelemetry/logs/search`
- `GET /diagnostics/opentelemetry/storage`
- `GET /diagnostics/opentelemetry/collector-configuration`

For live updates, the diagnostics module also maps the SignalR hub route:

- `/elsa/hubs/diagnostics/opentelemetry`

Read access is protected by `read:diagnostics:opentelemetry`.

## Securing OTLP ingestion

The diagnostics collector has a separate ingestion permission concept.

- Read endpoints use `read:diagnostics:opentelemetry`.
- OTLP ingestion uses `ingest:diagnostics:opentelemetry` internally.

For HTTP/protobuf ingestion, `OpenTelemetryDiagnosticsOptions` supports API key
protection. Configure it through standard options binding:

```csharp
builder.Services.Configure<OpenTelemetryDiagnosticsOptions>(options =>
{
    options.ApiKey = "replace-me";
    options.ApiKeyHeaderName = "x-otlp-api-key";
});
```

If you configure an API key, callers send it in the `x-otlp-api-key` header by
default. The collector configuration endpoint intentionally returns
`<configured>` instead of the secret value.

## Collector configuration and endpoint options

`OpenTelemetryDiagnosticsOptions` lets you tune collector behavior, including:

- `HttpEndpointPath`, default `/elsa/otlp/v1`
- `HubRoute`, default `/elsa/hubs/diagnostics/opentelemetry`
- `TraceCapacity`
- `SpanCapacity`
- `MetricPointCapacity`
- `LogRecordCapacity`
- `ResourceCapacity`
- `SubscriberChannelCapacity`
- `MaxQuerySize`
- `MaxHttpRequestBodySize`
- `EnableGrpc`
- `GrpcEndpointPath`

When you enable the diagnostics shell feature through
`UseOpenTelemetryDiagnostics`, the feature itself exposes the in-memory
capacity and max-request-body settings. For endpoint paths, hub route, API key,
and related collector behavior, configure `OpenTelemetryDiagnosticsOptions`
directly.

The `GET /diagnostics/opentelemetry/collector-configuration` endpoint reports
the active HTTP and gRPC collector metadata plus the expected OTEL environment
variables:

- `OTEL_SERVICE_NAME`
- `OTEL_EXPORTER_OTLP_ENDPOINT`
- `OTEL_EXPORTER_OTLP_PROTOCOL`

## About gRPC ingestion in 3.8.0

This release exposes shared gRPC collector metadata, but the shared
`Elsa.Diagnostics.OpenTelemetry` module does not itself bind a concrete gRPC
collector service.

What the code does in `release/3.8.0`:

- if `EnableGrpc` is `false`, no gRPC collector path is exposed;
- if `EnableGrpc` is `true` but `GrpcEndpointPath` is empty, Elsa throws during
  endpoint mapping;
- the shared module documents that actual gRPC service binding is host-specific.

So for a portable setup across Elsa hosts, use the HTTP/protobuf OTLP routes
unless your host explicitly adds the gRPC binding.

## Older `Elsa.OpenTelemetry` extension package

The `elsa-extensions` repository still contains an `Elsa.OpenTelemetry` module
that adds workflow and activity execution middleware with its own
`ActivitySource`.

That middleware:

- creates workflow spans named `execute workflow {name}`;
- creates activity spans named `execute activity {type}`;
- records incident details and correlation IDs on spans;
- supports `UseNewRootActivityForRemoteParent` and
  `UseDummyParentActivityAsRootSpan`.

You will still see it used in the `Elsa.Server.Web` workbench host in
`elsa-extensions`, but for general `release/3.8.0` guidance the stable
baseline is still `Elsa.Workflows` instrumentation plus, optionally, the
diagnostics collector.

Use the extension middleware only when you are intentionally adopting that
host-specific tracing behavior and understand its trace-root options.

## Recommended deployment patterns

Use one of these patterns:

### Backend-only tracing

Use this when operators work primarily in your external observability stack.

- Export `Elsa.Workflows` spans and metrics directly to your OTLP backend.
- Use Elsa incidents and activity records for workflow-local diagnosis.
- Keep Studio diagnostics focused on structured logs and runtime inspection.

### Backend plus Studio trace diagnostics

Use this when Elsa Studio users also need an in-product trace view.

- Export `Elsa.Workflows` telemetry to your OTLP backend.
- Send OTLP telemetry to Elsa's diagnostics collector as well.
- Grant operators `read:diagnostics:opentelemetry`.
- Tune in-memory capacities so Studio diagnostics stay bounded.

## Troubleshooting

If traces do not appear where you expect, check these points in order:

1. Confirm your OpenTelemetry pipeline subscribes to
   `WorkflowInstrumentation.ActivitySourceName`.
2. Confirm your metrics pipeline subscribes to
   `WorkflowInstrumentation.MeterName`.
3. Verify the OTLP exporter endpoint from the host process, not only from your
   workstation.
4. If Studio diagnostics are empty, verify the collector routes under
   `/elsa/otlp/v1` and the `x-otlp-api-key` header when enabled.
5. If SignalR trace streaming fails, verify the user has
   `read:diagnostics:opentelemetry`.
6. If you enabled gRPC ingestion, confirm your host actually bound the gRPC
   collector service instead of only setting collector metadata.

## Related guides

- [Monitoring & Observability](monitoring-observability.md)
- [Incidents](incidents/README.md)
- [Log Persistence](../optimize/log-persistence.md)
