# Throughput Tuning Examples

Practical configuration snippets and patterns for optimizing Elsa Workflows throughput. Each example includes the configuration, expected impact, and trade-offs.

---

## Commit Strategy Configuration

### Batched Commits for High-Throughput

**Scenario:** High-volume workflow processing where durability can be traded for throughput.

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflows(workflows =>
    {
        // Commit every 5 seconds or every 10 activities, whichever comes first
        workflows.CommitStateInterval = TimeSpan.FromSeconds(5);
        workflows.CommitStateActivityCount = 10;
    });
});
```

**Expected Impact:**
- ~80-95% reduction in database writes for long-running sequences
- Lower database CPU and I/O utilization
- Reduced lock contention on workflow instance rows

**Trade-offs:**
- Up to 5 seconds of uncommitted state on crash
- Slightly higher memory usage (holding uncommitted state)

### Immediate Commits for Critical Workflows

**Scenario:** Financial transactions or audit-critical workflows.

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflows(workflows =>
    {
        // Commit after every activity (default behavior)
        workflows.CommitStateInterval = TimeSpan.Zero;
        workflows.CommitStateActivityCount = 1;
    });
});
```

**Expected Impact:**
- Maximum durability - no data loss on crash
- Higher database load

**Trade-offs:**
- Lower throughput
- Higher latency per workflow

---

## Parallel Activity Patterns

### Controlled Fan-Out with Parallel

**Scenario:** Process multiple items in parallel with controlled concurrency.

**Flowchart JSON Example:**
```json
{
  "type": "Elsa.Flowchart",
  "activities": [
    {
      "id": "start",
      "type": "Elsa.Start"
    },
    {
      "id": "parallel",
      "type": "Elsa.Parallel",
      "activities": [
        {
          "id": "branch1",
          "type": "Elsa.Sequence",
          "activities": [
            { "type": "Elsa.WriteLine", "text": "Branch 1 - Step 1" },
            { "type": "Elsa.WriteLine", "text": "Branch 1 - Step 2" }
          ]
        },
        {
          "id": "branch2",
          "type": "Elsa.Sequence",
          "activities": [
            { "type": "Elsa.WriteLine", "text": "Branch 2 - Step 1" },
            { "type": "Elsa.WriteLine", "text": "Branch 2 - Step 2" }
          ]
        }
      ]
    },
    {
      "id": "end",
      "type": "Elsa.WriteLine",
      "text": "All branches complete"
    }
  ],
  "connections": [
    { "source": "start", "target": "parallel" },
    { "source": "parallel", "target": "end" }
  ]
}
```

### Dynamic Fan-Out with ForEach

**Scenario:** Process a variable number of items in parallel.

```csharp
// Programmatic workflow with bounded parallelism
// Note: This is a conceptual example showing the pattern
public class DynamicFanOutWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        // Define a variable to hold the items to process
        var items = builder.WithVariable<List<string>>();
        
        builder
            .StartWith<SetVariable>(setup =>
            {
                setup.Set(x => x.Variable, items);
                // In practice, items would come from workflow input or previous activity
                setup.Set(x => x.Value, new Elsa.Expressions.Models.Literal(
                    new List<string> { "item1", "item2", "item3", "item4" }));
            })
            .Then<ForEach>(forEach =>
            {
                forEach.Set(x => x.Items, items);
                forEach.Set(x => x.Parallel, true);
                // Limit parallelism to 2x CPU cores
                forEach.Set(x => x.MaxDegreeOfParallelism, Environment.ProcessorCount * 2);
            });
    }
}
```

**Best Practices:**
- Set `MaxDegreeOfParallelism` to `Environment.ProcessorCount * 2` as starting point
- Monitor CPU utilization and adjust
- For I/O-bound work, higher parallelism may be beneficial

---

## User-Defined Metrics

**Important:** Elsa does not emit built-in metrics. These examples show how to implement custom metrics for your observability needs.

### Workflow Duration Histogram

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

public class WorkflowMetricsMiddleware : IWorkflowExecutionMiddleware
{
    private static readonly Meter _meter = new("Elsa.Custom.Metrics");
    private static readonly Histogram<double> _durationHistogram = 
        _meter.CreateHistogram<double>(
            "elsa_workflow_run_duration_seconds",
            unit: "seconds",
            description: "Duration of workflow executions");

