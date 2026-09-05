---
description: Configure Quartz.NET as Elsa's persistent workflow scheduler.
---

# Quartz Scheduling

Use the Quartz integration when scheduled workflow work must survive an
application restart or be coordinated by multiple Elsa nodes. Quartz is a
replacement for Elsa's default workflow scheduler; it is not a replacement for
Elsa's workflow-definition or workflow-instance persistence.

This guide targets the Elsa `release/3.8.0` source line.

## Decide whether you need Quartz

`UseScheduling()` alone uses Elsa's default scheduler, which keeps scheduled
work in the application process. That is convenient for development and a
single-node experiment, but scheduled work is not durable across a process
restart.

Choose Quartz when you need one or more of the following:

| Requirement | Configuration |
| --- | --- |
| Local development with no scheduler database | `UseScheduling()` only |
| Scheduled work to survive restarts | `UseQuartzScheduler()` plus a persistent Quartz store |
| Multiple Elsa nodes sharing scheduled work | A persistent Quartz store with clustering enabled on every node |
| Workflow state to survive restarts | Configure Elsa workflow persistence separately |

Quartz coordinates scheduler jobs. Elsa still stores workflow instances,
bookmarks, definitions, and execution data through the persistence providers
configured for those Elsa stores.

## Install the packages

For a persistent Quartz job store, install the base scheduler package and
exactly one provider package:

```bash
dotnet add package Elsa.Scheduling.Quartz
dotnet add package Elsa.Scheduling.Quartz.EFCore.PostgreSql
```

The release also provides these EF Core provider packages:

| Quartz store | Package | Elsa helper |
| --- | --- | --- |
| PostgreSQL | `Elsa.Scheduling.Quartz.EFCore.PostgreSql` | `UsePostgreSql` |
| SQL Server | `Elsa.Scheduling.Quartz.EFCore.SqlServer` | `UseSqlServer` |
| MySQL | `Elsa.Scheduling.Quartz.EFCore.MySql` | `UseMySql` |
| SQLite | `Elsa.Scheduling.Quartz.EFCore.Sqlite` | `UseSqlite` |

Keep the Elsa packages on the same release line. The provider packages include
the Quartz EF Core migrations and the database-specific registration code.

## Configure a durable PostgreSQL scheduler

The smallest production-shaped setup has three separate concerns:

1. `UseWorkflowRuntime(...)` persists Elsa runtime state.
2. `UseScheduling(...UseQuartzScheduler())` selects Quartz as Elsa's workflow
   scheduler.
3. `UseQuartz(...UsePostgreSql(...))` configures Quartz's job store.

```csharp
using Elsa.Extensions;
using Elsa.Persistence.EFCore.Extensions;

var workflowConnectionString = builder.Configuration.GetConnectionString("Elsa")
    ?? throw new InvalidOperationException("Connection string 'Elsa' is missing.");

var quartzConnectionString = builder.Configuration.GetConnectionString("Quartz")
    ?? throw new InvalidOperationException("Connection string 'Quartz' is missing.");

builder.Services.AddElsa(elsa =>
{
    // Persist workflow instances, bookmarks, and runtime records in Elsa.
    elsa.UseWorkflowRuntime(runtime =>
        runtime.UseEntityFrameworkCore(ef => ef.UsePostgreSql(workflowConnectionString)));

    // Replace Elsa's in-process workflow scheduler with Quartz.
    elsa.UseScheduling(scheduling => scheduling.UseQuartzScheduler());

    // Persist Quartz jobs and triggers. Clustering defaults to true for PostgreSQL.
    elsa.UseQuartz(quartz =>
        quartz.UsePostgreSql(quartzConnectionString));
});
```

The `UsePostgreSql` helper registers the Quartz EF Core context factory,
configures Quartz's persistent store with JSON serialization, uses the
`quartz.qrtz_` table prefix, enables clustering by default, and registers the
provider migration initializer. Use the equivalent provider helper from the
table above for another database.

Do not add a second `AddQuartzHostedService` when using `UseQuartz(...)`. The
Elsa Quartz feature owns Quartz's hosted-service registration and lifecycle.

## Configure clustering deliberately

For a multi-node deployment, every node must use:

- the same Quartz database and table prefix;
- the same scheduler name; and
- a unique scheduler ID. Elsa defaults the ID to `AUTO` and the name to
  `ElsaScheduler`.

The SQL Server, PostgreSQL, and MySQL helpers enable clustering by default. You
can make the choice explicit:

```csharp
elsa.UseQuartz(quartz =>
    quartz.UsePostgreSql(
        quartzConnectionString,
        useClustering: true));
```

SQLite defaults to clustering disabled because a SQLite file is not a suitable
shared multi-node Quartz store:

```csharp
elsa.UseQuartz(quartz =>
    quartz.UseSqlite(quartzConnectionString,
        useClustering: false));
```

If an external system needs stable names, configure the scheduler identity
explicitly. The identity alone does not enable clustering; the persistent store
and its clustering setting are also required.

```csharp
elsa.UseQuartz(quartz => quartz
    .ConfigureClusteringIdentity("orders-node", "OrdersScheduler")
    .UsePostgreSql(quartzConnectionString, useClustering: true));
```

Quartz clustering requires synchronized clocks. Use a time-synchronization
service on all nodes and follow Quartz's [clustering guidance](https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/advanced-enterprise-features.html).

## What Quartz schedules

Elsa's scheduling feature classifies the work and delegates it through
`IWorkflowScheduler`. With Quartz selected, Elsa stores these as Quartz
triggers:

| Elsa work | Quartz job | Result |
| --- | --- | --- |
| `StartAt` trigger | `RunWorkflowJob` | Starts a new workflow instance once |
| `Timer` or `Cron` trigger | `RunWorkflowJob` | Starts new instances on a recurring schedule |
| `Delay`, `Timer`, or `StartAt` bookmark | `ResumeWorkflowJob` | Resumes an existing workflow instance once |
| `Cron` bookmark | `ResumeWorkflowJob` | Resumes an existing instance on each cron occurrence |

The job data carries the workflow definition or instance identifiers, bookmark
information, inputs, properties, and tenant ID. The job handler restores the
tenant context before starting or resuming the workflow.

This means Quartz persistence and Elsa persistence work together:

```text
Elsa workflow instance + bookmark state  ->  Elsa persistence provider
Scheduled resume/start job + trigger     ->  Quartz persistent job store
Quartz job execution                     ->  Elsa starts or resumes the workflow
```

Persisting a Quartz trigger does not make an Elsa workflow instance durable if
the Elsa runtime store is still in memory. Configure both sides for restart
survival.

## Cron expressions and schedule changes

Quartz's cron parser uses six or seven fields, including seconds. For example:

```text
0 0 9 * * MON-FRI   # 09:00:00 on weekdays
0 */15 * * * *      # every 15 minutes
```

The Quartz scheduler is the cron parser used by Elsa after
`UseQuartzScheduler()`. Validate expressions against the [Quartz 3.x cron
format](https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontriggers.html),
including its seconds field.

When Elsa reschedules a task with the same task name, Quartz uses the same
trigger identity. Unschedule the existing task before scheduling a replacement
if the caller needs to change an existing trigger; the released scheduler
deliberately treats an already-existing trigger as an expected concurrent race
and leaves it in place.

## Configure transient retry behavior

Quartz's Elsa jobs reschedule themselves after an exception that Elsa's
transient-exception detector classifies as transient. The default delay is ten
seconds. Configure it through the scheduling feature when that default does not
match the deployment:

```csharp
elsa.UseScheduling(scheduling =>
    scheduling.UseQuartzScheduler(quartz =>
        quartz.ConfigureOptions(options =>
        {
            options.TransientExceptionRetryDelay = TimeSpan.FromSeconds(30);
        })));
```

Non-transient failures are logged and the affected Quartz job is removed. Treat
this as scheduler failure handling, not as a substitute for an Elsa incident
strategy or idempotency in activities that perform external side effects.

## Startup, migrations, and shutdown

The EF Core provider helpers register migrations for the Quartz tables. The
provider migration initializer runs before the scheduler starts. In a
production deployment, make sure the Quartz database credentials can create or
update the required schema, or apply the provider migrations through your
normal controlled migration process before starting the application.

