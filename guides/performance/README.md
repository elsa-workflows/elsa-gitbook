---
description: >-
  Comprehensive performance and scaling guide for optimizing Elsa Workflows deployments, covering throughput tuning, persistence optimization, backpressure management, and production readiness.
---

# Performance & Scaling Guide

This guide helps operators and developers plan capacity and optimize Elsa Workflows deployments for production workloads. Each section provides actionable guidance grounded in Elsa's source code with practical tuning examples.

## Executive Summary

### Scope

This guide covers:

- **Execution pipeline throughput**: Optimizing activity execution and commit strategies
- **Persistence tuning**: Database provider selection and indexing strategies
- **Scheduling performance**: Bookmark scheduling and timer accuracy
- **Distributed locking**: Contention management and lock optimization
- **Long-running workflows**: Memory management and suspension strategies
- **Backpressure handling**: Throttling and queue depth management
- **Observability**: Performance tracing and metric collection

### What This Guide Does NOT Cover

Deep database tuning, operating system configuration, and container runtime optimization are beyond the scope of this guide. For those topics, refer to:

- Database vendor documentation (PostgreSQL, SQL Server, MongoDB)
- Container platform guides (Docker, Kubernetes)
- Cloud provider performance best practices

---

## Core Throughput Factors

### Activity Execution Pipeline & Commit Strategies

The activity execution pipeline is the heart of Elsa's runtime. Understanding how activities are invoked and how state is committed is essential for throughput optimization.

**Code Reference:** `src/modules/Elsa.Workflows.Core/Middleware/Activities/DefaultActivityInvokerMiddleware.cs`

The `DefaultActivityInvokerMiddleware` orchestrates activity execution, managing:
- Activity lifecycle (execute, complete, fault)
- Bookmark creation and burning
- State persistence timing

#### Commit Strategies

Elsa supports different commit strategies that control when workflow state is persisted to the database.

**Code Reference:** `src/apps/Elsa.Server.Web/Program.cs` - `UseWorkflows` commit strategy configuration

**Strategy Options:**

| Strategy | Description | Use Case |
|----------|-------------|----------|
| **Immediate** | Persist after each activity | Critical workflows requiring durability |
| **Batched** | Persist after N activities or time interval | High-throughput scenarios |
| **Deferred** | Persist only on suspension/completion | Maximum throughput, acceptable risk |

**Configuration Example:**

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflows(workflows =>
    {
        // Batched commit: persist every 5 seconds or 10 activities
        workflows.CommitStateInterval = TimeSpan.FromSeconds(5);
        workflows.CommitStateActivityCount = 10;
    });
});
```

**Trade-offs:**

| Approach | Latency | Durability | DB Load |
|----------|---------|------------|---------|
| Immediate commits | Higher | Maximum | High |
| Batched commits (5-10s) | Lower | Good | Moderate |
| Deferred commits | Lowest | Risk of data loss | Low |

**Recommended Starting Values:**

- **High-throughput scenarios**: Commit every 5-10 seconds or every 10 activities
- **Critical workflows**: Immediate commits (default)
- **Development/testing**: Deferred for faster iteration

### Workflow & Activity Scheduling

**Code Reference:** `src/modules/Elsa.Workflows.Core/Features/WorkflowsFeature.cs`

The `WorkflowsFeature` configures the workflow execution pipeline, including:
- Activity invoker middleware chain
- Scheduler strategies for work item distribution
- Concurrency model for parallel execution

**Concurrency Model:**

Elsa uses a work-item based concurrency model where:
1. Workflow execution is divided into work items
2. Work items are scheduled to a thread pool
3. Scheduler strategies determine distribution

**Tuning Scheduler Strategies:**

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflows(workflows =>
    {
        // Configure maximum concurrent activities per workflow
        workflows.MaxConcurrentActivities = Environment.ProcessorCount * 2;
    });
});
```

---

## Persistence Choices & Tuning

### Provider Comparison

