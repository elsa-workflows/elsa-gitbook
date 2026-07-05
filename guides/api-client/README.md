---
description: >-
  Comprehensive guide to interacting with Elsa Server programmatically via HTTP
  APIs and the elsa-api-client library, covering workflow publishing, instance
  management, bookmarks, and resilience pattern
---

# API & Client

## Executive Summary

This guide covers how to interact with Elsa Workflows programmatically using:

1. **HTTP APIs** — Direct REST calls to Elsa Server endpoints
2. **elsa-api-client** — Official .NET client library for type-safe API interactions

### When to Choose Direct HTTP vs Client Library

| Approach            | Best For                                                 | Pros                                                    | Cons                                 |
| ------------------- | -------------------------------------------------------- | ------------------------------------------------------- | ------------------------------------ |
| **Direct HTTP**     | Polyglot teams, non-.NET clients, simple integrations    | Language-agnostic, minimal dependencies                 | Manual serialization, no type safety |
| **elsa-api-client** | .NET applications, complex workflows, production systems | Type-safe, automatic serialization, resilience patterns | .NET-only, additional dependency     |

**Recommendation:** For .NET applications, prefer `elsa-api-client` for type safety and built-in conveniences. For non-.NET clients or simple webhook integrations, use direct HTTP calls.

**Code Reference:** `src/clients/Elsa.Api.Client/` — Official API client implementation.

***

## Architecture Overview

### Core Concepts

Elsa's API is organized around three primary entities:

| Entity                   | Purpose                               | Key Endpoints               |
| ------------------------ | ------------------------------------- | --------------------------- |
| **Workflow Definitions** | Blueprint templates for workflows     | `/api/workflow-definitions` |
| **Workflow Instances**   | Running or completed executions       | `/api/workflow-instances`   |
| **Bookmarks**            | Suspension points for workflow resume | `/api/bookmarks`            |

### High-Level Workflow Lifecycle

```
┌────────────────────────────────────────────────────────────────────────────┐
│                                                                            │
│  Design ──► Publish ──► Instantiate ──► Execute ──► Suspend ──► Resume    │
│                            │                          (bookmark)    │      │
│                            │                              │         │      │
│                            └──────────────────────────────┴─────────┘      │
│                                                           │                │
│                                                           ▼                │
│                                                       Complete             │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

1. **Design** — Create workflow definition (via Studio or programmatically)
2. **Publish** — Activate the definition for execution
3. **Instantiate** — Create a new workflow instance
4. **Execute** — Run activities until completion or suspension
5. **Suspend (Bookmark)** — Workflow pauses at blocking activity, creates bookmark
6. **Resume** — External event triggers bookmark, execution continues
7. **Complete** — Workflow finishes with success or fault status

**Code Reference:** `src/modules/Elsa.Workflows.Core/Contexts/ActivityExecutionContext.cs` — Contains `CreateBookmark()` for suspending workflows.

***

## Authentication & Identity

Elsa Server supports multiple authentication mechanisms. For comprehensive security guidance, see the [Security & Authentication Guide](../security/) (DOC-020).

### Quick Reference

#### Bearer Token (curl)

```bash
curl -X GET "https://your-elsa-server.com/elsa/api/workflow-definitions" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json"
```

#### API Key (curl)

```bash
curl -X GET "https://your-elsa-server.com/elsa/api/workflow-definitions" \
  -H "Authorization: ApiKey YOUR_API_KEY" \
  -H "Content-Type: application/json"
```

#### elsa-api-client Configuration

```csharp
using Elsa.Api.Client.Extensions;

services.AddDefaultApiClientsUsingApiKey(options =>
{
    options.BaseAddress = new Uri("https://your-elsa-server.com/elsa/api");
    options.ApiKey = "YOUR_API_KEY";
});
```

The default API-key handler sends `Authorization: ApiKey <key>`. For bearer tokens, use `AddDefaultApiClients` and set the `Authorization` header in `ConfigureHttpClient`.

**Code Reference:** `src/clients/Elsa.Api.Client/Extensions/DependencyInjectionExtensions.cs` — Client registration extensions.

***

## Publishing Workflow Definitions

### Using elsa-api-client (C#)

The client library provides a type-safe way to create and publish workflow definitions.

```csharp
using Elsa.Api.Client.Resources.WorkflowDefinitions.Contracts;
using Elsa.Api.Client.Resources.WorkflowDefinitions.Models;
using Elsa.Api.Client.Resources.WorkflowDefinitions.Requests;

