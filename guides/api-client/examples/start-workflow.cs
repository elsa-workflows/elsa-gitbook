// start-workflow.cs
// Example: Executing a workflow definition using elsa-api-client.
//
// Code reference:
// src/clients/Elsa.Api.Client/Resources/WorkflowDefinitions/Contracts/IExecuteWorkflowApi.cs

using Elsa.Api.Client.Resources.WorkflowDefinitions.Contracts;
using Elsa.Api.Client.Resources.WorkflowDefinitions.Requests;
using Elsa.Api.Client.Shared.Models;

namespace Elsa.Examples.ApiClient;

/// <summary>
/// Service for executing workflow definitions via the Elsa API client.
/// </summary>
public class WorkflowStarterService
{
    private readonly IExecuteWorkflowApi _executeWorkflowApi;

    public WorkflowStarterService(IExecuteWorkflowApi executeWorkflowApi)
    {
        _executeWorkflowApi = executeWorkflowApi;
    }

    /// <summary>
    /// Executes a workflow definition with correlation ID and input data.
    /// </summary>
    public async Task ExecuteWorkflowAsync(
        string definitionId,
        string? correlationId = null,
        Dictionary<string, object>? input = null)
    {
        var request = new ExecuteWorkflowDefinitionRequest
        {
            CorrelationId = correlationId,
            Input = input
        };

        var response = await _executeWorkflowApi.ExecuteAsync(definitionId, request);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Executes an order processing workflow with order-specific data.
    /// </summary>
    public async Task ExecuteOrderWorkflowAsync(
        string orderId,
        string customerEmail,
        decimal orderTotal)
    {
        var input = new Dictionary<string, object>
        {
            ["orderId"] = orderId,
            ["customerEmail"] = customerEmail,
            ["orderTotal"] = orderTotal,
            ["createdAt"] = DateTime.UtcNow
        };

        await ExecuteWorkflowAsync(
            definitionId: "order-processing",
            correlationId: $"order-{orderId}",
            input: input);
    }

    /// <summary>
    /// Executes a specific workflow definition version.
    /// </summary>
    public async Task ExecuteSpecificVersionAsync(string definitionId, int version)
    {
        var request = new ExecuteWorkflowDefinitionRequest
        {
            VersionOptions = new VersionOptions
            {
                Version = version
            }
        };

        var response = await _executeWorkflowApi.ExecuteAsync(definitionId, request);
        response.EnsureSuccessStatusCode();
    }
}

// Usage example:
//
// var services = new ServiceCollection();
// services.AddDefaultApiClientsUsingApiKey(options =>
// {
//     options.BaseAddress = new Uri("https://your-elsa-server.com/elsa/api");
//     options.ApiKey = "YOUR_API_KEY";
// });
// services.AddTransient<WorkflowStarterService>();
//
// var serviceProvider = services.BuildServiceProvider();
// var starter = serviceProvider.GetRequiredService<WorkflowStarterService>();
//
// await starter.ExecuteWorkflowAsync(
//     definitionId: "hello-world",
//     correlationId: "session-12345");
//
// await starter.ExecuteOrderWorkflowAsync(
//     orderId: "ORD-2025-001",
//     customerEmail: "customer@example.com",
//     orderTotal: 99.95m);
