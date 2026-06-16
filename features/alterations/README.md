# Alterations

An [alteration](../../getting-started/concepts/#alteration) represents a change that can be applied to a given [workflow instance](../../getting-started/concepts/#workflow-instance).

Using alterations, you can change the state of a running workflow instance without republishing the workflow definition. In `release/3.8.0`, Elsa exposes alterations through server APIs and Elsa Studio.

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

## Elsa Studio

Elsa Studio in `release/3.8.0` includes an alterations module. When the backend Alterations feature is enabled, Studio shows an **Alterable instances** page and adds **Alter** actions for running workflow instances.

Studio currently focuses on altering individual running instances. For bulk operations across many instances, use alteration plans and their filter-based API.

## Persistence and dispatch options

By default, Alterations uses in-memory stores and an in-memory background dispatcher. For durable or multi-node deployments, configure persistence for alteration plans and jobs:

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
