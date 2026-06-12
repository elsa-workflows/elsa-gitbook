---
description: >-
  Source-backed guide to the main Elsa Studio customization seams in release
  3.8.0, including host composition, branding, menus, widgets, activity
  pickers, and editor extensibility.
---

# Customizing Elsa Studio

This guide is based on the `release/3.8.0` source code in
`elsa-studio` and, where activity metadata is involved, `elsa-core`.

Use this guide when you are embedding or hosting Elsa Studio and need to
change how Studio looks, what it exposes, or how its editors behave.

If you only need host setup, start with [Studio Integration](integration/README.md).
If you only need embedded surfaces, go straight to
[Custom Elements Embedding](integration/custom-elements.md).

## Start With The Smallest Useful Seam

In Elsa Studio 3.8.0, customization is mostly done through dependency
injection and feature modules.

| Goal | Use this seam | Source-backed entry point |
| --- | --- | --- |
| Change Studio shell services or options | Host startup | `AddShell`, `ShellOptions` |
| Change branding in a standalone host | Replace `IBrandingProvider` | `MainLayout`, server host `Program.cs` |
| Add or remove Studio capabilities | Host module registration | `AddWorkflowsModule`, `AddDashboardModule`, `AddSecretsModule`, and similar |
| Add navigation items | `IMenuProvider` | `DefaultMenuService` |
| Add app-bar UI | `IAppBarService` from an `IFeature` | `DefaultAppBarService` |
| Add panels, tabs, or editor widgets | `IWidget` or `IWidgetRegistry` | `DefaultWidgetRegistry` |
| Change the workflow activity picker | Replace `IActivityPickerComponentProvider` | workflows module plus host override |
| Render a new input editor | Studio `IUIHintHandler` plus backend `UIHint` metadata | `AddDefaultUIHintHandlers`, `ActivityDescriber` |
| Decorate an existing input editor | `IUIFieldExtensionHandler` | `FieldExtension.razor` |

Pick the narrowest seam that solves the problem. That keeps your host
close to the stock Studio behavior and reduces upgrade risk.

## Host-Level Composition

The three released Studio hosts all compose Studio by registering services
and modules in `Program.cs`:

- `src/hosts/Elsa.Studio.Host.Server`
- `src/hosts/Elsa.Studio.Host.Wasm`
- `src/hosts/Elsa.Studio.Host.CustomElements`

That means host composition is the first customization seam.

### Shell Options

`AddShell` configures shared shell services and binds `ShellOptions`.
In release 3.8.0, the exposed shell option is:

```json
{
  "Shell": {
    "DisableAuthorization": false
  }
}
```

`App.razor.cs` reads this option to decide whether authorization should be
enforced in the shell.

### Branding

The shared services layer registers `DefaultBrandingProvider`, and
`MainLayout.razor` renders the current provider inside the drawer header.

The server host shows the supported replacement pattern by registering
`StudioBrandingProvider` as `IBrandingProvider`.

Use this seam when you need:

- custom logos or product naming
- organization-specific shell branding
- different login or navigation branding in a dedicated Studio host

### Module Set

Studio capabilities are opt-in at host startup. For example, the released
server host registers:

- dashboard modules
- workflows and workflow dashboard modules
- alterations
- diagnostics modules
- secrets
- localization

If a host does not register a module, that capability is not present in
the shell. This is the primary seam for building a smaller, purpose-built
Studio.

## Navigation, App Bar, And Feature Gating

Studio uses `IFeature`, `IMenuProvider`, and `IAppBarService` to compose
user-facing shell behavior.

### Menus

`DefaultMenuService` asks every registered `IMenuProvider` for menu items
and then orders the combined result.

Use `IMenuProvider` when you want to:

- add a new top-level navigation item
- add menu entries for a custom Studio module
- hide stock menu areas by omitting the corresponding module from the host

### App Bar

`MainLayout.razor` renders `AppBarService.AppBarComponents`.

Features such as localization and environment selection add app-bar
elements during feature initialization. Use `IAppBarService` when you need
global shell controls such as:

- environment switchers
- tenant switchers
- custom status or action buttons

### Remote Feature Gating

`DefaultFeatureService` initializes all local `IFeature` registrations, but
it skips any feature decorated with `RemoteFeatureAttribute` when the
backend does not advertise the matching capability.

This is how modules such as OpenTelemetry and console logs stay absent from
the shell when the connected Elsa Server does not support them.

Use the same pattern for optional modules that should appear only when a
backend feature is available.

## Widgets And Editor Surface Extensions

Widgets are the main seam for adding UI inside existing Studio surfaces.

`DefaultWidgetRegistry` collects all registered `IWidget` instances and
renders them by zone and order.

Use widgets when you need to extend an existing page rather than create a
completely separate screen.

Examples from the released source include:

- workflow definition metadata, settings, and info widgets
- workflow definition labels widgets
- console log widgets in workflow instance views
- dashboard widgets and dashboard companion widgets
- platform submission widgets from `AddPlatformIntegrationModule`

Use widgets for:

- extra tabs or panels in workflow or instance screens
- organization-specific metadata editors
- submit or approval actions tied to existing Studio pages

## Workflow Editor Customization

The workflow editor has three main seams in release 3.8.0.

### Activity Picker

`AddWorkflowsModule()` registers
`AccordionActivityPickerComponentProvider` by default.

The server host replaces it with
`TreeviewActivityPickerComponentProvider`.

If you need a different activity browsing experience, replace
`IActivityPickerComponentProvider` in your host.

### Input Editors

Studio-side input editors are selected through `IUIHintHandler`.

`AddDefaultUIHintHandlers()` registers the built-in handlers for hints such
as:

- `singleline`
- `dropdown`
- `checkbox`
- `json-editor`
- `code-editor`
- `workflow-definition-picker`
- `dynamic-outcomes`

If you introduce a new backend `UIHint`, register a matching Studio
`IUIHintHandler`.

For the full backend-plus-Studio flow, see
[Custom UI Components](custom-ui-components.md).

### Field Decorations

`FieldExtension.razor` wraps input editors and renders matching
`IUIFieldExtensionHandler` instances above or below the editor.

Use field extensions when you want to:

- add helper UI around an existing editor
- add syntax-specific controls
- add activity-specific hints or toolbars

Use this seam when the stock editor is still correct and only the framing
needs to change.

For the detailed contract and example, see
[Field Extensions](../../studio/workflow-editor/field-extensions.md).

## Backend Metadata Still Matters

Some Studio customization starts in `elsa-core`, not `elsa-studio`.

`ActivityDescriber` builds input descriptors from activity metadata, and
`PropertyUIHandlerResolver` combines:

- explicit `UIHandler` or `UIHandlers` from `[Input]`
- default property UI handlers associated with the selected backend UI hint

The resulting UI metadata is sent to Studio in
`InputDescriptor.UISpecifications`.

If the metadata marks an input for refresh, Studio recomputes descriptor
options through:

`POST /descriptors/activities/{activityTypeName}/options/{propertyName}`

That split matters:

- use `elsa-core` to define activity metadata and option-generation logic
- use `elsa-studio` to decide how that metadata is rendered

## Choosing The Right Customization Shape

Use a dedicated standalone host when you need:

- a full Studio application
- custom branding
- server-side or SPA-level auth handling
- a curated module set for operators or designers

Use custom-elements embedding when you need:

- selected Studio surfaces inside another application shell
- host-controlled navigation and auth
- incremental adoption instead of a full Studio shell

Use widgets, menu providers, and app-bar features when you need to extend
the stock shell without forking its pages.

Use UI hint handlers and field extensions when the change belongs inside
the workflow inspector rather than the shell.

## Related Guides

- [Studio Integration](integration/README.md)
- [Custom Elements Embedding](integration/custom-elements.md)
- [Custom UI Components](custom-ui-components.md)
- [Field Extensions](../../studio/workflow-editor/field-extensions.md)
