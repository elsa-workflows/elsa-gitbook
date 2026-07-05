---
description: >-
  Release-backed guide to faults, incidents, retries, and operator recovery in
  Elsa 3.8.0.
---

# Incidents

In Elsa `release/3.8.0`, an **incident** is the runtime record created when an
activity or workflow faults. Incidents answer "what failed, where, and with
which exception details?" They are separate from retry policy and separate from
operator recovery actions.

Use this page to understand:

- what Elsa records when work faults
- how incident strategies affect the workflow sub-status
- where to inspect incidents in Studio and the API
- when to use resilience retries versus operational retries

For the configuration surface, see [Configuration](configuration.md). For the
built-in strategies, see [Strategies](strategies.md).

## What Elsa records when an activity faults

When an unhandled exception escapes an activity, Elsa's activity exception
middleware:

1. marks the activity execution as `Faulted`
2. captures an `ExceptionState`
3. appends an `ActivityIncident` to `WorkflowExecutionContext.Incidents`
4. resolves the configured incident strategy and invokes it

Each incident includes:

- `ActivityId`
- `ActivityNodeId`
- `ActivityType`
- `Message`
- `Exception`
- `Timestamp`

Elsa also writes execution-log entries such as `Started`, `Suspended`, and
`Faulted`, so incidents and journal entries complement each other instead of
duplicating each other.

## Workflow faults versus activity incidents

Activity-level incident strategies do not decide everything about workflow
failure.

- If an activity throws, the activity is already marked faulted and the
  incident is already recorded before the strategy runs.
- The strategy mainly decides whether the overall workflow transitions to
  `WorkflowSubStatus.Faulted` or keeps running.
- If an exception escapes the workflow pipeline itself, Elsa records an
  incident and transitions the workflow to `Faulted` directly.

In practice, that means `ContinueWithIncidentsStrategy` is not an automatic
retry mechanism. It only prevents the workflow from being faulted immediately
for activity-level incidents.

## Where incidents are stored and surfaced

In `release/3.8.0`, incidents are stored on `WorkflowState.Incidents` and flow
through the normal workflow instance APIs and Studio runtime views.

### Elsa Studio

Studio exposes incidents in two main places:

- the workflow instance list can filter by `Has Incidents`
- the workflow instance viewer shows an **Incidents** tab with the recorded
  message and exception payload

When resilience retries are enabled for an activity, Studio also surfaces a
**Retries** tab for activity executions that recorded retry attempts.

### Runtime APIs

Use these runtime surfaces during troubleshooting:

- `GET /workflow-instances/{id}` to inspect `workflowState.incidents`
- `GET /workflow-instances/{id}/journal`
- `POST /workflow-instances/{id}/journal` to filter journal events
- `GET /resilience/retries/{activityInstanceId}` when the resilience feature is
  enabled and retry attempts were recorded

Use incidents for the failing activity and exception details. Use the journal
for the sequence of events around the failure.

## Choosing the right recovery path

Elsa has three different recovery stories in `3.8.0`.

### 1. Change incident strategy

Use an incident strategy when you want to control whether an activity fault
should fault the whole workflow immediately or let the workflow continue with a
recorded incident.

This is a workflow-behavior decision, not a retry policy.

### 2. Use resilience retries for transient activity failures

Use resilience when an activity should retry automatically inside the same
execution attempt before it becomes an incident.

In `release/3.8.0`:

- the resilience feature evaluates an activity's `resilienceStrategy`
  configuration
- retry attempts are recorded only when a configured resilience pipeline
  actually retries
- the HTTP module registers `HttpResilienceStrategy` as an available strategy
  type

This is the right fit for transient downstream failures such as `429`,
`503`, timeouts, or short network interruptions.

### 3. Retry faulted workflow instances operationally

Use the Alterations retry endpoint when the workflow instance has already
faulted and you want to retry the faulted activities after investigation or a
fix.

`release/3.8.0` exposes:

```http
POST /alterations/workflows/retry
```

The request requires `workflowInstanceIds` and optionally `activityIds`. When
`activityIds` is omitted, Elsa retries all incident activity IDs from the
workflow state's incident list.

This endpoint belongs to the Alterations module and requires the
`run:alterations` permission. See
[Applying Alterations REST API](../../features/alterations/applying-alterations/rest-api.md).

## Practical troubleshooting flow

For most production incidents:

1. Open the workflow instance in Studio or retrieve the workflow instance from
   the API.
2. Inspect `workflowState.incidents` to identify the failing activity and
   exception.
3. Check the workflow journal for the surrounding `Faulted`, `Suspended`, and
   resume events.
4. If the failure was transient and resilience should have handled it, inspect
   the activity execution retries.
5. Decide whether to:
   - fix the root cause and retry the faulted activity with alterations
   - cancel the workflow instance
   - change the workflow's incident strategy or resilience configuration for
     future runs

## Related docs

- [Configuration](configuration.md)
- [Strategies](strategies.md)
- [Monitoring & Observability](../monitoring-observability.md)
- [Troubleshooting](../../guides/troubleshooting/README.md)
- [Applying Alterations REST API](../../features/alterations/applying-alterations/rest-api.md)
