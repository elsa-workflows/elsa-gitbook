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
