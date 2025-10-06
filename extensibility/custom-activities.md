---
description: This topic covers extending Elsa with your own custom activities.
---

# Custom Activities

Elsa includes many ready-made activities for various tasks, from simple ones like "Set Variable" to complex ones like "Send Email." These tools help build and

To unlock Elsa's full potential, create activities specific to your needs. Custom activities designed for your domain can improve workflow creation and management, making it more efficient and personalised.

Learn how to create custom activities to enhance Elsa's features, with easy steps to integrate these solutions into your domain.

## Creating Custom Activities <a href="#creating-custom-activities" id="creating-custom-activities"></a>

To create a custom activity, start by defining a new class that implements the `IActivity` interface or inherits from a base class that does. Examples include `Activity` or `CodeActivity`.

A simple example of a custom activity is one that outputs a message to the console:

```csharp
using Elsa.Extensions;
using Elsa.Workflows;

public class PrintMessage : Activity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        Console.WriteLine("Hello world!");
        await context.CompleteActivityAsync();
    }
}
```

Let's dissect the sample `PrintMessage` activity.

## **Essential Components**﻿

* The `PrintMessage` class inherits from `Elsa.Workflows.Activity`, which implements the `IActivity` interface.
* The core of an activity is the `ExecuteAsync` method. It defines the action the activity performs when executed within a workflow.
* The `ActivityExecutionContext` parameter, named `context` here, provides access to the workflow's execution context. It's a gateway to the workflow's environment, offering methods to interact with the workflow's execution flow, data, and more.

## **Key Operations**﻿

* `ExecuteAsync` is where the main action happens. For example, `Console.WriteLine("Hello world!");` prints a message to the console. In real-world cases, this section would handle core tasks like data processing or connecting to other systems.
* Using `await context.CompleteActivityAsync();` means the activity is done. Completing an activity is key to moving the workflow.

## Activity vs CodeActivity <a href="#activity-vs-code-activity" id="activity-vs-code-activity"></a>

If your custom activity has a simple workflow and ends right after finishing its task, using `CodeActivity` makes things easier. This base class automatically marks the activity as complete once it's done, so you don't need to write any additional completion code.

Let's look at how to redo the `PrintMessage` activity using `CodeActivity` as the base. This highlights that manual completion isn't needed:

```csharp
using Elsa.Workflows;

public class PrintMessage : CodeActivity
{
    protected override void Execute(ActivityExecutionContext context)
    {
        Console.WriteLine("Hello world!");
    }
}
```

## Metadata <a href="#activity-metadata" id="activity-metadata"></a>

The `ActivityAttribute` can be used to give user-friendly details to your custom activity, such as its display name and description. Here's an example using `ActivityAttribute` with the `PrintMessage` activity. This is useful in tools like Elsa Studio.

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Attributes;

[Activity("MyCompany", "MyPlatform/MyFunctions", "Print a message to the console")]
public class PrintMessage : CodeActivity
{
    protected override void Execute(ActivityExecutionContext context)
    {
        Console.WriteLine("Hello world!");
    }
}
```

In this example, the activity is annotated with a namespace of `"MyCompany"` , a category of `"MyPlatform/MyFunctions"` and a description for clarity.

The Treeview activity picker in Elsa Studio supports nested categories within the tree. Simply use the `/` character to separate categories. More detials can be found [here](../studio/design/activity-pickers-3.7-preview.md).

## Composition <a href="#composite-activities" id="composite-activities"></a>

Composite activities merge several tasks into one, enabling complex processes with conditions and branches. This is shown in the `If` activity example below:

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Contracts;
using Elsa.Workflows.Models;

public class If : Activity
{
    public Input<bool> Condition { get; set; } = default!;
    public IActivity? Then { get; set; }
    public IActivity? Else { get; set; }

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var result = context.Get(Condition);
        var nextActivity = result ? Then : Else;
        await context.ScheduleActivityAsync(nextActivity, OnChildCompleted);
    }

    private async ValueTask OnChildCompleted(ActivityCompletedContext context)
    {
        await context.CompleteActivityAsync();
    }
}
```

