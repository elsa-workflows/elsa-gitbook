---
description: Configure Apache Kafka consumers and producers for Elsa workflows in 3.8.0.
---

# Kafka activities

The `Elsa.ServiceBus.Kafka` extension adds server-side activities for producing
Kafka messages and starting or resuming workflows when messages are consumed.
It uses the Confluent .NET client and keeps Kafka consumers, producers, topic
choices, and schema registries in the Elsa Server process.
The 3.8.0 Extensions release pins `Confluent.Kafka` and
`Confluent.SchemaRegistry.Serdes.Avro` to `2.12.0`.

This is a standalone Kafka integration. It is separate from the MassTransit
integration and its transport-specific activities.
Elsa Studio does not connect to Kafka directly; it receives activity
descriptors and definition dropdown values from the Elsa Server.

## Install and register the server module

Install the package in the application that executes workflows:

```bash
dotnet add package Elsa.ServiceBus.Kafka --version 3.8.0
```

Register the feature during Elsa startup. The following example declares a
JSON consumer, a topic for the producer dropdown, and a JSON producer:

```csharp
using Confluent.Kafka;
using Elsa.Extensions;
using Elsa.ServiceBus.Kafka;
using Elsa.ServiceBus.Kafka.Factories;

builder.Services.AddElsa(elsa =>
{
    elsa.UseKafka(kafka =>
    {
        kafka.ConfigureOptions(options =>
        {
            options.Consumers.Add(new ConsumerDefinition
            {
                Id = "orders-consumer",
                Name = "Orders consumer",
                FactoryType = typeof(ExpandoObjectConsumerFactory),
                Config = new ConsumerConfig
                {
                    BootstrapServers = "localhost:9092",
                    GroupId = "elsa-orders",
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    EnableAutoCommit = true
                }
            });

            options.Topics.Add(new TopicDefinition
            {
                Id = "orders-replies",
                Name = "orders.replies"
            });

            options.Producers.Add(new ProducerDefinition
            {
                Id = "orders-producer",
                Name = "Orders producer",
                FactoryType = typeof(ExpandoObjectProducerFactory),
                Config = new ProducerConfig
                {
                    BootstrapServers = "localhost:9092"
                }
            });
        });
    });
});
```

`UseKafka` registers the activities, definition providers, UI handlers,
consumer and producer factories, message handlers, and a hosted startup task.
The built-in options provider reads definitions from `KafkaOptions`; it does
not load them from an Elsa management database.

For configuration-driven hosts, bind the same options explicitly:

```csharp
elsa.UseKafka(kafka =>
    kafka.ConfigureOptions(options => configuration.GetSection("Kafka").Bind(options)));
```

When `FactoryType` is supplied through JSON configuration, use the
assembly-qualified name of a factory type available to your application. The
sample workbench configuration in the release contains old `Elsa.Kafka`
assembly names; do not copy those names into a 3.8.0 application.

## Configure definitions

`KafkaOptions` contains four collections and three header settings:

- `Consumers` describes the Kafka clients that receive messages.
- `Producers` describes the Kafka clients that publish messages.
- `Topics` supplies named choices to **Produce Message**. A topic does not
  need to be declared here to be consumed by **Message Received**.
- `SchemaRegistries` supplies Confluent-compatible Schema Registry clients for
  Avro consumers and the Studio schema-name dropdown.
- `CorrelationHeaderKey` defaults to `x-correlation-id`.
- `WorkflowInstanceIdHeaderKey` defaults to `x-workflow-instance-id`.
- `TenantHeaderKey` defaults to `Tenant`.

Each consumer and producer definition has an inherited `Id`, a display `Name`,
a `FactoryType`, a Confluent `ConsumerConfig` or `ProducerConfig`, and an
optional `SchemaRegistryId`. The configuration is application-owned. Store
credentials in deployment configuration or a secret store, and avoid placing
SASL passwords or registry credentials in workflow definitions or logs.

Use the regular Confluent client settings for bootstrap servers, consumer
groups, offset reset, SASL/SSL, timeouts, and producer behavior. The release
module does not add an explicit Kafka commit or transaction layer; delivery and
offset behavior follows the client configuration and broker setup.

