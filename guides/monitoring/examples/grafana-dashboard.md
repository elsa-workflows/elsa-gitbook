# Grafana Dashboard for Elsa Workflows

This guide provides panel suggestions, PromQL queries, and configuration recommendations for creating Grafana dashboards to visualize Elsa Workflows metrics and traces.

## Prerequisites

- Grafana instance running (v9.0 or later recommended)
- Prometheus data source configured in Grafana
- Elsa Server exposing Prometheus metrics (see [Prometheus Metrics Setup](prometheus-metrics.md))
- Optional: Tempo or Jaeger data source for distributed traces

## Dashboard Overview

A comprehensive Elsa Workflows dashboard should include:

1. **Executive Overview** - High-level KPIs and health indicators
2. **Workflow Execution** - Execution rates, durations, and success metrics
3. **Bookmarks & Scheduling** - Bookmark processing and timer operations
4. **Distributed Locking** - Lock acquisition and timeout metrics
5. **Infrastructure** - System resource utilization
6. **Traces** - Distributed trace visualization (if trace backend configured)

## Dashboard Variables

Define dashboard variables for filtering and drill-down:

### Variable Definitions

```json
{
  "templating": {
    "list": [
      {
        "name": "datasource",
        "type": "datasource",
        "query": "prometheus",
        "current": {
          "text": "Prometheus",
          "value": "Prometheus"
        }
      },
      {
        "name": "environment",
        "type": "query",
        "datasource": "${datasource}",
        "query": "label_values(elsa_workflows_executed_total, environment)",
        "current": {
          "text": "All",
          "value": "$__all"
        },
        "multi": true,
        "includeAll": true
      },
      {
        "name": "cluster",
        "type": "query",
        "datasource": "${datasource}",
        "query": "label_values(elsa_workflows_executed_total{environment=~\"$environment\"}, cluster)",
        "current": {
          "text": "All",
          "value": "$__all"
        },
        "multi": true,
        "includeAll": true
      },
      {
        "name": "instance",
        "type": "query",
        "datasource": "${datasource}",
        "query": "label_values(elsa_workflows_executed_total{environment=~\"$environment\",cluster=~\"$cluster\"}, instance)",
        "current": {
          "text": "All",
          "value": "$__all"
        },
        "multi": true,
        "includeAll": true
      },
      {
        "name": "workflow_definition_id",
        "type": "query",
        "datasource": "${datasource}",
        "query": "label_values(elsa_workflows_executed_total, workflow_definition_id)",
        "current": {
          "text": "All",
          "value": "$__all"
        },
        "multi": true,
        "includeAll": true
      }
    ]
  }
}
```

## Panel Configurations

### 1. Executive Overview Row

#### Panel: Total Workflows Executed (Stat)

**PromQL**:
```promql
sum(increase(elsa_workflows_executed_total{environment=~"$environment",cluster=~"$cluster"}[24h]))
```

**Configuration**:
- Visualization: Stat
- Unit: short (number)
- Color: Value-based coloring (green)
- Sparkline: Show trend

#### Panel: Current Success Rate (Gauge)

**PromQL**:
```promql
sum(rate(elsa_workflows_executed_total{status="completed",environment=~"$environment",cluster=~"$cluster"}[5m]))
  /
sum(rate(elsa_workflows_executed_total{environment=~"$environment",cluster=~"$cluster"}[5m]))
```

**Configuration**:
- Visualization: Gauge
- Unit: percentunit (0.0-1.0)
- Thresholds: Red < 0.95, Yellow 0.95-0.99, Green > 0.99
- Min: 0, Max: 1

#### Panel: Active Workflow Instances (Stat)

**PromQL**:
```promql
sum(elsa_workflows_active{environment=~"$environment",cluster=~"$cluster"})
```

**Configuration**:
- Visualization: Stat
- Unit: short
- Color: Value-based (blue)

#### Panel: Bookmark Backlog (Stat)

**PromQL**:
```promql
sum(elsa_workflows_bookmarks_pending{environment=~"$environment",cluster=~"$cluster"})
```

**Configuration**:
- Visualization: Stat
- Unit: short
- Thresholds: Green < 100, Yellow 100-1000, Red > 1000
- Sparkline: Show trend

### 2. Workflow Execution Row

#### Panel: Workflow Execution Rate (Time Series)

**PromQL**:
```promql
# Total execution rate
sum(rate(elsa_workflows_executed_total{environment=~"$environment",cluster=~"$cluster"}[5m])) by (status)
```

**Configuration**:
- Visualization: Time series
- Unit: ops (operations per second)
- Legend: Show, display as table
- Series overrides:
  - status="completed": Green, line + points
  - status="faulted": Red, line + points
  - status="cancelled": Orange, line + points

