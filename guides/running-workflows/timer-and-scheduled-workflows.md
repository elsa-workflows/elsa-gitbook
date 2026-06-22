# Timer and Scheduled Workflows

Use Elsa's scheduling activities when a workflow should pause until a future
time or start on a schedule instead of responding immediately to an HTTP
request, message, or manual dispatch.

In `release/3.8.0`, the scheduling behavior is implemented by:

- `Delay`, `Timer`, `Cron`, and `StartAt` in `Elsa.Scheduling.Activities`
- `DefaultBookmarkScheduler` for turning bookmarks into scheduled work
- `ResumeWorkflowTask` for resuming suspended workflow instances
- `QuartzSchedulerFeature` and `QuartzFeature` when you choose Quartz-backed
  scheduling

## Choose the right activity

Use these activities for different timing behaviors:

| Activity | Use it for | Starts a workflow | Resumes a running workflow |
| --- | --- | --- | --- |
| `Delay` | Pause for a fixed duration | No | Yes |
| `StartAt` | Start or continue at a specific timestamp | Yes | Yes |
| `Timer` | Repeat on a fixed interval | Yes | Yes |
| `Cron` | Repeat using a cron expression | Yes | Yes |

## How Elsa schedules work

When a scheduling activity blocks, Elsa creates a bookmark and hands it to the
bookmark scheduler.

`DefaultBookmarkScheduler` groups bookmarks by stimulus type:

- `Elsa.Delay`
- `Elsa.StartAt`
- `Elsa.Timer`
- `Elsa.Cron`

It then asks `IWorkflowScheduler` to do one of two things:

- `ScheduleAtAsync(...)` for one-time resumes such as `Delay`, `StartAt`, and
  the next `Timer` occurrence
- `ScheduleCronAsync(...)` for recurring cron execution

When the scheduled time arrives, Elsa runs `ResumeWorkflowTask`, which loads the
workflow instance and resumes it at the bookmarked activity.

## Local scheduler vs Quartz

If you only call `UseScheduling()`, Elsa uses the default in-memory
`LocalScheduler`.

That is acceptable for:

- local development
- demos
- single-process workloads where losing scheduled state on restart is
  acceptable

It is not a good fit when you need durable scheduled execution across restarts
or multiple nodes.

For production scheduling, enable Quartz-backed scheduling:

{% code title="Program.cs" %}
```csharp
using Elsa.Extensions;

builder.Services.AddElsa(elsa => elsa
    .UseScheduling(scheduling => scheduling.UseQuartzScheduler())
    .UseQuartz(quartz => quartz.UsePostgreSql(connectionString)));
```
{% endcode %}

`QuartzFeature` configures Quartz itself, while `UseQuartzScheduler()` swaps
Elsa's `IWorkflowScheduler` and cron parser to Quartz-backed implementations.

## Single-node setup

For a single-node application that still needs durable scheduled work, use
Quartz with a persistent store but without clustering:

{% code title="Program.cs" %}
```csharp
using Elsa.Extensions;

builder.Services.AddElsa(elsa => elsa
    .UseScheduling(scheduling => scheduling.UseQuartzScheduler())
    .UseQuartz(quartz => quartz.UseSqlite("Data Source=quartz.db")));
```
{% endcode %}

This keeps scheduled jobs outside process memory so they survive app restarts.

## Multi-node setup

For clustered deployments, combine `UseQuartzScheduler()` with a shared Quartz
database and clustering enabled on the provider:

{% code title="Program.cs" %}
```csharp
using Elsa.Extensions;

builder.Services.AddElsa(elsa => elsa
    .UseScheduling(scheduling => scheduling.UseQuartzScheduler())
    .UseQuartz(quartz => quartz
        .ConfigureClusteringIdentity()
        .UsePostgreSql(connectionString, useClustering: true)));
```
{% endcode %}

In `release/3.8.0`, `QuartzFeature.ConfigureClusteringIdentity(...)` sets the
scheduler ID and name that Quartz uses for clustered coordination. The shared
job store is what lets only one node claim each scheduled resume.

## Delay

`Delay` pauses the current workflow instance for a `TimeSpan`.

Internally, `Delay.Execute(...)` calls `context.DelayFor(timeSpan)`, which:

- resolves the current UTC clock
- calculates `resumeAt = now + delay`
- creates a bookmark with an `Elsa.Delay` stimulus and `DelayPayload`

