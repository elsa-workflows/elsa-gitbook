---
description: >-
  Learn how Elsa Studio expression editors map to Elsa Server expression
  engines, how to access workflow variables, and which syntax choices are
  actually available in Elsa 3.8.
---

# Expressions in Elsa Studio

Elsa Studio lets you enter activity values in different expression syntaxes. The exact choices you see are not hardcoded in Studio alone: Studio asks Elsa Server for the available expression descriptors and renders those options in the property editor.

This matters for two reasons:

- The list of available expression types depends on which expression features your server enables.
- Some syntaxes shown in Studio are UI helpers, while others are real runtime expression engines.

## How expression selection works

For most activity inputs, Studio shows a **Default** option plus one or more code-oriented expression types.

- **Default** means: use the activity's normal UI editor for that field.
- **Literal** and **Object** are built-in Studio/UI syntaxes used behind the scenes for plain values and structured values.
- **JavaScript**, **Liquid**, **C#**, **Python**, and other custom types are provided by the server.

In the 3.8 sample server, the app enables these expression engines:

```csharp
.UseCSharp(...)
.UseJavaScript(...)
.UsePython(...)
.UseLiquid(...)
```

If your server does not register one of these features, Studio will not offer that syntax.

{% hint style="info" %}
In Elsa 3.8, C# and Python are only browsable in Studio when host code execution is allowed for those engines. If you do not see them in the picker, check your server configuration first.
{% endhint %}

## Which syntax should you use?

Use the simplest option that matches the job:

- **Default** for plain text, numbers, booleans, dropdowns, and other normal field editors.
- **JavaScript** for most dynamic value composition and workflow-variable lookups.
- **Liquid** for templated text output.
- **C#** when you explicitly want Roslyn-based expressions and have enabled them for trusted authors.
- **Python** only when your server is configured for Python.NET expressions and you need it specifically.

## JavaScript expressions

JavaScript is usually the most convenient dynamic syntax in Studio.

### Reading variables

If variable wrappers are enabled and your variable names are valid JavaScript property names, you can use:

```javascript
variables.OrderId
variables.Customer.Name
variables.Items[0]
```

Elsa also exposes explicit helper functions:

```javascript
getVariable("OrderId")
getVariable("Customer")
```

Use `getVariable` when:

- the variable name is not a valid JavaScript identifier.
- variable wrappers were disabled on the server.
- you want to avoid depending on the `variables.SomeName` convenience wrapper.

### Writing variables

To assign a value from JavaScript, use:

```javascript
setVariable("OrderId", getGuidString())
setVariable("Status", "Approved")
```

### Accessing inputs and outputs

Elsa's JavaScript helpers also include:

```javascript
getInput("CustomerId")
getOutputFrom("SendHttpRequest", "ParsedContent")
getLastResult()
```

### Important wrapper limitation

The `variables.SomeName` style depends on two JavaScript runtime settings:

- variable wrappers must be enabled.
- variable copying must be enabled.

If either is disabled, use `getVariable(...)` and `setVariable(...)` instead.

## C# expressions

C# expressions are available when the server enables the C# expression feature and allows host code execution for trusted workflow authors.

Elsa generates a variable proxy for C# expressions. In Elsa 3.8, both `Variables` and `Variable` work, but `Variable` is the preferred alias.

### Reading variables

```csharp
Variable.OrderId
Variable.Get<Guid>("OrderId")
Variable.Get<string>("CustomerName")
```

### Writing variables

```csharp
Variable.Set("OrderId", Guid.NewGuid());
Variable.Set("Status", "Completed");
```

### When to prefer `Get<T>()`

Use `Get<T>()` when:

- the variable name is not a valid generated property name.
- you want explicit typing.
- wrappers were disabled in C# options.

## Liquid expressions

Liquid is best for text templating.

Elsa 3.8 registers workflow variables through the `Variables` object and workflow or activity inputs through `Input`.

```liquid
Hello {{ Variables.CustomerName }}
Order {{ Variables.OrderId }} is {{ Variables.Status }}
Customer input: {{ Input.CustomerId }}
```

Use Liquid when the result should primarily be formatted text, not when you need more procedural logic.

## Python expressions

Python expressions are available only when the Python feature is enabled and host code execution is allowed.

Elsa injects these globals into the Python scope:

- `execution_context`
- `input`
- `output`
- `outcome`
- `variables`

Typical variable access looks like this:

```python
variables.OrderId
variables.set("Status", "Processed")
variables.get("CustomerName")
```

## Variable naming guidance

If you want the most convenient cross-language experience in Studio, use variable names that are valid identifiers, such as:

- `OrderId`
- `CustomerName`
- `RetryCount`

Avoid names that require quoting or special handling, such as:

- `order-id`
- `customer name`
- `123value`

Those names can still work, but you will need accessor methods like `getVariable("order-id")` instead of wrapper-style property access.

## Variable storage and expressions

Expressions read the current value of workflow variables regardless of whether the variable is stored in memory or in workflow-instance storage.

In Elsa 3.8:

- **Memory** storage keeps the value in memory for the lifetime of the current execution context.
- **Workflow Instance** storage persists the value in workflow state.
- The legacy **Workflow** storage driver still exists for backward compatibility, but `Workflow Instance` is the current persisted option.

Choose storage based on lifecycle and suspension/resumption needs; choose expression syntax based on authoring convenience.

## Recommended patterns

- Use **Default** unless you actually need dynamic behavior.
- Use **JavaScript** for most dynamic field values in Studio.
- Use **Liquid** for generated messages, templates, and text bodies.
- Use **C#** and **Python** only for trusted-author scenarios where you intentionally enable host code execution.
- Prefer identifier-friendly variable names so wrapper syntax stays simple.

## Common pitfalls

### "I don't see C# or Python in Studio"

Usually this means the backend did not enable that engine, or host code execution for that engine is disabled.

### "`variables.MyValue` does not work in JavaScript"

Check these first:

- the variable name is a valid identifier.
- JavaScript variable wrappers were not disabled.
- JavaScript variable copying was not disabled.

If any of those are not true, switch to:

```javascript
getVariable("MyValue")
```

### "My field shows Default instead of a code editor"

That is normal. `Default` uses the activity's UI hint. Switch the syntax picker to a code-backed expression type such as JavaScript or Liquid if you want to write an expression.

## See also

- [Workflow Instance Variables](../../operate/workflow-instance-variables.md)
- [Using Elsa Studio](../running-workflows/using-elsa-studio.md)
