---
description: Use Azure Service Bus queues and topics from Elsa workflows in 3.8.0.
---

# Azure Service Bus activities

The `Elsa.ServiceBus.AzureServiceBus` extension adds server-side activities for
sending messages to Azure Service Bus and waiting for messages from a queue or
topic subscription. Use it when a workflow needs brokered point-to-point or
publish/subscribe messaging through Azure Service Bus.

This is a different integration from the MassTransit-backed activities. The
standalone module uses `Azure.Messaging.ServiceBus` directly and has its own
queue, topic, subscription, worker, and message-payload model.

Elsa Studio does not connect to Azure Service Bus. It receives activity
descriptors from the Elsa Server, while the server process owns the connection,
workers, resource initialization, and message delivery.

## Install and register the server module

Install the package in the application that executes workflows:

```bash
dotnet add package Elsa.ServiceBus.AzureServiceBus --version 3.8.0
```

Register the feature with either a connection string or a configured
connection-string name:

```csharp
using Elsa.Extensions;
using Elsa.ServiceBus.AzureServiceBus.Models;

builder.Services.AddElsa(elsa =>
{
    elsa.UseAzureServiceBus("AzureServiceBus", feature =>
    {
        feature.AzureServiceBusOptions += options =>
        {
            options.Queues.Add(new QueueDefinition("orders"));
            options.Topics.Add(new TopicDefinition
            {
                Name = "order-events",
                Subscriptions = new List<SubscriptionDefinition>
                {
                    new() { Name = "workflow" }
                }
            });
        };
    });
});
```

When the first argument is a connection-string name, the module first looks it
up with `IConfiguration.GetConnectionString` and uses the value itself if no
matching named connection exists. Keep connection strings in deployment
configuration or a secret store rather than in workflow definitions.

The feature can also read the connection name from its options:

```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "Endpoint=sb://...;SharedAccessKeyName=...;SharedAccessKey=..."
  },
  "AzureServiceBus": {
    "ConnectionStringOrName": "AzureServiceBus"
  }
}
```

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseAzureServiceBus(feature =>
    {
        feature.AzureServiceBusOptions += options =>
            options.ConnectionStringOrName = "AzureServiceBus";
    });
});
```

The released feature constructs `ServiceBusClient` and
`ServiceBusAdministrationClient` from the resolved connection-string value.
Replace those factories when your host needs a different credential setup.

## Choose queues, topics, and subscriptions

Azure Service Bus queues are a point-to-point shape. Topics are
publish/subscribe entities, and workflows receive topic messages through a
subscription rather than directly from the topic. This is also how the Azure
SDK models the processor that Elsa starts.

Declare resources in `AzureServiceBusOptions` when the application should
ensure they exist:

```csharp
feature.AzureServiceBusOptions += options =>
{
    options.Queues.Add(new QueueDefinition("invoice-work"));
    options.Topics.Add(new TopicDefinition
    {
        Name = "invoice-events",
        Subscriptions = new List<SubscriptionDefinition>
        {
            new() { Name = "billing-workflow" },
            new() { Name = "audit-workflow" }
        }
    });
};
```

The current configuration shape puts subscriptions inside their
`TopicDefinition`. `AzureServiceBusOptions.Subscriptions` and
`SubscriptionDefinition.Topic` remain as obsolete compatibility properties;
prefer the nested form for new configuration.

By default, `CreateQueuesTopicsAndSubscriptions` is `true`. At host startup,
the feature checks the configured queues, topics, and subscriptions and creates
missing entities. It does not delete entities or update the configuration of
entities that already exist. Set it to `false` when infrastructure is managed
outside the Elsa process:

```csharp
elsa.UseAzureServiceBus("AzureServiceBus", feature =>
{
    feature.CreateQueuesTopicsAndSubscriptions = false;
});
```

If automatic creation is disabled, create every queue, topic, and subscription
before workflows begin waiting for messages. The Elsa feature does not expose a
Studio resource-management page.

## Send a message

Add **Send Message** from the **Azure Service Bus** category. Its inputs are:

- **Message Body** — required. A `string` is sent as-is; other values are
  serialized with the selected formatter, defaulting to Elsa's JSON formatter.
- **Queue or Topic** — the destination entity name.
- **Content Type**, **Subject**, and **Correlation ID** — optional Azure
  Service Bus message metadata.
- **Formatter Type** — an optional exact formatter type used for non-string
  bodies.
- **Application Properties** — optional advanced properties represented as a
  dictionary.

For example, a programmatic activity can send JSON text without requiring a
custom formatter:

```csharp
using Elsa.ServiceBus.AzureServiceBus.Activities;
using Elsa.Workflows.Models;

