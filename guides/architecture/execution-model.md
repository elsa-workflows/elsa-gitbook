---
description: >-
  Release-backed guide to Elsa's execution model in 3.8.0, covering direct
  execution, dispatched execution, triggers, bookmarks, stimuli, persistence,
  and recovery.
---

# Execution Model

This guide explains how Elsa `3.8.0` starts workflows, pauses them, resumes
them, and persists their state. It is intended for both developers wiring Elsa
into an application and Studio users who need a reliable mental model for what
happens after a workflow is published or executed.

If you want the broad platform picture first, read the
[Architecture](README.md) overview. If you need activity-level waiting and
resume patterns, read
[Long-Running Workflows](../running-workflows/long-running-workflows.md).

## The four execution paths

Elsa uses four closely related runtime paths:

| Path | Primary service | What it does | Best for |
| --- | --- | --- | --- |
| Direct execution | `IWorkflowRunner` | Runs a workflow in-process immediately | tests, simple in-process execution, custom host code |
| Runtime client | `IWorkflowRuntime` | Creates workflow clients that start, run, and resume persisted instances | most application code |
| Background dispatch | `IWorkflowDispatcher` | Queues definition, instance, trigger, and resume work for background execution | server-side and distributed execution |
| Stimulus delivery | `IStimulusSender` / `IStimulusDispatcher` | Matches external stimuli to triggers and bookmarks | HTTP, timers, messages, events, callbacks |

In practice:

- `IWorkflowRunner` is the low-level "run now" path.
- `IWorkflowRuntime` is the higher-level operational API.
- `IWorkflowDispatcher` is the asynchronous queueing boundary.
- stimuli are how external events start new instances or resume waiting ones.

## Start, run, pause, resume

The normal lifecycle in `3.8.0` looks like this:

1. A workflow definition is published.
2. Elsa indexes startable trigger activities from the published definition.
3. A workflow is started directly, dispatched, or matched by a trigger.
4. Activities execute until the workflow finishes or an activity creates a
   bookmark.
5. The commit-state handler persists bookmarks, variables, execution logs, and
   workflow state.
6. Later, a matching stimulus resumes the workflow from the stored bookmark.

This cycle can repeat many times for one workflow instance.

## Execute versus dispatch

### Execute

`IWorkflowRunner` builds a `WorkflowExecutionContext`, schedules the workflow,
and runs it in the current process. It is the most direct path and does not
queue work.

Use direct execution when:

- you are writing tests
- you need an immediate result in the same process
- you are running a workflow without background infrastructure

### Dispatch

`IWorkflowDispatcher` is the queueing abstraction. In `3.8.0`, it supports four
request types:

- `DispatchWorkflowDefinitionRequest` to start a new instance from a definition
- `DispatchWorkflowInstanceRequest` to continue an existing instance
- `DispatchTriggerWorkflowsRequest` to start workflows from a trigger stimulus
- `DispatchResumeWorkflowsRequest` to resume workflows waiting on bookmarks

The default `BackgroundWorkflowDispatcher` sends these requests to background
command handling. When dispatch happens during workflow execution and
transactional outbox support is enabled, `TransactionalWorkflowDispatcher`
writes the work to Elsa's workflow dispatch outbox first, then hands it off
after state commit.

Use dispatch when:

- the caller should return before workflow work completes
- you need queue-based orchestration that can be combined with persistent
  runtime storage and distributed hosting
- child workflow execution should not be tied to the current request lifetime

For lower-level dispatch details, see
[Workflow Dispatcher Architecture](workflow-dispatcher.md).

## Triggers, bookmarks, and stimuli

These three concepts are the core of Elsa's event-driven model.

### Triggers start new workflow instances

In `3.8.0`, Elsa indexes triggers from published workflow definitions using
`TriggerIndexer`. Two conditions matter:

- the activity must implement `ITrigger`
- the activity must be marked `CanStartWorkflow`

That means not every blocking activity automatically becomes a start trigger.
For example, timer-style activities only start new workflow instances when the
workflow designer or code marks them as startable.

The stored trigger record contains the workflow definition/version IDs, the
activity ID, a trigger name, an optional payload, and a deterministic hash.

For Studio users, this is the practical rule:

- publishing a workflow makes Elsa index its start triggers
- unpublishing or changing the workflow causes trigger reindexing
- if a workflow does not start from an expected trigger, first confirm the
  activity is configured as a start trigger

### Bookmarks pause existing workflow instances

