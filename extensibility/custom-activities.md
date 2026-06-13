---
description: >-
  Release-backed guide to authoring custom Elsa activities in 3.8.0,
  including inputs, outputs, bookmarks, triggers, registration, Studio
  metadata, and testing.
---

# Custom Activities

This guide is based on the `release/3.8.0` source code in
`elsa-core`, `elsa-studio`, and `elsa-extensions`.

Use custom activities when you need to package domain logic, external
system integration, or reusable workflow building blocks behind the same
activity model that Elsa uses for its built-in activities.

## Choose the Right Base Type

In `release/3.8.0`, the usual starting points are:

| Base type | Use it when |
| --- | --- |
| `CodeActivity` | The activity does its work and completes immediately. |
| `CodeActivity<T>` | Same as `CodeActivity`, but the activity also returns a typed result. |
| `Activity` | You need manual control over completion, child scheduling, or bookmarks. |
| `Activity<T>` | Same as `Activity`, but the activity also returns a typed result. |
| `Trigger` / `Trigger<T>` | The activity can start workflows and also behaves like a bookmark-driven waiting activity at runtime. |

The important difference is completion behavior:

- `CodeActivity` adds Elsa's auto-complete behavior, so you do not call
  `CompleteActivityAsync` yourself.
- `Activity` does not auto-complete. If you neither complete the activity
  nor create bookmarks nor schedule child work, execution will stall.

## A Minimal Immediate Activity

This is the simplest authoring path and maps directly to how many
built-in activities are implemented.

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

[Activity("Acme", "Notifications", "Write a greeting to the console.")]
public class WriteGreeting : CodeActivity
{
    [Input(Description = "The name to greet.")]
    public Input<string> Name { get; set; } = new("world");

    protected override void Execute(ActivityExecutionContext context)
    {
        var name = context.Get(Name);
        Console.WriteLine($"Hello, {name}!");
    }
}
```

Notes:

- `Input<T>` lets the property accept literals, variables, and installed
  expression syntaxes.
- `context.Get(input)` is the standard way to read evaluated input values.
- `CodeActivity` completes automatically after `Execute` returns.

## Returning Data

If the activity produces a result, inherit from `CodeActivity<T>` or
`Activity<T>`. Elsa exposes the `Result` output automatically.

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

[Activity("Acme", "Text", "Build a greeting string.")]
public class BuildGreeting : CodeActivity<string>
{
    [Input(Description = "The name to greet.")]
    public Input<string> Name { get; set; } = new("world");

    protected override void Execute(ActivityExecutionContext context)
    {
        var name = context.Get(Name);
        context.Set(Result, $"Hello, {name}!");
    }
}
```

Use `Activity<T>` instead when you need both a result and manual control
over completion, outcomes, or bookmarks.

## Inputs and Studio Metadata

Elsa Studio builds activity editors from the descriptor metadata that
Elsa generates from your activity type.

The most important attribute is `InputAttribute`:

```csharp
[Input(
    DisplayName = "Recipient",
    Description = "Email address to notify.",
    Category = "Delivery",
    DefaultValue = "ops@example.com",
    UIHint = InputUIHints.SingleLine
)]
public Input<string> Recipient { get; set; } = default!;
```

`release/3.8.0` supports these `InputAttribute` capabilities that are
especially useful for custom activities:

- `UIHint` chooses the Studio editor component.
- `Options` provides static choices for dropdown, checklist, and radio
  editors.
- `DefaultSyntax` and `SupportedSyntaxes` shape the expression authoring
  experience.
- `AutoEvaluate = false` lets the activity evaluate the expression
  itself.
- `CanContainSecrets = true` marks sensitive inputs such as tokens or
  passwords.
- `UIHandler` and `UIHandlers` attach custom property UI handlers.

For the built-in hints and how Studio resolves them, see
[UI Hints](../studio/workflow-editor/ui-hints.md).

## Outputs

You can expose additional outputs by declaring `Output<T>` properties and
setting them from the execution context.

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

[Activity("Acme", "Math", "Divide two numbers.")]
public class DivideNumbers : CodeActivity
{
    [Input] public Input<decimal> Dividend { get; set; } = default!;
    [Input] public Input<decimal> Divisor { get; set; } = default!;

    [Output(Description = "The quotient.")]
    public Output<decimal> Quotient { get; set; } = default!;

