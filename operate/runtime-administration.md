---
description: >-
  Release-backed operator guide for pausing, resuming, draining, and checking
  an Elsa 3.8.0 workflow runtime.
---

# Runtime administration and graceful drain

Use runtime administration when you need to stop new workflow work on a host
without immediately terminating the process. Elsa exposes status, pause,
resume, and force-drain endpoints through the workflow runtime API. These
operations control runtime admission; they do not change the state of
individual workflow definitions or instances unless a force-drain has to
interrupt active execution cycles. The admission boundary is implemented by
each ingress adapter, so a paused runtime flag is not a promise that every
queued or scheduled dispatch has stopped.

## Choose the operation

| Situation | Operation | Result |
| --- | --- | --- |
| Inspect whether the host accepts work | `GET /admin/workflow-runtime/status` | Returns runtime state, ingress-source state, and active execution-cycle count |
| Temporarily stop new work | `POST /admin/workflow-runtime/pause` | Sets an administrative pause; existing work can finish |
| Return a paused host to normal operation | `POST /admin/workflow-runtime/resume` | Clears the administrative pause, unless the runtime is draining |
| Escalate an unsafe or stuck shutdown | `POST /admin/workflow-runtime/force-drain` | Immediately force-cancels active execution cycles and leaves the runtime draining |

The route prefix is relative to the Elsa API base configured by your host. The
release endpoints use the `ManageWorkflowRuntime` permission for pause,
resume, and force-drain. Status accepts either `read:workflow-runtime` or
`ManageWorkflowRuntime`. See [Elsa API Permissions](../guides/authentication/permissions.md)
for role design.

## Configure graceful shutdown

The workflow runtime feature registers graceful-shutdown behavior. Configure
it through `ConfigureGracefulShutdown` when the default timings or pause
persistence policy do not fit your deployment:

```csharp
using Elsa.Extensions;
using Elsa.Workflows.Runtime.Options;

builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.ConfigureGracefulShutdown(options =>
        {
            options.DrainDeadline = TimeSpan.FromSeconds(30);
            options.IngressPauseTimeout = TimeSpan.FromSeconds(5);
            options.PausePersistence = PausePersistencePolicy.SessionScoped;
        });
    });
});
```

The release defaults are a 30-second overall drain deadline, a 5-second
per-ingress-source pause timeout, a 10,000-item paused stimulus-queue depth,
`Buffer` overflow behavior, session-scoped pause state, and a cap of 100
force-cancelled instance IDs in the force-drain response. Elsa clamps the
effective drain deadline to the host shutdown timeout minus a small safety
margin.

`DrainDeadline` and `IngressPauseTimeout` must be greater than zero. A source
may provide its own positive pause timeout; the orchestrator still caps it at
the remaining overall drain budget.

## Inspect runtime status

```http
GET /admin/workflow-runtime/status
Authorization: Bearer <token with read:workflow-runtime>
```

The response contains the composite quiescence state, each registered ingress
source, and the number of active execution cycles:

```json
{
  "state": {
    "reason": "AdministrativePause",
    "isAcceptingNewWork": false,
    "pausedAt": "2026-09-10T09:12:43Z",
    "drainStartedAt": null,
    "pauseReasonText": "broker migration",
    "pauseRequestedBy": "ops@example.com",
    "generationId": "..."
  },
  "sources": [
    {
      "name": "http.trigger",
      "state": "Paused",
      "lastError": null,
      "lastTransitionAt": "2026-09-10T09:12:43Z"
    }
  ],
  "activeExecutionCycleCount": 2
}
```

`reason` is a flags value. `None` means the runtime is accepting new work;
`AdministrativePause` means an operator pause is active; `Drain` means the
runtime is draining. The flags can coexist. A drain is forward-only for the
current runtime generation and is cleared only when a new generation starts.

## Pause and resume safely

Pause is idempotent. A repeated pause returns the current post-request state
without publishing another effective-transition audit event. The optional
reason is human-readable and is included in the status response.

```bash
curl -X POST https://localhost:8080/elsa/api/admin/workflow-runtime/pause \
  -H 'Authorization: Bearer <token>' \
  -H 'Content-Type: application/json' \
  -d '{"reason":"broker migration"}'
```

While paused, HTTP-triggered workflow requests are rejected with `503 Service
Unavailable` and a reason-aware `Retry-After` header (`60` seconds for an
administrative pause, `5` seconds while draining). Other ingress adapters have
their own admission and buffering behavior. Do not infer from the runtime
status alone that scheduled, broker, or already queued internal work has
stopped.

The paused stimulus-queue policy is `Buffer` by default. If you configure a
maximum paused depth and choose `Reject`, new stimuli beyond the threshold are
rejected instead. In the 3.8.0 Core source, the internal bookmark-queue
processor does not itself consult `IQuiescenceSignal`, even though its
diagnostic ingress-source entry reports `Paused`; treat queued bookmark
processing as a separate operational concern and verify it before using pause
as a hard freeze.

The readiness probe becomes degraded while the runtime is paused or draining,
so a load balancer or orchestrator should stop routing new traffic to that
host.

Resume clears only the administrative-pause flag:

```bash
curl -X POST https://localhost:8080/elsa/api/admin/workflow-runtime/resume \
  -H 'Authorization: Bearer <token>' \
  -H 'Content-Type: application/json' \
  -d '{}'
```

If a drain is active, resume returns `409 Conflict` with the code
`runtime-draining`. Restart or reactivate the runtime to create a new
generation; do not treat resume as a way to reverse a drain.

### Persist a pause across reactivation

