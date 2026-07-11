---
description: >-
  Release-backed guidance for measuring and tuning Elsa 3.8.0 throughput without
  trading away workflow durability or operational visibility by accident.
---

# Performance tuning

Tune Elsa from measurements, not from a generic worker-count target. A useful
baseline includes representative workflow definitions, production-like
persistence, and traffic that contains both new starts and resume work. Record
throughput, end-to-end latency, database pressure, and the rate of incidents
before changing one setting at a time.

In `release/3.8.0`, the main controls are:

1. how often workflow state is committed;
2. how much mediator work the host processes concurrently;
3. whether in-workflow dispatch uses the transactional outbox; and
4. the persistence, logging, and trace data retained for each execution.

## Start with the bottleneck

Use the workflow and activity spans from `Elsa.Workflows` to distinguish slow
activity work from dispatcher, persistence, or downstream-service pressure.
The built-in meter publishes `elsa.workflow.started`,
`elsa.workflow.completed`, `elsa.workflow.faulted`, and
`elsa.activity.duration`. The accompanying spans include workflow and activity
identifiers, status, correlation ID, and tenant ID. See [Distributed
Tracing](../../operate/distributed-tracing.md) for exporter setup.

| Observation | Investigate before increasing concurrency |
| --- | --- |
| Activity durations rise while arrivals stay steady | the activity's downstream dependency, connection pool, or resource limit |
| Workflow duration rises around commits | database latency, state size, bookmarks, variables, and log persistence |
| Work arrives faster than it completes | command, job, or notification worker saturation; then downstream capacity |
| Cross-workflow dispatch is slow or unreliable after a commit | transactional-outbox settings and the outbox store |

Do not treat a faster benchmark as sufficient. Re-run failure and restart cases
after every tuning change: a commit policy defines the recovery boundary.

## Choose a commit policy deliberately

A commit persists more than the workflow row. Elsa's default commit handler
persists bookmark changes, activity execution logs, workflow execution logs,
variables, and workflow state, then runs deferred work. More commits therefore
usually improve durability and state visibility while adding persistence work.

`UseCommitStrategies` registers strategies that a workflow definition can
select. Registering a strategy does not make it the default; set a fallback
explicitly when definitions without a selection need one.

```csharp
using Elsa.Extensions;
using Elsa.Workflows.CommitStates;
using Elsa.Workflows.CommitStates.Strategies;

builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflows(workflows =>
    {
        workflows.UseCommitStrategies(strategies =>
        {
            strategies.AddStandardStrategies();
            strategies.Add(
                "Periodic10Seconds",
                "Every 10 seconds",
                "Commit workflow state at least every 10 seconds during execution.",
                new PeriodicWorkflowStrategy(TimeSpan.FromSeconds(10)));
        });

        // Fallback for definitions with no CommitStrategyName.
        workflows.WithDefaultWorkflowCommitStrategy(
            new PeriodicWorkflowStrategy(TimeSpan.FromSeconds(10)));
    });
});
```

`AddStandardStrategies()` registers these workflow strategy names:

| Name | When it commits | Typical fit |
| --- | --- | --- |
| `WorkflowExecuting` | when workflow execution starts | capture initial state early |
| `WorkflowExecuted` | when workflow execution ends | short workflows where reduced write volume matters |
| `ActivityExecuting` | before each activity executes | higher durability and diagnostic visibility |
| `ActivityExecuted` | after each activity executes | workflows needing state after each completed activity |

`PeriodicWorkflowStrategy` starts with a commit, then commits when its interval
has elapsed. Give it a stable registry name and a meaningful display name with
the four-argument `Add(...)` overload. `CommitStrategyName` stores the stable
name (`Periodic10Seconds` in the example), while Studio shows the display name.
This also avoids overwriting one periodic interval with another. It is a good
starting point for long-running workflows only after you test its recovery
behavior and database cost under realistic load.

### Select the policy per workflow

Set `WorkflowOptions.CommitStrategyName` to the registered name. In Elsa
Studio, open the workflow's **Properties**, choose **Settings**, and select
**Commit Strategy**. Studio loads the available strategies from the backend and
saves the selected name in the workflow definition; the empty **Default**
selection uses the host fallback.

Use a per-workflow policy when one workload needs durability after every step
while another is short-lived and can safely reduce persistence churn. Avoid
inventing method calls such as `UseWorkflowExecutedStrategy()` or
`UsePeriodicStrategy()`—they are not part of the 3.8.0 API.

## Increase worker counts carefully

Elsa's mediator has independent command, notification, and job worker counts,
each defaulting to four. Change only the queue that matches measured backlog;
raising all three multiplies concurrent work against the same database and
external systems.

```csharp
using Elsa.Mediator.Options;

builder.Services.Configure<MediatorOptions>(options =>
{
    options.CommandWorkerCount = 8;
    options.JobWorkerCount = 4;
    options.NotificationWorkerCount = 4;
});
```

The background workflow dispatcher queues work through the command path and
returns before that work is executed. Start with a small increase, watch
activity duration, database saturation, and faults, then keep or revert it.
More workers are not a substitute for a slow activity implementation or an
undersized downstream service.

## Decide whether dispatch needs an outbox

For dispatch initiated during workflow execution, `WorkflowDispatcherOptions`
can enable a transactional outbox. With `UseTransactionalOutbox` enabled, Elsa
writes eligible dispatches with the workflow state commit and delivers them
afterward. This makes recovery behavior more robust, but adds persistence and
delivery work to the path.

```csharp
using Elsa.Workflows.Runtime.Options;

builder.Services.Configure<WorkflowDispatcherOptions>(options =>
{
    options.UseTransactionalOutbox = true;
    options.ProcessOutboxAfterCommit = true;
    options.OutboxProcessorBatchSize = 100;
});
```

`ProcessOutboxAfterCommit` defaults to `true`: disable it only when you accept
waiting for the recurring outbox sweep in exchange for lower commit-path work.
Tune `OutboxProcessorBatchSize` from observed backlog and database capacity;
do not increase it blindly.

## A safe tuning loop

1. Establish a baseline with representative starts, resumes, faults, and
   restarts.
2. Identify one bottleneck in traces, metrics, and persistence telemetry.
3. Change one commit policy, worker count, or outbox setting.
4. Re-run the same load and recovery test; compare throughput, latency,
   database pressure, and incident rate.
5. Keep the change only when the whole operating profile improves.

For distributed topology and locking prerequisites, see [Distributed
Hosting](../../hosting/distributed-hosting.md). For retaining less execution-log
data, see [Log Persistence](../../optimize/log-persistence.md) and make that a
separate measurement-driven decision.

## Related guides

- [Throughput tuning examples](examples/throughput-tuning.md)
- [Worker count](../../optimize/workers.md)
- [Distributed tracing](../../operate/distributed-tracing.md)
- [Source references](README-REFERENCES.md)
