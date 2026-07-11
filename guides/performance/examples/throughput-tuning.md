---
description: >-
  Small, release-backed Elsa 3.8.0 configuration examples for testing commit,
  worker, and transactional-outbox tradeoffs under load.
---

# Throughput tuning examples

These are starting configurations, not production targets. Benchmark each
change against your own workflow shape, persistence provider, and downstream
services. Keep recovery tests in the benchmark: throughput is only useful when
the workflow remains correct after a restart or fault.

## Register selectable commit strategies

This configuration registers the standard strategy names and a named periodic
strategy. A workflow definition can select any registered name through
`WorkflowOptions.CommitStrategyName` or the Elsa Studio **Properties →
Settings → Commit Strategy** selector.

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
                "Periodic30Seconds",
                "Every 30 seconds",
                "Commit workflow state at least every 30 seconds during execution.",
                new PeriodicWorkflowStrategy(TimeSpan.FromSeconds(30)));
        });
    });
});
```

Use `WorkflowExecuted` as a test candidate for short workflows only after
verifying the state that must survive a failure before completion. Use
`ActivityExecuted` when persistence after each completed activity is more
important than the additional writes. Test a named periodic strategy for
long-running workflows that need a bounded recovery interval. In this example,
the stored strategy name is `Periodic30Seconds`; Studio shows `Every 30
seconds`.

## Set a host fallback

Definitions with no commit-strategy selection use the host fallback. This is
not added to the Studio/API registry, so register a named strategy separately
when authors should be able to choose it.

```csharp
using Elsa.Extensions;
using Elsa.Workflows.CommitStates.Strategies;

builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflows(workflows =>
    {
        workflows.WithDefaultWorkflowCommitStrategy(
            new PeriodicWorkflowStrategy(TimeSpan.FromSeconds(30)));
    });
});
```

## Increase only the measured worker queue

The three mediator queues default to four workers each. If command processing
is the proven bottleneck, begin with a modest command-only change and compare
the result with the baseline.

```csharp
using Elsa.Mediator.Options;

builder.Services.Configure<MediatorOptions>(options =>
{
    options.CommandWorkerCount = 8;
    options.JobWorkerCount = 4;
    options.NotificationWorkerCount = 4;
});
```

The command path is relevant to background workflow dispatch. Do not increase
the other counts merely to match it: notifications and jobs can put additional
load on the same persistence and external dependencies.

## Use the transactional outbox for in-workflow dispatch

Enable the outbox when dispatching another workflow must be coordinated with
the current workflow's state commit. It is not a free throughput feature: it
adds persisted outbox work, so measure both successful dispatch latency and
recovery behavior.

```csharp
using Elsa.Workflows.Runtime.Options;

builder.Services.Configure<WorkflowDispatcherOptions>(options =>
{
    options.UseTransactionalOutbox = true;
    options.ProcessOutboxAfterCommit = true;
    options.OutboxProcessorBatchSize = 100;
});
```

If the immediate delivery work is itself a measurable commit-path bottleneck,
test `ProcessOutboxAfterCommit = false`. Delivery will then rely on the
recurring processor, so validate the resulting dispatch delay and restart
behavior before adopting it.

## Compare results consistently

For every run, capture:

- completed workflows per interval and end-to-end latency;
- `elsa.workflow.started`, `elsa.workflow.completed`, and
  `elsa.workflow.faulted` rates;
- `elsa.activity.duration` percentiles and the slowest activity spans;
- database resource use and commit latency; and
- the result of a forced-restart recovery test.

Use the same traffic mix for each run. Change one variable, retain the result
only if it improves the system rather than shifting pressure to the database or
a downstream dependency.

See [Performance tuning](../README.md) for the decision guide and
[Distributed tracing](../../../operate/distributed-tracing.md) for Elsa's
OpenTelemetry instrumentation.