### Topics are not infrastructure provisioning

`TopicDefinition` is a named `{ Id, Name }` choice used by the producer input
handler. The Kafka module does not create topics or configure partitions,
replication, retention, or ACLs. Provision those through Kafka administration
or deployment tooling.

### Schema Registry definitions

Configure a registry when a consumer uses `AvroConsumerFactory`:

```csharp
using Confluent.SchemaRegistry;

options.SchemaRegistries.Add(new SchemaRegistryDefinition
{
    Id = "orders-registry",
    Name = "Orders registry",
    Config = new SchemaRegistryConfig
    {
        Url = "https://schema-registry.example.com",
        BasicAuthUserInfo = "key:secret"
    }
});
```

The built-in Studio dropdown enumerates Avro record schemas from configured
registries. It always includes **(any)**, skips key subjects and registries
using `AuthCredentialsSource.SaslInherit`, and logs-and-skips an unavailable
or unparseable registry. Set `SchemaFullNamePrefix` when the dropdown should
only show names with a particular prefix.

The built-in producer factories in 3.8.0 serialize strings, JSON objects, or
JSON model values. `SchemaRegistryId` is passed to custom producer factories,
but it does not make those built-in factories produce Avro messages.

## Choose a factory

The factory determines the key/value types of the Confluent client and the
value shape delivered to the workflow.

### Consumer factories

- `DefaultConsumerFactory` consumes `string` values with an ignored key. Use
  it for text payloads.
- `ExpandoObjectConsumerFactory` consumes JSON into an `ExpandoObject`. Use it
  when downstream expressions need dynamic properties.
- `GenericConsumerFactory<TKey, TValue>` consumes JSON into a known .NET type.
  Set `FactoryType` to a closed generic type from your application. The Kafka
  feature does not register the open generic itself; the configured closed type
  is created when the worker starts.
- `AvroConsumerFactory` consumes Avro records through Schema Registry and
  converts each record to a serializable `Dictionary<string, object?>`.

The JSON serializer/deserializer uses `System.Text.Json`. A malformed or
incompatible payload raises the deserialization error from the Confluent
client; it is not converted into an Elsa activity result.

### Producer factories

- `DefaultProducerFactory` produces strings and uses a `Null` key.
- `ExpandoObjectProducerFactory` serializes an `ExpandoObject` as JSON and
  uses a `Null` key.
- `GenericProducerFactory<TKey, TValue>` serializes `TValue` as JSON and uses
  the configured `TKey` type.

The activity converts the content to the producer's value type before sending.
Supply a key compatible with the producer's configured key type; omit **Key**
when the selected built-in producer uses `Null` keys.

## Receive Kafka messages

Add **Message Received** from the **Kafka** category. Configure:

- **Consumer** — one configured `ConsumerDefinition`.
- **Topics** — one or more Kafka topic names. This is a free-text multi-value
  input, not the producer topic dropdown.
- **Schema Full Name** — optional exact Avro record full name. It only matches
  when the consumer exposes schema metadata, as the built-in Avro factory does.
- **Predicate** — optional JavaScript expression evaluated against the message.
- **Local** — for an activity waiting inside a workflow, restricts resumption
  to the current instance or a matching correlation fallback.

The activity is both a trigger and a blocking activity:

- As a workflow start trigger, a matching message invokes a new workflow
  instance.
- Inside a running workflow, it creates a bookmark and resumes after a
  matching message arrives.

The result is the deserialized message body. The `TransportMessage` output is
a `KafkaTransportMessage` containing `Key`, `Value`, `Topic`, a dictionary of
header byte arrays, and optional `SchemaFullName`.

For predicates, the handler creates `transportMessage` and `message` variables,
where `message` is the deserialized value. In the exact 3.8.0 source, the
start-trigger predicate path returns a new expression context after setting
those variables, so expressions that depend on them should be tested before
being used as a production gate. Predicate exceptions are logged and treated
as a non-match.

## Produce Kafka messages

