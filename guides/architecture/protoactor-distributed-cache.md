---
description: >-
  Configure Proto.Actor cache-key invalidation across Elsa hosts and
  troubleshoot the member-local change-token signal path.
---

# Proto.Actor distributed-cache invalidation

Elsa can publish cache-key change-token signals through a Proto.Actor cluster
so that every cluster member invalidates its own local memory cache. Use this
when multiple Elsa hosts cache the same definitions or other Elsa-managed
data and a change on one host must invalidate matching entries on the others.

This feature distributes invalidation signals, not cache values. It does not
turn `IMemoryCache` into a shared cache, persist cache entries, replace
workflow persistence, process workflow messages, or provide a durable
exactly-once delivery contract.

The feature is part of the
[`Elsa.Caching.Distributed.ProtoActor` module in `elsa-extensions`
3.9.0](https://github.com/elsa-workflows/elsa-extensions/tree/aa646675043fc37e01936c760ad97522d9097a46/src/modules/caching).
Elsa Studio has no corresponding cache-transport setting in the
[`release/3.9.0` source](https://github.com/elsa-workflows/elsa-studio/tree/bd443662bb5f8a3e07ca1c489f39a37527526200/src/modules);
configure and observe this on the server hosts.

## When to use it

Choose Proto.Actor distributed-cache invalidation when:

- two or more Elsa server hosts use local memory caches;
- those hosts already share a Proto.Actor cluster or you want Proto.Actor to
  provide the cache-signal transport; and
- a cache-key change on one member must trigger the same local change-token
  handler on every member.

Use the [MassTransit cache-invalidation path](../integration/message-broker-topology.md#distributed-cache-invalidation)
when the deployment already uses MassTransit for this transport. The two
paths implement the same `IChangeTokenSignalPublisher` boundary with
different transports; choose the transport that matches the cluster and
broker topology you operate.

## Register the feature

Install the distributed-cache Proto.Actor package in every host that uses the
feature:

```bash
dotnet add package Elsa.Caching.Distributed.ProtoActor --version 3.9.0
```

Register the distributed-cache feature and the Proto.Actor cluster separately.
The first call selects Proto.Actor for cache signals. The second creates the
`ActorSystem` and `Cluster` that the cache feature needs.

```csharp
using Elsa.Extensions;
using Proto.Cluster.Kubernetes;
using Proto.Remote;
using Proto.Remote.GrpcNet;

builder.Services.AddElsa(elsa =>
{
    elsa.UseDistributedCache(distributedCaching =>
    {
        distributedCaching.UseProtoActor();
    });

    elsa.UseProtoActor(proto =>
    {
        proto.ClusterName = "elsa-cluster";
        proto.CreateClusterProvider = _ =>
            new KubernetesProvider(new KubernetesProviderConfig());
        proto.ConfigureRemoteConfig = _ =>
            RemoteConfig.BindToAllInterfaces(
                advertisedHost: Environment.GetEnvironmentVariable("POD_IP"));
    });
});
```

The cluster provider and advertised remote address are deployment choices.
For Kubernetes, advertise a reachable pod address rather than `localhost`.
Every participating host must use compatible Proto.Actor modules, the same
cluster name, and mutually reachable remote addresses. The broader
[Proto.Actor workflow runtime guide](protoactor-workflow-runtime.md) explains
cluster and remote-transport configuration in more detail; the cache feature
does not require selecting Proto.Actor as Elsa's workflow runtime.

The release workbench selects a distributed-cache transport with
`UseDistributedCache` and enables the Proto.Actor cluster separately. Its
[`Elsa.Server.Web` configuration](https://github.com/elsa-workflows/elsa-extensions/blob/aa646675043fc37e01936c760ad97522d9097a46/src/workbench/Elsa.Server.Web/Program.cs#L571-L608)
shows that the cache transport and workflow runtime are independent choices.

## How a signal moves through the cluster

The 3.9.0 path is:

1. Elsa's local `IChangeTokenSignaler` triggers a key on the member where the
   change occurred.
2. `DistributedChangeTokenSignaler` invokes the configured
   `IChangeTokenSignalPublisher` after the local trigger.
3. `ProtoActorChangeTokenSignalPublisher` publishes a protobuf message
   containing the cache key to the `change-token-signals` Proto.Actor topic.
4. Each member starts one system actor named `$memory-cache-invalidator` and
   subscribes that actor to the topic.
5. The member-local actor receives the key and invokes that host's
   `IChangeTokenSignalInvoker`, which cancels the local change token.

The local change-token registry is keyed by string and removes the token when
it is triggered. A cache entry that was registered with that token can then be
evicted by the normal memory-cache behavior. The signal contains the key only;
it does not contain the cached object or a replacement value.

```text
Host A local change
    │
    ├─ local change token is triggered
    └─ Proto.Actor publishes { Key: "..." }
             │
             ▼
      change-token-signals topic
          ┌──┴──┐
          ▼     ▼
      Host A   Host B
      local    local
      actor    actor
          │     │
          └─ trigger the same local key ─┘
```

The implementation is split across the
[`DistributedChangeTokenSignaler`](https://github.com/elsa-workflows/elsa-extensions/blob/aa646675043fc37e01936c760ad97522d9097a46/src/modules/caching/Elsa.Caching.Distributed/Services/DistributedChangeTokenSignaler.cs),
[`ProtoActorChangeTokenSignalPublisher`](https://github.com/elsa-workflows/elsa-extensions/blob/aa646675043fc37e01936c760ad97522d9097a46/src/modules/caching/Elsa.Caching.Distributed.ProtoActor/Services/ProtoActorChangeTokenSignalPublisher.cs),
and [`StartLocalCacheActor`](https://github.com/elsa-workflows/elsa-extensions/blob/aa646675043fc37e01936c760ad97522d9097a46/src/modules/caching/Elsa.Caching.Distributed.ProtoActor/HostedServices/StartLocalCacheActor.cs).
Core's [`IChangeTokenSignaler`](https://github.com/elsa-workflows/elsa-core/blob/9fbbef9411eb313c40e1aa301044bb7dc8dd7a36/src/modules/Elsa.Caching/Contracts/IChangeTokenSignaler.cs)
and [`ChangeTokenSignalInvoker`](https://github.com/elsa-workflows/elsa-core/blob/9fbbef9411eb313c40e1aa301044bb7dc8dd7a36/src/modules/Elsa.Caching/Services/ChangeTokenSignalInvoker.cs)
define the local key/token contract.

## Cluster and lifecycle requirements

The signal topic is useful only when every host is a member of the same
reachable Proto.Actor cluster. Check these settings on every host:

- `ClusterName` is identical for members that should share invalidation
  signals;
- the cluster provider can discover the other members;
- remote addresses are reachable from the other members and do not advertise
  `localhost` in a multi-host deployment; and
- the host can start and stop the local cache actor and its topic subscription.

At startup, `StartLocalCacheActor` spawns the member-local actor and subscribes
it to the topic. If the subscription fails, the feature stops the spawned
actor and propagates the failure. At shutdown, it unsubscribes and stops the
actor. A rolling deployment should therefore be tested for both subscription
registration and clean removal of the old member.

The integration tests verify that two members with the same cluster name each
receive one copy of a published signal, and that a stopped member no longer
receives later signals. That test evidence demonstrates the configured
in-memory cluster path; it is not a general promise of durable or exactly-once
delivery for every production provider.

## What this feature does not provide

Keep these responsibilities separate:

- **Cache values:** remain local to each host. A miss on one host does not read
  another host's memory cache.
- **Cache durability:** cache entries are not persisted by this feature and can
  disappear on process restart.
- **Workflow persistence:** definitions, instances, bookmarks, and execution
  data still use the normal Elsa persistence configuration.
- **Proto.Actor workflow runtime:** cache transport can use Proto.Actor without
  routing workflow-instance commands through Proto.Actor.
- **Workflow messaging:** the `change-token-signals` topic carries cache
  invalidation messages, not workflow-dispatch or application messages.
- **Delivery guarantees:** the feature publishes to Proto.Actor Pub/Sub and
  does not document a durable, replayable, or application-level exactly-once
  contract. Treat the cache as eventually refreshed by normal reads and test
  the failure behavior of the chosen cluster provider.

## Troubleshoot stale data

When one host still serves an old value, inspect the path in this order:

1. Confirm the host actually enabled `UseDistributedCache(...UseProtoActor())`
   and that its normal Elsa cache users are enabled.
2. Confirm `UseProtoActor(...)` created a real cluster for the host. The
   distributed-cache registration alone does not provide cluster discovery or
   remote transport.
3. Compare `ClusterName`, cluster-provider configuration, and advertised
   remote addresses across hosts.
4. Check startup logs for the `$memory-cache-invalidator` actor and the
   `change-token-signals` subscription. A failed subscription stops startup;
   an unreachable member or an incorrectly isolated cluster can prevent the
   expected fan-out.
5. Verify the changed key is the same string on every path. The signal carries
   the key verbatim; it does not translate definition IDs, tenant IDs, or
   application-specific aliases.
6. Reproduce with a rolling-restart test. Confirm that a restarted host
   subscribes again and that a stopped host is removed from the topic's
   subscriber set.

For the alternative broker-backed transport and per-node consumer topology,
see [Distributed cache invalidation in the message-broker guide](../integration/message-broker-topology.md#distributed-cache-invalidation).

## Source references

- [`ProtoActorDistributedCacheFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/aa646675043fc37e01936c760ad97522d9097a46/src/modules/caching/Elsa.Caching.Distributed.ProtoActor/Features/ProtoActorDistributedCacheFeature.cs)
  wires the distributed signal publisher, hosted service, actor provider, and
  invalidator actor.
- [`ProtoActorDistributedCacheFeatureExtensions`](https://github.com/elsa-workflows/elsa-extensions/blob/aa646675043fc37e01936c760ad97522d9097a46/src/modules/caching/Elsa.Caching.Distributed.ProtoActor/Extensions/ProtoActorDistributedCacheFeatureExtensions.cs)
  exposes `UseProtoActor` for the distributed-cache feature.
- [`MemoryCacheInvalidatorActor`](https://github.com/elsa-workflows/elsa-extensions/blob/aa646675043fc37e01936c760ad97522d9097a46/src/modules/caching/Elsa.Caching.Distributed.ProtoActor/Actors/MemoryCacheInvalidatorActor.cs)
  invokes the local change-token signal invoker for each received key.
- [`LocalCache.Messages.proto`](https://github.com/elsa-workflows/elsa-extensions/blob/aa646675043fc37e01936c760ad97522d9097a46/src/modules/caching/Elsa.Caching.Distributed.ProtoActor/Proto/LocalCache.Messages.proto)
  defines the protobuf signal payload.
- [`DistributedCacheFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/aa646675043fc37e01936c760ad97522d9097a46/src/modules/caching/Elsa.Caching.Distributed/Features/DistributedCacheFeature.cs)
  decorates the local change-token signaler and installs the distributed
  invoker boundary.
