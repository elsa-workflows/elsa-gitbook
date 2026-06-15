---
description: >-
  Understand what Elsa 3.8 stores in workflow state, activity state, variables,
  inputs, outputs, bookmarks, incidents, and execution logs.
---

# Workflow Context

Elsa workflows execute with more than one kind of state in play. Some state belongs to the workflow instance as a whole, some belongs to the currently active activity stack, and some is only available while expressions are being evaluated.

This page explains how that fits together in Elsa `release/3.8.0`, and where each piece shows up in code, APIs, and Elsa Studio.

## The three layers

### Workflow definition

The workflow definition is the published design: activities, variables, inputs, outputs, outcomes, options, and metadata.

In code-first workflows, these are declared on `IWorkflowBuilder`:

```csharp
protected override void Build(IWorkflowBuilder builder)
{
    var customerId = builder.WithInput<string>("CustomerId");
    builder.WithOutput<string>("Result");
    var status = new Variable<string>("Status", "Pending");

    builder.WithVariable(status);
    builder.Root = new Sequence
    {
        Activities =
        {
            new WriteLine(context => $"Customer: {context.GetInput<string>(customerId)}")
        }
    };
}
```

This matches the `Inputs`, `Outputs`, and `Variables` collections on `WorkflowBuilder`.

### Workflow instance

A workflow instance is the persisted runtime state for one execution. In Elsa 3.8, `WorkflowState` stores:

- definition identity and version
- workflow `Input`
- workflow `Output`
- workflow `Properties`
- `Bookmarks`
- `Incidents`
- active `ActivityExecutionContexts`
- scheduled work items and completion callbacks
- timestamps and execution status

That is the state you inspect in the instance viewer, structured logs, and workflow-instance APIs.

### Activity execution context

Each active activity executes inside an `ActivityExecutionContext`. This is where Elsa keeps activity-local runtime data such as:

- evaluated activity input state in `ActivityState`
- activity-specific `Properties`
- lightweight `Metadata`
- `DynamicVariables`
- activity `JournalData`
- bookmarks created by the activity

When a workflow is suspended, Elsa persists the active activity execution contexts as a flattened call stack inside workflow state and reconstructs them when resuming.

## What lives where

| Concern | Where it lives | Notes |
| --- | --- | --- |
| Workflow inputs | `WorkflowExecutionContext.Input` / `WorkflowState.Input` | Persisted only for inputs configured with a workflow or workflow-instance storage driver. |
| Workflow outputs | `WorkflowExecutionContext.Output` / `WorkflowState.Output` | Written explicitly, typically with `SetOutput`. |
| Workflow properties | `WorkflowExecutionContext.Properties` / `WorkflowState.Properties` | Global property bag for application or activity data. |
| Variables | Memory blocks referenced by `Variable` objects | Scoped through the expression context chain. |
| Activity input snapshots | `ActivityExecutionContext.ActivityState` | Persisted for historical/runtime inspection, subject to activity-state filtering. |
| Bookmarks | `WorkflowExecutionContext.Bookmarks` / `WorkflowState.Bookmarks` | Pause and resume points for blocking work. |
| Incidents | `WorkflowExecutionContext.Incidents` / `WorkflowState.Incidents` | Recorded failures and diagnostic information. |
| Execution log | `WorkflowExecutionContext.ExecutionLog` plus persisted log records | Used for the journal and structured log views. |

## Variables and scoping

Variables in Elsa are memory block references. A variable declared as `new Variable<string>("Foo", "Bar")` gets a deterministic ID derived from its name and is resolved through the current expression-context chain.

Variables can be declared at more than one level:

- workflow level
- container level, such as `Sequence`
- dynamically at runtime

Scope matters. Elsa resolves variables from the nearest matching scope first. The integration tests on `release/3.8.0` verify that when two scopes define `Foo`, `SetVariable` updates the nearest one.

```csharp
var workflowLevelVariable = new Variable<string>("Foo", "Workflow Value");
var sequenceLevelVariable = new Variable<string>("Foo", "Initial Value");

workflow.Root = new Sequence
{
    Variables = { workflowLevelVariable },
    Activities =
    {
        new Sequence
        {
            Variables = { sequenceLevelVariable },
            Activities =
            {
                new SetVariable
                {
                    Variable = sequenceLevelVariable,
                    Value = new("Sequence Value")
                },
                new WriteLine(context => context.GetVariable<string>("Foo"))
            }
        }
    }
};
```