This example illustrates how a composite activity can evaluate a condition and then proceed with one of two possible paths, effectively modeling an "if-else" statement within a workflow.

{% hint style="info" %}
**Programmatic Workflows and Dynamic Activities**

There is an open issue reported on GitHub related to the Elsa Workflows project: Dynamically provided activities are not yet supported within programmatic workflows. You can view the issue [here](https://github.com/elsa-workflows/elsa-core/issues/5162).
{% endhint %}

The following example shows how to use the `If` activity:

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Contracts;

public class IfWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new If
        {
            Condition = new(context => DateTime.Now.IsDaylightSavingTime()),
            Then = new WriteLine("Welcome to the light side!"),
            Else = new WriteLine("Welcome to the dark side!")
        };
    }
}
```

## Outcomes <a href="#activity-outcomes" id="activity-outcomes"></a>

Setting custom outcomes for activities gives precise control over what happens based on certain conditions. You can declare potential outcomes by using the `FlowNodeAttribute` on the activity class. For example:

```
[FlowNode("Pass", "Fail")]
```

This attribute specifies two distinct outcomes for the activity: "Pass" and "Fail." These outcomes dictate the possible execution paths following the activity's completion. To trigger a specific outcome during runtime, utilize the `CompleteActivityWithOutcomesAsync` method within your activity's execution logic.

Consider the following sample activity:

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Attributes;

[FlowNode("Pass", "Fail")]
public class PerformTask : Activity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.CompleteActivityWithOutcomesAsync("Pass");
    }
}
```

In this example, the defined outcomes guide the flow of execution within flowcharts, enabling conditional progression based on the result of the activity. This mechanism enhances the flexibility and decision-making capabilities within workflows, allowing for dynamic responses to activity results.

## Input <a href="#activity-input" id="activity-input"></a>

Similar to C# methods accepting arguments and returning results, activities can accept input and produce output.

In essence, an activity functions within a workflow much like a statement within a program, serving as a fundamental component that constructs the logic of the workflow.

To define inputs on an activity, simply expose public properties within your activity class. For instance, the `PrintMessage` activity below is updated to receive a message as input:

```csharp
using Elsa.Workflows;

public class PrintMessage : CodeActivity
{
    public string Message { get; set; }

    protected override void Execute(ActivityExecutionContext context)
    {
        Console.WriteLine(Message);
    }
}
```

### **Metadata**﻿

Use the `InputAttribute` to add input details to your custom activity, making it easy to include display names and descriptions. This feature improves the clarity of activity inputs, especially in tools like Elsa Studio.

Here is an instance where the `InputAttribute` is applied to the `Message` property:

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Attributes;

[Activity("MyCompany", "Print Message")]
public class PrintMessage : CodeActivity
{
    [Input(Description = "The message to print.")]
    public string Message { get; set; }

    protected override void Execute(ActivityExecutionContext context)
    {
        Console.WriteLine(Message);
    }
}
```

## **Expressions**﻿

Often, you'll want to dynamically set the activity's input through expressions, instead of fixed, literal values.

For instance, you might want the message to be printed to originate from a workflow variable, rather than being hardcoded into the activity's input.

To enable this, you should encapsulate the input property type within `Input<T>`.

As an illustration, the `PrintMessage` activity below is modified to support expressions for its `Message` input property:

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Models;

public class PrintMessage : CodeActivity
{
    public Input<string> Message { get; set; } = default!;

    protected override void Execute(ActivityExecutionContext context)
    {
        var message = Message.Get(context);
        Console.WriteLine(message);
    }
}
```

Note that encapsulating an input property with `Input<T>` changes the manner in which its value is accessed:

```csharp
var message = Message.Get(context);
```

The example below demonstrates specifying an expression for the `Message` property in a workflow created using the workflow builder API:

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Contracts;

public class PrintMessageWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        var message = builder.WithVariable<string>("Message", "Hello, World!");

        builder.Root = new PrintMessage
        {
            Message = new(context => $"The message is: {message.Get(context)}")
        };
    }
}
```

In this scenario, we use a simple C# delegate expression to dynamically determine the message to print at runtime.

Alternatively, other installed expression provider syntaxes, such as JavaScript, can be used:

```csharp
using Elsa.JavaScript.Models;
using Elsa.Workflows;
using Elsa.Workflows.Contracts;

