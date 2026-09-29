---
description: Convert activity outputs at typed variable and workflow-output bindings in Elsa 3.9.0.
---

# Output converters

An output converter transforms an activity's native output only when Elsa
writes that output to a selected variable or workflow-output binding. Use one
when the activity produces a value in one type but a particular destination
expects another type, such as formatting a `decimal` as text.

Conversion is synchronous, deterministic, and side-effect free. It is a
binding-level transformation, not a general serializer, an expression engine,
or a hook for asynchronous work. The activity's output must still be
compatible with the converter's declared source type, and the converted value
must be assignable to the destination type.

## Runtime model

An output binding can contain an optional converter configuration:

```json
{
  "typeName": "Decimal",
  "memoryReference": {
    "id": "formatted-total"
  },
  "converter": {
    "id": "sample.number-to-text.v1",
    "settings": {
      "format": "0.00"
    }
  }
}
```

Without `converter`, Elsa uses the normal output-binding assignment. With it,
Elsa resolves the registered converter, validates the declared source and
destination types and the settings, invokes the converter, validates the
result, and then writes the result to the destination. A converter can be
configured only when the output has a variable or workflow-output destination.

The converter receives four values in its `OutputConversionContext`: the
non-null native value, its declared source type, the destination type, and a
clone of the optional JSON settings. The activity output register retains the
native value; only the selected destination receives the converted value.

Null values are not passed to the converter. Elsa delivers a null value only
when the destination permits null; otherwise the assignment fails validation.
If conversion or result validation fails, Elsa leaves the destination
unchanged and reports an `OutputConversionException` through the normal
activity fault pipeline.

