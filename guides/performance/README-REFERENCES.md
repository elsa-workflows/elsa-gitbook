# Source File References (DOC-021)

This file lists the exact source file paths referenced in the Performance & Scaling Guide for maintainers to verify grounding. All paths are relative to the respective repository roots.

---

## elsa-core Repository

| File Path | Purpose | Referenced In |
|-----------|---------|---------------|
| `src/apps/Elsa.Server.Web/Program.cs` | Demonstrates commit strategies and EF Core configuration. Contains `UseWorkflows` setup with batching and commit interval options. | Commit Strategies, Core Throughput Factors |
| `src/modules/Elsa.Workflows.Core/Features/WorkflowsFeature.cs` | Configures the workflow execution pipeline, scheduler strategies, and concurrency model. Defines activity invoker middleware chain. | Workflow & Activity Scheduling |
| `src/modules/Elsa.Workflows.Runtime/Services/WorkflowResumer.cs` | Implements resume logic with distributed locking. Acquires locks via `IDistributedLockProvider` before resuming workflow instances. | Distributed Locking & Contention, Bookmark Lifecycle |
| `src/modules/Elsa.Scheduling/Services/DefaultBookmarkScheduler.cs` | Categorizes bookmarks (Timer, Delay, Cron) and schedules them for future execution via Quartz. Creates `ResumeWorkflowTask` jobs. | Scheduling & Bookmark Throughput |
| `src/modules/Elsa.Scheduling/Tasks/ResumeWorkflowTask.cs` | Quartz job that executes workflow resume at scheduled time. Triggered by `DefaultBookmarkScheduler`. | Scheduling & Bookmark Throughput |
| `src/modules/Elsa.Workflows.Core/Contexts/ActivityExecutionContext.cs` | Provides `CreateBookmark()` method for bookmark creation with payload, callback, and auto-burn settings. Computes deterministic hashes. | Bookmark Lifecycle |
| `src/modules/Elsa.Workflows.Core/Middleware/Activities/DefaultActivityInvokerMiddleware.cs` | Entry point for activity execution pipeline. Manages activity lifecycle, fault handling, and bookmark burning. | Core Throughput Factors |

---

## elsa-extensions Repository

| File Path | Purpose | Referenced In |
|-----------|---------|---------------|
| `src/modules/diagnostics/Elsa.OpenTelemetry/Middleware/OpenTelemetryTracingWorkflowExecutionMiddleware.cs` | Adds OpenTelemetry tracing spans for workflow execution. Creates parent spans for workflow run duration. | Observability for Performance |
| `src/modules/diagnostics/Elsa.OpenTelemetry/Middleware/OpenTelemetryTracingActivityExecutionMiddleware.cs` | Adds OpenTelemetry tracing spans for individual activity execution. Enables activity-level performance analysis. | Observability for Performance |

---

## Performance Testing Repository

| File Path | Purpose | Referenced In |
|-----------|---------|---------------|
| `test/performance/Elsa.Workflows.PerformanceTests/ConsoleActivitiesBenchmark.cs` | Demonstrates BenchmarkDotNet pattern for Elsa performance testing. Uses synthetic workflows with no external I/O. | Load & Performance Testing |

---

## Key Implementation Details

### Commit Strategy Implementation

The `WorkflowsFeature` configures commit behavior through:
- `CommitStateInterval`: Time-based commit trigger
- `CommitStateActivityCount`: Activity-count-based commit trigger

```csharp
// From WorkflowsFeature.cs (conceptual)
public WorkflowsFeature ConfigureCommitStrategy(
    TimeSpan? interval = null,
    int? activityCount = null)
{
    CommitStateInterval = interval ?? TimeSpan.Zero;
    CommitStateActivityCount = activityCount ?? 1;
    return this;
}
```

### WorkflowResumer Locking Pattern

The `WorkflowResumer` service acquires a distributed lock before resuming:

