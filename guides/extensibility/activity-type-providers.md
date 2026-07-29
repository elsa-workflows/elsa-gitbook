---
description: >-
  Explain how Elsa activity type providers create descriptors, how the runtime
  refreshes them, and how Elsa Studio uses them to design and run workflows.
---

# Activity Type Providers

Use an activity type provider when the activities available to Elsa are not a
fixed list of CLR types. Providers are useful for activities generated from
external metadata, message contracts, tenant configuration, or workflow
definitions that are marked as reusable activities.

This page describes the behavior in Elsa `release/3.8.0`. It complements the
[custom activities guide](../../extensibility/custom-activities.md), which
covers how to implement the activity that a provider ultimately constructs.

## Understand the boundary

There are two related but different registration paths:

- For a known set of activity classes, use `AddActivitiesFrom<T>()`,
  `AddActivity<T>()`, or `services.AddActivitiesFrom<T>()`.
- For descriptors generated from runtime or external data, implement
  `IActivityProvider` and register it with `AddActivityProvider<T>()`.

An `IActivityProvider` is a server-side source of descriptors:

```csharp
using Elsa.Workflows.Models;

public interface IActivityProvider
{
    ValueTask<IEnumerable<ActivityDescriptor>> GetDescriptorsAsync(
        CancellationToken cancellationToken = default);
}
```

An `ActivityDescriptor` is the contract between the runtime and Studio. It
contains the activity type name, version, display metadata, inputs, outputs,
ports, category, and the constructor Elsa uses to create the runtime activity.
The constructor is server-side data; it is not sent to Studio as JSON.

The provider does not make Studio execute arbitrary code. Studio receives
descriptor metadata from the Elsa Server, while every server or worker that
can execute the workflow must have the corresponding activity implementation
and provider available.

## Implement a provider

For a provider that exposes ordinary CLR activities, use Elsa's
`IActivityDescriber`. It creates the descriptor from the activity's attributes,
inputs, outputs, and constructor behavior, so the metadata used by Studio
stays aligned with the activity implementation.

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Management;
using Elsa.Workflows.Models;

public sealed class ReportingActivityProvider(
    IActivityDescriber activityDescriber) : IActivityProvider
{
    public async ValueTask<IEnumerable<ActivityDescriptor>> GetDescriptorsAsync(
        CancellationToken cancellationToken = default)
    {
        var descriptor = await activityDescriber.DescribeActivityAsync(
            typeof(GenerateReport), cancellationToken);

        return [descriptor];
    }
}
```

Register the provider in the server's dependency-injection container:

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseWorkflowManagement()
    .UseWorkflowRuntime());

builder.Services.AddActivityProvider<ReportingActivityProvider>();
```

`AddActivityProvider<T>()` registers `T` and exposes it as an
`IActivityProvider`. Elsa's workflow management feature registers the built-in
providers in the same way. A custom module can put the registration in its
feature instead:

```csharp
public override void Apply()
{
    Services.AddActivityProvider<ReportingActivityProvider>();
}
```

The provider should return descriptors that are executable by the same server.
If it creates descriptors from external metadata instead of a CLR type, the
descriptor still needs a stable `TypeName`, an intentional `Version`, and a
`Constructor` that can create a concrete activity with the metadata needed to
run it. Do not return a Studio-only descriptor for an activity that the server
cannot deserialize and execute.

## Dynamic descriptors

A dynamic provider commonly maps one external type to one or more Elsa
activities. For example, the Elsa 3.8.0 MassTransit extension registers
`MassTransitActivityTypeProvider`. For each configured message type it creates:

- a `MessageReceived` trigger descriptor with the message type as its output;
- a `PublishMessage` action descriptor when the message type is a class.

The extension registers the provider with `AddActivityProvider` and gets its
message types from `MassTransitActivityOptions`. The provider also derives
display names, categories, descriptions, inputs, and outputs from the message
type and its attributes.

This pattern is useful when a domain specialist should see one activity per
business message, event, or external capability. Keep the mapping deterministic:
the same external type should produce the same Elsa `TypeName` and version on
every server node.

## Refresh and startup behavior

The server stores provider output in `IActivityRegistry`. During a refresh, Elsa
queries each registered provider, groups the returned descriptors by tenant,
replaces that provider's previous descriptors for each returned tenant, and
adds the new set. The registry keeps descriptors by `(TypeName, Version)` and
also tracks the latest version for each type. A provider should return a
complete current snapshot for each tenant it serves; do not rely on an omitted
tenant group being removed automatically.

At startup, the runtime populates the registry as part of its registry startup
task. In `release/3.8.0`, the default population sequence is important when
workflows can themselves be used as activities:

1. populate normal activity descriptors;
2. load workflow definitions;
3. populate the activity registry again so reusable workflow definitions appear;
4. update the workflow-definition store with the current activity set;
5. notify the runtime when workflow definitions have been reloaded.

The workflow-management provider for reusable workflow definitions is refreshed
when definitions are published, retracted, or deleted. This is why a workflow
marked **Usable as an activity** can appear in the activity picker after it is
published, while retracting or deleting it removes the corresponding
descriptor.

If an external source changes after startup, refresh the registry explicitly:

```http
GET /descriptors/activities?refresh=true
```

The endpoint requires `read:*` or `read:activity-descriptors`. It refreshes the
server registry before returning the descriptor list. Make sure the provider
reads current data and that all nodes share the same source and configuration.