public class WorkflowPublisher
{
    private readonly IWorkflowDefinitionsApi _api;

    public WorkflowPublisher(IWorkflowDefinitionsApi api)
    {
        _api = api;
    }

    public async Task<WorkflowDefinition> PublishWorkflowAsync()
    {
        var request = new SaveWorkflowDefinitionRequest
        {
            Model = new WorkflowDefinitionModel
            {
                DefinitionId = "my-workflow",
                Name = "My Workflow",
                Description = "A sample workflow",
                Version = 1,
                IsPublished = true,
                Root = new Activity
                {
                    Type = "Elsa.WriteLine",
                    Id = "write-line-1"
                    // Configure activity properties as needed
                },
                Options = new WorkflowOptions
                {
                    CommitStrategyName = "WorkflowExecuted",
                    ActivationStrategyType = "Singleton",
                    AutoUpdateConsumingWorkflows = true
                }
            },
            Publish = true
        };

        var response = await _api.SaveAsync(request);
        return response.WorkflowDefinition;
    }
}
```

**Key Properties:**

| Property                               | Description                                                           |
| -------------------------------------- | --------------------------------------------------------------------- |
| `DefinitionId`                         | Unique identifier for the workflow definition                         |
| `Version`                              | Version number (increments with each publish)                         |
| `Root`                                 | The root activity of the workflow                                     |
| `Options.CommitStrategyName`           | How often workflow state is persisted (see DOC-021)                   |
| `Options.ActivationStrategyType`       | Controls how new instances are created (e.g., `Singleton`, `Default`) |
| `Options.AutoUpdateConsumingWorkflows` | Whether to update workflows that reference this one                   |

**Code Reference:** `src/clients/Elsa.Api.Client/Resources/WorkflowDefinitions/Models/WorkflowOptions.cs`

### HTTP Variant (POST)

```bash
curl -X POST "https://your-elsa-server.com/elsa/api/workflow-definitions" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "model": {
      "definitionId": "my-workflow",
      "name": "My Workflow",
      "description": "A sample workflow",
      "version": 1,
      "isPublished": true,
      "root": {
        "type": "Elsa.WriteLine",
        "id": "write-line-1",
        "text": "Hello, World!"
      },
      "options": {
        "commitStrategyName": "WorkflowExecuted",
        "activationStrategyType": "Default",
        "autoUpdateConsumingWorkflows": true
      }
    },
    "publish": true
  }'
```

**Response (Success - 201 Created):**

```json
{
  "workflowDefinition": {
    "id": "abc123",
    "definitionId": "my-workflow",
    "name": "My Workflow",
    "version": 1,
    "isPublished": true,
    "createdAt": "2025-01-01T00:00:00Z"
  },
  "alreadyPublished": false,
  "consumingWorkflowCount": 0
}
```

***

## Versioning & Publishing Semantics

### Draft vs Published

Workflow definitions have two states:

| State         | Description                 | Can Execute? |
| ------------- | --------------------------- | ------------ |
| **Draft**     | Work-in-progress definition | No           |
| **Published** | Active, executable version  | Yes          |

Multiple versions can exist, but only one version is the "latest published" version at any time.

### Publishing Options

When saving a workflow definition, set `Publish = true` to immediately publish:

```csharp
var request = new SaveWorkflowDefinitionRequest
{
    Model = new WorkflowDefinitionModel { /* ... */ },
    Publish = true  // Immediately publish this version
};
```

### AutoUpdateConsumingWorkflows

When set to `true`, workflows that reference this definition (via Dispatch Workflow activity) will automatically use the new version:

```csharp
Options = new WorkflowOptions
{
    AutoUpdateConsumingWorkflows = true
}
```

**Code Reference:** `src/modules/Elsa.Workflows.Core/Models/WorkflowOptions.cs`

### Activation Strategies

Control how new workflow instances are created:

| Strategy    | Behavior                                 |
| ----------- | ---------------------------------------- |
| `Default`   | Each trigger creates a new instance      |
| `Singleton` | Only one running instance per definition |

See the documentation on Activation Strategies in the main Elsa docs for detailed guidance.

**Code Reference:** `src/clients/Elsa.Api.Client/Resources/WorkflowDefinitions/Models/WorkflowOptions.cs` — `ActivationStrategyType` property.

***

## Executing / Dispatching Workflows

### Using elsa-api-client (C#)

```csharp
using System.Net.Http;
using Elsa.Api.Client.Resources.WorkflowDefinitions.Contracts;
using Elsa.Api.Client.Resources.WorkflowDefinitions.Requests;