Add **Produce Message** from the **Kafka** category. Configure:

- **Topic** — a topic name or a declared `TopicDefinition` choice.
- **Producer** — one configured `ProducerDefinition`.
- **Content** — the value to serialize.
- **Key** — optional and type-compatible with the producer factory.
- **Correlation ID** — optional value written as UTF-8 bytes to
  `CorrelationHeaderKey`.
- **Local** — writes the current workflow instance ID as UTF-8 bytes to
  `WorkflowInstanceIdHeaderKey`.

For example, a JSON-capable producer can publish an object built by the
workflow:

```csharp
using Elsa.ServiceBus.Kafka.Activities;
using Elsa.Workflows.Models;

var produce = new ProduceMessage
{
    Topic = new Input<string>("orders.replies"),
    ProducerDefinitionId = new Input<string>("orders-producer"),
    Content = new Input<object>(new { Status = "accepted", OrderId = "42" }),
    CorrelationId = new Input<string>("order-42")
};
```

The activity creates the selected producer, sends one message, flushes the
producer, and disposes it. It does not provision the destination topic.

## Matching, correlation, and tenancy

The hosted worker matches each consumed message against the stored
`MessageReceived` triggers and bookmarks for that consumer. Topic names must
match exactly. Trigger matches can also require the configured tenant header,
an exact Avro schema full name, and a true predicate.

The default `HeaderCorrelationStrategy` reads the UTF-8 value of
`x-correlation-id`. Replace it with `NullCorrelationStrategy` or a custom
`ICorrelationStrategy` when another header or message field should supply the
correlation ID:

```csharp
elsa.UseKafka(kafka =>
{
    kafka.WithCorrelationStrategy<NullCorrelationStrategy>();
});
```

`Local` is implemented as a workflow-instance header on produced messages. A
waiting local bookmark first checks that header and can fall back to its stored
correlation ID. A non-local bookmark is matched by consumer, topic, schema, and
predicate. The correlation strategy also supplies the correlation ID when a
matching start trigger is invoked.

For a trigger with a tenant, the worker compares the message's `Tenant` header
(or `TenantHeaderKey`) to the trigger tenant. The 3.8.0 bookmark matching path
does not apply that same tenant-header comparison, so multi-tenant deployments
must test waiting workflows carefully and should isolate broker consumers and
permissions according to their tenancy model.

## Worker lifecycle and operations

At startup, the module:

1. Enumerates configured consumer definitions and creates one worker per
   definition.
2. Reads stored `MessageReceived` triggers and bookmarks.
3. Subscribes each worker to the union of topics used by its bound triggers and
   bookmarks.
4. Starts the Confluent consumer loop.

When workflow triggers or bookmarks change, the worker updates its topic
subscriptions. If a consumer definition changes, Elsa stops and recreates its
worker. Declared topics alone do not start consumers, and a consumer with no
bound trigger or bookmark subscribes to no topics.

The worker processes one consumed record at a time through the Elsa mediator.
It ignores partition-end markers, logs `ConsumeException` failures, and stops
after more than 100 consecutive consume exceptions. Configure broker-side
retention, consumer-group identity, offset policy, authentication, and
scaling for the delivery guarantees your workflow requires.

In a multi-node Elsa deployment, each node can create a consumer for the same
definition. Use Kafka consumer-group and partition design deliberately, and
ensure every node can reach the broker and, when applicable, Schema Registry.

## Studio and deployment boundaries

- Install the Kafka package and register `UseKafka` in every Elsa Server that
  executes or receives the workflows. Installing it only in Studio does not
  make the activities executable.
- Studio displays **Message Received** and **Produce Message** from the
  descriptors supplied by the connected server. It does not provide a
  Kafka-broker administration page or create definitions for you.
- The topic, consumer, producer, and schema dropdowns call server-side
  providers. If a dropdown is empty, inspect server registration and options
  binding before editing the workflow JSON.
- The module's shell feature exposes the workflow-instance header setting as a
  restart-required infrastructure setting. Consumer/producer definitions and
  broker credentials remain application configuration.

## Troubleshooting

