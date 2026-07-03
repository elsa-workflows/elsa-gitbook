# Long-Running Workflows

Use long-running workflows when work should pause and resume instead of
finishing in a single request or execution burst. In Elsa `release/3.8.0`, that
model is built on bookmarks, triggers, scheduling, queued stimuli, and runtime
recovery.

This guide connects the pieces that are otherwise spread across the scheduling,
running, clustering, and troubleshooting docs.

## What makes a workflow long-running

A workflow becomes long-running when it creates a wait point and Elsa persists
enough runtime state to continue later. Typical wait points are:

- a scheduled pause such as `Delay`, inline `Timer`, inline `Cron`, or inline
  `StartAt`
- a callback wait such as an approval link or other bookmark-based resume
- a trigger or blocking activity waiting for external input such as HTTP,
  events, signals, or broker messages
- a background hand-off such as `RunTask`

The important distinction is whether the current workflow instance must survive
beyond the current execution burst. If yes, design it as long-running from the
start.

## Host capabilities you need

Long-running workflows usually need more than one Elsa module:

| Need | Required module | Why it matters |
| --- | --- | --- |
| Pause and resume existing instances | `UseWorkflowRuntime()` | Stores bookmarks, resumes workflow instances, and runs recovery tasks |
| Wait for future timestamps | `UseScheduling()` | Schedules resume work for `Delay`, `Timer`, `Cron`, and `StartAt` |
| Survive process restarts | persistent runtime storage | Keeps workflow instances, bookmarks, and related runtime state durable |
| Run safely on multiple nodes | `UseDistributedRuntime()` plus clustered scheduling/storage | Prevents competing resume work and coordinates background processing |

At minimum, enable the workflow runtime:

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseWorkflowRuntime());
```

If the workflow waits for future timestamps such as `Delay`, `Timer`, `Cron`,
or `StartAt`, also enable scheduling and a runtime persistence provider:

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseWorkflowRuntime(runtime =>
    {
        runtime.UseEntityFrameworkCore(ef => ef.UseSqlite());
        runtime.UseDistributedRuntime();
    })
    .UseScheduling());
```

Three practical rules matter for long-running workflows:

- use runtime persistence if instances, bookmarks, or queued resume work must
  survive process restarts
- use scheduling if workflows wait for future times
- use distributed runtime and clustered scheduling when multiple nodes can
  resume the same work

The sample server in `release/3.8.0` enables runtime persistence,
`UseDistributedRuntime()`, and `UseScheduling()` together for exactly this
reason.

## How Elsa pauses and resumes

When an activity creates a bookmark, Elsa records a wait condition for the
current workflow instance and suspends execution after the current burst
finishes.

| Concept | What it does | Typical examples |
| --- | --- | --- |
| Bookmark | Pause point for an existing workflow instance | `Delay`, inline `Timer`, `Event`, `RunTask` |
| Trigger | Start point for a workflow definition | `HttpEndpoint`, trigger `Timer`, trigger `Cron`, trigger `StartAt` |
| Stimulus | Payload used to match a bookmark or trigger | event name, HTTP route, timer payload, task ID |

In `release/3.8.0`, Elsa uses these runtime paths:

1. The activity writes a bookmark or trigger payload.
2. Elsa stores runtime state through the configured runtime store.
3. If the wait is time-based, Elsa schedules resume work through
   `IWorkflowScheduler`.
4. When the stimulus arrives, `WorkflowResumer` looks up matching bookmarks and
   acquires a distributed lock for the bookmark filter before resuming them.

That lock is why clustered resume operations do not rely on sticky sessions.

## Choose the right activation model

Use the smallest mechanism that matches the business event:

