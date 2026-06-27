---
description: Release-backed guidance for Delay, StartAt, Timer, and Cron workflows.
---

# Timer and Scheduled Workflows

Elsa ships with four scheduling-oriented activities that look similar in the designer but serve different jobs:

| Activity | What it does | Typical use |
| --- | --- | --- |
| `Delay` | Pauses the current workflow instance for a `TimeSpan` | Wait 10 minutes, 2 days, or 30 seconds before continuing |
| `StartAt` | Starts a workflow at a specific `DateTimeOffset`, or waits until that instant inside a running workflow | Launch at a known timestamp such as `2026-07-01T09:00:00Z` |
| `Timer` | Starts new workflow instances on a fixed interval; inside a running workflow it waits one interval before continuing | Poll every 5 minutes or run a recurring maintenance workflow |
| `Cron` | Starts new workflow instances from a cron expression; inside a running workflow it waits until the next matching occurrence | Run every weekday at 06:00 UTC |

This behavior is grounded in the `Elsa.Scheduling` module on `release/3.8.0`:

* `Delay` reads `TimeSpan` and calls `context.DelayFor(...)`.
* `StartAt` stores a `StartAtPayload` and completes immediately if the configured time is already in the past.
* `Timer` inherits `TimerBase`, which calls `context.RepeatWithInterval(...)`.
* `Cron` computes the next occurrence from `ICronParser` and creates a cron bookmark.

## Choose the right activity

Use `Delay` when you are already inside a workflow instance and want to pause it for a relative duration.

Use `StartAt` when you need a one-time absolute schedule. This is the best fit for "run exactly at this timestamp".

Use `Timer` when you want a recurring interval. As a workflow trigger, it schedules repeated new workflow runs. Inside a workflow body, it waits one interval and then continues.

Use `Cron` when you need calendar-style recurrence such as weekdays, first day of month, or every 15 minutes.

## How Elsa schedules them

When you enable `UseScheduling(...)`, Elsa registers the scheduling module and, by default, an in-process `LocalScheduler`.

For workflow bookmarks, `DefaultBookmarkScheduler` translates scheduling bookmarks into scheduler requests:

* `Delay`, `StartAt`, and `Timer` bookmarks are scheduled with `ScheduleAtAsync(...)`.
* `Cron` bookmarks are scheduled with `ScheduleCronAsync(...)`.

For workflow triggers, `DefaultTriggerScheduler` does the equivalent work for published workflow definitions:

* `Timer` triggers call `ScheduleRecurringAsync(...)`.
* `StartAt` triggers call `ScheduleAtAsync(...)`.
* `Cron` triggers call `ScheduleCronAsync(...)`.

This distinction matters:

* bookmarks resume an existing workflow instance
* triggers start a new workflow instance from a published definition

## Delay

`Delay` is the simplest option when the workflow is already running and only needs to pause for a duration.

```csharp
using Elsa.Workflows.Activities;
using Elsa.Scheduling.Activities;

builder.Root = new Sequence
{
    Activities =
    {
        new WriteLine("Waiting 30 minutes"),
        new Delay(TimeSpan.FromMinutes(30)),
        new WriteLine("Continuing after the delay")
    }
};
```

In `release/3.8.0`, the public input is named `TimeSpan`. Older examples that show `Duration` are not correct for Elsa 3.8.

## StartAt

`StartAt` is a one-time absolute schedule.

```csharp
using Elsa.Scheduling.Activities;

builder.Root = new StartAt(new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero));
```

Important behavior from source:

* the input is `DateTime`, not `StartAt` or `ExecuteAt`
* if the configured time is already in the past when the activity executes inside a running workflow, Elsa completes the activity immediately
* when used as a trigger, Elsa stores a `StartAtPayload` and schedules a single workflow start

Prefer UTC timestamps. The scheduling code uses `ISystemClock.UtcNow`, so using `DateTimeOffset` in UTC avoids avoidable timezone confusion.

## Timer

`Timer` is interval-based, not timestamp-based.

```csharp
using Elsa.Scheduling.Activities;

builder.Root = new Timer(TimeSpan.FromMinutes(5));
```

The public input is `Interval`.

Two common ways to use it:

1. As the first activity in a published workflow definition, where it acts as a recurring trigger and starts a new workflow instance every interval.
2. Inside a running workflow, where it creates a timer bookmark for `UtcNow + Interval` and resumes that same instance once the interval elapses.

If you need "run every day at 09:00", prefer `Cron` or `StartAt`, not `Timer`.

## Cron

`Cron` is for calendar-based recurring schedules.

```csharp
using Elsa.Scheduling.Activities;

builder.Root = new Cron("0 0 6 ? * MON-FRI *");
```

The public input is `CronExpression`.

In `release/3.8.0`:

* core scheduling uses `CronosCronParser` by default
* `Cron` stores the next occurrence in journal data as `ExecuteAt`
* `DefaultTriggerScheduler` skips empty cron payloads and logs warnings for invalid cron expressions

Use cron when your schedule is tied to the calendar rather than a fixed interval.

## Elsa Studio guidance

For Studio users, the practical choice is:

* drag `Delay` into the workflow body for relative waits
* use `StartAt`, `Timer`, or `Cron` as the first activity when you want the workflow definition itself to start on a schedule
* publish the workflow after configuring the trigger, otherwise the trigger will not be scheduled

If you are evaluating a workflow and need to see whether the trigger is starting new instances or resuming an existing one, inspect the workflow instance list and execution logs in Studio after publishing.

## Single-node vs durable scheduling

By default, `SchedulingFeature` registers `LocalScheduler`, which keeps schedules in memory. That is fine for local development and single-node scenarios, but it is not durable across process restarts.

For durable or clustered scheduling, switch the workflow scheduler implementation:

```csharp
services.AddElsa(elsa =>
{
    elsa.UseScheduling(scheduling => scheduling.UseQuartzScheduler());
    elsa.UseQuartz(quartz => quartz.UsePostgreSql(connectionString));
});
```

Quartz support in `release/3.8.0` replaces the scheduling feature's `WorkflowScheduler` with `QuartzWorkflowScheduler` and swaps the cron parser to `QuartzCronParser`.

Hangfire is also available:

```csharp
using Hangfire.SqlServer;

services.AddElsa(elsa =>
{
    elsa.UseHangfire(hangfire => hangfire.UseJobStorage(new SqlServerStorage(connectionString)));
    elsa.UseScheduling(scheduling => scheduling.UseHangfireScheduler());
});
```

Hangfire support replaces the scheduling feature's `WorkflowScheduler` with `HangfireWorkflowScheduler`.

Choose Quartz or Hangfire when you need restart durability or multi-node coordination. Keep the default scheduler when you only need lightweight in-process scheduling.

## Common mistakes

| Mistake | What to do instead |
| --- | --- |
| Using `Timer` to mean "at 09:00 tomorrow" | Use `StartAt` for one time or `Cron` for recurring calendar schedules |
| Using old `Delay { Duration = ... }` examples | Use `Delay { TimeSpan = ... }` or `new Delay(TimeSpan.FromMinutes(...))` |
| Forgetting to publish a trigger-based workflow | Publish the workflow definition so Elsa can schedule its triggers |
| Using in-memory scheduling in a clustered deployment | Use Quartz or Hangfire-backed scheduling |

## Related guides

* [Using a Trigger](using-a-trigger.md)
* [Clustering](../clustering/README.md)
* [Workflow Dispatcher Architecture](../architecture/workflow-dispatcher.md)
* [Blocking Activities & Triggers](../../activities/blocking-and-triggers/README.md)
