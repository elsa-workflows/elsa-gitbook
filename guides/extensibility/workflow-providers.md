---
description: >-
  Explain how Elsa workflow providers load workflow definitions from code,
  files, or external systems and how reload, persistence, and Studio fit
  together in Elsa 3.8.0.
---

# Workflow Providers

Use a workflow provider when workflow definitions come from code, files, a
catalog, or another external system and should be loaded into Elsa's normal
workflow-definition store. This guide describes the behavior in
`release/3.8.0`.

A provider is a server-side integration point. Elsa Studio does not discover or
execute provider classes directly; after the server imports the definitions,
Studio sees them through the ordinary workflow-definition API.

## Choose the right extension point

| Requirement | Use |
| --- | --- |
| Known CLR workflows | `AddWorkflow<T>()` or `AddWorkflowsFrom<TMarker>()` |
| External workflow source | `IWorkflowsProvider` |
| Studio definitions | Persistence and workflow-definition API |
| Load one JSON document and run it immediately | [Loading Workflows from JSON](../loading-workflows-from-json.md) |
| Supply dynamic activity types to the Studio picker | [`IActivityProvider`](activity-type-providers.md) |
| Register CLR types used by workflow data | [Registering Custom Types](custom-types.md) |

`IWorkflowsProvider` supplies already materialized `Workflow` objects. It is
not a replacement for `IWorkflowDefinitionStore`, and it does not itself
define how workflow JSON, ElsaScript, or YAML is parsed. A source that uses a
serialized format normally combines a provider with an
`IWorkflowMaterializer`.

## Understand the runtime path

The release runtime follows this path:

1. Elsa resolves every registered `IWorkflowsProvider`.
2. Each provider returns zero or more `MaterializedWorkflow` values.
3. Elsa filters results to the current tenant or the tenant-agnostic tenant
   (`"*"`).
4. Elsa writes or updates workflow-definition records, preserving the
   provider name, materializer name, materializer context, and original source.
5. Published definitions have their triggers indexed.
6. The activity registry is populated before and after workflow definitions
   are loaded. This second pass makes workflows marked **Usable as an
   activity** available to other workflows.

The startup task performs this population automatically in a hosted Elsa
application. Applications that do not run the startup tasks can call
`PopulateRegistriesAsync` as described in the
[JSON loading guide](../loading-workflows-from-json.md).

The provider result is represented by this release contract:

```csharp
public record MaterializedWorkflow(
    Workflow Workflow,
    string ProviderName,
    string MaterializerName,
    object? MaterializerContext = null,
    string? OriginalSource = null);
```

`ProviderName` is persisted with the definition and should identify the source
that owns it. `MaterializerName` must identify a materializer registered on
every server that must load or edit the definition. Set `OriginalSource` when
round-trip preservation of the source representation matters.

## Implement a provider

The provider contract has a name and one asynchronous snapshot method. The
following example builds workflows from records returned by an application
catalog. The catalog and record types are application code; the builder and
`MaterializedWorkflow` types are Elsa 3.8.0 APIs.

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Runtime;

public sealed record CatalogWorkflow(
    string DefinitionId,
    string VersionId,
    int Version,
    string Name,
    string Message,
    string? TenantId);

public interface IWorkflowCatalog
{
    Task<IReadOnlyList<CatalogWorkflow>> ListAsync(CancellationToken cancellationToken);
}

public sealed class CatalogWorkflowsProvider(
    IWorkflowCatalog catalog,
    IWorkflowBuilderFactory workflowBuilderFactory) : IWorkflowsProvider
{
    public string Name => "AcmeCatalog";

    public async ValueTask<IEnumerable<MaterializedWorkflow>> GetWorkflowsAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await catalog.ListAsync(cancellationToken);
        var results = new List<MaterializedWorkflow>();

        foreach (var record in records)
        {
            var builder = workflowBuilderFactory.CreateBuilder();
            builder.DefinitionId = record.DefinitionId;
            builder.Id = record.VersionId;
            builder.Version = record.Version;
            builder.Name = record.Name;
            builder.Root = new WriteLine(record.Message);

            var workflow = await builder.BuildWorkflowAsync(cancellationToken);
            workflow.Identity = workflow.Identity with
            {
                TenantId = record.TenantId ?? "*"
            };

            results.Add(new(
                workflow,
                ProviderName: Name,
                MaterializerName: "Catalog",
                MaterializerContext: record));
        }

        return results;
    }
}
```

The sample uses the name `Catalog` for a custom materializer. Register an
`IWorkflowMaterializer` with that name that can reconstruct the workflow from
the persisted `MaterializerContext`; otherwise the definition can be imported
but cannot be materialized later for execution or editing. A provider that
loads registered CLR workflow types can instead follow the built-in CLR
provider's pattern and persist a `ClrWorkflowMaterializerContext`.

Register the provider with the current extension method:

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseWorkflowManagement()
    .UseWorkflowRuntime());

builder.Services.AddWorkflowsProvider<CatalogWorkflowsProvider>();
```

