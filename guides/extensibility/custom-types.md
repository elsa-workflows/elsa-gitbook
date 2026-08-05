---
description: >-
  Register custom CLR types for Elsa workflow variables, inputs, outputs,
  expressions, serialization, and Elsa Studio in Elsa 3.8.0.
---

# Registering Custom Types

Use a custom type when a workflow variable, input, output, argument, or
persisted value needs to use an application-defined CLR type. In Elsa 3.8,
type registration crosses several boundaries. A type can be known to the
workflow serializer without appearing in Studio, or be visible in Studio
without being exposed as a JavaScript runtime class.

This guide covers CLR type registration. It is different from:

- [Activity type providers](activity-type-providers.md), which populate the
  activity catalog.
- [JavaScript type-definition providers](../studio/javascript-type-definition-providers.md),
  which add TypeScript declarations to Studio's JavaScript editor.
- A [custom serializer](../plugins-modules/README.md#custom-serializers),
  which changes how a value is encoded rather than registering its Elsa type
  identifier.

## Choose the registration surface

| Need | Use | Studio | Runtime |
| --- | --- | --- | --- |
| Variable, input, output | `AddVariableTypeAndAlias<T>` | Yes | All aliases |
| Persisted type only | `RegisterTypeAlias` | No | JSON resolution |
| JavaScript CLR class | `JintOptions.RegisterType<T>` | No | Jint class |
| JS IntelliSense | Variable + JavaScript | Declarations | Types |

For most application-defined workflow data types, use
`AddVariableTypeAndAlias<T>`. It keeps the alias used by Studio, expression
conversion, and workflow serialization consistent.

The `AddVariableTypeAndAlias<T>` helper also registers the type with Elsa's
well-known type registry. That registry handles literal and argument
conversion. Use it instead of wiring `IWellKnownTypeRegistry` manually for
normal workflow data.

## Register a type for Studio and workflow data

The feature-style API is the most direct option when configuring Elsa through
`AddElsa`:

```csharp
using System.ComponentModel;
using Elsa.Extensions;
using Elsa.Workflows.Management;

[Description("A summary of an order returned by the order service.")]
public sealed record OrderSummary(string OrderId, decimal Total);

builder.Services.AddElsa(elsa => elsa
    .UseWorkflowManagement(management =>
        management.AddVariableTypeAndAlias<OrderSummary>("OrderSummary", "Orders")));
```

The alias is the stable identifier stored in workflow definitions. Prefer an
explicit alias such as `OrderSummary` over a name that depends on a namespace
or assembly. The category groups the entry in Studio. The optional
`DescriptionAttribute` supplies the Studio description when no explicit
descriptor description is provided.

If your host uses the shell-compatible `IServiceCollection` extensions, the
equivalent is:

```csharp
using Elsa.Workflows.Management.Extensions;

builder.Services.AddVariableTypeAndAlias<OrderSummary>(
    alias: "OrderSummary",
    category: "Orders");
```

The release implementation adds a `VariableDescriptor`, registers the alias
with expression options, and registers it with `SerializationTypeOptions`.
That is why this helper is preferable to adding only a Studio descriptor or
only a JSON alias.

Extension modules use the same contract. For example, the 3.8.0 compression
extension makes `ZipEntry` available under the `Compression` category with a
stable `ZipEntry` alias:

```csharp
public override void Configure()
{
    Module.AddVariableTypeAndAlias<ZipEntry>("ZipEntry", "Compression");
}
```

Use this pattern in a feature's `Configure` method when the type belongs to a
reusable Elsa extension rather than one host application.

### What Studio receives

Elsa Server exposes registered variable types from
`GET /descriptors/variables`. The endpoint returns the type name, display name,
category, and description; it requires `read:*` or
`read:variable-descriptors`. Elsa Studio calls this endpoint and groups the
returned descriptors by category in its type picker. The same remote list
supplies the type choices for workflow variables, inputs, outputs, and
type-picker UI hints.

Studio stores the returned `TypeName` in the workflow definition. The browser
does not load or execute your CLR type. The Elsa Server that imports, edits,
or executes the definition must have the type's assembly and the same alias
registration.

## Register only a serialization alias

Use `SerializationTypeOptions` directly when a type must be resolved from
workflow JSON but should not be selectable as a Studio variable type:

```csharp
using Elsa.Common.Serialization;
using Elsa.Extensions;
using Microsoft.Extensions.DependencyInjection;

builder.Services.Configure<SerializationTypeOptions>(options =>
    options.RegisterTypeAlias(typeof(OrderSummary), "OrderSummary"));
```

`RegisterTypeAlias` creates both directions of the preferred mapping:

- The `OrderSummary` identifier resolves to the CLR `OrderSummary` type when
  Elsa reads a type identifier.
- The CLR `OrderSummary` type is emitted as `OrderSummary` when Elsa writes
  the type.

This registration does not add a `VariableDescriptor`, so it does not add an
entry to the Studio variable picker. If the type is used as a workflow input,
output, or variable, prefer `AddVariableTypeAndAlias` so the serializer and
Studio use the same contract.

Do not treat a CLR name supplied by workflow JSON as a request to load an
arbitrary assembly. In 3.8.0, unregistered `Type` metadata is written with an
`UnregisteredClrType:` marker and resolves to `System.Exception` on read;
polymorphic object metadata is omitted when Elsa cannot resolve a registered
alias. Register only types that the host intentionally supports.

### Preserve compatibility when a type moves

Aliases are part of persisted workflow data. Keep an old identifier readable
when renaming a type or changing its preferred alias:

```csharp
builder.Services.Configure<SerializationTypeOptions>(options =>
{
    options.RegisterTypeAlias(typeof(OrderSummary), "OrderSummary");
    options.RegisterLegacyTypeName(
        typeof(OrderSummary),
        "Legacy.Contracts.OrderSummary, Legacy.Contracts");
});
```

The `AddTypeAliasWithLegacyName<T>` helper is useful when the legacy identifier
is the type's current simple assembly-qualified name:

```csharp
builder.Services.Configure<SerializationTypeOptions>(options =>
    options.AddTypeAliasWithLegacyName<OrderSummary>("OrderSummary"));
```

Treat aliases as versioned data. Deploy the compatibility registration before
publishing definitions that use the new alias, and keep it on every node that
may import or execute older definitions.

## Understand expressions and JavaScript

`AddVariableTypeAndAlias` also registers the alias in Elsa's well-known type
registry. Elsa uses that registry when converting literal expressions,
workflow arguments, and workflow inputs. It is the reason a Studio-selected
type can be converted by the server without dynamically loading an arbitrary
CLR type from a workflow document.

When JavaScript is enabled, Elsa additionally registers the configured
variable types with the JavaScript type-alias registry at host startup. The
JavaScript type-definition provider describes eligible custom classes,
interfaces, and enums for IntelliSense. Primitive, generic, and several
dynamic types are intentionally excluded. See
[JavaScript IntelliSense type definitions](../studio/javascript-type-definition-providers.md)
for the editor endpoint and declaration flow.

Registering a variable type does not expose every public CLR class to a
JavaScript expression. If a trusted application explicitly needs a CLR class
at JavaScript runtime, configure Jint separately:

```csharp
using Elsa.Extensions;

builder.Services.AddElsa(elsa => elsa
    .UseJavaScript(options => options
        .RegisterType<OrderSummary>()));
```

`RegisterType<T>()` adds the class to the Jint engine under `OrderSummary`,
using the CLR type name. It is not a serialization alias and does not create a
Studio variable-picker entry. Avoid enabling broad CLR access or exposing
application classes when workflows can be authored by untrusted users; the
release `JintOptions` documentation explicitly treats that as a trust-boundary
decision.

## Design and deployment checklist

Before publishing a workflow that uses a custom type, verify:

1. The type is concrete enough for the value being stored. Interfaces and
   abstract types can be described or used as contracts, but they are not
   automatically instantiable workflow values.
2. The alias is explicit, unique, and stable. Do not reuse an alias for a
   different CLR type.
3. Every Elsa Server node has the same type assembly and registration.
4. A type that must be selectable in Studio uses
   `AddVariableTypeAndAlias<T>`, not only `RegisterTypeAlias`.
5. The Studio backend identity has `read:variable-descriptors` (or the
   broader `read:*`) if the picker is empty or the endpoint returns 403.
6. A type rename includes a legacy identifier registration and a rollout plan
   for existing definitions.
7. JavaScript runtime exposure is intentional and limited to trusted workflow
   authors.

If Studio does not show the type, check the server's `/descriptors/variables`
response first. If Studio shows the type but publishing or execution fails,
compare the saved `TypeName` with the alias registered on the server and check
that the server process loaded the assembly. If an old definition cannot be
read after a deployment, restore its legacy alias before changing the
definition itself.

## Release source

The guide is validated against Elsa 3.8.0:

- [`AddVariableTypeAndAlias` and management options](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Management/Extensions/ModuleExtensions.cs)
- [`IServiceCollection` variable-type registration](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Management/Extensions/ManagementServiceCollectionExtensions.cs)
- [`SerializationTypeOptions`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Common/Serialization/SerializationTypeOptions.cs)
- [`TypeJsonConverter` trust boundary](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Core/Serialization/Converters/TypeJsonConverter.cs)
- [`WellKnownTypeRegistry`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Expressions/Services/WellKnownTypeRegistry.cs)
- [`/descriptors/variables`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Api/Endpoints/VariableTypes/List/Endpoint.cs)
- [`VariableTypeDefinitionProvider`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Expressions.JavaScript/Providers/VariableTypeDefinitionProvider.cs)
- [`JintOptions.RegisterType`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Expressions.JavaScript/Options/JintOptions.cs)
- [`RemoteVariableTypeService` in Studio](https://github.com/elsa-workflows/elsa-studio/blob/release/3.8.0/src/modules/Elsa.Studio.Workflows.Core/Domain/Services/RemoteVariableTypeService.cs)
- [`CompressionFeature` extension example](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/src/modules/io/Elsa.IO.Compression/Features/CompressionFeature.cs)
