# Alteration Plans

An alteration plan represents a collection of alterations that Elsa applies asynchronously to workflow instances selected by a filter.

Use alteration plans when you want Elsa to:

* select workflow instances for you
* create one alteration job per matching instance
* process the work in the background
* let you inspect plan status and per-instance job status separately

In `release/3.8.0`, `IAlterationPlanScheduler` accepts `AlterationPlanParams`, which contains:

* `alterations`: the changes to apply
* `filter`: an `AlterationWorkflowInstanceFilter` describing which workflow instances to target
* `id`: an optional plan ID; Elsa generates one when omitted

## What happens when you submit a plan

Submitting a plan does not run the alterations inline with the request. Elsa instead:

1. stores or generates the plan ID
2. dispatches the built-in `Elsa.Alterations.ExecuteAlterationPlan` system workflow
3. stores the plan
4. finds matching workflow instances from the filter
5. creates one alteration job per matching instance
6. dispatches those jobs through the configured alteration job dispatcher

This is why alteration plans are the right fit for bulk operational work, scheduled worker processing, and cases where you need job-level inspection later.

### Creating Alteration Plans <a href="#creating-alteration-plans" id="creating-alteration-plans"></a>

To create an alteration plan in code, create an `AlterationPlanParams` instance:

```csharp
var plan = new AlterationPlanParams
{
    Alterations = new List<IAlteration>
    {
        new ModifyVariable
        {
            VariableId = "83fde420b5794bc39a0a7db725405511",
            Value = "MyValue"
        }
    },
    Filter = new AlterationWorkflowInstanceFilter
    {
        WorkflowInstanceIds = new[] { "26cf02e60d4a4be7b99a8588b7ac3bb9" }
    }
};
```

The workflow-instance filter supports more than explicit instance IDs. In `release/3.8.0`, you can also filter by correlation IDs, names, search term, definition IDs, definition version IDs, statuses, sub-statuses, incidents, system-workflow flag, activity filters, and timestamp filters.

### Filter guidance

Use explicit `workflowInstanceIds` when you already know the exact targets but still want background execution and plan tracking.

Use broader filters when you are operating on a live slice of runtime state, for example:

* all running instances of a specific workflow definition
* instances with incidents
* instances waiting in a specific sub-status
* instances created, updated, or finished in a specific time range

Use `POST /alterations/dry-run` before submitting a broad filter so you can confirm which instance IDs Elsa would target.

### Submitting Alteration Plans <a href="#submitting-alteration-plans" id="submitting-alteration-plans"></a>

To submit an alteration plan, use the `IAlterationPlanScheduler` service. For example:

```csharp
var scheduler = serviceProvider.GetRequiredService<IAlterationPlanScheduler>();
var planId = await scheduler.SubmitAsync(plan, cancellationToken);
```

When a plan is submitted, Elsa dispatches the built-in `Elsa.Alterations.ExecuteAlterationPlan` system workflow. That workflow stores the plan, finds matching workflow instances from the filter, creates one **alteration job** per matching instance, and dispatches those jobs through the configured alteration job dispatcher.

Alteration plans are executed asynchronously in the background. To monitor the execution of an alteration plan, use the `IAlterationPlanStore` service. For example:

```csharp
var store = serviceProvider.GetRequiredService<IAlterationPlanStore>();
var plan = await store.FindAsync(new AlterationPlanFilter { Id = planId }, cancellationToken);
```

To get the alteration jobs that were created as part of the plan, use the `IAlterationJobStore` service. For example:

```csharp
var store = serviceProvider.GetRequiredService<IAlterationJobStore>();
var jobs = (await store.FindManyAsync(new AlterationJobFilter { PlanId = planId }, cancellationToken)).ToList();
```

If the filter matches no workflow instances, Elsa still stores the plan, but it does not create jobs.

## Plan status and job status

The plan and job records are separate on purpose:

* the plan tells you whether Elsa accepted and processed the bulk request
* the jobs tell you what happened for each targeted workflow instance

Use plan timestamps plus per-job logs to answer operational questions such as:

* did Elsa find any matching instances
* which instances failed
* which alteration inside the plan failed for a specific instance
* whether a plan has finished creating and dispatching its jobs

## Elsa Studio workflow

In Studio, the alterations designer submits the same `AlterationPlanParams` payload used by the server API, but in `release/3.8.0` it builds that payload from the currently selected workflow instance instead of exposing the full filter authoring surface.

After submission, Studio navigates to a plan details page that shows:

* the stored plan payload
* current plan status
* generated jobs
* per-job log entries

Use Studio when operators want to alter a known running instance and then inspect the resulting plan and jobs without scripting the REST API directly.
