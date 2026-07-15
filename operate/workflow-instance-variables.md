---
description: >-
  Inspect and update workflow instance variables through Elsa Studio, the
  runtime API, or `IWorkflowInstanceVariableManager`.
---

# Workflow Instance Variables

Use workflow instance variable management when you need to inspect or correct the current variable values of a running or suspended workflow without restarting it.

{% hint style="info" %}
If you need the broader model first, read [Workflow Context](../getting-started/concepts/workflow-context.md). That page explains how variables relate to workflow inputs, outputs, activity state, bookmarks, incidents, and the journal.
{% endhint %}

## What this page is for

This page is specifically about live instance variable inspection and mutation.

For the wider operator workflow—finding an instance, reading its journal, and
inspecting the activity that produced a value—see [Investigate a Workflow
Instance](workflow-state-and-journal.md).

Use it when you need to:

- inspect the current values of persisted workflow variables
- correct a bad value on a suspended instance
- build an operations tool around Elsa's variable-management API

For authoring variables in workflow definitions, use [Workflow Context](../getting-started/concepts/workflow-context.md) and [Expressions in Elsa Studio](../guides/studio/expressions.md).

## Elsa Studio

Elsa Studio exposes variables in at least two places in 3.8:

- the workflow instance viewer has a **Variables** tab for instance inspection
- the alterations UI can load workflow instance variables before staging a change

## Programmatic Access﻿ <a href="#programmatic-access" id="programmatic-access"></a>

### Listing﻿ <a href="#list-variables" id="list-variables"></a>

You can use the `IWorkflowInstanceVariableManager` service to retrieve all variables of a workflow instance. The following example demonstrates how to read the variables:

```csharp
var workflowInstanceId = "some-workflow-instance-id";
var variables = await _workflowInstanceVariableManager.GetVariablesAsync(workflowInstanceId, null, cancellationToken);

// Print each variable.
foreach (var variable in variables)
{
    Console.WriteLine($"Id: {variable.Variable.Id}, Name: {variable.Variable.Name}, Value: {variable.Value}");
}
```

{% hint style="info" %}
The `GetVariablesAsync` method retrieves all the variables associated with a specified workflow instance. Ensure that the `workflowInstanceId` is valid and that the workflow instance exists.
{% endhint %}

Each variable retrieved is represented by a unique ID, a name, and a value.

### Updating﻿ <a href="#update-variables" id="update-variables"></a>

You can also use the `IWorkflowInstanceVariableManager` service to update one or more variables of a workflow instance. Provide the variable IDs you want to change and the new values to assign to them:

```csharp
var workflowInstanceId = "some-workflow-instance-id";
var variablesToUpdate = new[]
{
    new VariableUpdateValue("some-variable-id", "Some variable value"),
    new VariableUpdateValue("another-variable-id", 42)
};

// Update the variables. This returns a complete list of variables, including both unchanged and changed variables.
var variables = await _workflowInstanceVariableManager.SetVariablesAsync(workflowInstanceId, variablesToUpdate, cancellationToken);

// Print each variable.
foreach (var variable in variables)
{
    Console.WriteLine($"Id: {variable.Variable.Id}, Name: {variable.Variable.Name}, Value: {variable.Value}");
}
```

{% hint style="info" %}
`SetVariablesAsync` updates only the variables you specify by ID and then saves the workflow instance. Variables not included in the request keep their current values.
{% endhint %}

Updating variables in a workflow instance can be particularly useful for dynamically adjusting the workflow's behaviour based on changing data inputs or conditions during execution.

## API Access﻿ <a href="#api-access" id="api-access"></a>

### Listing﻿ <a href="#api-list-variables" id="api-list-variables"></a>

The workflow instance API exposes a relative endpoint at `/workflow-instances/{id}/variables`.

If you run the default Elsa Server host, that endpoint is typically available under the global API prefix `/elsa/api`, so the full URL becomes `/elsa/api/workflow-instances/{id}/variables`.

You can retrieve the variables for a workflow instance with:

```bash
curl --location \
'https://localhost:5001/elsa/api/workflow-instances/{your-workflow-instance-id}/variables' \
--header 'Authorization: ApiKey {your-api-key}'
```

The API returns a JSON object containing the variables associated with the workflow instance. Below is an example response:

```json
{
    "items": [
        {
            "id": "ff1c0b14864811ea",
            "name": "Message",
            "value": "Hello, World!"
        },
        {
            "id": "ea1bbdf90ea22ca7",
            "name": "Sender",
            "value": "Elsa"
        }
    ],
    "count": 2
}
```

Each item in the response includes the variable's unique `id`, `name`, and `value`, allowing you to inspect the current state of the workflow instance's variables.

### Updating﻿ <a href="#api-update-variables" id="api-update-variables"></a>

To update one or more variables in a workflow instance, send a `POST` request to the same route:

```bash
curl --location \
'https://localhost:5001/elsa/api/workflow-instances/{your-workflow-instance-id}/variables' \
--header 'Content-Type: application/json' \
--header 'Authorization: ApiKey {your-api-key}' \
--data '{
"variables": [
    {
        "id": "ff1c0b14864811ea",
        "value": "Hello, Elsa!"
    },
    {
        "id": "ea1bbdf90ea22ca7",
        "value": "World"
    }
]
}'
```

The request payload uses `VariableUpdateValue` records, so each entry contains:

- `id`: the variable ID
- `value`: the new value

After the update, Elsa saves the workflow instance and returns the resolved variable list.