#### Panel: Workflow Success/Failure Split (Time Series)

**PromQL**:
```promql
# Success rate
sum(rate(elsa_workflows_executed_total{status="completed",environment=~"$environment",cluster=~"$cluster"}[5m]))

# Failure rate
sum(rate(elsa_workflows_executed_total{status="faulted",environment=~"$environment",cluster=~"$cluster"}[5m]))
```

**Configuration**:
- Visualization: Time series
- Unit: ops
- Stack: Normal (stacked area)
- Fill opacity: 50%

#### Panel: Workflow Execution Duration - P50/P95/P99 (Time Series)

**PromQL**:
```promql
# P50 (median)
histogram_quantile(0.50, 
  sum(rate(elsa_workflow_duration_seconds_bucket{environment=~"$environment",cluster=~"$cluster"}[5m])) by (le))

# P95
histogram_quantile(0.95, 
  sum(rate(elsa_workflow_duration_seconds_bucket{environment=~"$environment",cluster=~"$cluster"}[5m])) by (le))

# P99
histogram_quantile(0.99, 
  sum(rate(elsa_workflow_duration_seconds_bucket{environment=~"$environment",cluster=~"$cluster"}[5m])) by (le))
```

**Configuration**:
- Visualization: Time series
- Unit: s (seconds)
- Legend: P50, P95, P99
- Y-axis: Right side for SLA line (e.g., 30s)

#### Panel: Top 10 Slowest Workflows (Bar Gauge)

**PromQL**:
```promql
topk(10,
  histogram_quantile(0.95,
    sum(rate(elsa_workflow_duration_seconds_bucket{environment=~"$environment",cluster=~"$cluster"}[5m])) 
    by (workflow_definition_id, le))
)
```

**Configuration**:
- Visualization: Bar gauge
- Unit: s
- Orientation: Horizontal
- Display mode: Gradient

#### Panel: Workflow Execution Duration Heatmap (Heatmap)

**PromQL**:
```promql
sum(increase(elsa_workflow_duration_seconds_bucket{environment=~"$environment",cluster=~"$cluster"}[$__interval])) by (le)
```

**Configuration**:
- Visualization: Heatmap
- Unit: s
- Color scheme: Spectral
- Data format: Time series buckets

### 3. Bookmarks & Scheduling Row

#### Panel: Bookmark Resume Rate (Time Series)

**PromQL**:
```promql
sum(rate(elsa_workflows_bookmarks_resumed_total{environment=~"$environment",cluster=~"$cluster"}[5m])) by (status)
```

**Configuration**:
- Visualization: Time series
- Unit: ops
- Legend: success, not_found, failed

#### Panel: Bookmark Backlog Over Time (Time Series)

**PromQL**:
```promql
sum(elsa_workflows_bookmarks_pending{environment=~"$environment",cluster=~"$cluster"})
```

**Configuration**:
- Visualization: Time series
- Unit: short
- Fill: Below line with gradient
- Thresholds: Yellow at 100, Red at 1000

#### Panel: Bookmark Backlog Growth Rate (Stat)

**PromQL**:
```promql
deriv(
  sum(elsa_workflows_bookmarks_pending{environment=~"$environment",cluster=~"$cluster"})[10m:]
)
```

**Configuration**:
- Visualization: Stat
- Unit: bookmarks/min
- Thresholds: Green < 0, Yellow 0-10, Red > 10
- Show: Value and trend

#### Panel: Timer Fires and Failures (Time Series)

**PromQL**:
```promql
# Successful timer fires
sum(rate(elsa_scheduling_timer_fires_total{status="success",environment=~"$environment",cluster=~"$cluster"}[5m]))

# Failed timer fires
sum(rate(elsa_scheduling_timer_fires_total{status="failed",environment=~"$environment",cluster=~"$cluster"}[5m]))
```

**Configuration**:
- Visualization: Time series
- Unit: ops
- Stack: Normal
- Series colors: Green (success), Red (failed)

#### Panel: Scheduled Jobs Pending (Gauge)

**PromQL**:
```promql
sum(elsa_scheduling_jobs_pending{environment=~"$environment",cluster=~"$cluster"})
```

**Configuration**:
- Visualization: Gauge
- Unit: short
- Thresholds: Green < 50, Yellow 50-100, Red > 100
- Min: 0, Max: 200

#### Panel: Bookmark Resume Duration - P95 (Time Series)

**PromQL**:
```promql
histogram_quantile(0.95,
  sum(rate(elsa_bookmark_resume_duration_seconds_bucket{environment=~"$environment",cluster=~"$cluster"}[5m])) by (le))
```