`AddWorkflowsProvider<T>()` registers the provider as a scoped
`IWorkflowsProvider`. The older `AddWorkflowDefinitionProvider<T>()` method is
obsolete in `release/3.8.0`; use `AddWorkflowsProvider<T>()` in new code.

### Code-first workflows

For code-first workflows, use the built-in CLR provider instead of writing a
second provider. `AddWorkflow<T>()` and `AddWorkflowsFrom<TMarker>()` register
workflow types in runtime options. The CLR provider builds them, assigns a
default definition ID from the workflow type name when needed, uses
`{TypeName}:v{Version}` as the default version ID, and treats an unspecified
tenant as tenant-agnostic.

### Serialized or file-backed workflows

For JSON, ElsaScript, YAML, or another serialized format, keep parsing in a
materializer and let the provider return the resulting workflow plus its
source metadata. The release Blob Storage provider illustrates this design:
it lists files recursively, filters by the extensions supported by its format
handlers, and returns only files accepted by a handler. Its built-in feature
registers the provider with `AddWorkflowsProvider<BlobStorageWorkflowsProvider>()`.

The [JSON loading guide](../loading-workflows-from-json.md) shows the package
setup for Blob Storage. Use this provider guide when you need to implement a
different source or need to understand reload and persistence behavior.

## Identity, versions, and tenants

Treat these values as part of the persisted workflow contract:

- `DefinitionId` identifies the logical workflow across versions.
- `Id` identifies one version record and must remain stable for that version.
- `Version` controls latest-version selection.
- `TenantId` scopes the definition. Use `"*"` for a tenant-agnostic workflow.
- `ProviderName` identifies the source that materialized the workflow.

When Elsa imports a result, it updates the latest and published flags for the
same definition ID and version. If an older version is returned after a newer
latest or published version, Elsa does not demote the newer version. Providers
should therefore return a complete, deterministic snapshot with stable IDs
and versions on every node.

The store populator does not treat an omitted result as a delete operation. If
a source removes a workflow, delete or retract the corresponding persisted
definition through the workflow-definition management APIs, or implement that
reconciliation explicitly in the application. A provider reload re-reads what
the provider returns; it is not a general source-to-store garbage collector.

## Reload versus refresh

These operations have different purposes:

| Operation | Calls providers? | Effect |
| --- | --- | --- |
| Reload | Yes | Loads providers and registries. |
| Refresh | No | Re-indexes stored published triggers. |

The operations use these routes:

- `POST /actions/workflow-definitions/reload` re-populates registries and
  workflow definitions from all providers, then publishes a reload
  notification.
- `POST /actions/workflow-definitions/refresh` re-indexes triggers for
  published definitions already in the store; it does not re-read an external
  provider.

The reload endpoint requires `actions:workflow-definitions:reload`. In a
distributed host, Elsa uses a distributed lock so only one node performs the
reload at a time.

The refresh endpoint requires `actions:workflow-definitions:refresh`. Send an
optional list of definition IDs to limit the operation; when it is omitted,
all definitions are processed. The endpoint processes definitions in batches
of 10 and can return `202 Accepted` when another refresh is already in
progress.

After changing the external source, use reload. Use refresh when the stored
published definitions are correct but trigger indexes need rebuilding.

## How Studio users experience provider-backed workflows

