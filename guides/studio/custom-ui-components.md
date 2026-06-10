---
description: >-
  Source-backed guide to customizing Elsa Studio input editors in release
  3.8.0 using backend UI hints, property UI handlers, and Studio UI hint
  handlers.
---

# Custom UI Components in Studio

This guide is based on `release/3.8.0` in `elsa-core` and `elsa-studio`.

Custom activity editors in Elsa 3.8.0 are built in two layers:

1. Your activity declares a `UIHint` and optional `UIHandler` metadata on
   the backend.
2. Elsa Studio resolves that metadata to a Studio-side `IUIHintHandler`
   that renders the editor component.

Use this guide when you need a custom input editor for an activity
property. If you only need to add UI around an existing editor, use
[Field Extensions](../../studio/workflow-editor/field-extensions.md)
instead.

## The Customization Model

`elsa-core` and `elsa-studio` each have an `IUIHintHandler`, but they do
different jobs:

- In `elsa-core`, `IUIHintHandler` maps a UI hint string such as
  `dropdown` or `json-editor` to one or more backend
  `IPropertyUIHandler` types that provide UI metadata.
- In `elsa-studio`, `IUIHintHandler` is the rendering contract. Studio
  asks `IUIHintService` for the first handler whose
  `GetSupportsUIHint(string uiHint)` returns `true`, then calls
  `DisplayInputEditor(DisplayInputEditorContext context)`.

That means a working custom editor usually needs both sides:

- a backend activity descriptor with the right `UIHint`
- a Studio handler that knows how to render that hint

## When To Use Each Extension Point

Use `UIHint` when you need a different editor type.

Use `UIHandler` or `UIHandlers` when you want to keep an existing editor
type but supply metadata such as options, adornments, editor height, or
refresh behavior.

Use a Studio `IUIHintHandler` when Studio needs to render a brand-new
editor for a hint string that the default handler set does not support.

Use a Studio `IUIFieldExtensionHandler` when you want to decorate an
existing editor instead of replacing it.

## How The Pieces Fit Together

For a typical activity input, the flow is:

1. `ActivityDescriber` inspects your `[Input]` attribute.
2. It sets the input descriptor's `UIHint`.
3. `PropertyUIHandlerResolver` collects any explicit `UIHandler` or
   `UIHandlers`, then adds default metadata handlers associated with the
   chosen backend UI hint.
4. The resulting UI metadata is stored in `InputDescriptor.UISpecifications`.
5. Studio loads the activity descriptor, looks up a Studio
   `IUIHintHandler`, and renders the editor in the workflow inspector.
6. If the metadata contains `"Refresh": true`, Studio posts to
   `/descriptors/activities/{activityTypeName}/options/{propertyName}` to
   recompute UI metadata with the current input values as context.

This split is important: backend code decides what metadata an input has,
but Studio code decides how that input is rendered.

## Step 1: Mark The Activity Input

Start by assigning a UI hint on the activity:

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
    }
}
```

If your goal is only to customize a built-in editor, keep a built-in hint
and attach a property UI handler instead:

```csharp
[Input(
    UIHint = InputUIHints.DropDown,
    UIHandler = typeof(ChannelOptionsProvider)
)]
public Input<string> Channel { get; set; } = default!;
```

That pattern is used throughout the release branch for inputs such as:

- HTTP endpoint paths
- dispatcher channel selection
- SQL client selection
- Kafka producer and consumer selection

## Step 2: Add Backend UI Metadata When Needed

Backend `IPropertyUIHandler` implementations provide the metadata that
Studio editors consume.

For example, a dropdown options provider can inherit
`DropDownOptionsProviderBase`:

```csharp
using System.Reflection;
using Elsa.Workflows.UIHints.Dropdown;

public class ChannelOptionsProvider : DropDownOptionsProviderBase
{
    protected override ValueTask<ICollection<SelectListItem>> GetItemsAsync(
        PropertyInfo propertyInfo,
        object? context,
        CancellationToken cancellationToken)
    {
        ICollection<SelectListItem> items = new List<SelectListItem>
        {
            new("Default", ""),
            new("Priority", "Priority")
        };

        return new(items);
    }
}
```

If the available options depend on other inputs, override
`RefreshOnChange` in one of these base classes:

- `DropDownOptionsProviderBase`
- `CheckListOptionsProviderBase`
- `RadioListOptionsProviderBase`

When `RefreshOnChange` returns `true`, the backend includes
`"Refresh": true` in the UI metadata and Studio refreshes the descriptor
options through the activity descriptor options endpoint.

## Step 3: Render The Custom Hint In Studio

If you introduce a new hint string such as `custom-email-input`, Studio
must also know how to render it.

Implement `Elsa.Studio.Contracts.IUIHintHandler`:

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

Register it with DI:

```csharp
using Elsa.Studio.Extensions;