public class WorkflowRunner
{
    private readonly IExecuteWorkflowApi _api;

    public WorkflowRunner(IExecuteWorkflowApi api)
    {
        _api = api;
    }

    public async Task<HttpResponseMessage> ExecuteWorkflowAsync(
        string definitionId,
        string? correlationId = null,
        Dictionary<string, object>? input = null)
    {
        var request = new ExecuteWorkflowDefinitionRequest
        {
            CorrelationId = correlationId,
            Input = input
        };

        var response = await _api.ExecuteAsync(definitionId, request);
        response.EnsureSuccessStatusCode();
        return response;
    }
}
```

### HTTP Variant (POST)

```bash
curl -X POST "https://your-elsa-server.com/elsa/api/workflow-definitions/my-workflow/execute" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "correlationId": "order-12345",
    "input": {
      "orderId": "12345",
      "customerEmail": "customer@example.com"
    }
  }'
```

**Response (Success - 200 OK):**

```json
{
  "workflowInstanceId": "inst-abc123",
  "status": "Running"
}
```

Use `/workflow-definitions/{definitionId}/dispatch` with the same request body to enqueue execution asynchronously.

### Key Parameters

| Parameter        | Description                                        | Required |
| ---------------- | -------------------------------------------------- | -------- |
| `definitionId`   | ID of the workflow definition in the route         | Yes      |
| `correlationId`  | External identifier for correlation                | No       |
| `input`          | Dictionary of input values                         | No       |
| `versionOptions` | Which version to run (Latest, Published, Specific) | No       |

> **Note:** In 3.7.0, the API client executes workflow definitions through `IExecuteWorkflowApi.ExecuteAsync` and `IExecuteWorkflowApi.DispatchAsync`. `IWorkflowInstancesApi` is for listing, reading, cancelling, deleting, importing, and exporting workflow instances.

**Code Reference:** `src/clients/Elsa.Api.Client/Resources/WorkflowDefinitions/Contracts/IExecuteWorkflowApi.cs`.

***

## Querying Workflow Definitions & Instances

### Filtering Workflow Instances

The API supports filtering by status, correlationId, definitionId, and version with pagination.

#### Using elsa-api-client (C#)

```csharp
using Elsa.Api.Client.Resources.WorkflowInstances.Contracts;
using Elsa.Api.Client.Resources.WorkflowInstances.Requests;
using Elsa.Api.Client.Resources.WorkflowInstances.Enums;

public class WorkflowQuerier
{
    private readonly IWorkflowInstancesApi _api;

    public WorkflowQuerier(IWorkflowInstancesApi api)
    {
        _api = api;
    }

    public async Task<PagedListResponse<WorkflowInstanceSummary>> QueryByCorrelationAsync(
        string correlationId,
        WorkflowStatus? status = null,
        int page = 0,
        int pageSize = 25)
    {
        var request = new ListWorkflowInstancesRequest
        {
            CorrelationId = correlationId,
            Status = status,
            Page = page,
            PageSize = pageSize
        };

        return await _api.ListAsync(request);
    }
}
```

#### HTTP Variant (curl)

```bash
# Query by correlationId
curl -X POST "https://your-elsa-server.com/elsa/api/workflow-instances" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"correlationId":"order-12345","page":0,"pageSize":25}'

