---
description: >-
  Comprehensive guide to monitoring and observability for Elsa Workflows, covering metrics, traces, logs, OpenTelemetry integration, Prometheus, Grafana, and Datadog with production-ready examples.
---

# Monitoring & Observability Guide

## Executive Summary

Effective monitoring and observability are essential for running Elsa Workflows in production environments. This guide provides actionable steps for instrumenting your Elsa Server and worker applications with metrics, distributed traces, and structured logs using industry-standard tools like OpenTelemetry (OTel), Prometheus, Grafana, and Datadog.

### Why Monitoring Matters for Elsa Workflows

**Production Challenges Monitoring Solves:**

1. **Workflow Health Visibility**: Track workflow execution success rates, duration, and failure patterns
2. **Performance Optimization**: Identify bottlenecks in bookmark processing, scheduling, and workflow execution
3. **Operational Troubleshooting**: Quickly diagnose issues with distributed locking, queue backlogs, and timer fires
4. **Capacity Planning**: Monitor resource utilization and scale appropriately
5. **SLA Compliance**: Alert on critical metrics to maintain service level agreements

**Key Metrics to Monitor:**

- Workflow execution rate and duration
- Bookmark creation and resumption counts
- Scheduled job executions and timer fires
- Queue lengths and backlog growth
- Distributed lock acquisition timeouts
- Database connection pool utilization
- HTTP endpoint response times

Without proper monitoring, you may experience:
- Undetected workflow failures leading to business process disruption
- Performance degradation that goes unnoticed until customer impact
- Difficulty troubleshooting distributed system issues across cluster nodes
- Inability to proactively scale before capacity limits are reached

## Table of Contents

