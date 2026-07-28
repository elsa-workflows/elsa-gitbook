---
description: >-
  Explain the Elsa 3.8 package manifest generated for CShells extension
  packages, including runtime compatibility, infrastructure requirements, and
  deploy-time settings.
---

# Package manifests for extensions

Use this guide when you publish a NuGet package that contains Elsa server
extensions implemented as CShells features. Elsa 3.8 can generate an
`elsa-package.json` manifest while the package is built and include it at the
root of the NuGet package. The manifest describes what the package contains
and what an operator needs to configure; it does not replace your feature's
service registration or provision infrastructure.

## What the manifest is for

The package manifest is build-time metadata for extension packages. It helps
package and runtime tooling answer questions such as:

- Is this package intended for Elsa Server or Elsa Studio?
- Does a feature need a database, cache, message broker, or another resource?
- Which deploy-time settings should an operator provide?
- Is a setting secret, advanced, experimental, or restart-sensitive?

The manifest is not an application configuration file. `ManifestInfrastructure`
and `ManifestSetting` do not create a database, bind options, add a health
check, restart a shell, or make a setting available in Studio automatically.
Your feature still has to register services and read its configuration.

## How release packages generate it

The release source uses `Elsa.Platform.PackageManifest.Generator` as a
private build dependency. Core imports the shared package-manifest MSBuild
props for projects that contain a `ShellFeatures` directory; extension
modules apply the same generator package and compile the runtime-kind hint
file from their modules build props. The generator creates the manifest during
build/pack, and the default package path is `elsa-package.json`.

The release source uses different generator patch versions in the two
repositories (`0.0.1-preview.53` in Core and `0.0.1-preview.50` in
Extensions). Keep the generator version aligned with the Elsa release and
inspect the generated manifest before publishing.

If your extension project is not already covered by a shared build props
file, add the generator as a private build dependency. This is the pattern
used by the released Extensions modules:

```xml
<PackageReference
    Include="Elsa.Platform.PackageManifest.Generator"
    Version="0.0.1-preview.50"
    PrivateAssets="all" />
```

Use the generator version selected by the release you target; Core 3.8.0 uses
`0.0.1-preview.53`.

Relevant release files:

- [Core package-manifest MSBuild props](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/PackageManifest.props)
- [Core package-manifest runtime hint](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/PackageManifestHints.cs)
- [Extensions module build props](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/Directory.Build.props)
- [Extensions package-manifest runtime hint](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/PackageManifestHints.cs)
- [Extensions MassTransit Azure Service Bus feature](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/servicebus/Elsa.ServiceBus.MassTransit.AzureServiceBus/ShellFeatures/MassTransitAzureServiceBusFeature.cs)

## Declare runtime compatibility

Mark the package at assembly level when all of its features target one runtime
kind:

```csharp
using Elsa.Platform.PackageManifest.Generator.Hints;

[assembly: ManifestRuntimeKind(ElsaRuntimeKinds.Server)]
```

The released Core and Extensions sources both use `ElsaRuntimeKinds.Server`,
which resolves to `elsa.server`. The generator also supports feature-level
runtime hints when only part of a package is runtime-specific. Use
`ElsaRuntimeKinds.Studio` for Studio features, and do not label a package as
Studio-compatible merely because it contributes workflow activities that
appear in a Studio-hosted designer.

## Describe infrastructure requirements

Attach one or more infrastructure requirements to the feature that uses the
resource. For example, the released Azure Service Bus feature declares the
provider, a stable identifier, the infrastructure kind, and related
configuration keys:

```csharp
[ManifestInfrastructure(
    "azure-service-bus",
    "service-bus",
    Reason = "Publishes and consumes workflow messages through Azure Service Bus.",
    Providers = new[] { "Azure Service Bus" },
    ConfigurationKeys = new[] { "ConnectionStringOrName" })]
public class AzureServiceBusShellFeature : IShellFeature
{
    // The feature still binds and uses this setting in ConfigureServices.
}
```

The first argument is the requirement identifier and the second is its kind.
The attribute also supports `Optional`, `Reason`, `Capabilities`, `Providers`,
`ConfigurationKeys`, and `Extensions`. Use the metadata to explain the
dependency to tooling and operators; it is not a provisioning declaration.

Examples from the release use `database` for Quartz and persistence stores,
`message-broker` for RabbitMQ and Kafka, and `service-bus` for Azure Service
Bus. The same logical infrastructure can be declared by multiple features;
give each requirement an identifier that is stable and meaningful within the
package.

A generated manifest contains package and feature records. The following
fragment shows the fields relevant to infrastructure and settings; inspect the
file produced for your package for the complete schema and inferred metadata:

```json
{
  "schemaVersion": "1.0",
  "package": {
    "id": "MyCompany.Elsa.Messaging",
    "version": "1.0.0"
  },
  "features": [
    {
      "id": "MyCompany.Elsa.Messaging.AzureServiceBus",
      "settings": [
        {
          "name": "ConnectionStringOrName",
          "required": true,
          "secret": true,
          "restartRequired": true
        }
      ],
      "infrastructure": [
        {
          "id": "azure-service-bus",
          "kind": "service-bus",
          "providers": ["Azure Service Bus"]
        }
      ]
    }
  ]
}
```

