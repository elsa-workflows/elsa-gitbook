# Monitoring & Observability Guide

## Executive Summary

This guide covers observability for Elsa Workflows with a focus on Elsa-specific considerations. It explains what the **Elsa.OpenTelemetry** extension provides (distributed tracing), what you must implement yourself (custom metrics), and how to integrate with common monitoring platforms.

> **Note**: For advanced OpenTelemetry platform configuration, exporters, and collector setup, please refer to the [official OpenTelemetry documentation](https://opentelemetry.io/docs/).

---

## What the Elsa.OpenTelemetry Extension Provides

The **Elsa.OpenTelemetry** extension from [elsa-extensions](https://github.com/elsa-workflows/elsa-extensions) provides **distributed tracing only**. It does **not** include built-in metrics.

### Built-in Tracing Capabilities

| Component | Description | Source File |
|-----------|-------------|-------------|
| `ActivitySource` | Named `"Elsa.Workflows"` for creating spans | `OpenTelemetryHelpers.cs` |
| Workflow Execution Middleware | Creates spans for workflow execution | `OpenTelemetryTracingWorkflowExecutionMiddleware.cs` |
| Activity Execution Middleware | Creates spans for individual activity execution | `OpenTelemetryTracingActivityExecutionMiddleware.cs` |
| OpenTelemetry Feature | Registers tracing services | `OpenTelemetryFeature.cs` |
| Module Extensions | Extension method `.UseOpenTelemetry()` | `ModuleExtensions.cs` |

### Enabling Tracing

```csharp
using Elsa.Extensions;
using Elsa.OpenTelemetry.Extensions;

services.AddElsa(elsa =>
{
    elsa.UseWorkflows(workflows =>
    {
        workflows.UseOpenTelemetry(); // Enable distributed tracing
    });
});
```

### Span Tags Set by Middleware

The tracing middleware automatically adds these tags to spans:

- `workflow.definition.id` - The workflow definition identifier
- `workflow.instance.id` - The workflow instance identifier  
- `workflow.correlation.id` - The correlation ID (if set)
- `activity.type` - The activity type name
- `activity.id` - The activity identifier
- `activity.name` - The activity display name

---

## What You Must Add Yourself

### Metrics (No Built-in Meters)

Elsa does **not** provide built-in metrics. All metrics described in this guide are **recommended custom instrumentation** that you must implement yourself.

This includes:
- Workflow execution counters
- Duration histograms
- Active instance gauges
- Bookmark tracking metrics
- Error rate counters

---

## Implementing Custom Metrics

### Step 1: Create a Static Meter Class

Create a centralized class to define your metrics instruments:

```csharp
using System.Diagnostics.Metrics;

namespace YourApp.Observability;

/// <summary>
/// Custom metrics for Elsa workflow monitoring.
/// These metrics are user-implemented and not built into Elsa Core.
/// </summary>
public static class ElsaMetrics
{
    public static readonly string MeterName = "YourApp.Elsa.Workflows";
    
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    // Counters
    public static readonly Counter<long> WorkflowsStarted = 
        Meter.CreateCounter<long>(
            "elsa_workflows_started_total",
            description: "Total number of workflows started");

    public static readonly Counter<long> WorkflowsCompleted = 
        Meter.CreateCounter<long>(
            "elsa_workflows_completed_total",
            description: "Total number of workflows completed");

    public static readonly Counter<long> WorkflowsFaulted = 
        Meter.CreateCounter<long>(
            "elsa_workflows_faulted_total",
            description: "Total number of workflows that faulted");

    public static readonly Counter<long> ActivitiesExecuted = 
        Meter.CreateCounter<long>(
            "elsa_activities_executed_total",
            description: "Total number of activities executed");

    public static readonly Counter<long> BookmarksCreated = 
        Meter.CreateCounter<long>(
            "elsa_bookmarks_created_total",
            description: "Total bookmarks created");

    public static readonly Counter<long> BookmarksResumed = 
        Meter.CreateCounter<long>(
            "elsa_bookmarks_resumed_total",
            description: "Total bookmarks resumed");

    // Histograms
    public static readonly Histogram<double> WorkflowDuration = 
        Meter.CreateHistogram<double>(
            "elsa_workflow_duration_seconds",
            unit: "s",
            description: "Duration of workflow executions in seconds");

    public static readonly Histogram<double> ActivityDuration = 
        Meter.CreateHistogram<double>(
            "elsa_activity_duration_seconds",
            unit: "s",
            description: "Duration of activity executions in seconds");

    // Observable Gauge (for active instances)
    // Note: You must provide a callback that returns the current value
    public static ObservableGauge<int> CreateActiveWorkflowsGauge(
        Func<int> observeValue)
    {
        return Meter.CreateObservableGauge(
            "elsa_workflows_active",
            observeValue,
            description: "Number of currently active workflow instances");
    }
}
```

### Step 2: Create an Instrumentation Decorator for IWorkflowRunner

Use the decorator pattern to wrap `IWorkflowRunner` and record metrics:

```csharp
using System.Diagnostics;
using Elsa.Workflows;
using Elsa.Workflows.Runtime.Contracts;
using Elsa.Workflows.Runtime.Options;
using Elsa.Workflows.Runtime.Results;
using YourApp.Observability;

namespace YourApp.Instrumentation;

/// <summary>
/// Decorator that adds custom metrics instrumentation to IWorkflowRunner.
/// </summary>
public class InstrumentedWorkflowRunner : IWorkflowRunner
{
    private readonly IWorkflowRunner _inner;

    public InstrumentedWorkflowRunner(IWorkflowRunner inner)
    {
        _inner = inner;
    }

    public async Task<WorkflowResult> RunAsync(
        RunWorkflowOptions options,
        CancellationToken cancellationToken = default)
    {
        var workflowName = options.WorkflowDefinitionHandle?.DefinitionId ?? "unknown";
        var tags = new TagList
        {
            { "workflow.name", workflowName }
        };

        ElsaMetrics.WorkflowsStarted.Add(1, tags);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _inner.RunAsync(options, cancellationToken);
            
            stopwatch.Stop();
            ElsaMetrics.WorkflowDuration.Record(
                stopwatch.Elapsed.TotalSeconds, 
                tags);

            if (result.WorkflowState.Status == WorkflowStatus.Finished)
            {
                ElsaMetrics.WorkflowsCompleted.Add(1, tags);
            }

            return result;
        }
        catch (Exception)
        {
            ElsaMetrics.WorkflowsFaulted.Add(1, tags);
            throw;
        }
    }
}
```

### Step 3: Register the Decorator

Register your instrumented wrapper in dependency injection:

```csharp
// In Program.cs or Startup.cs
services.Decorate<IWorkflowRunner, InstrumentedWorkflowRunner>();
```

> **Note**: You may need the `Scrutor` package for the `Decorate` extension method, or implement manual decoration.

### Step 4: Configure OpenTelemetry to Export Metrics

```csharp
using OpenTelemetry.Metrics;

services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(ElsaMetrics.MeterName) // Your custom meter
            .AddPrometheusExporter(); // Or other exporter
    });
```

### Optional: Observable Gauge for Active Workflows

If you track active workflow instances (e.g., in a service), you can create an observable gauge:

```csharp
public class WorkflowTrackingService
{
    private int _activeCount;
    private readonly ObservableGauge<int> _gauge;

    public WorkflowTrackingService()
    {
        _gauge = ElsaMetrics.CreateActiveWorkflowsGauge(() => _activeCount);
    }

    public void IncrementActive() => Interlocked.Increment(ref _activeCount);
    public void DecrementActive() => Interlocked.Decrement(ref _activeCount);
}
```

---

## Suggested Metric Catalog (User-Implemented)

The following metrics are **recommended patterns** that you should implement using the approach above. **None of these are built into Elsa Core.**

| Metric Name | Type | Description | Labels |
|-------------|------|-------------|--------|
| `elsa_workflows_started_total` | Counter | Total workflows started | `workflow.name` |
| `elsa_workflows_completed_total` | Counter | Total workflows completed successfully | `workflow.name` |
| `elsa_workflows_faulted_total` | Counter | Total workflows that faulted | `workflow.name`, `error.type` |
| `elsa_workflow_duration_seconds` | Histogram | Workflow execution duration | `workflow.name` |
| `elsa_workflows_active` | Gauge | Currently running workflow instances | `workflow.name` |
| `elsa_activities_executed_total` | Counter | Total activities executed | `activity.type`, `workflow.name` |
| `elsa_activity_duration_seconds` | Histogram | Activity execution duration | `activity.type` |
| `elsa_bookmarks_created_total` | Counter | Total bookmarks created | `activity.type` |
| `elsa_bookmarks_resumed_total` | Counter | Total bookmarks resumed | `activity.type` |
| `elsa_bookmarks_pending` | Gauge | Pending bookmarks awaiting resumption | `activity.type` |

---

## Prometheus & Grafana Integration

See [Prometheus Metrics Setup](examples/prometheus-metrics.md) for detailed configuration.

See [Grafana Dashboard Examples](examples/grafana-dashboard.md) for sample dashboard panels.

### Example Alert Rules (Using User-Defined Metrics)

```yaml
# prometheus-alerts.yml
groups:
  - name: elsa-workflow-alerts
    rules:
      - alert: HighWorkflowFaultRate
        expr: |
          rate(elsa_workflows_faulted_total[5m]) 
          / rate(elsa_workflows_started_total[5m]) > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High workflow fault rate detected"
          description: "More than 10% of workflows are faulting"

      - alert: SlowWorkflowExecution
        expr: |
          histogram_quantile(0.95, 
            rate(elsa_workflow_duration_seconds_bucket[5m])) > 30
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Slow workflow execution detected"
          description: "95th percentile workflow duration exceeds 30 seconds"
```

---

## Datadog Integration

See [Datadog Notes](examples/datadog-notes.md) for Datadog-specific configuration.

Key points:
- Tracing middleware comes from **Elsa.OpenTelemetry** extension
- Metrics must be user-defined or collected via generic .NET runtime instrumentation
- Use OTLP exporter to send traces to Datadog Agent

---

## Logging & Correlation

Elsa supports structured logging with correlation fields. Configure structured logging to include:

| Field | Description |
|-------|-------------|
| `WorkflowInstanceId` | Unique identifier for the workflow instance |
| `WorkflowDefinitionId` | The workflow definition being executed |
| `CorrelationId` | User-supplied correlation ID for request tracing |
| `BookmarkId` | Identifier for suspended bookmarks |
| `ActivityId` | The current activity being executed |

### Example: Enriching Logs with Serilog

```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] " +
        "{Message:lj} " +
        "{Properties:j}{NewLine}{Exception}")
    .CreateLogger();
```

When using `ILogger` within workflow activities, Elsa automatically populates log context with workflow and activity identifiers.

---

## Verification Checklist

Before going to production, verify your observability setup:

### Single Node

- [ ] Traces appear in your tracing backend (Jaeger, Zipkin, etc.)
- [ ] Custom metrics show up after implementing instrumentation code
- [ ] Logs contain structured fields (WorkflowInstanceId, CorrelationId)
- [ ] Prometheus/Grafana dashboards display data (if configured)

### Cluster Scenario

- [ ] Traces are properly correlated across nodes
- [ ] Metrics aggregate correctly from all instances
- [ ] Lock timeouts are tracked (if you implemented custom instrumentation)
- [ ] Bookmark resumption works across nodes with proper trace propagation

---

## Troubleshooting

| Symptom | Possible Cause | Solution |
|---------|---------------|----------|
| Bookmark not resumed | Bookmark expired or deleted; wrong correlation ID | Check bookmark store; verify correlation ID matches |
| Duplicate resume attempts | No distributed lock or race condition | Implement distributed locking; check cluster configuration |
| Missing spans in traces | OpenTelemetry not configured; wrong ActivitySource | Verify `.UseOpenTelemetry()` is called; check `Elsa.Workflows` source is added to tracer |
| Metrics absent | Custom instrumentation not implemented; meter not registered | Implement metrics as shown above; add meter to OpenTelemetry configuration |
| Traces not correlated | Missing trace context propagation | Ensure HTTP headers propagate trace context; check W3C TraceContext setup |
| High memory in tracing | Too many spans or attributes | Reduce span granularity; filter activities; sample traces |

---

## Architecture Diagram

<!-- Placeholder for architecture diagram showing:
     - Elsa Application with OpenTelemetry SDK
     - Trace flow to collector/backend
     - Custom metrics flow to Prometheus
     - Grafana visualization
-->

*[Diagram placeholder: Observability architecture]*

---

## Related Documentation

- [OpenTelemetry Setup](examples/otel-setup.md)
- [Prometheus Metrics](examples/prometheus-metrics.md)
- [Grafana Dashboard](examples/grafana-dashboard.md)
- [Datadog Notes](examples/datadog-notes.md)
- [References](README-REFERENCES.md)
