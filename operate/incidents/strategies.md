---
description: Built-in incident strategies and their real 3.8.0 behavior.
---

# Strategies

An incident strategy implements `IIncidentStrategy`:

```csharp
public interface IIncidentStrategy
{
    void HandleIncident(ActivityExecutionContext context);
}
```

In `release/3.8.0`, Elsa registers two built-in strategies.

## `FaultStrategy`

`FaultStrategy` transitions the workflow to `WorkflowSubStatus.Faulted` when
the workflow can still move to that state.

Use it when:

- a fault in one activity should stop the overall workflow
- operators should investigate before any more work is scheduled
- you want the workflow instance to present as faulted immediately in runtime
  and Studio

This is Elsa's effective default when neither the workflow nor the host
configured another strategy.

## `ContinueWithIncidentsStrategy`

`ContinueWithIncidentsStrategy` is intentionally a no-op.

That does **not** mean "ignore the error". By the time the strategy runs:

- the activity has already been marked faulted
- the incident has already been appended to `WorkflowExecutionContext.Incidents`
- the exception has already been captured on the activity execution context

The strategy simply leaves the workflow sub-status unchanged so the workflow
can continue if the surrounding workflow structure allows it.

Use it when:

- you want to collect incident data without faulting the whole workflow
- later branches, compensation, or manual review can still provide value
- the failing activity is not always fatal to the business process

Do not use it as a substitute for retry logic. If the real requirement is
"retry transient failures before faulting", configure a resilience strategy
instead.

## Custom strategies

You can implement your own `IIncidentStrategy` when you need custom behavior.

In `3.8.0`, a custom strategy becomes visible to Studio automatically when:

1. it is registered as `IIncidentStrategy`
2. the descriptors endpoint can resolve it from DI

If you add `DisplayName`, `Display`, or `Description` attributes, Studio uses
those values when it renders the incident strategy picker.
