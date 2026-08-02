---
description: Release-backed guidance for workflows that pause and resume later.
---

# Long-Running Workflows

Use a long-running workflow when the work must pause and resume after the
current execution burst. In Elsa `release/3.8.0`, the runtime represents that
pause with bookmarks, stores the workflow state, and resumes it when a matching
stimulus arrives.

This guide connects the runtime model to the choices made by process designers,
application developers, and operators.

## What makes a workflow long-running?

A workflow is long-running when it creates a wait point and must continue later.
Typical wait points include:

- a relative or scheduled pause such as `Delay`, `Timer`, `Cron`, or `StartAt`;
- an external callback, approval, event, signal, HTTP request, or message; and
- a background task requested with `RunTask`.

The important question is whether the workflow instance must survive beyond the
current request or execution burst. If it does, plan for persistence, resume
behavior, and operational recovery from the start.

## The runtime model

| Concept | Role | Examples |
| --- | --- | --- |
| Bookmark | Wait point for an existing workflow instance | `Delay`, inline `Timer`, `Event`, `RunTask` |
| Trigger | Start point for a workflow definition | `HttpEndpoint`, trigger `Timer`, `Cron`, `StartAt` |
| Stimulus | Data used to match a bookmark or trigger | event name, HTTP route, timer payload, task ID |

When an activity creates a bookmark, Elsa records the wait condition for the
current instance and ends the current execution cycle. A later stimulus is
matched to the stored bookmark, then the runtime resumes the instance.

In `release/3.8.0`, a waiting instance has:

- `WorkflowStatus.Running` as its top-level status; and
- `WorkflowSubStatus.Suspended` while it waits for external stimuli.

This matters when filtering instances in Studio or through the API: do not look
for a top-level `Suspended` workflow status.

## Host capabilities you need

Long-running workflows usually need these capabilities:

| Need | Elsa configuration | Why it matters |
| --- | --- | --- |
| Pause and resume instances | `UseWorkflowRuntime()` | Registers the runtime, bookmark store, and resume services |
| Wait for a future time | `UseScheduling()` | Schedules `Delay`, `Timer`, `Cron`, and `StartAt` work |
| Survive process restarts | Runtime persistence | Stores workflow instances, bookmarks, triggers, and runtime logs durably |
| Run on multiple nodes | `UseDistributedRuntime()` plus shared persistence/locking | Coordinates runtime work across nodes |

The runtime and scheduling features are separate. Enable scheduling only when
the workflow actually uses time-based waits or schedule triggers.

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseWorkflowRuntime(runtime =>
    {
        runtime.UseEntityFrameworkCore(ef => ef.UseSqlite());
        runtime.UseDistributedRuntime();
    })
    .UseScheduling());
```

This follows the released `Elsa.Server.Web` setup. Replace SQLite with the
persistence provider appropriate for your deployment. If no runtime persistence
provider is configured, the default runtime uses in-memory bookmark, trigger,
queue, and execution-log stores; those defaults are suitable for development,
not for workflows that must survive a restart.

The default scheduling feature also registers an in-process `LocalScheduler`.
Use a durable scheduler such as Quartz or Hangfire when scheduled work must
survive restarts or coordinate across nodes. See [Timer and Scheduled
Workflows](./timer-and-scheduled-workflows.md) for scheduler-specific setup.

## Choose the right pattern

| Need | Best fit | Behavior |
| --- | --- | --- |
| Wait for a duration | `Delay` | One relative pause in the current instance |
| Wait until a timestamp | `StartAt` | One absolute wait; completes immediately if the time is already past |
| Continue at the next calendar match | Inline `Cron` | One wait for the next occurrence, then continue |
| Start new instances on an interval | Trigger `Timer` | Recurring new-workflow starts |
| Start new instances on a calendar schedule | Trigger `Cron` | New-workflow starts for each matching occurrence |
| Start once at a timestamp | Trigger `StartAt` | One scheduled workflow start |
| Wait for a callback or event | Bookmark or blocking activity | Resume the existing instance when the stimulus matches |
| Wait for application work | `RunTask` | Dispatch a task request and resume when the host reports back |

## Pattern 1: wait for a future time

Use scheduling activities inline when the current instance should continue
later.

```csharp
using Elsa.Scheduling.Activities;
using Elsa.Workflows.Activities;

builder.Root = new Sequence
{
    Activities =
    {
        new WriteLine("Request received"),
        new Delay(TimeSpan.FromHours(4)),
        new WriteLine("Sending follow-up")
    }
};
```

In `release/3.8.0`:

- `Delay` exposes the `TimeSpan` input and calls `context.DelayFor(...)`;
- inline `Timer` creates a one-shot timer bookmark;
- inline `Cron` creates a bookmark for the next cron occurrence; and
- inline `StartAt` creates a bookmark only when its `DateTime` is in the future.

The scheduling module sends these bookmarks to the bookmark scheduler, which
uses `IWorkflowScheduler` to schedule existing workflow instances.

## Pattern 2: start new instances on a schedule

Use a scheduling activity as a trigger when each occurrence should start a new
workflow instance rather than resume an existing one.

```csharp
using Elsa.Scheduling.Activities;