    public async ValueTask InvokeAsync(WorkflowExecutionContext context, WorkflowMiddlewareDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            _durationHistogram.Record(
                stopwatch.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("definition_id", context.Workflow.Identity.DefinitionId),
                new KeyValuePair<string, object?>("status", context.Status.ToString()));
        }
    }
}
```

### Bookmark Resume Counter

```csharp
public class BookmarkMetricsService
{
    private static readonly Meter _meter = new("Elsa.Custom.Metrics");
    private static readonly Counter<long> _resumeCounter =
        _meter.CreateCounter<long>(
            "elsa_bookmark_resume_total",
            description: "Total bookmark resume attempts");
    private static readonly Counter<long> _resumeFailureCounter =
        _meter.CreateCounter<long>(
            "elsa_bookmark_resume_failures_total",
            description: "Total failed bookmark resume attempts");

    public void RecordResumeAttempt(string bookmarkName, bool success)
    {
        var tags = new KeyValuePair<string, object?>("bookmark_name", bookmarkName);
        
        _resumeCounter.Add(1, tags);
        
        if (!success)
            _resumeFailureCounter.Add(1, tags);
    }
}
```

### Active Workflows Gauge

```csharp
public class ActiveWorkflowsGauge
{
    private static readonly Meter _meter = new("Elsa.Custom.Metrics");
    private readonly IWorkflowInstanceStore _store;

    public ActiveWorkflowsGauge(IWorkflowInstanceStore store)
    {
        _store = store;
        
        _meter.CreateObservableGauge(
            "elsa_active_workflow_instances",
            observeValue: GetActiveCount,
            description: "Number of currently active workflow instances");
    }

    private int GetActiveCount()
    {
        // Note: This should be cached/sampled in production
        var filter = new WorkflowInstanceFilter { Status = WorkflowStatus.Running };
        return _store.CountAsync(filter).GetAwaiter().GetResult();
    }
}
```

### Suggested Metric Names

| Metric Name | Type | Description |
|-------------|------|-------------|
| `elsa_workflow_run_duration_seconds` | Histogram | Duration of workflow executions |
| `elsa_workflow_completed_total` | Counter | Total completed workflows by definition |
| `elsa_workflow_faulted_total` | Counter | Total faulted workflows by definition |
| `elsa_bookmark_resume_duration_seconds` | Histogram | Time to resume a bookmark |
| `elsa_bookmark_resume_total` | Counter | Total bookmark resume attempts |
| `elsa_bookmark_resume_failures_total` | Counter | Failed bookmark resumes |
| `elsa_active_workflow_instances` | Gauge | Currently running instances |
| `elsa_pending_executions` | Gauge | Workflows waiting to execute |
| `elsa_lock_acquisition_duration_seconds` | Histogram | Time to acquire distributed lock |
| `elsa_db_query_duration_seconds` | Histogram | Database query execution time |

---

## Connection Pool Tuning

### PostgreSQL Connection String

```
Server=postgres-host;
Port=5432;
Database=elsa;
User Id=elsa;
Password=YOUR_PASSWORD;
MaxPoolSize=100;
MinPoolSize=10;
Connection Idle Lifetime=300;
Connection Pruning Interval=10;
Keepalive=30
```

**Parameter Guidance:**
| Parameter | Development | Production | High-Load |
|-----------|-------------|------------|-----------|
| `MaxPoolSize` | 20 | 100 | 200 |
| `MinPoolSize` | 2 | 10 | 25 |
| `Connection Idle Lifetime` | 300 | 300 | 180 |

### SQL Server Connection String

```
Server=sql-host;
Database=elsa;
User Id=elsa;
Password=YOUR_PASSWORD;
Max Pool Size=100;
Min Pool Size=10;
Connection Lifetime=300;
Pooling=true
```

### MongoDB Connection String

```
mongodb://elsa:YOUR_PASSWORD@mongo-host:27017/elsa?
maxPoolSize=100&
minPoolSize=10&
waitQueueTimeoutMS=10000&
serverSelectionTimeoutMS=5000
```

---

## Lock Provider Configuration

### Redis with Optimized Settings

```csharp
runtime.DistributedLockProvider = serviceProvider =>
{
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    var connection = ConnectionMultiplexer.Connect(redisConnection);
    
    return new RedisDistributedSynchronizationProvider(
        connection.GetDatabase(),
        options =>
        {
            // Lock TTL - should be longer than max expected workflow operation
            options.Expiry(TimeSpan.FromSeconds(30));
            
            // Minimum expiry to prevent premature release
            options.MinimumDatabaseExpiry(TimeSpan.FromSeconds(10));
            
            // Busy wait settings for high-contention scenarios
            options.BusyWaitSleepTime(
                minSleepTime: TimeSpan.FromMilliseconds(10),
                maxSleepTime: TimeSpan.FromMilliseconds(100));
        });
};
```

### PostgreSQL with Advisory Locks

```csharp
runtime.DistributedLockProvider = serviceProvider =>
{
    var connectionString = builder.Configuration.GetConnectionString("PostgreSql");
    
    return new PostgresDistributedSynchronizationProvider(
        connectionString,
        options =>
        {
            // Keep connections alive for lock sessions
            options.KeepaliveCadence(TimeSpan.FromMinutes(5));
            
            // Use connection multiplexing for efficiency
            options.UseMultiplexing();
        });
};
```

---

## Database Index Creation Scripts

### PostgreSQL

```sql
-- Workflow Instances
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflow_instances_status 
    ON elsa.workflow_instances(status);
    
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflow_instances_definition_id 
    ON elsa.workflow_instances(definition_id);
    
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflow_instances_correlation_id 
    ON elsa.workflow_instances(correlation_id) 
    WHERE correlation_id IS NOT NULL;
    
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflow_instances_created_at 
    ON elsa.workflow_instances(created_at);
    
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflow_instances_updated_at 
    ON elsa.workflow_instances(updated_at);