# Query by status
curl -X POST "https://your-elsa-server.com/elsa/api/workflow-instances" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"status":"Running","page":0,"pageSize":25}'

# Query by definitionId and version
curl -X POST "https://your-elsa-server.com/elsa/api/workflow-instances" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"definitionId":"my-workflow","version":2}'
```

### Query Parameters

| Parameter       | Description                                                       | Type    |
| --------------- | ----------------------------------------------------------------- | ------- |
| `status`        | Filter by workflow status (Running, Suspended, Finished, Faulted) | Enum    |
| `correlationId` | Filter by correlation ID                                          | String  |
| `definitionId`  | Filter by workflow definition ID                                  | String  |
| `version`       | Filter by specific version                                        | Integer |
| `page`          | Page number (0-indexed)                                           | Integer |
| `pageSize`      | Number of results per page (default: 25, max: 100)                | Integer |

***

## Bookmarks & Resuming

### How Bookmarks Work

Bookmarks are created when a workflow reaches a blocking activity (e.g., waiting for HTTP callback, human approval, or external event). The workflow suspends until resumed via the bookmark.

**Code Reference:** `src/modules/Elsa.Workflows.Core/Contexts/ActivityExecutionContext.cs` — `CreateBookmark()` method.

### Bookmark Creation

Activities create bookmarks using `ActivityExecutionContext.CreateBookmark()`:

```csharp
// Internal to activity implementation
var bookmark = context.CreateBookmark(
    callback: ResumeAsync,
    payload: new MyBookmarkPayload { Data = "value" },
    options: new CreateBookmarkArgs { AutoBurn = true }
);
```

### Stimulus Hashing

Bookmarks are matched using a deterministic hash based on:

* Activity type name
* Stimulus payload data

This allows the runtime to efficiently find matching bookmarks without knowing the instance ID.

**Code Reference:** `src/modules/Elsa.Workflows.Core/Bookmarks/` — Stimulus hashing implementation (e.g., `StimulusHasher`).

### Resuming Workflows

#### Token-Based Resume (URL)

HTTP triggers generate tokenized URLs for easy resumption:

```bash
# Token-based resume (generated URL)
curl -X POST "https://your-elsa-server.com/elsa/api/bookmarks/resume?t=ENCRYPTED_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"input":{"status":"approved"}}'
```

**Code Reference:** `src/modules/Elsa.Http/Extensions/BookmarkExecutionContextExtensions.cs` — `GenerateBookmarkTriggerUrl` method.

The HTTP endpoint expects the encrypted `t` query-string token generated by Elsa. The token payload contains the bookmark ID and workflow instance ID; it is not a general stimulus-resume API.

See [resume-bookmark.curl.md](examples/resume-bookmark.curl.md) for complete examples.

#### Runtime Service Resume

```csharp
using Elsa.Workflows.Runtime;

await workflowResumer.ResumeAsync(new ResumeBookmarkRequest
{
    WorkflowInstanceId = instanceId,
    BookmarkId = bookmarkId,
    Input = new Dictionary<string, object>
    {
        ["status"] = "approved"
    }
});
```

There is no 3.7.0 `elsa-api-client` resume method on `IWorkflowInstancesApi`; use the generated tokenized URL over HTTP or the runtime services inside the server process.

### Resume Flow & Locking

The `WorkflowResumer` service:

1. Acquires a distributed lock for the bookmark filter
2. Loads the workflow state from the database
3. Finds the matching bookmark
4. Resumes execution from the bookmarked activity
5. Burns (deletes) the bookmark if `AutoBurn = true`

**Code Reference:** `src/modules/Elsa.Workflows.Runtime/Services/WorkflowResumer.cs` — Resume logic and locking.

### Security Note

Tokenized resume URLs should be treated as secrets:

* Use HTTPS to prevent interception
* Tokens expire when the bookmark is burned or workflow is cancelled
* See [Security & Authentication Guide](../security/) (DOC-020) for token security best practices

***

## Incidents & Retry / Resilience

### Resilience Strategies

In `release/3.8.0`, the .NET API client configures activity retry behavior with
`ResilienceStrategyConfig` on `customProperties.resilienceStrategy`.

#### Setting a Resilience Strategy

Use `ActivityExtensions.SetResilienceStrategy(...)` or assign the same payload
directly through `CustomProperties`:

```csharp
using Elsa.Api.Client.Extensions;
using Elsa.Api.Client.Resources.Resilience.Models;
using Elsa.Api.Client.Resources.WorkflowDefinitions.Models;

