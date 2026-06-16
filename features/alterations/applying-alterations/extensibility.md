# Extensibility

Elsa Workflows supports custom alteration types, allowing developers to define their own types and utilize them as alterations.

To define a custom alteration type, implement the `IAlteration` interface.

```csharp
public interface IAlteration
{
}
```

Next, implement an **alteration handler** that handles the alteration type.

```csharp
public interface IAlterationHandler
{
    bool CanHandle(IAlteration alteration);
    ValueTask HandleAsync(AlterationContext context);
}
```

Or, derive from the `AlterationHandlerBase<T>` base class to simplify the implementation.

Finally, register the alteration handler with the service collection.

```csharp
services.AddElsa(elsa => 
{
    elsa.UseAlterations(alterations => 
    {
        alterations.AddAlteration<MyAlteration, MyAlterationHandler>();
    })
});
```

## Example <a href="#example" id="example"></a>

The following example demonstrates how to define a custom alteration type and handler.

```csharp
public class MyAlteration : IAlteration
{
    public string Message { get; set; } = default!;
}

public class MyAlterationHandler : AlterationHandlerBase<MyAlteration>
{
    protected override ValueTask HandleAsync(AlterationContext context, MyAlteration alteration)
    {
        context.WorkflowExecutionContext.Output["Message"] = alteration.Message;
        context.Succeed();
        return ValueTask.CompletedTask;
    }
}
```

## Handler guidance

An alteration handler runs inside Elsa's alteration pipeline against an existing workflow instance. In practice, that means your handler should:

* validate that the target activity, variable, or workflow state exists
* call `context.Succeed()` when the alteration completed successfully
* call `context.Fail(...)` when the alteration cannot be applied safely
* schedule additional work only when the workflow should continue after the change