Use variables when the data belongs to workflow state that several activities need to read or update over time.

Use workflow inputs when the caller supplies the value at start or resume time.

Use workflow outputs when the workflow needs to return named results to the caller or parent workflow.

## Inputs

Workflow inputs are declared on the workflow definition and stored in the workflow input bag at runtime.

In code, you usually read them in one of two ways:

```csharp
var message = builder.WithInput<string>("Message");

builder.Root = new WriteLine(context => context.GetInput<string>(message));
```

```csharp
builder.Root = new WriteLine(context => context.GetWorkflowInput<string>("Message"));
```

There is one important nuance in Elsa 3.8: `ExpressionExecutionContext.GetInput(name)` first checks for a variable with that name when the current activity executes inside a composite activity. If such a variable exists, that variable value wins over the workflow input.

For Studio users, workflow inputs are defined on the workflow's **Inputs** tab and can then be referenced from activity editors and expressions.

## Outputs

Workflow outputs are named values declared on the workflow definition. Elsa persists them in `WorkflowExecutionContext.Output` and `WorkflowState.Output`.

Code-first workflows typically declare outputs with `WithOutput<T>()` and set them with the `SetOutput` activity:

```csharp
var valueInput = builder.WithInput<double>("Value");
var output = builder.WithOutput<double>("Output");

builder.Root = new Sequence
{
    Activities =
    {
        new SetOutput
        {
            OutputName = new(output.Name),
            OutputValue = new(context => context.GetInput<double>(valueInput) * 2)
        }
    }
};
```

`SetOutput` writes to the nearest ancestor output when running inside a composite or workflow-as-activity scenario, and also updates the root workflow output bag when needed.

For Studio users, workflow outputs are defined on the **Outputs** tab and are shown in the workflow instance viewer under **Input/output**.

## Activity state versus variable state

This is a common source of confusion:

- Variables are live workflow data meant to be read and updated by activities and expressions.
- `ActivityState` is Elsa's persisted snapshot of evaluated activity input values for an activity execution context.

`ActivityState` is useful for diagnostics, replay context, and historical inspection. It is not the main API you should use to exchange data between activities.

Elsa can filter activity state before persistence. For example, the sample server registers an activity-state filter that strips HTTP authentication header values from persisted HTTP request state.

## Bookmarks and suspension

Bookmarks represent places where a workflow can pause and later resume. Blocking activities create them. They are stored on workflow state, not on a separate variable system.

When a workflow blocks:

1. active activity execution contexts are persisted
2. bookmarks are stored on `WorkflowState.Bookmarks`
3. the workflow resumes later by selecting the matching bookmark

This is why long-running workflows survive process restarts as long as runtime persistence is configured correctly.

## Incidents

Incidents are workflow-level records of execution failures. They are stored in `WorkflowState.Incidents` and surfaced in Elsa Studio's workflow instance viewer.

Use incidents when you want to understand:

- which activity failed
- the exception message and stack trace
- whether a workflow needs retry, cancellation, or manual intervention

## Execution logs and journal data

Elsa records execution-log entries such as `Started`, `Resumed`, `Suspended`, `Completed`, and `Faulted` during workflow and activity execution.

Activity execution contexts also expose `JournalData`, which is appended to execution-log records. Elsa uses that to record details such as selected outcomes and serialized output values.

Use the journal when you need an execution timeline.

Use variables, inputs, outputs, and properties when you need the current state of the workflow.

## Elsa Studio mapping

For workflow authors using Studio:

- **Variables**, **Inputs**, and **Outputs** are workflow-definition tabs.
- The workflow instance viewer shows **Variables**, **Input/output**, and **Incidents** for a running or completed instance.
- The alteration designer can also load and modify workflow instance variables.
- Expression editors can read workflow inputs and variables; see [Expressions in Elsa Studio](../../guides/studio/expressions.md).

## Inspecting and updating instance variables

If you need to inspect or correct variable values on a live workflow instance, use the variable-management API or `IWorkflowInstanceVariableManager`.

That operational workflow is covered in [Workflow Instance Variables](../../operate/workflow-instance-variables.md).

## When to use what

Use this rule of thumb:

- Use **inputs** for caller-supplied values.
- Use **variables** for mutable workflow state shared across steps.
- Use **outputs** for named results.
- Use **properties** for application-owned workflow metadata.
- Use **bookmarks** to understand why a workflow is waiting.
- Use **incidents** and the **journal** to diagnose faults and execution history.