1. **Activities are missing.** Verify that `Elsa.ServiceBus.Kafka` is installed
   and `UseKafka` runs in the server connected to Studio.
2. **A dropdown is empty.** Check the matching `KafkaOptions` collection and
   the definition provider. `Topics` affects the producer dropdown; consumed
   topics are entered directly on **Message Received**.
3. **No message is consumed.** Verify `BootstrapServers`, `GroupId`, the exact
   topic name, broker ACLs, and `AutoOffsetReset`. A consumer with no stored
   trigger or bookmark has no topic subscription.
4. **The message shape is wrong.** Check the selected factory. The default
   consumer returns text, the Expando factory returns dynamic JSON, the generic
   factory returns the configured model, and Avro returns a dictionary.
5. **Avro setup fails.** Set `SchemaRegistryId` on the consumer, provide a
   reachable registry, and verify the registry authentication settings. The
   schema full-name filter is an exact match.
6. **A waiting workflow does not resume.** Check consumer and topic equality,
   predicate results, the `Local` workflow-instance header, correlation header,
   and the bookmark's persisted state. For tenants, verify the configured
   tenant header and test the 3.8.0 bookmark boundary described above.
7. **Messages appear duplicated or skipped.** Inspect the Confluent consumer
   group's committed offsets and the `EnableAutoCommit`/offset settings. Elsa
   does not add an explicit commit or idempotency mechanism.

## Related integrations

- [MassTransit activities](masstransit/README.md) use registered .NET message
  types and MassTransit transports. Their configuration is separate from this
  standalone Kafka module.
- [Azure Service Bus activities](azure-service-bus.md) document the separate
  Azure SDK-based queue/topic integration.
- [Message broker topology](../guides/integration/message-broker-topology.md)
  compares Elsa's broker-backed runtime paths.
- [Activity Reference](activity-reference.md) explains how server-enabled
  activity descriptors appear in the picker.
- [Long-running Workflows](../guides/running-workflows/long-running-workflows.md)
  explains the bookmark and persistence model used by waiting activities.
- [Confluent .NET client](https://docs.confluent.io/kafka-clients/dotnet/current/overview.html)
  documents the client types used by this module.
- [Apache Kafka documentation](https://kafka.apache.org/documentation/)
  explains topics, partitions, consumer groups, and broker operations.

## Release source

This page is validated against `release/3.8.0` in `elsa-extensions` at
`a44e2b09af1202ff4936f493756e114c357eff81`:

- [`UseKafka` and Kafka feature registration][kafka-use]
- [`Confluent.Kafka` package versions][kafka-packages]
- [`KafkaFeature` services, factories, handlers, and startup task][kafka-feature]
- [`KafkaOptions` and definition entities][kafka-module]
- [`MessageReceived`][kafka-receive]
- [`ProduceMessage`][kafka-produce]
- [`Worker` and `WorkerManager`][kafka-workers]
- [`TriggerWorkflows` matching and invocation][kafka-trigger]
- [`Consumer` and producer factories][kafka-factories]
- [`Schema Registry dropdown handler`][kafka-schema-dropdown]

[kafka-use]: https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.Kafka/Extensions/ModuleExtensions.cs
[kafka-packages]: https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/Directory.Packages.props
[kafka-feature]: https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.Kafka/Features/KafkaFeature.cs
[kafka-module]: https://github.com/elsa-workflows/elsa-extensions/tree/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.Kafka
[kafka-receive]: https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.Kafka/Activities/MessageReceived.cs
[kafka-produce]: https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.Kafka/Activities/ProduceMessage.cs
[kafka-workers]: https://github.com/elsa-workflows/elsa-extensions/tree/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.Kafka/Implementations
[kafka-trigger]: https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.Kafka/Handlers/TriggerWorkflows.cs
[kafka-factories]: https://github.com/elsa-workflows/elsa-extensions/tree/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.Kafka/Factories
[kafka-schema-dropdown]: https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/servicebus/Elsa.ServiceBus.Kafka/UIHints/SchemaFullNameDropdownOptionsProvider.cs
