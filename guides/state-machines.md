# State machines

Use a `StateMachine` when a workflow represents a business lifecycle: an
order moves from `New` to `Approved`, a case moves from `Open` to `Resolved`,
or a request moves through a set of named stages. Each state can run an entry
and exit activity. Transitions connect states and can be automatic, triggered,
conditional, or augmented with an action activity.

This guide describes the runtime behavior in Elsa 3.8.4 and the corresponding
Elsa Studio designer.

## The runtime model

A state machine contains:

| Element | Purpose |
| --- | --- |
| `States` | Named lifecycle stages. A state may have an `Entry` and `Exit` activity. |
| `InitialState` | The state entered when the activity has no current state. |
| `CurrentState` | The active state. During execution, the value stored in the activity execution context takes precedence over the serialized property. |
| `Transitions` | Routes from one named state to another. |
| `Trigger` | An activity that makes a transition eligible to run. Omit it for an automatic transition. |
| `Condition` | An `Input<bool>` evaluated after a triggered transition fires, or while an automatic transition is considered. A missing condition is treated as eligible. |
| `Action` | An activity that runs after the source state's exit activity and before the target state becomes active. |

The transition lifecycle is:

```text
enter state
  -> run entry activity
  -> evaluate automatic transitions and wait for triggered transitions
  -> evaluate the selected transition's condition
  -> run source exit activity
  -> run transition action
  -> set the target as current
  -> run target entry activity
  -> evaluate the target state's outbound transitions
```

If a state has no outbound transition to a known state, it is terminal and the
state machine completes after entering it. If several automatic transitions
are eligible, the first one in declaration order is selected. If an automatic
cycle is intentional, Elsa yields the continuation to the workflow scheduler
between iterations; it does not complete while the cycle continues.

## A C# example

The following example models an order that waits for two external events. The
`Event` instances are separate activities and have distinct non-empty IDs. The
runtime rejects shared trigger instances and duplicate non-empty trigger IDs;
Studio also requires valid activity and node metadata in serialized activity
slots.

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Activities.StateMachine.Activities;
using Elsa.Workflows.Activities.StateMachine.Models;
using Elsa.Workflows.Runtime.Activities;

