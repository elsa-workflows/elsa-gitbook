---
description: Choose the Elsa activity that fits a workflow task and find its detailed guide.
---

# Activity Reference

This is a task-oriented map of the activities supplied by Elsa's core modules.
Use it to choose a building block in Elsa Studio or in code, then follow the
linked guide when the activity has important setup or operational behavior.

> The picker is not a fixed product catalogue. Elsa Studio builds its activity
> tree from the activity descriptors supplied by the server, grouped by each
> activity's category. The activities you can use therefore depend on the Elsa
> modules that the host has enabled, plus any custom activities it registers.

## Start with the job to do

| If the workflow needs to… | Start with | Notes |
| --- | --- | --- |
| Move through steps in order | **Sequence** or a **Flowchart** | A Sequence executes child activities in order. A Flowchart models connected nodes and is the designer format supported by Elsa Studio. |
| Choose a path | **Decision**, **If**, **Switch**, or **Fork (flow)** | Use Decision/If for a Boolean choice, Switch for cases, and Fork (flow) for concurrent flowchart paths. |
| Repeat work | **For**, **For Each**, **While**, or **Parallel For Each** | Use the parallel form only when the child work is safe to run concurrently. **Break** exits a loop. |
| Keep or change workflow data | **Set Variable**, **Correlate**, or **Set Name** | Variables carry data in their declared scope; correlation and instance name make related instances easier to find. |
| Wait for time or an external event | **Delay**, **Timer**, **Cron**, **HTTP Endpoint**, or **Event** | These activities create a wait/trigger boundary rather than keeping a request open. |
| Start another workflow | **Dispatch Workflow**, **Execute Workflow**, or **Bulk Dispatch Workflows** | Pick the child-workflow behavior deliberately; the linked guides explain dispatch and fan-out semantics. |
| Call or expose an HTTP service | **HTTP Request**, **HTTP Request (flow)**, **HTTP Endpoint**, or **HTTP Response** | Enable the HTTP module and review security before exposing an endpoint. |
| Record progress or deliberately stop | **Log**, **Write Line**, **Finish**, or **Fault** | Log/Write Line are diagnostics; Finish ends normally and Fault raises a categorized workflow fault. |

## Core workflow construction

These activities are supplied by the core workflow/runtime modules in Elsa
3.8.0.

| Category | Activities | Use them when |
| --- | --- | --- |
| Workflow shapes | **Sequence**, **Flowchart**, **State Machine**, **Parallel** | Choosing the structure that owns and schedules child activities. Elsa Studio supports dedicated designers for Sequence, Flowchart, and State Machine. |
| Flowchart nodes | **Start**, **End**, **Container**, **Decision**, **Switch (flow)**, **Fork (flow)**, **Join** | Connecting and steering paths in a Flowchart. |
| Branching and loops | **If**, **Switch**, **Fork**, **For**, **For Each**, **While**, **Parallel For Each**, **Break** | Selecting a branch or iterating. Guard loop conditions and make parallel work independent. |
| State and identity | **Set Variable**, **Set Name**, **Correlate** | Updating values in their declared scope, the instance display name, or its correlation ID. See [common properties](common-properties.md) for expression, storage, and persistence settings. |
| Completion and faults | **Complete**, **Finish**, **Fault** | Completing a composite, finishing the workflow, or reporting a categorized fault for incident handling. |
| Composition | **Dispatch Workflow**, **Execute Workflow**, **Bulk Dispatch Workflows** | Starting child instances, either as a dispatch, an execution, or one per item. See [workflow-as-activity](workflow-as-activity/README.md), [Dispatch Workflow](../guides/running-workflows/dispatch-workflow-activity.md), and [Bulk Dispatch Workflows](../guides/running-workflows/bulk-dispatch-workflows.md). |
| Events | **Publish Event**, **Event** | Publishing a named event or waiting for one. For a custom blocking or trigger activity, see [Blocking Activities & Triggers](blocking-and-triggers/README.md). |

## Time and scheduled execution

The scheduling module supplies **Delay**, **Start At**, **Timer**, and
**Cron**. Delay pauses the current execution for a duration. Start At, Timer,
and Cron are trigger-oriented scheduling activities; they schedule work for a
specific future time, interval, or cron expression.

Use the [Timer and Scheduled Workflows guide](../guides/running-workflows/timer-and-scheduled-workflows.md)
to choose the right activity, configure persistent scheduling, and understand
how scheduled work resumes after a restart. For workflows that wait on people
or systems as well as time, see [Long-running Workflows](../guides/running-workflows/long-running-workflows.md).

## HTTP activities

When the HTTP module is enabled, the HTTP category includes:

- **HTTP Endpoint** — waits for an inbound request matching its configured path and methods.
- **HTTP Response** and **HTTP File Response** — write the response for the current HTTP request.
- **HTTP Request** and **HTTP Request (flow)** — send an outbound request; use the flow variant when wiring its outcomes in a Flowchart.
- **Download File** — downloads a file from a URL.

The [HTTP workflows guide](../guides/http-workflows/README.md) covers the
programmatic and designer paths. Before publishing an HTTP Endpoint, follow
the [HTTP endpoint security guide](../guides/security/http-endpoint-security.md);
workflow endpoint authorization is configured separately from access to Elsa's
management APIs.

## Integration, diagnostics, and custom activities

Additional installed modules contribute their own categories. They only appear
after the corresponding module is installed and configured.

- [MassTransit](masstransit/README.md) explains message send, publish, and receive patterns.
- [Diagnostics](diagnostics/README.md) explains the Log activity and where to inspect output.
- [Custom Activities](../extensibility/custom-activities.md) shows how to add a domain-specific activity to the same catalogue.

## Choosing safely in Elsa Studio

1. Search the activity picker by the task or display name, then check the
   category and description before adding it.
2. Configure inputs as expressions or literals as appropriate; give important
   activities meaningful names and display names so later expressions and
   operators can identify them.
3. Treat every wait, trigger, and child-workflow activity as a lifecycle
   decision: decide who resumes it, how it is observed, and what happens when
   it faults.
4. Test a representative path, including outcomes and any external callback,
   before publishing the workflow definition.

If an activity is absent, first verify that its module is enabled on the Elsa
Server that Studio is connected to. If it is a custom activity, its host must
register it; adding a type only to Studio does not make it executable on the
server.
