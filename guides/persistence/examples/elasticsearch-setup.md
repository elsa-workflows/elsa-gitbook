---
description: >-
  Configure the Elsa 3.8.0 Elasticsearch extension for workflow-instance and
  execution-log persistence, with its provider boundaries and production
  caveats.
---

# Elasticsearch Setup Example

Elsa 3.8.0 includes an `Elsa.Persistence.Elasticsearch` extension for teams
that already operate Elasticsearch and want Elasticsearch-backed workflow
instance and workflow execution-log stores. It is not a complete replacement
for every Elsa persistence store: workflow definitions, bookmarks, inbox
messages, activity execution records, and other stores remain backed by the
provider you configure for them.

## When to use it

Choose Elasticsearch when:

- your operations team already runs Elasticsearch and can manage its index,
  mapping, security, and retention lifecycle;
- workflow-instance and execution-log search are important workloads; and
- you are comfortable validating the release implementation's filter and
  update limitations before using it for production operations.

Use [EF Core](efcore-setup.md), [MongoDB](mongodb-setup.md), or
[Dapper](dapper-setup.md) when you need a provider guide for the broader Elsa
management and runtime store set. A mixed setup is possible, but document the
store ownership explicitly so operators know where each type of data lives.

## Packages and endpoint

Install the extension package that matches the rest of your Elsa 3.8.0 package
set:

```bash
dotnet add package Elsa.Persistence.Elasticsearch --version 3.8.0
```

`ElasticsearchOptions.Endpoint` can be either an Elasticsearch URI or the name
of a .NET connection string. The extension first looks up a connection string
with that name and otherwise treats the value as the URI.

For example:

```json
{
  "ConnectionStrings": {
    "Elasticsearch": "https://elasticsearch.example.com:9200"
  },
  "Elasticsearch": {
    "ApiKey": "store-this-in-your-secret-provider"
  }
}
```

Prefer a secret manager, mounted configuration, or environment variables for
credentials. The extension supports either an API key or a username/password
pair. When both are supplied, the API key is selected.

## Configure the stores

The extension has three separate registration points:

- `elsa.UseElasticsearch(...)` registers the shared Elasticsearch client and
  connection options.
- `management.UseElasticsearch()` replaces the workflow-instance store.
- `runtime.UseElasticsearch()` replaces the workflow-execution-log store.

Enable only the stores you intend to move. The following example enables both
Elasticsearch stores while leaving the other Elsa stores with their existing
configuration:

```csharp
using Elsa.Extensions;
using Elsa.Persistence.Elasticsearch.Extensions;
using Elsa.Persistence.Elasticsearch.Modules.Management;
using Elsa.Persistence.Elasticsearch.Modules.Runtime;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddElsa(elsa =>
{
    elsa.UseElasticsearch(options =>
    {
        // Resolves ConnectionStrings:Elasticsearch from configuration.
        options.Endpoint = "Elasticsearch";
        options.ApiKey = builder.Configuration["Elasticsearch:ApiKey"];
    });

    elsa.UseWorkflowManagement(management =>
    {
        // Workflow definitions are not replaced by this call.
        management.UseElasticsearch();
    });

    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.UseElasticsearch();
    });

    elsa.UseWorkflowsApi();
});

var app = builder.Build();
app.UseWorkflows();
app.Run();
```

If you only want execution logs in Elasticsearch, omit the management call.
If you only want workflow instances there, omit the runtime call. Configure a
definition store and the remaining runtime stores separately, using the
[persistence overview](../README.md) to choose an approach.

This snippet is intentionally focused on the Elasticsearch registrations; it
does not by itself make every Elsa store durable. Treat the companion-store
configuration as part of your production persistence design.

## Indexes and naming

The default index name is the dasherized simple document type name:

| Elsa document | Default index |
| --- | --- |
| `WorkflowInstance` | `workflow-instance` |
| `WorkflowExecutionLogRecord` | `workflow-execution-log-record` |

Override names in the shared options when your deployment uses a naming
standard or separate indices:

```csharp
using Elsa.Persistence.Elasticsearch.Options;
using Elsa.Workflows.Management.Entities;
using Elsa.Workflows.Runtime.Entities;

elsa.UseElasticsearch(options =>
{
    options.Endpoint = "Elasticsearch";
    options.IndexNameMappings[typeof(WorkflowInstance)] = "elsa-workflow-instances";
    options.IndexNameMappings[typeof(WorkflowExecutionLogRecord)] = "elsa-execution-logs";
});
```

