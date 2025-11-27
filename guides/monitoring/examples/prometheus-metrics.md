# Prometheus Metrics for Elsa Workflows

This guide explains how to expose Prometheus metrics from your Elsa Server and provides recommended metric definitions, labels, and alerting rules.

## Prerequisites

- Elsa Server application running
- Prometheus server or compatible metrics backend
- Basic understanding of Prometheus metric types (counter, gauge, histogram)

## Exposing Metrics

You have two primary options for exposing Prometheus metrics from .NET applications:

### Option 1: OpenTelemetry Prometheus Exporter (Recommended)

Uses OpenTelemetry SDK with Prometheus exporter for vendor-neutral instrumentation.

#### Installation

```bash
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore
```

#### Configuration

In `Program.cs`:

```csharp
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddMeter("Elsa.*") // Custom Elsa metrics
        .AddPrometheusExporter());

var app = builder.Build();

// Expose /metrics endpoint
app.MapPrometheusScrapingEndpoint();

app.Run();
```

This exposes metrics at `http://localhost:5000/metrics` in Prometheus format.

### Option 2: prometheus-net Library

Direct Prometheus client library for .NET with explicit metric definitions.

#### Installation

```bash
dotnet add package prometheus-net.AspNetCore
```

#### Configuration

In `Program.cs`:

```csharp
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ... other service registrations

var app = builder.Build();

// Enable metrics collection middleware
app.UseHttpMetrics();

// Expose /metrics endpoint
app.MapMetrics();

// Enable gRPC metrics (if using gRPC)
app.UseGrpcMetrics();

app.Run();
```

## Recommended Elsa Metrics

Define custom metrics for Elsa-specific operations using either approach.

### Using OpenTelemetry Meters

```csharp
using System.Diagnostics.Metrics;

public class ElsaMetrics
{
    private static readonly Meter Meter = new Meter("Elsa.Workflows", "1.0.0");
    
    // Workflow execution counters
    public static readonly Counter<long> WorkflowsExecuted = 
        Meter.CreateCounter<long>(
            "elsa_workflows_executed_total",
            description: "Total number of workflow executions");
    
    public static readonly Counter<long> WorkflowsFaulted = 
        Meter.CreateCounter<long>(
            "elsa_workflows_faulted_total",
            description: "Total number of faulted workflow executions");
    
    // Bookmark counters
    public static readonly Counter<long> BookmarksCreated = 
        Meter.CreateCounter<long>(
            "elsa_workflows_bookmarks_created_total",
            description: "Total number of bookmarks created");
    
    public static readonly Counter<long> BookmarksResumed = 
        Meter.CreateCounter<long>(
            "elsa_workflows_bookmarks_resumed_total",
            description: "Total number of bookmarks resumed");
    
    // Scheduling counters
    public static readonly Counter<long> TimerFires = 
        Meter.CreateCounter<long>(
            "elsa_scheduling_timer_fires_total",
            description: "Total number of timer fires");
    
    public static readonly Counter<long> CronTriggers = 
        Meter.CreateCounter<long>(
            "elsa_scheduling_cron_triggers_total",
            description: "Total number of cron trigger executions");
    
    // Lock counters
    public static readonly Counter<long> LockAcquisitions = 
        Meter.CreateCounter<long>(
            "elsa_locking_acquisitions_total",
            description: "Total number of distributed lock acquisitions");
    
    public static readonly Counter<long> LockTimeouts = 
        Meter.CreateCounter<long>(
            "elsa_locking_timeouts_total",
            description: "Total number of distributed lock timeouts");
    
    // Gauges
    public static readonly ObservableGauge<int> ActiveWorkflows = 
        Meter.CreateObservableGauge<int>(
            "elsa_workflows_active",
            description: "Current number of active workflow instances");
    
    public static readonly ObservableGauge<int> BookmarksPending = 
        Meter.CreateObservableGauge<int>(
            "elsa_workflows_bookmarks_pending",
            description: "Current number of pending bookmarks");
    
    public static readonly ObservableGauge<int> ScheduledJobsPending = 
        Meter.CreateObservableGauge<int>(
            "elsa_scheduling_jobs_pending",
            description: "Current number of pending scheduled jobs");
    
    // Histograms
    public static readonly Histogram<double> WorkflowDuration = 
        Meter.CreateHistogram<double>(
            "elsa_workflow_duration_seconds",
            unit: "s",
            description: "Workflow execution duration in seconds");
    
    public static readonly Histogram<double> BookmarkResumeDuration = 
        Meter.CreateHistogram<double>(
            "elsa_bookmark_resume_duration_seconds",
            unit: "s",
            description: "Bookmark resume duration in seconds");
    
    public static readonly Histogram<double> LockWaitDuration = 
        Meter.CreateHistogram<double>(
            "elsa_locking_wait_duration_seconds",
            unit: "s",
            description: "Distributed lock wait duration in seconds");
}
```

