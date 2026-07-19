# Message broker topology cookbook

Message-broker topology is part of the Elsa deployment contract. It determines
which queues, topics, subscriptions, and consumers exist, which nodes process
them, and which resources can be removed when a node or deployment disappears.

This guide describes the behavior shipped in `release/3.8.0`. Use it when you
are splitting API and worker nodes, moving workflow dispatch over a broker, or
deciding whether Elsa should create and clean up Azure Service Bus resources.

## Choose the topology path first

Elsa has several broker integrations that solve different problems. Do not
assume that configuring one enables the others.

| Integration | Topology owner | Use it when |
| --- | --- | --- |
| [MassTransit message activities](../../activities/masstransit/README.md) | MassTransit configures endpoints for registered consumers and message types. | Workflows publish or receive strongly typed application messages. |
| MassTransit workflow dispatcher | Elsa creates a default dispatch queue and one queue for each configured dispatcher channel. | Workflow execution and stimulus delivery should cross process or node boundaries through MassTransit. |
| Azure Service Bus activities | Your Elsa configuration names queues, topics, and subscriptions; Elsa can create missing resources at startup. | A workflow needs Azure Service Bus-specific queue/topic inputs rather than a MassTransit contract. |
| MassTransit distributed cache | Elsa publishes a change-token message and each node consumes it from a temporary endpoint. | In-memory caches on multiple nodes must be invalidated after a change. |

The MassTransit activity integration and the Azure Service Bus activity module
are separate. A workflow using `SendMessage` or `MessageReceived` from the
Azure Service Bus activity module does not use the MassTransit-generated
consumer topology.

The MassTransit workflow dispatcher is also opt-in. In the classic feature API,
enable `UseMassTransitDispatcher()` on the workflow management and workflow
runtime features. Registering MassTransit message types or selecting RabbitMQ
or Azure Service Bus as a transport does not, by itself, move Elsa's workflow
dispatch traffic onto the broker.

## Endpoint naming in release 3.8.0

Elsa configures MassTransit with kebab-case endpoint names. The exact names
depend on whether the endpoint is a dispatcher channel, a temporary consumer,
or a consumer discovered by MassTransit from a registered message type.

### Workflow dispatcher queues in the classic feature API

In the classic `AddElsa(...)` feature API, when the MassTransit workflow
dispatcher is enabled, the transport features explicitly configure the default
dispatcher endpoint:

```text
elsa-dispatch-workflow-request
```

If you add a dispatcher channel named `high-priority`, the classic transport
path creates the corresponding endpoint:

```text
elsa-dispatch-workflow-request-high-priority
```

The channel name is kebab-cased before it is appended. Elsa configures the
default endpoint and every configured channel endpoint with
`DispatchWorkflowRequestConsumer`.

Use channels to separate workload classes that need different broker-level
throughput or consumer limits. A channel is only useful when the sending side
selects that channel and a worker is configured to consume the corresponding
endpoint.

The CShells transport path in `release/3.8.0` calls MassTransit's
`ConfigureEndpoints` but does not call Elsa's explicit
`SetupWorkflowDispatcherEndpoints` helper. Do not assume that a configured
non-default channel queue exists in that hosting path: verify the generated
endpoint or provision the channel topology before selecting it.

### Fixed and per-instance Elsa consumers

The dispatcher is not the whole broker topology. The following endpoints are
also relevant when the corresponding Elsa features are enabled:

| Endpoint name in the classic transport path | Role | Lifetime |
| --- | --- | --- |
| `elsa-dispatch-stimulus` | Consumes stimulus-dispatch messages and delivers them to the runtime. | Fixed consumer endpoint. |
| `<application-instance>-elsa-dispatch-cancel-workflow` | Consumes workflow-cancellation requests. | Temporary, per instance. |
| `<application-instance>-elsa-workflow-definition-updates` | Consumes workflow-definition update events so the node refreshes its local registry/cache. | Temporary, per instance; intentionally ignores `DisableConsumers`. |
| `<application-instance>-elsa-trigger-change-token-signal` | Consumes distributed-cache change-token signals. | Temporary, per instance; intentionally ignores `DisableConsumers`. |

