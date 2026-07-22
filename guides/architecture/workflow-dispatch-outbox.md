---
description: >-
  Release-backed guidance for configuring, operating, and diagnosing Elsa's
  transactional outbox for workflow dispatch.
---

# Workflow dispatch outbox

Use the workflow dispatch outbox when a workflow dispatch must not become
visible until the current workflow state has been committed. It coordinates
in-workflow dispatch with the owner's state commit and provides recovery when
delivery is delayed or the host restarts.

The outbox is for Elsa workflow dispatch commands. It is not a general-purpose
outbox for arbitrary application messages or external side effects.

{% hint style="warning" %}
"Transactional" describes marker-gated coordination: the outbox item is
written before the workflow state commit, and the processor delivers it only
after the committed owner state contains its marker. This is not one atomic
transaction spanning the outbox store and the workflow-state store.
{% endhint %}

## When to use it

Enable it when a workflow starts, resumes, or triggers other workflows and the
child or resumed work must not be dispatched from a parent state that may later
be rolled back. This is especially useful for long-running, distributed, or
failure-sensitive workflows.

The outbox does not change dispatch calls made outside workflow execution. In
that case, Elsa uses the normal background dispatcher. It also does not make
the downstream workflow's activities transactional with the parent workflow.

## Enable the outbox

Configure `WorkflowDispatcherOptions` in the Elsa host:

```csharp
using Elsa.Workflows.Runtime.Options;

builder.Services.Configure<WorkflowDispatcherOptions>(options =>
{
    options.UseTransactionalOutbox = true;
    options.ProcessOutboxAfterCommit = true;
    options.OutboxProcessorBatchSize = 100;
});
```

The Elsa runtime registers the outbox, its processor, the post-commit handler,
and a recurring processor when the workflow runtime feature is enabled. The
default execution pipeline also includes the middleware that exposes the
current workflow execution context to the outbox. If you replace that pipeline,
retain `UseWorkflowDispatchOutbox()`.

The release defaults are:

| Option | Default | Effect |
| --- | ---: | --- |
| `UseTransactionalOutbox` | `false` | Enables transactional dispatch. |
| `ProcessOutboxAfterCommit` | `true` | Attempts delivery after a commit. |
| `OrphanedOutboxItemRetention` | 1 day | Keeps ownerless items. |
| `MaxOutboxDeliveryAttempts` | 10 | Failed sends before abandonment. |
| `OutboxProcessorBatchSize` | 100 | Items loaded per processor cycle. |

`ProcessOutboxAfterCommit = false` does not disable the outbox. It skips the
eager post-commit attempt and leaves delivery to the recurring sweep.

## How delivery works

For a dispatch made during workflow execution, Elsa follows this sequence:

1. The transactional dispatcher creates an outbox item for a definition,
   instance, trigger, or resume dispatch.
2. Elsa stores the item and adds its ID to the current workflow state as an
   ownership marker.
3. The workflow state commit persists that marker with the owner workflow.
4. After the commit, Elsa may try to process the item immediately. A recurring
   task also scans for pending items every 10 seconds by default.
5. The processor takes a distributed lock, verifies that the owner exists and
   that its committed state contains the item ID, and sends the command through
   the background command path.
6. After a successful send, Elsa deletes the outbox item and removes its marker
   from the owner workflow state.

The processor uses a tenant-scoped lock when a tenant is active, and preserves
the item's tenant ID as dispatch headers. Multiple nodes can therefore run the
processor without intentionally sending the same pending item concurrently,
provided they use a shared distributed lock provider. The release default is a
file-system lock under `App_Data/locks`, which is suitable for one machine but
must be replaced for nodes running on separate machines. The lock does not
provide exactly-once delivery.

## What is placed in the outbox

The transactional dispatcher supports these four command kinds:

| Command kind | Meaning |
| --- | --- |
| Workflow definition | Start a new workflow instance from a definition. |
| Workflow instance | Dispatch an existing workflow instance. |
| Trigger workflows | Dispatch workflows matched by a stimulus. |
| Resume workflows | Resume workflows matched by a stimulus or bookmark. |

The **Dispatch Workflow** and **Bulk Dispatch Workflows** activities use
`IWorkflowDispatcher` while they execute, so their child dispatches can enter
the outbox. If either activity is configured to wait for child completion, the
parent still waits on its bookmark; the outbox only controls when the child
dispatch becomes eligible for delivery.

## Failure, retry, and cleanup behavior

### Delivery failures

If sending the command fails, Elsa increments `DeliveryAttempts` and keeps the
item for a later processor cycle. There is no application-level exponential
backoff in this processor; the retry cadence is determined by the eager attempt
and recurring sweep. The processor continues with the next item in the batch
when one item fails.

When the attempt count reaches `MaxOutboxDeliveryAttempts`, Elsa abandons the
item by deleting it and removing its committed marker. If deletion fails, Elsa
preserves the attempt count and tries again during a later cycle.

### Missing or uncommitted owners

An item is not delivered until its owner workflow exists and its committed
state contains the item ID. This prevents an item saved before a failed or
rolled-back owner commit from being dispatched.

If the owner is missing, or the owner never committed the marker, Elsa retains
the item until `OrphanedOutboxItemRetention` expires. Set that option to zero or
less only when immediate cleanup of such items is acceptable.