builder.Services.AddUIHintHandler<CustomEmailInputHandler>();
```

Studio resolves the first handler whose `GetSupportsUIHint` method
matches the hint. If none matches, `DefaultUIHintService` falls back to
`UnsupportedUIHintHandler`. If you are overriding a built-in hint,
registration order matters because the first matching handler wins.

## Step 4: Build The Editor Component

Studio editor components receive a `DisplayInputEditorContext`.

For literal string input, a component typically reads the current value
with `GetLiteralValueOrDefault()` and writes changes with
`UpdateValueOrLiteralExpressionAsync(...)`:

```razor
@using Elsa.Studio.Models

<ExpressionInput EditorContext="@EditorContext">
    <ChildContent>
        <MudTextField
            T="string"
            Label="@EditorContext.InputDescriptor.DisplayName"
            HelperText="@EditorContext.InputDescriptor.Description"
            Value="@EditorContext.GetLiteralValueOrDefault()"
            ValueChanged="OnValueChanged"
            InputType="InputType.Email"
            ReadOnly="EditorContext.IsReadOnly"
            Disabled="EditorContext.IsReadOnly" />
    </ChildContent>
</ExpressionInput>

@code {
    [Parameter] public DisplayInputEditorContext EditorContext { get; set; } = null!;

    private Task OnValueChanged(string value) =>
        EditorContext.UpdateValueOrLiteralExpressionAsync(value);
}
```

The same context also supports:

- `GetValueOrDefault<T>()`
- `GetObjectValueOrDefault()`
- `GetExpressionValueOrDefault()`
- `UpdateValueAsync(...)`
- `UpdateExpressionAsync(...)`
- `UpdateValueOrObjectExpressionAsync(...)`

Those helpers matter because many activity inputs are wrapped as
`Input<T>`, not plain values.

## Built-In Studio Handlers In 3.8.0

`AddDefaultUIHintHandlers()` in `elsa-studio` registers handlers for:

- `singleline`
- `checkbox`
- `checklist`
- `dictionary`
- `multitext`
- `multiline`
- `dropdown`
- `code-editor`
- `expression-editor`
- `json-editor`
- `switch-editor`
- `http-status-codes`
- `variable-picker`
- `input-picker`
- `type-picker`
- `workflow-definition-picker`
- `output-picker`
- `radiolist`
- `outcome-picker`
- `dynamic-outcomes`
- `datetime-picker`

The secrets module also adds a `secret-picker` Studio handler.

If you can express your requirement with one of those hints plus backend
metadata, that is usually simpler than inventing a brand-new hint.

## Field Extensions Versus Custom Editors

Field extensions are Studio-only decorations around existing editors.
They implement `IUIFieldExtensionHandler` and are registered with:

```csharp
services.AddUIFieldEnhancerHandler<CustomFieldExtension>();
```

Choose a field extension when you want to add helper UI, warnings,
buttons, or toolbars around an existing input component. Choose a custom
UI hint handler when the editor itself needs to change.

## Custom Elements Are A Different Feature

The custom-elements host is for embedding Elsa Studio surfaces in another
application. It does not register input editors.

The `Elsa.Studio.Host.CustomElements` host registers these elements in
`release/3.8.0`:

- `elsa-backend-provider`
- `elsa-workflow-definition-editor`
- `elsa-workflow-instance-viewer`
- `elsa-workflow-instance-list`
- `elsa-workflow-definition-list`

The React wrapper forwards `remote-endpoint`, `api-key`, and
`access-token` attributes to `elsa-backend-provider`. Use that model for
embedding Studio. Use `IUIHintHandler` when you want to change how an
activity input is edited inside Studio.

## Practical Guidance

- Start with a built-in UI hint before introducing a custom one.
- Use backend `UIHandler` metadata providers for options, adornments, and
  editor configuration.
- Add a Studio `IUIHintHandler` only when the built-in handlers cannot
  render the experience you need.
- Keep the backend and Studio halves aligned. A custom hint string is not
  enough on its own.
- Prefer field extensions when you are augmenting, not replacing, an
  existing editor.

## Related Reading

- [UI Hints](../../studio/workflow-editor/ui-hints.md)
- [Field Extensions](../../studio/workflow-editor/field-extensions.md)
- [Integration](integration/README.md)
