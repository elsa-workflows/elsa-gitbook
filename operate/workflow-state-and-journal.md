---
description: >-
  Investigate a live Elsa 3.8.2 workflow instance with Studio and the runtime
  APIs for state, journal entries, activity executions, and variables.
---

# Investigate a Workflow Instance

Use this guide when an operator needs to answer: **what is this workflow doing,
where did it stop, and what should we inspect next?** It is an investigation
playbook, not a replacement for normal workflow design or incident handling.

In the default Elsa Server host, the routes below are under `/elsa/api`. Hosts
can use a different API prefix, so treat that prefix as an example and keep the
route suffixes unchanged.

## Choose the right signal

| Question | Start here | Required permission |
| --- | --- | --- |
| Which instances are waiting, faulted, or have incidents? | `GET` or `POST /workflow-instances` | `read:workflow-instances` |
| What is the full persisted state of one instance? | `GET /workflow-instances/{id}` | `read:workflow-instances` |
| Did it change state recently? | `GET /workflow-instances/{id}/execution-state` | `read:workflow-instances` |
| Which activity transition explains the result? | `GET` or `POST /workflow-instances/{id}/journal` | `read:workflow-instances` |
| What was captured for one activity execution? | `/activity-executions/*` | `read:activity-execution` |
| What are the root workflow's declared variables? | `/workflow-instances/{id}/variables` | `read:workflow-instances` |

The two permissions are deliberately separate. A role that can read an
instance's status and journal does not automatically have access to detailed
activity execution records.

## Investigation flow

### 1. Find the instance

In Elsa Studio, open **Workflow Instances**, use the definition, status, and
incident filters, then open the instance. Start with its status, incidents, and
journal; use the activity and variable views when the timeline identifies a
specific execution or value to inspect.

For a support tool or script, `GET` and `POST /workflow-instances` both use the
same request model. Use `POST` when the filter is easier to send as JSON:

```bash
curl --request POST \
  'https://localhost:5001/elsa/api/workflow-instances' \
  --header 'Authorization: Bearer YOUR_TOKEN' \
  --header 'Content-Type: application/json' \
  --data '{
    "definitionId": "order-approval",
    "statuses": ["Running"],
    "subStatuses": ["Suspended"],
    "hasIncidents": false,
    "page": 0,
    "pageSize": 20
  }'
```

The list response contains `items` and `totalCount`. The list endpoint accepts
definition ID, correlation ID, name, version, status, sub-status, incident,
system-workflow, search, timestamp, ordering, and paging filters. Pages are
zero-based; use a positive `pageSize`.

### 2. Read status before assuming it is stuck

Fetch the instance when you need its full persisted `workflowState`, incident
count, correlation ID, timestamps, and parent instance ID:

```bash
curl \
  'https://localhost:5001/elsa/api/workflow-instances/INSTANCE_ID' \
  --header 'Authorization: Bearer YOUR_TOKEN'
```

Use the smaller execution-state endpoint for polling:

```bash
curl \
  'https://localhost:5001/elsa/api/workflow-instances/INSTANCE_ID/execution-state' \
  --header 'Authorization: Bearer YOUR_TOKEN'
```

In Elsa 3.8.2, the broad status is `Running` or `Finished`. The sub-status
provides the operational detail:

| Sub-status | Meaning for an operator |
| --- | --- |
| `Pending` | The instance is awaiting execution. |
| `Executing` | The runtime is executing its current cycle. |
| `Suspended` | The instance is waiting for an external stimulus, such as a timer or bookmark resume. |
| `Finished` | The instance completed successfully. |
| `Cancelled` | An operator or workflow path cancelled the instance. |
| `Faulted` | Execution ended with a fault; inspect incidents and the journal. |
| `Interrupted` | A graceful runtime drain force-cancelled the current execution cycle. The instance is resumable and recovered by the runtime. |

`Running` with `Suspended` is usually a healthy waiting workflow, not a failed
one. For timer and bookmark behavior, see [Timer and Scheduled
Workflows](../guides/running-workflows/timer-and-scheduled-workflows.md).

### Read incidents as execution evidence

An incident's `ActivityNodeId` identifies the static node in the workflow
definition. The 3.8.2 incident model does not carry a separate activity-
execution ID, so when the same node can run in a loop, be retried, or execute
concurrently, correlate the incident with the journal and activity execution
records instead of treating the node ID as a unique execution identifier.

If a containing activity handles a child fault, `RecoverFromFault` resets the
activity fault count and returns the activity to `Running`. In `release/3.8.2`,
it does not remove the incident or clear the recorded exception. The journal
and persisted incident collection must therefore be read separately when
deciding whether a fault remains operationally visible.

### Understand state rehydration warnings

`WorkflowState` is the persisted snapshot used to resume an instance. It
contains the definition identity, status and sub-status, bookmarks, incidents,
scheduled work, properties, input/output, and the active activity execution
contexts needed to reconstruct the in-memory call stacks.

When a persisted instance is loaded against a newer definition, a saved
activity execution context or completion callback can refer to an activity or
node that no longer exists. Elsa `release/3.8.2` skips that unresolved
reference and logs a warning classified as `MigrationCompatible` when the
persisted and target definition version IDs differ, or `Unexpected` otherwise.
Treat that warning as migration/recovery evidence: inspect the host logs and
definition version before retrying or mutating the instance. It does not mean
that every field in the persisted workflow state was discarded.

