# Tutorial

This example shows the minimum setup needed to publish and receive a typed
message through Elsa's MassTransit integration in `release/3.8.0`.

## 1. Define a message contract

Use a normal .NET message type. A `record` works well here because it is still a
class type, which means Elsa can generate both the trigger activity and the
publish activity.

{% code title="OrderCreated.cs" %}

```csharp
public record OrderCreated(string OrderId, string ProductId, int Quantity);
```

{% endcode %}

## 2. Register MassTransit and the message type

{% code title="Program.cs" %}

```csharp
using Elsa.Extensions;

services.AddElsa(elsa =>
{
    elsa.UseMassTransit(massTransit =>
    {
        massTransit.AddMessageType<OrderCreated>();
        massTransit.UseRabbitMq("amqp://guest:guest@localhost:5672");
    });
});
```

{% endcode %}

If you omit `UseRabbitMq(...)` or `UseAzureServiceBus(...)`, Elsa uses the
in-memory MassTransit transport instead.

## 3. Use the generated activities

After startup, Elsa exposes two activities derived from `OrderCreated`:

* `Order Created`
* `Publish Order Created`

### `Order Created`

Use this as a workflow trigger or as a blocking point later in the workflow.
When a MassTransit consumer receives an `OrderCreated` message, Elsa sends that
message through the stimulus pipeline and:

* starts a matching workflow if the activity is the workflow trigger
* resumes a waiting workflow instance if a bookmark already exists

The received message becomes the activity output, so downstream activities can
read `OrderId`, `ProductId`, and `Quantity`.

### `Publish Order Created`

Use this when the workflow needs to publish an `OrderCreated` event to the bus.
Internally, the generated activity resolves `IBus` and calls `Publish(...)`.

## 4. Example workflow shape

A common pattern is:

1. receive `Order Created`
2. map fields from the received message
3. call internal or external order-processing steps
4. optionally publish another integration event

This works well when Elsa is part of an event-driven workflow architecture and
the workflow itself is the business process handler.

## 5. Deployment notes

* Keep consumers enabled on nodes that should process broker messages.
* Set `massTransit.DisableConsumers = true` on nodes that should host the API
  but not consume workflow messages.
* Tune queue and broker throughput with `MassTransitOptions` and the transport
  configurator callbacks.

## 6. Troubleshooting checklist

* If the activities do not appear, verify `AddMessageType<OrderCreated>()` runs
  during startup.
* If messages publish but workflows do not start, verify consumers are enabled
  on the running node.
* If you use a real broker, verify the relevant RabbitMQ or Azure Service Bus
  transport package is installed and configured.
* If correlation matters, confirm the producer sets a MassTransit
  `CorrelationId` that matches your workflow design.
