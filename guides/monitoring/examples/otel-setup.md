# OpenTelemetry Setup for Elsa Workflows

This guide covers enabling OpenTelemetry tracing and configuring custom metrics for Elsa Workflows.

## Enabling Tracing via Elsa.OpenTelemetry

The **Elsa.OpenTelemetry** extension provides distributed tracing support. Enable it using the `.UseOpenTelemetry()` extension method:

```csharp
using Elsa.Extensions;
using Elsa.OpenTelemetry.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflows(workflows =>
    {
        // Enable distributed tracing
        workflows.UseOpenTelemetry();
    });
    
    // Other Elsa configuration...
});
```

## Configuring OpenTelemetry SDK

Add the OpenTelemetry SDK packages and configure tracing:

```bash
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore
```

### Basic Configuration

```csharp
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "MyElsaApp",
            serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            // Add the Elsa.Workflows ActivitySource
            .AddSource("Elsa.Workflows")
            // Add ASP.NET Core instrumentation
            .AddAspNetCoreInstrumentation()
            // Add HTTP client instrumentation  
            .AddHttpClientInstrumentation()
            // Export to OTLP (works with Jaeger, Datadog, etc.)
            .AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            // Add your custom meter (metrics are user-defined, not built into Elsa)
            .AddMeter("YourApp.Elsa.Workflows")
            // Add runtime metrics
            .AddRuntimeInstrumentation()
            // Export to Prometheus
            .AddPrometheusExporter();
    });
```

## Important Notes on Metrics

> **Metrics are user-defined**: Elsa does not provide built-in meters or metrics instruments. All workflow metrics (counters, histograms, gauges) must be implemented by you using the patterns described in the [main Monitoring guide](../README.md#implementing-custom-metrics).

The `.AddMeter("YourApp.Elsa.Workflows")` line registers your custom meter—you must create and populate this meter in your application code.

## Exporter Configuration

For detailed exporter configuration (OTLP endpoints, authentication, batching, sampling), refer to the official OpenTelemetry documentation:

- [OpenTelemetry .NET SDK](https://opentelemetry.io/docs/instrumentation/net/)
- [OTLP Exporter Configuration](https://opentelemetry.io/docs/specs/otel/protocol/exporter/)
- [Prometheus Exporter](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.Prometheus.AspNetCore)

### Example: OTLP to Jaeger

```csharp
.AddOtlpExporter(options =>
{
    options.Endpoint = new Uri("http://jaeger:4317");
    options.Protocol = OtlpExportProtocol.Grpc;
})
```

### Example: OTLP to Datadog Agent

```csharp
.AddOtlpExporter(options =>
{
    options.Endpoint = new Uri("http://localhost:4317");
})
```

Ensure the Datadog Agent is configured to receive OTLP traces.

## Environment Variable Configuration

OpenTelemetry SDK respects standard environment variables:

```bash
# OTLP endpoint
OTEL_EXPORTER_OTLP_ENDPOINT=http://collector:4317

# Service name
OTEL_SERVICE_NAME=my-elsa-app

# Trace sampling (always_on, always_off, traceidratio)
OTEL_TRACES_SAMPLER=always_on

# Resource attributes
OTEL_RESOURCE_ATTRIBUTES=deployment.environment=production,service.version=1.0.0
```

## Exposing Prometheus Metrics Endpoint

If using the Prometheus exporter:

```csharp
var app = builder.Build();

// Map the Prometheus scrape endpoint
app.MapPrometheusScrapingEndpoint();

app.Run();
```

This exposes metrics at `/metrics` for Prometheus to scrape.

## Next Steps

- [Prometheus Metrics Examples](prometheus-metrics.md)
- [Grafana Dashboard Setup](grafana-dashboard.md)
- [Datadog Integration Notes](datadog-notes.md)
