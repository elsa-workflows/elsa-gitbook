---
description: >-
  A release-3.8 reference for Elsa's management API, .NET client interfaces,
  endpoint permissions, and bookmark-resume callbacks.
---

# API & Client

Use Elsa's HTTP API to automate workflow delivery and operations outside Elsa
Studio. In .NET applications, use the `Elsa.Api.Client` interfaces instead of
constructing request URLs and JSON by hand.

This page is grounded in Elsa `release/3.8.0`. It is a map of the high-value
surfaces, not a replacement for the OpenAPI document when your host exposes
one.

## Choose the right surface

| Need | Use | Notes |
| --- | --- | --- |
| Create, publish, import, export, or query workflow designs | Workflow definitions API | A definition is a versioned design, not a running workflow. |
| Start work now or place it on the dispatcher queue | Execute or dispatch API | `execute` runs immediately; `dispatch` hands work to the runtime queue. |
| Find, inspect, cancel, export, or diagnose executions | Workflow instances API | Use the journal and execution-state endpoints for diagnosis. |
| Discover activity metadata for a designer or integration | Activity descriptors API | Returns the activity catalogue exposed by the server. |
| Continue a waiting workflow from an external callback | Bookmark-resume API | This route is anonymous by design; its token is the capability. |

The API base path is host-configurable. The examples use
`https://elsa.example/elsa/api`; replace that whole prefix with the API base
URL for your deployment.

## Authentication and permissions

Management endpoints are protected by Elsa permission claims when API security
is enabled. For example, listing workflow definitions requires
`read:workflow-definitions`, while listing workflow instances requires
`read:workflow-instances`. Give an integration only the permissions it needs;
do not reuse a Studio administrator token for background services.

Configure authentication and claims before calling these endpoints. See
[Authentication & Authorization](../authentication.md) and
[External Identity Providers](../security/external-identity-providers.md).
For a route map and least-privilege role templates, see [Elsa API
Permissions](../security/permission-reference.md).

The exception is `GET`/`POST /bookmarks/resume`. Elsa deliberately allows this
route anonymously because the encrypted `t` token identifies the bookmark and
instance. Treat a resume URL like a secret: send it only over HTTPS, do not log
it, and use a bounded lifetime when generating it.

## .NET client setup

`Elsa.Api.Client` supplies Refit-based interfaces for the management API. The
API-key helper configures the same default clients and their base address.

```csharp
using Elsa.Api.Client.Extensions;

builder.Services.AddDefaultApiClientsUsingApiKey(options =>
{
    options.BaseAddress = new Uri("https://elsa.example/elsa/api");
    options.ApiKey = builder.Configuration["Elsa:ApiKey"]!;
});
```

For bearer tokens or another authentication scheme, use
`AddDefaultApiClients` and configure the underlying `HttpClient` rather than
adding an API key. Keep the base address at the API root: client routes start
with paths such as `/workflow-definitions` and `/workflow-instances`.

Inject the narrowest interface that matches the job:

```csharp
using Elsa.Api.Client.Resources.WorkflowDefinitions.Contracts;
using Elsa.Api.Client.Resources.WorkflowDefinitions.Requests;

public sealed class WorkflowCatalog(IWorkflowDefinitionsApi definitions)
{
    public async Task ListAsync(CancellationToken cancellationToken)
    {
        await definitions.ListAsync(
            new ListWorkflowDefinitionsRequest { Page = 0, PageSize = 25 },
            cancellationToken: cancellationToken);
    }
}
```

## Core management API map