## Describe deploy-time settings

Use `ManifestSetting` on a public, settable feature property when the property
is a deploy-time setting. The released Azure Service Bus feature marks its
connection value as required, secret, and restart-sensitive:

```csharp
[ManifestSetting(
    DisplayName = "Connection string or name",
    Description = "Azure Service Bus connection string or configured name.",
    Category = "Connection",
    Secret = true,
    Required = true,
    HasRequired = true,
    RestartRequired = true)]
public string ConnectionStringOrName { get; set; } = string.Empty;
```

The metadata fields used by the 3.8 generator include:

| Field | Use it for |
| --- | --- |
| `DisplayName`, `Description` | Operator-facing labels and help text. |
| `Category`, `Group` | Organizing related settings. |
| `Required`, `HasRequired` | Required-setting metadata. |
| `DefaultValue` | A deployment default. |
| `Secret`, `Sensitive` | Sensitivity metadata. |
| `RestartRequired` | The setting takes effect after a restart. |
| `Advanced`, `Experimental` | Progressive disclosure and release status. |
| `UIHint` | A consumer-facing hint for choosing an editor or presentation. |

`Secret` and `Sensitive` are manifest hints, not secret storage. Keep
connection strings, tokens, and passwords in environment variables, a secret
manager, or another deployment-specific provider. The feature code must still
bind the value to the configuration section it actually uses. If the value is
intended to be a named Elsa workflow secret, see the [Secrets management
guide](../security/secrets-management.md) for the separate runtime feature and
its Studio picker.

For a modular host, the manifest setting name is not by itself the full
configuration path. The host places feature settings under the shell's
`Settings` object, and the feature selects the section it binds. For example,
the released MassTransit Azure Service Bus feature binds
`MassTransitAzureServiceBus:ConnectionStringOrName`, which can be represented
in the modular host configuration as:

```json
{
  "CShells": {
    "Shells": [
      {
        "Name": "Default",
        "Settings": {
          "MassTransitAzureServiceBus": {
            "ConnectionStringOrName": "${ConnectionStrings:Messaging}"
          }
        }
      }
    ]
  }
}
```

The package manifest documents the setting; the shell configuration and
feature binding determine whether the value is actually used. See
[Standalone and Modular Hosting](../architecture/standalone-and-modular-hosting.md)
and [Configuration Management](../deployment/configuration-management.md) for
host-specific configuration shapes.

For example, the released Quartz feature marks scheduler properties as
restart-required because they affect service registration and lifecycle
behavior. The manifest does not perform that restart; it communicates the
operational consequence.

## Build, inspect, and pack

Build the package before packing it, then inspect the generated manifest:

```bash
dotnet build -c Release
find obj -name elsa-package.json -print
dotnet pack -c Release
unzip -p bin/Release/*.nupkg elsa-package.json
```

The generator's defaults generate the manifest and include it in the package.
If a pipeline uses `dotnet pack --no-build`, the manifest must already exist
under the matching `obj/<configuration>/<target-framework>/` directory. A
missing manifest causes packing to fail when inclusion is enabled. Use the
generator's MSBuild properties to change this behavior, including
`GenerateElsaPackageManifest`, `ElsaPackageManifestOutputPath`, and
`ElsaPackageManifestIncludeInPackage`.

When metadata cannot be inferred from code, add an `elsa-package.overrides.json`
file beside the project file and pass it with
`ElsaPackageManifestOverrideFile`. Treat override data as packaging metadata:
it does not change the feature's runtime configuration.

## What this means for Studio users

An extension package can be compatible with Elsa Server, contribute activities
that a Studio user can place on a canvas, or provide a Studio-specific
feature. These are different concerns. The `elsa.server` hint in the released
Core and Extensions packages describes the package's runtime compatibility; it
does not mean that Studio will provision its database or message broker.

For a Studio-facing extension, document the separate Studio host registration,
activity or UI registration, backend URL, and any required permissions. Verify
the behavior in the target Studio host rather than assuming that a package
manifest turns `ManifestSetting` metadata into a designer editor.

## Extension author checklist

- Add the generator as a private build dependency for the package projects that
  contain CShells features.
- Declare package- or feature-level runtime compatibility.
- Add infrastructure metadata where a feature depends on an external resource.
- Mark only deploy-time properties as manifest settings.
- Make `ConfigurationKeys` match the names your feature actually reads.
- Keep sensitive values out of source-controlled JSON and example defaults.
- Build, inspect `elsa-package.json`, and verify the `.nupkg` contains the file.
- Document the actual Server and Studio registration paths separately.

For the complete feature implementation behind the examples, see the
[released Azure Service Bus shell feature](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/servicebus/Elsa.ServiceBus.AzureServiceBus/ShellFeatures/AzureServiceBusShellFeature.cs)
and [released Quartz shell feature](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/scheduling/Elsa.Scheduling.Quartz/ShellFeatures/QuartzFeature.cs).
