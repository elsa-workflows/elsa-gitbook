---
description: >-
  Release-backed guidance for configuring Elsa runtime, persistence, and
  distributed-lock readiness checks.
---

# Readiness and health checks

Use health endpoints to answer an operational question: **can this host
receive workflow work right now?** A workflow instance being `Running` or
`Suspended` is not the same thing as the host being ready. Health checks belong
to the host and orchestrator; Elsa Studio does not replace them with workflow
status indicators.

The Elsa 3.8.0 runtime provides three Elsa-specific readiness probes:

| Probe | What it checks | Healthy when |
| --- | --- | --- |
| Runtime | The runtime can create a workflow client and is accepting new work | The runtime is not paused or draining |
| Persistence | The registered workflow stores can be reached | Each registered store probe succeeds |
| Distributed locks | The configured lock provider can create and acquire a short-lived probe lock | A unique probe lock is acquired within the configured timeout |

Registration and endpoint mapping are separate steps. `AddElsaReadinessChecks`
registers probes; it does not create `/health/ready` or `/health/live`.

## Register the Elsa probes

Call the extension on the ASP.NET Core health-check builder after configuring
the Elsa runtime and its providers:

```csharp
using Elsa.Extensions;

builder.Services
    .AddHealthChecks()
    .AddElsaReadinessChecks(
        includePersistence: true,
        includeDistributedLocks: true,
        configureOptions: options =>
        {
            options.DistributedLockAcquisitionTimeout = TimeSpan.FromSeconds(2);
            options.ContinuePersistenceProbesAfterFailure = true;
        });
```

The defaults are `includePersistence: true`,
`includeDistributedLocks: false`, a one-second distributed-lock acquisition
timeout, and `ContinuePersistenceProbesAfterFailure: false`. Enable the lock
probe for a deployment that uses distributed runtime coordination. Keep
persistence enabled unless this host intentionally does not register Elsa
workflow stores.

The lock timeout must be greater than zero. Elsa validates it when options are
validated on start, so an invalid value can prevent the host from starting.

## Map liveness and readiness endpoints

The released `Elsa.Server.Web` sample maps liveness and readiness separately:

```csharp
using Elsa.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

app.MapHealthChecks("/health/live", new()
{
    // Liveness answers only whether the process and HTTP pipeline respond.
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new()
{
    Predicate = check =>
        check.Tags.Contains(HealthCheckExtensions.ElsaTag) &&
        check.Tags.Contains(HealthCheckExtensions.ReadinessTag),
    ResultStatusCodes =
    {
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});
```

The sample's `/health/live` endpoint selects no checks, so it is deliberately
cheap and does not test a database or the workflow runtime. Use it to decide
whether an orchestrator should restart a process. Use `/health/ready` to decide
whether a load balancer or orchestrator should send traffic to the process.

The readiness predicate selects the tags `elsa` and `elsa-readiness`, which
`AddElsaReadinessChecks` applies to all three Elsa probes. The sample maps both
`Degraded` and `Unhealthy` to HTTP 503; a healthy report returns 200. If you
add application checks and expect them to affect this endpoint, give them the
same tags or map a separate endpoint for your broader application contract.

## Understand the probe results

### Runtime

The runtime probe first asks `IWorkflowRuntime` to create a workflow client,
then reads the quiescence signal:

- **Healthy** means the runtime is accepting new work.
- **Degraded** means it is paused or draining. This is useful during an
  administrative pause or graceful shutdown and should normally remove the
  host from traffic without restarting it.
- **Unhealthy** means the runtime could not create a workflow client.

The health result includes the acceptance flag, quiescence reason, and active
execution-cycle count. It does not prove that a particular workflow definition
or external dependency is usable.

### Persistence

The persistence probe checks store reachability with harmless lookups. It
probes these stores in order:

1. Workflow definitions.
2. Workflow instances.
3. Triggers.
4. Bookmark queue.

