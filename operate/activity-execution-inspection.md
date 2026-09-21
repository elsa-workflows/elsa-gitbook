---
description: Inspect captured activity execution evidence in Elsa Studio.
---

# Inspect activity executions in Elsa Studio

Use the activity-execution view when you need to understand what a particular
activity instance captured: its state, selected outcomes, outputs, or recorded
retry attempts. It is useful after the workflow status and journal identify an
activity to investigate.

The view reads activity-execution records from the Elsa server. It is not a
complete live-runtime trace, and an empty panel can mean that the host did not
persist that kind of data. Start with [Investigate a Workflow
Instance](workflow-state-and-journal.md) when you first need the workflow's
status, sub-status, incidents, or journal timeline.

## Before you start

- Open a Studio host connected to the Elsa server that owns the instance.
- Make sure your role can read activity executions with
  `read:activity-execution`. The summary, full-record, and call-stack endpoints
  use this permission.
- If you need retry details, the connected host must also expose the Elsa
  Resilience feature and allow the retry endpoint's configured permissions:
  `read:*`, `read:resilience`, or `read:resilience:retries`. A retry panel is
  not shown when no retry records are returned.
- If state, outcomes, or output are missing, check the workflow's [log
  persistence](../optimize/log-persistence.md) configuration before treating
  the absence as an execution failure.

## Open the execution details

1. In Elsa Studio, open **Workflow Instances** and select the instance you
   want to investigate. The instance viewer is available at the
   `/workflows/instances/{id}/view` route.
2. In the read-only workflow diagram, select the activity. Studio loads the
   execution summaries for that activity.
3. In the activity properties pane, select **Executions**. The tab badge shows
   the number of returned execution summaries.
4. Select an execution row. The row includes its start and completion times,
   duration, status, retry indicator, and activity-instance ID. Studio loads
   the full record and opens **Execution Details** in a drawer.
5. Close the drawer with **Close** or by selecting the overlay. Select another
   row to inspect a different execution.

For a running activity that has not reached a fused terminal state, Studio
refreshes the selected record and its retry data about once per second. It
stops that refresh after the record becomes fused. This makes the drawer useful
for observing an in-progress execution, but it does not replace host logs or
the workflow journal.

## Read the drawer

The drawer contains these panels:

| Panel | What it represents |
| --- | --- |
| **State** | The persisted activity-state values associated with the selected execution. Keys beginning with `_` are hidden by Studio. |
| **Outcomes** | The `Outcomes` value from the execution payload, when one was recorded. |
| **Output** | Values from the execution record's output collection. |
| **Retry Attempts** | Retry records returned for the activity execution. The panel is rendered only when at least one record is returned. |

Studio displays an explicit no-data message for an empty State, Outcomes, or
Output panel. That message means the selected record has no value available to
render; it does not prove that the activity never held an in-memory value.

The drawer does not present the workflow's incident or exception details. Use
the journal and incident views for the execution timeline and fault context,
then use the activity record to inspect the captured state and output. The
**Activity** tab may also show the last execution's status and exception data;
it is a different view from the execution-specific drawer.

## Interpret common results

### The Executions tab is empty

Confirm that you selected the intended activity node and that the instance has
actually reached that activity. Then check the server response and the
`read:activity-execution` permission. The tab is populated from the activity
execution summary endpoint, filtered by workflow instance ID and activity node
ID.

### A row exists but the drawer has no state or output

The summary and full record are separate reads. A row can therefore exist even
when the full record has no persisted state, outcomes, or outputs. Review the
workflow's log-persistence settings and the activity's configured persistence
scope. Do not infer that the activity returned no output solely from an empty
panel.

### No Retry Attempts panel appears

Studio only renders the panel when its retry request returns records. Check
that the Elsa Resilience feature is enabled for the host, the role can read
resilience retries, and the selected activity execution actually recorded
retry attempts. A missing panel is not, by itself, proof that no transient
retry occurred elsewhere in the workflow.

### The instance is still running or waiting

Use the status, sub-status, journal, and bookmark information in [Investigate a
Workflow Instance](workflow-state-and-journal.md) to decide whether the
workflow is executing, suspended, or waiting for an external stimulus. Use
the execution drawer to inspect the selected activity's captured evidence, not
to decide whether the whole workflow is healthy.

## Use the API when Studio is not enough

Studio uses the following server contracts for this view. Hosts may configure a
different API prefix; the route suffixes remain the same.

