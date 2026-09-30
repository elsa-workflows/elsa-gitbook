---
description: >-
  Configure the Elsa 3.9.0 Proto.Actor workflow runtime for actor-based
  coordination across hosts, with explicit persistence and cluster boundaries.
---

# Proto.Actor workflow runtime

Elsa 3.9.0 includes an optional workflow runtime backed by Proto.Actor. It
routes workflow-instance commands to virtual actors in a Proto.Actor cluster.
Use it when workflow execution needs actor-based routing across multiple
processes or hosts. It is a runtime and coordination choice; it does not
replace Elsa's workflow-definition, workflow-instance, execution-log, or
distributed-lock providers.

The implementation is shipped by the `Elsa.Workflows.Runtime.ProtoActor` and
`Elsa.Actors.ProtoActor` modules in
[`elsa-extensions` release/3.9.0](https://github.com/elsa-workflows/elsa-extensions/tree/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules).
Elsa Studio has no Proto.Actor-specific module in the same release; Studio
continues to use the server's normal HTTP API.

## Decide whether to use it

Choose Proto.Actor when:

- a workflow instance should have a stable cluster address derived from its
  instance ID;
- several Elsa hosts must route commands through a cluster; or
- you already operate Proto.Actor infrastructure and want Elsa workflow
  coordination to use it.

Use the regular local or distributed workflow runtime when you only need the
runtime options described in the [distributed hosting guide](../../hosting/distributed-hosting.md)
and do not need actor-based routing. Proto.Actor adds an actor cluster,
remote transport, and a separate actor persistence provider to the deployment.

## Register the runtime

Install `Elsa.Workflows.Runtime.ProtoActor` and register both parts of the
feature:

1. `runtime.UseProtoActor()` selects `ProtoActorWorkflowRuntime` as the
   `IWorkflowRuntime` implementation.
2. `elsa.UseProtoActor(...)` installs the actor system, cluster, virtual actor
   provider, remote transport, and actor persistence service.

```csharp
using Elsa.Extensions;
using Microsoft.Data.Sqlite;
using Proto.Persistence.Sqlite;

builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowRuntime(runtime => runtime.UseProtoActor());

    // The extension's defaults are in-memory actor persistence and
    // in-memory Pub/Sub subscriber storage.
    elsa.UseProtoActor(proto =>
    {
        proto.PersistenceProvider = _ =>
            new SqliteProvider(new SqliteConnectionStringBuilder(
                "Data Source=proto-actor.db"));
    });
});
```

The two registrations are intentionally separate. The runtime feature sets
the workflow runtime implementation, while the actor feature creates the
`ActorSystem` and registers the `Cluster`. The release source shows both
extension methods in
[`WorkflowRuntimeFeatureExtensions.cs`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/runtimes/Elsa.Workflows.Runtime.ProtoActor/Extensions/WorkflowRuntimeFeatureExtensions.cs)
and
[`ProtoActorFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/actors/Elsa.Actors.ProtoActor/Features/ProtoActorFeature.cs).

### Development defaults

If you omit the `PersistenceProvider`, the actor feature uses
`InMemoryProvider`. If you omit the cluster provider and remote configuration,
it uses Proto.Actor's in-memory test provider and binds remote transport to
localhost. Those defaults are useful for a single-process development host;
they are not a multi-node deployment configuration. They are defined in
[`ProtoActorFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/actors/Elsa.Actors.ProtoActor/Features/ProtoActorFeature.cs).

## Persist Pub/Sub subscribers in Redis

Proto.Actor's topic actors keep their subscriber lists in the
`IKeyValueStore<Subscribers>` registered by `ProtoActorFeature`. In 3.9.0 the
default store is still in memory: it survives topic reactivation only within
the same process and cluster member, not a process restart or actor migration.
For a multi-host deployment, install the Redis extension and replace that
store with a shared Redis database.

```bash
dotnet add package Elsa.Actors.ProtoActor.PubSub.Redis
```

Use the package version that matches the other Elsa 3.9.0 packages in the
host.

Register an `IConnectionMultiplexer` and opt in from the same
`UseProtoActor` callback that configures the actor cluster:

```csharp
using Elsa.Extensions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddElsa(elsa =>
{
    elsa.UseProtoActor(proto =>
    {
        proto.UseRedisPubSubSubscribersStore(redis =>
        {
            redis.ConfigureOptions = options =>
                options.KeyPrefix = "elsa";
        });
    });
});
```

The Redis feature resolves the default database from the registered
`IConnectionMultiplexer`. If the subscribers should use another logical Redis
database, set `CreateDatabase` explicitly:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseProtoActor(proto =>
    {
        proto.UseRedisPubSubSubscribersStore(redis =>
        {
            redis.CreateDatabase = services =>
                services.GetRequiredService<IConnectionMultiplexer>().GetDatabase(5);
        });
    });
});
```

Every cluster member must use the same Redis database, cluster name, and
deterministic key configuration. With the default formatter, a topic is stored
under:

```text
{KeyPrefix}:{clusterName}:pubsub:topic:{topic}
```

The default `KeyPrefix` is `proto`. Set `KeyFormatter` when an existing Redis
key convention must be preserved; it receives the cluster name and topic and
replaces the default format. For example:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseProtoActor(proto =>
    {
        proto.UseRedisPubSubSubscribersStore(redis =>
        {
            redis.ConfigureOptions = options =>
                options.KeyFormatter = (clusterName, topic) =>
                    $"elsa:{clusterName}:{topic}";
        });
    });
});
```