| Need | Best fit | Notes |
| --- | --- | --- |
| Wait for a duration | `Delay` | Clearest one-shot pause |
| Wait until a known timestamp | `StartAt` | Completes immediately if the timestamp is already in the past |
| Continue at the next calendar match | inline `Cron` | Resumes once, then continues |
| Start a workflow on a schedule | trigger `Timer`, `Cron`, or `StartAt` | Starts a new workflow instance |
| Wait for an app or user callback | custom bookmark or tokenized bookmark URL | Good for approvals and external callbacks |
| Wait for a background task to report back | `RunTask` | Creates a bookmark keyed by task ID |
| Wait for an external event or message | trigger/blocking activity pair | For example HTTP, Signal, Event, or MassTransit message activities |

## Pattern 1: wait for a future time

Use scheduling activities inline when the current workflow instance should
continue later instead of starting a brand-new instance.

```csharp
using Elsa.Scheduling.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Activities;

public class FollowUpWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new WriteLine("Request received"),
                new Delay(TimeSpan.FromHours(4)),
                new WriteLine("Sending follow-up")
            }
        };
    }
}
```

In `release/3.8.0`:

- `Delay` calls `context.DelayFor(...)`, which creates a delay bookmark with a
  `ResumeAt` timestamp
- inline `Timer` also creates a one-shot bookmark
- inline `Cron` creates a bookmark for the next cron occurrence
- inline `StartAt` creates a bookmark only when the target time is in the
  future

The scheduling module then hands those bookmarks to
`DefaultBookmarkScheduler`, which schedules resume work through
`IWorkflowScheduler`.

## Pattern 2: start new workflow instances on a schedule

Use the same activities as triggers when the schedule should launch a new
instance each time.

```csharp
new Timer(TimeSpan.FromMinutes(15))
{
    CanStartWorkflow = true
}
```

This is different from an inline timer:

- trigger `Timer` schedules recurring new-workflow starts
- trigger `Cron` schedules recurring new-workflow starts from the cron
  expression
- trigger `StartAt` schedules one future workflow start and logs a catch-up
  message if the configured time is already in the past
- inline scheduling activities wait inside the current workflow instance

In `release/3.8.0`, trigger schedules are created by
`DefaultTriggerScheduler`, while inline waits are created by
`DefaultBookmarkScheduler`.

For the scheduling-specific details, see
[Timer and Scheduled Workflows](./timer-and-scheduled-workflows.md).

## Pattern 3: wait for an external callback

For approvals, webhooks, and external hand-offs, create a bookmark and expose a
resume URL or token.

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;

