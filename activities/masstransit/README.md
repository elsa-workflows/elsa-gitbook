# MassTransit

MassTransit integration in Elsa lets you expose selected .NET message types as workflow activities. In `release/3.8.0`, Elsa generates these activities dynamically from the message types you register with the MassTransit feature.

Use this integration when you want workflows to publish application messages or wait for messages that arrive through a MassTransit bus.

## What This Feature Does

After you call `UseMassTransit(...)` and register one or more message types with `AddMessageType<T>()`, Elsa adds dynamic activities for those types:

- A trigger activity named after the message type, implemented by `MessageReceived`
- For class types only, an action activity named `Publish {MessageType}`, implemented by `PublishMessage`

For example, registering `OrderCreated` adds:

- `Order Created`
- `Publish Order Created`

This behavior comes from `MassTransitActivityTypeProvider`, which creates a `MessageReceived` descriptor for every registered message type and a `PublishMessage` descriptor only when the type is a class.

## Registration and Transport

Install the base package for the feature and one transport package when you need broker-backed messaging:

- `Elsa.ServiceBus.MassTransit`
- `Elsa.ServiceBus.MassTransit.RabbitMq` or `Elsa.ServiceBus.MassTransit.AzureServiceBus`

At minimum, enable the feature and register the message types you want to expose:

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseMassTransit(mt =>
    {
        mt.AddMessageType<OrderCreated>();
    }));
```

If you do not configure a transport, Elsa uses MassTransit's in-memory transport. That is useful for local development and single-process setups, but it does not provide inter-process messaging.

For multi-node or broker-backed messaging, configure a transport explicitly:

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseMassTransit(mt =>
    {
        mt.AddMessageType<OrderCreated>();
        mt.UseRabbitMq(rabbitMqConnectionString);
    }));
```

`release/3.8.0` also provides `UseAzureServiceBus(...)` in the `Elsa.ServiceBus.MassTransit.AzureServiceBus` package.

## How Receiving Works

When MassTransit receives a registered message type, Elsa's generated `WorkflowMessageConsumer<T>` sends a stimulus whose activity type name matches the registered message type and whose workflow input contains the message under the `Message` key.

The generated `MessageReceived` activity then:

- Matches incoming messages by exact CLR type
- Stores the received message in the activity output
- Removes the `Message` input from workflow input before moving to the next activity
- Creates bookmarks without an activity instance ID, so matching messages can resume waiting workflow instances of the same activity type

The generated activity is a trigger activity, but it does not automatically start every workflow that contains it. To start new workflow instances from a message, configure the activity as a start trigger, just as you would with other Elsa triggers.

## How Publishing Works

The generated `Publish {MessageType}` activity resolves `IBus` from DI and calls `bus.Publish(...)` with the configured message payload.

Two details matter in practice:

- The publish activity exists only for class message types. If you register an interface, Elsa creates the receive trigger but not the publish activity.
- The message input is converted to the configured CLR type before publishing, so the payload must be compatible with the registered message type.

## MassTransit Activities vs Other Messaging Features

This page covers MassTransit-backed message-type activities. Elsa also has two related but different messaging integration paths:

- The MassTransit workflow dispatcher, enabled with `UseMassTransitDispatcher()` on workflow management and runtime features, moves workflow dispatch and stimulus work over MassTransit. It is infrastructure for Elsa itself, not a replacement for message-type activities.
- The Azure Service Bus activity module (`UseAzureServiceBus()`) provides transport-specific activities such as `SendMessage` and `MessageReceived`. Those are separate from the generic message-type activities described here.

Use the generic MassTransit activities when your workflows exchange strongly typed application messages. Use transport-specific activity modules when you need queue or topic concepts that belong to a specific broker.

## Operational Notes

- `DisableConsumers` can be used to stop MassTransit consumers from being configured on a given node. The Elsa workbench uses this to keep API-only nodes from consuming messages directly.
- `MassTransitOptions` in `release/3.8.0` include `TemporaryQueueTtl`, `ConcurrentMessageLimit`, `PrefetchCount`, and `MaxAutoRenewDuration` (Azure Service Bus only).
- If you are using MassTransit for clustered workflow dispatch or distributed cache invalidation, see the distributed hosting and clustering guides as well. Those features share the broker, but they solve a different problem than workflow message activities.
