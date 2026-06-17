# Tutorial

This example shows the full path for using a strongly typed message with Elsa:

1. Define a message contract.
2. Register the message type with the MassTransit feature.
3. Configure a transport.
4. Use the generated trigger and publish activities in workflows.

## 1. Define the message contract

{% code title="OrderCreated.cs" %}
```csharp
public record OrderCreated(string Id, string ProductId, int Quantity);
```
{% endcode %}

`OrderCreated` is a class, so Elsa will generate both a receive trigger and a publish activity for it.

## 2. Register the message type and configure MassTransit

{% code title="Program.cs" %}
```csharp
builder.Services.AddElsa(elsa => elsa
    .UseMassTransit(massTransit =>
    {
        massTransit.AddMessageType<OrderCreated>();

        // Use a broker for cross-process messaging.
        massTransit.UseRabbitMq(rabbitMqConnectionString);
    }));
```
{% endcode %}

With that configuration, Elsa adds two activities:

* Order Created
* Publish Order Created

If you omit `UseRabbitMq(...)` or another transport configuration method, Elsa falls back to MassTransit's in-memory transport.

## 3. Start a workflow when the message arrives

Use the generated `Order Created` trigger activity at the start of a workflow and mark it as a start trigger in Studio.

In code, the equivalent looks like this:

{% code title="OrderCreatedWorkflow.cs" %}
```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;

public class OrderCreatedWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        var order = builder.WithVariable<OrderCreated>();

        builder.Root = new Sequence
        {
            Activities =
            {
                new Elsa.ServiceBus.MassTransit.Activities.MessageReceived
                {
                    CanStartWorkflow = true,
                    MessageType = typeof(OrderCreated),
                    Result = new Output<OrderCreated>(order)
                },
                new WriteLine(context => $"Received order {order.Get(context)!.Id}")
            }
        };
    }
}
```
{% endcode %}

When a MassTransit consumer receives `OrderCreated`, Elsa resumes or starts matching workflows by sending a stimulus that contains the message payload.

## 4. Publish the message from a workflow

Use the generated `Publish Order Created` activity when a workflow needs to emit the message:

{% code title="PublishOrderWorkflow.cs" %}
```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;
using Elsa.ServiceBus.MassTransit.Activities;

public class PublishOrderWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new PublishMessage
        {
            MessageType = typeof(OrderCreated),
            Message = new Input<object>(_ => new OrderCreated("1001", "SKU-1", 2))
        };
    }
}
```
{% endcode %}

In Studio, the equivalent activity is `Publish Order Created`.

## When to Use This Pattern

Use this pattern when:

- Your application already uses MassTransit contracts between services.
- You want workflows to react to or emit strongly typed domain messages.
- You want the same broker to work across processes or nodes.

Use a transport-specific module instead when you need broker-specific concepts such as Azure Service Bus queues, topics, or subscriptions.
