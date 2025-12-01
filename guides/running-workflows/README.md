# Running Workflows

There are multiple ways to run a workflow:

* Using Elsa Studio.
* Using a trigger, such as HTTP Endpoint.
* Using Dispatch Workflow Activity
* Using the Elsa REST API.
* Using the Elsa library.

In this guide, we will see an example of each of these methods.

## Before you start﻿ <a href="#before-you-start" id="before-you-start"></a>

For this guide, you will need the following:

* An [Elsa Server](../../application-types/elsa-server.md) project
* An [Elsa Studio](../../getting-started/containers/docker.md#elsa-studio) instance

## Running Workflows via REST API

The Elsa Server exposes REST API endpoints that allow you to execute workflows programmatically. This is useful for integrating workflows into external applications or services.

### Execute a Workflow by Definition ID

To execute a workflow by its definition ID, send a POST request to the following endpoint:

```
POST /elsa/api/workflow-definitions/{definitionId}/execute
```

#### Example using cURL

```bash
curl --location --request POST 'https://localhost:5001/elsa/api/workflow-definitions/my-workflow/execute' \
--header 'Authorization: ApiKey YOUR_API_KEY' \
--header 'Content-Type: application/json' \
--data-raw '{
  "input": {
    "message": "Hello from API",
    "userId": 123
  },
  "correlationId": "optional-correlation-id"
}'
```

#### Example using HTTPie

```bash
http POST https://localhost:5001/elsa/api/workflow-definitions/my-workflow/execute \
  Authorization:"ApiKey YOUR_API_KEY" \
  input:='{"message":"Hello from API","userId":123}' \
  correlationId="optional-correlation-id"
```

#### Request Body Parameters

* `input` (optional): A dictionary of input values to pass to the workflow
* `correlationId` (optional): A correlation ID to associate with the workflow instance
* `name` (optional): A custom name for the workflow instance
* `triggerActivityId` (optional): The ID of a specific trigger activity to start from
* `versionOptions` (optional): Options for selecting the workflow version

#### Sample Response

```json
{
  "workflowState": {
    "id": "workflow-instance-id",
    "definitionId": "my-workflow",
    "definitionVersionId": "version-id",
    "status": "Finished",
    "subStatus": "Finished",
    "output": {
      "result": "Workflow completed successfully"
    }
  }
}
```

### Authentication

The REST API requires authentication. For detailed information about setting up API keys and authentication, see the [Security and Authentication Guide](../security/README.md).

## Running Workflows via the Library

You can also run workflows programmatically from your .NET application using Elsa's API client or by directly using the workflow runtime services.

### Using IWorkflowRunner

The `IWorkflowRunner` service executes workflows directly in-process. This is useful for short-lived workflows that don't require background execution.

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Microsoft.Extensions.DependencyInjection;

// Setup service container
var services = new ServiceCollection();
services.AddElsa();
var serviceProvider = services.BuildServiceProvider();

// Define a workflow
var workflow = new Sequence
{
    Activities =
    {
        new WriteLine("Starting workflow..."),
        new WriteLine("Processing data..."),
        new WriteLine("Workflow completed!")
    }
};

// Get the workflow runner (IWorkflowRunner is in Elsa.Workflows namespace)
var workflowRunner = serviceProvider.GetRequiredService<IWorkflowRunner>();

// Execute the workflow
var result = await workflowRunner.RunAsync(workflow);

Console.WriteLine($"Workflow status: {result.WorkflowState.Status}");
```

### Using IWorkflowRuntime (New Client API)

For running workflows by definition ID with input parameters, use the new `IWorkflowRuntime` client API:

```csharp
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Messages;
using Elsa.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;

// Assume you have a configured service provider with Elsa services
var workflowRuntime = serviceProvider.GetRequiredService<IWorkflowRuntime>();

// Create a workflow client
var client = await workflowRuntime.CreateClientAsync();

// Create and run a workflow instance with input
var result = await client.CreateAndRunInstanceAsync(new CreateAndRunWorkflowInstanceRequest
{
    WorkflowDefinitionHandle = WorkflowDefinitionHandle.ByDefinitionId("my-workflow"),
    Input = new Dictionary<string, object>
    {
        ["message"] = "Hello from the library!",
        ["userId"] = 123
    },
    CorrelationId = "optional-correlation-id",
    IncludeWorkflowOutput = true
});

// Access the workflow state
var workflowState = result.WorkflowState;
Console.WriteLine($"Workflow status: {workflowState.Status}");

// Access output if available
if (workflowState.Output != null)
{
    foreach (var output in workflowState.Output)
    {
        Console.WriteLine($"Output {output.Key}: {output.Value}");
    }
}
```

### Using IWorkflowRuntime (Legacy API - Obsolete)

> **Note:** The following API is marked as obsolete in Elsa 3.2+. Use the new client API shown above instead.

```csharp
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Parameters;
using Microsoft.Extensions.DependencyInjection;

var workflowRuntime = serviceProvider.GetRequiredService<IWorkflowRuntime>();

// Start a workflow with input parameters (obsolete API)
var result = await workflowRuntime.StartWorkflowAsync(
    "my-workflow",
    new StartWorkflowRuntimeParams
    {
        Input = new Dictionary<string, object>
        {
            ["message"] = "Hello!",
            ["userId"] = 123
        },
        CorrelationId = "my-correlation-id"
    });

Console.WriteLine($"Workflow status: {result.WorkflowState.Status}");
```