For in-memory transport, the temporary consumer name is used without the
broker instance prefix. For RabbitMQ and MassTransit Azure Service Bus, the
transport prefixes temporary consumer endpoints with the application instance
name as described below. MassTransit-generated application-message endpoints
are additional topology and depend on the message types registered by the
host.

### Temporary consumer endpoints

Temporary consumers are used for node-specific or short-lived concerns such as
distributed cache invalidation and cancellation dispatch. With RabbitMQ and
MassTransit Azure Service Bus, Elsa prefixes the consumer name with the
application instance name:

```text
<application-instance>-<consumer-name>
```

The instance prefix is important when each concurrently running node has a
unique application instance name. It prevents temporary consumers on different
nodes from unintentionally sharing one queue and gives Azure Service Bus's
cleanup logic a way to associate a subscription with an application instance.

Configure a stable, unique instance name for each logical node when restarts
should reuse its transport entities. The Core `release/3.8.0` source supports
`ApplicationInstanceOptions.InstanceName` or an environment variable such as
`HOSTNAME`; without a configured name, the default provider generates a random
name for each process start. Random names can accumulate abandoned
per-instance entities on transports with entity-count limits.

```csharp
builder.Services.Configure<ApplicationInstanceOptions>(options =>
{
    options.InstanceNameEnvironmentVariable = "HOSTNAME";
});
```

The final broker name can also be shortened or normalized to satisfy transport
limits, so inspect the broker after startup rather than hard-coding the full
name in external tooling.

In-memory MassTransit uses the registered consumer name directly because the
transport exists only inside the process. It cannot deliver messages between
processes.

### Explicit Azure Service Bus activity endpoints

The non-MassTransit Azure Service Bus activities use the names supplied by the
workflow:

* `SendMessage` sends to the `QueueOrTopic` input.
* `MessageReceived` listens to `QueueOrTopic` and, when the input is a topic,
  its `Subscription` input.

When `CreateQueuesTopicsAndSubscriptions` is enabled (the release default),
the Azure Service Bus activity module creates missing configured queues,
topics, and subscriptions during startup. It does not provide the
MassTransit temporary-endpoint cleanup behavior, so treat these names as
application-owned infrastructure and manage their lifecycle separately.

## Place consumers deliberately

Consumer placement is a deployment decision, not just a broker setting.

For a split deployment using the feature API that exposes
`MassTransitFeature.DisableConsumers`, keep consumers enabled on worker nodes
and disable them on API-focused nodes:

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseMassTransit(massTransit =>
    {
        massTransit.DisableConsumers = appRole == ApplicationRole.Api;
        massTransit.UseRabbitMq(rabbitMqConnectionString);
    }));
