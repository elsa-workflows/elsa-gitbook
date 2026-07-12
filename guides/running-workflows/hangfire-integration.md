---
description: Configure Hangfire as Elsa's durable workflow scheduler.
---

# Hangfire Integration

Use Hangfire when Elsa's scheduled workflow work must be stored outside the
application process and executed by Hangfire workers. It is a scheduler
replacement for `Delay`, `Timer`, `Cron`, and `StartAt` work; it does not
replace Elsa workflow persistence, workflow dispatching, or clustering setup.

For a single-node development host, `UseScheduling()` uses Elsa's local,
in-memory scheduler. Choose Hangfire when you already operate Hangfire and want
its persistent storage and worker model for Elsa's scheduled workflow jobs.

## What the integration schedules

After you enable `UseHangfireScheduler()`, Elsa replaces the scheduling
feature's `IWorkflowScheduler` implementation with
`HangfireWorkflowScheduler`.

| Elsa schedule | Hangfire job |
| --- | --- |
| A future start of a new workflow instance | A scheduled `RunWorkflowJob` |
| A `Timer` or `Cron` trigger that starts workflow instances | A recurring `RunWorkflowJob` |
| A `Delay`, `Timer`, or `StartAt` bookmark in a running instance | A scheduled `ResumeWorkflowJob` |
| A `Cron` bookmark in a running instance | A recurring `ResumeWorkflowJob` |

The integration carries the current tenant ID into each job and restores that
tenant context before it runs the workflow. Elsa's job handlers ask Hangfire to
fail after its retry attempts are exhausted; decide your broader retry and
incident strategy separately in [Error handling and retry logic](../../operate/incidents/README.md).

## Configure Elsa and Hangfire

Install `Elsa.Scheduling.Hangfire` and the Hangfire storage provider you choose.
For example, the SQL Server setup below also needs `Hangfire.SqlServer`.

```csharp
using Elsa.Extensions;
using Hangfire.SqlServer;

var connectionString = builder.Configuration.GetConnectionString("Hangfire")
    ?? throw new InvalidOperationException("Connection string 'Hangfire' is missing.");

builder.Services.AddElsa(elsa =>
{
    elsa.UseHangfire(hangfire =>
    {
        hangfire.UseJobStorage(new SqlServerStorage(connectionString));
    });

    elsa.UseScheduling(scheduling => scheduling.UseHangfireScheduler());
});
```

`UseHangfire(...)` registers Hangfire's services and background server. Elsa's
defaults are one worker and a one-second schedule-polling interval. Keep those
defaults while proving the integration, then tune them only against the capacity
of the selected Hangfire storage and the cost of the workflows it will run.

```csharp
elsa.UseHangfire(hangfire =>
{
    hangfire.UseJobStorage(new SqlServerStorage(connectionString));
    hangfire.ConfigureBackgroundServerOptions((_, options) =>
    {
        options.WorkerCount = 4;
        options.SchedulePollingInterval = TimeSpan.FromSeconds(5);
    });
});
```

Do not call `UseHangfire(...)` when the host already configures `AddHangfire`
and `AddHangfireServer` itself. The Elsa feature is intended to own that
registration. In that case, wire Elsa's `IWorkflowScheduler` deliberately in
your host instead of registering a second Hangfire server through Elsa.

## Choose and operate storage deliberately

`UseHangfire(...)` uses Hangfire memory storage when no storage is supplied.
That is suitable only for local experimentation: scheduled jobs are lost when
the process stops. Use a durable Hangfire storage implementation for restart
survival or a multi-node deployment.

The released integration ships a general `UseJobStorage(JobStorage)` hook. Its
older `UseSqlServerStorage(...)` and `UseSqliteStorage(...)` convenience APIs
are obsolete; configure the storage directly on `HangfireFeature` instead, as
in the example above. This also lets a host use another Hangfire storage
provider without relying on an Elsa-specific wrapper.

When operating the system, monitor the same Hangfire queues that contain Elsa
jobs. An Elsa unschedule request searches for matching scheduled jobs and jobs
queued on Hangfire's `default` queue. It removes recurring `RunWorkflowJob`
records, too. Do not treat that as a universal cleanup mechanism for every
Hangfire queue or recurring resume job; verify cancellation behavior for your
workflow and storage provider before relying on it operationally.

## Know the boundaries

* `UseHangfireScheduler()` affects Elsa's workflow scheduler. It does not make
  every background activity execute through Hangfire. The package has a
  separate `UseHangfireBackgroundActivityScheduler()` integration for hosts
  that intentionally use Elsa's `IBackgroundActivityScheduler`.
* The scheduler stores and executes work, but it is not a substitute for durable
  workflow state. Configure Elsa persistence separately.
* One-time and recurring job names originate from Elsa scheduler task names.
  Treat them as implementation identifiers, not as a stable dashboard-facing
  naming convention.
* Recurring registrations use Hangfire's `AddOrUpdate` operation. Reusing an
  Elsa scheduler task name replaces the existing recurring job. The `Timer`
  trigger's interval scheduler converts only day, hour, minute, and second
  components to cron and does not use its `startAt` argument. Enabling the
  Hangfire scheduler does not replace Elsa's Cronos parser: a published cron
  trigger must first pass Elsa validation, then be accepted by Hangfire when
  Elsa passes it to the recurring-job manager.
* Hangfire's storage, dashboard exposure, retention, authentication, and
  backup policies belong to your hosting and security design. Elsa does not
  configure those policies for you.

## Verify the integration

1. Configure durable Elsa persistence and a durable Hangfire job storage.
2. Publish a workflow with a future `StartAt`, `Timer`, or `Cron`; or publish
   and run a workflow that reaches a `Delay` bookmark.
3. Confirm the expected scheduled or recurring job appears in Hangfire.
4. Confirm an Elsa workflow instance starts or resumes at the expected time and
   under the correct tenant when multitenancy is enabled.
5. Restart one application node and confirm jobs remain in the shared Hangfire
   storage; then test the same workflow under the intended production worker
   topology.

## Related guides

* [Timer and Scheduled Workflows](timer-and-scheduled-workflows.md)
* [Long-Running Workflows](long-running-workflows.md)
* [Clustering](../clustering/README.md)
* [Configuration Management](../deployment/configuration-management.md)
