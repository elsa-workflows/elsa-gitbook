---
description: >-
  Source-backed guide to Elsa Studio activity port providers in release 3.8.0,
  including dynamic ports, embedded activities, provider selection, and custom
  registration.
---

# Activity Port Providers

Activity port providers control the ports that Elsa Studio renders around an
activity in the workflow designer. Use them when an activity's connections or
embedded child activities depend on its current JSON configuration rather than
on a fixed list of ports in its activity descriptor.

This is a Studio customization seam. It changes how a workflow is edited and
how the designer reads or writes the activity JSON; it does not change the
activity's runtime execution, outcomes, or server-side activity implementation.

## When to use a provider

Use an activity port provider when:

- an activity exposes a variable number of flow outcomes, such as one port per
  configured case or status code;
- an activity contains child activities under a configuration-specific branch;
  or
- the ports declared by the server's activity descriptor are not enough for
  the Studio designer.

If the activity always has the same ports, declare those ports in the activity
descriptor instead. Studio's fallback provider returns the descriptor's ports.

## How Studio selects a provider

The contract is `IActivityPortProvider` from the Studio Workflows Core module.
Each provider receives a `PortProviderContext` containing:

- `ActivityDescriptor`: the descriptor loaded from the Elsa backend;
- `Activity`: the current activity as a mutable `JsonObject`.

A provider implements these operations:

- `Priority` determines which matching provider wins.
- `GetSupportsActivityType` says whether the provider handles this descriptor
  and JSON shape.
- `GetPorts` returns the current `Port` objects shown by the designer.
- `ResolvePort` finds the child activity currently assigned to a port.
- `AssignPort` assigns a child activity to a port.
- `ClearPort` removes a child activity from a port.

For each activity, Studio filters providers whose support method returns
`true`, then selects the one with the highest `Priority`. It uses that same
provider to render ports and to navigate into, assign, or clear embedded
activities. If no custom provider matches, the built-in
`DefaultActivityPortProvider` handles the activity and returns the ports from
the descriptor.

The fallback provider has priority `-1000`; the normal base-class priority is
`0`. A custom provider therefore wins over the fallback automatically. Set a
higher priority when deliberately replacing another provider that also claims
the activity type.

## Port kinds

The `Port` model distinguishes the two designer relationships most custom
providers need:

- `PortType.Flow` represents an outgoing workflow connection. The port does
  not necessarily contain a child activity.
- `PortType.Embedded` represents a child activity stored inside the current
  activity, such as a `Switch` case branch.

Use stable, unique port names. Studio uses the name to find the child activity
and stores it in designer path segments, so changing a name can make existing
embedded paths or connections difficult to reopen.

## Built-in providers in release 3.8.0

`AddWorkflowsModule()` registers the default provider module automatically.
The released module adds providers for these cases:

- `DynamicOutcomesPortProvider` supports an input with the
  `dynamic-outcomes` UI hint. It returns flow ports from the configured
  outcome expression and any fixed outcomes.
- `SwitchPortProvider` supports `Elsa.Switch`. It returns embedded ports from
  case labels and adds a `Default` port when one is absent.
- `FlowSwitchPortProvider` supports `Elsa.FlowSwitch`. It returns flow ports
  from case labels and adds a `Default` port when one is absent.
- `HttpEndpointPortProvider` supports `Elsa.HttpEndpoint`. It returns `Done`
  and enabled error outcomes such as request-too-large or invalid MIME type.
- `SendHttpRequestPortProvider` supports `Elsa.SendHttpRequest`. It returns
  embedded ports for expected status codes, unmatched status, connection
  failure, and timeout.
- `FlowHttpRequestPortProvider` supports `Elsa.FlowSendHttpRequest` and
  `Elsa.DownloadHttpFile`. It returns flow ports for expected status codes and
  the unmatched, failure, timeout, and done outcomes.

The exact labels are derived from the activity JSON. For example, changing a
`Switch` case label changes the corresponding Studio port label; enabling an
`HttpEndpoint` error outcome adds that port.

## Author a custom provider

The easiest starting point is `ActivityPortProviderBase`. Override
`GetSupportsActivityType` and `GetPorts` for flow-only ports. For embedded
ports, also override `ResolvePort`, `AssignPort`, and `ClearPort` so the port
maps to the activity's actual JSON shape.

The following provider supports an activity whose descriptor type name is
`Acme.Approve` and whose JSON stores embedded branches as:

```json
{
  "type": "Acme.Approve",
  "branches": [
    { "label": "Approved", "activity": null },
    { "label": "Rejected", "activity": null }
  ]
}
```

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Elsa.Api.Client.Resources.ActivityDescriptors.Enums;
using Elsa.Api.Client.Resources.ActivityDescriptors.Models;
using Elsa.Studio.Workflows.Domain.Contexts;
using Elsa.Studio.Workflows.Domain.Providers;

public sealed class ApprovalPortProvider : ActivityPortProviderBase
{
    public override bool GetSupportsActivityType(PortProviderContext context) =>
        context.ActivityDescriptor.TypeName == "Acme.Approve";

