---
description: >-
  Configure the key-value and distributed-lock providers that Elsa uses for
  durable outbox records and multi-node workflow coordination.
---

# Runtime coordination storage

Elsa has two infrastructure contracts that are easy to confuse:

| Contract | What it stores or coordinates | Elsa 3.8.0 default |
| --- | --- | --- |
| `IKeyValueStore` | Small serialized records such as outbox items, instance heartbeats, and persisted administrative pauses | In-memory |
| `IDistributedLockProvider` | Leases that ensure only one node performs a coordinated operation at a time | Local files under `App_Data/locks` |

These defaults are useful for development and single-host tests. They are not
durable or cross-node production infrastructure. A production cluster needs a
key-value provider visible to every node and a lock provider that coordinates
across every node.

{% hint style="warning" %}
`UseDistributedRuntime()` does not select a shared lock provider or make the
default key-value store durable. Configure both storage boundaries explicitly
when more than one host can process the same Elsa deployment.
{% endhint %}

## What uses each contract?

The stores are infrastructure, not user-facing workflow data stores. Their
consumers have different failure and durability requirements:

| Runtime path | Contract | Operational consequence |
| --- | --- | --- |
| Workflow dispatch outbox | `IKeyValueStore` and `IDistributedLockProvider` | Pending dispatch records must survive restarts, and only one processor should claim a tenant-scoped batch at a time. See [Workflow dispatch outbox](workflow-dispatch-outbox.md). |
| Distributed bookmark queue worker | `IDistributedLockProvider` | Only one node processes the distributed queue at a time; a node that cannot acquire the lock retries locally. |
| Workflow resumption and trigger indexing | `IDistributedLockProvider` | Concurrent resume and trigger-indexing operations are serialized across nodes. |
| Instance heartbeats | `IKeyValueStore` and `IDistributedLockProvider` | Every node writes a heartbeat; the monitor takes a lock before finding timed-out nodes. |
| Administrative pause persistence | `IKeyValueStore` | `PausePersistencePolicy.AcrossReactivations` can restore a pause after the host is reactivated. |

The key-value store is not a replacement for the workflow definition,
instance, bookmark, or execution-log stores. Configure the runtime persistence
provider for those stores as well, and point every node at the same logical
runtime database or document store.

## Choose a key-value provider

The default `MemoryKeyValueStore` loses its records when the process exits and
is local to one process. Select a runtime persistence provider that also
registers an `IKeyValueStore` implementation:

| Runtime persistence | Key-value implementation in the release line | Typical fit |
| --- | --- | --- |
| EF Core | `EFCoreKeyValueStore` | Relational database deployments that already use EF Core runtime persistence |
| MongoDB | `MongoKeyValueStore` | Deployments using the MongoDB runtime provider |
| Dapper | `DapperKeyValueStore` | Deployments using the Dapper runtime provider |

For EF Core, the runtime persistence feature wires the key-value store for
you:

```csharp
using Elsa.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;

builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowManagement(management =>
    {
        management.UseEntityFrameworkCore(ef =>
        {
            ef.UsePostgreSql(builder.Configuration.GetConnectionString("Elsa"));
        });
    });

    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.UseEntityFrameworkCore(ef =>
        {
            ef.UsePostgreSql(builder.Configuration.GetConnectionString("Elsa"));
        });
    });
});
```

Use the matching runtime persistence extension when you use MongoDB or Dapper.
Do not register a durable workflow instance store while leaving
`IKeyValueStore` on the in-memory default: outbox records and coordination
records would still disappear on restart.

{% hint style="info" %}
The outbox is marker-gated rather than one atomic transaction across every
store. A durable key-value provider improves recovery, but it does not turn a
workflow-state commit and an outbox write into a single cross-store
transaction. See [Workflow dispatch outbox](workflow-dispatch-outbox.md) for
the delivery and recovery semantics.
{% endhint %}

## Choose a distributed lock provider

The runtime feature exposes a provider factory on `UseWorkflowRuntime`. The
release default is a `FileDistributedSynchronizationProvider` rooted at
`App_Data/locks`. That coordinates processes that can safely share the same
local file system; it does not coordinate independent containers or hosts.

