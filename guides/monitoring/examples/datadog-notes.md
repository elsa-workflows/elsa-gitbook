# Datadog Integration for Elsa Workflows

This guide covers integrating Elsa Workflows with Datadog APM for distributed tracing, metrics, and unified observability.

## Overview

Datadog provides an all-in-one monitoring platform with:
- **APM (Application Performance Monitoring)**: Distributed traces and service maps
- **Metrics**: Time-series metrics with high cardinality support
- **Logs**: Centralized log aggregation with correlation to traces
- **Infrastructure**: Host and container monitoring
- **Dashboards & Alerts**: Rich visualization and alerting

Datadog supports OpenTelemetry, allowing you to use vendor-neutral instrumentation while exporting to Datadog backend.

## Integration Approaches

### Approach 1: Datadog APM with OTel Auto-Instrumentation (Recommended)

Use Datadog's OpenTelemetry support for automatic instrumentation without code changes.

### Approach 2: Datadog .NET Tracer

Use Datadog's native .NET tracer for deep APM integration.

### Approach 3: OpenTelemetry SDK with OTLP Exporter

Use OpenTelemetry SDK with OTLP exporter pointing to Datadog agent.

## Reference: Datadog Dockerfile

The elsa-core repository includes a reference Dockerfile for Datadog integration:

- **Path**: `docker/ElsaServer-Datadog.Dockerfile` (in elsa-core repository)
- **GitHub**: [ElsaServer-Datadog.Dockerfile](https://github.com/elsa-workflows/elsa-core/blob/main/docker/ElsaServer-Datadog.Dockerfile)

This Dockerfile demonstrates:
- Setting OpenTelemetry environment variables for auto-instrumentation
- Datadog agent configuration as sidecar or unified container
- Trace context propagation settings
- Service tagging for Unified Service Tagging

**Key patterns shown**:
- `DD_SERVICE`, `DD_ENV`, `DD_VERSION` environment variables for service identification
- `DD_TRACE_ENABLED`, `DD_LOGS_ENABLED` feature toggles
- `DD_AGENT_HOST` and `DD_TRACE_AGENT_PORT` for agent connectivity
- OTel-compatible environment variables for seamless integration

## Datadog APM with OpenTelemetry

### Prerequisites

- Datadog account and API key
- Datadog agent deployed (host agent, container sidecar, or Kubernetes DaemonSet)

### Install Datadog Agent

#### Docker Container (Sidecar Pattern)

```bash
docker run -d --name datadog-agent \
  -e DD_API_KEY=<YOUR_DATADOG_API_KEY> \
  -e DD_SITE=datadoghq.com \
  -e DD_APM_ENABLED=true \
  -e DD_LOGS_ENABLED=true \
  -e DD_DOGSTATSD_NON_LOCAL_TRAFFIC=true \
  -e DD_APM_NON_LOCAL_TRAFFIC=true \
  -v /var/run/docker.sock:/var/run/docker.sock:ro \
  -v /proc/:/host/proc/:ro \
  -v /sys/fs/cgroup/:/host/sys/fs/cgroup:ro \
  -p 8126:8126 \
  -p 8125:8125/udp \
  datadog/agent:latest
```

#### Kubernetes DaemonSet

```yaml
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: datadog-agent
  namespace: monitoring
spec:
  selector:
    matchLabels:
      app: datadog-agent
  template:
    metadata:
      labels:
        app: datadog-agent
    spec:
      containers:
        - name: datadog-agent
          image: datadog/agent:latest
          env:
            - name: DD_API_KEY
              valueFrom:
                secretKeyRef:
                  name: datadog-secret
                  key: api-key
            - name: DD_SITE
              value: "datadoghq.com"
            - name: DD_APM_ENABLED
              value: "true"
            - name: DD_LOGS_ENABLED
              value: "true"
            - name: DD_KUBERNETES_KUBELET_NODENAME
              valueFrom:
                fieldRef:
                  fieldPath: spec.nodeName
          ports:
            - containerPort: 8126
              name: traceport
            - containerPort: 8125
              name: dogstatsdport
              protocol: UDP
          volumeMounts:
            - name: dockersocket
              mountPath: /var/run/docker.sock
              readOnly: true
            - name: procdir
              mountPath: /host/proc
              readOnly: true
            - name: cgroups
              mountPath: /host/sys/fs/cgroup
              readOnly: true
      volumes:
        - name: dockersocket
          hostPath:
            path: /var/run/docker.sock
        - name: procdir
          hostPath:
            path: /proc
        - name: cgroups
          hostPath:
            path: /sys/fs/cgroup
```

### Configure Elsa Server for Datadog

#### Environment Variables

Set these environment variables in your Elsa Server deployment (following patterns from ElsaServer-Datadog.Dockerfile):

```bash
# Unified Service Tagging
DD_SERVICE=elsa-server
DD_ENV=production
DD_VERSION=3.0

# Datadog Agent Connection
DD_AGENT_HOST=datadog-agent  # Or localhost if agent on same host
DD_TRACE_AGENT_PORT=8126
DD_DOGSTATSD_PORT=8125

# Enable APM and Logs
DD_TRACE_ENABLED=true
DD_LOGS_ENABLED=true
DD_RUNTIME_METRICS_ENABLED=true

# Trace Sampling
DD_TRACE_SAMPLE_RATE=0.1  # Sample 10% of traces (adjust for production)

# OpenTelemetry Compatibility
OTEL_SERVICE_NAME=elsa-server
OTEL_RESOURCE_ATTRIBUTES=service.version=3.0,deployment.environment=production
OTEL_EXPORTER_OTLP_ENDPOINT=http://datadog-agent:4318
OTEL_TRACES_SAMPLER=traceidratio
OTEL_TRACES_SAMPLER_ARG=0.1
```

#### Docker Compose Example

```yaml
version: '3.8'

services:
  datadog-agent:
    image: datadog/agent:latest
    environment:
      - DD_API_KEY=${DATADOG_API_KEY}
      - DD_SITE=datadoghq.com
      - DD_APM_ENABLED=true
      - DD_LOGS_ENABLED=true
      - DD_APM_NON_LOCAL_TRAFFIC=true
      - DD_DOGSTATSD_NON_LOCAL_TRAFFIC=true
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
      - /proc/:/host/proc/:ro
      - /sys/fs/cgroup/:/host/sys/fs/cgroup:ro
    ports:
      - "8126:8126"
      - "8125:8125/udp"

  elsa-server:
    image: elsa-workflows/elsa-server:latest
    depends_on:
      - datadog-agent
    environment:
      # Unified Service Tagging
      - DD_SERVICE=elsa-server
      - DD_ENV=production
      - DD_VERSION=3.0
      
      # Datadog Agent Connection
      - DD_AGENT_HOST=datadog-agent
      - DD_TRACE_AGENT_PORT=8126
      
      # Enable Features
      - DD_TRACE_ENABLED=true
      - DD_LOGS_ENABLED=true
      - DD_RUNTIME_METRICS_ENABLED=true
      
      # OpenTelemetry
      - OTEL_SERVICE_NAME=elsa-server
      - OTEL_EXPORTER_OTLP_ENDPOINT=http://datadog-agent:4318
      
      # Application Config
      - ConnectionStrings__Elsa=...
    ports:
      - "5000:80"
    labels:
      com.datadoghq.ad.logs: '[{"source": "elsa-server", "service": "elsa-server"}]'
```

#### Kubernetes Deployment Example

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: elsa-server
  namespace: elsa-workflows
  labels:
    app: elsa-server
    tags.datadoghq.com/service: elsa-server
    tags.datadoghq.com/env: production
    tags.datadoghq.com/version: "3.0"
spec:
  replicas: 3
  selector:
    matchLabels:
      app: elsa-server
  template:
    metadata:
      labels:
        app: elsa-server
        tags.datadoghq.com/service: elsa-server
        tags.datadoghq.com/env: production
        tags.datadoghq.com/version: "3.0"
      annotations:
        ad.datadoghq.com/elsa-server.logs: '[{"source":"elsa-server","service":"elsa-server"}]'
    spec:
      containers:
        - name: elsa-server
          image: elsa-workflows/elsa-server:3.0
          env:
            # Unified Service Tagging
            - name: DD_SERVICE
              value: "elsa-server"
            - name: DD_ENV
              value: "production"
            - name: DD_VERSION
              value: "3.0"
            
            # Datadog Agent Connection (via hostIP for DaemonSet)
            - name: DD_AGENT_HOST
              valueFrom:
                fieldRef:
                  fieldPath: status.hostIP
            - name: DD_TRACE_AGENT_PORT
              value: "8126"
            
            # Enable Features
            - name: DD_TRACE_ENABLED
              value: "true"
            - name: DD_LOGS_ENABLED
              value: "true"
            - name: DD_RUNTIME_METRICS_ENABLED
              value: "true"
            
            # OpenTelemetry
            - name: OTEL_SERVICE_NAME
              value: "elsa-server"
            - name: OTEL_RESOURCE_ATTRIBUTES
              value: "service.version=3.0,deployment.environment=production"
            - name: OTEL_EXPORTER_OTLP_ENDPOINT
              value: "http://$(DD_AGENT_HOST):4318"
          ports:
            - containerPort: 80
              name: http
```

## Datadog .NET Tracer (Alternative)

For deeper Datadog integration, use the native .NET tracer.

### Installation

```bash
# Install Datadog .NET Tracer NuGet package
dotnet add package Datadog.Trace
```

### Configuration in Program.cs

```csharp
using Datadog.Trace;
using Datadog.Trace.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure Datadog tracer
var settings = TracerSettings.FromDefaultSources();
settings.ServiceName = "elsa-server";
settings.Environment = builder.Environment.EnvironmentName;
settings.ServiceVersion = "3.0";
settings.AgentUri = new Uri("http://datadog-agent:8126");
settings.TraceEnabled = true;

// Create global tracer
Tracer.Configure(settings);

// ... register Elsa services

var app = builder.Build();

// Datadog middleware (must be first)
app.UseMiddleware<Datadog.Trace.AspNetCore.TracingMiddleware>();

// ... other middleware
app.UseElsa();

app.Run();
```

### Auto-Instrumentation with .NET Tracer

For auto-instrumentation without code changes:

#### Download Tracer

```bash
# Linux
curl -LO https://github.com/DataDog/dd-trace-dotnet/releases/latest/download/datadog-dotnet-apm_amd64.deb
sudo dpkg -i datadog-dotnet-apm_amd64.deb

# Windows
# Download MSI from GitHub releases
```

#### Set Environment Variables

```bash
# Enable profiler
CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}