    public override IEnumerable<Port> GetPorts(PortProviderContext context)
    {
        foreach (var branch in GetBranches(context.Activity))
        {
            var label = GetLabel(branch);
            if (label is null)
                continue;

            yield return new Port
            {
                Name = label,
                DisplayName = label,
                Type = PortType.Embedded
            };
        }
    }

    public override JsonObject? ResolvePort(
        string portName,
        PortProviderContext context)
    {
        return GetBranches(context.Activity)
            .FirstOrDefault(branch => GetLabel(branch) == portName)?["activity"]
            ?.AsObject();
    }

    public override void AssignPort(
        string portName,
        JsonObject activity,
        PortProviderContext context)
    {
        var branch = GetBranches(context.Activity)
            .FirstOrDefault(branch => GetLabel(branch) == portName);

        if (branch is not null)
            branch["activity"] = activity;
    }

    public override void ClearPort(string portName, PortProviderContext context)
    {
        var branch = GetBranches(context.Activity)
            .FirstOrDefault(branch => GetLabel(branch) == portName);

        if (branch is not null)
            branch["activity"] = null;
    }

    private static IEnumerable<JsonObject> GetBranches(JsonObject activity) =>
        (activity["branches"] as JsonArray ?? new JsonArray())
            .OfType<JsonObject>();

    private static string? GetLabel(JsonObject branch) =>
        branch["label"]?.GetValue<string>();
}
```

If the provider only creates flow ports and never stores child activities,
leave the base implementation of `ResolvePort`, `AssignPort`, and `ClearPort`
in place only if its camel-cased property mapping matches your JSON. Otherwise,
implement those methods explicitly or return no embedded ports.

### Register it in the Studio host

Register the provider after `AddWorkflowsModule()` in the Studio host that
should use it:

```csharp
builder.Services.AddWorkflowsModule();
builder.Services.AddActivityPortProvider<ApprovalPortProvider>();
```

`AddActivityPortProvider<T>()` registers the provider as a scoped
`IActivityPortProvider`. The provider assembly must be referenced by the host.
Use `builder.Services` in the Server and Custom Elements hosts, or `services`
in the WASM host. Register it in each host where the custom designer behavior
is required.

You do not need to call `AddDefaultActivityPortProviders()` in a stock host:
`AddWorkflowsModule()` calls it and also registers the descriptor fallback.
Call the default-provider extension directly only when composing a lower-level
custom module instead of the released workflows module.

## Design and troubleshooting checklist

- Confirm the provider's `TypeName` matches the activity descriptor returned by
  the backend, including the activity version behavior your host uses.
- Inspect the activity JSON property names and casing. Port names are designer
  identifiers; JSON property names are your storage contract.
- Keep port names stable and unique within one activity.
- Use `Flow` for ordinary outgoing connections and `Embedded` only when the
  activity owns the child activity JSON.
- Give a provider a higher priority if another provider claims the same
  activity type and should remain available as a fallback.
- Reopen the activity after changing its configuration. Providers are queried
  from the current descriptor and JSON, so a changed case list or expected
  status-code list can change the visible ports.
- If Studio reports that no port provider is found, verify that the workflows
  module is registered and that the provider assembly is loaded into the host.

## Boundary with runtime behavior

Activity port providers live in `elsa-studio`. They are not an Elsa Server
activity provider, a workflow runtime extension, or a replacement for runtime
outcome logic. The activity still needs to implement the corresponding runtime
behavior, and its descriptor still needs to expose the metadata Studio uses to
load the activity.

This distinction matters when deploying a custom activity: installing the
provider in Studio changes the editing experience, but every execution host
that runs the workflow must still reference and register the activity's server
or extension module.

## Release source

The contract and selection behavior are implemented in the
[Studio `IActivityPortProvider` contract](https://github.com/elsa-workflows/elsa-studio/blob/bee3c1605fd2c5937fed3621b2860465a2f8c448/src/modules/Elsa.Studio.Workflows.Core/Domain/Contracts/IActivityPortProvider.cs),
[provider selection service](https://github.com/elsa-workflows/elsa-studio/blob/bee3c1605fd2c5937fed3621b2860465a2f8c448/src/modules/Elsa.Studio.Workflows.Core/Domain/Services/DefaultActivityPortService.cs),
and [registration extensions](https://github.com/elsa-workflows/elsa-studio/blob/bee3c1605fd2c5937fed3621b2860465a2f8c448/src/modules/Elsa.Studio.Workflows.Core/Extensions/ServiceCollectionExtensions.cs).
The built-in registrations are in the
[activity port provider module](https://github.com/elsa-workflows/elsa-studio/blob/bee3c1605fd2c5937fed3621b2860465a2f8c448/src/modules/Elsa.Studio.ActivityPortProviders/Extensions/ServiceCollectionExtensions.cs),
and the stock workflows host composes them through
[`AddWorkflowsModule`](https://github.com/elsa-workflows/elsa-studio/blob/bee3c1605fd2c5937fed3621b2860465a2f8c448/src/modules/Elsa.Studio.Workflows/Extensions/ServiceCollectionExtensions.cs).

## Related guides

- [Custom Activities](../../extensibility/custom-activities.md)
- [Customizing Elsa Studio](../studio/customization.md)
- [UI Hints](../../studio/workflow-editor/ui-hints.md)