var activity = new Activity
{
    Type = "Elsa.FlowSendHttpRequest",
    Id = "http-call-1"
};

activity.SetResilienceStrategy(new ResilienceStrategyConfig
{
    Mode = ResilienceStrategyConfigMode.Identifier,
    StrategyId = "http-default"
});
```

The referenced strategy ID must exist in the host's resilience strategy
catalog, for example from `Resilience:Strategies` configuration.

### Handling Incidents

When activities fail, inspect `WorkflowState.Incidents` on the workflow
instance:

```csharp
var instance = await _instancesApi.GetAsync(instanceId);

if (instance.WorkflowState.Incidents.Any())
{
    foreach (var incident in instance.WorkflowState.Incidents)
    {
        Console.WriteLine($"Activity {incident.ActivityId} failed: {incident.Message}");
    }
}
```

For workflow-level incident handling and operator retry guidance, see
[Incidents](../../operate/incidents/README.md).

***

## Commit Strategies

Commit strategies control when workflow state is persisted to the database. This affects durability and performance.

### Selecting a Strategy

When creating a workflow definition, specify the commit strategy:

```csharp
Options = new WorkflowOptions
{
    CommitStrategyName = "WorkflowExecuted"  // Minimal commits, highest throughput
}
```

### Available Strategies

| Strategy           | Behavior                         | Use Case                         |
| ------------------ | -------------------------------- | -------------------------------- |
| `WorkflowExecuted` | Commits after workflow completes | High throughput, short workflows |
| `ActivityExecuted` | Commits after each activity      | Maximum durability               |
| `Periodic`         | Commits at regular intervals     | Long-running workflows           |

For detailed guidance on commit strategies and performance tuning, see [Performance & Scaling Guide](../performance/) (DOC-021).

**Code Reference:** `src/modules/Elsa.Workflows.Core/CommitStates/CommitStrategiesFeature.cs` — Strategy registration.

***

## Pagination & Performance

### Pagination Parameters

All list endpoints support pagination:

| Parameter  | Description              | Default | Max |
| ---------- | ------------------------ | ------- | --- |
| `page`     | Zero-indexed page number | 0       | -   |
| `pageSize` | Results per page         | 25      | 100 |

### Example with Pagination

```bash
# First page
curl -X POST "https://your-elsa-server.com/elsa/api/workflow-instances" \
  -H "Content-Type: application/json" \
  -d '{"page":0,"pageSize":50}'

# Second page
curl -X POST "https://your-elsa-server.com/elsa/api/workflow-instances" \
  -H "Content-Type: application/json" \
  -d '{"page":1,"pageSize":50}'