The release configures a flattened mapping for
`WorkflowInstance.WorkflowState.Properties`. Treat index mappings as a
deployment concern: create and review them before sending production data,
and keep index naming/mapping changes compatible with your rollover and
retention policy.

## Index lifecycle and deployment checks

The release source contains a `ConfigureClientAsync` hook that can create the
workflow-instance index, but the 3.8.0 source tree contains no caller for that
hook. Do not assume that registering the package provisions indices or applies
your production mappings. Before starting Elsa, verify the following in the
target cluster:

1. The endpoint resolves from the deployed configuration.
2. The Elsa identity has permission to read, write, search, and delete the
   selected indices as required by your operations.
3. The expected indices exist with reviewed mappings, or your cluster's
   auto-create policy is an intentional part of the deployment.
4. Index templates, rollover, snapshots, and retention are owned by the same
   operational process that owns the Elasticsearch cluster.
5. A test workflow can be created, queried, updated, and deleted before the
   provider is used for production workloads.

The extension does not configure Elasticsearch retention, rollover, snapshots,
or cluster availability. Use the Elasticsearch operational controls approved
by your organization.

## Release-backed limits to test

The Elasticsearch workflow-instance store in 3.8.0 does not implement every
`WorkflowInstanceFilter` option. Its source handles the singular identity,
version, correlation, status, sub-status, and search-term fields, while
collection-based ID filters and parent-instance filters remain TODO. The
store's `UpdateUpdatedTimestampAsync` method also throws
`NotImplementedException`. Avoid assuming that every Elsa feature that uses a
workflow-instance filter or timestamp-only update has the same behavior as an
EF Core or MongoDB store.

The execution-log store supports filtering by workflow instance ID, activity
ID, and event name. Other log-query requirements should be verified against
the API and the release source before you promise them to operators.

## Troubleshooting

### Elsa cannot connect

- Confirm that `Endpoint` is either a valid absolute URI or the exact
  `ConnectionStrings` key.
- Confirm TLS, DNS, firewall, and cluster health from the Elsa host.
- Confirm that the deployed API key or username/password has not been placed
  in source control and is accepted by the cluster.

### Documents are rejected or searches return unexpected results

- Inspect the actual index name after applying `IndexNameMappings`.
- Compare the index mapping with the workflow-instance state and execution
  log documents emitted by your application.
- Check that the request uses a filter supported by the release store; a
  different persistence provider may support a broader filter set.

### Studio shows incomplete data

The release Studio source has no Elasticsearch provider reference. Studio
consumes the Elsa API, so confirm that the server has the intended provider
registration, all required companion stores, and the permissions needed by the
Studio user. Elasticsearch configuration belongs to the server deployment,
not to the Studio browser package.

## Release source

The behavior described here is based on the `release/3.8.0` sources:

- [`ModuleExtensions.cs`](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/persistence/Elsa.Persistence.Elasticsearch/Extensions/ModuleExtensions.cs)
  and [`ElasticsearchFeature.cs`](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/persistence/Elsa.Persistence.Elasticsearch/Features/ElasticsearchFeature.cs)
  define endpoint, authentication, and client setup.
- [`ElasticWorkflowInstanceFeature.cs`](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/persistence/Elsa.Persistence.Elasticsearch/Modules/Management/ElasticWorkflowInstanceFeature.cs)
  and [`WorkflowInstanceStore.cs`](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/persistence/Elsa.Persistence.Elasticsearch/Modules/Management/WorkflowInstanceStore.cs)
  define the workflow-instance store and its filter behavior.
- [`ElasticExecutionLogRecordFeature.cs`](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/persistence/Elsa.Persistence.Elasticsearch/Modules/Runtime/ElasticExecutionLogRecordFeature.cs)
  and [`WorkflowExecutionLogStore.cs`](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/persistence/Elsa.Persistence.Elasticsearch/Modules/Runtime/WorkflowExecutionLogStore.cs)
  define the execution-log store.
- Core's [`WorkflowManagementFeature`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Management/Features/WorkflowManagementFeature.cs)
  and [`WorkflowRuntimeFeature`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Runtime/Features/WorkflowRuntimeFeature.cs)
  provide the management/runtime composition points.

## Related guidance

- [Persistence overview](../README.md)
- [MongoDB setup](mongodb-setup.md)
- [Dapper setup](dapper-setup.md)
- [Investigate a workflow instance](../../../operate/workflow-state-and-journal.md)
- [Monitoring and observability](../../../operate/monitoring-observability.md)
