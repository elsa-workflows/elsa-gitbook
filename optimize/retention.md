---
description: >-
  Configure release-backed retention policies that remove workflow instances
  and their related runtime records.
---

# Retention

The `Elsa.Retention` extension runs a recurring, server-side cleanup job. A
policy selects workflow instances with a `RetentionWorkflowInstanceFilter`,
then the default deletion strategies remove the matching instance and its
related bookmarks, activity execution records, and workflow execution logs.
Retention is destructive: it is not an archive, backup, or preview feature.

Use it when you have an explicit data-lifecycle policy for completed or
otherwise disposable instances. Keep long-running, incident-bearing, and
audit-sensitive instances outside a deletion policy until you have verified
the filter against representative data.

## Install and enable the module

Add the extension to the server that owns the workflow management and runtime
stores:

```bash
dotnet add package Elsa.Retention --version 3.8.0
```

Register it inside `AddElsa`. The retention module does not add a deletion
policy by itself; no instances are selected until you call `AddDeletePolicy`
or register a custom `IRetentionPolicy`.

```csharp
using Elsa.Common;
using Elsa.Extensions;
using Elsa.Retention.Extensions;
using Elsa.Retention.Models;
using Elsa.Workflows;
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddElsa(elsa =>
{
    // Configure UseWorkflowManagement and UseWorkflowRuntime with
    // the persistence providers used by this host.

    elsa.UseRetention(retention =>
    {
        retention.SweepInterval = TimeSpan.FromHours(4);
        retention.ConfigureCleanupOptions = options =>
        {
            options.PageSize = 25;
        };

        retention.AddDeletePolicy("Finished workflows", _ =>
            new RetentionWorkflowInstanceFilter
            {
                WorkflowStatus = WorkflowStatus.Finished
            });
    });
});
```

In 3.8.0, `SweepInterval` defaults to four hours and `CleanupOptions.PageSize`
defaults to 25. `PageSize` is the number of workflow instances loaded into a
cleanup batch; it is not a limit on the total number of instances a policy can
delete. The filter factory is called on each cleanup run, so resolve services
or calculate a moving time threshold inside the factory.

