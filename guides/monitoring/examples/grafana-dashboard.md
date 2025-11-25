# Grafana Dashboard for Elsa Workflows

This guide provides example Grafana dashboard panels for monitoring Elsa workflows.

> **Note**: All panels in this guide assume you have implemented the custom metrics described in the [main Monitoring guide](../README.md#implementing-custom-metrics). Elsa does not provide built-in metrics—you must instrument your application code first.

## Dashboard Overview

A comprehensive Elsa monitoring dashboard should include:

1. **Overview Row**: Key workflow KPIs
2. **Execution Row**: Workflow and activity execution metrics
3. **Error Row**: Fault tracking and error rates
4. **Performance Row**: Duration histograms and latency

---

## Panel Definitions

### 1. Workflows Started (Stat Panel)

```json
{
  "title": "Workflows Started (1h)",
  "type": "stat",
  "targets": [
    {
      "expr": "sum(increase(elsa_workflows_started_total[1h]))",
      "legendFormat": "Started"
    }
  ],
  "options": {
    "colorMode": "value",
    "graphMode": "area"
  }
}
```

### 2. Workflows Completed (Stat Panel)

```json
{
  "title": "Workflows Completed (1h)",
  "type": "stat",
  "targets": [
    {
      "expr": "sum(increase(elsa_workflows_completed_total[1h]))",
      "legendFormat": "Completed"
    }
  ],
  "options": {
    "colorMode": "value",
    "graphMode": "area"
  }
}
```

### 3. Workflow Fault Rate (Gauge Panel)

```json
{
  "title": "Fault Rate",
  "type": "gauge",
  "targets": [
    {
      "expr": "sum(rate(elsa_workflows_faulted_total[5m])) / sum(rate(elsa_workflows_started_total[5m])) * 100",
      "legendFormat": "Fault %"
    }
  ],
  "options": {
    "thresholds": {
      "steps": [
        { "value": 0, "color": "green" },
        { "value": 5, "color": "yellow" },
        { "value": 10, "color": "red" }
      ]
    }
  }
}
```

### 4. Workflow Throughput (Time Series Panel)

```json
{
  "title": "Workflow Throughput",
  "type": "timeseries",
  "targets": [
    {
      "expr": "sum(rate(elsa_workflows_started_total[1m]))",
      "legendFormat": "Started/sec"
    },
    {
      "expr": "sum(rate(elsa_workflows_completed_total[1m]))",
      "legendFormat": "Completed/sec"
    },
    {
      "expr": "sum(rate(elsa_workflows_faulted_total[1m]))",
      "legendFormat": "Faulted/sec"
    }
  ]
}
```

### 5. Workflow Duration Heatmap

```json
{
  "title": "Workflow Duration Distribution",
  "type": "heatmap",
  "targets": [
    {
      "expr": "sum(rate(elsa_workflow_duration_seconds_bucket[5m])) by (le)",
      "legendFormat": "{{le}}",
      "format": "heatmap"
    }
  ]
}
```

### 6. 95th Percentile Duration by Workflow (Time Series)

```json
{
  "title": "P95 Workflow Duration",
  "type": "timeseries",
  "targets": [
    {
      "expr": "histogram_quantile(0.95, sum by (le, workflow_name) (rate(elsa_workflow_duration_seconds_bucket[5m])))",
      "legendFormat": "{{workflow_name}}"
    }
  ],
  "fieldConfig": {
    "defaults": {
      "unit": "s"
    }
  }
}
```

### 7. Active Workflows (Gauge)

```json
{
  "title": "Active Workflows",
  "type": "gauge",
  "targets": [
    {
      "expr": "sum(elsa_workflows_active)",
      "legendFormat": "Active"
    }
  ]
}
```

### 8. Workflows by Type (Pie Chart)

```json
{
  "title": "Workflows by Type (24h)",
  "type": "piechart",
  "targets": [
    {
      "expr": "sum by (workflow_name) (increase(elsa_workflows_started_total[24h]))",
      "legendFormat": "{{workflow_name}}"
    }
  ]
}
```

### 9. Faults by Workflow (Bar Gauge)

```json
{
  "title": "Faults by Workflow (1h)",
  "type": "bargauge",
  "targets": [
    {
      "expr": "sum by (workflow_name) (increase(elsa_workflows_faulted_total[1h]))",
      "legendFormat": "{{workflow_name}}"
    }
  ],
  "options": {
    "orientation": "horizontal",
    "displayMode": "gradient"
  }
}
```

### 10. Bookmarks Pending (Time Series)

```json
{
  "title": "Pending Bookmarks",
  "type": "timeseries",
  "targets": [
    {
      "expr": "sum(elsa_bookmarks_pending)",
      "legendFormat": "Pending"
    },
    {
      "expr": "sum(rate(elsa_bookmarks_created_total[5m]))",
      "legendFormat": "Created/sec"
    },
    {
      "expr": "sum(rate(elsa_bookmarks_resumed_total[5m]))",
      "legendFormat": "Resumed/sec"
    }
  ]
}
```

---

## Sample Dashboard JSON

<!-- Placeholder for complete dashboard JSON export -->

*[Placeholder: Complete Grafana dashboard JSON export]*

You can create a dashboard by:
1. Creating a new dashboard in Grafana
2. Adding panels with the configurations above
3. Adjusting time ranges and refresh intervals as needed

---

## Dashboard Variables

Add these variables for filtering:

### Workflow Name Variable

```
Name: workflow_name
Type: Query
Query: label_values(elsa_workflows_started_total, workflow_name)
Multi-value: true
Include All: true
```

### Environment Variable

```
Name: environment
Type: Query  
Query: label_values(elsa_workflows_started_total, environment)
```

Use in queries with: `{workflow_name=~"$workflow_name"}`

---

## Alert Configuration in Grafana

Example alert rule for high fault rate:

```yaml
name: High Workflow Fault Rate
condition: B
data:
  - refId: A
    relativeTimeRange:
      from: 300
      to: 0
    model:
      expr: |
        sum(rate(elsa_workflows_faulted_total[5m])) 
        / sum(rate(elsa_workflows_started_total[5m]))
  - refId: B
    relativeTimeRange:
      from: 0
      to: 0
    model:
      conditions:
        - evaluator:
            type: gt
            params: [0.1]
          operator:
            type: and
          reducer:
            type: last
noDataState: NoData
execErrState: Error
for: 5m
annotations:
  summary: Workflow fault rate exceeds 10%
labels:
  severity: warning
```

---

## Related

- [Main Monitoring Guide](../README.md)
- [Prometheus Metrics](prometheus-metrics.md)
- [OpenTelemetry Setup](otel-setup.md)