    [Output(Description = "Whether the division succeeded.")]
    public Output<bool> Success { get; set; } = default!;

    protected override void Execute(ActivityExecutionContext context)
    {
        var dividend = context.Get(Dividend);
        var divisor = context.Get(Divisor);

        if (divisor == 0)
        {
            context.Set(Success, false);
            return;
        }

        context.Set(Quotient, dividend / divisor);
        context.Set(Success, true);
    }
}
```

## Resolving Services

Custom activities are workflow model objects, not the usual place for
constructor injection. In `release/3.8.0`, the normal pattern is to
resolve services from `ActivityExecutionContext`.

```csharp
protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
{
    var loggerFactory = context.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("Acme.CustomActivities");
    var message = context.Get(Message);
    logger.LogInformation("Dispatching message {Message}", message);
    await Task.CompletedTask;
}
```

That is the same pattern used by built-in activities such as `Log`,
which resolves runtime services from the execution context.

## Blocking Activities with Bookmarks

Use `Activity` or `Activity<T>` when the activity must pause and resume
later. In Elsa, that is usually implemented by creating a bookmark.

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

[Activity("Acme", "Approvals", "Wait for a review decision.")]
public class WaitForReview : Activity<string>
{
    [Input(Description = "The review request ID.")]
    public Input<string> RequestId { get; set; } = default!;

    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var requestId = context.Get(RequestId);
        context.CreateBookmark(new ReviewStimulus(requestId), ResumeAsync, includeActivityInstanceId: false);
        return ValueTask.CompletedTask;
    }

    private async ValueTask ResumeAsync(ActivityExecutionContext context)
    {
        var decision = context.GetWorkflowInput<string>("Decision");
        context.Set(Result, decision);
        await context.CompleteActivityAsync();
    }
}

public record ReviewStimulus(string RequestId);
```

This pattern is grounded in the same APIs used by built-in runtime
activities such as `Event`, `RunTask`, `DispatchWorkflow`, and
`ExecuteWorkflow`.

Use these rules when choosing bookmark behavior:

- Keep the default `includeActivityInstanceId: true` when the bookmark
  should resume one specific activity instance.
- Use `includeActivityInstanceId: false` when the stimulus identifies the
  logical wait point across instances, which is how runtime event-style
  activities are typically authored.
- Call `CompleteActivityAsync` from the resume callback when the activity
  should finish after resumption.

## Trigger Activities

`Trigger` and `Trigger<T>` are for activities that can start workflows in
addition to waiting inside a running workflow.

They have two responsibilities:

1. Provide trigger payloads for indexing through
   `GetTriggerPayload`, `GetTriggerPayloads`, or
   `GetTriggerPayloadsAsync`.
2. Create a bookmark in `Execute` or `ExecuteAsync` so the activity can
   also wait and resume at runtime.

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

[Activity("Acme", "CRM", "Start or resume workflows when a customer event arrives.")]
public class CustomerEventReceived : Trigger<string>
{
    [Input(Description = "The customer event name.")]
    public Input<string> EventName { get; set; } = default!;

    protected override object GetTriggerPayload(TriggerIndexingContext context)
    {
        var eventName = EventName.Get(context.ExpressionExecutionContext);
        return new CustomerEventStimulus(eventName);
    }

    protected override void Execute(ActivityExecutionContext context)
    {
        var eventName = context.Get(EventName);
        context.CreateBookmark(new CustomerEventStimulus(eventName), ResumeAsync, includeActivityInstanceId: false);
    }

    private async ValueTask ResumeAsync(ActivityExecutionContext context)
    {
        var payload = context.GetWorkflowInput<string>("Payload");
        context.Set(Result, payload);
        await context.CompleteActivityAsync();
    }
}

public record CustomerEventStimulus(string EventName);
```

The built-in `Event` activity in `Elsa.Workflows.Runtime` follows this
same split: trigger payload indexing on one side and bookmark-backed
runtime waiting on the other.

## Outcomes and Child Ports

There are two common ways to shape control flow from a custom activity.

### Flowchart outcomes

Use `FlowNodeAttribute` when the activity should emit named flowchart
outcomes.

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Attributes;

[Activity("Acme", "Validation", "Validate an order.")]
[FlowNode("Valid", "Invalid")]
public class ValidateOrder : Activity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var isValid = true;
        await context.CompleteActivityWithOutcomesAsync(isValid ? "Valid" : "Invalid");
    }
}
```