# Linux
CORECLR_PROFILER_PATH=/opt/datadog/Datadog.Trace.ClrProfiler.Native.so
DD_DOTNET_TRACER_HOME=/opt/datadog

# Windows
CORECLR_PROFILER_PATH=%ProgramFiles%\Datadog\.NET Tracer\Datadog.Trace.ClrProfiler.Native.dll
DD_DOTNET_TRACER_HOME=%ProgramFiles%\Datadog\.NET Tracer

# Service configuration
DD_SERVICE=elsa-server
DD_ENV=production
DD_VERSION=3.0
DD_AGENT_HOST=datadog-agent
DD_TRACE_AGENT_PORT=8126
```

## Log Collection

### Structured Logging with Serilog

Configure Serilog to output JSON logs that Datadog can parse:

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console"],
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"],
    "Properties": {
      "Application": "Elsa.Server",
      "dd.service": "elsa-server",
      "dd.env": "production",
      "dd.version": "3.0"
    }
  }
}
```

### Correlation IDs in Logs

Inject trace correlation into logs:

```csharp
using Serilog.Context;
using Datadog.Trace;

public async Task ExecuteWorkflowAsync(string workflowInstanceId)
{
    var scope = Tracer.Instance.ActiveScope;
    var traceId = scope?.Span.TraceId.ToString() ?? "N/A";
    var spanId = scope?.Span.SpanId.ToString() ?? "N/A";
    
    using (LogContext.PushProperty("dd.trace_id", traceId))
    using (LogContext.PushProperty("dd.span_id", spanId))
    using (LogContext.PushProperty("workflow.instance.id", workflowInstanceId))
    {
        _logger.LogInformation("Executing workflow {WorkflowInstanceId}", workflowInstanceId);
        
        await _workflowRunner.ExecuteAsync(workflowInstanceId);
        
        _logger.LogInformation("Workflow executed successfully");
    }
}
```

