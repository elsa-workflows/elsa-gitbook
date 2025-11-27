# OpenTelemetry Setup for Elsa Workflows

This guide provides actionable steps to enable OpenTelemetry (OTel) instrumentation in an ASP.NET Core application hosting Elsa Workflows.

## Prerequisites

- Elsa Server application (.NET 8.0 or later)
- Access to an OTLP collector endpoint (e.g., Jaeger, Grafana Tempo, Datadog)
- Basic understanding of ASP.NET Core dependency injection

## Install Required Packages

Add the following NuGet packages to your Elsa Server project:

```bash
# Core OpenTelemetry SDK
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Extensions.Hosting

# Instrumentation libraries
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.Runtime

# Optional: Database instrumentation
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore
dotnet add package OpenTelemetry.Instrumentation.SqlClient

# Exporters
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol  # OTLP
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore  # Prometheus

# Optional: Console exporter for debugging
dotnet add package OpenTelemetry.Exporter.Console
```

## Configure OpenTelemetry in Program.cs

### Reference: Elsa Server Program.cs

The Elsa Server entrypoint where you register services:
- **Path**: `src/apps/Elsa.Server.Web/Program.cs` (in elsa-core repository)
- **GitHub**: [Elsa.Server.Web/Program.cs](https://github.com/elsa-workflows/elsa-core/blob/main/src/apps/Elsa.Server.Web/Program.cs)

This file shows the typical service registration pattern used in Elsa Server. Add OpenTelemetry configuration after Elsa services are registered.

### Basic Configuration

Add the following configuration to your `Program.cs`:

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configure service resource attributes
var serviceName = builder.Configuration["ServiceName"] ?? "elsa-server";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
var environment = builder.Environment.EnvironmentName;

// Register Elsa services (existing Elsa configuration)
builder.Services.AddElsa(elsa =>
{
    // Your existing Elsa configuration
    // ...
});

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: serviceName,
            serviceVersion: serviceVersion,
            serviceInstanceId: Environment.MachineName)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = environment,
            ["host.name"] = Environment.MachineName
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = (httpContext) =>
            {
                // Don't trace health check endpoints
                return !httpContext.Request.Path.StartsWithSegments("/health");
            };
        })
        .AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
        })
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.SetDbStatementForStoredProcedure = true;
        })
        .AddSqlClientInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.RecordException = true;
        })
        .AddSource("Elsa.*") // Capture Elsa activity sources if available
        .AddOtlpExporter(otlpOptions =>
        {
            otlpOptions.Endpoint = new Uri(
                builder.Configuration["OpenTelemetry:OtlpEndpoint"] 
                ?? "http://localhost:4317");
        })
        .AddConsoleExporter()) // Optional: for debugging
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddMeter("Elsa.*") // Capture Elsa meters if available
        .AddOtlpExporter(otlpOptions =>
        {
            otlpOptions.Endpoint = new Uri(
                builder.Configuration["OpenTelemetry:OtlpEndpoint"] 
                ?? "http://localhost:4317");
        })
        .AddPrometheusExporter()); // Exposes /metrics endpoint

var app = builder.Build();

// Configure Elsa middleware (existing Elsa configuration)
app.UseElsa();

// Map Prometheus metrics endpoint
app.MapPrometheusScrapingEndpoint(); // Exposes at /metrics

app.Run();
```

### Configuration via appsettings.json

Add OpenTelemetry configuration to `appsettings.json`:

```json
{
  "ServiceName": "elsa-server",
  "OpenTelemetry": {
    "OtlpEndpoint": "http://otel-collector:4317",
    "TracingSampleRate": 1.0,
    "MetricsExportIntervalMilliseconds": 30000
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "OpenTelemetry": "Warning"
    }
  }
}
```

### Environment Variable Configuration

OpenTelemetry supports configuration via environment variables following the [OTel specification](https://opentelemetry.io/docs/specs/otel/configuration/sdk-environment-variables/):

```bash
# Service identification
OTEL_SERVICE_NAME=elsa-server
OTEL_RESOURCE_ATTRIBUTES=service.version=3.0,deployment.environment=production

