# Long-Running Workflows

Use long-running workflows when work should pause and resume instead of
finishing in a single request or execution burst. In Elsa `release/3.8.0`, that
model is built on bookmarks, triggers, scheduling, and runtime recovery.

This guide connects the pieces that are otherwise spread across the scheduling,
running, clustering, and troubleshooting docs.

## Runtime prerequisites

At minimum, enable the workflow runtime:

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseWorkflowRuntime());
```

If the workflow waits for future timestamps such as `Delay`, `Timer`, `Cron`,
or `StartAt`, also enable scheduling:

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

- use runtime persistence if instances must survive process restarts
- use scheduling if workflows wait for future times
- use distributed runtime and clustered scheduling when multiple nodes can
  resume the same work

The sample server in `release/3.8.0` enables runtime persistence,
`UseDistributedRuntime()`, and `UseScheduling()` together for exactly this
reason.

## How Elsa pauses and resumes

When an activity calls `CreateBookmark(...)`, Elsa adds a bookmark to workflow
state and suspends the workflow after pending work completes.

| Concept | What it does | Typical examples |
| --- | --- | --- |
| Bookmark | Pause point for an existing workflow instance | `Delay`, inline `Timer`, `Event`, `RunTask` |
| Trigger | Start point for a workflow definition | `HttpEndpoint`, trigger `Timer`, trigger `Cron`, trigger `StartAt` |
| Stimulus | Payload used to match a bookmark or trigger | event name, HTTP route, timer payload, task ID |

In `release/3.8.0`, `WorkflowResumer` matches bookmarks by hash and acquires a
distributed lock before resuming them. That is why clustered resume operations
do not rely on sticky sessions.

## Choose the right pattern

Use the simplest pattern that matches the business event:

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

- `Delay` creates a bookmark with a `ResumeAt` timestamp
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

This is different from an inline timer. In `release/3.8.0`, trigger timers are
scheduled through `DefaultTriggerScheduler.ScheduleRecurringAsync`, while inline
timers are one-shot waits.

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

- Long-running durability depends on runtime persistence for bookmarks,
  triggers, and queued resume work.
- `TriggerBookmarkQueueRecurringTask` periodically signals bookmark queue
  processing so queued stimuli are not missed.
- `PurgeBookmarkQueueRecurringTask` removes expired bookmark queue items based on
  `BookmarkQueuePurgeOptions`.
- `RestartInterruptedWorkflowsTask` looks for workflow instances that are still
  marked as executing but have been inactive longer than
  `RuntimeOptions.InactivityThreshold`, then requeues them.
- For multi-node hosting, pair long-running workflows with the clustered
  guidance in [Clustering](../clustering/README.md) and
  [Distributed Hosting](../../hosting/distributed-hosting.md).

## Common mistakes

- Using in-memory runtime storage for workflows that must survive restarts.
- Treating inline `Timer` or inline `Cron` as recurring loops. They are one-shot
  waits unless they start the workflow as triggers.
- Expecting synchronous HTTP responses from workflows that can suspend.
- Forgetting clustered locking and scheduling when multiple nodes can process
  the same bookmarks.

## Related guides

- [Workflow Context](../../getting-started/concepts/workflow-context.md)
- [Blocking Activities & Triggers](../../activities/blocking-and-triggers/README.md)
- [Timer and Scheduled Workflows](./timer-and-scheduled-workflows.md)
- [Running Workflows](./README.md)
- [Clustering](../clustering/README.md)