## How Elsa Studio consumes providers

Studio does not discover provider classes directly. Its remote activity
registry calls the server's descriptors endpoint with `refresh=true` and caches
the returned descriptors. Studio refreshes that registry when it receives
workflow definition save, publish, retract, or delete notifications.

The designer then uses the descriptor metadata to:

- show only descriptors with `IsBrowsable = true` in the activity picker;
- group activities by `Category` and filter by name, type, display name, or
  description;
- render input and output editors;
- display ports, descriptions, icons, and activity labels;
- resolve the exact `TypeName` and `Version` for an activity already stored in a
  workflow.

Studio lists the latest descriptor for each type in the picker, but it retains
all returned versions in its registry. When a persisted activity has an exact
version, Studio looks up that version. If the server does not provide it, the
designer can fall back to an unknown-activity representation rather than
silently changing the workflow to a different implementation.

## Versioning and persistence

Treat `TypeName` and `Version` as part of the persisted workflow contract.

- Keep the existing type name and version when the behavior remains compatible.
- Add a new version when the activity's behavior or serialized shape is not
  compatible with workflows that already use it.
- Keep old descriptor versions available for as long as persisted workflows may
  contain them.
- Deploy the provider and implementation to every server that may load or
  resume those workflows.

The registry selects the highest version for an unversioned lookup. A duplicate
`(TypeName, Version)` from another provider replaces the existing descriptor
and logs a warning, so provider authors should coordinate names and versions
instead of relying on registration order.

Tenant-aware providers must also be deliberate about `TenantId`. Tenant-agnostic
descriptors are shared; tenant-specific descriptors take precedence for that
tenant. Do not emit different descriptors for the same tenant and type unless
the version and behavior make that difference explicit.

## Troubleshooting

### The activity is missing from Studio

1. Confirm the provider is registered as `IActivityProvider` on the Elsa Server.
2. Call `GET /descriptors/activities?refresh=true` with a permitted identity and
   check whether the descriptor is returned.
3. Check `IsBrowsable`, `Category`, and `TypeName` in the response.
4. Confirm Studio is connected to that server and reload its activity registry.

### The activity appears but cannot run

The descriptor is metadata; it does not install an implementation. Confirm the
server can construct the activity, resolve its dependencies, and deserialize
the persisted `TypeName` and `Version`. Install the provider and activity
assembly on every worker that can execute the workflow.

### A newly added external activity is not visible

Refresh the registry after the external source changes. If the provider uses
options, verify the configured message/type set is the one used by the server
process. For multiple nodes, check that the source and options are consistent
across nodes.

### An existing workflow shows an unknown activity

The server or Studio cannot find the exact persisted type/version. Restore that
descriptor version or provide a deliberate migration. Do not fix the symptom by
silently pointing the old type name at a new incompatible implementation.

## Provider checklist

Before shipping a provider, verify:

- the provider is registered on the server and all workflow workers;
- each descriptor has a stable type name, category, display metadata, and
  version;
- inputs, outputs, ports, and UI hints match the executable activity;
- the descriptor constructor creates the correct concrete activity;
- descriptor refresh replaces the provider's entries for each returned tenant
  without removing entries owned by another provider;
- existing workflow definitions can still resolve their stored type/version;
- Studio can list, configure, save, and reopen the activity;
- the provider's external source and tenant behavior are consistent across nodes.

## Release source

This page was checked against the following `release/3.8.0` implementations:

- [Core `IActivityProvider`](https://github.com/elsa-workflows/elsa-core/blob/7e82a55a9f4a3ee6967e2ea5ecc86eeb484e0b6c/src/modules/Elsa.Workflows.Core/Contracts/IActivityProvider.cs)
- [Core `ActivityRegistry`](https://github.com/elsa-workflows/elsa-core/blob/7e82a55a9f4a3ee6967e2ea5ecc86eeb484e0b6c/src/modules/Elsa.Workflows.Core/Services/ActivityRegistry.cs)
- [Core `ActivityRegistryPopulator`](https://github.com/elsa-workflows/elsa-core/blob/7e82a55a9f4a3ee6967e2ea5ecc86eeb484e0b6c/src/modules/Elsa.Workflows.Management/Services/ActivityRegistryPopulator.cs)
- [Core activity-descriptor endpoint](https://github.com/elsa-workflows/elsa-core/blob/7e82a55a9f4a3ee6967e2ea5ecc86eeb484e0b6c/src/modules/Elsa.Workflows.Api/Endpoints/ActivityDescriptors/List/Endpoint.cs)
- [Studio remote activity registry provider](https://github.com/elsa-workflows/elsa-studio/blob/ef6a39d1/src/modules/Elsa.Studio.Workflows.Core/Domain/Services/RemoteActivityRegistryProvider.cs)
- [Studio default activity registry](https://github.com/elsa-workflows/elsa-studio/blob/ef6a39d1/src/modules/Elsa.Studio.Workflows.Core/Domain/Services/DefaultActivityRegistry.cs)
- [Studio activity picker](https://github.com/elsa-workflows/elsa-studio/blob/ef6a39d1/src/modules/Elsa.Studio.Workflows/ActivityPickers/Treeview/ActivityPicker.razor.cs)
- [Extensions MassTransit activity provider](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/servicebus/Elsa.ServiceBus.MassTransit/Services/MassTransitActivityTypeProvider.cs)