Studio sees provider-backed definitions only after the Elsa Server has loaded
them into its workflow-definition store. In Studio they appear in the normal
workflow list, with the usual latest/published version behavior. Designers can
edit or publish a definition only when the server exposes the corresponding
management operation and the definition is not read-only.

Provider authors should tell Studio users which fields are authoritative. A
provider that reloads definitions from an external catalog can overwrite edits
when the next reload imports the catalog snapshot. If Studio is the system of
record, use the normal workflow-definition persistence path instead of treating
Studio edits as changes to a read-only external source.

## Production checklist

Before enabling a provider in production, verify:

- every returned workflow has a stable definition ID, version ID, version, and
  tenant assignment;
- the source returns a deterministic snapshot and honors cancellation;
- the workflow can be materialized and executed by every server or worker node;
- `MaterializerName`, `MaterializerContext`, and `OriginalSource` are present
  when the definition must be reopened or round-tripped;
- all providers use distinct source names and do not emit colliding IDs unless
  sharing ownership is intentional;
- published workflows expose valid triggers and reload indexing is tested;
- provider removal and deletion are handled explicitly;
- reload is authorized and operationally safe for the number of definitions;
- Studio users know whether a provider-backed definition is editable or
  overwritten by the external source.

## Release source

This page was checked against the remote `release/3.8.0` branches on 2026-08-04:

- [Core `IWorkflowsProvider`](https://github.com/elsa-workflows/elsa-core/blob/ff10a6810036d9e01e9dce9780b7be3967b6ac0a/src/modules/Elsa.Workflows.Runtime/Contracts/IWorkflowsProvider.cs)
- [Core `MaterializedWorkflow`](https://github.com/elsa-workflows/elsa-core/blob/ff10a6810036d9e01e9dce9780b7be3967b6ac0a/src/modules/Elsa.Workflows.Runtime/Models/MaterializedWorkflow.cs)
- [Core provider registration and code-first registration](https://github.com/elsa-workflows/elsa-core/blob/ff10a6810036d9e01e9dce9780b7be3967b6ac0a/src/modules/Elsa.Workflows.Runtime/Extensions/DependencyInjectionExtensions.cs)
- [Core workflow-definition store populator](https://github.com/elsa-workflows/elsa-core/blob/ff10a6810036d9e01e9dce9780b7be3967b6ac0a/src/modules/Elsa.Workflows.Runtime/Services/DefaultWorkflowDefinitionStorePopulator.cs)
- [Core registry population sequence](https://github.com/elsa-workflows/elsa-core/blob/ff10a6810036d9e01e9dce9780b7be3967b6ac0a/src/modules/Elsa.Workflows.Runtime/Services/DefaultRegistriesPopulator.cs)
- [Core reload endpoint](https://github.com/elsa-workflows/elsa-core/blob/ff10a6810036d9e01e9dce9780b7be3967b6ac0a/src/modules/Elsa.Workflows.Api/Endpoints/WorkflowDefinitions/Reload/Endpoint.cs)
- [Core refresh endpoint](https://github.com/elsa-workflows/elsa-core/blob/ff10a6810036d9e01e9dce9780b7be3967b6ac0a/src/modules/Elsa.Workflows.Api/Endpoints/WorkflowDefinitions/Refresh/Endpoint.cs)
- [Core CLR workflow provider](https://github.com/elsa-workflows/elsa-core/blob/ff10a6810036d9e01e9dce9780b7be3967b6ac0a/src/modules/Elsa.Workflows.Runtime/Providers/ClrWorkflowsProvider.cs)
- [Core Blob Storage workflow provider](https://github.com/elsa-workflows/elsa-core/blob/ff10a6810036d9e01e9dce9780b7be3967b6ac0a/src/modules/Elsa.WorkflowProviders.BlobStorage/Providers/BlobStorageWorkflowsProvider.cs)
- [Studio workflow-definition list](https://github.com/elsa-workflows/elsa-studio/blob/07c506862d17a2fe48913ee79d5cdfc95b3ee820/src/modules/Elsa.Studio.Workflows/Components/WorkflowDefinitionList/WorkflowDefinitionList.razor.cs)

The remote `elsa-extensions` `release/3.8.0` branch was also checked; it has
no `IWorkflowsProvider` implementation relevant to this guide.