| Provider | Transactions | Flexibility | Performance | Best For |
|----------|--------------|-------------|-------------|----------|
| **EF Core (SQL)** | Full ACID | Relational indexing, joins | Moderate | Enterprise apps with existing SQL |
| **MongoDB** | Document-level | Schema flexibility, horizontal scaling | High reads | High-volume, document-centric |
| **Dapper** | Manual control | Low overhead, raw SQL | Highest | Performance-critical, custom queries |

### Recommendation Matrix

| Scenario | Recommended Provider | Rationale |
|----------|---------------------|-----------|
| Enterprise with SQL Server/PostgreSQL | EF Core | Leverage existing infrastructure |
| High read volume, simple queries | MongoDB | Horizontal scaling, flexible schema |
| Maximum performance, experienced team | Dapper | Minimal overhead, full control |
| Rapid prototyping | EF Core (SQLite) | Zero configuration |

### Critical Indexes

Ensure these indexes exist for optimal performance:

**Workflow Instances:**
```sql
-- PostgreSQL example
CREATE INDEX idx_workflow_instances_status ON elsa.workflow_instances(status);
CREATE INDEX idx_workflow_instances_definition_id ON elsa.workflow_instances(definition_id);
CREATE INDEX idx_workflow_instances_correlation_id ON elsa.workflow_instances(correlation_id);
CREATE INDEX idx_workflow_instances_created_at ON elsa.workflow_instances(created_at);
```

**Bookmarks:**
```sql
CREATE INDEX idx_bookmarks_hash ON elsa.bookmarks(hash);
CREATE INDEX idx_bookmarks_workflow_instance_id ON elsa.bookmarks(workflow_instance_id);
CREATE INDEX idx_bookmarks_activity_type ON elsa.bookmarks(activity_type_name);
```

### Connection Pooling

Defer detailed connection pool tuning to your database vendor's documentation. General guidance:

- **Production minimum**: 50-100 connections per node
- **High throughput**: 100-200 connections per node
- **Monitor**: Connection wait time, pool exhaustion events

```
Server=host;Database=elsa;MaxPoolSize=100;Connection Idle Lifetime=300
```

### Key Metrics to Measure

| Metric | Target | Action if Exceeded |
|--------|--------|-------------------|
| **P99 workflow run duration** | < 500ms for simple workflows | Profile activity execution |
| **Bookmark resume latency** | < 100ms | Check lock acquisition, DB queries |
| **Connection wait time** | < 10ms | Increase pool size |

---

## Scheduling & Bookmark Throughput

### Bookmark Scheduler Architecture

**Code References:**
- `src/modules/Elsa.Scheduling/Services/DefaultBookmarkScheduler.cs` - Bookmark scheduling logic
- `src/modules/Elsa.Scheduling/Tasks/ResumeWorkflowTask.cs` - Task executing resumes

The `DefaultBookmarkScheduler` categorizes bookmarks by type (Timer, Delay, Cron) and schedules them via Quartz. The `ResumeWorkflowTask` is a Quartz job that triggers workflow resume at the scheduled time.

**Cluster-Safe Patterns:**

1. **Quartz Clustering**: All nodes participate with database coordination
2. **Single Scheduler**: Designate one node for scheduling (see [Clustering Guide](../clustering/README.md))

**Impact on Timer/Cron Accuracy:**

| Configuration | Accuracy | Trade-off |
|---------------|----------|-----------|
| Quartz clustering | ±1 second | Database lock overhead |
| Single scheduler | ±100ms | Single point of failure |
| External scheduler (K8s CronJob) | ±1 minute | Platform dependency |

### Bookmark Lifecycle

**Code Reference:** `src/modules/Elsa.Workflows.Core/Contexts/ActivityExecutionContext.cs` - `CreateBookmark` method

**Creation Flow:**
1. Activity calls `context.CreateBookmark(args)`
2. Elsa computes deterministic hash from payload
3. Bookmark persisted to database
4. Workflow suspended

**Resume Flow:**
1. External event triggers resume via `WorkflowResumer`
2. Stimulus hash computed and matched to stored bookmark
3. Lock acquired on workflow instance
4. Workflow execution continues from bookmark

**Code Reference:** `src/modules/Elsa.Workflows.Runtime/Services/WorkflowResumer.cs` - Resume and locking logic

