---
description: >-
  Add custom activity icons to Elsa Studio with a release-backed display
  settings provider.
---

# Custom Activity Icons

Use a custom activity icon when your Elsa Studio users need to recognize a
domain activity quickly in the activity picker, workflow designer, or runtime
inspection views. In Elsa `release/3.8.0`, icons are a Studio display concern:
the Core `ActivityDescriptor` contains activity metadata, but no icon field.

That means the icon provider belongs in the Elsa Studio host (Server, WASM, or
Custom Elements), not in the Elsa Server runtime. It changes how Studio
renders an activity; it does not add the activity to the server, change its
execution behavior, or install an icon asset for other clients.

## How the icon path works

The release implementation has three parts:

1. `IActivityDisplaySettingsProvider.GetSettings()` returns a dictionary keyed
   by the exact activity `TypeName`.
2. `ActivityDisplaySettings` supplies an `Icon` string and a `Color` string.
3. `DefaultActivityDisplaySettingsRegistry` combines all providers and returns
   the matching settings. Unknown activity types receive Studio's default
   icon and color.

The registry is used by the activity pickers and by designer and workflow
instance components. A custom provider therefore updates several Studio
surfaces at once without changing workflow JSON.

## Add a provider

Create a Studio-side provider in the project or module that owns your Studio
customization:

```csharp
using System.Collections.Generic;
using Elsa.Studio.Workflows.UI.Contracts;
using Elsa.Studio.Workflows.UI.Models;

public sealed class AcmeActivityDisplaySettingsProvider
    : IActivityDisplaySettingsProvider
{
    private const string InvoiceIcon = """
        <svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <path fill="currentColor" d="M4 3h16v18H4z" />
            <path fill="currentColor" d="M7 7h10v2H7zm0 4h10v2H7zm0 4h6v2H7z" />
        </svg>
        """;

    public IDictionary<string, ActivityDisplaySettings> GetSettings() =>
        new Dictionary<string, ActivityDisplaySettings>
        {
            ["Acme.SendInvoice"] = new("#2563eb", InvoiceIcon)
        };
}
```

The key, `Acme.SendInvoice`, must match the activity descriptor's `TypeName`
exactly. It is not the display name shown to designers. For a CLR activity,
confirm the value in the descriptors response or in the activity descriptor
that Studio receives from the server.

The `Icon` value is passed to the Studio components as an icon string. Elsa's
stock provider uses both `ElsaStudioIcons` and MudBlazor icon constants. An
inline SVG is useful when you own the artwork, but keep it small and suitable
for the icon slot. If the SVG should follow the configured activity color,
use `currentColor` for its `fill` or `stroke`; the tree picker applies the
activity color to those exact SVG attributes.

## Register it in the Studio host

Register the provider after the workflows module in the Studio host's
dependency-injection setup. The same pattern applies to Server, WASM, and
Custom Elements hosts; use the container exposed by that host.

```csharp
builder.Services.AddWorkflowsModule();
builder.Services.AddActivityDisplaySettingsProvider<
    AcmeActivityDisplaySettingsProvider>();
```

For a WASM host, the equivalent is `services.AddWorkflowsModule()` followed by
`services.AddActivityDisplaySettingsProvider<...>()`. The generic registration
adds the provider as a scoped `IActivityDisplaySettingsProvider`.

Do not register this provider only in `elsa-core` or only on the Elsa Server.
Those processes do not own the Studio display registry. If you deploy more
than one Studio host, include the provider in each host that should show the
custom icon.

## Dynamic activity sets

The provider may build its dictionary from the Studio activity registry when a
family of activities is discovered dynamically. The released Agents extension
uses this pattern: it selects descriptors whose `RootType` custom property is
`AgentActivity`, then maps each descriptor's `TypeName` to one shared robot
icon and color.

Use a dynamic provider only when the set of activity type names really is
dynamic. For a fixed set of custom activities, a static dictionary is easier
to review and less sensitive to registry timing.

## Provider precedence and fallback

The release registry evaluates providers in sequence and assigns each returned
dictionary entry into one combined dictionary. If two providers return the
same type name, the later assignment wins. Use unique keys or make an override
intentional; do not depend on incidental registration order.