public class PrintMessageWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        var message = builder.WithVariable<string>("Message", "Hello, World!");

        builder.Root = new PrintMessage
        {
            Message = new(JavaScriptExpression.Create("`The message is: ${variables.message}`"))
        };
    }
}
```

## Output <a href="#activity-output" id="activity-output"></a>

Activities can generate outputs. To do so, implement properties typed as `Output<T>`.

For instance, the activity below generates a random number between 0 and 100:

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Models;

public class GenerateRandomNumber : CodeActivity
{
    public Output<decimal> Result { get; set; } = default!;

    protected override void Execute(ActivityExecutionContext context)
    {
        var randomNumber = Random.Shared.Next(1, 100);
        Result.Set(context, randomNumber);
    }
}
```

### **Metadata**﻿

Like input properties, output properties can be enriched with metadata.

This is done using the `OutputAttribute`.

An example of the `OutputAttribute` applied to the `Result` property follows:

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

public class GenerateRandomNumber : CodeActivity
{
    [Output(Description = "The generated random number.")]
    public Output<decimal> Result { get; set; } = default!;

    protected override void Execute(ActivityExecutionContext context)
    {
        var randomNumber = Random.Shared.Next(1, 100);
        Result.Set(context, randomNumber);
    }
}
```

Workflow users have two approaches to using activity output:

1. Capturing the output via a workflow variable.
2. Direct access to the output from the workflow engine's memory register.

Let's examine both methods in detail.

### **Capture via Variable**﻿

Here's how to capture the output using a workflow variable:

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Contracts;

public class GenerateRandomNumberWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        var randomNumber = builder.WithVariable("RandomNumber", 0m);

        builder.Root = new Sequence
        {
            Activities =
            {
                new GenerateRandomNumber
                {
                    Result = new(randomNumber)
                },
                new PrintMessage
                {
                    Message = new(context => $"The random number is: {randomNumber.Get(context)}")
                }
            }
        };
    }
}
```

In this workflow, the steps include:

* Executing the `GenerateRandomNumber` activity
* Capturing the activity's output in a variable named `RandomNumber`
* Displaying a message with the value of the `RandomNumber` variable

### **Direct Access**﻿

And here's how to access to the output from the `GenerateRandomNumber` activity directly:

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Contracts;

public class GenerateRandomNumberWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new GenerateRandomNumber
                {
                    Name = "GenerateRandomNumber1"
                },
                new PrintMessage
                {
                    Message = new(context => $"The random number is: {context.GetOutput("GenerateRandomNumber1", "Result")}")
                }
            }
        };
    }
}
```

This approach requires naming the activity from which the output will be accessed, as well as the output property's name.

An alternative, type-safe method is to declare the activity as a local variable initially. This allows for referencing both the activity and its output, as demonstrated below:

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Contracts;

public class GenerateRandomNumberWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        var generateRandomNumber = new GenerateRandomNumber();

        builder.Root = new Sequence
        {
            Activities =
            {
                generateRandomNumber,
                new PrintMessage
                {
                    Message = new(context => $"The random number is: {generateRandomNumber.GetOutput<GenerateRandomNumber, decimal>(context, x => x.Result)}")
                }
            }
        };
    }
}
```

While both approaches are effective for managing activity output, it's crucial to note a key distinction: activity output is transient, existing only for the duration of the current execution burst.

To access the output value beyond these bursts, capturing the output in a variable is recommended, as variables are inherently persistent.

## Dependency Injection <a href="#activity-dependency-injection" id="activity-dependency-injection"></a>

To use services in your activities, you can get them using the `context` in the activity's `ExecuteAsync` method. This allows for easy use of dependency injection in workflows.

Here's a simple example of how to use a service in an activity:

```csharp
using Elsa.Extensions;
using Elsa.Workflows;

public class GetWeatherForecast : CodeActivity<WeatherForecast>
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var apiClient = context.GetRequiredService<IWeatherApi>();
        var forecast = await apiClient.GetWeatherAsync();
        context.SetResult(forecast);
    }
}
```

