---
description: >-
  Release-backed configuration matrix for code-first Elsa hosts and the
  CShells-based modular server in Elsa 3.8.0.
---

# Standalone and Modular Hosting

Elsa 3.8.0 supports two host-composition models:

- **Standalone (code-first)**: register Elsa in an ASP.NET Core application
  with `AddElsa(...)`, then compose modules with `UseX(...)` methods.
- **Modular**: use `Elsa.ModularServer.Web`, register CShells, and activate
  shell features from configuration. This is useful when feature composition
  and per-shell settings need to be changed without recompiling the host.

These models use the same Elsa modules, but their configuration surfaces are
different. Use the matrix below when moving from one host model to the other.

<!-- markdownlint-disable MD013 -->

## Host registration

| Concern | Standalone host | Modular host |
| --- | --- | --- |
| Register Elsa | `services.AddElsa(elsa => { ... })` | `builder.AddShells(shells => shells.WithConfigurationProvider(configuration) ...)` |
| Activate features | Call extension methods such as `UseIdentity()`, `UseWorkflowRuntime()`, or `UseWorkflowsApi()` | Add shell feature types in `shell.WithFeatures(...)` and configure their settings under `CShells:Shells:<name>:Features` |
| Set feature options | Configure the feature callback or bind an options section in C# | Set the shell feature's public settings under that feature's configuration object |
| Add a route prefix or shell path | Configure the relevant ASP.NET Core or Elsa option in code | Configure the shell's `WebRouting` settings and enable CShells web routing in the host |
| Add or remove a capability | Change the compiled `UseX(...)` chain | Change the shell feature set and reload/restart according to the modular host's lifecycle |

The release modular server wires these pieces together in
`Elsa.ModularServer.Web/Program.cs`:

```csharp
builder.AddShells(shells => shells
    .WithHostAssemblies()
    .WithConfigurationProvider(configuration)
    .WithWebRouting(options => options.EnablePathRouting = true)
    .WithAuthenticationAndAuthorization()
    .ConfigureAllShells(shell => shell.WithFeatures(
        typeof(ElsaFeature),
        typeof(WorkflowManagementFeature),
        typeof(WorkflowRuntimeFeature),
        typeof(WorkflowsFeature),
        typeof(WorkflowsApiFeature))));
```

`ConfigureAllShells(...)` supplies the feature set to every shell created by
the host. The values for an individual shell are then read from that shell's
configuration section.

## Configuration matrix

The modular column separates features activated in the host's
`WithFeatures(...)` list from settings exposed by a shell feature. A feature
can be active without needing a configuration object.

| Capability | Standalone registration | Modular feature/settings |
| --- | --- | --- |
| Elsa core | `elsa` module is created by `AddElsa(...)` | `"Elsa": {}` |
| Identity services | `.UseIdentity(...)` | `"Identity": { "SigningKey": "..." }` |
| JWT/API-key authentication | `.UseDefaultAuthentication()` | `"DefaultAuthentication": {}` |
| Workflow management and runtime | `.UseWorkflowManagement(...)` and `.UseWorkflowRuntime(...)` | Activate `WorkflowManagementFeature` and `WorkflowRuntimeFeature` in `WithFeatures(...)`; provider-specific persistence features supply storage. The shipped `appsettings.json` does not need empty settings objects for these two features |
| Workflow API | `.UseWorkflowsApi()` | `"WorkflowsApi": {}` |
| HTTP activities and triggers | `.UseHttp(http => { ... })` | `"Http": { "HttpActivityOptions": { ... } }` |
| SQLite workflow persistence | `UseEntityFrameworkCore(ef => ef.UseSqlite(...))` in the management/runtime configuration | `"SqliteWorkflowPersistence": { "ConnectionString": "..." }` |

<!-- markdownlint-enable MD013 -->

For example, the standalone HTTP configuration in the release server binds
`HttpActivityOptions` in the `UseHttp(...)` callback. The modular
`HttpFeature` exposes the same options as its `HttpActivityOptions` setting,
so the modular equivalent is:

```json
{
  "CShells": {
    "Shells": {
      "Default": {
        "Features": {
          "Http": {
            "HttpActivityOptions": {
              "BaseUrl": "https://localhost:5001"
            }
          }
        }
      }
    }
  }
}
```

For a code-first host, the equivalent is an explicit callback:

```csharp
services.AddElsa(elsa => elsa.UseHttp(http =>
{
    http.ConfigureHttpOptions = options =>
        configuration.GetSection("Http").Bind(options);
}));
```

The important distinction is the level at which the setting is bound: a
standalone callback binds an Elsa options object directly, while a modular
feature receives its own shell-scoped configuration object.

## Identity and secrets

The configuration path is not interchangeable between host models:

- code-first `Elsa.Server.Web` binds token settings from
  `Identity:Tokens`;
- the modular `Identity` shell feature binds its settings from the shell's
  `Identity` feature section, for example
  `CShells:Shells:Default:Features:Identity:SigningKey`.

For environment variables, that modular path becomes:

```text
CShells__Shells__Default__Features__Identity__SigningKey
```

Do not copy a code-first `Identity:Tokens:SigningKey` environment variable into
a modular host and assume it will be discovered. Bind the setting at the
configuration path used by the host model.

## Choosing a model

Choose a standalone host when:

- Elsa is part of an existing ASP.NET Core application;
- feature composition is owned by the application code;
- you need arbitrary C# callbacks, custom services, or application-specific
  startup logic.

Choose a modular host when:

- the deployment needs CShells and shell-scoped configuration;
- feature sets are assembled from host or package assemblies;
- different shells need different settings such as route paths or persistence
  connections.

The modular model does not make `AddElsa(...)` configuration automatically
available. A `UseX(...)` call must be translated to the corresponding shell
feature, and any callback logic must be replaced by settings that the shell
feature actually exposes.

## Configuration-shape warning

The 3.8.0 source tree contains both `src/apps/Elsa.ModularServer.Web/appsettings.json`
and `appsettings.Example.json`. They show different shell collection shapes:
the checked-in runtime file uses an object keyed by shell name, while the
example file uses a list of shell objects with `Name`, `Settings`, and
`Features` fields.

Treat the configuration file shipped with the exact modular host and its
CShells package version as authoritative. Do not combine the two shapes in one
deployment without verifying how that host's configuration provider parses
them.

## Related guides

- [Hosting Elsa in an Existing App](../onboarding/hosting-elsa-in-existing-app.md)
- [Modules and Plugins](../extensibility/modules-and-plugins.md)
- [Configuration Management](../deployment/configuration-management.md)
- [Authentication & Authorization](../authentication.md)

## Release source references

- [Standalone `AddElsa` registration](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa/Extensions/DependencyInjectionExtensions.cs)
- [Standalone server composition](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/apps/Elsa.Server.Web/Program.cs)
- [Modular server composition](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/apps/Elsa.ModularServer.Web/Program.cs)
- [Modular server settings](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/apps/Elsa.ModularServer.Web/appsettings.json)
- [Identity shell feature](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Identity/ShellFeatures/IdentityFeature.cs)
- [HTTP shell feature](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Http/ShellFeatures/HttpFeature.cs)
- [Module system notes](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/doc/wiki/module-system.md)
