# Bulk Dispatch Workflows Activity

Use `BulkDispatchWorkflows` when one parent workflow should fan out work to
many child workflow instances in one step.

In `release/3.8.0`, the activity lives in
`Elsa.Workflows.Runtime.Activities.BulkDispatchWorkflows` and is exposed in the
`Composition` category in Elsa Studio.

## When to use it

Use `BulkDispatchWorkflows` when you need to:

- dispatch the same child workflow for each item in a collection
- optionally wait for all child workflows to finish
- react differently when individual child workflows finish or fault
- assign per-item correlation IDs

Choose the surrounding pattern based on where the fan-out should happen:

| Use this | When |
| --- | --- |
| [Dispatch Workflow Activity](dispatch-workflow-activity.md) | You only need one child workflow instance. |
| `BulkDispatchWorkflows` | You need one child workflow instance per item in a collection. |
| `ForEach` | You want to iterate inside the same workflow instance instead of creating child workflow instances. |
| `POST /workflow-definitions/{definitionId}/bulk-dispatch` | An external client needs to queue multiple instances of the same workflow definition. |

## What it does

At execution time, Elsa:

1. evaluates `Items` into a materialized list
2. resolves the published child workflow definition from `WorkflowDefinitionId`
3. creates a new child workflow instance per item
4. sets `ParentWorkflowInstanceId` on each dispatch request so the runtime can
   track the parent-child relationship
5. adds `ParentInstanceId` to the child input and dispatch properties
6. merges the current item into the child workflow input
7. dispatches the child workflow through the selected channel
8. either completes immediately or waits for child completion, depending on
   `WaitForCompletion`

If the target workflow definition does not have a published version, the
activity faults.

If `Items` is empty, the activity completes immediately even when
`WaitForCompletion` is `true`.

## Input mapping

Each dispatched child workflow starts with the optional `Input` dictionary and
then receives per-item input:

- if an item is a plain value, Elsa sends it under `DefaultItemInputKey`
  (default: `Item`)
- if an item is already an `IDictionary<string, object>`, Elsa merges that
  dictionary directly into the child input instead

The activity also adds `ParentInstanceId` to the child workflow input before
merging item dictionaries. If your item dictionaries use the same key, they
overwrite that input value. The same overwrite behavior applies to any matching
keys that were already present in the optional `Input` dictionary.

## Waiting vs fire-and-forget

`WaitForCompletion` defaults to `true`.

| Setting | Runtime behavior |
| --- | --- |
| `true` | Creates a bookmark and waits until all dispatched child workflows finish |
| `false` | Dispatches child workflows and completes the parent activity immediately |

When Elsa waits for completion, it tracks how many child instances were
dispatched and resumes the parent activity whenever a finished child workflow
reports back through `ResumeBulkDispatchWorkflowActivity`.

## Outcomes and child ports

`BulkDispatchWorkflows` exposes these flowchart outcomes in its activity
metadata:

- `Done`
- `Completed`
- `Canceled`

In `release/3.8.0`, the implementation completes with:

- `Done` immediately when `WaitForCompletion` is `false`
- `Done` immediately when `Items` is empty
- `Completed` and `Done` after the last child finishes when
  `WaitForCompletion` is `true`

You can also attach per-child ports:

- `ChildCompleted` runs for each child workflow whose sub-status is `Finished`
- `ChildFaulted` runs for each child workflow whose sub-status is `Faulted`

While those child-port activities run, Elsa adds a `ChildInstanceId` workflow
variable and passes these values as workflow input:

- `WorkflowOutput`
- `WorkflowInstanceId`
- `WorkflowStatus`
- `WorkflowSubStatus`

If `WaitForCompletion` is `false`, Elsa never schedules these ports because the
parent workflow does not wait for child completion events.

## Using code

### Wait for all child workflows

This example dispatches one child workflow per employee and waits for all of
them to complete.

{% code title="GreetEmployeesWorkflow.cs" %}

```csharp
using System.Collections.Generic;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Runtime.Activities;

public class GreetEmployeesWorkflow : WorkflowBase
{
    public const string DefinitionId = "greet-employees";

    protected override void Build(IWorkflowBuilder builder)
    {
        builder.WithDefinitionId(DefinitionId);

        var employees = new[]
        {
            new Dictionary<string, object> { ["Employee"] = "Alice" },
            new Dictionary<string, object> { ["Employee"] = "Bob" },
            new Dictionary<string, object> { ["Employee"] = "Charlie" }
        };

        builder.Root = new Sequence
        {
            Activities =
            {
                new BulkDispatchWorkflows
                {
                    WorkflowDefinitionId = new(EmployeeGreetingWorkflow.DefinitionId),
                    Items = new(employees),
                    WaitForCompletion = new(true)
                },
                new WriteLine("All employee greeting workflows finished.")
            }
        };
    }
}
```

{% endcode %}

{% code title="EmployeeGreetingWorkflow.cs" %}

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;

public class EmployeeGreetingWorkflow : WorkflowBase
{
    public const string DefinitionId = "employee-greeting";

    protected override void Build(IWorkflowBuilder builder)
    {
        builder.WithDefinitionId(DefinitionId);
        var employee = builder.WithInput<string>("Employee");

        builder.Root = new WriteLine(context => $"Hello {context.GetInput<string>(employee)}");
    }
}
```

{% endcode %}

Because each item is a dictionary, the child workflow receives `Employee`
directly instead of an `Item` wrapper key.

### Fire-and-forget batch dispatch

Set `WaitForCompletion` to `false` when the parent workflow should continue
without waiting for the children:

```csharp
new BulkDispatchWorkflows
{
    WorkflowDefinitionId = new("SlowBulkChildWorkflow"),
    Items = new(new object[] { "A", "B", "C" }),
    WaitForCompletion = new(false)
}
```

This still records `ParentWorkflowInstanceId` on the dispatched workflow
request, but the parent activity does not create a waiting bookmark.

### Per-item correlation IDs

`CorrelationIdFunction` is evaluated once per item. The current item is exposed
to the expression evaluator arguments used by the activity.

```csharp
using Elsa.Expressions.JavaScript.Models;

new BulkDispatchWorkflows
{
    WorkflowDefinitionId = new("BulkChildWorkflow"),
    Items = new(new object[] { 1, 2, 3 }),
    CorrelationIdFunction = new(JavaScriptExpression.Create("`correlation-${getItem()}`")),
    WaitForCompletion = new(true)
}
```

## Elsa Studio notes

In Elsa Studio, look for `Bulk Dispatch Workflows` under the `Composition`
activity category.

The main properties to configure are:

- `Workflow Definition`
- `Items`
- `Default Item Input Key`
- `Correlation ID Function`
- `Input`
- `Wait For Completion`
- `Channel`
- `Start New Trace`

Leaving `Channel` empty uses the default dispatcher channel.

Use `ChildCompleted` and `ChildFaulted` ports when the parent workflow needs
per-child follow-up logic.

## Activity vs REST bulk dispatch

Elsa Server also exposes `POST /workflow-definitions/{definitionId}/bulk-dispatch`.

That endpoint is useful when an external caller wants to start the same workflow
`Count` times with the same input payload.

`BulkDispatchWorkflows` is different:

- it runs inside a parent workflow
- it can send different input per item
- it can wait for children and react to child completion or faults
- it can dispatch to a configured workflow channel

## Related guides

- [Dispatch Workflow Activity](dispatch-workflow-activity.md)
- [Running Workflows](README.md)
- [Timer and Scheduled Workflows](timer-and-scheduled-workflows.md)