{% code title="ApprovalWorkflow.cs" %}
```csharp
using Elsa.Scheduling.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Activities;

public class ApprovalWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new WriteLine("Waiting one day before sending a reminder."),
                Delay.FromDays(1),
                new WriteLine("Reminder window opened.")
            }
        };
    }
}
```
{% endcode %}

Use `Delay` when the workflow is already running and should simply wait before
continuing.

## StartAt

`StartAt` triggers execution at a specific `DateTimeOffset`.

Two behaviors matter:

- if the workflow reaches `StartAt` after the requested time has already
  passed, the activity completes immediately
- if the timestamp is in the future, Elsa creates an `Elsa.StartAt` bookmark

{% code title="ProgrammaticWorkflow.cs" %}
```csharp
using Elsa.Scheduling.Activities;
using Elsa.Workflows;

public class ProgrammaticWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new StartAt(DateTimeOffset.UtcNow.AddHours(2))
        {
            CanStartWorkflow = true
        };
    }
}
```
{% endcode %}

Use `StartAt` when you need an explicit future timestamp instead of "wait this
many minutes".

## Timer

`Timer` is for recurring execution with a fixed `TimeSpan` interval.

`TimerBase.Execute(...)` calls `context.RepeatWithInterval(...)`, which behaves
differently depending on whether the activity is acting as the workflow trigger:

- when the workflow is starting from the timer trigger, Elsa completes the
  trigger path immediately
- otherwise, Elsa creates an `Elsa.Timer` bookmark with the next `ResumeAt`
  timestamp

{% code title="HeartbeatWorkflow.cs" %}
```csharp
using Elsa.Scheduling.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Activities;

public class HeartbeatWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new Timer(TimeSpan.FromMinutes(5)) { CanStartWorkflow = true },
                new WriteLine("Heartbeat workflow started by timer.")
            }
        };
    }
}
```
{% endcode %}

Use `Timer` when the interval is naturally expressed as a `TimeSpan` such as
every 5 minutes or every 24 hours.

## Cron

`Cron` is for recurring execution based on a cron expression.

In `release/3.8.0`, Elsa's built-in `CronosCronParser` parses expressions with
seconds included, and `UseQuartzScheduler()` swaps that parser for
`QuartzCronParser`.

Blank cron expressions are treated as disabled:

- a trigger `Cron` activity is skipped during trigger creation
- an inline `Cron` activity completes immediately instead of scheduling a
  bookmark

When it is not starting the workflow directly, `Cron.ExecuteAsync(...)`:

- resolves the cron parser from DI
- calculates the next occurrence
- writes that timestamp to journal data as `ExecuteAt`
- creates a `CronBookmarkPayload`

{% code title="NightlyWorkflow.cs" %}
```csharp
using Elsa.Scheduling.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Activities;

public class NightlyWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new Cron("0 0 2 * * ?") { CanStartWorkflow = true },
                new WriteLine("Nightly maintenance started.")
            }
        };
    }
}
```
{% endcode %}

Use `Cron` when you need calendar-based schedules such as "2 AM every day" or
"every Monday at 09:00".

## Studio usage

In Elsa Studio:

1. Add `Delay`, `StartAt`, `Timer`, or `Cron` from the scheduling category.
2. Mark `StartAt`, `Timer`, or `Cron` as a start trigger when the workflow
   should begin from that activity.
3. Configure the duration, timestamp, interval, or cron expression.
4. Publish the workflow.

If scheduled workflows behave differently across environments, verify that the
runtime node actually runs the configured scheduler.

## Operational guidance

- Use the local scheduler only when losing scheduled state on restart is
  acceptable.
- Use Quartz with shared storage for clustered or durable scheduled execution.
- Keep all cluster nodes on consistent time settings; scheduling uses UTC clock
  calculations in Elsa and coordinated execution in Quartz.
- If a scheduled workflow instance is deleted before resume time,
  `ResumeWorkflowTask` logs a warning and skips execution instead of failing the
  scheduler.

## Troubleshooting

- If a timer or cron workflow never fires, confirm `UseScheduling()` is enabled.
- If scheduled work disappears after restart, you are likely using the in-memory
  `LocalScheduler` instead of Quartz persistence.
- If timers fire multiple times in a cluster, verify Quartz uses a shared store
  with clustering enabled.
- If a `StartAt` activity appears to do nothing, check whether the configured
  timestamp is already in the past.

## Related topics

- [Using a Trigger](using-a-trigger.md)
- [Clustering](../clustering/README.md)
- [Troubleshooting](../troubleshooting/README.md)
- [Performance & Scaling](../performance/README.md)