builder.Root = new Timer(TimeSpan.FromMinutes(15))
{
    CanStartWorkflow = true
};
```

The released trigger scheduler treats `Timer`, `Cron`, and `StartAt` triggers as
new-workflow requests. The equivalent activities inside a running workflow are
bookmarks instead, so they resume that same instance.

For a calendar schedule, use `Cron` rather than trying to convert a wall-clock
time into a fixed `Timer` interval. Prefer UTC timestamps for `StartAt` values.

## Pattern 3: wait for an external callback

Custom approval and callback activities create a bookmark and expose a
tokenized resume URL. The built-in bookmark resume endpoint accepts both `GET`
and `POST` requests:

```text
{api-route-prefix}/bookmarks/resume?t={token}
```

With the default API route prefix, this is commonly
`/elsa/api/bookmarks/resume?t=...`. The endpoint validates the token before
resuming the bookmark. A `POST` can also carry workflow input in its JSON body.

Add `async=true` when the caller should enqueue the resume and return without
waiting for the workflow execution to finish:

```text
/elsa/api/bookmarks/resume?t={token}&async=true
```

Protect the URL and choose an expiration appropriate to the business process.
The endpoint is intentionally anonymous at the HTTP endpoint level because the
token is the resume credential; the token itself must therefore be treated as
secret.

## Pattern 4: wait for background work

`RunTask` coordinates work performed by the host application or another
component:

1. Elsa generates a task ID and creates a bookmark keyed by that task ID.
2. Elsa dispatches a `RunTaskRequest` through `ITaskDispatcher`.
3. The host performs the task and reports a matching `RunTaskStimulus`.
4. Elsa resumes the workflow and exposes the reported value as activity output.

Use `RunTask` when the workflow should own the business process but the actual
work belongs to an application service or worker.

## What operators should inspect

The released workflow-instance list API supports these filters:

- `status=Running` for active instances;
- `subStatus=Suspended` for instances waiting on a bookmark or other stimulus;
- `hasIncidents=true` for instances with recorded incidents; and
- `read:workflow-instances` permission for the list, state, and journal APIs.

For a waiting instance, an operator can:

1. list instances with `Status=Running` and `SubStatus=Suspended`;
2. inspect the instance and its execution state;
3. review the journal at `POST /workflow-instances/{id}/journal`;
4. check `HasIncidents` or `SubStatus=Faulted` if the last resume failed; and
5. verify the external stimulus, schedule, or callback token before retrying.

A wait is not an incident. An incident is recorded when activity execution
faults. The selected incident strategy can fault the workflow or allow it to
continue with the incident recorded; see [Incident
Strategies](../../operate/incidents/strategies.md).

For Studio users, the same model means that a waiting workflow remains visible
as an active instance with a suspended sub-status. In the workflow instance
list, choose the `Suspended` sub-status; use the `Has Incidents` filter when
looking for failed resumes. Open the instance details and journal to
distinguish a healthy wait from a failed resume.

## Recovery and multi-node behavior

The runtime includes background work that supports recovery:

- `TriggerBookmarkQueueRecurringTask` signals the bookmark queue worker so
  deferred stimuli are processed reliably;
- `PurgeBookmarkQueueRecurringTask` removes expired bookmark queue items; and
- `RestartInterruptedWorkflowsTask` finds stale running instances using
  `RuntimeOptions.InactivityThreshold` and asks the runtime to restart them.

Graceful-drain recovery is a separate path: the runtime persists
`WorkflowSubStatus.Interrupted` and `RecoverInterruptedWorkflowsStartupTask`
requeues those instances when the next runtime generation starts. Do not treat
that sub-status as the same condition as a stale `Executing` instance found by
the timeout-based task above.

`WorkflowResumer` acquires a distributed lock for the bookmark filter before it
loads matching bookmarks and resumes their instances. In a multi-node host,
configure a shared distributed lock provider and shared persistence; sticky
sessions are not a substitute for runtime coordination.

## Common mistakes

- Using in-memory runtime storage for workflows that must survive a restart.
- Looking for `WorkflowStatus.Suspended` instead of
  `WorkflowStatus.Running`/`WorkflowSubStatus.Suspended`.
- Treating an inline `Timer` or `Cron` as a recurring loop. Inline scheduling
  waits once; trigger scheduling starts new instances repeatedly.
- Using a fixed `Timer` interval for a calendar schedule.
- Expecting a synchronous HTTP request to finish while a workflow is waiting.
- Exposing a bookmark resume token in logs, URLs, or untrusted client state.
- Running multiple nodes without shared persistence, scheduling, and locking.

## Related guides

- [Timer and Scheduled Workflows](./timer-and-scheduled-workflows.md)
- [Using a Trigger](./using-a-trigger.md)
- [Workflow Context](../../getting-started/concepts/workflow-context.md)
- [Blocking Activities & Triggers](../../activities/blocking-and-triggers/README.md)
- [Incident Strategies](../../operate/incidents/strategies.md)
- [Clustering](../clustering/README.md)