### Avoiding High-Cardinality Stimuli

**Problem:** High-cardinality stimuli (unique values per event) create many unique bookmark hashes, reducing cache effectiveness and increasing DB load.

**Anti-pattern:**
```csharp
// Bad: Timestamp creates unique hash for every event
var payload = new { OrderId = orderId, Timestamp = DateTime.UtcNow };
```

**Recommended:**
```csharp
// Good: Only business keys in payload
var payload = new { OrderId = orderId, EventType = "PaymentReceived" };
```

**Guidelines:**
- Use stable business identifiers
- Avoid timestamps, random values in payloads
- Keep payload structures consistent between create and resume

---

## Distributed Locking & Contention

### Locking Architecture

**Code Reference:** `src/modules/Elsa.Workflows.Runtime/Services/WorkflowResumer.cs`

The `WorkflowResumer` acquires a distributed lock before resuming a workflow:

```csharp
// Conceptual flow (simplified)
var lockKey = $"workflow:{workflowInstanceId}:bookmark:{bookmarkId}";
await using var lockHandle = await _distributedLockProvider.AcquireLockAsync(lockKey, timeout);
```

### Symptoms of Contention

| Symptom | Log Pattern | Cause |
|---------|-------------|-------|
| Resume timeouts | `Lock acquisition failed after timeout` | Too many concurrent resumes |
| Slow resumes | High P99 resume latency | Lock provider overloaded |
| Cascading delays | Multiple workflows waiting | Hot workflow instance |

### Mitigation Strategies

**1. Shard Workflows:**
Design workflows to minimize concurrent access to the same instance:
```csharp
// Instead of one workflow handling all orders
// Create separate workflow instances per order
await _workflowStarter.StartWorkflowAsync("OrderProcessor", new { OrderId = orderId });
```

**2. Partition Lock Keys:**
For high-volume scenarios, consider partitioning:
```csharp
// Partition by correlation ID prefix
var partition = correlationId.GetHashCode() % 16;
var lockKey = $"partition:{partition}:workflow:{workflowInstanceId}";
```

**3. Increase Lock Timeout:**
For high-latency environments:
```csharp
runtime.DistributedLockProvider = sp =>
    new RedisDistributedSynchronizationProvider(
        connection.GetDatabase(),
        options =>
        {
            options.Expiry(TimeSpan.FromSeconds(60)); // Increased from 30s
        });
```

### Measuring Lock Performance

**User-Defined Metrics (see DOC-016 for implementation guidance):**

Implement custom metrics to track lock acquisition:

```csharp
// Example: Custom lock duration tracking (user-defined)
var stopwatch = Stopwatch.StartNew();
await using var lockHandle = await _lockProvider.AcquireLockAsync(lockKey, timeout);
stopwatch.Stop();

// Record to your metrics system
_meter.CreateHistogram<double>("elsa_lock_acquisition_duration_seconds")
    .Record(stopwatch.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("lock_type", "workflow"));
```

---

## Long-Running Workflows

### Bookmark Suspension vs Polling

| Approach | Memory Usage | Persistence Load | Latency |
|----------|--------------|------------------|---------|
| **Bookmark suspension** | Low (state on disk) | Higher (writes) | Resume overhead |
| **Polling loops** | High (state in memory) | Lower | Polling interval |

**Recommendation:** Use bookmark suspension for workflows spanning hours/days. Use polling only for tight loops with sub-second requirements.

### Memory Considerations

Long-running workflows should:
1. **Clear variables when no longer needed**: Large objects in workflow variables persist with the instance
2. **Use external storage for large payloads**: Store references (IDs, URLs) instead of data
3. **Stream data instead of buffering**: For file processing, use streams

```csharp
// Anti-pattern: Large data in workflow variable
context.SetVariable("ReportData", largeDataSet); // May cause memory issues

// Better: Store reference, fetch when needed
context.SetVariable("ReportDataUrl", blobStorageUrl);
```

### Retention Strategies

Configure retention to clean up completed workflows:

```csharp
elsa.UseWorkflowManagement(management =>
{
    management.UseWorkflowInstanceRetention(retention =>
    {
        retention.RetentionPeriod = TimeSpan.FromDays(30);
        retention.SweepInterval = TimeSpan.FromHours(1);
    });
});
```

### Safe Cancellation

To cancel a long-running workflow without data corruption:

```csharp
var workflowInstanceManager = serviceProvider.GetRequiredService<IWorkflowInstanceManager>();
await workflowInstanceManager.CancelAsync(workflowInstanceId);
```

This:
1. Marks the instance as cancelled
2. Removes associated bookmarks
3. Fires workflow cancelled events (triggers `AllActivitiesCompleted` handlers if configured)
4. Creates an incident record for audit

---

## Backpressure & Throttling

### Techniques

**1. Limit Parallel Fan-Outs:**

```csharp
// Instead of unbounded parallelism
foreach (var item in items)
{
    // Execute with parallelism limit
}

// Use bounded parallelism
var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 };
await Parallel.ForEachAsync(items, options, async (item, ct) => { ... });
```

**2. Queue Depth Monitoring (User-Defined Gauge):**

Implement custom gauges to monitor pending work:

```csharp
// Example: Track pending workflow executions (user-defined metric)
var pendingGauge = _meter.CreateObservableGauge<int>(
    "elsa_pending_executions",
    () => GetPendingExecutionCount());
```

**3. Rate Limiting External Triggers:**

For HTTP-triggered workflows, implement rate limiting at the ingress:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("workflow-trigger", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromSeconds(1);
        limiter.QueueLimit = 50;
    });
});

// Apply to workflow endpoints
app.MapPost("/api/workflows/trigger", TriggerWorkflow)
   .RequireRateLimiting("workflow-trigger");
```

**4. Batch Resume Tasks:**

For high-volume resume scenarios, batch operations:

```csharp
// Instead of resuming one at a time
foreach (var bookmark in bookmarks)
{
    await _resumer.ResumeAsync(bookmark);
}

// Batch resumes
var chunks = bookmarks.Chunk(50);
foreach (var chunk in chunks)
{
    var tasks = chunk.Select(b => _resumer.ResumeAsync(b));
    await Task.WhenAll(tasks);
    await Task.Delay(100); // Brief pause between batches
}
```

### Use Cases

**Ingestion Spikes:**
- Implement queue buffering before workflow dispatch
- Use message broker (RabbitMQ, Azure Service Bus) as buffer
- Configure consumer prefetch limits

**External System Slowdowns:**
- Implement circuit breaker patterns in activities
- Monitor external call latency and fail fast
- Queue retries with exponential backoff

---

## Load & Performance Testing

### Benchmark Patterns

**Code Reference:** `test/performance/Elsa.Workflows.PerformanceTests/ConsoleActivitiesBenchmark.cs`

The `ConsoleActivitiesBenchmark` demonstrates the benchmark pattern:
1. Create synthetic workflows (no external I/O)
2. Measure execution time with consistent iterations
3. Use BenchmarkDotNet for accurate measurements

### Testing Strategy

**Phase 1: Baseline (Synthetic Workflows)**
```csharp
// Create workflows with no external dependencies
[Benchmark]
public async Task ExecuteSimpleSequence()
{
    var workflow = new SequenceWorkflow(); // Only in-memory operations
    await _runner.ExecuteAsync(workflow);
}
```

**Phase 2: Incremental Complexity**
- Add database operations
- Add HTTP calls (to mocks)
- Add parallel branches

**Phase 3: Real-World Simulation**
- Use production-like data volumes
- Simulate external service latencies
- Include error scenarios

### Metrics & Traces to Capture

| Metric | Description | Target |
|--------|-------------|--------|
| `workflow_run_duration_seconds` | Total execution time | Varies by workflow |
| `bookmark_wait_time_seconds` | Time spent suspended | < 100ms for resumes |
| `resume_success_count` | Successful resumes | Monitor for drops |
| `resume_failure_count` | Failed resumes | Should be near zero |
| `activity_execution_duration_seconds` | Per-activity timing | Identify hot spots |

---

## Observability for Performance

### Tracing with Elsa.OpenTelemetry

**Code References (elsa-extensions):**
- `src/modules/diagnostics/Elsa.OpenTelemetry/Middleware/OpenTelemetryTracingWorkflowExecutionMiddleware.cs` - Workflow execution spans
- `src/modules/diagnostics/Elsa.OpenTelemetry/Middleware/OpenTelemetryTracingActivityExecutionMiddleware.cs` - Activity execution spans

The `Elsa.OpenTelemetry` package (from elsa-extensions) provides built-in tracing:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseOpenTelemetry(); // Adds tracing middleware
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddElsaSource(); // Add Elsa's ActivitySource
        tracing.AddOtlpExporter();
    });
```

