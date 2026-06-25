# MassTransit

Use the MassTransit extension packages when your workflows need to react to
brokered messages or publish messages to other services.

In `release/3.8.0`, the integration lives in the Elsa extensions repository
under `Elsa.ServiceBus.MassTransit`. Once enabled, Elsa can:

* generate workflow activities from message types you register
* connect those activities to MassTransit consumers and `IBus.Publish(...)`
* optionally use MassTransit as the transport for workflow dispatch features in
  the runtime and management modules

## What the integration actually adds

Registering a message type with `AddMessageType<T>()` makes Elsa generate:

* a trigger activity based on `MessageReceived`
* a publish activity based on `PublishMessage`

For a message type such as `OrderCreated`, Studio exposes activities similar to:

* `Order Created`
* `Publish Order Created`

The generated trigger waits for a MassTransit consumer to receive a matching
message and then starts or resumes workflows whose bookmarks match that message
type.

The generated publish activity resolves `IBus` from DI and calls
`Publish(...)` with the configured message payload.

## Package and transport setup

Use the MassTransit extension package plus a transport package when you want a
real broker:

* `Elsa.ServiceBus.MassTransit`
* `Elsa.ServiceBus.MassTransit.RabbitMq` for RabbitMQ
* `Elsa.ServiceBus.MassTransit.AzureServiceBus` for Azure Service Bus

If you enable `UseMassTransit(...)` without configuring a transport, Elsa falls
back to MassTransit's in-memory transport.

## Basic registration pattern

{% code title="Program.cs" %}

```csharp
using Elsa.Extensions;

services.AddElsa(elsa =>
{
    elsa.UseMassTransit(massTransit =>
    {
        massTransit.AddMessageType<OrderCreated>();
    });
});
```

{% endcode %}

This is enough to:

* register a workflow-facing consumer for `OrderCreated`
* expose generated activities in Studio and the activity registry
* use the in-memory transport unless another transport is configured

## Configuring a broker transport

Use the transport-specific extension inside the same `UseMassTransit(...)`
callback.

### RabbitMQ

{% code title="Program.cs" %}

```csharp
using Elsa.Extensions;

services.AddElsa(elsa =>
{
    elsa.UseMassTransit(massTransit =>
    {
        massTransit.AddMessageType<OrderCreated>();
        massTransit.UseRabbitMq("amqp://guest:guest@localhost:5672", rabbit =>
        {
            rabbit.ConfigureTransportBus = (context, bus) =>
            {
                bus.PrefetchCount = 50;
                bus.ConcurrentMessageLimit = 32;
            };
        });
    });
});
```

{% endcode %}

### Azure Service Bus

{% code title="Program.cs" %}

```csharp
using Elsa.Extensions;

services.AddElsa(elsa =>
{
    elsa.UseMassTransit(massTransit =>
    {
        massTransit.AddMessageType<OrderCreated>();
        massTransit.UseAzureServiceBus("<connection-string>", bus =>
        {
            bus.EnableAutomatedSubscriptionCleanup = true;
            bus.SubscriptionCleanupOptions = options =>
                options.Interval = System.TimeSpan.FromMinutes(5);
        });
    });
});
```

{% endcode %}

Use RabbitMQ when your application architecture already standardizes on AMQP and
queue-based routing. Use Azure Service Bus when Elsa participates in Azure
native messaging and you want broker-managed topics and subscriptions.

## How message reception works

When a MassTransit consumer receives a registered message type, Elsa's
`WorkflowMessageConsumer<T>` sends a stimulus whose payload identifies that .NET
type. The `MessageReceived` trigger:

* starts a workflow when used as the workflow trigger
* resumes an existing workflow instance when the activity already created a
  bookmark
* exposes the received message as the activity output

MassTransit correlation IDs are passed through to the stimulus metadata, so you
can combine broker correlation with Elsa workflow correlation when your workflow
design requires it.

## Node topology and operational notes

By default, the MassTransit feature registers consumers for the generated
message activities.

Set `massTransit.DisableConsumers = true` on nodes that should host the Elsa API
or Studio backend but should not consume bus messages.

MassTransit messaging for generated activities is separate from MassTransit as a
workflow-dispatch transport. If you also enable:

* `management.UseMassTransitDispatcher()`
* `runtime.UseMassTransitDispatcher()`

then Elsa uses MassTransit to dispatch workflow-management and workflow-runtime
messages as well.

## When to use this integration

Use MassTransit-backed workflow activities when:

* workflows need to start from or wait on brokered integration events
* workflows need to publish typed integration events to other services
* your team wants message contracts to appear directly as activities in Studio

Use direct HTTP, scheduler, or native workflow-dispatch APIs instead when you
do not need brokered messaging semantics.

## Next step

Continue with the [MassTransit tutorial](tutorial.md) for a minimal
publish-and-receive setup.