-- Bookmarks
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_bookmarks_hash 
    ON elsa.bookmarks(hash);
    
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_bookmarks_workflow_instance_id 
    ON elsa.bookmarks(workflow_instance_id);
    
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_bookmarks_activity_type 
    ON elsa.bookmarks(activity_type_name);
    
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_bookmarks_created_at 
    ON elsa.bookmarks(created_at);

-- Composite index for common query patterns
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflow_instances_status_definition 
    ON elsa.workflow_instances(status, definition_id);

-- Analyze tables after index creation
ANALYZE elsa.workflow_instances;
ANALYZE elsa.bookmarks;
```

### SQL Server

```sql
-- Workflow Instances
CREATE NONCLUSTERED INDEX IX_WorkflowInstances_Status 
    ON elsa.WorkflowInstances(Status);
    
CREATE NONCLUSTERED INDEX IX_WorkflowInstances_DefinitionId 
    ON elsa.WorkflowInstances(DefinitionId);
    
CREATE NONCLUSTERED INDEX IX_WorkflowInstances_CorrelationId 
    ON elsa.WorkflowInstances(CorrelationId) 
    WHERE CorrelationId IS NOT NULL;

-- Bookmarks
CREATE NONCLUSTERED INDEX IX_Bookmarks_Hash 
    ON elsa.Bookmarks(Hash);
    
CREATE NONCLUSTERED INDEX IX_Bookmarks_WorkflowInstanceId 
    ON elsa.Bookmarks(WorkflowInstanceId);
```

---

## Quartz Scheduler Tuning

### High-Throughput Quartz Configuration

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseQuartz(quartz =>
    {
        quartz.UsePostgreSql(connectionString);
        
        // Thread pool sizing
        quartz.Configure(q =>
        {
            q.ThreadPool.MaxConcurrency = 20; // Adjust based on workload
            q.MisfireThreshold = TimeSpan.FromSeconds(60);
        });
    });
});
```

### Quartz.properties for Clustering

```properties
# High-throughput cluster settings
quartz.threadPool.threadCount = 20
quartz.jobStore.misfireThreshold = 60000
quartz.jobStore.clusterCheckinInterval = 15000
quartz.jobStore.acquireTriggersWithinLock = true

# Batch acquisition for efficiency
quartz.scheduler.batchTriggerAcquisitionMaxCount = 10
quartz.scheduler.batchTriggerAcquisitionFireAheadTimeWindow = 1000
```

---

## Rate Limiting for Workflow Triggers

### ASP.NET Core Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    // Global limit for all workflow triggers
    options.AddFixedWindowLimiter("workflow-trigger", limiter =>
    {
        limiter.PermitLimit = 100;        // 100 requests
        limiter.Window = TimeSpan.FromSeconds(1);  // per second
        limiter.QueueLimit = 50;          // queue up to 50
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    
    // Per-definition limit for expensive workflows
    options.AddSlidingWindowLimiter("expensive-workflow", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.SegmentsPerWindow = 6;  // 10-second segments
        limiter.QueueLimit = 5;
    });
});

// Apply to endpoints
app.MapPost("/api/workflows/trigger/{definitionId}", TriggerWorkflow)
   .RequireRateLimiting("workflow-trigger");

app.MapPost("/api/workflows/trigger/expensive-report", TriggerExpensiveReport)
   .RequireRateLimiting("expensive-workflow");
```

---

## Related Documentation

- [Performance & Scaling Guide](../README.md) - Full optimization guide
- [Load Test Checklist](load-test-checklist.md) - Testing methodology
- [Clustering Guide](../../clustering/README.md) - Distributed deployment

---

**Last Updated:** 2025-11-27
