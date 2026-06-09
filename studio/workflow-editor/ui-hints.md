---
description: >-
  Source-backed guide to Elsa Studio UI hints in release 3.8.0, including
  default inference, built-in editors, option providers, and custom handlers.
---

# UI Hints

This page is based on the `release/3.8.0` source code in `elsa-core`
and `elsa-studio`.

UI hints tell Elsa Studio which editor to render for an activity input
or workflow input definition. In code, they are usually assigned with
`InputAttribute.UIHint`.

```csharp
[Input(
    Description = "Choose how to process the file",
    Options = new[] { "Archive", "Delete" },
    UIHint = InputUIHints.RadioList
)]
public Input<string> Action { get; set; } = default!;
```

## When You Need to Set a UI Hint

You do not always need to set one manually.

When `UIHint` is omitted, `ActivityDescriber.GetUIHint` in
`Elsa.Workflows.Core` applies these defaults:

| Property type | Default UI hint |
| --- | --- |
| `bool` / `bool?` | `checkbox` |
| `string` | `singleline` |
| `IEnumerable` | `dropdown` |
| enum / nullable enum | `dropdown` |
| `Variable` | `variable-picker` |
| `WorkflowInput` | `input-picker` |
| `Type` | `type-picker` |
| everything else | `singleline` |

Set `UIHint` explicitly when the default editor is not the one you want,
or when the editor needs extra behavior such as static options, custom
options, or a specialized Studio component.

## Built-In UI Hints

The `Elsa.Workflows.UIHints.InputUIHints` class in `elsa-core` exposes
these backend-facing constants:

| UI hint | Typical use |
| --- | --- |
| `checkbox` | Boolean values |
| `checklist` | Multi-select list from static or dynamic options |
| `code-editor` | Script or code text |
| `datetime-picker` | Date and time input |
| `dictionary` | Key-value pairs |
| `dropdown` | Single choice from a list |
| `dynamic-outcomes` | Outcome lists for script activities |
| `expression-editor` | Expression-oriented editor |
| `json-editor` | JSON payloads |
| `multiline` | Free-form multi-line text |
| `multitext` | Repeated text values such as tags or header names |
| `outcome-picker` | Existing workflow outcomes |
| `output-picker` | Workflow outputs |
| `radiolist` | Single choice shown as radio buttons |
| `singleline` | Short text input |
| `type-picker` | .NET type selection |
| `variable-picker` | Workflow variables |
| `input-picker` | Workflow inputs |
| `workflow-definition-picker` | Workflow definition selection |

Elsa Studio registers handlers for all of these and also includes
Studio-side handlers for:

- `http-status-codes`
- `flow-switch-editor`
- `switch-editor`

Those three are present in `elsa-studio` but are not exposed by the
`elsa-core` `InputUIHints` class in `release/3.8.0`. They are mainly
editor-specific hints rather than the usual constants used by custom
activity authors.

## Option-Based Editors

`dropdown`, `checklist`, and `radiolist` become useful when you provide options.

For static options, set `InputAttribute.Options`:

```csharp
[Input(
    Description = "Allowed HTTP methods",
    Options = new[] { "GET", "POST", "PUT", "DELETE" },
    UIHint = InputUIHints.CheckList
)]
public Input<ICollection<string>> Methods { get; set; } = default!;
```

In `release/3.8.0`, Elsa builds these option lists from
`InputAttribute.Options` through:

- `StaticDropDownOptionsProvider`
- `StaticCheckListOptionsProvider`
- `StaticRadioListOptionsProvider`

Enums also work with `dropdown` without providing `Options`.
`StaticDropDownOptionsProvider` turns enum values into select items
automatically, including nullable enums.

## Editors with Extra Metadata

Some hints render a specialized editor and also use UI metadata returned
from property UI handlers.

Examples from `release/3.8.0`:

- `json-editor` gets JSON-oriented code editor options from
  `JsonCodeOptionsProvider`.
- `code-editor` can be customized with a `UIHandler`, such as
  `RunCSharpOptionsProvider`, `RunJavaScriptOptionsProvider`, or
  `RunPythonOptionsProvider`.
- `singleline` can receive extra properties like `AdornmentText` and
  `EnableCopyAdornment`.

The `HttpEndpoint` activity uses that pattern for its `Path` input:

```csharp
[Input(
    UIHint = InputUIHints.SingleLine,
    UIHandler = typeof(HttpEndpointPathUIHandler)
)]
public Input<string> Path { get; set; } = default!;
```

`HttpEndpointPathUIHandler` adds the resolved base path as adornment
text and sets `"Refresh" = true`, which tells Studio to request fresh
UI metadata when needed.

## Dynamic Option Providers

When the options depend on runtime configuration or other activity
inputs, use a property UI handler instead of hard-coded `Options`.

The built-in `DispatcherChannelOptionsProvider` is a good example. It
inherits `DropDownOptionsProviderBase`, reads
`WorkflowDispatcherOptions`, and produces the list of available
dispatcher channels for `DispatchWorkflow` and
`BulkDispatchWorkflows`.

Custom providers follow the same pattern:

```csharp
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

Then attach it to the input:

```csharp
[Input(
    UIHint = InputUIHints.DropDown,
    UIHandler = typeof(ChannelOptionsProvider)
)]
public Input<string> Channel { get; set; } = default!;
```

If you need the editor metadata to refresh after a value change, derive
from one of these base classes and override `RefreshOnChange`:

- `DropDownOptionsProviderBase`
- `CheckListOptionsProviderBase`
- `RadioListOptionsProviderBase`

Studio checks for `"Refresh"` in the returned UI metadata and reloads
the descriptor options through the activity descriptor options API.

## What Studio Exposes in the Input Dialog

When a Studio user defines workflow inputs from the workflow editor, the
input dialog currently offers this curated set of hints:

- `singleline`
- `multiline`
- `checkbox`
- `checklist`
- `radiolist`
- `dropdown`
- `multitext`
- `code-editor`
- `variable-picker`
- `workflow-definition-picker`
- `output-picker`
- `outcome-picker`
- `json-editor`

This list comes from `EditInputDialog` in `Elsa.Studio.Workflows`. It is
smaller than the full handler set. For example, `dictionary`,
`datetime-picker`, `input-picker`, `dynamic-outcomes`, and the switch
editors can still be rendered for activity descriptors coming from the
backend, but they are not currently exposed in that dialog as
general-purpose workflow input choices.

## Related Features

- Use [Field Extensions](field-extensions.md) when you want to add
  Studio-side UI around an existing editor.
- Use [Custom UI Components](../../guides/studio/custom-ui-components.md)
  when you need a custom editor component instead of one of the
  built-in UI hint handlers.

## Practical Guidance

- Start with the default inferred editor unless you need something more specific.
- Use `Options` for simple fixed lists.
- Use `UIHandler` or `UIHandlers` when editor metadata depends on
  configuration or other inputs.
- Prefer the constants from `Elsa.Workflows.UIHints.InputUIHints` for
  backend code instead of hard-coded strings.
- If you need Studio-specific behavior that does not have a core
  constant in `release/3.8.0`, verify that the corresponding Studio
  handler exists before relying on the raw string value.