If no provider returns a matching type name, Studio uses its built-in default
icon and color. This fallback is useful while a provider is being rolled out,
but it can also hide a type-name mismatch. When an icon does not appear, check
the descriptor `TypeName` first.

The registry builds its combined dictionary lazily and caches it for the
current scope. `IActivityDisplaySettingsRegistry` exposes `MarkStale()` to
clear that cache, but the release source does not automatically invalidate it
when an arbitrary provider's backing data changes. Prefer stable startup
mappings; if your provider is genuinely dynamic, make cache invalidation part
of the same explicit refresh operation.

## Troubleshooting

### The activity still has the default icon

Check these in order:

1. The provider is registered in the Studio host that the browser is actually
   using.
2. The dictionary key exactly matches the activity descriptor `TypeName`,
   including punctuation and casing.
3. The provider is included in the deployed Studio module or application.
4. The icon string is non-empty and is accepted by the Studio icon component.
5. The browser has loaded the updated Studio deployment rather than a cached
   application bundle.

### The icon appears in one surface but not another

The activity picker and designer use the same display-settings registry, but
they render the icon through different components. Verify the SVG markup is
valid and keep `fill="currentColor"` or `stroke="currentColor"` where the
activity color should be applied. A library icon constant can avoid SVG
markup differences between components.

### The activity is missing entirely

An icon provider does not register activities. Confirm the Elsa Server returns
the activity descriptor and that the activity is browsable. Use the
[activity type provider guide](activity-type-providers.md) when the activity
itself is generated dynamically or is not present in the activity picker.

## Related guides

- [Customizing Elsa Studio](../studio/customization.md)
- [Custom Activities](../../extensibility/custom-activities.md)
- [Activity Type Providers](activity-type-providers.md)

## Release source

This page was checked against the following `release/3.8.0` implementations:

- [Studio display settings contract](https://github.com/elsa-workflows/elsa-studio/blob/d25f0aaeb5f14af6c5938d173aae828d87ebad5c/src/modules/Elsa.Studio.Workflows.Core/UI/Contracts/IActivityDisplaySettingsProvider.cs)
- [Studio display settings model](https://github.com/elsa-workflows/elsa-studio/blob/d25f0aaeb5f14af6c5938d173aae828d87ebad5c/src/modules/Elsa.Studio.Workflows.Core/UI/Models/ActivityDisplaySettings.cs)
- [Studio display settings registry contract](https://github.com/elsa-workflows/elsa-studio/blob/d25f0aaeb5f14af6c5938d173aae828d87ebad5c/src/modules/Elsa.Studio.Workflows.Core/UI/Contracts/IActivityDisplaySettingsRegistry.cs)
- [Studio display settings registry](https://github.com/elsa-workflows/elsa-studio/blob/d25f0aaeb5f14af6c5938d173aae828d87ebad5c/src/modules/Elsa.Studio.Workflows.Core/Domain/Services/DefaultActivityDisplaySettingsRegistry.cs)
- [Studio provider registration](https://github.com/elsa-workflows/elsa-studio/blob/d25f0aaeb5f14af6c5938d173aae828d87ebad5c/src/modules/Elsa.Studio.Workflows.Core/Extensions/ServiceCollectionExtensions.cs)
- [Studio tree activity picker](https://github.com/elsa-workflows/elsa-studio/blob/d25f0aaeb5f14af6c5938d173aae828d87ebad5c/src/modules/Elsa.Studio.Workflows/ActivityPickers/Treeview/ActivityPicker.razor.cs)
- [Studio icon color handling](https://github.com/elsa-workflows/elsa-studio/blob/d25f0aaeb5f14af6c5938d173aae828d87ebad5c/src/modules/Elsa.Studio.Workflows/ActivityPickers/Treeview/ActivityTreeItem.cs)
- [Core activity descriptor](https://github.com/elsa-workflows/elsa-core/blob/c58fe8770ff7ba39be74b58cd4b1e6017ee5e140/src/modules/Elsa.Workflows.Core/Models/ActivityDescriptor.cs)
- [Extensions Agents provider example](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/agents/Elsa.Studio.Agents/UI/Providers/DefaultActivityDisplaySettingsProvider.cs)
