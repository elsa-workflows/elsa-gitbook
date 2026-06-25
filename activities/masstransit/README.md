# MassTransit

MassTransit integration in Elsa lets you expose selected .NET message types as
workflow activities. In `release/3.8.0`, Elsa generates these activities from
the types you register with the MassTransit feature.

Use this integration when workflows need to publish strongly typed application
messages or wait for messages that arrive through a MassTransit bus.

## What this feature adds

After you call `UseMassTransit(...)` and register one or more message types
with `AddMessageType<T>()`, Elsa adds dynamic activities for those types:

* a trigger activity named after the message type, implemented by
  `MessageReceived`
* for class types only, an action activity named
  `Publish {MessageType}`, implemented by `PublishMessage`

For example, registering `OrderCreated` adds:

* `Order Created`
* `Publish Order Created`

This behavior comes from `MassTransitActivityTypeProvider`, which creates a
`MessageReceived` descriptor for every registered message type and a
`PublishMessage` descriptor only when the type is a class.

## Registration and transport

Install the base package for the feature and one transport package when you
need broker-backed messaging:

* `Elsa.ServiceBus.MassTransit`
* `Elsa.ServiceBus.MassTransit.RabbitMq`
* `Elsa.ServiceBus.MassTransit.AzureServiceBus`

At minimum, enable the feature and register the message types you want to
expose:

{% code title="Program.cs" %}

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseMassTransit(mt =>
    {
        mt.AddMessageType<OrderCreated>();
    }));
```

{% endcode %}

If you do not configure a transport, Elsa uses MassTransit's in-memory
transport. That is useful for local development and single-process setups, but
it does not provide inter-process messaging.

For broker-backed messaging, configure a transport explicitly:

### RabbitMQ

{% code title="Program.cs" %}

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseMassTransit(mt =>
    {
        mt.AddMessageType<OrderCreated>();
        mt.UseRabbitMq(rabbitMqConnectionString);
    }));
```

{% endcode %}

### Azure Service Bus

{% code title="Program.cs" %}

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseMassTransit(mt =>
    {
        mt.AddMessageType<OrderCreated>();
        mt.UseAzureServiceBus("<connection-string>", bus =>
        {
            bus.EnableAutomatedSubscriptionCleanup = true;
            bus.SubscriptionCleanupOptions = options =>
                options.Interval = System.TimeSpan.FromMinutes(5);
        });
    }));
```

{% endcode %}

Use RabbitMQ when your environment already standardizes on AMQP-style queues.
Use Azure Service Bus when Elsa participates in Azure-native messaging and you
want broker-managed topics and subscriptions.

## How receiving works

When MassTransit receives a registered message type, Elsa's generated
`WorkflowMessageConsumer<T>` sends a stimulus whose activity type name matches
the registered message type and whose workflow input contains the message under
the `Message` key.

The generated `MessageReceived` activity then:

* matches incoming messages by exact CLR type
* stores the received message in the activity output
* removes the `Message` input from workflow input before moving to the next
  activity
* creates bookmarks without an activity instance ID, so matching messages can
  resume waiting workflow instances of the same activity type

The generated activity is a trigger activity, but it does not automatically
start every workflow that contains it. To start new workflow instances from a
message, configure the activity as a start trigger, just as you would with
other Elsa triggers.

MassTransit correlation IDs are passed through in the stimulus metadata, so
message-bus correlation can flow into your workflow design when needed.

## How publishing works

The generated `Publish {MessageType}` activity resolves `IBus` from DI and
calls `bus.Publish(...)` with the configured message payload.

Two details matter in practice:

* the publish activity exists only for class message types; if you register an
  interface, Elsa creates the receive trigger but not the publish activity
* the message input is converted to the configured CLR type before publishing,
  so the payload must be compatible with the registered message type

## MassTransit activities vs other messaging features

This page covers MassTransit-backed message-type activities. Elsa also has two
related but different messaging integration paths:

* the MassTransit workflow dispatcher, enabled with
  `UseMassTransitDispatcher()` on workflow management and runtime features,
  moves workflow dispatch and stimulus work over MassTransit; it is
  infrastructure for Elsa itself, not a replacement for message-type activities
* the Azure Service Bus activity module (`UseAzureServiceBus()`) provides
  transport-specific activities such as `SendMessage` and `MessageReceived`;
  those are separate from the generic message-type activities described here

Use the generic MassTransit activities when your workflows exchange strongly
typed application messages. Use transport-specific activity modules when you
need queue or topic concepts that belong to a specific broker.

## Operational notes

* `DisableConsumers` can stop MassTransit consumers from being configured on a
  given node. The Elsa workbench uses this to keep API-only nodes from
  consuming messages directly.
* `MassTransitOptions` in `release/3.8.0` include `TemporaryQueueTtl`,
  `ConcurrentMessageLimit`, `PrefetchCount`, and `MaxAutoRenewDuration` for
  Azure Service Bus.
* If you are also using MassTransit for clustered workflow dispatch or
  distributed cache invalidation, see the distributed hosting and clustering
  guides as well. Those features share the broker, but they solve a different
  problem than workflow message activities.

## Next step

Continue with the [MassTransit tutorial](tutorial.md) for a minimal
publish-and-receive setup.