[Activity("Custom", "Approvals", "Wait for an external approval callback.")]
public class WaitForApproval : Activity
{
    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var bookmark = context.CreateBookmark(OnResumeAsync);
        var resumeUrl = context.GenerateBookmarkTriggerUrl(bookmark.Id, TimeSpan.FromDays(1));
        context.JournalData["ResumeUrl"] = resumeUrl;
        return ValueTask.CompletedTask;
    }

    private async ValueTask OnResumeAsync(ActivityExecutionContext context)
    {
        var decision = context.GetWorkflowInput<string>("Decision");
        await context.CompleteActivityWithOutcomesAsync(decision == "Approved" ? "Approved" : "Rejected");
    }
}
```

`GenerateBookmarkTriggerUrl(...)` comes from Elsa's HTTP integration, so use it
when your host includes the HTTP module.

The built-in resume endpoint in `release/3.8.0` is:

- `GET {RoutePrefix}/bookmarks/resume?t=...`
- `POST {RoutePrefix}/bookmarks/resume?t=...`

With default API settings, that means `/elsa/api/bookmarks/resume?t=...`.

The endpoint also supports `async=true`, which enqueues bookmark resumption
instead of resuming the workflow synchronously in the request.

Use the asynchronous form when the callback should return quickly or when the
resume path might do meaningful work after the bookmark is matched.

## Pattern 4: wait for background work

`RunTask` is useful when the workflow asks the host application to do work
outside the current execution path and continue later with a result.

In `release/3.8.0`, `RunTask`:

- generates a task ID
- creates a bookmark keyed by a `RunTaskStimulus`
- dispatches the task request through `ITaskDispatcher`
- resumes when the host reports back using that task stimulus

Use this when the workflow runtime should coordinate the task, but the task
itself runs elsewhere.

## Resume paths you can rely on

In `release/3.8.0`, long-running workflows typically resume through one of four
paths:

| Resume path | Typical source | What Elsa does |
| --- | --- | --- |
| scheduled resume | `Delay`, inline `Timer`, inline `Cron`, inline `StartAt` | Scheduler enqueues or executes resume work for an existing bookmark |
| bookmark resume endpoint | approval links, custom callback URLs | HTTP endpoint validates token and resumes immediately or enqueues with `async=true` |
| trigger dispatch | HTTP, `Timer`, `Cron`, `StartAt`, message triggers | Elsa starts a new workflow instance from a trigger |
| custom stimulus dispatch | `RunTask`, events, signals, broker callbacks | Elsa matches stored bookmarks or triggers from the incoming stimulus payload |

## Dispatch vs execute

For long-running flows, prefer entry points that do not assume the workflow will
finish in the same HTTP request.

- use `POST {RoutePrefix}/workflow-definitions/{definitionId}/dispatch` when
  the workflow may suspend or continue in the background
- use `GET` or `POST {RoutePrefix}/workflow-definitions/{definitionId}/execute`
  only when the caller expects a synchronous response and the workflow path can
  complete immediately

If an HTTP workflow can block on timers, callbacks, or external events, design
it as a dispatch-and-observe flow instead of a request-response flow.

## Studio notes

In Elsa Studio, the same runtime distinction shows up through configuration:

- enable `Trigger Workflow` when an activity should start the workflow
- leave it disabled when the activity should pause the current path

That rule applies to built-in scheduling activities and to custom activities
that can act as triggers.

## Operational notes

- Long-running durability depends on runtime persistence for workflow
  instances, bookmarks, triggers, and queued resume work.
- `TriggerBookmarkQueueRecurringTask` signals bookmark queue processing for
  queued bookmark resumes and other deferred bookmark stimuli.
- `PurgeBookmarkQueueRecurringTask` removes expired bookmark queue items based on
  `BookmarkQueuePurgeOptions`.
- `RestartInterruptedWorkflowsTask` looks for workflow instances that are still
  marked as executing but have been inactive longer than
  `RuntimeOptions.InactivityThreshold` and asks the runtime to restart them.
- For multi-node hosting, pair long-running workflows with the clustered
  guidance in [Clustering](../clustering/README.md) and
  [Distributed Hosting](../../hosting/distributed-hosting.md).

For operators, the most important runtime settings are:

- runtime persistence provider configuration
- distributed locking configuration when `UseDistributedRuntime()` is enabled
- `RuntimeOptions.InactivityThreshold` for interrupted workflow recovery
- recurring task schedules for bookmark queue triggering and purge
- API and HTTP ingress behavior if workflows are resumed through public or
  semi-public callback URLs

## Minimal operations checklist

Before calling a workflow long-running and production-ready, verify:

1. runtime persistence is configured for the workflow runtime
2. scheduling is enabled for any time-based waits
3. distributed runtime and shared backing stores are configured for multi-node
   hosting
4. resume endpoints or external callback handlers are authenticated or
   token-protected appropriately
5. operators know where to inspect blocked instances, incidents, and queued
   background work

## Common mistakes

- Using in-memory runtime storage for workflows that must survive restarts.
- Treating inline `Timer` or inline `Cron` as recurring loops. They are one-shot
  waits unless they start the workflow as triggers.
- Assuming a trigger activity and the same activity inline have the same
  runtime behavior.
- Expecting synchronous HTTP responses from workflows that can suspend.
- Forgetting clustered locking and scheduling when multiple nodes can process
  the same bookmarks.

## Related guides

- [Workflow Context](../../getting-started/concepts/workflow-context.md)
- [Blocking Activities & Triggers](../../activities/blocking-and-triggers/README.md)
- [Timer and Scheduled Workflows](./timer-and-scheduled-workflows.md)
- [Running Workflows](./README.md)
- [Clustering](../clustering/README.md)