**Configuration**:
- Visualization: Time series
- Unit: s
- Y-axis: SLA line at 5s

### 4. Distributed Locking Row

#### Panel: Lock Acquisition Rate by Status (Time Series)

**PromQL**:
```promql
sum(rate(elsa_locking_acquisitions_total{environment=~"$environment",cluster=~"$cluster"}[5m])) by (status)
```

**Configuration**:
- Visualization: Time series
- Unit: ops
- Stack: Normal
- Series: success (green), timeout (red), failed (orange)

#### Panel: Lock Timeout Rate (Stat)

**PromQL**:
```promql
sum(rate(elsa_locking_acquisitions_total{status="timeout",environment=~"$environment",cluster=~"$cluster"}[5m]))
```

**Configuration**:
- Visualization: Stat
- Unit: ops
- Thresholds: Green < 1, Yellow 1-10, Red > 10
- Sparkline: Show

#### Panel: Lock Success Rate (Gauge)

**PromQL**:
```promql
sum(rate(elsa_locking_acquisitions_total{status="success",environment=~"$environment",cluster=~"$cluster"}[5m]))
  /
sum(rate(elsa_locking_acquisitions_total{environment=~"$environment",cluster=~"$cluster"}[5m]))
```

**Configuration**:
- Visualization: Gauge
- Unit: percentunit
- Thresholds: Red < 0.95, Yellow 0.95-0.99, Green > 0.99
- Min: 0, Max: 1

#### Panel: Lock Wait Duration - P95 (Time Series)

**PromQL**:
```promql
histogram_quantile(0.95,
  sum(rate(elsa_locking_wait_duration_seconds_bucket{environment=~"$environment",cluster=~"$cluster"}[5m])) by (le))
```

**Configuration**:
- Visualization: Time series
- Unit: s
- Y-axis: SLA line at 1s

#### Panel: Current Locks Held (Stat)

**PromQL**:
```promql
sum(elsa_locking_locks_held{environment=~"$environment",cluster=~"$cluster"})
```

**Configuration**:
- Visualization: Stat
- Unit: short
- Color: Blue

### 5. Infrastructure Row

#### Panel: CPU Usage (Time Series)

**PromQL**:
```promql
sum(rate(process_cpu_seconds_total{environment=~"$environment",cluster=~"$cluster"}[5m])) by (instance) * 100
```

**Configuration**:
- Visualization: Time series
- Unit: percent (0-100)
- Thresholds: Yellow at 70%, Red at 90%

#### Panel: Memory Usage (Time Series)

**PromQL**:
```promql
sum(process_working_set_bytes{environment=~"$environment",cluster=~"$cluster"}) by (instance) / 1024 / 1024
```

**Configuration**:
- Visualization: Time series
- Unit: MiB
- Fill: Below line

#### Panel: HTTP Request Rate (Time Series)

**PromQL**:
```promql
sum(rate(http_server_requests_total{environment=~"$environment",cluster=~"$cluster"}[5m])) by (method, endpoint)
```

**Configuration**:
- Visualization: Time series
- Unit: reqps (requests per second)
- Legend: Show

#### Panel: HTTP Request Duration - P95 (Time Series)

**PromQL**:
```promql
histogram_quantile(0.95,
  sum(rate(http_server_request_duration_seconds_bucket{environment=~"$environment",cluster=~"$cluster"}[5m])) by (le))
```

**Configuration**:
- Visualization: Time series
- Unit: s

#### Panel: Database Connection Pool (Time Series)

**PromQL**:
```promql
# Active connections
sum(dotnet_db_connections_active{environment=~"$environment",cluster=~"$cluster"})

# Idle connections
sum(dotnet_db_connections_idle{environment=~"$environment",cluster=~"$cluster"})
```

**Configuration**:
- Visualization: Time series
- Unit: short
- Stack: Normal

#### Panel: GC Collections (Time Series)

**PromQL**:
```promql
sum(rate(dotnet_gc_collections_total{environment=~"$environment",cluster=~"$cluster"}[5m])) by (generation)
```

**Configuration**:
- Visualization: Time series
- Unit: ops
- Legend: Gen0, Gen1, Gen2

### 6. Traces Row (Optional)

#### Panel: Trace Links (Table)

**Configuration**:
- Visualization: Table (or custom panel plugin)
- Data source: Tempo or Jaeger
- Links to trace viewer with workflow instance ID filter

**Note**: This requires a trace data source configured and may use a custom panel plugin for deep linking.

## Alert Annotations

Add alert annotations to visualize when alerts fired:

**Configuration**:
```json
{
  "annotations": {
    "list": [
      {
        "datasource": "Prometheus",
        "enable": true,
        "expr": "ALERTS{alertname=~\".*Elsa.*\"}",
        "iconColor": "red",
        "name": "Elsa Alerts",
        "tagKeys": "alertname,severity",
        "titleFormat": "{{ alertname }}",
        "textFormat": "{{ annotations.description }}"
      }
    ]
  }
}
```

## Dashboard JSON Model Placeholder

A complete dashboard JSON export would be several thousand lines. Here's the structural outline:

```json
{
  "dashboard": {
    "title": "Elsa Workflows Monitoring",
    "uid": "elsa-workflows-main",
    "tags": ["elsa", "workflows"],
    "timezone": "browser",
    "schemaVersion": 38,
    "version": 1,
    "refresh": "30s",
    "time": {
      "from": "now-1h",
      "to": "now"
    },
    "templating": {
      "list": [
        /* Variables defined above */
      ]
    },
    "panels": [
      /* Panels defined above, organized in rows */
    ],
    "annotations": {
      /* Alert annotations defined above */
    }
  }
}
```

**To Export Your Dashboard**:
1. Build the dashboard in Grafana UI using panels above
2. Click Dashboard Settings (gear icon)
3. Select "JSON Model" from left menu
4. Copy the JSON and save to version control
5. Share with team or provision via Grafana provisioning

## Screenshot Placeholders

**Note**: This documentation does not include screenshot images. After creating your dashboard, take screenshots for team documentation:

1. **Overview Dashboard** - Full dashboard view showing all panels
2. **Workflow Execution Details** - Close-up of execution rate and duration panels
3. **Lock Timeout Alert** - Example of lock timeout spike with alert annotation
4. **Bookmark Backlog Growth** - Visualization of growing backlog with trend
5. **Trace Drill-Down** - Example of clicking from panel to trace viewer

Save screenshots in your team wiki or internal documentation repository.

## Dashboard Organization Tips

### Panel Sizing
- **Stat/Gauge panels**: 4-6 columns wide, 4-5 rows tall
- **Time series panels**: 12-24 columns wide, 8-10 rows tall
- **Tables**: 24 columns wide, 10-12 rows tall
- **Heatmaps**: 24 columns wide, 8-10 rows tall

### Row Organization
1. Executive Overview (1-2 rows)
2. Workflow Execution (2-3 rows)
3. Bookmarks & Scheduling (1-2 rows)
4. Distributed Locking (1 row)
5. Infrastructure (1-2 rows)
6. Traces (if applicable, 1 row)

### Color Schemes
- **Success/Completed**: Green (#73BF69)
- **Failed/Faulted**: Red (#E02F44)
- **Warning/Timeout**: Orange (#FF9830)
- **Info/Pending**: Blue (#5794F2)
- **Neutral**: Gray (#B7B7B7)

## Advanced Features

### Drill-Down Links

Add data links to panels for drill-down navigation:

```json
{
  "fieldConfig": {
    "defaults": {
      "links": [
        {
          "title": "View Workflow Logs",
          "url": "/explore?left={\"queries\":[{\"expr\":\"{workflow_definition_id=\\\"${__field.labels.workflow_definition_id}\\\"}\"}],\"datasource\":\"Loki\"}",
          "targetBlank": true
        }
      ]
    }
  }
}
```

### Dynamic Thresholds

Use query results as thresholds:

```promql
# Set threshold at 2x average
avg_over_time(elsa_workflows_executed_total[7d]) * 2
```

### Alerting from Grafana

Configure Grafana alerts (alternative to Prometheus alerts):

**Alert Rule**:
- Name: High Workflow Failure Rate
- Condition: `sum(rate(elsa_workflows_executed_total{status="faulted"}[5m])) / sum(rate(elsa_workflows_executed_total[5m])) > 0.05`
- For: 5 minutes
- Action: Send to Slack/PagerDuty

## Related Resources

- [Prometheus Metrics Setup](prometheus-metrics.md) - Metric definitions and scraping
- [OpenTelemetry Setup](otel-setup.md) - Trace integration for drill-down
- [Main Monitoring Guide](../README.md) - Comprehensive monitoring overview

## External References

- [Grafana Dashboard Best Practices](https://grafana.com/docs/grafana/latest/best-practices/best-practices-for-creating-dashboards/)
- [Grafana Provisioning](https://grafana.com/docs/grafana/latest/administration/provisioning/)
- [PromQL Cheat Sheet](https://promlabs.com/promql-cheat-sheet/)
- [Grafana Community Dashboards](https://grafana.com/grafana/dashboards/)