See the [Core output converter contract](https://github.com/elsa-workflows/elsa-core/blob/0352fd28b41ff072152620bc49f5f91217d109fe/src/modules/Elsa.Workflows.Core/Contracts/IOutputConverter.cs),
[invoker](https://github.com/elsa-workflows/elsa-core/blob/0352fd28b41ff072152620bc49f5f91217d109fe/src/modules/Elsa.Workflows.Core/Services/OutputConverterInvoker.cs),
the [execution-context binding path](https://github.com/elsa-workflows/elsa-core/blob/0352fd28b41ff072152620bc49f5f91217d109fe/src/modules/Elsa.Workflows.Core/Contexts/ActivityExecutionContext.cs),
and [output JSON converter](https://github.com/elsa-workflows/elsa-core/blob/0352fd28b41ff072152620bc49f5f91217d109fe/src/modules/Elsa.Workflows.Core/Serialization/Converters/OutputJsonConverter.cs)
for the release implementation.

## Add a converter to a server

Implement `IOutputConverter`, give it a stable public ID, describe its source
and result types, and register it with `AddOutputConverter`:

```csharp
// NumberToTextConverter.cs
using System.Globalization;
using System.Text.Json;
using Elsa.Workflows;
using Elsa.Workflows.Models;

public sealed class NumberToTextConverter : IOutputConverter
{
    public object Convert(OutputConversionContext context)
    {
        var format = context.Settings is { } settings &&
                     settings.ValueKind == JsonValueKind.Object &&
                     settings.TryGetProperty("format", out var formatElement)
            ? formatElement.GetString()
            : null;

        return ((decimal)context.Value).ToString(format, CultureInfo.InvariantCulture);
    }
}
```

Register the converter from the server's startup code:

```csharp
// Program.cs
using System.Text.Json;
using Elsa.Extensions;

using var schemaDocument = JsonDocument.Parse("""
    {
      "type": "object",
      "properties": {
        "format": { "type": "string", "default": "0.00" }
      }
    }
    """);

builder.Services.AddOutputConverter<NumberToTextConverter>(
    new OutputConverterDescriptor(
        "sample.number-to-text.v1",
        typeof(decimal),
        typeof(string),
        "Number to text",
        "Formats a decimal using an invariant format.",
        schemaDocument.RootElement));
```

The default converter lifetime is scoped. Elsa uses the converter ID as the
keyed service key and rejects duplicate IDs, including IDs that differ only by
case. Treat the ID as a persisted contract: do not reuse it for breaking
behavior or incompatible settings. Register a new versioned ID instead.

The optional `SettingsSchema` is JSON Schema for per-binding settings. It can
provide defaults for Studio's settings editor. Implement
`ValidateSettings` when validation depends on rules that the schema cannot
express, and do not include sensitive setting values in returned messages.

For the registration API, see the [3.9.0 service-collection extension](https://github.com/elsa-workflows/elsa-core/blob/0352fd28b41ff072152620bc49f5f91217d109fe/src/modules/Elsa.Workflows.Core/Extensions/OutputConverterServiceCollectionExtensions.cs)
and [descriptor model](https://github.com/elsa-workflows/elsa-core/blob/0352fd28b41ff072152620bc49f5f91217d109fe/src/modules/Elsa.Workflows.Core/Models/OutputConverterDescriptor.cs).

## Use a converter in a workflow

The persisted configuration belongs to an `Output<T>` binding. In code, the
binding can be configured with an `OutputConverterConfiguration`:

```csharp
// In a workflow-building method.
using System.Text.Json;
using Elsa.Workflows.Models;

using var settingsDocument = JsonDocument.Parse("""{ "format": "0.00" }""");

activity.Result = new Output<decimal>(targetVariable)
{
    Converter = new OutputConverterConfiguration(
        "sample.number-to-text.v1",
        settingsDocument.RootElement)
};
```

In Elsa Studio, select an activity and open its **Outputs** tab. First bind
the output to a workflow variable or workflow output. Studio then asks the
connected server for converters compatible with the output and destination
types. Choose a converter and edit its settings; the descriptor's display name,
description, and optional JSON Schema drive the available UI.

Studio keeps converter discovery scoped to the declared source/destination
type pair and caches successful requests for that pair. A read-only workspace
disables converter editing. If a saved workflow references a converter that is
not returned by discovery, Studio keeps the persisted ID visible and warns
that the converter is unavailable; it does not silently delete the binding.

See the [Studio Outputs tab](https://github.com/elsa-workflows/elsa-studio/blob/08c0fe72b52a28129d6e8f66cb7d05c92c5508f3/src/modules/Elsa.Studio.Workflows/Components/WorkflowDefinitionEditor/Components/ActivityProperties/Tabs/Outputs/Components/OutputsTab.razor)
and its [discovery and binding logic](https://github.com/elsa-workflows/elsa-studio/blob/08c0fe72b52a28129d6e8f66cb7d05c92c5508f3/src/modules/Elsa.Studio.Workflows/Components/WorkflowDefinitionEditor/Components/ActivityProperties/Tabs/Outputs/Components/OutputsTab.razor.cs).

## Discovery API and permissions

Elsa exposes compatible converter descriptors through:

```http
GET /descriptors/output-converters?sourceType=Decimal&destinationType=String
```

Both query parameters are required. Each type must be a registered type alias
or a resolvable safe type name. The response contains metadata only: the
stable ID, source and result type names, display text, description, and
optional settings schema. It does not expose converter instances or
implementation types.

The endpoint requires the `view` verb on the
`workflows/descriptors/output-converters` resource. Grant this permission to
the Studio/backend caller that needs to populate the Outputs tab. It is a
discovery permission; it does not grant permission to publish workflow
definitions or execute workflows. See the [Core endpoint](https://github.com/elsa-workflows/elsa-core/blob/0352fd28b41ff072152620bc49f5f91217d109fe/src/modules/Elsa.Workflows.Api/Endpoints/OutputConverters/List/Endpoint.cs)
and [permission descriptor](https://github.com/elsa-workflows/elsa-core/blob/0352fd28b41ff072152620bc49f5f91217d109fe/src/modules/Elsa.Workflows.Api/Permissions/WorkflowPermissions.cs).

## Validation and deployment

Elsa validates converter bindings when a workflow definition is validated and
again when the binding runs. The checks include:

- a non-empty, registered converter ID;
- a variable or workflow-output destination;
- compatibility between the activity output and converter source type;
- compatibility between converter result type and destination type; and
- a JSON-object settings value that satisfies the registered schema and
  converter-owned validation.

Deploy the converter implementation and its registration to every server that
may validate or execute a definition that references it. A workflow can be
saved with converter JSON, but publishing or execution will fail if the
server cannot resolve that ID or if the deployed implementation no longer
accepts the binding's types or settings. Removing or changing a converter is
therefore a workflow-definition compatibility change; use a new ID for a
breaking version and migrate bindings deliberately.

The management validation handler is implemented in
[`ValidateOutputConverters`](https://github.com/elsa-workflows/elsa-core/blob/0352fd28b41ff072152620bc49f5f91217d109fe/src/modules/Elsa.Workflows.Management/Handlers/Notifications/ValidateOutputConverters.cs).

## Design and troubleshooting guidance

- Keep converters small and deterministic. Use an activity when the operation
  needs I/O, asynchronous work, retries, or workflow state.
- Make locale, timezone, rounding, encoding, and similar environmental choices
  explicit settings rather than reading ambient process state.
- Treat settings as per-binding workflow data. Do not put credentials or other
  secrets in converter settings; use Elsa's [secrets management](../security/secrets-management.md)
  and resolve secrets through an activity or another explicit integration
  boundary.
- If Studio shows no converter, verify that the output is bound, the server
  registered a compatible descriptor, the connected caller has descriptor
  `view` permission, and the backend API can resolve the declared type names.
- If publishing or execution fails, check the converter ID, source/result type
  pair, settings schema, converter registration, and server logs for the
  conversion failure stage.

Output converters are available in the Core runtime and Studio 3.9.0 surfaces;
they do not create an administration screen for converter implementations or
provision external resources.
