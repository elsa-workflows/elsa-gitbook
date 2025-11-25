# References - Monitoring & Observability Guide

This document lists the source file references used in the Monitoring & Observability guide.

## elsa-core Repository

Base URL: `https://github.com/elsa-workflows/elsa-core`

| Component | Path |
|-----------|------|
| IWorkflowRunner | `src/modules/Elsa.Workflows.Runtime/Contracts/IWorkflowRunner.cs` |
| WorkflowStatus | `src/modules/Elsa.Workflows.Core/Enums/WorkflowStatus.cs` |
| WorkflowExecutionContext | `src/modules/Elsa.Workflows.Core/Contexts/WorkflowExecutionContext.cs` |
| ActivityExecutionContext | `src/modules/Elsa.Workflows.Core/Contexts/ActivityExecutionContext.cs` |
| Bookmark | `src/modules/Elsa.Workflows.Core/Models/Bookmark.cs` |

## elsa-extensions Repository

Base URL: `https://github.com/elsa-workflows/elsa-extensions`

### Elsa.OpenTelemetry Extension

| Component | Path |
|-----------|------|
| OpenTelemetryHelpers | `src/modules/diagnostics/Elsa.OpenTelemetry/Helpers/OpenTelemetryHelpers.cs` |
| OpenTelemetryTracingWorkflowExecutionMiddleware | `src/modules/diagnostics/Elsa.OpenTelemetry/Middleware/OpenTelemetryTracingWorkflowExecutionMiddleware.cs` |
| OpenTelemetryTracingActivityExecutionMiddleware | `src/modules/diagnostics/Elsa.OpenTelemetry/Middleware/OpenTelemetryTracingActivityExecutionMiddleware.cs` |
| OpenTelemetryFeature | `src/modules/diagnostics/Elsa.OpenTelemetry/Features/OpenTelemetryFeature.cs` |
| ModuleExtensions | `src/modules/diagnostics/Elsa.OpenTelemetry/Extensions/ModuleExtensions.cs` |

## External Documentation

| Topic | URL |
|-------|-----|
| OpenTelemetry .NET SDK | https://opentelemetry.io/docs/instrumentation/net/ |
| Prometheus .NET Client | https://github.com/prometheus-net/prometheus-net |
| Grafana Documentation | https://grafana.com/docs/ |
| Datadog .NET Tracer | https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core/ |
| Datadog OTLP Ingest | https://docs.datadoghq.com/tracing/trace_collection/open_standards/otlp_ingest_in_the_agent/ |

## Key Concepts from Source

### ActivitySource Name

The `Elsa.Workflows` ActivitySource is defined in `OpenTelemetryHelpers.cs` and used by the tracing middleware to create spans.

### Middleware Pipeline

The OpenTelemetry middleware integrates into Elsa's workflow and activity execution pipelines:

1. `OpenTelemetryTracingWorkflowExecutionMiddleware` - Wraps workflow execution in a span
2. `OpenTelemetryTracingActivityExecutionMiddleware` - Wraps each activity execution in a span

### Feature Registration

The `OpenTelemetryFeature` class registers the tracing services. It is enabled via the `UseOpenTelemetry()` extension method from `ModuleExtensions`.

## Diagram Placeholders

The following diagrams would enhance this documentation:

1. **Observability Architecture Diagram**
   - Shows Elsa application, OpenTelemetry SDK, and flow to observability backends
   - *[Placeholder: observability-architecture.png]*

2. **Trace Hierarchy Diagram**
   - Shows parent-child relationship of workflow and activity spans
   - *[Placeholder: trace-hierarchy.png]*

3. **Metrics Flow Diagram**
   - Shows custom metrics instrumentation and export path
   - *[Placeholder: metrics-flow.png]*

4. **Dashboard Screenshot**
   - Example Grafana dashboard with key panels
   - *[Placeholder: grafana-dashboard-example.png]*