var send = new SendMessage
{
    QueueOrTopic = new Input<string>("orders"),
    MessageBody = new Input<object>("{\"orderId\":\"42\",\"status\":\"accepted\"}"),
    ContentType = new Input<string>("application/json"),
    CorrelationId = new Input<string>("order-42")
};
```

The activity creates a sender for the destination, sends one
`ServiceBusMessage`, and disposes the sender. It does not create the queue or
topic itself; resource initialization is a separate feature concern.

When setting application properties from a designer, use string-valued JSON
properties. The 3.8.0 implementation reads each supplied value as a
`JsonElement` and calls `GetString()` before adding it to the Azure message.

## Receive a message

Add **Message Received** from the **Azure Service Bus** category and configure
the entity to read:

- Set **Queue or Topic** to a queue name and leave **Subscription** empty for a
  queue.
- Set **Queue or Topic** to a topic name and **Subscription** to the
  subscription name for a topic.
- Leave **Message Type** at its default (`string`) when the body should remain
  text.
- Select a formatter and target message type when the body should be
  deserialized into a .NET value.

The activity exposes two outputs:

- **Message** — the body as a string when no formatter is selected, or the
  formatter result when one is selected.
- **Transport Message** — a serializable copy of the Azure message metadata,
  including the body bytes, subject, content type, correlation ID, delivery
  count, timing fields, message identifiers, session fields, dead-letter fields,
  and application properties.

The body is converted from the received bytes to text before the optional
formatter runs. Select a formatter that can parse that text into the configured
message type; a formatter or type mismatch faults the activity.

## How waiting and workers behave

**Message Received** is a trigger activity. When it is encountered inside an
already-running workflow, Elsa creates a bookmark containing the queue/topic
and optional subscription. When it is the workflow's start trigger, the
incoming stimulus can resume it immediately.

The extension starts one Azure Service Bus processor for each distinct
queue/topic and subscription referenced by indexed triggers or bookmarks:

1. A startup task reads existing `MessageReceived` triggers and bookmarks and
   starts workers for them.
2. Later trigger or bookmark indexing notifications start workers for newly
   introduced message waits.
3. Each worker creates a `ServiceBusProcessor`, receives messages, copies the
   Azure message into `ReceivedServiceBusMessageModel`, and sends an Elsa
   stimulus.
4. The stimulus carries the message correlation ID into Elsa's runtime and
   resumes matching triggers or bookmarks.

The module does not require an ASP.NET endpoint mapping for receiving messages.
It consumes through the hosted workers configured by the feature. The worker
uses the Azure SDK's default `ServiceBusProcessorOptions`; the Elsa feature
does not expose processor options such as receive mode or concurrency in its
public configuration surface.

## Studio and deployment boundaries

- Install and register the package in every Elsa Server process that executes
  the activities or must consume messages. Installing it only in Studio does
  not make the activities executable.
- Studio displays the descriptors returned by the connected server. If the
  activities are missing, verify the server module and the Studio connection
  before changing the workflow definition.
- The server needs permission to send/receive messages and, when automatic
  initialization is enabled, permission to inspect and create queues, topics,
  and subscriptions.
- A multi-node deployment can start a worker for the same entity on each node.
  Use the Azure Service Bus entity and consumer behavior appropriate for your
  deployment, and verify throughput, lock duration, retries, and settlement
  behavior with your Azure SDK configuration.
- Do not put connection strings, shared access keys, or sensitive message
  bodies into workflow definitions or diagnostic logs.

## Troubleshooting checklist

1. If the activities are absent, confirm `Elsa.ServiceBus.AzureServiceBus` is
   installed in the workflow execution host and `.UseAzureServiceBus(...)` runs
   during startup.
2. If startup fails while creating resources, verify the resolved connection
   string and the namespace permissions for queue/topic/subscription
   administration. Set `CreateQueuesTopicsAndSubscriptions` to `false` when
   the application identity cannot administer infrastructure.
3. If sending fails, verify that the destination exists and that the host has
   send permission. Check that non-string message bodies have a registered
   formatter of the selected type.
4. If a receive workflow remains waiting, check the exact queue/topic and
   subscription names. A topic receiver must use a subscription.
5. If the message output has the wrong shape, remember that no formatter means
   string output. Configure both the formatter and the target `Message Type`.
6. If Studio cannot find the activities, confirm it is connected to the server
   where the feature is enabled; Studio does not discover them from the NuGet
   package installed in another process.

## Related integrations

- [MassTransit activities](masstransit/README.md) use registered .NET message
  types and MassTransit transports. Their Azure Service Bus package is a
  transport for MassTransit, not this standalone activity module.
- [Activity Reference](activity-reference.md) explains how server-enabled
  activity descriptors appear in the picker.
- [Long-running Workflows](../guides/running-workflows/long-running-workflows.md)
  explains the bookmark and persistence model used by waiting activities.
- [Azure Service Bus queues, topics, and subscriptions](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-queues-topics-subscriptions)
  provides the platform-level messaging model.

## Release source

This page is validated against `release/3.8.0` in `elsa-extensions` at
`a44e2b09af1202ff4936f493756e114c357eff81`:

- [`UseAzureServiceBus` and feature registration](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.AzureServiceBus/Extensions/ModuleExtensions.cs)
- [`AzureServiceBusFeature` startup, clients, workers, and activities](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.AzureServiceBus/Features/AzureServiceBusFeature.cs)
- [`AzureServiceBusOptions` and resource definitions](https://github.com/elsa-workflows/elsa-extensions/tree/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.AzureServiceBus/Models)
- [`SendMessage`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.AzureServiceBus/Activities/SendMessage.cs)
- [`MessageReceived`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.AzureServiceBus/Activities/MessageReceived.cs)
- [`ServiceBusInitializer`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.AzureServiceBus/Services/ServiceBusInitializer.cs)
- [`Worker`, startup, and dynamic worker updates](https://github.com/elsa-workflows/elsa-extensions/tree/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.AzureServiceBus)
