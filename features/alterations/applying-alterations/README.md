# Applying Alterations

Use immediate execution when you already know which workflow instance IDs you want to alter and you want the results in the current request.

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