Configure a provider supplied by your infrastructure package. Redis,
PostgreSQL, and SQL Server are examples of cross-node providers supported by
the release's startup diagnostic. The provider must use the same shared
resource namespace and compatible lease/timeout settings on every node.

The following shows the Elsa integration point; the provider construction is
intentionally omitted because its type and connection lifecycle depend on the
package you choose:

```csharp
using Elsa.Extensions;
using Elsa.Workflows.Runtime.Distributed.Extensions;
using Medallion.Threading;

// Construct this with the provider package and shared connection used by every node.
// The constructor is provider-specific; see the Redis example below.
IDistributedLockProvider crossNodeLockProvider = CreateCrossNodeLockProvider();

builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.UseDistributedRuntime();
        runtime.DistributedLockProvider = _ => crossNodeLockProvider;
        runtime.DistributedLockingOptions = options =>
        {
            options.LockAcquisitionTimeout = TimeSpan.FromMinutes(2);
        };
    });
});
```

Use the constructor and connection lifecycle recommended by the Redis,
PostgreSQL, SQL Server, or cloud-lock package you selected. The important Elsa
integration point is `runtime.DistributedLockProvider`; registering an
unrelated provider under a different service key does not replace this feature
setting.

For a complete Redis example, see [Redis distributed locking](../clustering/examples/redis-lock-setup.md).
For the broader distributed-hosting checklist, see [Distributed hosting](../../hosting/distributed-hosting.md).

## Development and single-host exceptions

The distributed runtime validates its provider during startup. If it detects
the built-in file or no-op provider, it logs a warning because the provider
cannot coordinate across application nodes. The warning is useful: do not
silence it as a substitute for configuring a shared provider.

For a deliberately single-host development or test deployment, acknowledge the
local provider explicitly:

```csharp
elsa.UseWorkflowRuntime(runtime =>
{
    runtime.UseDistributedRuntime();
    runtime.DistributedLockingOptions = options =>
    {
        options.AllowLocalLockProviderInDistributedRuntime = true;
    };
});
```

The Elsa Server sample enables this option by default only in development. In
production, set it to `false` and treat any local-provider warning as a
configuration defect. A shared network folder is not a substitute for a
lock service: file semantics and failure detection may not be consistent
across hosts.

## Validate a deployment

Before adding a second node, verify all of the following:

1. Every node uses the same durable runtime persistence provider and logical
   database or document store.
2. Every node resolves the same cross-node `IDistributedLockProvider`.
3. The lock provider can acquire and release a probe lock from each node.
4. Outbox, heartbeat, and runtime coordination records remain visible after a
   restart.
5. A two-node test exercises concurrent resume, trigger indexing, and outbox
   processing; inspect logs for local-provider warnings and lock timeouts.
6. Tenant-aware workloads use the same tenant resolution and shared storage
   configuration on every node.

Elsa exposes a distributed-lock readiness check when you register readiness
checks with `includeDistributedLocks: true`. Add it to the host's readiness
pipeline so a node with an unreachable lock provider is not presented as
ready:

```csharp
using Elsa.Extensions;

builder.Services
    .AddHealthChecks()
    .AddElsaReadinessChecks(includeDistributedLocks: true);
```

For the complete runtime, persistence, and endpoint contract—including the
sample's `/health/live` and `/health/ready` mappings—see [Readiness and Health
Checks](../../operate/readiness-and-health-checks.md).

The readiness probe confirms that the configured provider can reach and acquire
a probe lock; it does not prove that the provider is configured with the right
business-level topology. Keep the two-node concurrency test in your deployment
validation as well.

## Related guides

- [Workflow dispatch outbox](workflow-dispatch-outbox.md)
- [Distributed hosting](../../hosting/distributed-hosting.md)
- [Redis distributed locking](../clustering/examples/redis-lock-setup.md)
- [Persistence](../persistence/README.md)
- [Performance tuning](../performance/README.md)