### At-least-once delivery

Delivery is at-least-once. If Elsa sends the command successfully but cannot
delete the item, the item is not counted as a delivery failure and may be sent
again. The outbox processor does not deduplicate child starts; the owner marker
and outbox item ID only guard commit eligibility. Use application-level
idempotency or deduplication where duplicate delivery would be harmful.

If the item is deleted successfully but marker cleanup fails, Elsa does not
recreate the item. The owner can temporarily retain a stale marker, which a
later commit or state sweep may prune.

Retry limits and orphan cleanup intentionally delete or abandon items. The
release does not provide a separate dead-letter store or replay endpoint, so
retain the relevant logs and failure context if operators need to investigate
an abandoned dispatch.

The default key-value outbox store also includes recovery and index records so
an interrupted store write can be found and repaired on a later scan. For
production, ensure that the configured key-value store and workflow-instance
store are durable and shared by the nodes that process the same tenant/workload.
Without a persistence provider, the release's default key-value store is
in-memory and is not a restart-safe outbox.

## Operating and diagnosing the outbox

Use this checklist when a child workflow is delayed or appears more than once:

- **Dispatch is delayed after a commit:** Check
  `ProcessOutboxAfterCommit`, the 10-second sweep, batch size, processor lock,
  and command-worker backlog.
- **The item remains after a send failure:** Inspect the delivery-failure log
  and `DeliveryAttempts`; confirm the configured maximum has not been reached.
- **The item is never sent:** Confirm the owner workflow committed successfully
  and that the workflow-state store and outbox store are available from the
  same host.
- **The same child appears more than once:** Treat this as possible redelivery
  under at-least-once semantics; inspect outbox deletion failures and make the
  downstream operation idempotent.
- **Items disappear without delivery:** Check orphan retention and the
  `Abandoning workflow dispatch outbox item` warning. Both missing owners and
  max-attempt items are intentionally cleaned up.
- **Only one node appears to process the queue:** This is expected while the
  distributed processor lock is held. Check lock-provider configuration and
  tenant identity when progress stops. A local file lock is not sufficient for
  nodes on separate machines.

For Studio users, the outbox is server-side runtime behavior. Configure it in
the host, then use the activity's **Wait for Completion** option according to
the workflow contract. The option controls whether the parent waits for the
child; it does not turn delivery into a transaction across both workflows.
Core exposes no outbox inspection endpoint, so operational diagnosis relies on
host logs, persistence-store telemetry, and workflow-state investigation.

## Related guides

- [Workflow Dispatcher Architecture](workflow-dispatcher.md) explains the
  dispatcher contracts and request types.
- [Dispatch Workflow Activity](../running-workflows/dispatch-workflow-activity.md)
  explains how to start one child workflow from a workflow.
- [Bulk Dispatch Workflows Activity](../running-workflows/bulk-dispatch-workflows.md)
  explains fan-out dispatch and completion behavior.
- [Performance tuning](../performance/README.md) covers measurement-driven
  batch and commit-path tuning.
- [Distributed hosting](../../hosting/distributed-hosting.md) covers the
  multi-node locking and shared-storage prerequisites.

## Release source references

This guide is grounded in Elsa Core `release/3.8.0` at
[`e96c8f23`](https://github.com/elsa-workflows/elsa-core/tree/e96c8f23c998ee01d1b63151d26832b31be534b):

- [`WorkflowDispatcherOptions`](https://github.com/elsa-workflows/elsa-core/blob/e96c8f23c998ee01d1b63151d26832b31be534b/src/modules/Elsa.Workflows.Runtime/Options/WorkflowDispatcherOptions.cs)
- [`TransactionalWorkflowDispatcher`](https://github.com/elsa-workflows/elsa-core/blob/e96c8f23c998ee01d1b63151d26832b31be534b/src/modules/Elsa.Workflows.Runtime/Services/TransactionalWorkflowDispatcher.cs)
- [`WorkflowDispatchOutboxProcessor`](https://github.com/elsa-workflows/elsa-core/blob/e96c8f23c998ee01d1b63151d26832b31be534b/src/modules/Elsa.Workflows.Runtime/Services/WorkflowDispatchOutboxProcessor.cs)
- [`ProcessWorkflowDispatchOutbox`](https://github.com/elsa-workflows/elsa-core/blob/e96c8f23c998ee01d1b63151d26832b31be534b/src/modules/Elsa.Workflows.Runtime/Handlers/ProcessWorkflowDispatchOutbox.cs)
- [`WorkflowRuntimeFeature`](https://github.com/elsa-workflows/elsa-core/blob/e96c8f23c998ee01d1b63151d26832b31be534b/src/modules/Elsa.Workflows.Runtime/Features/WorkflowRuntimeFeature.cs)
- [`KeyValueFeature`](https://github.com/elsa-workflows/elsa-core/blob/e96c8f23c998ee01d1b63151d26832b31be534b/src/modules/Elsa.KeyValues/ShellFeatures/KeyValueFeature.cs)
- [`WorkflowDispatchCommandFactory`](https://github.com/elsa-workflows/elsa-core/blob/e96c8f23c998ee01d1b63151d26832b31be534b/src/modules/Elsa.Workflows.Runtime/Extensions/WorkflowDispatchCommandFactory.cs)
