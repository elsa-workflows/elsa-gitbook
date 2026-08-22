---
description: >-
  Configure Elsa's release/3.8.0 OpenTelemetry workflow and activity tracing
  middleware, understand its spans, and customize fault metadata.
---

# OpenTelemetry workflow and activity tracing

Elsa `release/3.8.0` has two server-side workflow telemetry layers:

- Core `Elsa.Workflows` instrumentation automatically emits baseline workflow
  and activity spans plus workflow counters and an activity-duration histogram.
- The optional `Elsa.OpenTelemetry` extension adds middleware spans with
  extension-specific tags, status events, remote-parent handling, and ordered
  error-span handlers.

Both layers use the `Elsa.Workflows` activity source. If both are enabled and
your tracer provider subscribes to that source, seeing both baseline and
extension spans is expected. Elsa Studio does not consume the extension
package directly.

This package is separate from `Elsa.Diagnostics.OpenTelemetry`, which receives
OTLP data and exposes trace-search and live-feed endpoints for Studio. Use the
[distributed tracing guide](../../operate/distributed-tracing.md) when you
also want Elsa to collect and display telemetry.

## When to use it

Use `Elsa.OpenTelemetry` when you need workflow-specific spans in an existing
OpenTelemetry pipeline, for example to correlate a workflow execution with an
HTTP request, message, or downstream service call.

The extension module does not create an exporter, collector, or storage
provider. Your host must subscribe an OpenTelemetry exporter to the
`Elsa.Workflows` source. Subscribe to the meter as well if you want the core
workflow counters and activity-duration metric.

## Install and register the runtime pieces

Install the Elsa module and the OpenTelemetry hosting/exporter packages used by
your application. For OTLP export, a minimal setup is:

```shell
dotnet add package Elsa.OpenTelemetry
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

Register the exporter and explicitly insert both tracing middleware components
into the default Elsa pipelines:

```csharp
using Elsa.Extensions;
using Elsa.OpenTelemetry.Middleware;
using Elsa.Workflows.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

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

`UseOpenTelemetry()` configures the error-span handlers and remote-parent
options. The two `Use*ExecutionTracing()` calls are what insert the middleware.
If you build custom workflow or activity pipelines, add the corresponding
middleware to those pipelines as well.

The core instrumentation and extension both use an `ActivitySource` named
`Elsa.Workflows`. If the source is not included in the OpenTelemetry tracer
provider, `StartActivity` returns `null` and the relevant instrumentation
continues execution without producing spans.

## Core instrumentation versus extension middleware

Core instrumentation runs from the workflow runner and activity invoker. It
uses the `Elsa.Workflows.Telemetry.WorkflowInstrumentation` API and emits:

- workflow spans named `workflow.execute`;
- activity spans named `activity.execute`;
- `elsa.workflow.started`, `elsa.workflow.completed`, and
  `elsa.workflow.faulted` counters;
- an `elsa.activity.duration` histogram.

The extension middleware is additive. It inserts its workflow and activity
components at position `0`, making them the outermost components in their
respective pipelines. Core instrumentation wraps the pipeline, so the
extension span is normally nested inside the corresponding core span when both
layers are active. If you only want the core telemetry, configure the
OpenTelemetry provider without adding the extension middleware calls.

## Workflow spans

The workflow middleware creates an `ActivityKind.Server` span named:

```text
execute workflow {workflow name}
```

It adds these release-defined tags when the values are available:

- `operation.name`: `elsa.workflow.execution`
- `span.type`: `workflow`
- `workflow.definition.id`, `workflow.definition.version`, and
  `workflow.definition.name`
- `workflow.definition.tenant.id` and `workflow.instance.id`
- `workflow.trigger.activity.id`, `.name`, `.type`, and `.version` when the
  workflow has a trigger activity
- `workflow.correlation_id` when a correlation ID is present

The span records `executing`, then one of `finished`, `faulted`, `canceled`, or
`suspended`. A faulted workflow records `faulted` and sets the span status to
`Error`; when an incident exists, the incident message is also used as the
status description.

Every incident is recorded as an `incident` event. The event includes the
incident message and, when available, exception message, stack trace, and type.
The same values are also written under indexed
`workflow.incidents.{index}.*` tags. Treat exception and incident data as
potentially sensitive when choosing an exporter and retention policy.

## Activity spans

The activity middleware creates an `ActivityKind.Internal` span named:

```text
execute activity {activity type}
```

It adds activity identity and workflow context tags, including:

- `operation.name`: `elsa.activity.execution`
- `activity.id`, `activity.node.id`, `activity.type`, `activity.name`, and
  `activity.version`
- `activity.instance.id` and `activity.tenant.id`
- `workflow.instance.id`, `workflow.definition.id`, and
  `workflow.definition.version`
- `workflow.correlation_id` when a correlation ID is present

Activities with the `Job` kind, or asynchronous `Task` activities, also get
`span.type=job`. The middleware records one of `executing`, `faulted`,
`canceled`, `completed`, or `pending` after the activity pipeline returns.
Faulted activity spans have status `Error`.

Custom activity extension metadata is copied to tags with the prefix
`activity.extensions.`. Keys are humanized, lower-cased, and separated with
underscores. Do not put secrets, tokens, or large payloads in activity
extension metadata merely to make them visible in traces.

## Remote parents and new trace roots

`OpenTelemetryFeature` exposes two options:

```csharp
.UseOpenTelemetry(otel =>
{
    otel.UseNewRootActivityForRemoteParent = true;
    otel.UseDummyParentActivityAsRootSpan = false;
})
```

The defaults are `true` and `false` respectively.