{% hint style="info" %}
Choosing Service Location over Constructor Injection

Elsa prefers to use service location over constructor dependency injection to make it easier to create activity instances in workflow definitions. Using constructor-based DI would make it harder to build and change workflow graphs programmatically.
{% endhint %}

## Blocking Activities <a href="#blocking-activities" id="blocking-activities"></a>

Blocking activities represent an important concept in workflow design, enabling a workflow to pause its execution until a specified external event occurs. Instead of completing immediately, these activities generate a bookmark—a placeholder of sorts—that allows the workflow to resume from the same point once the required conditions are met. This mechanism is particularly useful for orchestrating asynchronous operations or waiting for external inputs. Examples of blocking activities include the `Event` and `Delay` activities.

Here is an example of a blocking activity that creates a bookmark to pause its execution, awaiting an external trigger to proceed:

```csharp
using Elsa.Workflows;

public class MyEvent : Activity
{
    protected override void Execute(ActivityExecutionContext context)
    {
        context.CreateBookmark("MyEvent");
    }
}
```

Here's how to add the `MyEvent` activity to a workflow:

```csharp
public class MyEventWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
       {
           Activities =
           {
               new WriteLine("Starting workflow..."),
               new MyEvent(), // This will block further execution until the MyEvent's bookmark is resumed.
               new WriteLine("Event occurred!")
           }
       };
    }
}
```

When the workflow starts, it will run until it reaches the `MyEvent` activity. At this point, the workflow will pause, create a bookmark, and wait for an external signal to continue. While waiting, there is nothing else for the workflow engine to do, so it will save the workflow instance and remove it from memory.

To pick up a workflow from a bookmark, the system needs certain information:

* The type of activity that initiated the bookmark
* The bookmark payload, which was generated by the activity

How to resume a workflow Using `IStimulusSender`:

Follow these steps to restart a workflow using the bookmark payload as stimulus from the blocking activity, by using `IStimulusSender`:

```csharp
var stimulus = "MyEvent";
var activityTypeName = ActivityTypeNameHelper.GenerateTypeName<MyEvent>();
await _stimulusSender.SendAsync(activityTypeName, stimulus);
```

This method can be easily added to an API controller to resume workflows when external events happen.

## Triggers <a href="#activity-triggers" id="activity-triggers"></a>

Triggers serve as specialised activities designed to initiate workflows in reaction to specific external events, such as HTTP requests or messages from a message queue. This capability allows workflows to dynamically respond to outside stimuli, making them highly versatile in various automated processes.

To illustrate, the `MyEvent` activity, previously discussed as a blocking activity, can also be adapted to function as a trigger:

```csharp
using Elsa.Extensions;
using Elsa.Workflows;

namespace Elsa.Server.Web.Activities;

public class MyEvent : Trigger
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        if (context.IsTriggerOfWorkflow())
        {
            await context.CompleteActivityAsync();
            return;
        }

        context.CreateBookmark("MyEvent");
    }

    protected override object GetTriggerPayload(TriggerIndexingContext context)
    {
        return "MyEvent";
    }
}
```

By using the `ITrigger` interface or inheriting from `Trigger`, an activity becomes a trigger. This allows services like `IWorkflowRuntime` to start workflows based on certain events, which is key for creating reactive and event-driven workflows.

Understanding trigger activities in workflows is key. These activities check if they directly started the current execution. If they did, they finish immediately instead of pausing the workflow for an external trigger. This prevents the workflow from stopping at the start and ensures it only pauses when needed later.

By using this mechanism, workflows start automatically when events like the `MyEvent` activity happen. Here's an example of a workflow

```csharp
public class MyEventWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
       {
           Activities =
           {
               new MyEvent
               {
                    CanStartWorkflow = true // Enable this activity to start this workflow when triggered.
               },
               new WriteLine("Event occurred!")
           }
       };
    }
}
```

Set the `CanStartWorkflow` property to `true` on the trigger activity. This allows the activity to start the workflow, making it essential for activating workflow triggers.

