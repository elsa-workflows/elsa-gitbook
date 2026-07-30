---
description: >-
  Choose the right Elsa 3.8.0 alteration path, apply changes safely, and
  monitor the resulting workflow and alteration jobs.
---

# Alter a running workflow instance

Alterations change the state of an existing workflow instance without changing
the workflow definition for every instance. They are useful for operational
corrections, controlled recovery, and migrating a running instance to another
version of the same definition.

This guide describes the behavior of Elsa `release/3.8.0`. For the individual
request and extension contracts, see the [Alterations feature reference](../../features/alterations/README.md).

## Choose an execution path

Start with the target-selection and auditability requirements:

- **Known instance IDs:** use `POST /alterations/run` or
  `IAlterationRunner`. You get one result per requested instance; the HTTP
  endpoint also resumes successful instances that have scheduled work.
- **Filtered or auditable changes:** use `POST /alterations/dry-run`, then
  `POST /alterations/submit`. Elsa creates an asynchronous plan and one job
  for each matched instance.
- **One visible instance in Studio:** use **Alterations → Instances**. Studio
  stages a plan for the selected instance and provides plan/job inspection.
- **Retrying faulted work:** use `GET` or
  `POST /alterations/workflows/retry`. Elsa creates `ScheduleActivity`
  alterations and dispatches the instance.

Use a plan when the target set is defined by runtime state, such as a
workflow definition, incident state, activity, or time range. Use immediate
execution when the target set is already known and the caller needs the
alteration log synchronously.

## Enable the feature

Register the Core Alterations module on the Elsa Server:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseAlterations();
});
```

The module registers the five built-in alteration types:

- `ModifyVariable` changes an existing variable by variable ID.
- `ScheduleActivity` schedules an activity by activity ID or activity-instance ID.
- `CancelActivity` cancels one or more running activity instances.
- `Cancel` cancels the workflow instance.
- `Migrate` loads a specific version of the same workflow definition.

The server endpoints require `run:alterations` for writes and dry runs, and
`read:alterations` to retrieve a stored plan and its jobs. The Studio module
also needs to be enabled and connected to the server's Alterations feature.

## Apply a known change immediately

Use `POST /alterations/run` when you already have the workflow instance IDs.
The request applies the alterations to each requested instance and returns a
`RunAlterationsResult` containing the instance ID, log, success flag, and
whether the updated workflow has scheduled work.

For example, the following changes a variable and schedules an activity:

```http
POST /alterations/run HTTP/1.1
Host: localhost:5001
Content-Type: application/json

{
  "workflowInstanceIds": ["<workflow-instance-id>"],
  "alterations": [
    {
      "type": "ModifyVariable",
      "variableId": "<variable-id>",
      "value": "approved"
    },
    {
      "type": "ScheduleActivity",
      "activityId": "ReviewOrder"
    }
  ]
}
```

The HTTP endpoint dispatches successful results that contain scheduled work.
If you call `IAlterationRunner` directly, it only applies and commits the
state. Call `IAlteredWorkflowDispatcher` yourself when the result has
scheduled work:

```csharp
var results = await runner.RunAsync(
    workflowInstanceIds,
    alterations,
    cancellationToken);

await alteredWorkflowDispatcher.DispatchAsync(results, cancellationToken);
```

An alteration can fail for an individual instance—for example, when a
variable, activity, or target workflow version does not exist. Inspect the
result log before treating the operation as complete.

## Use a plan for filtered or auditable changes

Plans are asynchronous. Submission dispatches Elsa's system workflow
`Elsa.Alterations.ExecuteAlterationPlan`, which stores the plan, finds matching
instances, creates one alteration job per match, and dispatches those jobs.
The plan and jobs have separate statuses and logs, so you can distinguish
"the plan found no targets" from "one targeted instance failed".

For a broad filter, first run a dry run:

```http
POST /alterations/dry-run HTTP/1.1
Host: localhost:5001
Content-Type: application/json

{
  "definitionIds": ["order-processing"],
  "statuses": ["Running"],
  "hasIncidents": true,
  "isSystem": false
}
```

The response contains the workflow instance IDs that the filter would select.
If the result is correct, submit the same filter with the alterations:

```http
POST /alterations/submit HTTP/1.1
Host: localhost:5001
Content-Type: application/json

{
  "alterations": [
    {
      "type": "ScheduleActivity",
      "activityId": "ReviewOrder"
    }
  ],
  "filter": {
    "workflowInstanceIds": ["<confirmed-instance-id>"]
  }
}
```

The submission response contains the plan ID. Retrieve its current plan and
jobs with:

```http
GET /alterations/<plan-id> HTTP/1.1
Host: localhost:5001
```

An empty match is still a valid plan: Elsa stores the plan, creates no jobs,
and completes the plan through the no-job path. Use `read:alterations` to
inspect the plan, job status, timestamps, and per-job log entries.

## Use the Studio workflow

With the server and Studio Alterations modules enabled:

1. Open **Alterations → Instances**. Studio lists running, non-system workflow
   instances and provides an **Alter** action for each one.
2. Stage one or more of the five built-in alterations for that instance.
3. Submit the staged plan.
4. Open **Alterations → Plans** and select the plan to inspect its status,
   generated jobs, target instance links, and log entries.

Studio's staging flow is intentionally instance-oriented. It does not replace
the filter authoring and dry-run workflow for bulk operations; use the server
API for those operations and Studio's Plans view for follow-up inspection.

## Configure durability and background execution

`UseAlterations()` uses in-memory plan and job stores and an in-memory job
dispatcher unless you replace them. These defaults are suitable for local
development, but plan and job records and queued jobs are not durable across a
process restart.

For durable plan and job records, configure an available persistence provider
inside `UseAlterations`:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseAlterations(alterations =>
    {
        alterations.UseEntityFrameworkCore(ef =>
            ef.UseSqlServer(connectionString));
    });
});
```