- [Prerequisites](#prerequisites)
- [Observability Components](#observability-components)
- [OpenTelemetry Integration](#opentelemetry-integration)
- [Prometheus Metrics](#prometheus-metrics)
- [Distributed Traces](#distributed-traces)
- [Structured Logging](#structured-logging)
- [Grafana Dashboards](#grafana-dashboards)
- [Datadog Integration](#datadog-integration)
- [Recommended Metrics](#recommended-metrics)
- [Alerting and SLOs](#alerting-and-slos)
- [Troubleshooting](#troubleshooting)
- [Verification Checklist](#verification-checklist)
- [Production Best Practices](#production-best-practices)
- [Related Resources](#related-resources)

## Prerequisites

Before implementing monitoring, ensure you have:

- Elsa Server deployed and running (see [Elsa Server Setup](../../application-types/elsa-server.md))
- .NET 8.0 or later SDK
- Basic understanding of observability concepts (metrics, traces, logs)
- Access to a monitoring backend (Prometheus, Grafana, Datadog, or similar)
- For clustered deployments, familiarity with [Clustering Guide](../clustering/README.md)

## Observability Components

Modern observability is built on three pillars:

### 1. Metrics (What is happening?)

Numerical measurements collected over time that provide quantitative insights:
- **Counters**: Monotonically increasing values (e.g., total workflows executed)
- **Gauges**: Point-in-time values that can go up or down (e.g., active workflow instances)
- **Histograms**: Distribution of values (e.g., workflow execution duration buckets)

**Example Elsa Metrics:**
- `elsa_workflows_executed_total` - Total count of workflow executions
- `elsa_workflows_active` - Current number of running workflows
- `elsa_workflow_duration_seconds` - Distribution of workflow execution times

### 2. Traces (Why is it happening?)

Distributed traces track request flow through your system, showing causal relationships:
- **Spans**: Individual operations with start time, duration, and context
- **Trace Context**: Correlation IDs that link related spans across services
- **Attributes**: Key-value pairs providing operation details

**Example Elsa Traces:**
- Workflow execution span with activity execution child spans
- Bookmark resume span with distributed lock acquisition span
- HTTP trigger span showing incoming request to workflow completion

### 3. Logs (Detailed context)

Structured log messages providing detailed context about system behavior:
- **Correlation IDs**: Link logs to traces and workflow instances
- **Structured Fields**: Machine-parseable key-value pairs
- **Log Levels**: DEBUG, INFO, WARNING, ERROR for filtering

**Example Elsa Logs:**
```json
{
  "timestamp": "2024-11-24T17:45:23.123Z",
  "level": "Information",
  "message": "Workflow resumed successfully",
  "workflowInstanceId": "abc123",
  "workflowDefinitionId": "order-processing",
  "correlationId": "xyz789",
  "activityId": "SendEmail",
  "traceId": "def456",
  "spanId": "ghi789"
}
```

## OpenTelemetry Integration

OpenTelemetry (OTel) is the industry-standard observability framework for cloud-native applications. It provides vendor-neutral APIs and SDKs for collecting metrics, traces, and logs.

### Why OpenTelemetry?

- **Vendor Neutral**: Export to Prometheus, Grafana, Datadog, Azure Monitor, AWS X-Ray, etc.
- **Automatic Instrumentation**: Built-in instrumentation for ASP.NET Core, HttpClient, database clients
- **Context Propagation**: Distributed trace context flows across service boundaries
- **Rich Ecosystem**: Extensive library support and community

### Enabling OpenTelemetry in Elsa Server

OpenTelemetry can be configured in your Elsa Server's `Program.cs` using the OpenTelemetry .NET SDK.

#### Reference: Elsa Server Program.cs

The standard Elsa Server entrypoint is located at:
- **Path**: `src/apps/Elsa.Server.Web/Program.cs` (in elsa-core repository)
- **GitHub**: [Elsa.Server.Web/Program.cs](https://github.com/elsa-workflows/elsa-core/blob/main/src/apps/Elsa.Server.Web/Program.cs)

This file demonstrates the typical ASP.NET Core host setup where you can register OpenTelemetry services.

#### Configuration Steps

See [OpenTelemetry Setup Example](examples/otel-setup.md) for detailed code snippets showing:
1. Installing required NuGet packages
2. Configuring OTel in `Program.cs`
3. Setting OTLP exporter endpoints
4. Enabling automatic instrumentation for ASP.NET Core, HttpClient, and database providers

#### Environment Variables for OTel

OpenTelemetry supports configuration via environment variables (following [OTel spec](https://opentelemetry.io/docs/specs/otel/configuration/sdk-environment-variables/)):

```bash
# OTLP Exporter Configuration
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4318
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
OTEL_SERVICE_NAME=elsa-server
OTEL_RESOURCE_ATTRIBUTES=service.version=3.0,deployment.environment=production

# Trace Configuration
OTEL_TRACES_SAMPLER=always_on
OTEL_TRACES_EXPORTER=otlp

# Metrics Configuration
OTEL_METRICS_EXPORTER=otlp
```

#### Docker Auto-Instrumentation with Datadog

For containerized deployments with Datadog APM, see the reference Dockerfile:
- **Path**: `docker/ElsaServer-Datadog.Dockerfile` (in elsa-core repository)
- **GitHub**: [ElsaServer-Datadog.Dockerfile](https://github.com/elsa-workflows/elsa-core/blob/main/docker/ElsaServer-Datadog.Dockerfile)

This Dockerfile demonstrates:
- OTel auto-instrumentation environment variables
- Datadog agent integration
- Trace propagation configuration

See [Datadog Integration Notes](examples/datadog-notes.md) for more details.

## Prometheus Metrics

Prometheus is a popular open-source monitoring system with a powerful query language (PromQL) and built-in alerting.

### Exposing Metrics for Prometheus

To expose metrics in Prometheus format, you have two options:

#### Option 1: OpenTelemetry Prometheus Exporter

Use the OpenTelemetry .NET Prometheus exporter to expose metrics at `/metrics` endpoint:

```bash
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore
```

Configuration in `Program.cs`:
```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();
app.MapPrometheusScrapingEndpoint(); // Exposes /metrics
```

#### Option 2: prometheus-net

Use the `prometheus-net` library for direct Prometheus metric exposition:

```bash
dotnet add package prometheus-net.AspNetCore
```

See [Prometheus Metrics Setup](examples/prometheus-metrics.md) for complete implementation examples.

### Scraping Configuration

Configure Prometheus to scrape your Elsa Server metrics:

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'elsa-server'
    scrape_interval: 15s
    static_configs:
      - targets: ['elsa-server:5000']
    metrics_path: '/metrics'
```

For Kubernetes deployments, use ServiceMonitor (Prometheus Operator) or pod annotations for automatic discovery.

## Distributed Traces

Distributed tracing provides end-to-end visibility into workflow execution across services and infrastructure.

### Trace Context in Elsa Workflows

Elsa Workflows maintains trace context throughout workflow execution:

1. **Incoming Request Trace**: HTTP triggers or API calls create a root span
2. **Workflow Execution Span**: Parent span for the entire workflow run
3. **Activity Execution Spans**: Child spans for each activity execution
4. **External Call Spans**: HTTP activities, database queries, message publishing
5. **Bookmark Operations**: Spans for bookmark creation, scheduling, and resumption

### Trace Correlation

Correlation IDs link traces, logs, and workflow instances:

- **Workflow Instance ID**: Unique identifier for each workflow execution
- **Correlation ID**: Business identifier linking related workflows (see [Correlation ID Concept](../../getting-started/concepts/correlation-id.md))
- **Trace ID**: OpenTelemetry trace identifier spanning distributed operations
- **Span ID**: Individual operation identifier within a trace

### Viewing Traces

Traces can be viewed in:
- **Jaeger**: Open-source distributed tracing platform
- **Grafana Tempo**: Grafana's tracing backend
- **Datadog APM**: Commercial APM solution
- **Azure Application Insights**: Azure-native APM
- **AWS X-Ray**: AWS-native tracing service

## Structured Logging

Structured logging emits log messages as JSON with queryable fields rather than plain text.

### Log Structure for Elsa

Configure structured logging in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Elsa": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"],
    "Properties": {
      "Application": "Elsa.Server",
      "Environment": "Production"
    }
  }
}
```

### Correlation IDs in Logs

Ensure logs include correlation identifiers for troubleshooting:

```csharp
// Example enriching log context (in middleware or activity)
using Serilog.Context;

LogContext.PushProperty("WorkflowInstanceId", workflowInstance.Id);
LogContext.PushProperty("CorrelationId", workflowInstance.CorrelationId);
LogContext.PushProperty("TraceId", Activity.Current?.TraceId.ToString());
LogContext.PushProperty("SpanId", Activity.Current?.SpanId.ToString());

_logger.LogInformation("Workflow {WorkflowDefinitionId} resumed successfully", 
    workflowInstance.DefinitionId);
```

### Log Aggregation

Send structured logs to centralized logging platforms:
- **Elasticsearch + Kibana**: ELK stack for log aggregation and search
- **Grafana Loki**: Log aggregation optimized for Kubernetes
- **Datadog Logs**: Unified logs and APM
- **Azure Monitor Logs**: Azure-native log analytics
- **AWS CloudWatch Logs**: AWS-native log aggregation

## Grafana Dashboards

Grafana provides rich visualization for Prometheus metrics and distributed traces.

### Dashboard Components

A production-ready Elsa dashboard should include:

1. **Overview Panels**
   - Total workflows executed (counter)
   - Active workflow instances (gauge)
   - Workflow execution rate (rate per minute)
   - Success vs. failure rates

2. **Performance Panels**
   - Workflow execution duration (histogram)
   - Activity execution time distribution
   - Bookmark processing latency
   - Database query performance

3. **Operational Panels**
   - Bookmark backlog size (gauge)
   - Scheduled jobs pending (gauge)
   - Lock acquisition timeouts (counter)
   - Queue lengths for background work

4. **Infrastructure Panels**
   - CPU and memory utilization
   - HTTP request latency
   - Database connection pool usage
   - Distributed cache hit ratio

See [Grafana Dashboard Guide](examples/grafana-dashboard.md) for:
- Recommended panel configurations
- PromQL query examples
- Dashboard JSON model placeholders
- Screenshot placeholders

**Note**: Actual dashboard images are not included in this guide. Create dashboards in your Grafana instance and take screenshots for team documentation.

## Datadog Integration

Datadog provides an all-in-one monitoring platform with APM, logs, metrics, and traces.

### OTel Auto-Instrumentation

Datadog supports OpenTelemetry auto-instrumentation for .NET applications. This approach requires no code changes.

#### Reference: Datadog Dockerfile

See the reference Dockerfile for Datadog integration:
- **Path**: `docker/ElsaServer-Datadog.Dockerfile` (in elsa-core repository)
- **GitHub**: [ElsaServer-Datadog.Dockerfile](https://github.com/elsa-workflows/elsa-core/blob/main/docker/ElsaServer-Datadog.Dockerfile)

This Dockerfile shows:
- OTel auto-instrumentation setup
- Required environment variables
- Datadog agent sidecar configuration

### Datadog APM Setup

For detailed Datadog configuration, see [Datadog Integration Notes](examples/datadog-notes.md) covering:
- Environment variable configuration
- Datadog agent deployment patterns
- Unified Service Tagging
- Log and trace correlation

## Recommended Metrics

The following metrics are essential for monitoring Elsa Workflows in production.

### Workflow Execution Metrics

```promql
# Total workflows executed (counter)
elsa_workflows_executed_total{status="completed"}
elsa_workflows_executed_total{status="faulted"}
elsa_workflows_executed_total{status="cancelled"}

# Active workflow instances (gauge)
elsa_workflows_active

# Workflow execution duration (histogram)
elsa_workflow_duration_seconds_bucket{le="1.0"}    # <= 1 second
elsa_workflow_duration_seconds_bucket{le="5.0"}    # <= 5 seconds
elsa_workflow_duration_seconds_bucket{le="30.0"}   # <= 30 seconds
elsa_workflow_duration_seconds_bucket{le="+Inf"}   # all
```

### Bookmark and Resume Metrics

```promql
# Bookmarks created (counter)
elsa_workflows_bookmarks_created_total

# Bookmarks resumed (counter)
elsa_workflows_bookmarks_resumed_total{status="success"}
elsa_workflows_bookmarks_resumed_total{status="not_found"}
elsa_workflows_bookmarks_resumed_total{status="failed"}

# Bookmark backlog (gauge)
elsa_workflows_bookmarks_pending
```

### Scheduling Metrics

```promql
# Timer fires (counter)
elsa_scheduling_timer_fires_total{status="success"}
elsa_scheduling_timer_fires_total{status="failed"}

# Scheduled jobs pending (gauge)
elsa_scheduling_jobs_pending

# Cron trigger executions (counter)
elsa_scheduling_cron_triggers_total
```

### Distributed Locking Metrics

```promql
# Lock acquisitions (counter)
elsa_locking_acquisitions_total{status="success"}
elsa_locking_acquisitions_total{status="timeout"}
elsa_locking_acquisitions_total{status="failed"}

# Lock wait time (histogram)
elsa_locking_wait_duration_seconds_bucket

# Current locks held (gauge)
elsa_locking_locks_held
```

### Queue and Background Work Metrics

```promql
# Background work items queued (counter)
elsa_background_work_queued_total

# Background work items processed (counter)
elsa_background_work_processed_total{status="success"}
elsa_background_work_processed_total{status="failed"}

# Queue depth (gauge)
elsa_background_work_queue_depth
```

### HTTP Endpoint Metrics

```promql
# HTTP requests (counter)
http_server_requests_total{method="POST", endpoint="/elsa/api/workflow-instances"}

# HTTP request duration (histogram)
http_server_request_duration_seconds_bucket{endpoint="/elsa/api/workflow-instances"}

# HTTP errors (counter)
http_server_requests_total{status="500"}
```

### Database Metrics

```promql
# Database connection pool (gauge)
dotnet_db_connections_active
dotnet_db_connections_idle

# Database query duration (histogram)
dotnet_db_query_duration_seconds_bucket

# Database errors (counter)
dotnet_db_errors_total
```

**Note**: Actual metric names depend on your instrumentation library (OpenTelemetry, prometheus-net, etc.). The above names follow common conventions and should be adapted to your setup.

## Alerting and SLOs

Define alerts and Service Level Objectives (SLOs) to proactively detect issues.

### Critical Alerts

#### High Workflow Failure Rate

```promql
# Alert if workflow failure rate > 5% over 5 minutes
rate(elsa_workflows_executed_total{status="faulted"}[5m]) 
  / 
rate(elsa_workflows_executed_total[5m]) > 0.05
```

**Action**: Investigate recent workflow failures, check logs for error patterns.

#### Lock Timeout Surge

```promql
# Alert if lock timeouts > 10 per minute
rate(elsa_locking_acquisitions_total{status="timeout"}[1m]) > 10
```

**Action**: Check distributed lock provider health (Redis, PostgreSQL), verify network connectivity, inspect lock contention patterns.

#### Bookmark Backlog Growing

```promql
# Alert if bookmark backlog > 1000 and increasing
elsa_workflows_bookmarks_pending > 1000
  and
deriv(elsa_workflows_bookmarks_pending[10m]) > 0
```

**Action**: Scale worker capacity, check scheduler health, investigate slow bookmark processing.

#### Timer Fire Failures

```promql
# Alert if timer fire failure rate > 1% over 5 minutes
rate(elsa_scheduling_timer_fires_total{status="failed"}[5m])
  /
rate(elsa_scheduling_timer_fires_total[5m]) > 0.01
```

**Action**: Check Quartz.NET scheduler health, verify database connectivity, review timer job logs.

#### High Database Latency

```promql
# Alert if P95 database query latency > 1 second
histogram_quantile(0.95, 
  rate(dotnet_db_query_duration_seconds_bucket[5m])
) > 1.0
```

**Action**: Check database performance, inspect slow queries, verify connection pool configuration.

### Service Level Objectives (SLOs)

Define SLOs to measure service reliability:

#### Workflow Execution Success Rate SLO

**Target**: 99.9% of workflows execute successfully

```promql
# Success rate over 30 days
sum(rate(elsa_workflows_executed_total{status="completed"}[30d]))
  /
sum(rate(elsa_workflows_executed_total[30d]))
```

**Error Budget**: With 99.9% SLO, you have a 0.1% error budget (43.8 minutes of downtime per month).

#### Workflow Execution Latency SLO

**Target**: 95% of workflows complete within 30 seconds

```promql
# P95 latency over 30 days
histogram_quantile(0.95,
  rate(elsa_workflow_duration_seconds_bucket[30d])
)
```

#### Bookmark Resume Latency SLO

**Target**: 99% of bookmark resumes complete within 5 seconds

```promql
# P99 resume latency
histogram_quantile(0.99,
  rate(elsa_bookmark_resume_duration_seconds_bucket[30d])
)
```

See [Prometheus Alerting Rules](examples/prometheus-metrics.md) for complete alert rule definitions.

## Troubleshooting

Common monitoring-related issues and solutions.

### No Metrics Exposed

**Symptom**: `/metrics` endpoint returns 404 or empty response.

**Checks**:
1. Verify Prometheus exporter is registered in `Program.cs`
2. Check `MapPrometheusScrapingEndpoint()` is called
3. Ensure firewall allows access to metrics port
4. Review application startup logs for errors

### Traces Not Appearing

**Symptom**: No traces visible in Jaeger/Grafana/Datadog.

**Checks**:
1. Verify OTel exporter is configured with correct endpoint
2. Check OTLP collector is running and reachable
3. Ensure trace sampler is enabled (`always_on` for testing)
4. Review application logs for OTel exporter errors
5. Verify network connectivity to tracing backend

### Logs Missing Correlation IDs

**Symptom**: Cannot correlate logs with workflow instances or traces.

**Checks**:
1. Ensure log enrichment is configured (Serilog `FromLogContext`)
2. Verify correlation IDs are pushed to log context in activities
3. Check structured logging is enabled (JSON output)
4. Review log aggregation pipeline configuration

### High Cardinality Metrics

**Symptom**: Prometheus or metrics backend experiencing high cardinality issues.

**Solution**:
- Avoid using workflow instance IDs or correlation IDs as metric labels
- Use finite label values (status, type, definition ID)
- Aggregate metrics at query time rather than during collection
- Consider using exemplars for linking metrics to traces

## Verification Checklist

Use this checklist to validate monitoring setup in development clusters:

### Metrics Verification

- [ ] `/metrics` endpoint is accessible and returns Prometheus format metrics
- [ ] Counter metrics increment when workflows execute
- [ ] Gauge metrics reflect current system state (active workflows, pending jobs)
- [ ] Histogram buckets are properly configured for duration metrics
- [ ] Labels are correctly applied (status, workflow definition, etc.)

### Traces Verification

- [ ] Traces appear in tracing backend (Jaeger/Grafana/Datadog)
- [ ] Trace spans include workflow instance ID and correlation ID
- [ ] Parent-child span relationships are correct (workflow → activities)
- [ ] Trace context propagates across HTTP calls and message bus
- [ ] Span attributes include relevant metadata (activity type, outcome)

### Logs Verification

- [ ] Logs are emitted in structured format (JSON)
- [ ] Logs include trace ID, span ID, workflow instance ID
- [ ] Log levels are appropriate (INFO for normal operations, ERROR for failures)
- [ ] Logs are aggregated in centralized logging platform
- [ ] Search by correlation ID returns all related log entries

### Alerting Verification

- [ ] Alert rules are deployed to Prometheus/Alertmanager
- [ ] Test alerts fire correctly (temporarily break something)
- [ ] Alert notifications reach intended channels (PagerDuty, Slack, email)
- [ ] Alert runbooks are documented and accessible
- [ ] SLO dashboards are created and visible to team

### Dashboard Verification

- [ ] Grafana dashboard is created and displays metrics
- [ ] All PromQL queries execute without errors
- [ ] Dashboard variables work correctly (environment, cluster)
- [ ] Dashboard is shared with team and pinned/favorited
- [ ] Dashboard includes links to relevant runbooks

## Production Best Practices

### Do's

✅ **Use Sampling for Traces**: Sample 1-10% of traces in production to reduce overhead  
✅ **Set Metric Cardinality Limits**: Limit label values to prevent cardinality explosion  
✅ **Implement Log Levels**: Use DEBUG for development, INFO/WARNING for production  
✅ **Correlate Signals**: Link metrics, traces, and logs via correlation IDs  
✅ **Monitor the Monitors**: Alert if metrics collection fails (e.g., Prometheus down)  
✅ **Document Runbooks**: Create runbooks for each alert with troubleshooting steps  
✅ **Regular Review**: Periodically review dashboards and alerts, remove stale ones  
✅ **Test in Staging**: Validate monitoring setup in staging before production  

### Don'ts

❌ **Don't Log Sensitive Data**: Avoid logging PII, secrets, or sensitive business data  
❌ **Don't Use High Cardinality Labels**: Never use instance IDs or unbounded values as metric labels  
❌ **Don't Sample Critical Traces**: Always trace critical paths (payments, auth) at 100%  
❌ **Don't Ignore Alert Fatigue**: If alerts are noisy, tune thresholds or remove alert  
❌ **Don't Skip Retention Policies**: Configure retention for metrics, logs, and traces to manage costs  
❌ **Don't Forget Backups**: Export dashboards and alert rules to version control  
❌ **Don't Over-Instrument**: Too many metrics and spans add overhead; instrument what matters  

### Security Considerations

- **Secure Metrics Endpoint**: Protect `/metrics` with authentication or restrict to internal network
- **Sanitize Logs**: Never log passwords, tokens, or API keys
- **Control Access**: Restrict dashboard and trace access to authorized users
- **Encrypt Transport**: Use TLS for OTLP exporters and log shipping
- **Audit Monitoring Access**: Track who accesses sensitive observability data

## Related Resources

### Internal Documentation

- [Clustering Guide](../clustering/README.md) - Distributed locking and scheduling monitoring
- [Database Configuration](../../getting-started/database-configuration.md) - Database connection metrics
- [Correlation ID Concept](../../getting-started/concepts/correlation-id.md) - Understanding correlation in workflows
- [Logging Framework](../../features/logging-framework.md) - Elsa's structured logging features

### External Resources

- [OpenTelemetry Documentation](https://opentelemetry.io/docs/) - OTel concepts and SDKs
- [Prometheus Documentation](https://prometheus.io/docs/) - Prometheus query language and alerting
- [Grafana Documentation](https://grafana.com/docs/) - Dashboard creation and data sources
- [Datadog APM](https://docs.datadoghq.com/tracing/) - Datadog distributed tracing
- [.NET Observability](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/) - .NET diagnostics and metrics

### Elsa Core References

See [Source File References](README-REFERENCES.md) for a complete list of elsa-core source files referenced in this guide.

---

**Need Help?**

If you encounter issues not covered in this guide, please open an issue on the [Elsa Workflows GitHub repository](https://github.com/elsa-workflows/elsa-core/issues).
