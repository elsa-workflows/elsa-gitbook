# Alterations

An [alteration](../../getting-started/concepts/#alteration) represents a change that can be applied to a given [workflow instance](../../getting-started/concepts/#workflow-instance).

Using alterations, you can change the state of a running workflow instance without republishing the workflow definition. In `release/3.8.0`, Elsa exposes alterations through server APIs and Elsa Studio.

## What Elsa ships in `release/3.8.0`

Enabling `UseAlterations()` adds:

* the built-in `Elsa.Alterations.ExecuteAlterationPlan` system workflow used to execute submitted plans
* REST endpoints under `/alterations`
* in-memory alteration plan and job stores by default
* an in-memory background dispatcher for alteration jobs by default

All alteration APIs shown on this page require the `run:alterations` permission.

## When to use alterations

Use alterations when you need to correct or steer existing workflow instances. Typical examples include:

* updating a variable on a live instance
* scheduling an activity so a stalled workflow can continue
* canceling a workflow or a specific running activity
* migrating a running instance to a newer published workflow version

## Alteration Types <a href="#alteration-types" id="alteration-types"></a>

Elsa Workflows supports the following alteration types:

* **Cancel**: Cancels the entire workflow instance.
* **CancelActivity**: Cancels a specific running activity instance, or all running instances of an activity ID.
* **ScheduleActivity**: Schedules an activity for execution by activity ID or activity instance ID.
* **ModifyVariable**: Modifies a workflow variable by variable ID.
* **Migrate**: Migrates a workflow instance to a newer published version of the same workflow definition.

## Two execution modes

Elsa supports two ways to execute alterations:

1. Submit an **alteration plan** when you want Elsa to select target instances from a filter and process them asynchronously in the background.
2. **Apply alterations immediately** when you already know the workflow instance IDs and want the results in the current request.

Use these pages for each mode:

* [Alteration Plans](alteration-plans/README.md)
* [Applying Alterations](applying-alterations/README.md)

### Which mode fits which job

| Situation | Use |
| --- | --- |
| You already know the exact workflow instance IDs and need the response now | `POST /alterations/run` or `IAlterationRunner` |
| You need Elsa to find matching workflow instances from filters and process them in the background | alteration plans |
| You need to retry faulted activities across one or more instances | `POST /alterations/workflows/retry` |
| You want designers or operators to stage a bulk plan visually in Studio | Elsa Studio alterations module |

## Elsa Studio

Elsa Studio in `release/3.8.0` includes an alterations module. When the backend Alterations feature is enabled, Studio shows an **Alterable instances** page and adds **Alter** actions for running workflow instances.

Studio exposes:

* an **Alterations** top-level menu with plan and instance views
* an **Alterable instances** page that lists non-system running workflow instances
* an alteration designer for staging the five built-in alteration types against an instance
* plan details pages that show plan status, generated jobs, and per-job logs
* quick **Alter** actions from workflow-instance screens

Studio currently focuses on staging or inspecting plans around individual running instances. For cross-instance bulk operations, use alteration plans and their filter-based API.

## Persistence and dispatch options

By default, Alterations uses in-memory stores and an in-memory background dispatcher. That is fine for local development, but plans and jobs are not durable across process restarts.

For durable or multi-node deployments, configure persistence for alteration plans and jobs:

```csharp
services.AddElsa(elsa => elsa.UseAlterations(alterations =>
{
    alterations.UseEntityFrameworkCore(ef => ef.UseSqlServer(connectionString));
}));
```

MongoDB persistence is available from `elsa-extensions`:

```csharp
services.AddElsa(elsa => elsa.UseAlterations(alterations =>
{
    alterations.UseMongoDb();
}));
```

`elsa-extensions` also provides a MassTransit-backed dispatcher for background alteration jobs:

```csharp
services.AddElsa(elsa => elsa.UseAlterations(alterations =>
{
    alterations.UseMassTransitDispatcher();
}));
```

Use the MassTransit dispatcher when alteration jobs should survive node boundaries or be processed by worker nodes connected through MassTransit. The dispatcher replaces the default in-memory background queue for alteration jobs only; it does not change how the immediate `/alterations/run` endpoint executes.

## Operational notes

* Submitted plans create one alteration job per matched workflow instance.
* If a submitted plan matches no instances, Elsa still stores the plan but generates no jobs.
* `/alterations/run` dispatches successful workflow instances that still have scheduled work after the alterations finish.
* `IAlterationRunner` by itself does not dispatch scheduled work; pair it with `IAlteredWorkflowDispatcher` when you call the service directly.
* The built-in bulk retry endpoint schedules faulted activities by creating `ScheduleActivity` alterations and then dispatching the affected instances.