The `elsa-extensions` repository also provides MongoDB persistence and a
MassTransit alteration-job dispatcher:

```csharp
elsa.UseAlterations(alterations =>
{
    alterations.UseMongoDb();
    alterations.UseMassTransitDispatcher();
});
```

The MassTransit dispatcher changes how generated alteration jobs are sent to a
consumer. It does not change the synchronous `/alterations/run` path. In a
multi-node deployment, install the same alteration handlers and workflow
definition versions on every node that can load or resume the affected
instances, and use durable stores and broker topology appropriate for the
failure model.

## Retry faulted activities

Use the retry endpoint when the intent is specifically to retry faulted work.
If `activityIds` is omitted, Elsa reads the incident activity IDs from each
target workflow instance. If it is supplied, Elsa schedules only those
activity IDs.

```http
POST /alterations/workflows/retry HTTP/1.1
Host: localhost:5001
Content-Type: application/json

{
  "workflowInstanceIds": ["<workflow-instance-id>"],
  "activityIds": ["CapturePayment"]
}
```

In `release/3.8.0`, the endpoint accepts both `GET` and `POST`. Send one
workflow instance ID per request: the released handler loops over the loaded
instances but passes the full request ID collection to the alteration runner,
which can repeat work and result entries when several IDs are batched.

## Add a custom alteration

When the change cannot be expressed with the built-in types, implement
`IAlteration` and an `IAlterationHandler` (or derive from
`AlterationHandlerBase<T>`), then register the pair with
`AddAlteration<T, THandler>()`. The handler runs against the existing workflow
execution context and must explicitly succeed or fail the operation. See
[Alteration extensibility](../../features/alterations/applying-alterations/extensibility.md)
for the implementation contract.

## Operational checklist

- Confirm the target IDs or dry-run filter results before changing state.
- Use `run:alterations` and `read:alterations` as separate least-privilege
  capabilities.
- Test a representative instance before submitting a broad plan.
- Keep variable IDs, activity IDs, and workflow definition versions stable
  enough for the persisted instances being operated on.
- Use durable plan/job storage and a durable job dispatcher when a restart or
  node failure must not lose alteration work.
- Review plan status, job status, and log entries; a submitted plan is not the
  same thing as a successful alteration on every target.

## Release source

This page was checked against the following `release/3.8.0` implementations:

- [Core Alterations feature](https://github.com/elsa-workflows/elsa-core/blob/edb5f7cd51e1c24a6ccbbe215684661e3d6c1e33/src/modules/Elsa.Alterations/Features/AlterationsFeature.cs)
- [Core immediate execution endpoint](https://github.com/elsa-workflows/elsa-core/blob/edb5f7cd51e1c24a6ccbbe215684661e3d6c1e33/src/modules/Elsa.Alterations/Endpoints/Alterations/Run/Endpoint.cs)
- [Core alteration runner](https://github.com/elsa-workflows/elsa-core/blob/edb5f7cd51e1c24a6ccbbe215684661e3d6c1e33/src/modules/Elsa.Alterations/Services/DefaultAlterationRunner.cs)
- [Core alteration-plan scheduler](https://github.com/elsa-workflows/elsa-core/blob/edb5f7cd51e1c24a6ccbbe215684661e3d6c1e33/src/modules/Elsa.Alterations/Services/DefaultAlterationPlanScheduler.cs)
- [Core alteration-plan workflow](https://github.com/elsa-workflows/elsa-core/blob/edb5f7cd51e1c24a6ccbbe215684661e3d6c1e33/src/modules/Elsa.Alterations/Workflows/ExecuteAlterationPlanWorkflow.cs)
- [Core retry endpoint](https://github.com/elsa-workflows/elsa-core/blob/edb5f7cd51e1c24a6ccbbe215684661e3d6c1e33/src/modules/Elsa.Alterations/Endpoints/Workflows/Retry/Endpoint.cs)
- [Core alteration filter](https://github.com/elsa-workflows/elsa-core/blob/edb5f7cd51e1c24a6ccbbe215684661e3d6c1e33/src/modules/Elsa.Alterations.Core/Models/AlterationWorkflowInstanceFilter.cs)
- [Studio Alterations menu](https://github.com/elsa-workflows/elsa-studio/blob/ef6a39d103d1a76e9f33fd4a37f499c5f02a4bfa/src/modules/Elsa.Studio.Alterations/Menu/AlterationsMenu.cs)
- [Studio instance list](https://github.com/elsa-workflows/elsa-studio/blob/ef6a39d103d1a76e9f33fd4a37f499c5f02a4bfa/src/modules/Elsa.Studio.Alterations/Pages/Instances/Index.razor)
- [Studio plan details](https://github.com/elsa-workflows/elsa-studio/blob/ef6a39d103d1a76e9f33fd4a37f499c5f02a4bfa/src/modules/Elsa.Studio.Alterations/Pages/Plans/Details.razor)
- [Extensions MongoDB alteration persistence](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/persistence/Elsa.Persistence.MongoDb/Modules/Alterations/Extensions.cs)
- [Extensions MassTransit alteration dispatcher](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/alterations/Elsa.Alterations.MassTransit/Extensions/ModuleExtensions.cs)