### Child activity ports

Use `[Port]` properties when the activity schedules other activities
itself. The built-in `If` activity in `Elsa.Workflows.Core` is the
reference pattern.

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

[Activity("Acme", "Branching", "Run one of two branches.")]
public class BranchByFlag : Activity<bool>
{
    [Input] public Input<bool> Flag { get; set; } = default!;

    [Port] public IActivity? WhenTrue { get; set; }
    [Port] public IActivity? WhenFalse { get; set; }

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var flag = context.Get(Flag);
        var nextActivity = flag ? WhenTrue : WhenFalse;

        context.Set(Result, flag);
        await context.ScheduleActivityAsync(nextActivity, OnChildCompletedAsync);
    }

    private static ValueTask OnChildCompletedAsync(ActivityCompletedContext context) =>
        context.TargetContext.CompleteActivityAsync();
}
```

## Registering Custom Activities

The activity type must be registered before Elsa can expose it through
its activity registry and Studio descriptors.

### In an application

This is the common host-level pattern and matches the sample server in
`elsa-core`:

```csharp
services.AddElsa(elsa => elsa.AddActivitiesFrom<Program>());
```

You can also register a single type:

```csharp
services.AddElsa(elsa => elsa.AddActivity<WriteGreeting>());
```

### In a module or feature

If you are authoring an Elsa module, use the module extensions:

```csharp
public override void Configure()
{
    Module.AddActivitiesFrom<MyFeature>();
}
```

For shell-feature-compatible service registration, `release/3.8.0` also
provides:

```csharp
services.AddActivitiesFrom<MyFeature>();
```

That path writes into `ManagementOptions` and is the service-collection
equivalent of workflow management feature registration.

## Activity Hosts

If your use case maps naturally to method-based activities, Elsa also
supports activity hosts.

The sample `Penguin` host in `elsa-core` is registered with:

```csharp
services.AddElsa(elsa => elsa.AddActivityHost<Penguin>());
```

Each public method becomes an activity, and method parameters become
inputs except for special parameters such as
`ActivityExecutionContext` and `CancellationToken`.

Use activity hosts when you want fast exposure of a service-like API as
activities. Use normal activity classes when you need precise control
over metadata, bookmarks, ports, or custom outputs.

## Studio Customization Hooks

For most custom activities, `ActivityAttribute`, `InputAttribute`, and
`OutputAttribute` are enough.

When they are not, `release/3.8.0` provides two deeper hooks:

- `IActivityDescriptorModifier` lets you reshape activity descriptors
  after registration.
- Property UI handlers let you provide dynamic options or editor
  metadata for specific inputs.

Use those when the Studio contract depends on runtime configuration or
when static attributes are not expressive enough.

Related docs:

- [UI Hints](../studio/workflow-editor/ui-hints.md)
- [Field Extensions](../studio/workflow-editor/field-extensions.md)
- [Custom UI Components](../guides/studio/custom-ui-components.md)

## Testing Custom Activities

`release/3.8.0` includes test helpers in `Elsa.Testing.Shared`.

For unit-style activity testing in isolation, use `ActivityTestFixture`:

```csharp
var activity = new BuildGreeting
{
    Name = new("Elsa")
};

var fixture = new ActivityTestFixture(activity);
var context = await fixture.ExecuteAsync();

var greeting = context.Get(activity.Result);
```

`ActivityTestFixture` registers core workflow services, evaluates input
properties, and builds an `ActivityExecutionContext` for the activity
under test. For broader workflow-level coverage, `Elsa.Testing.Shared`
also includes `WorkflowTestFixture` and `TestApplicationBuilder`.

## Practical Guidance

- Start with `CodeActivity` unless you need bookmarks, child scheduling,
  or manual completion.
- Use `Input<T>` for anything that should support variables or
  expressions.
- Use `CodeActivity<T>` or `Activity<T>` when the activity returns a
  single primary result.
- Resolve services from `ActivityExecutionContext`, not from custom
  constructors.
- Add Studio metadata deliberately. Good `Description`, `Category`, and
  `UIHint` values matter for usability.
- Keep runtime behavior and Studio behavior aligned. If the activity
  requires a custom editor, document and ship that editor alongside the
  backend type.
