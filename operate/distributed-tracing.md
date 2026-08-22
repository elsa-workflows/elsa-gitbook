---
description: >-
  Release-backed guide to tracing Elsa 3.8.0 workflows with
  Elsa.OpenTelemetry workflow/activity middleware and the OpenTelemetry
  diagnostics collector.
---

# Distributed Tracing

In `release/3.8.0`, Elsa has two distinct OpenTelemetry stories:

- Core `Elsa.Workflows` instrumentation emits baseline workflow/activity spans
  and metrics.
- `Elsa.OpenTelemetry` adds workflow/activity spans from explicit Elsa
  execution-pipeline middleware.
- `Elsa.Diagnostics.OpenTelemetry` ingests OTLP data so Elsa Studio can query
  and stream traces, metrics, and logs.

Treat those as complementary parts of one tracing setup:

1. your Elsa host emits workflow/activity telemetry;
2. your OTLP backend stores and correlates it;
3. Elsa's diagnostics collector can optionally receive the same OTLP traffic
   for Studio diagnostics.

## Server-side workflow and activity spans

Core instrumentation uses an `ActivitySource` and `Meter` both named
`Elsa.Workflows`. It emits `workflow.execute` and `activity.execute` spans,
the `elsa.workflow.started`, `elsa.workflow.completed`, and
`elsa.workflow.faulted` counters, and the `elsa.activity.duration` histogram.

The optional `Elsa.OpenTelemetry` extension package adds another workflow and
activity span layer. It uses the same source, but the module does not register
its middleware or an exporter for you. Add both pipeline components and
subscribe your OpenTelemetry provider to the source and meter:

```csharp
using Elsa.Extensions;
using Elsa.OpenTelemetry.Middleware;
using Elsa.Workflows.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(WorkflowInstrumentation.ActivitySourceName)
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(WorkflowInstrumentation.MeterName)
        .AddOtlpExporter());

builder.Services.AddElsa(elsa => elsa
    .UseWorkflows(workflows =>
    {
        workflows.WithDefaultWorkflowExecutionPipeline(pipeline =>
            pipeline.UseWorkflowExecutionTracing());
        workflows.WithDefaultActivityExecutionPipeline(pipeline =>
            pipeline.UseActivityExecutionTracing());
    })
    .UseOpenTelemetry());
```

The extension middleware adds one workflow span and one span per executed
activity when the provider is listening to `Elsa.Workflows`. With core
instrumentation also active, both the core and extension span layers are
exported. For exact extension span names, tags, status events, remote-parent
behavior, and custom error handlers, see
[OpenTelemetry workflow and activity tracing](../guides/extensibility/opentelemetry-tracing.md).

The extension does not add another meter; core metrics come from
`WorkflowInstrumentation`. Configure other host instrumentation separately if
your application needs it.

## Elsa Studio collector and trace viewer

`Elsa.Diagnostics.OpenTelemetry` does not replace the `Elsa.OpenTelemetry`
middleware. It adds a diagnostics collector and Studio-facing query surface.

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

## `Elsa.OpenTelemetry` middleware

`Elsa.OpenTelemetry` is the release/3.8.0 extension package that supplies the
workflow and activity execution middleware. It is not enabled by
`UseOpenTelemetry()` alone: add `UseWorkflowExecutionTracing()` and
`UseActivityExecutionTracing()` to the pipelines you want to instrument.

The [OpenTelemetry workflow and activity tracing guide](../guides/extensibility/opentelemetry-tracing.md)
covers the emitted span names and tags, error-handler ordering, remote-parent
options, and the Studio boundary. The package's `ActivitySource` is named
`Elsa.Workflows`; core instrumentation and the extension therefore share one
exporter subscription. The extension adds spans and error handling but does not
replace the core `WorkflowInstrumentation` metrics/API.

## Recommended deployment patterns

Use one of these patterns:

### Backend-only tracing

Use this when operators work primarily in your external observability stack.

- Export `Elsa.Workflows` spans directly to your OTLP backend.
- Use Elsa incidents and activity records for workflow-local diagnosis.
- Keep Studio diagnostics focused on structured logs and runtime inspection.

### Backend plus Studio trace diagnostics

Use this when Elsa Studio users also need an in-product trace view.

- Export `Elsa.Workflows` spans to your OTLP backend.
- Send OTLP telemetry to Elsa's diagnostics collector as well.
- Grant operators `read:diagnostics:opentelemetry`.
- Tune in-memory capacities so Studio diagnostics stay bounded.

## Troubleshooting

If traces do not appear where you expect, check these points in order:

1. Confirm your OpenTelemetry tracer provider subscribes to
   `.AddSource("Elsa.Workflows")`.
2. If you expect the additional extension layer, confirm both Elsa tracing
   middleware components are present in the workflow and activity pipelines.
3. If you expect core metrics, confirm the meter provider subscribes to
   `.AddMeter("Elsa.Workflows")` or `WorkflowInstrumentation.MeterName`.
4. Verify the OTLP exporter endpoint from the host process, not only from your
   workstation.
5. If Studio diagnostics are empty, verify the collector routes under
   `/elsa/otlp/v1` and the `x-otlp-api-key` header when enabled.
6. If SignalR trace streaming fails, verify the user has
   `read:diagnostics:opentelemetry`.
7. If you enabled gRPC ingestion, confirm your host actually bound the gRPC
   collector service instead of only setting collector metadata.

## Related guides

- [Monitoring & Observability](monitoring-observability.md)
- [Incidents](incidents/README.md)
- [Log Persistence](../optimize/log-persistence.md)