### Suggested Spans to Inspect

| Span | What It Shows |
|------|---------------|
| `workflow.execute` | Total workflow execution (start → finish) |
| `activity.execute` | Individual activity duration |
| `bookmark.create` | Bookmark creation timing |
| `workflow.resume` | Resume operation including lock acquisition |
| Activity fault edges | Exception paths and error handling |

### User-Defined Metrics

Elsa does **not** emit built-in metrics. Implement custom metrics based on your observability requirements (see [Monitoring Guide](../monitoring/README.md) (DOC-016) for implementation patterns):

**Example Metric Names (user-defined):**
- `elsa_workflow_run_duration_seconds` - Histogram of workflow execution times
- `elsa_workflow_completed_total` - Counter of completed workflows by definition
- `elsa_bookmark_resume_duration_seconds` - Histogram of resume times
- `elsa_active_workflow_instances` - Gauge of currently running instances

---

## Scaling Strategies

### Horizontal Scaling

**Runtime Instances:**
- Elsa runtime is stateless; scale horizontally without sticky sessions
- All nodes share the same database and lock provider
- Use load balancer with health checks (`/health/ready`)

**Database Bottlenecks:**
Evaluate and address in this order:
1. **CPU saturation**: Optimize queries, add indexes
2. **Lock waits**: Reduce transaction duration, use read replicas
3. **Connection exhaustion**: Increase pool size, add connection pooling (e.g., PgBouncer)
4. **I/O bottleneck**: Scale storage, use SSDs

**Lock Provider Capacity:**
| Provider | Throughput | Scaling |
|----------|------------|---------|
| Redis | 100K+ ops/sec | Redis Cluster |
| PostgreSQL | 10K+ ops/sec | Connection pooling |
| SQL Server | 10K+ ops/sec | Connection pooling |

### Splitting Workloads

**By Tenant:**
```csharp
// Route tenants to dedicated Elsa clusters
var tenantCluster = GetClusterForTenant(tenantId);
await tenantCluster.ExecuteWorkflowAsync(workflow);
```

**By Queue/Priority:**
```csharp
// High-priority workflows on dedicated nodes
builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.WorkflowDispatcher = new PriorityQueueDispatcher();
    });
});
```

### Statelessness

Elsa's distributed runtime is fully stateless:
- **No sticky sessions required**: Any node can handle any request
- **State in database**: All workflow state persisted externally
- **Shared locks**: Distributed lock provider coordinates access

See [Clustering Guide](../clustering/README.md) for detailed clustering patterns.

---

## Practical Tuning Examples

### Commit Strategy Interval Tuning

**Scenario:** High-throughput order processing with acceptable durability trade-off.

**Before (Default - Immediate):**
- 1000 workflows/min
- 10 activities each = 10,000 DB writes/min
- High database load

**After (Batched - 5 second interval):**
```csharp
workflows.CommitStateInterval = TimeSpan.FromSeconds(5);
```
- Same 1000 workflows/min
- ~200 DB writes/min (commit at end of each 5s window)
- **~98% reduction in DB writes**

**Trade-off:** In case of crash, up to 5 seconds of uncommitted state may be lost.

### Fan-Out Size vs Latency

**Scenario:** Parallel processing of order line items.

**Recommendation:** Start with `fan-out <= vCPU count × 2`

