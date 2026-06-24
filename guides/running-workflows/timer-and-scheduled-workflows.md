# Timer and Scheduled Workflows

Elsa 3.8.0 provides four built-in scheduling activities that cover most time-based workflow patterns:

* `Delay`: pause a running workflow and resume it later.
* `Timer`: start a workflow repeatedly at a fixed interval, or wait for an interval inside a running workflow.
* `Cron`: start a workflow repeatedly from a cron schedule, or wait for the next cron occurrence inside a running workflow.
* `StartAt`: start a workflow once at a specific timestamp, or continue immediately if that timestamp is already in the past.

Use this guide when you need reminders, polling jobs, recurring background processes, or workflows that wait before continuing.

## Choose the right activity

| Need | Activity |
| --- | --- |
| Pause the current workflow for 5 minutes, 2 hours, or 1 day | `Delay` |
| Run a workflow every fixed interval, such as every 15 minutes | `Timer` |
| Run a workflow on a calendar schedule, such as every weekday at 09:00 UTC | `Cron` |
| Run a workflow once at a known future timestamp | `StartAt` |

The main distinction is this:

* `Delay` is for resuming an existing workflow instance.
* `Timer`, `Cron`, and `StartAt` can act as workflow triggers when `CanStartWorkflow` is enabled.

## How scheduling works in Elsa 3.8.0

At the application level, scheduling is enabled with `UseScheduling()`:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseScheduling();
});
```

In `release/3.8.0`, `UseScheduling()` wires Elsa to the default local scheduler, which is an in-memory, in-process scheduler. That is fine for local development and single-node deployments, but it is not the right operational model for durable multi-node timer execution.

For clustered scheduled workloads, follow the patterns in the [Clustering guide](../clustering/README.md), especially the Quartz-based scheduler pattern and the single-scheduler-node pattern.

## Starting workflows on a schedule

When `Timer`, `Cron`, or `StartAt` should create new workflow instances, the activity must be indexed as a trigger. In practice, that means the activity needs `CanStartWorkflow = true`.

### Timer trigger

Use `Timer` when you want a workflow to start repeatedly at a fixed interval.

```csharp
using Elsa.Scheduling.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Activities;

public class RecurringCleanupWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new Timer(TimeSpan.FromMinutes(15))
                {
                    CanStartWorkflow = true
                },
                new WriteLine("Running scheduled cleanup")
            }
        };
    }
}
```

In 3.8.0, Elsa indexes a timer trigger by calculating `StartAt = UtcNow + Interval` at trigger-index time. In practice, the first run is relative to when the workflow definition is published or re-indexed, not relative to a fixed wall-clock time.

### Cron trigger

Use `Cron` when the schedule must follow calendar rules instead of a simple interval.

```csharp
using Elsa.Scheduling.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Activities;

public class WeekdayReportWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new Cron("0 0 9 * * MON-FRI")
                {
                    CanStartWorkflow = true
                },
                new WriteLine("Generating weekday report")
            }
        };
    }
}
```

Elsa 3.8.0 validates cron expressions through the `Cronos` parser using the six-field format with seconds. For example:

* `0 0 9 * * MON-FRI` means 09:00:00 UTC on weekdays.
* `0 */15 * * * *` means every 15 minutes.

By default in 3.8.0, invalid cron expressions block publishing because workflow publishing fails on validation errors. If you intentionally want publishing to continue while surfacing validation warnings, disable that behavior in workflow management options:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowManagement(management => management.UseFailOnValidationErrors(false));
});
```

### StartAt trigger

Use `StartAt` when the workflow should start once at a specific future timestamp.

```csharp
using Elsa.Scheduling.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Activities;

public class LaunchWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new StartAt(new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero))
                {
                    CanStartWorkflow = true
                },
                new WriteLine("Launch window opened")
            }
        };
    }
}
```

If a stored `StartAt` trigger is already in the past when Elsa schedules it, Elsa still schedules a catch-up execution. Inside an already running workflow instance, however, `StartAt` completes immediately when the configured time is in the past or equal to now.

## Waiting inside a running workflow

### Delay

Use `Delay` to suspend an existing workflow instance and resume it later.

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
                new WriteLine("Initial work"),
                Delay.FromHours(2),
                new WriteLine("Continue after the delay")
            }
        };
    }
}
```

`Delay` creates a bookmark with a specific resume time. It does not start new workflow instances by itself.

### Timer and Cron inside a running workflow

`Timer` and `Cron` can also be used inside a running workflow instead of only at the start.

* `Timer` waits for the configured interval, then continues.
* `Cron` waits until the next matching cron occurrence, then continues.

That makes them useful for recurring loops, polling, and wait-until-next-window patterns where the workflow instance should keep its state between resumptions.

## Using Elsa Studio

In Elsa Studio, these activities are available from the scheduling/toolbox categories exposed by the server's registered activities.

For schedule-driven workflows:

1. Add `Timer`, `Cron`, or `StartAt` near the beginning of the workflow.
2. In the activity properties, enable `CanStartWorkflow`.
3. Publish the workflow so Elsa can index and schedule the trigger.
4. Verify executions from the workflow instances view or logs.

For pause-and-resume workflows:

1. Add a `Delay` activity where the workflow should pause.
2. Configure the delay value.
3. Publish and run the workflow.
4. Inspect the suspended instance if you need to confirm it is waiting on scheduled work.

If scheduled workflows do not fire, check the [Troubleshooting guide](../troubleshooting/README.md) and the [Clustering guide](../clustering/README.md) before assuming the activity configuration is wrong.

## Operational notes

Keep these 3.8.0 behaviors in mind:

* `Timer`, `Cron`, and `StartAt` only become workflow-starting triggers when `CanStartWorkflow` is enabled.
* `Delay` always resumes an existing workflow instance; it is not a start trigger.
* `Timer` schedules its first trigger occurrence relative to trigger indexing time.
* `Cron` uses the Cronos parser with seconds included.
* `UseScheduling()` configures the default local in-memory scheduler.
* Scheduled bookmarks for `Delay`, `Timer`, `Cron`, and `StartAt` are all handed to Elsa's workflow scheduler, so the deployment model determines whether scheduled execution is local-only or suitable for clustered workloads.

## Related guides

* [Using a Trigger](using-a-trigger.md)
* [Running Workflows](README.md)
* [Long-Running Workflows](long-running-workflows.md)
* [Clustering](../clustering/README.md)
* [Troubleshooting](../troubleshooting/README.md)