### Docker Log Collection

Datadog agent automatically collects container logs when configured:

```yaml
# docker-compose.yml
services:
  elsa-server:
    # ... other config
    labels:
      com.datadoghq.ad.logs: |
        [{
          "source": "elsa-server",
          "service": "elsa-server",
          "log_processing_rules": [{
            "type": "multi_line",
            "name": "log_start_with_date",
            "pattern": "\\d{4}-\\d{2}-\\d{2}"
          }]
        }]
```

## Custom Metrics with DogStatsD

Send custom metrics to Datadog using DogStatsD:

### Installation

```bash
dotnet add package DogStatsD-CSharp-Client
```

### Configuration

```csharp
using StatsdClient;

// Configure DogStatsD
var dogstatsdConfig = new StatsdConfig
{
    StatsdServerName = "datadog-agent",
    StatsdPort = 8125,
    Prefix = "elsa"
};

DogStatsd.Configure(dogstatsdConfig);

// Send metrics
public async Task ExecuteWorkflowAsync(string workflowDefinitionId)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        await _workflowRunner.ExecuteAsync(workflowDefinitionId);
        
        DogStatsd.Increment("workflows.executed", 1, 
            tags: new[] { $"status:completed", $"workflow:{workflowDefinitionId}" });
    }
    catch (Exception)
    {
        DogStatsd.Increment("workflows.executed", 1,
            tags: new[] { $"status:faulted", $"workflow:{workflowDefinitionId}" });
        throw;
    }
    finally
    {
        stopwatch.Stop();
        DogStatsd.Histogram("workflow.duration", stopwatch.Elapsed.TotalSeconds,
            tags: new[] { $"workflow:{workflowDefinitionId}" });
    }
}
```

## Datadog Dashboard Configuration

### Pre-built Dashboards

Datadog automatically creates APM dashboards for your services. Navigate to:
- APM → Services → elsa-server
- APM → Service Map (visualize service dependencies)
- APM → Traces (search and analyze individual traces)

### Custom Dashboard Widgets

Create custom dashboards with Elsa-specific metrics:

#### Workflow Execution Rate

```
sum:elsa.workflows.executed{*}.as_count()
```

#### Workflow Success Rate

```
sum:elsa.workflows.executed{status:completed}.as_count() / sum:elsa.workflows.executed{*}.as_count()
```

#### Workflow P95 Duration

```
p95:elsa.workflow.duration{*}
```

#### Bookmark Backlog

```
avg:elsa.bookmarks.pending{*}
```

## Alerting in Datadog

### Monitor Configuration

Create monitors for critical conditions:

#### High Workflow Failure Rate

```
Metric: elsa.workflows.executed
Aggregation: sum
Tags: status:faulted
Alert threshold: > 5% over 5 minutes
Warning threshold: > 2% over 5 minutes
```

#### Lock Timeout Surge

```
Metric: elsa.locking.timeouts
Aggregation: sum
Alert threshold: > 10/min over 2 minutes
```

#### Trace Error Rate

```
Metric: trace.servlet.request.errors
Service: elsa-server
Alert threshold: > 5% over 5 minutes
```

### Notification Channels