### 3. Follow the journal timeline

The journal is the ordered execution log for one workflow instance. Retrieve a
page in sequence order:

```bash
curl \
  'https://localhost:5001/elsa/api/workflow-instances/INSTANCE_ID/journal?page=0&pageSize=100' \
  --header 'Authorization: Bearer YOUR_TOKEN'
```

Each item includes the activity and node IDs, sequence, timestamp, event name,
message, source, activity state, and payload. Use the `activityNodeId` from a
relevant journal entry in the next step.

To narrow a noisy journal, post a filter. Elsa supports activity IDs, node IDs,
event names, and excluded activity types:

```bash
curl --request POST \
  'https://localhost:5001/elsa/api/workflow-instances/INSTANCE_ID/journal' \
  --header 'Authorization: Bearer YOUR_TOKEN' \
  --header 'Content-Type: application/json' \
  --data '{
    "filter": {
      "activityNodeIds": ["ApproveOrder"],
      "eventNames": ["Faulted", "Suspended"]
    },
    "page": 0,
    "pageSize": 50
  }'
```

The journal is the right place for a timeline. Use an incident for exception
details and the activity-execution APIs for the captured activity snapshot.

### 4. Inspect the relevant activity execution

Activity execution APIs require `read:activity-execution`. They are distinct
from the workflow-instance APIs because they can expose the detailed captured
state of an activity. For the Studio workflow-instance viewer's click path and
the limits of the execution drawer, see [Inspect Activity Executions in Elsa
Studio](activity-execution-inspection.md).

List a compact view for a workflow instance and activity node:

```bash
curl \
  'https://localhost:5001/elsa/api/activity-execution-summaries/list?workflowInstanceId=INSTANCE_ID&activityNodeId=ApproveOrder' \
  --header 'Authorization: Bearer YOUR_TOKEN'
```

Use `/activity-executions/list` instead when you need full records. Then fetch
one record by its ID:

```bash
curl \
  'https://localhost:5001/elsa/api/activity-executions/ACTIVITY_EXECUTION_ID' \
  --header 'Authorization: Bearer YOUR_TOKEN'
```

For a nested or dispatched workflow, retrieve the execution chain to determine
the path that led to the record:

```bash
curl \
  'https://localhost:5001/elsa/api/activity-executions/ACTIVITY_EXECUTION_ID/call-stack?includeCrossWorkflowChain=true' \
  --header 'Authorization: Bearer YOUR_TOKEN'
```

Captured activity data depends on your log-persistence settings. If records are
missing inputs, outputs, or internal state, confirm the configured persistence
level before treating that as a runtime failure. See [Log
Persistence](../optimize/log-persistence.md).

### 5. Inspect or correct variables carefully

The instance viewer's **Variables** tab is the safer first choice for a manual
inspection. The API also exposes `GET /workflow-instances/{id}/variables` and
`POST /workflow-instances/{id}/variables`; the read route requires
`read:workflow-instances`, while mutation requires `write:workflow-instances`.
The list contains the root workflow's declared variables and excludes values
tagged `LargeData`; do not mistake it for every activity-local or dynamic value
in the execution context. Use the instance's persisted state or the
application's storage policy when an expected large value is not returned.

Variable mutation saves the workflow instance, so use it only after recording
the original value and confirming that the workflow is suspended or otherwise
safe to change. A mutation only applies when its ID matches a declared root
variable. For request and response shapes, the programmatic API, and mutation
guidance, see [Workflow Instance
Variables](workflow-instance-variables.md).

## Recovery decision

After investigation, choose the action that matches the evidence:

- **Suspended and expected:** leave it waiting; check its expected bookmark,
  timer, or external caller instead of cancelling it.
- **Faulted:** read the incident and surrounding journal entries, fix the
  cause, then use the documented operational retry path if appropriate.
- **Wrong variable value:** correct only the specific persisted variable, with
  the `write:workflow-instances` permission and an audit trail.
- **Unexpected execution chain:** use the call stack and parent workflow ID to
  trace the caller before retrying or cancelling.

For fault handling and retry choices, see [Incidents](incidents/README.md).

## Related guides

- [Monitoring & Observability](monitoring-observability.md)
- [Incidents](incidents/README.md)
- [Workflow Instance Variables](workflow-instance-variables.md)
- [Timer and Scheduled Workflows](../guides/running-workflows/timer-and-scheduled-workflows.md)
- [API & Client](../guides/api-client/README.md)

## Release source

This guide is grounded in Elsa Core `release/3.8.2` at commit
`33181ae3048f628f591a0155b5665a8e4d1bcea2` and Elsa Studio at commit
`1c72dc02c837919059b60efe5df2ed57ff2db2d9`:

- [Workflow state model](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Workflows.Core/State/WorkflowState.cs)
- [Workflow state extraction and rehydration](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Workflows.Core/Services/WorkflowStateExtractor.cs)
- [Activity fault and recovery](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Workflows.Core/Extensions/ActivityExecutionContextExtensions.cs)
- [Activity incident model](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Workflows.Core/Models/ActivityIncident.cs)
- [Studio workflow-instance details](https://github.com/elsa-workflows/elsa-studio/blob/1c72dc02c837919059b60efe5df2ed57ff2db2d9/src/modules/Elsa.Studio.Workflows/Components/WorkflowInstanceViewer/Components/WorkflowInstanceDetails.razor)