# OTLP Exporter
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4318
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf  # or grpc
OTEL_EXPORTER_OTLP_HEADERS=authorization=Bearer YOUR_TOKEN

# Trace configuration
OTEL_TRACES_EXPORTER=otlp
OTEL_TRACES_SAMPLER=traceidratio
OTEL_TRACES_SAMPLER_ARG=0.1  # Sample 10% of traces

# Metrics configuration
OTEL_METRICS_EXPORTER=otlp,prometheus
OTEL_METRIC_EXPORT_INTERVAL=30000  # 30 seconds

# Log configuration (if using OTLP logs)
OTEL_LOGS_EXPORTER=otlp
```

**Note**: Environment variables take precedence over `appsettings.json` configuration.

## Reference: Docker Auto-Instrumentation

For containerized deployments, see the Datadog Dockerfile example:
- **Path**: `docker/ElsaServer-Datadog.Dockerfile` (in elsa-core repository)
- **GitHub**: [ElsaServer-Datadog.Dockerfile](https://github.com/elsa-workflows/elsa-core/blob/main/docker/ElsaServer-Datadog.Dockerfile)

This Dockerfile demonstrates:
- Setting OTel environment variables in container runtime
- Auto-instrumentation without code changes (language-specific)
- Integration with Datadog APM agent

Example Dockerfile snippet (adapted from reference):

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

# Set OpenTelemetry environment variables
ENV OTEL_SERVICE_NAME=elsa-server
ENV OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318
ENV OTEL_TRACES_SAMPLER=always_on
ENV OTEL_METRICS_EXPORTER=otlp

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ElsaServer.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ElsaServer.dll"]
```

## OTLP Endpoint Configuration

### Local Development with Jaeger

Run Jaeger all-in-one for local testing:

```bash
docker run -d --name jaeger \
  -e COLLECTOR_OTLP_ENABLED=true \
  -p 16686:16686 \
  -p 4317:4317 \
  -p 4318:4318 \
  jaegertracing/all-in-one:latest
```

Set endpoint:
```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318
```

Access Jaeger UI at: http://localhost:16686

### Grafana Tempo

For Grafana Tempo backend:

```bash
# Tempo OTLP endpoint
OTEL_EXPORTER_OTLP_ENDPOINT=http://tempo:4318
```

### OpenTelemetry Collector

Deploy an OTel Collector as aggregation layer:

```yaml
# otel-collector-config.yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

processors:
  batch:
    timeout: 10s
  
exporters:
  otlp:
    endpoint: tempo:4317
  prometheus:
    endpoint: 0.0.0.0:8889

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [batch]
      exporters: [otlp]
    metrics:
      receivers: [otlp]
      processors: [batch]
      exporters: [prometheus]
```

Run collector:
```bash
docker run -d --name otel-collector \
  -p 4317:4317 \
  -p 4318:4318 \
  -p 8889:8889 \
  -v $(pwd)/otel-collector-config.yaml:/etc/otel-collector-config.yaml \
  otel/opentelemetry-collector:latest \
  --config=/etc/otel-collector-config.yaml
```

## Advanced Configuration

### Trace Sampling

For production, sample a percentage of traces to reduce overhead:

```csharp
.WithTracing(tracing => tracing
    .SetSampler(new TraceIdRatioBasedSampler(0.1)) // Sample 10%
    // ... other configuration
)
```

Or via environment variable:
```bash
OTEL_TRACES_SAMPLER=traceidratio
OTEL_TRACES_SAMPLER_ARG=0.1
```

### Custom Instrumentation

Add custom spans and metrics for Elsa-specific operations:

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

// Define activity source for custom spans
public class ElsaInstrumentation
{
    public static readonly ActivitySource ActivitySource = 
        new ActivitySource("Elsa.Workflows");
    