```csharp
// From WorkflowResumer.cs (conceptual)
public async Task<ResumeWorkflowResult> ResumeWorkflowAsync(
    string workflowInstanceId,
    string bookmarkId,
    CancellationToken cancellationToken)
{
    var lockKey = $"workflow:{workflowInstanceId}:bookmark:{bookmarkId}";
    
    await using var lockHandle = await _distributedLockProvider
        .AcquireLockAsync(lockKey, timeout: TimeSpan.FromSeconds(30), cancellationToken);
    
    if (lockHandle == null)
    {
        return ResumeWorkflowResult.AlreadyInProgress();
    }
    
    // Resume workflow under lock
    return await ResumeWorkflowCoreAsync(...);
}
```

### DefaultBookmarkScheduler Flow

1. Activity creates a bookmark with schedule metadata
2. `DefaultBookmarkScheduler.ScheduleAsync()` determines schedule type (Timer/Delay/Cron)
3. Quartz job created via `ISchedulerFactory`
4. `ResumeWorkflowTask` executes at trigger time
5. Calls `IWorkflowRuntime.ResumeWorkflowAsync()`

### OpenTelemetry Tracing Middleware

The tracing middleware creates spans following OpenTelemetry conventions:

```csharp
// From OpenTelemetryTracingWorkflowExecutionMiddleware.cs (conceptual)
public async ValueTask InvokeAsync(WorkflowExecutionContext context, WorkflowMiddlewareDelegate next)
{
    using var activity = _activitySource.StartActivity("workflow.execute");
    activity?.SetTag("workflow.definition_id", context.Workflow.Identity.DefinitionId);
    activity?.SetTag("workflow.instance_id", context.Id);
    
    await next(context);
    
    activity?.SetTag("workflow.status", context.Status.ToString());
}
```

---

## Repository Links

- **elsa-core**: https://github.com/elsa-workflows/elsa-core
- **elsa-extensions**: https://github.com/elsa-workflows/elsa-extensions

---

## Key Interfaces and Types

| Interface/Type | Location | Purpose |
|----------------|----------|---------|
| `IWorkflowResumer` | `Elsa.Workflows.Runtime.Contracts` | Resumes workflows by bookmark or stimulus |
| `IDistributedLockProvider` | `Medallion.Threading` (external) | Acquires distributed locks for concurrent access control |
| `IBookmarkScheduler` | `Elsa.Scheduling.Contracts` | Schedules bookmarks for future execution |
| `WorkflowsFeature` | `Elsa.Workflows.Core.Features` | Configures workflow execution pipeline |
| `ActivityExecutionContext` | `Elsa.Workflows.Core.Contexts` | Provides activity execution APIs including bookmark creation |

---

## Tracing Configuration Types

| Type | Location | Purpose |
|------|----------|---------|
| `OpenTelemetryTracingWorkflowExecutionMiddleware` | `Elsa.OpenTelemetry.Middleware` | Workflow-level tracing spans |
| `OpenTelemetryTracingActivityExecutionMiddleware` | `Elsa.OpenTelemetry.Middleware` | Activity-level tracing spans |
| `ElsaActivitySource` | `Elsa.OpenTelemetry` | Named ActivitySource for Elsa traces |

---

## Related Documentation Files

| Documentation Path | Description |
|-------------------|-------------|
| `guides/clustering/README.md` | DOC-015: Clustering and distributed deployment |
| `guides/troubleshooting/README.md` | DOC-017: Troubleshooting common issues |
| `guides/patterns/README.md` | DOC-018: Workflow patterns and best practices |
| `optimize/retention.md` | Data retention configuration |
| `getting-started/database-configuration.md` | Initial database setup |

---

## External Dependencies

| Package | Purpose |
|---------|---------|
| `Medallion.Threading` | Distributed locking abstraction (Redis, PostgreSQL, SQL Server) |
| `Quartz.NET` | Job scheduling for timers and cron expressions |
| `OpenTelemetry` | Observability and distributed tracing framework |
| `BenchmarkDotNet` | Performance testing framework |

---

## Maintenance Notes

When updating the Performance & Scaling Guide, verify:

1. File paths are still accurate after elsa-core refactoring
2. Method names and behaviors match current implementation
3. Commit strategy configuration APIs haven't changed
4. OpenTelemetry middleware still follows the same pattern
5. New performance-related source files are added to this reference list
6. Deprecated files are removed or marked as such

---

**Last Updated:** 2025-11-27