public class OrderLifecycleWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder workflow)
    {
        workflow.Root = new StateMachine
        {
            InitialState = "New",
            States =
            {
                new StateMachineState
                {
                    Name = "New",
                    Entry = new WriteLine("Order created"),
                    Exit = new WriteLine("Leaving New")
                },
                new StateMachineState
                {
                    Name = "Approved",
                    Entry = new WriteLine("Order approved")
                },
                new StateMachineState { Name = "Closed" }
            },
            Transitions =
            {
                new Transition
                {
                    Name = "Approve",
                    From = "New",
                    To = "Approved",
                    Trigger = new Event("OrderApproved") { Id = "order-approved-trigger" },
                    Condition = new(true),
                    Action = new WriteLine("Applying approval")
                },
                new Transition
                {
                    Name = "Close",
                    From = "Approved",
                    To = "Closed",
                    Trigger = new Event("OrderClosed") { Id = "order-closed-trigger" }
                }
            }
        };
    }
}
```

When `New` is entered, Elsa runs its entry activity and then waits for
`OrderApproved`. When that trigger completes, Elsa evaluates the condition,
runs the `New` exit activity, runs the transition action, changes the current
state to `Approved`, and runs the `Approved` entry activity. The machine then
waits for `OrderClosed`; entering `Closed` completes the state machine because
it has no outbound transition.

The same structure can be represented in the JSON shape used by Elsa Studio.
For a Studio workflow definition, the activity type is `Elsa.StateMachine`,
and the important properties are `initialState`, `currentState`, `states`, and
`transitions`:

```json
{
  "type": "Elsa.StateMachine",
  "initialState": "New",
  "states": [
    { "name": "New" },
    { "name": "Approved" },
    { "name": "Closed" }
  ],
  "transitions": [
    {
      "name": "Approve",
      "from": "New",
      "to": "Approved",
      "trigger": { "type": "Elsa.Event", "eventName": "OrderApproved" },
      "condition": true
    },
    {
      "name": "Close",
      "from": "Approved",
      "to": "Closed",
      "trigger": { "type": "Elsa.Event", "eventName": "OrderClosed" }
    }
  ]
}
```

Activity properties inside `entry`, `exit`, `trigger`, and `action` must be
complete activity definitions for the host that executes the workflow. The
JSON above is a compact shape example; use the activity picker or a serialized
workflow definition when you need the full activity metadata.

## Choosing transition behavior

### Triggered transitions

Add a trigger when the state should wait for an external or asynchronous event.
For example, use an `Event` activity for a domain event, or another trigger
activity supplied by an enabled Elsa module. Elsa schedules all outbound
triggered transitions for the current state. When one completes, Elsa checks
that it still originates from the current state before accepting it.

Give every triggered transition its own trigger activity instance. Reusing the
same trigger object, or assigning the same non-empty activity ID to multiple
triggers, is rejected by the runtime.

### Automatic transitions

Omit `Trigger` when the transition should be evaluated immediately after state
entry. Automatic transitions are useful for deterministic routing after an
entry activity has completed. They can also create loops, so make sure a
condition or another state change eventually makes the machine leave the loop.

Automatic transitions are considered in declaration order. The first eligible
transition wins. If an automatic transition's condition is false, Elsa checks
the next automatic transition and leaves the state active when none can be
taken.

### Conditions

`Condition` is an `Input<bool>`, so it can be a literal or an expression. A
missing condition is eligible by default. A false condition does not fault the
workflow: for a triggered transition, the trigger is re-armed; for an
automatic transition, the state remains active and other outbound transitions
may still be considered.

Conditions should answer whether the transition is eligible. Put work that
changes state or performs side effects in the transition `Action`, state
`Entry`, or state `Exit` activity instead.

### Entry, exit, and action activities

Use the lifecycle slots deliberately:

| Slot | Runs |
| --- | --- |
| State `Entry` | Whenever the state becomes active, including after a transition. |
| State `Exit` | Before an accepted transition leaves the state. It is not run when a condition rejects a transition. |
| Transition `Action` | After source exit and before the target state is entered. |

These slots can contain composite activities such as `Sequence`, not only one
simple activity.

## Authoring in Elsa Studio

When the connected host advertises the `Elsa.StateMachine` activity, selecting
it in the workflow editor opens Studio's dedicated state-machine designer. The
designer has two views:

1. **Diagram** shows states and transition edges. Use **Add state** to create
   a named state and **Add transition** to choose its **From** and **To**
   states. **Auto layout**, **Zoom to fit**, and **Center** arrange or frame
   the graph.
2. **Outline** lists states and transitions when the diagram is crowded or
   when you need to navigate by name.

Use the **Initial state** selector to choose where a new execution begins.
`Current state` is also exposed by the editor for inspecting or editing the
serialized activity state. For an executing instance, the runtime value stored
in the activity execution context takes precedence over that serialized
property.

Select a state to open its inspector. The inspector lets you rename the state,
add or replace its **Entry action** and **Exit action**, view incoming and
outgoing transitions, and see whether the state is **Initial**, **Current**, or
**Terminal**.

Select a transition to edit its name, display name, source, and target. Its
execution-story inspector exposes:

* **Trigger** — add or replace the activity that starts the transition.
* **Condition** — choose **Always**, **Never**, an expression provider, or
  **Custom JSON**, then select **Apply**. The available expression providers
  depend on the connected host.
* **Action** — add or replace the activity that runs after source exit.
* **To** — the target state reached after the action completes.

Studio validates duplicate or empty state names, missing transition endpoints,
duplicate transition identities, invalid activity slots, and invalid initial or
current-state references. Errors prevent exporting the edited graph. Warnings
such as an initial or current state that names no existing state should still
be resolved before publishing. In read-only mode, creation and editing
controls are disabled.

## Design and troubleshooting checklist

Before publishing a state machine, check:

- Every `From` and `To` value matches exactly one state name.
- `InitialState` names an existing state unless the host intentionally supplies
  the current state at runtime.
- Every triggered transition has a distinct trigger instance and activity ID.
- Automatic transitions have conditions that prevent accidental infinite loops.
- Entry, exit, and action activities are idempotent when an instance can be
  retried or resumed.
- A state with no outbound transition is intentional. It completes the
  `StateMachine` activity as soon as its entry work finishes.
- You test both the accepted and rejected condition paths, including the case
  where a triggered transition is re-armed.

If the machine completes earlier than expected, inspect the current state's
outbound transitions: a transition without a trigger is automatic, and a state
with no valid outbound route is terminal. If a transition never advances,
verify its trigger activity, condition result, source-state name, and target
state name before investigating persistence or hosting.

## Release source

The behavior and Studio affordances in this guide were checked against the
`release/3.8.4` refs used by the documentation run. The release refs currently
resolve to the following immutable commits:

- Core [`StateMachine`](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Workflows.Core/Activities/StateMachine/Activities/StateMachine.cs), [`StateMachineState`](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Workflows.Core/Activities/StateMachine/Models/StateMachineState.cs), and [`Transition`](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Workflows.Core/Activities/StateMachine/Models/Transition.cs)
- Core [`StateMachineTests`](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/test/unit/Elsa.Activities.UnitTests/StateMachine/StateMachineTests.cs)
- Studio [`StateMachineDesignerWrapper`](https://github.com/elsa-workflows/elsa-studio/blob/1c72dc02c837919059b60efe5df2ed57ff2db2d9/src/modules/Elsa.Studio.Workflows/DiagramDesigners/StateMachines/StateMachineDesignerWrapper.razor) and [code-behind](https://github.com/elsa-workflows/elsa-studio/blob/1c72dc02c837919059b60efe5df2ed57ff2db2d9/src/modules/Elsa.Studio.Workflows/DiagramDesigners/StateMachines/StateMachineDesignerWrapper.razor.cs)
- Studio [`StateMachineStateInspector`](https://github.com/elsa-workflows/elsa-studio/blob/1c72dc02c837919059b60efe5df2ed57ff2db2d9/src/modules/Elsa.Studio.Workflows/DiagramDesigners/StateMachines/Presentation/StateMachineStateInspector.razor), [`StateMachineTransitionInspector`](https://github.com/elsa-workflows/elsa-studio/blob/1c72dc02c837919059b60efe5df2ed57ff2db2d9/src/modules/Elsa.Studio.Workflows/DiagramDesigners/StateMachines/Presentation/StateMachineTransitionInspector.razor), and [`StateMachineValidator`](https://github.com/elsa-workflows/elsa-studio/blob/1c72dc02c837919059b60efe5df2ed57ff2db2d9/src/modules/Elsa.Studio.Workflows.Designer/Services/StateMachineValidator.cs)
- Studio [`StateMachineMapperTests`](https://github.com/elsa-workflows/elsa-studio/blob/1c72dc02c837919059b60efe5df2ed57ff2db2d9/src/modules/Elsa.Studio.Workflows.Designer.Tests/StateMachineMapperTests.cs)