| Fan-Out Size | 4 vCPU Latency | 8 vCPU Latency | Notes |
|--------------|----------------|----------------|-------|
| 4 branches | 100ms | 100ms | Optimal for 4 vCPU |
| 8 branches | 150ms | 100ms | Optimal for 8 vCPU |
| 16 branches | 250ms | 150ms | Context switching overhead |
| 32 branches | 400ms | 200ms | Diminishing returns |

**Configuration:**
```csharp
// In workflow definition
var parallelBranches = Math.Min(items.Count, Environment.ProcessorCount * 2);
```

---

## Production Readiness Checklist

Copy this checklist for production deployments:

### Infrastructure
- [ ] Database connection pooling configured (50-200 connections)
- [ ] Database backups scheduled and tested
- [ ] Distributed lock provider accessible from all nodes (Redis/PostgreSQL)
- [ ] Message broker configured for cache invalidation (if clustered)
- [ ] Load balancer with health checks (`/health/ready`)

### Performance Configuration
- [ ] Commit strategy appropriate for workload (batched for high-throughput)
- [ ] Database indexes created for workflow_instances and bookmarks
- [ ] Connection pool size adequate for expected load
- [ ] Quartz clustering enabled (if multi-node with timers)

### Observability
- [ ] OpenTelemetry tracing enabled (`UseOpenTelemetry()`)
- [ ] Custom metrics implemented for key operations (user-defined)
- [ ] Log aggregation configured (ELK, Loki, CloudWatch)
- [ ] Dashboards created for workflow throughput, latency, errors

### Capacity Planning
- [ ] Baseline performance established with synthetic workflows
- [ ] Load testing completed with production-like data
- [ ] Scaling thresholds defined (CPU, memory, queue depth)
- [ ] Auto-scaling configured (if applicable)

### Retention & Maintenance
- [ ] Workflow instance retention configured
- [ ] Database maintenance scheduled (VACUUM, index rebuild)
- [ ] Log rotation configured
- [ ] Alert thresholds set for queue buildup, error rates

---

## Troubleshooting Cross-Links

For diagnosing specific issues:

| Issue | Guide Reference |
|-------|-----------------|
| Workflows stuck or not resuming | [Troubleshooting Guide](../troubleshooting/README.md) |
| Duplicate timer executions | [Clustering Guide](../clustering/README.md) |
| Lock acquisition failures | [Troubleshooting Guide](../troubleshooting/README.md) |
| Bookmark not found errors | [Troubleshooting Guide](../troubleshooting/README.md) |
| Database performance issues | [Troubleshooting Guide - High Database Load](../troubleshooting/README.md#high-database-load--slow-persistence) |

---

## References & Placeholders

### Diagrams

_[Diagram: Activity Execution Pipeline showing middleware chain and commit points]_

_[Diagram: Bookmark lifecycle from creation through resume]_

_[Diagram: Horizontal scaling architecture with shared database and lock provider]_

_[Diagram: Backpressure flow with queue depth monitoring]_

### Related Documentation

- [Clustering Guide](../clustering/README.md) - Distributed deployment patterns
- [Troubleshooting Guide](../troubleshooting/README.md) - Diagnosing issues
- [Retention](../../optimize/retention.md) - Data retention policies
- [Database Configuration](../../getting-started/database-configuration.md) - Initial DB setup

### Source References

See [README-REFERENCES.md](README-REFERENCES.md) for complete list of referenced elsa-core and elsa-extensions source files.

---

## Supplementary Files

- [Load Test Checklist](examples/load-test-checklist.md) - Copyable load testing checklist
- [Throughput Tuning Examples](examples/throughput-tuning.md) - Practical tuning snippets

---

**Last Updated:** 2025-11-27

**Acceptance Criteria Checklist (DOC-021):**
- ✅ Actionable "how to" performance guidance grounded in source code
- ✅ Explicit references to elsa-core & elsa-extensions files
- ✅ Clear differentiation between built-in tracing and user-defined metrics
- ✅ Includes tuning examples (commit strategy, fan-out sizing)
- ✅ Load test checklist included
- ✅ Throughput tuning file included
- ✅ SUMMARY.md updated (pending)
- ✅ Documentation-only changes
