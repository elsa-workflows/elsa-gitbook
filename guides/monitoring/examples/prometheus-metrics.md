# Prometheus Metrics for Elsa Workflows

This guide shows how to expose workflow metrics to Prometheus.

> **Important**: All Elsa workflow metrics shown in this guide are **examples that require custom instrumentation code**. Elsa does not automatically expose workflow metrics—there are no built-in meters. You must implement the metrics as described in the [main Monitoring guide](../README.md#implementing-custom-metrics).

## Prerequisites

1. Implement custom metrics using `System.Diagnostics.Metrics`
2. Register your meter with OpenTelemetry
3. Configure the Prometheus exporter

## Example Metrics Implementation

First, create your metrics class (as described in the main guide):

```csharp
using System.Diagnostics.Metrics;

public static class ElsaMetrics
{
    public static readonly string MeterName = "YourApp.Elsa.Workflows";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> WorkflowsStarted = 
        Meter.CreateCounter<long>("elsa_workflows_started_total");

    public static readonly Counter<long> WorkflowsCompleted = 
        Meter.CreateCounter<long>("elsa_workflows_completed_total");

    public static readonly Counter<long> WorkflowsFaulted = 
        Meter.CreateCounter<long>("elsa_workflows_faulted_total");

    public static readonly Histogram<double> WorkflowDuration = 
        Meter.CreateHistogram<double>("elsa_workflow_duration_seconds");
}
```

## OpenTelemetry Configuration with Prometheus

```csharp
using OpenTelemetry.Metrics;

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("YourApp.Elsa.Workflows") // Your custom meter
            .AddRuntimeInstrumentation()        // Optional: .NET runtime metrics
            .AddPrometheusExporter();
    });

// ...

var app = builder.Build();
app.MapPrometheusScrapingEndpoint(); // Exposes /metrics endpoint
```

## Example Prometheus Output

After implementing custom instrumentation, your `/metrics` endpoint will include:

```prometheus
# HELP elsa_workflows_started_total Total number of workflows started
# TYPE elsa_workflows_started_total counter
elsa_workflows_started_total{workflow_name="OrderProcessing"} 1523
elsa_workflows_started_total{workflow_name="UserOnboarding"} 892

# HELP elsa_workflows_completed_total Total number of workflows completed
# TYPE elsa_workflows_completed_total counter
elsa_workflows_completed_total{workflow_name="OrderProcessing"} 1498
elsa_workflows_completed_total{workflow_name="UserOnboarding"} 887

# HELP elsa_workflows_faulted_total Total number of workflows that faulted
# TYPE elsa_workflows_faulted_total counter
elsa_workflows_faulted_total{workflow_name="OrderProcessing"} 25
elsa_workflows_faulted_total{workflow_name="UserOnboarding"} 5

# HELP elsa_workflow_duration_seconds Duration of workflow executions
# TYPE elsa_workflow_duration_seconds histogram
elsa_workflow_duration_seconds_bucket{workflow_name="OrderProcessing",le="0.1"} 120
elsa_workflow_duration_seconds_bucket{workflow_name="OrderProcessing",le="0.5"} 890
elsa_workflow_duration_seconds_bucket{workflow_name="OrderProcessing",le="1"} 1200
elsa_workflow_duration_seconds_bucket{workflow_name="OrderProcessing",le="5"} 1480
elsa_workflow_duration_seconds_bucket{workflow_name="OrderProcessing",le="+Inf"} 1498
elsa_workflow_duration_seconds_sum{workflow_name="OrderProcessing"} 2847.5
elsa_workflow_duration_seconds_count{workflow_name="OrderProcessing"} 1498
```

## Prometheus Scrape Configuration

Add to your `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'elsa-workflows'
    scrape_interval: 15s
    static_configs:
      - targets: ['elsa-app:8080']
    metrics_path: '/metrics'
```

For Kubernetes with service discovery:

```yaml
scrape_configs:
  - job_name: 'kubernetes-pods'
    kubernetes_sd_configs:
      - role: pod
    relabel_configs:
      - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
        action: keep
        regex: true
      - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_path]
        action: replace
        target_label: __metrics_path__
        regex: (.+)
```

## Example PromQL Queries

These queries assume you have implemented the custom metrics:

### Workflow Success Rate
```promql
sum(rate(elsa_workflows_completed_total[5m])) 
/ sum(rate(elsa_workflows_started_total[5m])) * 100
```

### Workflow Fault Rate by Type
```promql
sum by (workflow_name) (rate(elsa_workflows_faulted_total[5m]))
```

### 95th Percentile Workflow Duration
```promql
histogram_quantile(0.95, 
  sum by (le, workflow_name) (rate(elsa_workflow_duration_seconds_bucket[5m])))
```

### Workflows per Second
```promql
sum(rate(elsa_workflows_started_total[1m]))
```

## Alerting Rules

Example Prometheus alerting rules (all metrics are user-implemented):

```yaml
groups:
  - name: elsa-alerts
    rules:
      - alert: ElsaHighFaultRate
        expr: |
          sum(rate(elsa_workflows_faulted_total[5m])) 
          / sum(rate(elsa_workflows_started_total[5m])) > 0.05
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Elsa workflow fault rate above 5%"

      - alert: ElsaNoWorkflowsRunning
        expr: sum(rate(elsa_workflows_started_total[5m])) == 0
        for: 15m
        labels:
          severity: info
        annotations:
          summary: "No Elsa workflows have started in 15 minutes"
```

## Related

- [Main Monitoring Guide](../README.md)
- [OpenTelemetry Setup](otel-setup.md)
- [Grafana Dashboard](grafana-dashboard.md)