To programmatically trigger workflows using the `MyEvent` trigger, apply the same code used for resuming a bookmark with `IStimulusSender`.

```csharp
var stimulus = "MyEvent";
var activityTypeName = ActivityTypeNameHelper.GenerateTypeName<MyEvent>();
await _stimulusSender.SendAsync(activityTypeName, stimulus);
```

The `IStimulusSender` service is a helpful service for delivering stimuli to workflows that will cause workflows to be started and suspended workflows to be resumed. It allows developers to easily create workflows that adapt to different events, improving how interactive and responsive their applications are.

## Registering Activities <a href="#registering-activities" id="registering-activities"></a>

Register activities in the Activity Registry before using them in workflows.

The easiest way to register activities is through your application's startup code. For example, the Program.cs file below illustrates how to register the `PrintMessage` activity:

```csharp
services.AddElsa(elsa => elsa
    .AddActivity<PrintMessage>()
);
```

Alternatively, to register all activities from a specific assembly, the `AddActivitiesFrom<TMarker>` extension method can be used:

```csharp
services.AddElsa(elsa => elsa
    .AddActivitiesFrom<Program>()
);
```

This approach registers all activities discovered within the assembly containing the specified type. The marker type can be any class within the assembly, not necessarily an activity.

## Activity Providers <a href="#activity-providers" id="activity-providers"></a>

Activities can be provided to the system in various ways. The type of an activity is fundamentally represented by an **Activity Descriptor**.

Activity Descriptors are provided by **Activity Providers.** Out of the box, Elsa ships with one such implementation being the `TypedActivityProvider`. This provider generates activity descriptors based on the .NET types implementing the `IActivity` interface.

This abstraction layer enables sophisticated scenarios where activity descriptors' sources can be dynamic.

Consider a scenario where you generate activities from an Open API specification. Each resource operation is automatically represented as an activity, rather than using the `SendHttpRequest` activity directly.

To develop custom activity providers, follow these steps:

1. Implement the `Elsa.Workflows.Contracts.IActivityProvider` .
2. Register your custom activity provider with the system.

Below is a sample implementation of an activity provider that dynamically produces activities based on a list of fruits.

```csharp
using Elsa.Extensions;
using Elsa.Workflows.Contracts;
using Elsa.Workflows.Models;

namespace Elsa.Server.Web.Activities;

public class FruitActivityProvider(IActivityFactory activityFactory) : IActivityProvider
{
    public ValueTask<IEnumerable<ActivityDescriptor>> GetDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        var fruits = new[]
        {
            "Apples", "Bananas", "Cherries",
        };

        var activities = fruits.Select(x =>
        {
            var fullTypeName = $"Demo.Buy{x}";
            return new ActivityDescriptor
            {
                TypeName = fullTypeName,
                Name = $"Buy{x}",
                Namespace = "Demo",
                DisplayName = $"Buy {x}",
                Category = "Fruits",
                Description = $"Buy {x} from the store.",
                Constructor = context =>
                {
                    var activity = activityFactory.Create<PrintMessage>(context);

                    activity.Message = new($"Buying {x}...");
                    activity.Type = fullTypeName;
                    return activity;
                }
            };
        }).ToList();

        return new(activities);
    }
}
```

This provider leverages a simple array of fruit names as its source, generating an activity descriptor for each fruit, symbolising a "Buy (fruit)" activity.

To register this provider, utilise the `AddActivityProvider<T>` extension method:

```
services.AddActivityProvider<FruitActivityProvider>();
```

{% hint style="info" %}
**Programmatic Workflows and Dynamic Activities**

Currently, dynamically provided activities cannot be used within programmatic workflows.

An open issue exists for this functionality: [https://github.com/elsa-workflows/elsa-core/issues/5162](https://github.com/elsa-workflows/elsa-core/issues/5162)
{% endhint %}

## Summary <a href="#summary" id="summary"></a>

In this topic, we explored the creation, registration, and utilisation of custom activities. These are crucial in workflow development as they allow for the inclusion of domain-specific actions within a workflow.