Changing the prefix or formatter changes the keys that Proto.Actor reads, so
coordinate such a change as a subscriber-state migration.

This feature persists Pub/Sub subscriber lists only. It does not replace
`PersistenceProvider`, workflow persistence, the cluster provider, or remote
transport. Studio has no Redis-specific configuration surface in the 3.9.0
source; designers continue to work with the server's normal workflow API.

The extension points are implemented by
[`UseRedisPubSubSubscribersStore`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/actors/Elsa.Actors.ProtoActor.PubSub.Redis/Extensions/ProtoActorFeatureExtensions.cs),
[`RedisPubSubSubscribersStoreFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/actors/Elsa.Actors.ProtoActor.PubSub.Redis/Features/RedisPubSubSubscribersStoreFeature.cs),
and [`RedisSubscribersStore`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/actors/Elsa.Actors.ProtoActor.PubSub.Redis/Services/RedisSubscribersStore.cs).

## Configure a multi-host cluster

Every host that participates in the cluster needs compatible cluster and
remote-transport settings. Set a shared cluster name, select a real cluster
provider, and advertise an address that the other hosts can reach:

```csharp
using Proto.Cluster.Kubernetes;
using Proto.Remote;
using Proto.Remote.GrpcNet;

elsa.UseProtoActor(proto =>
{
    proto.ClusterName = "elsa-cluster";
    proto.CreateClusterProvider = _ =>
        new KubernetesProvider(new KubernetesProviderConfig());
    proto.ConfigureRemoteConfig = _ =>
        RemoteConfig.BindToAllInterfaces(
            advertisedHost: Environment.GetEnvironmentVariable(
                "POD_IP"));
});
```

The Kubernetes provider and the advertised host pattern are the same approach
used by the release workbench. In Kubernetes, supply the pod IP or another
reachable address through deployment configuration; do not advertise
`localhost` to other pods. See the workbench's
[`ProtoActor` setup](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/workbench/Elsa.Server.Web/Program.cs#L582-L606)
and the module's
[`ProtoActorFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/actors/Elsa.Actors.ProtoActor/Features/ProtoActorFeature.cs#L35-L145)
for the release-backed extension points.

`ClusterName`, the cluster-provider choice, remote addressing, and actor
persistence are infrastructure settings. Keep them aligned across hosts and
roll out changes as a cluster configuration change, not as a workflow
definition change.

## What is coordinated

The runtime creates a `ProtoActorWorkflowClient` for each workflow instance.
The client addresses a virtual actor named from the workflow instance ID, and
the actor loads workflow state and the workflow graph before running or
resuming it. The actor handles create, run, create-and-run, cancel, stop,
delete, export, and import messages in the generated
[`WorkflowInstance` protocol](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/runtimes/Elsa.Workflows.Runtime.ProtoActor/Proto/WorkflowInstance.proto).

This does not make all Elsa services actor-based. Workflow state is still
created and saved through `IWorkflowInstanceManager`, using the workflow
management/runtime persistence configured for the host. The actor feature's
`PersistenceProvider` is a separate Proto.Actor persistence service. Configure
both deliberately when you need restart durability.

The runtime implementation and the workflow actor are visible in
[`ProtoActorWorkflowRuntime.cs`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/runtimes/Elsa.Workflows.Runtime.ProtoActor/Services/ProtoActorWorkflowRuntime.cs),
[`ProtoActorWorkflowClient.cs`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/runtimes/Elsa.Workflows.Runtime.ProtoActor/Services/ProtoActorWorkflowClient.cs),
and
[`WorkflowInstance.cs`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/runtimes/Elsa.Workflows.Runtime.ProtoActor/Actors/WorkflowInstance.cs).

### Keep related infrastructure separate

- **Workflow persistence** stores definitions, instances, bookmarks, and
  execution data. Configure it through the normal Elsa management/runtime
  persistence APIs.
- **Actor persistence** is supplied through `ProtoActorFeature.PersistenceProvider`
  and belongs to the Proto.Actor infrastructure.
- **Distributed locking** remains a runtime coordination concern. Review the
  lock-provider guidance in [Distributed Hosting](../../hosting/distributed-hosting.md)
  when other runtime components require a shared lock.
- **MassTransit dispatch** and **Proto.Actor distributed caching** are separate
  transports. The release workbench can enable Proto.Actor for distributed
  cache invalidation independently of selecting it as the workflow runtime.

## Tenant propagation

When an Elsa tenant is active, the Proto.Actor workflow client adds the tenant
ID to the actor message headers. Actor middleware reads that header, resolves
the tenant, and creates a tenant scope before processing the message. This
keeps the actor's Elsa services aligned with the tenant that initiated the
operation.

The propagation path is implemented in
[`ProtoActorWorkflowClient`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/runtimes/Elsa.Workflows.Runtime.ProtoActor/Services/ProtoActorWorkflowClient.cs#L66-L102)
and
[`TenantScopeMiddleware`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/actors/Elsa.Actors.ProtoActor/Middleware/TenantScopeMiddleware.cs).
Configure Elsa's tenant resolution before relying on this behavior, and test
cross-tenant requests at the API boundary.

## Release limitations

The 3.9.0 client wrapper is not a complete replacement for every
`IWorkflowClient` operation:

- `DeleteAsync` throws `NotImplementedException` in
  `ProtoActorWorkflowClient`.
- `InstanceExistsAsync` also throws `NotImplementedException` in that client.
- The actor protocol and actor implementation contain delete and
  instance-existence messages, but the client wrapper does not yet call them.

Do not build application logic that assumes those two client methods work when
running the 3.9.0 Proto.Actor runtime. Use the supported API path for the
operation or choose another runtime until the release changes. This limitation
is visible in the release
[`ProtoActorWorkflowClient`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/runtimes/Elsa.Workflows.Runtime.ProtoActor/Services/ProtoActorWorkflowClient.cs#L52-L65).

There is no Proto.Actor-specific Studio setting in the 3.9.0
[Studio release source](https://github.com/elsa-workflows/elsa-studio/tree/e7fb3f717a4d15316674b508e05a47737946bf0e/src/modules).
Studio users should connect to the server normally; operators must configure
and observe the server cluster and both persistence layers.

## Production checklist

Before enabling the runtime in production, verify:

- all hosts use the same cluster name and compatible Proto.Actor package/module
  versions;
- the cluster provider can discover every host and remote addresses are
  reachable from every host;
- actor persistence is not left on the default in-memory provider when actor
  restart durability is required;
- Elsa workflow persistence is configured independently and tested for
  workflow-state recovery;
- tenant resolution is enabled and tenant-scoped commands are tested; and
- application code does not depend on the unsupported 3.9.0 client methods.

For general multi-node runtime, locking, caching, and scheduling guidance,
continue with [Distributed Hosting](../../hosting/distributed-hosting.md).