`PausePersistencePolicy.SessionScoped` is the default: a new runtime
generation starts unpaused. Select `AcrossReactivations` when an operator
pause must survive a host restart or shell reactivation:

```csharp
runtime.ConfigureGracefulShutdown(options =>
{
    options.PausePersistence = PausePersistencePolicy.AcrossReactivations;
});
```

This policy uses Elsa's `IKeyValueStore`. Configure a durable, shared provider
for a multi-node deployment; the in-memory store cannot preserve the pause
through a process exit or coordinate it across nodes. If no key-value store is
registered, the release implementation tolerates the missing store and the
configured policy does not persist anything, so verify the provider during
deployment checks. The persisted pause is re-applied on each node during
activation, and an explicit resume clears it.
See [Runtime coordination storage](../guides/architecture/runtime-coordination-storage.md)
for provider selection.

## Graceful drain and force-drain

When the host stops, Elsa's drain service runs a graceful drain. It marks the
runtime as draining, pauses ingress sources in parallel, waits for active
execution cycles until the effective deadline, and force-cancels remaining
cycles if the deadline is exceeded. A source that times out or throws is
reported as `PauseFailed`; if it supports force-stop, Elsa attempts that
escalation within the remaining budget.

Use force-drain only as an operator escalation:

```bash
curl -X POST https://localhost:8080/elsa/api/admin/workflow-runtime/force-drain \
  -H 'Authorization: Bearer <token>' \
  -H 'Content-Type: application/json' \
  -d '{"reason":"stuck shutdown"}'
```

Force-drain uses a zero deadline. Elsa force-cancels active execution cycles,
persists affected instances with `SubStatus = Interrupted`, and writes a
`WorkflowInterrupted` execution-log entry for each affected instance. The
response reports the overall result, phase durations, per-source outcomes, the
true number of force-cancelled cycles, and up to the configured number of
instance IDs.

Force-drain does not terminate the host process. The runtime remains in the
`Drain` state until the next runtime generation. If a competing drain is
already in progress and no prior outcome is available, the endpoint returns
`409 Conflict` with the code `drain-in-progress`; a repeated operator-force
call can instead receive the cached outcome. Force-drain does not delete
queued bookmark records.

## Verify readiness after an operation

The runtime readiness check creates a workflow client and reads the quiescence
state. It is healthy only when the runtime is accepting new work; paused or
draining is reported as degraded, and a client-creation failure is unhealthy.
With the standard endpoint mapping, degraded and unhealthy readiness produce
HTTP 503 while liveness remains a separate process check.

```bash
curl -i https://localhost:8080/health/ready
curl -i https://localhost:8080/health/live
```

Use readiness to remove a host from traffic and liveness to decide whether an
orchestrator should restart the process. For endpoint registration and the
persistence and distributed-lock probes, see [Readiness and Health Checks](readiness-and-health-checks.md).

## What Studio shows

The Elsa 3.8.0 Studio dashboard renders the backend dashboard's runtime status
as a compact, read-only chip:

| API status | Studio label | Visual meaning |
| --- | --- | --- |
| `AcceptingWork` | Accepting work | Success |
| `Paused` | Paused | Warning |
| `Draining` | Draining | Informational |
| missing, unauthorized, unavailable, or unknown | Unavailable | Error |

The dashboard status chip helps a process designer or operator understand why
new work is not arriving when the backend dashboard data is available. An
`Unavailable` chip can mean a missing dashboard feature, denied access, a
backend failure, or an unknown status; it is not proof that the runtime itself
is unhealthy. The chip does not replace the API permission check or provide
the pause/resume/force-drain controls. Use the runtime-admin API from an
authorized operational tool for state changes.

## Operator checklist

1. Call `/admin/workflow-runtime/status` and record `reason`, source states,
   and `activeExecutionCycleCount`.
2. For planned maintenance, pause the runtime and, if your host maps Elsa's
   readiness check, confirm `/health/ready` returns 503 before changing
   infrastructure.
3. Wait for active cycles to settle, or use force-drain only when interruption
   is acceptable and the interrupted-workflow recovery path is understood.
4. After maintenance, resume a paused runtime and verify status plus
   readiness. A drained runtime needs a new generation.
5. In a multi-node deployment, perform the check on every node and use shared
   durable coordination storage. Pause state is per runtime or shell; even
   with `AcrossReactivations`, persistence reapplies state on activation but
   does not broadcast a live pause to other nodes. A single node's status does
   not describe the cluster.

## Release source

This page is grounded in Elsa Core `release/3.8.0` and Elsa Studio
`release/3.8.0`:

- [Core runtime-admin endpoints](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Api/Endpoints/RuntimeAdmin)
- [Core runtime-admin response models](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Api/Endpoints/RuntimeAdmin/Models.cs)
- [Core runtime-admin service](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Runtime/Services/WorkflowRuntimeAdminService.cs)
- [Core quiescence signal](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Runtime/Services/QuiescenceSignal.cs)
- [Core drain orchestrator](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Runtime/Services/DrainOrchestrator.cs)
- [Core graceful-shutdown options](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Runtime/Options/GracefulShutdownOptions.cs)
- [Core runtime readiness check](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Runtime/HealthChecks/ElsaRuntimeHealthCheck.cs)
- [Studio dashboard runtime chip](https://github.com/elsa-workflows/elsa-studio/blob/release/3.8.0/src/modules/Elsa.Studio.Dashboard/Components/DashboardRuntimeChip.razor)
- [Studio runtime status mapping](https://github.com/elsa-workflows/elsa-studio/blob/release/3.8.0/src/modules/Elsa.Studio.Dashboard/Services/DashboardUiMapper.cs)
