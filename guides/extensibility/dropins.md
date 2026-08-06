---
description: >-
  Package and discover server-side Elsa extensions from a DropIns directory in
  Elsa 3.8.0.
---

# DropIns

The DropIns module lets an Elsa Server load extension assemblies or NuGet
packages from a directory at runtime. Use it when the host should discover
trusted extension artifacts without adding each extension as a compile-time
project reference.

DropIns is a server-side deployment mechanism. It does not install NuGet
dependencies, expose a package feed, or add a client-side Elsa Studio plugin.
If an extension also needs an activity catalog, variable type, API endpoint, or
Studio editor, configure those surfaces separately on the server and in Studio.

## When to use DropIns

Choose the extension path that matches the deployment model:

| Need | Prefer |
| --- | --- |
| Fixed build-time extension | Normal package reference and `Use...`/`Add...` registration |
| Independent trusted artifact | DropIns |
| Studio-only visual customization | [Studio customization](../studio/customization.md) |
| Activity catalog contribution | [Activity type providers](activity-type-providers.md) |

DropIns are most useful for controlled server deployments. Every assembly or
package under the configured directory is inspected and may execute code, so
do not point the directory at an upload location or an untrusted shared path.

## Enable DropIns in the host

Reference `Elsa.DropIns` from the server host and call `InstallDropIns` while
configuring Elsa. The release workbench uses an `App_Data/DropIns` directory;
an application can choose another location.

```csharp
using Elsa.DropIns.Extensions;
using Elsa.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddElsa(elsa => elsa
    .InstallDropIns(options =>
    {
        options.DropInRootDirectory = Path.Combine(
            builder.Environment.ContentRootPath,
            "App_Data",
            "DropIns");
    }));
```

`DropInRootDirectory` is a required operational setting. The monitor creates
the directory if it does not exist, but the host must still provide a usable
path. In a container, mount or copy the directory into every server replica
that is expected to load the same extensions.

## Author a DropIn

Reference `Elsa.DropIns.Core` from the extension project. Implement
`IDropIn` with a public parameterless constructor:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Elsa.DropIns.Core;
using Elsa.Features.Services;
using Microsoft.Extensions.DependencyInjection;

public sealed class AcmeDropIn : IDropIn
{
    public void Install(IModule module)
    {
        // This runs while Elsa is building its module configuration.
        module.Services.AddSingleton<AcmeGreetingService>();
    }

    public ValueTask ConfigureAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        // This runs after the host service provider is available.
        return ValueTask.CompletedTask;
    }

    public void Unconfigure(IServiceProvider serviceProvider)
    {
        // Release resources owned by this DropIn when its artifact is deleted.
    }
}

public sealed class AcmeGreetingService
{
}
```

The three methods have different responsibilities:

- `Install(IModule)` registers services or configures Elsa features. It runs
  during `InstallDropIns`, before the application service provider is built.
- `ConfigureAsync(...)` performs initialization that needs the application
  service provider. It is called for each discovered DropIn during the
  monitor's initial scan and when a watched path changes.
- `Unconfigure(...)` releases resources when the monitor processes a deleted
  artifact. It is not a general rollback mechanism for registrations made by
  `Install`.

The loader finds exported, non-abstract, non-interface implementations of
`IDropIn` and creates them with `Activator.CreateInstance`. Keep the DropIn
constructor parameterless and put dependency resolution in the lifecycle
methods instead.

## Deploy an artifact

The directory catalog recursively scans the configured root for both `.dll`
and `.nupkg` files.

### Deploy assemblies

Copy the DropIn assembly and its dependencies into an isolated subdirectory:

```text
App_Data/
  DropIns/
    Acme/
      Acme.Elsa.DropIn.dll
      Acme.Dependency.dll
```

The directory loader uses an assembly dependency resolver for the assembly
path. Keep the DropIn's dependency set together and avoid copying the whole
host's publish directory into the DropIns directory: every DLL below the root
is inspected for `IDropIn` implementations.

### Deploy NuGet packages

Copy a `.nupkg` into the root or a subdirectory:

```text
App_Data/
  DropIns/
    Acme/
      Acme.Elsa.DropIn.<version>.nupkg
```

The release loader reads every DLL in the package archive and searches the
loaded assemblies for `IDropIn` implementations. This is not package restore:
the package must already contain the assemblies the DropIn needs, and the
artifact must be trusted before it is copied into the directory.

Use an `Elsa.DropIns.Core` package version compatible with the server's Elsa
release. The DropIn is loaded into a separate assembly load context, but its
contract types still need to resolve to the host-compatible Elsa assemblies.

## Understand the lifecycle

`InstallDropIns` performs two related actions:

1. It scans the root immediately and calls `Install` on each discovered
   DropIn, allowing the DropIn to contribute to the `IModule`.
2. It registers `DropInDirectoryMonitorHostedService`, which creates the root
   directory if necessary, performs an initial scan, and calls
   `ConfigureAsync` with the real application service provider.

The monitor uses a `FileSystemWatcher` recursively. In the 3.8.0 source it
handles `Changed` and `Deleted` events, debounced by two seconds. When the
monitor has recorded instances for a deleted event path, those instances
receive `Unconfigure`. The initial scan is keyed differently in the release
implementation, so deletion is not a reliable rollback for every initially
loaded artifact. A changed artifact is scanned and configured again; the
module does not provide a transactional replacement operation.

Treat replacement as a deployment operation with a health check and, when
necessary, a restart. Do not assume that changing a file atomically adds a new
DropIn, rolls back service registrations, or unloads all assembly memory. The
release source calls `Unconfigure` for deletion but does not expose a public
assembly-unload or rollback contract.

## Troubleshoot loading

Check these boundaries in order:

1. Confirm the server calls `InstallDropIns` and that
   `DropInRootDirectory` resolves to the directory you populated.
2. Confirm the artifact extension is `.dll` or `.nupkg` and that it is below
   the root, including any required dependencies.
3. Confirm the DropIn type is public, concrete, implements `IDropIn`, and has
   a public parameterless constructor.
4. Check startup and hosted-service logs. `Install` runs during module setup;
   `ConfigureAsync` and monitor errors occur at a different lifecycle stage.
5. If a changed artifact behaves unpredictably, remove it only after the
   extension has released external resources, then restart the host to obtain
   a clean module and assembly state.

DropIns do not automatically update Studio. After a server extension is
loaded, Studio can use only the capabilities the server exposes through its
normal APIs and the Studio packages/configuration you have installed. For
reusable server modules, start with [Modules and Plugins](modules-and-plugins.md),
then use the focused guides for [custom activities](../../extensibility/custom-activities.md),
[activity type providers](activity-type-providers.md), and
[custom types](custom-types.md).

## Release source

The behavior described here is implemented in the
[release/3.8.0 DropIns source directory][dropins-source].
Key files include `Elsa.DropIns.Core/IDropIn.cs`,
`Elsa.DropIns/Extensions/ModuleExtensions.cs`,
`Elsa.DropIns/Catalogs/DirectoryDropInCatalog.cs`, and
`Elsa.DropIns/HostedServices/DropInDirectoryMonitorHostedService.cs`.

[dropins-source]: https://github.com/elsa-workflows/elsa-extensions/tree/release/3.8.0/src/modules/dropins