The returned entity or count is intentionally ignored. This is a connectivity
probe, not a migration verifier or a data-integrity scan. When a registered
store throws, the health-check result data identifies the failed store. Whether
that data appears in the HTTP response depends on the response writer your
host configures. By default Elsa stops after the first failed probe; set
`ContinuePersistenceProbesAfterFailure` to `true` when an operator needs the
full failure inventory in one request.

If an individual store is not registered, Elsa skips that probe. If no
persistence stores are registered, the persistence check is **Degraded**.
Treat that as a configuration problem for a host expected to run persisted
workflows, even though it is not reported as `Unhealthy` by the check itself.

### Distributed locks

The lock probe resolves `IDistributedLockProvider`, creates a unique lock named
`elsa-health-check-{guid}`, and tries to acquire it using
`DistributedLockAcquisitionTimeout`:

- **Healthy** means the provider was reachable and the probe lock was acquired.
- **Degraded** means no provider is registered or the provider was reachable
  but could not acquire the probe lock before the timeout.
- **Unhealthy** means the provider threw while creating or acquiring the lock.

Acquiring a probe lock proves provider reachability and the basic acquire path;
it does not prove that all nodes use the same namespace, lease settings, or
business-level topology. Keep the two-node concurrency test described in
[Runtime coordination storage](../guides/architecture/runtime-coordination-storage.md).

## Apply the checks to deployments

Use the endpoint contract that matches the deployment:

| Deployment | Liveness | Readiness |
| --- | --- | --- |
| Local development | `/health/live` | `/health/ready` when troubleshooting provider setup |
| Single-node production | `/health/live` | Runtime and persistence; include locks if the runtime uses coordinated work |
| Multi-node production | `/health/live` | Runtime, persistence, and distributed locks backed by shared infrastructure |

For a multi-node deployment, validate all of the following before routing
traffic to a new node:

- The node uses the same durable workflow management/runtime stores as its
  peers.
- The node resolves the same cross-node distributed-lock provider.
- `/health/ready` succeeds from the node itself, not only through the load
  balancer.
- Outbox, heartbeat, resume, and trigger-indexing behavior works in a
  two-node test. A green probe cannot validate every cross-node failure mode.

For the storage choices behind these checks, see [Runtime coordination
storage](../guides/architecture/runtime-coordination-storage.md). For
dispatch durability and retry behavior, see [Workflow dispatch
outbox](../guides/architecture/workflow-dispatch-outbox.md).

## Diagnose a failed readiness check

1. Call `/health/ready` directly on the affected node and record the HTTP
   status and any details your host's response writer exposes.
2. If the health-check result or logs name a persistence store, check its
   connection, schema, credentials, and provider registration. A skipped or
   missing store can indicate an incomplete host composition rather than a
   transient outage.
3. If the distributed-lock check is degraded, inspect lock contention and the
   acquisition timeout. If it is unhealthy, inspect provider connectivity and
   the shared resource configuration.
4. If the runtime check is degraded, look at the quiescence reason before
   restarting the process; the host may be intentionally paused or draining.
5. Compare the node's runtime providers and tenant/storage configuration with
   a healthy peer before changing workflow definitions or retrying work.

Do not use a failed readiness probe as evidence that individual workflow
instances are corrupt. Use Studio's instance status, incidents, journal, and
activity execution records for workflow-level diagnosis; use these endpoints
for host admission and orchestration decisions.

## Release source

This page is grounded in Elsa Core `release/3.8.0`:

- [`HealthCheckExtensions`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Runtime/Extensions/HealthCheckExtensions.cs)
- [`ElsaReadinessHealthCheckOptions`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Runtime/Options/ElsaReadinessHealthCheckOptions.cs)
- [`ElsaRuntimeHealthCheck`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Runtime/HealthChecks/ElsaRuntimeHealthCheck.cs)
- [`ElsaWorkflowPersistenceHealthCheck`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Runtime/HealthChecks/ElsaWorkflowPersistenceHealthCheck.cs)
- [`ElsaDistributedLockHealthCheck`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Runtime/HealthChecks/ElsaDistributedLockHealthCheck.cs)
- [`Elsa.Server.Web` health endpoint mapping](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/apps/Elsa.Server.Web/Program.cs)