### Using prometheus-net

```csharp
using Prometheus;

public static class ElsaMetrics
{
    // Workflow execution counters
    public static readonly Counter WorkflowsExecuted = Metrics
        .CreateCounter(
            "elsa_workflows_executed_total",
            "Total number of workflow executions",
            new CounterConfiguration
            {
                LabelNames = new[] { "status", "workflow_definition_id" }
            });
    
    public static readonly Counter BookmarksCreated = Metrics
        .CreateCounter(
            "elsa_workflows_bookmarks_created_total",
            "Total number of bookmarks created",
            new CounterConfiguration
            {
                LabelNames = new[] { "workflow_definition_id" }
            });
    
    public static readonly Counter BookmarksResumed = Metrics
        .CreateCounter(
            "elsa_workflows_bookmarks_resumed_total",
            "Total number of bookmarks resumed",
            new CounterConfiguration
            {
                LabelNames = new[] { "status", "bookmark_type" }
            });
    
    public static readonly Counter TimerFires = Metrics
        .CreateCounter(
            "elsa_scheduling_timer_fires_total",
            "Total number of timer fires",
            new CounterConfiguration
            {
                LabelNames = new[] { "status" }
            });
    
    public static readonly Counter LockAcquisitions = Metrics
        .CreateCounter(
            "elsa_locking_acquisitions_total",
            "Total number of distributed lock acquisitions",
            new CounterConfiguration
            {
                LabelNames = new[] { "status", "resource" }
            });
    
    // Gauges
    public static readonly Gauge ActiveWorkflows = Metrics
        .CreateGauge(
            "elsa_workflows_active",
            "Current number of active workflow instances");
    
    public static readonly Gauge BookmarksPending = Metrics
        .CreateGauge(
            "elsa_workflows_bookmarks_pending",
            "Current number of pending bookmarks");
    
    public static readonly Gauge ScheduledJobsPending = Metrics
        .CreateGauge(
            "elsa_scheduling_jobs_pending",
            "Current number of pending scheduled jobs");
    
    public static readonly Gauge LocksHeld = Metrics
        .CreateGauge(
            "elsa_locking_locks_held",
            "Current number of distributed locks held");
    
    // Histograms
    public static readonly Histogram WorkflowDuration = Metrics
        .CreateHistogram(
            "elsa_workflow_duration_seconds",
            "Workflow execution duration in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "workflow_definition_id" },
                Buckets = Histogram.ExponentialBuckets(0.1, 2, 10) // 0.1s to ~51s
            });
    
    public static readonly Histogram BookmarkResumeDuration = Metrics
        .CreateHistogram(
            "elsa_bookmark_resume_duration_seconds",
            "Bookmark resume duration in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.01, 2, 10) // 10ms to ~5s
            });
    
    public static readonly Histogram LockWaitDuration = Metrics
        .CreateHistogram(
            "elsa_locking_wait_duration_seconds",
            "Distributed lock wait duration in seconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 12) // 1ms to ~4s
            });
}
```

## Instrumenting Elsa Operations

### Recording Metrics in Workflow Execution

Example of instrumenting workflow execution:

```csharp
using System.Diagnostics;

public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
    string workflowDefinitionId,
    string workflowInstanceId)
{
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        var result = await _workflowRunner.ExecuteAsync(workflowDefinitionId, workflowInstanceId);
        
        // Record metrics
        ElsaMetrics.WorkflowsExecuted.Add(1, 
            new KeyValuePair<string, object>("status", result.Status.ToString()),
            new KeyValuePair<string, object>("workflow_definition_id", workflowDefinitionId));
        
        return result;
    }
    catch (Exception ex)
    {
        ElsaMetrics.WorkflowsExecuted.Add(1,
            new KeyValuePair<string, object>("status", "faulted"),
            new KeyValuePair<string, object>("workflow_definition_id", workflowDefinitionId));
        throw;
    }
    finally
    {
        stopwatch.Stop();
        ElsaMetrics.WorkflowDuration.Record(
            stopwatch.Elapsed.TotalSeconds,
            new KeyValuePair<string, object>("workflow_definition_id", workflowDefinitionId));
    }
}
```