With `UseNewRootActivityForRemoteParent=true`, a workflow that starts under a
remote `Activity` parent stops and clears the current activity, creates a new
root workflow span, and links the remote parent context to it. This changes the
trace layout: the remote span is a link rather than the new workflow span's
parent. Set it to `false` to keep the normal current parent relationship.

`UseDummyParentActivityAsRootSpan` controls the parent context used when a new
root is requested. When enabled, the extension supplies a synthetic recorded
parent context; leave it disabled unless the tracing backend or middleware you
use requires that compatibility behavior.

The workflow context can also request a new trace with the internal
`StartNewTrace` property. Treat that as an advanced host/runtime integration
point rather than a workflow designer setting.

## Customize error span handling

The module registers scoped handlers for both workflow and activity faults. It
selects the first handler ordered by `Order` whose `CanHandle` method returns
`true`.

The built-in behavior is:

- `FaultExceptionErrorSpanHandler` runs first (`Order=0`). Activity spans get
  exception attributes for the fault code, category, and type. Workflow spans
  get `error.code`, `error.category`, and `error_details.type` tags.
- `DefaultErrorSpanHandler` is the fallback (`Order=100000`). It adds the
  activity exception to a faulted activity span. Its workflow handler does not
  claim a context, so workflow incidents without a matching custom handler
  retain the middleware's status and incident data.

Register a custom handler when your organization needs backend-specific tags or
redaction. Keep it narrow and avoid copying workflow input, output, or secrets
into span attributes:

```csharp
using Elsa.OpenTelemetry.Contracts;
using Elsa.OpenTelemetry.Models;

public sealed class RedactingActivityErrorSpanHandler : IActivityErrorSpanHandler
{
    public float Order => 10;

    public bool CanHandle(ActivityErrorSpanContext context) =>
        context.Exception is TimeoutException;

    public void Handle(ActivityErrorSpanContext context)
    {
        context.Span.SetTag("error.category", "timeout");
        context.Span.AddException(context.Exception!);
    }
}
```

Register the handler after `AddElsa` or from your Elsa module:

```csharp
builder.Services.AddScoped<IActivityErrorSpanHandler,
    RedactingActivityErrorSpanHandler>();
```

The handler is selected per execution scope. It does not replace the tracing
middleware, and it does not change the workflow or activity status itself.

## Studio and collector boundaries

There are three separate concerns:

1. `Elsa.OpenTelemetry` creates workflow/activity spans in the server process.
2. The standard OpenTelemetry tracer provider exports those spans to an OTLP
   backend or another exporter.
3. `Elsa.Diagnostics.OpenTelemetry` optionally ingests OTLP data and gives
   Elsa Studio a query/live-feed surface.

Installing `Elsa.OpenTelemetry` alone does not add an OpenTelemetry page to
Studio. Conversely, enabling the Studio diagnostics module does not create
workflow/activity spans; the server must emit them and send them to the
collector. Configure collector ingestion and permissions in the
[distributed tracing guide](../../operate/distributed-tracing.md).

## Troubleshooting

- **No extension spans:** verify both extension pipeline calls are present.
  `UseOpenTelemetry` alone is not enough for the additional middleware layer.
- **Only one span layer appears:** confirm whether you configured the core
  instrumentation and extension middleware intentionally; both layers use the
  same source but the extension calls add separate spans.
- **No exported spans:** verify the tracer provider includes `Elsa.Workflows`
  with `.AddSource("Elsa.Workflows")` and that an exporter is configured.
- **Workflow spans appear but activity spans do not:** check that the activity
  pipeline being used is the default pipeline or explicitly includes
  `UseActivityExecutionTracing()`.
- **Studio shows no traces:** verify that the collector is enabled, the host
  sends OTLP data to its ingestion endpoint, and the operator has the required
  diagnostics permission.
- **Unexpected parent/trace layout:** inspect `UseNewRootActivityForRemoteParent`,
  `UseDummyParentActivityAsRootSpan`, and whether the workflow context contains
  `StartNewTrace`.
- **Sensitive data appears in traces:** remove it from custom activity
  extension metadata and custom error handlers, then review exporter retention
  and redaction settings.

## Release source

The behavior described here is implemented in the `release/3.8.0` source:

- [Core `WorkflowInstrumentation`](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/modules/Elsa.Workflows.Core/Telemetry/WorkflowInstrumentation.cs)
- [Core workflow instrumentation call site](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/modules/Elsa.Workflows.Core/Services/WorkflowRunner.cs)
- [Core activity instrumentation call site](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/modules/Elsa.Workflows.Core/Services/ActivityInvoker.cs)
- [`Elsa.OpenTelemetry` module and options](https://github.com/elsa-workflows/elsa-extensions/tree/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/diagnostics/Elsa.OpenTelemetry)
- [`UseOpenTelemetry`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/diagnostics/Elsa.OpenTelemetry/Extensions/ModuleExtensions.cs)
- [Workflow tracing middleware](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/diagnostics/Elsa.OpenTelemetry/Middleware/OpenTelemetryTracingWorkflowExecutionMiddleware.cs)
- [Activity tracing middleware](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/diagnostics/Elsa.OpenTelemetry/Middleware/OpenTelemetryTracingActivityExecutionMiddleware.cs)
- [Error handler contracts and built-ins](https://github.com/elsa-workflows/elsa-extensions/tree/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/diagnostics/Elsa.OpenTelemetry/Contracts)
- [Studio OpenTelemetry diagnostics module](https://github.com/elsa-workflows/elsa-studio/blob/935edd5cef34ea189376a72408dfe708f62bbfe5/src/modules/Elsa.Studio.Diagnostics.OpenTelemetry/Extensions/ServiceCollectionExtensions.cs)