For the released registration and defaults, see [`UseRetention` and
`RetentionFeature`](https://github.com/elsa-workflows/elsa-extensions/tree/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/retention/Elsa.Retention).

## Define a bounded deletion policy

`AddDeletePolicy` takes a name and a factory returning
`RetentionWorkflowInstanceFilter`. The filter supports:

- workflow definition ID or definition version ID, including collections of
  IDs;
- correlation ID, including a collection of IDs;
- one workflow status or a collection of statuses;
- one workflow sub-status or a collection of sub-statuses;
- `SearchTerm` matching instance name, definition IDs, version ID, instance ID,
  or correlation ID;
- `HasIncidents` and `IsSystem`; and
- timestamp filters on `CreatedAt`, `UpdatedAt`, or `FinishedAt`.

Combine a lifecycle status with an age condition so a newly finished instance
is not removed immediately:

```csharp
using Elsa.Common;
using Elsa.Retention.Models;
using Elsa.Workflows;
using Elsa.Workflows.Management.Entities;
using Elsa.Workflows.Management.Enums;
using Elsa.Workflows.Management.Models;
using Microsoft.Extensions.DependencyInjection;

retention.AddDeletePolicy("Finished invoices after 30 days", sp =>
{
    var clock = sp.GetRequiredService<ISystemClock>();

    return new RetentionWorkflowInstanceFilter
    {
        DefinitionId = "invoice-workflow",
        WorkflowStatus = WorkflowStatus.Finished,
        TimestampFilters =
        [
            new TimestampFilter
            {
                Column = nameof(WorkflowInstance.FinishedAt),
                Operator = TimestampFilterOperator.LessThanOrEqual,
                Timestamp = clock.UtcNow.AddDays(-30)
            }
        ]
    };
});
```

`RetentionWorkflowInstanceFilter` is converted to Core's
`WorkflowInstanceFilter` before querying the configured
`IWorkflowInstanceStore`. Timestamp columns are validated by Core; arbitrary
property names are not accepted. A timestamp filter with a date-only value
has the release's day-boundary semantics, so use a precise `DateTimeOffset`
when you need an exact age threshold.

## What the default cleanup removes

For each page of matching workflow instances, the built-in deletion policy
first collects and deletes related records, then deletes the workflow
instances. The 3.8.0 module registers these default pairs:

- `StoredBookmark` → `IBookmarkStore`
- `ActivityExecutionRecord` → `IActivityExecutionStore`
- `WorkflowExecutionLogRecord` → `IWorkflowExecutionLogStore`
- `WorkflowInstance` → `IWorkflowInstanceStore`

The related records are loaded in smaller chunks by their collectors. A
provider must therefore support the corresponding `FindMany` and delete
operations for the policy to complete cleanly. The cleanup job logs and
continues past a query failure for that policy, but missing cleanup strategies
or a failed workflow-instance deletion are configuration or implementation
errors that must be investigated.

The recurring task is marked `[SingleNodeTask]`. Elsa's task executor acquires
a distributed lock for such tasks, so a clustered host should configure a
shared distributed lock provider. This prevents every node from performing
the same retention sweep. The lock does not make deletion recoverable and does
not replace backups or a database transaction strategy.

## Extend retention safely

Use `IRelatedEntityCollector<TEntity>` when a custom entity belongs to a
workflow instance and must be cleaned up with it. Register a collector and the
matching `IDeletionCleanupStrategy<TEntity>`:

```csharp
using Elsa.Retention.Contracts;
using Elsa.Workflows.Management.Entities;
using Microsoft.Extensions.DependencyInjection;

public sealed class CustomDataCollector(ICustomDataStore store)
    : IRelatedEntityCollector<CustomWorkflowData>
{
    public async IAsyncEnumerable<ICollection<CustomWorkflowData>>
        GetRelatedEntities(ICollection<WorkflowInstance> instances)
    {
        var ids = instances.Select(x => x.Id).ToArray();
        yield return (await store.FindByWorkflowInstanceIdsAsync(ids)).ToArray();
    }
}

public sealed class DeleteCustomDataStrategy(ICustomDataStore store)
    : IDeletionCleanupStrategy<CustomWorkflowData>
{
    public async Task Cleanup(ICollection<CustomWorkflowData> collection)
    {
        await store.DeleteManyAsync(collection.Select(x => x.Id).ToArray());
    }
}

builder.Services.AddScoped<IRelatedEntityCollector<CustomWorkflowData>, CustomDataCollector>();
builder.Services.AddScoped<IDeletionCleanupStrategy<CustomWorkflowData>, DeleteCustomDataStrategy>();
```

The interfaces are intentionally generic. The collector returns entities for
the current batch; the deletion strategy receives those entities and performs
the cleanup. Make the operation idempotent because retries or an interrupted
process can cause the same logical cleanup to be attempted again.

To implement a non-deletion policy, define a marker strategy interface such as
`IArchiveCleanupStrategy<TEntity> : ICleanupStrategy<TEntity>`, implement that
interface for every entity the policy must handle, and set
`IRetentionPolicy.CleanupStrategy` to the open generic marker type. The module
uses that marker to resolve strategies for related entities and the workflow
instance itself. An archive policy is not supplied by the release; you own its
storage, failure handling, and verification.

## Operational checklist

- Start with a narrow definition ID and a generous age threshold.
- Test the filter against a copy or read-only query before enabling deletion.
- Exclude instances with incidents, active work, or audit obligations unless
  the business policy explicitly permits their removal.
- Use a durable, shared persistence provider and distributed lock provider in a
  multi-node deployment.
- Set `PageSize` to fit the database and related-record query load; larger
  batches reduce scheduling overhead but increase deletion work per sweep.
- Monitor cleanup logs and the provider's delete counts. Retention does not
  archive data, undo a deletion, or guarantee recovery after a failed sweep.

## Release references

- [`Elsa.Retention` module](https://github.com/elsa-workflows/elsa-extensions/tree/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/retention/Elsa.Retention)
- [`CleanupJob`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/retention/Elsa.Retention/Jobs/CleanupJob.cs)
- [`RetentionWorkflowInstanceFilter`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/retention/Elsa.Retention/Models/RetentionWorkflowInstanceFilter.cs)
- [`WorkflowInstanceFilter` timestamp validation](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/modules/Elsa.Workflows.Management/Filters/WorkflowInstanceFilter.cs)
- [`SingleNodeTaskAttribute` execution](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/modules/Elsa.Common/Multitenancy/Implementations/TaskExecutor.cs)