Configure notification integrations:
- Slack: #elsa-alerts channel
- PagerDuty: Critical alerts for on-call
- Email: Team distribution list

## Verification

After configuration, verify Datadog integration:

1. **Check APM Services**:
   - Navigate to APM → Services in Datadog UI
   - Verify "elsa-server" service appears
   - Click through to see traces and metrics

2. **Verify Traces**:
   - Execute some workflows
   - Search for traces: `service:elsa-server`
   - Verify trace spans include workflow operations

3. **Check Metrics**:
   - Navigate to Metrics → Explorer
   - Search for custom metrics: `elsa.*`
   - Verify metrics are being reported

4. **Verify Logs**:
   - Navigate to Logs → Search
   - Filter: `service:elsa-server`
   - Verify logs include trace correlation (`dd.trace_id`, `dd.span_id`)

5. **Test Correlation**:
   - Find a trace in APM
   - Click "View Logs" button
   - Verify related logs appear

## Troubleshooting

### No Traces Appearing

**Check**:
1. Verify Datadog agent is running: `docker ps | grep datadog`
2. Check agent logs: `docker logs datadog-agent`
3. Verify `DD_TRACE_ENABLED=true` is set
4. Check agent connectivity: `telnet datadog-agent 8126`
5. Review application logs for tracer errors

### Metrics Not Appearing

**Check**:
1. Verify DogStatsD port is open: `netstat -an | grep 8125`
2. Check DogStatsD configuration in agent
3. Test with simple counter: `echo "custom.metric:1|c" | nc -u -w1 datadog-agent 8125`
4. Verify metric namespace (prefix) is correct

### Logs Missing Correlation

**Check**:
1. Verify structured logging output (JSON format)
2. Ensure `dd.trace_id` and `dd.span_id` are in log context
3. Check log processing rules in Datadog
4. Verify Unified Service Tagging is consistent

### High APM Costs

**Solutions**:
1. Reduce trace sampling rate: `DD_TRACE_SAMPLE_RATE=0.01` (1%)
2. Use Ingestion Controls in Datadog UI
3. Filter out health check endpoints
4. Enable trace analytics only for critical services

## Best Practices

### Unified Service Tagging

Always use consistent tags across metrics, traces, and logs:
- `DD_SERVICE`: Service name (e.g., "elsa-server")
- `DD_ENV`: Environment (e.g., "production", "staging")
- `DD_VERSION`: Application version (e.g., "3.0", commit SHA)

### Tag Strategy

Use meaningful tags with low cardinality:
- ✅ `workflow:order-processing` (workflow definition ID)
- ✅ `status:completed` (finite set of statuses)
- ✅ `environment:production` (finite environments)
- ❌ `workflow_instance:abc-123` (high cardinality)
- ❌ `correlation_id:xyz-789` (unbounded)

### Sampling Strategy

- **Development**: 100% sampling (`DD_TRACE_SAMPLE_RATE=1.0`)
- **Staging**: 10-50% sampling (`DD_TRACE_SAMPLE_RATE=0.1`)
- **Production**: 1-10% sampling with error sampling at 100%

### Cost Optimization

- Use indexed spans sparingly (only critical operations)
- Set retention periods appropriately (15 days default)
- Archive historical traces to S3/Azure Blob for compliance
- Use metrics-based monitoring for high-frequency checks

## Related Resources

- [OpenTelemetry Setup](otel-setup.md) - OTel configuration compatible with Datadog
- [Prometheus Metrics](prometheus-metrics.md) - Alternative metrics approach
- [Main Monitoring Guide](../README.md) - Comprehensive monitoring overview

## External References

- [Datadog APM Documentation](https://docs.datadoghq.com/tracing/)
- [Datadog .NET Tracer](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core/)
- [DogStatsD](https://docs.datadoghq.com/developers/dogstatsd/)
- [Datadog Agent](https://docs.datadoghq.com/agent/)
- [Unified Service Tagging](https://docs.datadoghq.com/getting_started/tagging/unified_service_tagging/)