Bookmarks belong to workflow instances, not workflow definitions. Activities
create them through `ActivityExecutionContext.CreateBookmark(...)`.

When a bookmark is created, Elsa:

- captures the bookmark name, payload, hash, activity ID, activity node ID, and
  activity instance ID
- adds the bookmark to the execution context
- persists it during state commit

`CreateBookmark(...)` hashes the bookmark using the bookmark name, the payload,
and optionally the activity instance ID. That is why resume payload shape must
match what the activity originally stored.

Use bookmarks when a workflow instance should wait for:

- an HTTP callback or approval link
- an event or signal
- a timer or scheduled wake-up
- a message from another system
- a background task completion signal

### Stimuli either start or resume work

Stimuli are the external inputs Elsa tries to match against stored triggers and
bookmarks.

In `3.8.0`, `StimulusSender` first tries to start new workflows when the
stimulus is not scoped to a specific existing instance or activity. It then
tries to resume matching bookmarks.

That means one incoming event can do both:

- start new instances from trigger definitions
- resume existing suspended instances

If no bookmark matches, Elsa enqueues the stimulus in the bookmark queue as a
reliability measure. The recurring `TriggerBookmarkQueueRecurringTask` keeps
signaling the queue worker so recently created bookmarks can still pick up
stimuli that arrived slightly too early.

## What gets persisted

In `3.8.0`, bookmark persistence is no longer handled by the old bookmark
middleware. `DefaultCommitStateHandler` now commits runtime state.

On commit, Elsa persists:

- bookmark changes
- activity execution logs
- workflow execution logs
- persisted variables
- the workflow instance state itself

After saving state, Elsa emits `WorkflowStateCommitted`, which is also the hook
used to process deferred dispatch outbox work.

For operators, the key implication is simple: if you want workflows to survive
restarts, resumes, and delayed callbacks, configure runtime persistence rather
than relying on in-memory-only execution.

## How resume matching works

When Elsa resumes bookmarks, `WorkflowResumer`:

1. builds a bookmark filter from bookmark ID or from hashed stimulus data
2. acquires a distributed lock for that filter
3. loads matching bookmarks
4. creates a workflow client for each workflow instance
5. runs the instance from the matched bookmark

This lock is important in clustered deployments because it reduces duplicate
resume attempts across nodes.

## How to choose the right model

| Need | Use | Why |
| --- | --- | --- |
| Run a workflow immediately in process | `IWorkflowRunner` | simplest path, no queue boundary |
| Start or resume managed persisted workflows from application code | `IWorkflowRuntime` client API | higher-level operational API |
| Queue work for background execution | `IWorkflowDispatcher` | decouples execution from caller lifetime |
| React to external events | trigger plus stimulus delivery | starts new instances automatically |
| Pause and continue later | bookmark-based activity | persists wait state and resumes on matching stimulus |

## Practical examples

### Start immediately in code

```csharp
var result = await workflowRunner.RunAsync(workflow);
```

### Queue a new workflow instance

```csharp
await workflowDispatcher.DispatchAsync(new DispatchWorkflowDefinitionRequest(definitionVersionId)
{
    CorrelationId = orderId,
    Input = new Dictionary<string, object>
    {
        ["OrderId"] = orderId
    }
});
```

### Resume workflows waiting on a known stimulus

```csharp
await workflowDispatcher.DispatchAsync(new DispatchResumeWorkflowsRequest(
    activityTypeName: "OrderApprovedActivityType",
    bookmarkPayload: new { OrderId = orderId }));
```

The `activityTypeName` must match the bookmark-producing activity type or
bookmark name used when the workflow paused.

## Common mistakes

- Treating every blocking activity as a start trigger. In `3.8.0`, start
  triggers must be both `ITrigger` activities and marked `CanStartWorkflow`.
- Using in-memory-only runtime services for workflows that must survive process
  restarts.
- Sending a resume payload that does not match the original bookmark payload
  shape, causing the hash lookup to miss.
- Assuming dispatch means "already executed". Dispatch only means the request
  has been queued successfully.
- Expecting an unpublished workflow definition to receive trigger traffic.

## Related guides

- [Architecture](README.md)
- [Workflow Dispatcher Architecture](workflow-dispatcher.md)
- [Long-Running Workflows](../running-workflows/long-running-workflows.md)
- [Using a Trigger](../running-workflows/using-a-trigger.md)
- [Timer and Scheduled Workflows](../running-workflows/timer-and-scheduled-workflows.md)
- [Workflow Context](../../getting-started/concepts/workflow-context.md)