### Recording Bookmark Metrics

```csharp
public async Task CreateBookmarkAsync(Bookmark bookmark)
{
    await _bookmarkStore.SaveAsync(bookmark);
    
    ElsaMetrics.BookmarksCreated.Add(1,
        new KeyValuePair<string, object>("workflow_definition_id", bookmark.WorkflowDefinitionId));
    
    // Update pending bookmarks gauge
    var pendingCount = await _bookmarkStore.CountPendingAsync();
    // Gauge update depends on implementation (callback or direct set)
}

public async Task<bool> ResumeBookmarkAsync(string bookmarkId)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        var result = await _workflowResumer.ResumeAsync(bookmarkId);
        
        ElsaMetrics.BookmarksResumed.Add(1,
            new KeyValuePair<string, object>("status", result ? "success" : "not_found"));
        
        return result;
    }
    catch (Exception)
    {
        ElsaMetrics.BookmarksResumed.Add(1,
            new KeyValuePair<string, object>("status", "failed"));
        throw;
    }
    finally
    {
        stopwatch.Stop();
        ElsaMetrics.BookmarkResumeDuration.Record(stopwatch.Elapsed.TotalSeconds);
    }
}
```

### Recording Lock Metrics

```csharp
public async Task<bool> AcquireLockAsync(string resource, TimeSpan timeout)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        var acquired = await _lockProvider.TryAcquireAsync(resource, timeout);
        
        ElsaMetrics.LockAcquisitions.Add(1,
            new KeyValuePair<string, object>("status", acquired ? "success" : "timeout"),
            new KeyValuePair<string, object>("resource", SanitizeResourceName(resource)));
        
        if (!acquired)
        {
            ElsaMetrics.LockTimeouts.Add(1);
        }
        
        return acquired;
    }
    finally
    {
        stopwatch.Stop();
        ElsaMetrics.LockWaitDuration.Record(stopwatch.Elapsed.TotalSeconds);
    }
}

private string SanitizeResourceName(string resource)
{
    // Avoid high cardinality - use resource type instead of instance ID
    // E.g., "workflow-instance" instead of "workflow-instance-abc123"
    return resource.Split(':').FirstOrDefault() ?? "unknown";
}
```

## Prometheus Scraping Configuration

### Static Scrape Configuration

Add to `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'elsa-server'
    scrape_interval: 15s
    scrape_timeout: 10s
    metrics_path: '/metrics'
    static_configs:
      - targets:
          - 'elsa-server-1:5000'
          - 'elsa-server-2:5000'
          - 'elsa-server-3:5000'
        labels:
          environment: 'production'
          cluster: 'us-east-1'
```

### Kubernetes ServiceMonitor

For Prometheus Operator in Kubernetes:

```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: elsa-server
  namespace: elsa-workflows
  labels:
    app: elsa-server
spec:
  selector:
    matchLabels:
      app: elsa-server
  endpoints:
    - port: http
      path: /metrics
      interval: 15s
      scrapeTimeout: 10s
```

### Kubernetes Pod Annotations

For Prometheus scraping via pod annotations:

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: elsa-server
  annotations:
    prometheus.io/scrape: "true"
    prometheus.io/port: "5000"
    prometheus.io/path: "/metrics"
spec:
  # ... pod spec
```

## Sample PromQL Queries

### Workflow Execution Rate

```promql
# Workflows executed per second
rate(elsa_workflows_executed_total[5m])

# Workflows executed per minute
rate(elsa_workflows_executed_total[5m]) * 60

# By status
rate(elsa_workflows_executed_total{status="completed"}[5m])
rate(elsa_workflows_executed_total{status="faulted"}[5m])
```

### Workflow Success Rate

```promql
# Success rate over 5 minutes
sum(rate(elsa_workflows_executed_total{status="completed"}[5m]))
  /
sum(rate(elsa_workflows_executed_total[5m]))

