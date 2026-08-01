---
description: >-
  Extend Elsa Studio JavaScript IntelliSense with release-backed TypeScript
  declarations from the Elsa Server.
---

# JavaScript IntelliSense type definitions

Use a JavaScript type-definition provider when custom workflow values should
be discoverable in Elsa Studio's JavaScript editor. The provider supplies
TypeScript declarations for Monaco IntelliSense; it does not add a runtime
object, function, variable, or CLR type to the JavaScript engine.

This guide is based on the `release/3.8.0` source code in `elsa-core` and
`elsa-studio`.

## How the pieces fit together

When a Studio user opens a JavaScript expression editor for an activity input:

1. Studio sends the workflow definition ID, activity type name, and property
   name to `POST /scripting/javascript/type-definitions/{workflowDefinitionId}`.
2. Elsa Server resolves the latest workflow graph and asks every registered
   JavaScript definition provider for declarations.
3. Core renders the combined result as a TypeScript declaration document and
   returns it as `application/x-typescript`.
4. Studio adds the document to Monaco with
   `javascriptDefaults.addExtraLib`, enabling completion and type information
   for the open JavaScript editor.

The declaration document is contextual: a provider receives the workflow graph,
the activity type name, the property name, and a cancellation token. A provider
can therefore emit declarations for a particular activity input or workflow
shape, rather than publishing one unconditional global library.

## Choose the right extension point

The JavaScript feature has separate provider contracts:

| Goal | Extension point |
| --- | --- |
| Declare a custom type for IntelliSense | `ITypeDefinitionProvider` |
| Describe a global function | `IFunctionDefinitionProvider` |
| Describe a global variable | `IVariableDefinitionProvider` |
| Make a .NET type available at runtime | `JintOptions.RegisterType(...)` |

The first three affect the generated declaration file. `RegisterType` affects
Jint runtime configuration and is a separate security-sensitive decision. A
type-definition provider does not make `new Order()` or `order.Total` execute
successfully by itself; the expression still needs a real runtime value or
function supplied by the workflow context or JavaScript configuration.

## Add a provider

Reference the JavaScript expressions package and derive from
`TypeDefinitionProvider`:

```csharp
using Elsa.Expressions.JavaScript.TypeDefinitions.Abstractions;
using Elsa.Expressions.JavaScript.TypeDefinitions.Models;

public sealed class OrderIntellisenseProvider : TypeDefinitionProvider
{
    protected override IEnumerable<TypeDefinition> GetTypeDefinitions(
        TypeDefinitionContext context)
    {
        if (context.ActivityTypeName != "Acme.SendOrder")
            yield break;

        yield return new TypeDefinition
        {
            DeclarationKeyword = "interface",
            Name = "Order",
            Properties =
            {
                new PropertyDefinition { Name = "Id", Type = "string" },
                new PropertyDefinition { Name = "Total", Type = "number" },
                new PropertyDefinition { Name = "IsPriority", Type = "boolean" }
            }
        };
    }
}
```

`DeclarationKeyword` is rendered directly into the TypeScript declaration,
so use a keyword that produces valid output with the release renderer, such as
`interface`, `class`, or `enum`. Property `Type` values are TypeScript type
expressions such as `string`, `number`, `boolean`, or an already-declared
type. The provider should emit valid, stable names because the generated text
is inserted directly into Monaco.

The provider can also override the asynchronous method when declarations need
to be loaded from a service. Honor `context.CancellationToken` for remote or
expensive work.

## Register the provider

Register the provider in the server's dependency-injection container alongside
the JavaScript feature:

```csharp
using Elsa.Extensions;
using Elsa.Expressions.JavaScript.Extensions;

services.AddElsa(elsa =>
{
    elsa.UseJavaScript();
});

services.AddTypeDefinitionProvider<OrderIntellisenseProvider>();
```

Core registers the provider as a scoped service and includes all registered
`ITypeDefinitionProvider` instances when it builds the declaration document.
Constructor injection is therefore available for providers that need access to
application services.

## Keep runtime behavior separate

This is the boundary to keep in mind:

```csharp
// Runtime configuration: changes what Jint can access.
elsa.UseJavaScript(options => options.RegisterType<Order>());

// Editor configuration: changes what Studio can suggest and type-check.
services.AddTypeDefinitionProvider<OrderIntellisenseProvider>();
```

The two registrations can be used together, but neither one replaces the
other. If a workflow variable is the runtime value, create or bind that
variable through the normal workflow APIs. If its shape should appear in
Studio, emit a matching declaration. If a custom global function is available
at runtime, describe it with an `IFunctionDefinitionProvider` as well.

Do not enable broad CLR access only to make IntelliSense understand a type.
`AllowClrAccess` is intended for trusted scenarios; editor declarations are
the safer way to describe a known shape to workflow authors.

## Verify the Studio path

After registering the provider:

1. Enable JavaScript expressions on the Elsa Server.
2. Ensure the connected Studio host can read the workflow definition.
3. Open an activity input whose expression syntax is **JavaScript**.
4. Type the declared type or property name and confirm that Monaco offers the
   expected completion or type information.

The endpoint requires either `read:*` or the specific
`read:javascript-type-definitions` permission. A missing permission can look
like a provider problem because Studio cannot load the declaration library.
The endpoint resolves the latest workflow graph; a missing workflow
definition returns an API error instead of a declaration document.

## Troubleshooting

### The type does not appear in completion

Check the following in order:

- The server has `UseJavaScript()` enabled.
- The provider is registered in the server process connected to Studio.
- The provider's context filters do not exclude the current activity type.
- The declaration uses valid TypeScript syntax and identifiers.
- The Studio client has `read:javascript-type-definitions` or a wildcard
  permission.
- The input is using the **JavaScript** expression type, not **Default**,
  **Liquid**, or a custom UI editor.

### The editor suggests the type but the workflow fails

That is expected when only a declaration was registered. IntelliSense does not
create runtime values. Bind the value through workflow inputs, variables, or
activity outputs, or configure the corresponding JavaScript runtime behavior.

### The declaration is stale

Studio requests the declaration document when the JavaScript Monaco editor is
initialized. Reopen or reinitialize the editor after changing provider output,
and ensure the server is running the updated provider registration.

## Release source references

This page was checked against `release/3.8.0`:

- Core [`ITypeDefinitionProvider`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Expressions.JavaScript/TypeDefinitions/Contracts/ITypeDefinitionProvider.cs)
- Core [`TypeDefinitionContext`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Expressions.JavaScript/TypeDefinitions/Models/TypeDefinitionContext.cs)
- Core [`TypeDefinitionService`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Expressions.JavaScript/TypeDefinitions/Services/TypeDefinitionService.cs)
- Core [`AddTypeDefinitionProvider`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Expressions.JavaScript/Extensions/DependencyInjectionExtensions.cs)
- Core [type-definition endpoint](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Expressions.JavaScript/Endpoints/TypeDefinitions/Endpoint.cs)
- Studio [`TypeDefinitionService`](https://github.com/elsa-workflows/elsa-studio/blob/release/3.8.0/src/framework/Elsa.Studio.Shared/Services/TypeDefinitionService.cs)
- Studio [`JavaScriptMonacoHandler`](https://github.com/elsa-workflows/elsa-studio/blob/release/3.8.0/src/framework/Elsa.Studio.Shared/Monaco/Handlers/JavaScriptMonacoHandler.cs)
