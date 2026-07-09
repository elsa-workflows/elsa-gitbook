# MassTransit

MassTransit integration in Elsa lets you expose selected .NET message types as
workflow activities. In `release/3.8.0`, Elsa generates these activities from
the types you register with the MassTransit feature.

Use this integration when workflows need to publish strongly typed application
messages or wait for messages that arrive through a MassTransit bus.

For `release/3.8.0`, prefer concrete class or record contracts for the message
types you register. Elsa's generated receive activity accepts a message only
when the runtime type exactly matches the configured `MessageType`.

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

If you need to tune endpoint lifetime or throughput, configure
`MassTransitOptions` in your host:

{% code title="Program.cs" %}

```csharp
builder.Services.Configure<MassTransitOptions>(options =>
{
    options.TemporaryQueueTtl = TimeSpan.FromHours(1);
    options.ConcurrentMessageLimit = 16;
    options.PrefetchCount = 32;
    options.MaxAutoRenewDuration = TimeSpan.FromMinutes(5); // Azure Service Bus only
});
```

{% endcode %}

In `release/3.8.0`, these options are consumed by the in-memory, RabbitMQ, and
Azure Service Bus configurators. `TemporaryQueueTtl` controls the lifetime of
temporary queues created for temporary consumers, `ConcurrentMessageLimit` and
`PrefetchCount` shape throughput, and `MaxAutoRenewDuration` applies only to
Azure Service Bus.

## How message types shape the designer

Elsa uses reflection over the registered CLR type to build the generated
activity descriptor. In `release/3.8.0`, these attributes influence what
appears in Studio:

* `ActivityAttribute`: can override the generated type name, display name,
  category, and description
* `DisplayNameAttribute`: changes the visible activity title when
  `ActivityAttribute` does not provide one
* `CategoryAttribute`: changes the Studio category from the default
  `MassTransit`
* `DescriptionAttribute`: populates the activity description and variable
  descriptor metadata

For example:

{% code title="Contracts/OrderCreated.cs" %}

```csharp
using System.ComponentModel;
using Elsa.Workflows.Attributes;

[Activity(DisplayName = "Order Accepted", Category = "Sales")]
[Description("Raised when an order has been accepted by the sales system.")]
public record OrderCreated(string OrderId);
```

{% endcode %}

Registering that type generates a trigger named `Order Accepted` in the
`Sales` category, plus `Publish Order Accepted` because the type is a class.

## Endpoint topology and consumer behavior

The MassTransit feature does more than add activities. It also registers a
generated `WorkflowMessageConsumer<T>` for each message type you add with
`AddMessageType<T>()`.

In `release/3.8.0`:

* Elsa configures MassTransit with kebab-case endpoint names
* each registered message type gets a concrete MassTransit consumer that turns
  the incoming message into an Elsa stimulus
* the stimulus activity type name is derived from the CLR message type
* temporary consumers are created before the transport is configured so MassTransit
  can bind them correctly
* RabbitMQ and Azure Service Bus temporary endpoints are prefixed with the
  current application instance name, which helps isolate per-node temporary
  consumers

This matters operationally because the same broker can host both your
application messages and Elsa's own internal dispatcher traffic without those
concerns being the same feature.

## Contract guidance

`AddMessageType<T>()` accepts reference types, but the runtime behavior is more
specific than that:

* `MassTransitActivityTypeProvider` creates a receive trigger for every
  registered message type
* `Publish {MessageType}` is created only when the registered type is a class
* `MessageReceived` resumes only when `message.GetType() == MessageType`

In practice, that means concrete classes and records are the safest contracts
for both publishing and receiving in `release/3.8.0`. If you register an
interface or other abstraction, Elsa still creates the receive activity
descriptor, but the runtime message must still match the configured type
exactly.

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

For new workflow instances, the MassTransit `CorrelationId` is forwarded in the
stimulus metadata and becomes the trigger invocation's correlation ID. For
existing waiting workflows, Elsa uses that correlation ID to narrow bookmark
matching when one is present.

## How publishing works

The generated `Publish {MessageType}` activity resolves `IBus` from DI and
calls `bus.Publish(...)` with the configured message payload.

Two details matter in practice:

* the publish activity exists only for class message types; if you register an
  interface or other non-class reference type, Elsa does not generate the
  publish activity
* the message input is converted to the configured CLR type before publishing,
  so the payload must be compatible with the registered message type

## Deployment patterns

`DisableConsumers` is the main release-backed switch for deciding whether a
node should consume MassTransit messages directly.

Use it when you split responsibilities across nodes, for example:

* API-focused nodes: expose Elsa APIs and Studio-backed operations, but do not
  consume workflow messages
* worker-focused nodes: run the same application but keep consumers enabled so
  they receive workflow messages and dispatcher traffic

{% code title="Program.cs" %}

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseMassTransit(mt =>
    {
        mt.DisableConsumers = appRole == ApplicationRole.Api;
        mt.AddMessageType<OrderCreated>();
        mt.UseRabbitMq(rabbitMqConnectionString);
    }));
```

{% endcode %}

When consumers are disabled, the generated publish activities still exist, but
that node does not configure the message consumers that turn inbound MassTransit
messages into workflow stimuli.

## MassTransit activities vs MassTransit infrastructure features

This page covers MassTransit-backed message-type activities. Elsa also has two
related but different MassTransit integration paths:

* the MassTransit workflow dispatcher, enabled with
  `UseMassTransitDispatcher()` on workflow management and runtime features,
  moves workflow dispatch and stimulus work over MassTransit; it is
  infrastructure for Elsa itself, not a replacement for message-type activities
* the Azure Service Bus activity module (`UseAzureServiceBus()`) provides
  transport-specific activities such as `SendMessage` and `MessageReceived`;
  those are separate from the generic message-type activities described here

Use the generic MassTransit activities when your workflows exchange strongly
typed application messages. Use the workflow dispatcher when Elsa runtime and
management work should move over the bus between nodes. Use transport-specific
activity modules when you need queue or topic concepts that belong to a
specific broker.

## Operational notes

* `MassTransitHostOptions.WaitUntilStarted` is enabled so the host waits for
  the bus to start before startup completes.
* Azure Service Bus can run automated subscription cleanup, but that cleanup is
  broad: in `release/3.8.0` it can remove subscriptions that Elsa did not
  create if their queues cannot be found.
* If you are also using MassTransit for clustered workflow dispatch or
  distributed cache invalidation, see the distributed hosting and clustering
  guides as well. Those features share the broker, but they solve a different
  problem than workflow message activities.

## Next step

Continue with the [MassTransit tutorial](tutorial.md) for a minimal
publish-and-receive setup.