# Failure rate
sum(rate(elsa_workflows_executed_total{status="faulted"}[5m]))
  /
sum(rate(elsa_workflows_executed_total[5m]))
```

### Workflow Duration Percentiles

```promql
# P50 (median) workflow duration
histogram_quantile(0.50, 
  rate(elsa_workflow_duration_seconds_bucket[5m]))

# P95 workflow duration
histogram_quantile(0.95, 
  rate(elsa_workflow_duration_seconds_bucket[5m]))

# P99 workflow duration
histogram_quantile(0.99, 
  rate(elsa_workflow_duration_seconds_bucket[5m]))
```

### Bookmark Metrics

```promql
# Bookmark resume rate
rate(elsa_workflows_bookmarks_resumed_total[5m])

# Bookmark resume success rate
sum(rate(elsa_workflows_bookmarks_resumed_total{status="success"}[5m]))
  /
sum(rate(elsa_workflows_bookmarks_resumed_total[5m]))

# Current bookmark backlog
elsa_workflows_bookmarks_pending

# Bookmark backlog growth rate
deriv(elsa_workflows_bookmarks_pending[10m])
```

### Lock Metrics

```promql
# Lock timeout rate
rate(elsa_locking_acquisitions_total{status="timeout"}[5m])

# Lock success rate
sum(rate(elsa_locking_acquisitions_total{status="success"}[5m]))
  /
sum(rate(elsa_locking_acquisitions_total[5m]))

# P95 lock wait time
histogram_quantile(0.95,
  rate(elsa_locking_wait_duration_seconds_bucket[5m]))
```

## Prometheus Alerting Rules

Define alerting rules in Prometheus configuration.

### alerts.yml

```yaml
groups:
  - name: elsa_workflows
    interval: 30s
    rules:
      # High workflow failure rate
      - alert: HighWorkflowFailureRate
        expr: |
          (
            sum(rate(elsa_workflows_executed_total{status="faulted"}[5m]))
              /
            sum(rate(elsa_workflows_executed_total[5m]))
          ) > 0.05
        for: 5m
        labels:
          severity: critical
          component: elsa-workflows
        annotations:
          summary: "High workflow failure rate detected"
          description: "{{ $value | humanizePercentage }} of workflows are failing (threshold: 5%)"
          runbook_url: "https://wiki.example.com/runbooks/elsa-high-failure-rate"
      
      # Workflow execution stalled
      - alert: WorkflowExecutionStalled
        expr: |
          rate(elsa_workflows_executed_total[5m]) == 0
        for: 10m
        labels:
          severity: warning
          component: elsa-workflows
        annotations:
          summary: "No workflows executed in last 10 minutes"
          description: "Workflow execution may be stalled or no workflows are being triggered"
      
      # High P95 workflow duration
      - alert: HighWorkflowLatency
        expr: |
          histogram_quantile(0.95,
            rate(elsa_workflow_duration_seconds_bucket[5m])
          ) > 30
        for: 10m
        labels:
          severity: warning
          component: elsa-workflows
        annotations:
          summary: "High workflow execution latency"
          description: "P95 workflow duration is {{ $value }}s (threshold: 30s)"
      
      # Lock timeout surge
      - alert: LockTimeoutSurge
        expr: |
          rate(elsa_locking_acquisitions_total{status="timeout"}[1m]) > 10
        for: 2m
        labels:
          severity: critical
          component: elsa-locking
        annotations:
          summary: "High rate of lock acquisition timeouts"
          description: "{{ $value }} lock timeouts per second (threshold: 10/s)"
          runbook_url: "https://wiki.example.com/runbooks/elsa-lock-timeouts"
      
      # Bookmark backlog growing
      - alert: BookmarkBacklogGrowing
        expr: |
          elsa_workflows_bookmarks_pending > 1000
            and
          deriv(elsa_workflows_bookmarks_pending[10m]) > 0
        for: 15m
        labels:
          severity: warning
          component: elsa-bookmarks
        annotations:
          summary: "Bookmark backlog is growing"
          description: "Current backlog: {{ $value }} bookmarks and increasing"
          runbook_url: "https://wiki.example.com/runbooks/elsa-bookmark-backlog"
      
      # Timer fire failures
      - alert: TimerFireFailures
        expr: |
          (
            sum(rate(elsa_scheduling_timer_fires_total{status="failed"}[5m]))
              /
            sum(rate(elsa_scheduling_timer_fires_total[5m]))
          ) > 0.01
        for: 5m
        labels:
          severity: warning
          component: elsa-scheduling
        annotations:
          summary: "Timer fires are failing"
          description: "{{ $value | humanizePercentage }} of timer fires are failing (threshold: 1%)"
      
      # No bookmark resumes
      - alert: NoBookmarkResumes
        expr: |
          rate(elsa_workflows_bookmarks_resumed_total[5m]) == 0
            and
          elsa_workflows_bookmarks_pending > 0
        for: 10m
        labels:
          severity: warning
          component: elsa-bookmarks
        annotations:
          summary: "Bookmarks not being resumed"
          description: "{{ $value }} pending bookmarks but no resume activity detected"
      
      # High lock wait time
      - alert: HighLockWaitTime
        expr: |
          histogram_quantile(0.95,
            rate(elsa_locking_wait_duration_seconds_bucket[5m])
          ) > 1.0
        for: 10m
        labels:
          severity: warning
          component: elsa-locking
        annotations:
          summary: "High lock acquisition wait time"
          description: "P95 lock wait time is {{ $value }}s (threshold: 1s)"
