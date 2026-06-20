# Applying Alterations

Use immediate execution when you already know which workflow instance IDs you want to alter and you want the results in the current request.

This execution mode is synchronous from the caller's point of view: Elsa runs the requested alterations against the specified workflow instances and returns one result per instance.

At the service level, immediate execution uses `IAlterationRunner`:

```csharp
var alterations = new List<IAlteration>
{
    new ModifyVariable
    {
        VariableId = "83fde420b5794bc39a0a7db725405511",
        Value = "MyValue"
    }
};

var workflowInstanceIds = new[] { "26cf02e60d4a4be7b99a8588b7ac3bb9" };
var runner = serviceProvider.GetRequiredService<IAlterationRunner>();
var results = await runner.RunAsync(workflowInstanceIds, alterations, cancellationToken);
```

This updates the specified workflow instances synchronously and returns one `RunAlterationsResult` per instance.

## What the result tells you

Each `RunAlterationsResult` contains:

* `workflowInstanceId`
* `log`
* `workflowHasScheduledWork`
* `isSuccessful`

Use the log entries to see which alteration succeeded or failed for each targeted instance.

## Resuming scheduled work

When you use `IAlterationRunner` directly, successful alterations may leave workflow instances with scheduled work waiting to be dispatched. In that case, resume them with `IAlteredWorkflowDispatcher`:

```csharp
var dispatcher = serviceProvider.GetRequiredService<IAlteredWorkflowDispatcher>();
await dispatcher.DispatchAsync(results, cancellationToken);
```

The dispatcher only re-dispatches successful results that actually contain scheduled work.

## Service API versus HTTP API

This distinction matters:

* `IAlterationRunner` only runs the alterations and returns the results.
* `POST /alterations/run` runs the alterations and then automatically dispatches successful instances that have scheduled work.

If you are using the HTTP API, you do not need a separate resume step after a successful `/alterations/run` request.

## When to use immediate execution

Use immediate execution when:

* an operator already has one or more concrete workflow instance IDs
* you want the alteration log in the same request
* you do not need durable plan and job records

Use alteration plans instead when Elsa should discover the target instances for you or when you need persistent plan and job tracking.

## Retrying faulted workflow instances

`release/3.8.0` also ships a bulk retry endpoint for faulted instances: `POST /alterations/workflows/retry`.

That endpoint builds `ScheduleActivity` alterations for the specified activity IDs, or for all incident activity IDs when you omit `activityIds`, then dispatches the updated workflow instances.

Use the retry endpoint when your operational goal is specifically "retry the faulted work" rather than "apply an arbitrary alteration set".