| Area | HTTP method and route (relative to the API base) | .NET client interface | Typical permission |
| --- | --- | --- | --- |
| List definitions | `GET /workflow-definitions` | `IWorkflowDefinitionsApi.ListAsync` | `read:workflow-definitions` |
| Get a definition by stable ID | `GET /workflow-definitions/by-definition-id/{definitionId}` | `IWorkflowDefinitionsApi.GetByDefinitionIdAsync` | `read:workflow-definitions` |
| Save a definition | `POST /workflow-definitions` | `IWorkflowDefinitionsApi.SaveAsync` | `write:workflow-definitions` |
| Publish a definition | `POST /workflow-definitions/{definitionId}/publish` | `IWorkflowDefinitionsApi.PublishAsync` | `publish:workflow-definitions` |
| Run a definition now | `POST /workflow-definitions/{definitionId}/execute` | `IExecuteWorkflowApi.ExecuteAsync` | `exec:workflow-definitions` |
| Queue a definition to run | `POST /workflow-definitions/{definitionId}/dispatch` | `IExecuteWorkflowApi.DispatchAsync` | `exec:workflow-definitions` |
| List instances | `GET` or `POST /workflow-instances` | `IWorkflowInstancesApi.ListAsync` | `read:workflow-instances` |
| Get an instance | `GET /workflow-instances/{id}` | `IWorkflowInstancesApi.GetAsync` | `read:workflow-instances` |
| Read an instance journal | `GET /workflow-instances/{id}/journal` | `IWorkflowInstancesApi.GetJournalAsync` | `read:workflow-instances` |
| Read filtered journal records | `POST /workflow-instances/{id}/journal` | `IWorkflowInstancesApi.GetFilteredJournalAsync` | `read:workflow-instances` |
| Read execution state | `GET /workflow-instances/{id}/execution-state` | `IWorkflowInstancesApi.GetExecutionStateAsync` | `read:workflow-instances` |
| Cancel an instance | `POST /cancel/workflow-instances/{id}` | `IWorkflowInstancesApi.CancelAsync` | `cancel:workflow-instances` |
| List activity descriptors | `GET /descriptors/activities` | `IActivityDescriptorsApi.ListAsync` | `read:activity-descriptors` |

The table lists the principal routes rather than every bulk, import, export,
reload, and administration endpoint. When your host exposes OpenAPI, use the
document from the exact Elsa version you run for full schemas, optional fields,
and the complete route set.

## Execute versus dispatch

Both operations create work from a workflow definition, but they have different
operational intent:

- Use `execute` when the caller expects Elsa to start the workflow immediately.
- Use `dispatch` when the caller should hand off the request and let the
  configured workflow runtime consume it asynchronously.

Both client methods return `HttpResponseMessage`; inspect the status code and
response before assuming an instance was created. Send input and correlation
data through `ExecuteWorkflowDefinitionRequest` or
`DispatchWorkflowDefinitionRequest`.

```csharp
using Elsa.Api.Client.Resources.WorkflowDefinitions.Contracts;
using Elsa.Api.Client.Resources.WorkflowDefinitions.Requests;

public sealed class OrderWorkflowStarter(IExecuteWorkflowApi workflows)
{
    public async Task StartAsync(string orderId, CancellationToken cancellationToken)
    {
        var response = await workflows.DispatchAsync(
            "process-order",
            new DispatchWorkflowDefinitionRequest
            {
                CorrelationId = orderId,
                Input = new Dictionary<string, object> { ["OrderId"] = orderId }
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
```

## Inspect a running or failed workflow

Start with the instance list and filter by definition ID, correlation ID,
status, sub-status, incident presence, or timestamps. Then choose the detail
view that answers the question:

- **Instance**: current status and high-level instance data.
- **Journal**: chronological execution records; use the filtered journal for
  focused activity or incident investigation.
- **Execution state**: the persisted execution state for deeper runtime
  diagnosis.
- **Export**: a portable diagnostic or migration artifact.

Elsa Studio's workflow-instance viewer uses the same instance and journal
surfaces. Prefer Studio for an interactive investigation; use the API for an
operations integration, report, or controlled automation. For the operating
workflow, see [Troubleshooting](../troubleshooting/README.md) and
[Long-Running Workflows](../running-workflows/long-running-workflows.md).

## Resume a bookmark callback

An activity can generate a bookmark token URL for an approval or external
callback. Resume it with the `t` query value and, optionally, input for the
workflow. `POST` accepts an object containing `input`; `GET` accepts JSON in
the `in` query parameter. The request body is limited to 1 MiB in Elsa 3.8.

```bash
curl -X POST "https://elsa.example/elsa/api/bookmarks/resume?t=YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{"input":{"Decision":"Approved"}}'
```

Add `async=true` to enqueue the resume request through Elsa's bookmark queue;
without it, Elsa calls the workflow resumer directly. A successful accepted
request returns `200 OK`. An invalid or undecryptable token returns a validation
error.

Do not invent a bookmark token from an instance ID. Generate the token from the
activity's bookmark, and make handlers idempotent because network callers can
retry a callback.

## Before you automate

- Verify the host's API base path and authentication scheme.
- Give the client a least-privilege permission set.
- Use definition IDs for stable automation; versions and internal IDs change as
  designs are published.
- Persist the instance ID and correlation ID returned or supplied by your
  integration so operators can find the execution later.
- Prefer `dispatch` for fire-and-forget integrations and inspect the response
  for either path.
- Protect bookmark tokens as credentials, including in logs, browser history,
  support tickets, and analytics.

For release-specific request and response schemas, use the interfaces in the
matching `Elsa.Api.Client` package and, when available, the OpenAPI document
served by your host.
