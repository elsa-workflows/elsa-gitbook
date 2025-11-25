# Datadog Integration Notes for Elsa Workflows

This guide covers integrating Elsa Workflows with Datadog for observability.

## Overview

Datadog can receive:
- **Distributed traces** via OTLP or Datadog APM libraries
- **Metrics** via DogStatsD or OTLP
- **Logs** via Datadog Agent or direct API

## Tracing with Elsa.OpenTelemetry

The distributed tracing middleware originates from the **Elsa.OpenTelemetry** extension in [elsa-extensions](https://github.com/elsa-workflows/elsa-extensions). This provides automatic span creation for workflow and activity execution.

### Configuration

1. Enable tracing in Elsa:

```csharp
services.AddElsa(elsa =>
{
    elsa.UseWorkflows(workflows =>
    {
        workflows.UseOpenTelemetry(); // From Elsa.OpenTelemetry extension
    });
});
```

2. Configure OTLP export to Datadog Agent:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("Elsa.Workflows")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
            });
    });
```

3. Ensure the Datadog Agent is configured to receive OTLP:

```yaml
# datadog.yaml
otlp_config:
  receiver:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318
```

## Metrics

> **Important**: Metrics must be user-defined or collected via generic .NET runtime instrumentation. Elsa does not provide built-in workflow metrics.

### Option 1: Custom Metrics via OTLP

If you implement custom metrics using `System.Diagnostics.Metrics` (as described in the [main Monitoring guide](../README.md#implementing-custom-metrics)), export them via OTLP:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("YourApp.Elsa.Workflows") // Your custom meter
            .AddRuntimeInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
            });
    });
```

### Option 2: DogStatsD

Alternatively, send metrics directly to DogStatsD:

```csharp
// Using StatsdClient
var dogstatsdConfig = new StatsdConfig
{
    StatsdServerName = "127.0.0.1",
    StatsdPort = 8125,
    Prefix = "elsa"
};

using var service = new DogStatsdService();
service.Configure(dogstatsdConfig);

// Then in your instrumentation code:
service.Increment("workflows.started", tags: new[] { "workflow:OrderProcessing" });
service.Histogram("workflow.duration", durationMs, tags: new[] { "workflow:OrderProcessing" });
```

### Option 3: Generic Runtime Instrumentation

Datadog's .NET tracer provides automatic runtime metrics:
- GC metrics
- Thread pool metrics
- Memory usage

Enable via environment variables:

```bash
DD_RUNTIME_METRICS_ENABLED=true
```

## Auto-Instrumentation Environment Variables

When using the Datadog .NET tracer for auto-instrumentation:

```bash
# Enable Datadog APM
DD_TRACE_ENABLED=true
DD_AGENT_HOST=localhost
DD_TRACE_AGENT_PORT=8126

# Service identification
DD_SERVICE=my-elsa-app
DD_ENV=production
DD_VERSION=1.0.0

# Enable runtime metrics
DD_RUNTIME_METRICS_ENABLED=true

# Enable logs injection for correlation
DD_LOGS_INJECTION=true
```

## Adding Custom Spans

If you need additional spans beyond what the Elsa.OpenTelemetry middleware provides, you can hook into the `Elsa.Workflows` ActivitySource:

```csharp
using System.Diagnostics;

public class MyCustomService
{
    private static readonly ActivitySource ActivitySource = new("Elsa.Workflows");

    public async Task DoWorkAsync()
    {
        using var activity = ActivitySource.StartActivity("MyCustomOperation");
        activity?.SetTag("custom.attribute", "value");
        
        // Your work here
    }
}
```

Or create your own ActivitySource and add it to the tracer:

```csharp
private static readonly ActivitySource MyActivitySource = new("MyApp.CustomOperations");

// In configuration:
.WithTracing(tracing =>
{
    tracing
        .AddSource("Elsa.Workflows")
        .AddSource("MyApp.CustomOperations") // Add your custom source
        .AddOtlpExporter();
});
```

## Log Correlation

To correlate logs with traces in Datadog:

1. Enable logs injection:
```bash
DD_LOGS_INJECTION=true
```

2. Configure structured logging with trace context:

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("dd.trace_id", Activity.Current?.TraceId.ToString())
    .Enrich.WithProperty("dd.span_id", Activity.Current?.SpanId.ToString())
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();
```

3. Forward logs to Datadog via Agent or direct API.

## Service Map

Once traces are flowing, Datadog automatically builds a service map showing:
- Elsa workflow services
- Downstream HTTP dependencies
- Database calls
- Message queue interactions

## Dashboards

Create custom Datadog dashboards using:
- Trace metrics (if using auto-instrumentation)
- Custom metrics (if you implemented them)
- Runtime metrics
- Log metrics

Example widget for workflow throughput (requires custom metrics):

```json
{
  "title": "Workflow Throughput",
  "type": "timeseries",
  "requests": [
    {
      "q": "sum:elsa.workflows.started.count{*}.as_rate()",
      "display_type": "line"
    }
  ]
}
```

## Related

- [Main Monitoring Guide](../README.md)
- [OpenTelemetry Setup](otel-setup.md)
- [Datadog OTLP Documentation](https://docs.datadoghq.com/tracing/trace_collection/open_standards/otlp_ingest_in_the_agent/)