    public static readonly Meter Meter = 
        new Meter("Elsa.Workflows");
    
    public static readonly Counter<long> WorkflowsExecuted = 
        Meter.CreateCounter<long>("elsa.workflows.executed");
    
    public static readonly Histogram<double> WorkflowDuration = 
        Meter.CreateHistogram<double>("elsa.workflow.duration");
}

// Use in workflow execution code
using (var activity = ElsaInstrumentation.ActivitySource.StartActivity("ExecuteWorkflow"))
{
    activity?.SetTag("workflow.definition.id", workflowDefinitionId);
    activity?.SetTag("workflow.instance.id", workflowInstanceId);
    
    var stopwatch = Stopwatch.StartNew();
    try
    {
        // Execute workflow
        await ExecuteWorkflowAsync();
        
        ElsaInstrumentation.WorkflowsExecuted.Add(1, 
            new KeyValuePair<string, object>("status", "completed"));
        
        activity?.SetStatus(ActivityStatusCode.Ok);
    }
    catch (Exception ex)
    {
        ElsaInstrumentation.WorkflowsExecuted.Add(1, 
            new KeyValuePair<string, object>("status", "faulted"));
        
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        throw;
    }
    finally
    {
        stopwatch.Stop();
        ElsaInstrumentation.WorkflowDuration.Record(
            stopwatch.Elapsed.TotalSeconds,
            new KeyValuePair<string, object>("workflow.definition.id", workflowDefinitionId));
    }
}
```

Register custom sources in `Program.cs`:

```csharp
.WithTracing(tracing => tracing
    .AddSource("Elsa.Workflows")
    // ... other configuration
)
.WithMetrics(metrics => metrics
    .AddMeter("Elsa.Workflows")
    // ... other configuration
)
```

## Verification

After configuration, verify OpenTelemetry is working:

1. **Check Metrics Endpoint**:
   ```bash
   curl http://localhost:5000/metrics
   ```
   Should return Prometheus format metrics.

2. **Generate Test Traffic**:
   Execute some workflows to generate traces and metrics.

3. **Verify Traces**:
   - Open Jaeger UI (http://localhost:16686) or your tracing backend
   - Search for service: "elsa-server"
   - Verify traces appear with correct spans

4. **Check Application Logs**:
   Look for OpenTelemetry initialization logs:
   ```
   [Information] OpenTelemetry.ResourceBuilder: Resource attributes: service.name=elsa-server
   [Information] OpenTelemetry.Trace.TracerProviderSdk: TracerProvider created
   ```

## Troubleshooting

### No Traces Appearing

**Check**:
1. Verify OTLP endpoint is reachable: `curl http://otel-collector:4318/v1/traces`
2. Check sampler is enabled: Set `OTEL_TRACES_SAMPLER=always_on` for testing
3. Review application logs for OTel errors
4. Verify network connectivity to collector

### Metrics Not Exposed

**Check**:
1. Ensure `AddPrometheusExporter()` is called
2. Verify `MapPrometheusScrapingEndpoint()` is added
3. Check firewall rules allow access to metrics port
4. Try accessing: `http://localhost:5000/metrics`

### High Overhead

**Solutions**:
1. Reduce trace sampling rate: `OTEL_TRACES_SAMPLER_ARG=0.01` (1%)
2. Disable SQL statement recording in production
3. Filter out health check endpoints
4. Use batch processor with larger timeout

## Related Resources

- [Prometheus Metrics Setup](prometheus-metrics.md) - Prometheus-specific configuration
- [Datadog Integration](datadog-notes.md) - Datadog APM setup with auto-instrumentation
- [Grafana Dashboard Guide](grafana-dashboard.md) - Visualizing OTel data in Grafana
- [Main Monitoring Guide](../README.md) - Comprehensive monitoring overview

## External References

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/net/)
- [OTel Environment Variables Spec](https://opentelemetry.io/docs/specs/otel/configuration/sdk-environment-variables/)
- [ASP.NET Core Instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore)