| Need | Route |
| --- | --- |
| Activity execution summaries | `GET /activity-execution-summaries/list?workflowInstanceId=INSTANCE_ID&activityNodeId=NODE_ID` |
| Full execution record | `GET /activity-executions/EXECUTION_ID` |
| Full records for an activity | `GET /activity-executions/list?workflowInstanceId=INSTANCE_ID&activityNodeId=NODE_ID` |
| Execution call stack | `GET /activity-executions/EXECUTION_ID/call-stack` |
| Retry attempts | `GET /resilience/retries/EXECUTION_ID` |

All activity-execution routes require `read:activity-execution`. The retry
route has its own resilience-read permission configuration. Use the API when
you need a call stack, paging, raw record data, or a diagnostic script rather
than the Studio presentation.

## Release source

The behavior and UI path in this guide were checked against the Elsa `release/3.8.4`
refs used for this documentation run:

- Studio [`WorkflowInstanceDesigner`](https://github.com/elsa-workflows/elsa-studio/blob/9bff3f785fd13bd80a3a7ecf88fec4aec8eef7ae/src/modules/Elsa.Studio.Workflows/Components/WorkflowInstanceViewer/Components/WorkflowInstanceDesigner.razor), [`WorkflowInstanceViewer`](https://github.com/elsa-workflows/elsa-studio/blob/9bff3f785fd13bd80a3a7ecf88fec4aec8eef7ae/src/modules/Elsa.Studio.Workflows/Components/WorkflowInstanceViewer/WorkflowInstanceViewer.razor), [`WorkflowInstanceViewer` code-behind](https://github.com/elsa-workflows/elsa-studio/blob/9bff3f785fd13bd80a3a7ecf88fec4aec8eef7ae/src/modules/Elsa.Studio.Workflows/Components/WorkflowInstanceViewer/WorkflowInstanceViewer.razor.cs), [`ActivityExecutionsTab`](https://github.com/elsa-workflows/elsa-studio/blob/9bff3f785fd13bd80a3a7ecf88fec4aec8eef7ae/src/modules/Elsa.Studio.Workflows/Components/WorkflowInstanceViewer/Components/ActivityExecutionsTab.razor), [`ActivityExecutionsTab` code-behind](https://github.com/elsa-workflows/elsa-studio/blob/9bff3f785fd13bd80a3a7ecf88fec4aec8eef7ae/src/modules/Elsa.Studio.Workflows/Components/WorkflowInstanceViewer/Components/ActivityExecutionsTab.razor.cs), [`ActivityExecutionDetails`](https://github.com/elsa-workflows/elsa-studio/blob/9bff3f785fd13bd80a3a7ecf88fec4aec8eef7ae/src/modules/Elsa.Studio.Workflows/Components/WorkflowInstanceViewer/Components/ActivityExecutionDetails.razor), [`ActivityExecutionDetails` code-behind](https://github.com/elsa-workflows/elsa-studio/blob/9bff3f785fd13bd80a3a7ecf88fec4aec8eef7ae/src/modules/Elsa.Studio.Workflows/Components/WorkflowInstanceViewer/Components/ActivityExecutionDetails.razor.cs), and [`RemoteActivityExecutionService`](https://github.com/elsa-workflows/elsa-studio/blob/9bff3f785fd13bd80a3a7ecf88fec4aec8eef7ae/src/modules/Elsa.Studio.Workflows.Core/Domain/Services/RemoteActivityExecutionService.cs)
- Core [`activity-execution summary endpoint`](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Workflows.Api/Endpoints/ActivityExecutionSummaries/ListSummaries/Endpoint.cs), [`activity-execution list endpoint`](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Workflows.Api/Endpoints/ActivityExecutions/List/Endpoint.cs), [`activity-execution detail endpoint`](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Workflows.Api/Endpoints/ActivityExecutions/Get/Endpoint.cs), and [`retry endpoint`](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Resilience/Endpoints/Retries/List/Endpoint.cs)
- Studio regression coverage in [`ActivityExecutionDetailsTests`](https://github.com/elsa-workflows/elsa-studio/blob/9bff3f785fd13bd80a3a7ecf88fec4aec8eef7ae/src/modules/Elsa.Studio.Workflows.Tests/ActivityExecutionDetailsTests.cs) and [`ExecutionDetailsDrawerRenderTests`](https://github.com/elsa-workflows/elsa-studio/blob/9bff3f785fd13bd80a3a7ecf88fec4aec8eef7ae/src/modules/Elsa.Studio.Workflows.Tests/ExecutionDetailsDrawerRenderTests.cs)