```

On a node where consumers are disabled, Elsa filters the registered/manual
consumer set and does not configure the workflow-dispatcher endpoints. That
node can still publish messages, but it should not be treated as a workflow
worker. The release implementation still appends generated
`WorkflowMessageConsumer<T>` consumers for message types registered with
`AddMessageType<T>()`, so `DisableConsumers` is not a blanket switch for
application-message ingress. If API nodes must not receive those messages,
register the message activities only on the worker host or use separate host
configuration. Ensure that at least one worker node has consumers enabled and
has access to the broker and the workflow persistence store.

`DisableConsumers` suppresses registered consumers unless they are registered
to ignore the flag. In `release/3.8.0`, this includes both distributed-cache
invalidation and workflow-definition updates. API nodes may therefore still
host per-instance consumers such as
`<application-instance>-elsa-workflow-definition-updates` and
`<application-instance>-elsa-trigger-change-token-signal`. Do not remove these
consumers unless you have replaced workflow-definition propagation or cache
invalidation with another mechanism.

## Temporary resource lifetime and cleanup

`MassTransitOptions.TemporaryQueueTtl` controls the temporary endpoint lifetime
used by the broker transports. If it is not configured, the release uses one
hour.

| Transport | Temporary endpoint behavior | Operational implication |
| --- | --- | --- |
| In-memory | Endpoint exists only while the process is running. | No cross-process delivery and no broker cleanup. |
| RabbitMQ | Endpoint is prefixed with the instance name, non-durable, auto-delete, and has a queue-expiration value from `TemporaryQueueTtl`. | A stopped node's endpoint can disappear, but broker policy and expiry still affect when it is removed. |
| MassTransit Azure Service Bus | Endpoint is prefixed with the instance name and uses `AutoDeleteOnIdle` from `TemporaryQueueTtl`. | Idle queues/subscriptions can be removed by Azure Service Bus; instance naming is also used by Elsa's orphan handling. |

For example:

```csharp
builder.Services.Configure<MassTransitOptions>(options =>
{
    options.TemporaryQueueTtl = TimeSpan.FromHours(2);
    options.ConcurrentMessageLimit = 16;
    options.PrefetchCount = 32;
});
```

Choose a lifetime that covers expected restarts and transient outages without
leaving abandoned resources indefinitely. This option is about temporary
consumer endpoints; it does not change the retention of workflow instances,
bookmarks, or application messages.

### Azure Service Bus cleanup warning

MassTransit Azure Service Bus has two related cleanup paths in
`release/3.8.0`:

1. Heartbeat-timeout handling can remove temporary subscriptions and their
   forwarded queues for an instance that is no longer alive.
2. `EnableAutomatedSubscriptionCleanup` adds a periodic cleanup service. It
   starts after two minutes, then runs at the configured interval (seven days
   by default), using a distributed lock so only one node performs the scan.

The automated scan is not ownership-aware. It scans topics whose names start
with `elsa`, deletes empty topics, and removes subscriptions whose forwarded
queue cannot be found in the same namespace. The release source explicitly
warns that it cannot distinguish Elsa-created topology from other topology and
that queues in another namespace appear missing.

Enable this option only when the Azure Service Bus namespace is governed by
Elsa's naming and lifecycle rules. Otherwise, leave it disabled and clean up
orphaned resources with an ownership-aware operator or deployment process.

## Distributed cache invalidation

`distributedCaching.UseMassTransit()` is not workflow-message processing. When
a cache signal is published, Elsa sends a `TriggerChangeTokenSignal` containing
the cache key. Each node consumes the signal and invokes its local change-token
handler.

The released extensions feature registers the temporary consumer name
`elsa-trigger-change-token-signal`; the selected MassTransit transport adds the
application-instance prefix and may normalize the final broker entity name.

This endpoint is deliberately node-specific: each node needs one copy of the
signal so it can invalidate its own in-memory cache. If you instead configure
one shared queue for all nodes, the broker may deliver a signal to only one
consumer and other nodes can retain stale data.

Use the [distributed hosting guide](../../hosting/distributed-hosting.md) for
the broader cache and locking setup. Use the [MassTransit guide](../../activities/masstransit/README.md)
for application message contracts and generated workflow activities.

## Operations checklist

Before promoting a broker-backed Elsa deployment, verify:

1. The broker account can create and configure the queues, topics, and
   subscriptions required by the selected integration.
2. Worker nodes, rather than API-only nodes, consume the workflow-dispatcher
   endpoints.
3. Application instance names are stable across restarts of the same logical
   node and unique while nodes overlap during a rolling deployment; otherwise
   temporary endpoints can accumulate or collide.
4. Dispatcher channel names are documented alongside the code that selects
   them, and the corresponding endpoints exist after startup.
5. Temporary endpoint expiry is compatible with restart and outage windows.
6. Azure automated cleanup is disabled when the namespace contains topology
   owned by another application.
7. Cache invalidation uses a per-node consumer topology and is tested during a
   rolling restart.

For a first deployment, inspect the broker after Elsa starts and compare the
observed topology with the intended endpoint list. The endpoint names are
derived from runtime registrations, so a queue list is often the fastest way
to detect a missing transport package, disabled consumer, or unexpected
dispatcher channel.