The Quartz feature waits for running jobs to complete during shutdown by
default. This gives a rolling deployment a chance to drain active scheduler
work; still test shutdown behavior with the execution time and cancellation
policy of your workflows.

## Studio and designer boundary

Process designers can use `Timer`, `Cron`, `StartAt`, and `Delay` in Elsa Studio
without knowing which scheduler the server uses. The server's module
configuration decides whether those activities are executed by Elsa's local
scheduler or Quartz.

Quartz does not add a separate Studio scheduler. After publishing a workflow,
verify the server-side scheduler and persistence configuration when a timer or
cron workflow does not fire.

## Verify a deployment

Use this sequence before relying on scheduled work in production:

1. Confirm that the Quartz provider package and the matching Elsa persistence
   provider are installed.
2. Confirm that `UseScheduling(...UseQuartzScheduler())` and `UseQuartz(...)`
   are both registered.
3. Start the host and verify that the Quartz schema exists in the configured
   database. PostgreSQL and SQL Server use the provider's Quartz table prefix;
   do not assume an unqualified `qrtz_` prefix.
4. Publish a workflow with a short `Delay` or a `Timer` trigger and confirm the
   workflow resumes or starts.
5. In a multi-node deployment, start all nodes against the same Quartz store,
   then verify the scheduler state and that one node executes each scheduled
   firing.
6. Stop and restart a node with a future job, then confirm the trigger remains
   present and the workflow resumes under the intended cluster topology.

## Troubleshooting

### The workflow state survives, but the scheduled resume is lost

Check whether only Elsa persistence was configured. A durable workflow instance
does not imply a durable scheduler. Configure a persistent Quartz provider and
make sure the Quartz tables are available to the host.

### Multiple nodes execute scheduled work unexpectedly

Check that every node uses the same Quartz database, provider table prefix, and
scheduler name, and that clustering is enabled. For PostgreSQL, SQL Server, and
MySQL, inspect the `useClustering` argument rather than assuming a later
configuration change preserved the default. Do not use SQLite as a shared
multi-node Quartz store.

### A cron expression is rejected

Check the six-field Quartz format, including seconds. Also check that the
workflow is using the Quartz scheduler if the expression was authored for
Quartz rather than another scheduler's dialect.

### The host fails during startup

Inspect database connectivity and migration permissions first. The provider
initializes the Quartz EF Core schema before the scheduler starts, so a missing
database, invalid connection string, or migration failure can prevent the host
from becoming ready.

## Related guides

* [Timer and Scheduled Workflows](timer-and-scheduled-workflows.md)
* [Long-Running Workflows](long-running-workflows.md)
* [Hangfire Integration](hangfire-integration.md)
* [Clustering](../clustering/README.md)
* [Persistence](../persistence/README.md)

## Release-source references

* [Core `UseScheduling`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Scheduling/Extensions/ModuleExtensions.cs) and [default scheduler](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Scheduling/Services/DefaultWorkflowScheduler.cs)
* [Quartz module extensions](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/scheduling/Elsa.Scheduling.Quartz/Extensions/ModuleExtensions.cs)
* [Quartz feature and scheduler identity](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/scheduling/Elsa.Scheduling.Quartz/Features/QuartzFeature.cs)
* [Quartz workflow scheduler](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/scheduling/Elsa.Scheduling.Quartz/Services/QuartzWorkflowScheduler.cs)
* [PostgreSQL Quartz provider](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/scheduling/Elsa.Scheduling.Quartz.EFCore.PostgreSql/PostgreSqlQuartzExtensions.cs)
* [SQL Server Quartz provider](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/scheduling/Elsa.Scheduling.Quartz.EFCore.SqlServer/SqlServerQuartzExtensions.cs)
* [MySQL Quartz provider](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/scheduling/Elsa.Scheduling.Quartz.EFCore.MySql/MySqlQuartzExtensions.cs)
* [SQLite Quartz provider](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/scheduling/Elsa.Scheduling.Quartz.EFCore.Sqlite/SqliteQuartzExtensions.cs)
* [Quartz scheduler feature](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/scheduling/Elsa.Scheduling.Quartz/Features/QuartzSchedulerFeature.cs)
