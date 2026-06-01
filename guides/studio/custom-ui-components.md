---
description: >-
  Learn how to create custom Elsa Studio activity input editors using the
  Blazor UI hint handler model used by Elsa Studio 3.7.0.
---

# Custom UI Components in Studio

Elsa Studio renders activity input editors through UI hint handlers. Activity descriptors provide a UI hint string, Studio resolves an `IUIHintHandler` that supports that hint, and the handler returns a Blazor `RenderFragment` for the editor.

In Elsa Studio 3.7.0, custom property editors are Blazor components registered through dependency injection. Studio does not expose a JavaScript property editor registry such as `window.elsa.propertyEditors`, an `IPropertyEditor` TypeScript contract, or a generic `<elsa-studio-root>` custom element.

## What You Can Customize

- **Input editors**: Register an `IUIHintHandler` for a UI hint such as `custom-email-input`.
- **Field extensions**: Add UI around existing field editors with `IUIFieldExtensionHandler`.
- **Content visualizers**: Display output values with custom visualizers.
- **Workflow custom elements**: Embed specific workflow surfaces such as `elsa-workflow-definition-editor` from the custom-elements host.

## How Studio Resolves Input Editors

When an activity is selected in the workflow editor, Studio:

1. Reads the activity descriptor and its input descriptors.
2. Looks at each input descriptor's UI hint.
3. Resolves the first registered `IUIHintHandler` whose `GetSupportsUIHint` method returns `true`.
4. Calls `DisplayInputEditor(DisplayInputEditorContext context)`.
5. Uses the supplied `DisplayInputEditorContext` to read and update the input value.

The built-in handlers are registered by `AddWorkflowsModule()`, which calls `AddDefaultUIHintHandlers()`.

## Built-In UI Hints

Elsa Studio 3.7.0 includes handlers for these well-known input UI hints:

| UI Hint | Purpose |
| --- | --- |
| `singleline` | Single-line text input |
| `multiline` | Multi-line text input |
| `checkbox` | Boolean checkbox |
| `checklist` | Multiple-choice checklist |
| `radiolist` | Single-choice radio list |
| `dropdown` | Dropdown selection |
| `multitext` | Multiple text values |
| `code-editor` | Code editor |
| `expression-editor` | Expression editor |
| `json-editor` | JSON editor |
| `variable-picker` | Workflow variable picker |
| `input-picker` | Workflow input picker |
| `output-picker` | Workflow output picker |
| `outcome-picker` | Workflow outcome picker |
| `workflow-definition-picker` | Workflow definition picker |
| `type-picker` | Type picker |
| `datetime-picker` | Date/time picker |
| `http-status-codes` | HTTP status code picker |
| `dynamic-outcomes` | Dynamic outcome editor |

## Requesting a Custom Editor from an Activity

Use the activity input's `UIHint` value to select your custom handler:

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Attributes;

namespace MyCompany.Activities;

[Activity("MyCompany", "Communication", "Sends an email")]
public class SendCustomEmail : CodeActivity
{
    [Input(
        Description = "Recipient email address",
        UIHint = "custom-email-input",
        DefaultValue = "user@example.com")]
    public Input<string> ToEmail { get; set; } = default!;

    protected override void Execute(ActivityExecutionContext context)
    {
        var email = context.Get(ToEmail);
        // Send email.
    }
}
```

## Creating a Custom UI Hint Handler

Implement `IUIHintHandler` and render a Blazor component from `DisplayInputEditor`:

```csharp
using Elsa.Studio;
using Elsa.Studio.Contracts;
using Elsa.Studio.Models;
using Microsoft.AspNetCore.Components;

namespace MyCompany.Studio;

public class CustomEmailInputHandler : IUIHintHandler
{
    public bool GetSupportsUIHint(string uiHint) => uiHint == "custom-email-input";

    public string UISyntax => WellKnownSyntaxNames.Literal;

    public RenderFragment DisplayInputEditor(DisplayInputEditorContext context)
    {
        return builder =>
        {
            builder.OpenComponent<CustomEmailInput>(0);
            builder.AddAttribute(1, nameof(CustomEmailInput.EditorContext), context);
            builder.CloseComponent();
        };
    }
}
```

Register the handler with the Studio service collection:

```csharp
using Elsa.Studio.Extensions;

builder.Services.AddUIHintHandler<CustomEmailInputHandler>();
```

Register custom handlers before `AddWorkflowsModule()` when you want to override a built-in UI hint. The UI hint service uses the first matching handler from DI, so registration order can matter when multiple handlers support the same hint.

## Creating the Blazor Editor Component

A custom editor receives a `DisplayInputEditorContext`. Use it to read the descriptor, honor read-only mode, and write changes back to the workflow definition.

```razor
@using Elsa.Studio.Models
@inject ILocalizer Localizer

@{
    var inputDescriptor = EditorContext.InputDescriptor;
    var value = EditorContext.GetLiteralValueOrDefault();
}

<ExpressionInput EditorContext="@EditorContext">
    <ChildContent>
        <MudTextField
            T="string"
            Label="@Localizer[inputDescriptor.DisplayName]"
            HelperText="@Localizer[inputDescriptor.Description]"
            Value="@value"
            ValueChanged="OnValueChanged"
            Variant="Variant.Outlined"
            Margin="Margin.Dense"
            InputType="InputType.Email"
            ReadOnly="EditorContext.IsReadOnly"
            Disabled="EditorContext.IsReadOnly" />
    </ChildContent>
</ExpressionInput>

@code {
    [Parameter] public DisplayInputEditorContext EditorContext { get; set; } = null!;

    private Task OnValueChanged(string newValue)
    {
        return EditorContext.UpdateValueOrLiteralExpressionAsync(newValue);
    }
}
```

`DisplayInputEditorContext` also exposes helpers for object values and direct value updates:

- `GetLiteralValueOrDefault()`
- `GetObjectValueOrDefault()`
- `GetExpressionValueOrDefault()`
- `UpdateValueAsync(object? value)`
- `UpdateExpressionAsync(Expression expression)`
- `UpdateValueOrLiteralExpressionAsync(string value)`
- `UpdateValueOrObjectExpressionAsync(object value)`

## Custom Elements

Elsa Studio 3.7.0 includes a custom-elements host, but the registered elements are workflow surfaces, not individual input editor registrations:

```csharp
builder.RootComponents.RegisterCustomElsaStudioElements();
builder.RootComponents.RegisterCustomElement<BackendProvider>("elsa-backend-provider");
builder.RootComponents.RegisterCustomElement<WorkflowDefinitionEditorWrapper>("elsa-workflow-definition-editor");
builder.RootComponents.RegisterCustomElement<WorkflowInstanceViewerWrapper>("elsa-workflow-instance-viewer");
builder.RootComponents.RegisterCustomElement<WorkflowInstanceListWrapper>("elsa-workflow-instance-list");
builder.RootComponents.RegisterCustomElement<WorkflowDefinitionListWrapper>("elsa-workflow-definition-list");
```

Use these when a host page needs to embed a workflow list, editor, or instance view. Use `IUIHintHandler` when you need to customize an activity input editor inside Studio.

## Further Reading

- [Field Extensions](../../studio/workflow-editor/field-extensions.md) - Adding UI around existing field editors.
- [Content Visualisers](../../studio/workflow-editor/content-visualisers-3.6-preview.md) - Custom output visualization.
- [Custom Activities](../../extensibility/custom-activities.md) - Creating activities that use custom UI hints.