```

### Performance Recommendations

1.  **Use Server-Side Filtering:** Always filter on the server rather than fetching all results and filtering client-side.

    ```bash
    # Good: Server-side filtering
    curl -X POST "https://your-elsa-server.com/elsa/api/workflow-instances" \
      -H "Content-Type: application/json" \
      -d '{"status":"Running","correlationId":"order-123"}'

    # Avoid: Fetching all and filtering client-side
    ```
2. **Limit Page Size:** Use the smallest page size that meets your needs to reduce response time and memory usage.
3. **Avoid Large Instance Graphs:** When querying instances, request only summary data unless you need the full execution history.
4. **Consider Field Selection (Future):** Future versions may support field selection to reduce payload size. Check release notes for updates.

***

## Error Handling & Troubleshooting

### Common HTTP Status Codes

| Status               | Meaning               | Common Cause                                                   |
| -------------------- | --------------------- | -------------------------------------------------------------- |
| **200 OK**           | Success               | -                                                              |
| **201 Created**      | Resource created      | -                                                              |
| **400 Bad Request**  | Validation error      | Missing required field (e.g., `root` activity), malformed JSON |
| **401 Unauthorized** | Authentication failed | Invalid or expired token                                       |
| **404 Not Found**    | Resource not found    | Definition ID doesn't exist                                    |
| **409 Conflict**     | Publish conflict      | Version conflict during concurrent publish                     |

### Validation Errors (400)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "root": ["The Root field is required."],
    "definitionId": ["The DefinitionId field is required."]
  }
}
```

### Bookmark Resume Does Not Continue

Common causes:

* Bookmark already burned (consumed)
* Workflow instance was cancelled
* Token expired or invalid

**Resolution:**

1. Verify the workflow instance still exists and is suspended
2. Check if the bookmark was already consumed
3. Verify the resume URL includes the encrypted `t` token generated for that bookmark

### Troubleshooting Checklist

1. **Check logs** for detailed error messages
2. **Verify authentication** with a simple GET request
3. **Validate JSON** syntax before sending
4. **Check workflow state** via instance query
5. **Review bookmark existence** in database

For comprehensive troubleshooting, see [Troubleshooting Guide](../troubleshooting/) (DOC-017).

***

## Best Practices Summary

### Correlation IDs

* **Use correlation IDs** for multi-event workflows to track related activities
* Choose meaningful, unique identifiers (e.g., order ID, customer ID)
* Query by correlation ID for efficient instance lookup

```csharp
var request = new ExecuteWorkflowDefinitionRequest
{
    CorrelationId = $"order-{orderId}"  // Use meaningful ID
};

await executeWorkflowApi.ExecuteAsync("order-processing", request);
```

### Idempotent Resume Handlers

Design resume handlers to be idempotent (safe to call multiple times):

```csharp
// Check if action already performed
if (await HasAlreadyProcessedAsync(bookmarkId))
{
    return; // Skip duplicate processing
}

await ProcessAsync(input);
await MarkAsProcessedAsync(bookmarkId);
```

### Commit Strategy Selection

Choose commit strategy based on your durability vs throughput requirements:

| Requirement                          | Recommended Strategy |
| ------------------------------------ | -------------------- |
| High throughput, short workflows     | `WorkflowExecuted`   |
| Long-running, must not lose progress | `ActivityExecuted`   |
| Balanced for most use cases          | `Periodic`           |

### Client Instrumentation

For custom metrics and monitoring:

* **User-defined metrics** for throughput, latency, error rates
* **Built-in OpenTelemetry instrumentation** via `Elsa.Workflows` for workflow execution visibility

See [Performance & Scaling Guide](../performance/) and [Monitoring & Observability](../../operate/monitoring-observability.md) for observability patterns.

### General Recommendations

1. **Prefer elsa-api-client** for .NET applications
2. **Handle transient failures** with retry policies
3. **Use HTTPS** for all API calls
4. **Paginate results** to avoid memory issues
5. **Monitor workflow health** via status queries
6. **Clean up completed instances** via retention policies

***

## Example Files

* [resume-bookmark.curl.md](examples/resume-bookmark.curl.md) — Resume workflow via tokenized bookmark URL
* [Source File References](README-REFERENCES.md) — elsa-core source paths for grounding

## Related Documentation

* [Security & Authentication Guide](../security/) (DOC-020) — Authentication, tokenized URLs, security
* [Performance & Scaling Guide](../performance/) (DOC-021) — Commit strategies, observability
* [Persistence Guide](../persistence/) (DOC-022) — Database configuration
* [Troubleshooting Guide](../troubleshooting/) (DOC-017) — Diagnosing common issues
* [Clustering Guide](../clustering/) (DOC-015) — Distributed deployment

***

**Last Updated:** 2025-11-28
