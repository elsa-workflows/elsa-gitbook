using Elsa.Api.Client.Extensions;
using Elsa.Api.Client.Resources.Resilience.Models;
using Elsa.Api.Client.Resources.WorkflowDefinitions.Models;
using Elsa.Api.Client.Resources.WorkflowInstances.Models;

namespace Elsa.Examples.ApiClient;

public static class ResilienceStrategyExamples
{
    public static Activity CreateResilientHttpActivity()
    {
        var activity = new Activity
        {
            Type = "Elsa.FlowSendHttpRequest",
            Id = "http-call-with-retry"
        };

        activity.SetResilienceStrategy(new ResilienceStrategyConfig
        {
            Mode = ResilienceStrategyConfigMode.Identifier,
            StrategyId = "http-default"
        });

        return activity;
    }

    public static void PrintIncidents(WorkflowInstance instance)
    {
        foreach (var incident in instance.WorkflowState.Incidents)
            Console.WriteLine($"{incident.ActivityId}: {incident.Message}");
    }
}