```

### Alertmanager Configuration

Route alerts to appropriate channels:

```yaml
# alertmanager.yml
route:
  receiver: 'default'
  group_by: ['alertname', 'cluster', 'environment']
  group_wait: 30s
  group_interval: 5m
  repeat_interval: 4h
  routes:
    - match:
        severity: critical
      receiver: 'pagerduty'
    - match:
        severity: warning
      receiver: 'slack'

receivers:
  - name: 'default'
    slack_configs:
      - api_url: 'https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK'
        channel: '#elsa-alerts'
        title: '{{ .GroupLabels.alertname }}'
        text: '{{ range .Alerts }}{{ .Annotations.description }}{{ end }}'
  
  - name: 'pagerduty'
    pagerduty_configs:
      - service_key: 'YOUR_PAGERDUTY_SERVICE_KEY'
        description: '{{ .GroupLabels.alertname }}: {{ .GroupLabels.cluster }}'
  
  - name: 'slack'
    slack_configs:
      - api_url: 'https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK'
        channel: '#elsa-warnings'
```

## Best Practices

### Metric Labels

✅ **Do**:
- Use finite, low-cardinality labels (status, type, environment)
- Sanitize resource names to categories (e.g., "workflow-instance" not "workflow-instance-12345")
- Include workflow definition ID (finite set of definitions)

❌ **Don't**:
- Use workflow instance IDs as labels (unbounded cardinality)
- Use correlation IDs as labels
- Include timestamps or UUIDs as labels

### Histogram Buckets

Choose appropriate buckets for your SLAs:

```csharp
// For sub-second operations (bookmark resumes)
Histogram.ExponentialBuckets(0.01, 2, 10) // 10ms to ~5s

// For workflow execution (seconds to minutes)
Histogram.ExponentialBuckets(0.1, 2, 12) // 100ms to ~400s

// Custom buckets for specific SLAs
new[] { 0.1, 0.5, 1.0, 5.0, 10.0, 30.0, 60.0 } // Explicit SLA boundaries
```

### Scraping Frequency

- **Production**: 15-30 seconds (balances freshness with load)
- **Development**: 5-10 seconds (faster feedback)
- **High-traffic**: Consider longer intervals (60s) with aggregation

### Metric Retention

Configure Prometheus retention based on needs:

```yaml
# prometheus.yml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

# Command-line flags
# --storage.tsdb.retention.time=30d  # Keep 30 days
# --storage.tsdb.retention.size=50GB # Or 50GB limit
```

## Related Resources

- [OpenTelemetry Setup](otel-setup.md) - OTel configuration with Prometheus exporter
- [Grafana Dashboard Guide](grafana-dashboard.md) - Visualizing Prometheus metrics
- [Main Monitoring Guide](../README.md) - Comprehensive monitoring overview

## External References

- [Prometheus Documentation](https://prometheus.io/docs/)
- [PromQL Basics](https://prometheus.io/docs/prometheus/latest/querying/basics/)
- [prometheus-net Library](https://github.com/prometheus-net/prometheus-net)
- [OpenTelemetry Metrics](https://opentelemetry.io/docs/specs/otel/metrics/)
