# Bulk Dispatch Workflows Activity

Use `BulkDispatchWorkflows` when a workflow needs to create and dispatch one child workflow instance per item in a collection.

This activity is available in `release/3.8.0` under the **Composition** category as **Bulk Dispatch Workflows**.

## When to use it

Use `BulkDispatchWorkflows` when you want the parent workflow to fan out work across many child workflow instances:

| Use this | When |
| --- | --- |
| `DispatchWorkflow` | You need to start one child workflow instance. |
| `BulkDispatchWorkflows` | You need to start one child workflow instance per item in a collection. |
| `ForEach` | You want to iterate inside the same workflow instance instead of creating child workflow instances. |
| `POST /workflow-definitions/{definitionId}/bulk-dispatch` | An external client, not a parent workflow, needs to queue multiple instances of the same workflow definition. |

## What it does

For each item in `Items`, Elsa:

1. Resolves the published version of the child workflow definition.
2. Creates a new child workflow instance.
3. Adds `ParentInstanceId` to the child input and workflow properties.
4. Merges the current item into the child input.
5. Dispatches the child workflow through the selected channel.

If `WaitForCompletion` is `true`, the parent workflow creates a bookmark and resumes only after all dispatched child workflows finish. If `Items` is empty, the activity completes immediately.

## Input mapping

`BulkDispatchWorkflows` supports two item shapes:

- If each item is a dictionary, Elsa merges that dictionary directly into the child workflow input.
- If each item is any other value, Elsa stores that value under `DefaultItemInputKey`, which defaults to `Item`.

Elsa also merges any values from the activity's `Input` property into every child workflow input.

This means each child workflow receives:

- the shared `Input` values
- the current item data
- `ParentInstanceId`

## Code example

The following example is grounded in the `release/3.8.0` component tests. The parent workflow dispatches one child workflow for each employee record and waits for all of them to finish.

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
                new WriteLine("All greetings completed")
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

When the child workflow expects a single simple value instead of a dictionary, keep the default `DefaultItemInputKey = "Item"` and read that input from the child workflow.

## Correlation IDs per item

`CorrelationIdFunction` is evaluated once per item. In the `release/3.8.0` tests, Elsa computes correlation IDs with a JavaScript expression:

```javascript
`correlation-${getItem()}`
```

Use this when each dispatched child workflow should get its own predictable correlation ID based on the current item.

## Waiting vs fire-and-forget

`WaitForCompletion` defaults to `true`.

- `true`: the parent workflow pauses until every child workflow finishes.
- `false`: the parent workflow completes this activity immediately after dispatching the child workflows.

When Elsa waits for completion, each child workflow gets a `WaitForCompletion` marker in its properties so the runtime can resume the parent workflow when that child finishes.

## Child completion and fault ports

When `WaitForCompletion` is `true`, the activity can schedule extra work for each child result:

- `ChildCompleted` runs once for each child workflow that finishes successfully.
- `ChildFaulted` runs once for each child workflow that finishes with `WorkflowSubStatus.Faulted`.

While those ports run, Elsa provides:

- `WorkflowInstanceId` in the resumed input
- `WorkflowStatus` and `WorkflowSubStatus` in the resumed input
- `WorkflowOutput` in the resumed input
- a `ChildInstanceId` workflow variable

This makes the activity useful for fan-out/fan-in orchestration where the parent needs to count, aggregate, or compensate for per-child outcomes.

## Using it in Elsa Studio

In Elsa Studio, configure **Bulk Dispatch Workflows** with these fields:

- **Workflow Definition**: the child workflow definition to dispatch. Elsa resolves the published version.
- **Items**: the collection to fan out over.
- **Default Item Input Key**: the key used for non-dictionary items. Leave this as `Item` unless the child workflow expects a different input name.
- **Correlation ID Function**: an optional expression evaluated for each item.
- **Input**: shared input values added to every child workflow.
- **Wait For Completion**: whether the parent should block until all child workflows finish.
- **Channel**: optional dispatcher channel. Leaving it empty uses the default channel.

If you connect the `Child Completed` or `Child Faulted` ports, expect them to run once per child workflow, not once for the entire batch.

## Operational notes

- Elsa dispatches published workflow definitions only. If no published child workflow definition exists, the parent workflow faults.
- `BulkDispatchWorkflows` does not aggregate child outputs into one collection automatically. Handle that in the parent workflow through variables, `ChildCompleted`, or `ChildFaulted`.
- The parent-child relationship is tracked through `ParentWorkflowInstanceId` and `ParentInstanceId`, which lets Elsa resume the waiting parent workflow after child completion.
