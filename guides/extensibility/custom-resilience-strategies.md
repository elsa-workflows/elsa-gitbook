---
description: >-
  Add custom Polly-based resilience strategies and retry diagnostics to Elsa
  3.8.0 workflows.
---

# Custom resilience strategies

Elsa's resilience feature lets an activity execute its operation through a
Polly resilience pipeline. Use a custom strategy when the built-in HTTP retry
strategy does not match the failure policy for your activity—for example, a
circuit breaker for a payment provider or a timeout policy for a domain API.

This feature is different from [incident
strategies](../../operate/incidents/README.md): resilience retries happen
inside the activity execution before a final exception becomes an incident.
It is also different from the operator-driven retry of an activity that has
already faulted.

## How the pieces fit together

The release `3.8.0` flow is:

1. The host enables Elsa's `ResilienceFeature` with `UseResilience` (the HTTP
   module enables it for HTTP activities).
2. The host registers a custom strategy type with
   `AddResilienceStrategyType<T>()`. This adds the type to the serializer's
   polymorphic allow-list; it does not create a strategy instance by itself.
3. The host configures one or more instances under `Resilience:Strategies`.
   Elsa deserializes these instances and exposes them through the resilience
   strategy catalog.
4. A resilient activity stores a strategy identifier or expression in its
   `resilienceStrategy` configuration.
5. The activity calls `IResilientActivityInvoker`. Elsa resolves the selected
   strategy, builds its pipeline, and executes the activity operation through
   it.
6. If the pipeline retries and then completes successfully, Elsa passes the
   retry records to `IRetryAttemptRecorder`. The default recorder stores them
   in the activity execution properties, which the default reader exposes
   through the retry API and Studio.

If no strategy is configured, or the configured identifier cannot be resolved,
the invoker executes the operation directly. Registering a strategy type alone
therefore does not change workflow behavior.

## Implement a strategy

`IResilienceStrategy` has three members: an `Id`, a display name, and a method
that adds behavior to a generic `ResiliencePipelineBuilder<T>`. The following
example adds a circuit breaker that handles exceptions from any result type.
It uses the Polly package version selected by the Elsa `3.8.0` source tree.

```csharp
using Elsa.Resilience;
using Polly;
using Polly.CircuitBreaker;

public sealed class PaymentsCircuitBreaker : IResilienceStrategy
{
    public string Id { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public double FailureRatio { get; set; } = 0.5;
    public int MinimumThroughput { get; set; } = 10;
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(15);

    public Task ConfigurePipeline<T>(
        ResiliencePipelineBuilder<T> pipelineBuilder,
        ResilienceContext context)
    {
        pipelineBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<T>
        {
            FailureRatio = FailureRatio,
            MinimumThroughput = MinimumThroughput,
            SamplingDuration = SamplingDuration,
            BreakDuration = BreakDuration,
            ShouldHandle = new PredicateBuilder<T>().Handle<Exception>()
        });

        return Task.CompletedTask;
    }
}
```

The strategy controls the Polly pipeline only. It does not decide whether the
workflow becomes faulted after the pipeline gives up; that remains part of
Elsa's normal incident handling.

## Register the type and configure an instance

For a classic `IModule` host, register the type through the same Elsa module
used by the workflow host:

```csharp
using Elsa.Extensions;
using Elsa.Resilience.Extensions;

builder.Services.AddElsa(elsa =>
{
    elsa
        .UseResilience(resilience =>
            resilience.AddResilienceStrategyType<PaymentsCircuitBreaker>())
        .UseHttp();
});
```

If the host already calls `UseHttp`, its HTTP feature adds the built-in
`HttpResilienceStrategy` to the same resilience feature. Add your custom type
to that feature as shown above; do not replace the HTTP module unless you also
intend to remove HTTP activities.

Define at least one instance in configuration. `$type` is the serializer
discriminator registered by `AddResilienceStrategyType<T>()`; in `3.8.0` this
is the class's simple `type.Name` value, not a namespace-qualified name. `Id`
is the separate value referenced by workflow definitions.

```json
{
  "Resilience": {
    "Strategies": [
      {
        "$type": "PaymentsCircuitBreaker",
        "id": "payments-circuit",
        "displayName": "Payments circuit breaker",
        "failureRatio": 0.5,
        "minimumThroughput": 10,
        "samplingDuration": "00:00:30",
        "breakDuration": "00:00:15"
      }
    ]
  }
}
```

Elsa reads this array from `Resilience:Strategies` and lists the resulting
instances from `GET /resilience/strategies`. The strategy endpoint requires
one of `read:*`, `read:resilience`, or `read:resilience:strategies`.

### Modular shell hosts

The fluent `UseResilience` and `AddResilienceStrategyType<T>()` calls above are
the classic module-host surface. A modular shell host uses the service-level
registration extension instead:

```csharp
using Elsa.Resilience.Extensions;

builder.Services.AddResilienceStrategy<PaymentsCircuitBreaker>();
```

The shell host must also enable Elsa's Resilience shell feature. Its serializer
uses the same `Resilience:Strategies` configuration and the same simple
`$type` discriminator. The shell feature in `3.8.0` does not expose the fluent
`WithRetryAttemptRecorder<T>()` or `WithRetryAttemptReader<T>()` methods; shell
hosts replace `IRetryAttemptRecorder` and `IRetryAttemptReader` through their
service registrations instead.

## Make a custom activity use the pipeline

A strategy is applied only to activities that participate in the resilience
contract. The activity must implement `IResilientActivity` and supply
`CollectRetryDetails`. Its execution code must pass the operation to
`IResilientActivityInvoker`, as Elsa's HTTP activities do:

```csharp
var invoker = context.GetRequiredService<IResilientActivityInvoker>();

var result = await invoker.InvokeAsync(
    this,
    context,
    () => ExecutePaymentAsync(context),
    context.CancellationToken);
```

`CollectRetryDetails` lets the activity add safe, activity-specific fields to
each `RetryAttemptRecord`. Do not put secrets, access tokens, or full request
payloads in those details; the records are available to operators through the
workflow runtime surfaces.

For a workflow definition created through the API client, select the strategy
by identifier with `ResilienceStrategyConfig`:

```csharp
using Elsa.Api.Client.Extensions;
using Elsa.Api.Client.Resources.Resilience.Models;

jsonActivity.SetResilienceStrategy(new ResilienceStrategyConfig
{
    Mode = ResilienceStrategyConfigMode.Identifier,
    StrategyId = "payments-circuit"
});
```

The expression mode is also supported. Elsa can evaluate an expression to a
strategy identifier, an `IResilienceStrategy`, or a compatible object. Use it
when the selected policy genuinely depends on workflow data; an identifier is
easier to inspect and operate when the policy is deployment-wide.

## Retry-attempt recording

The default `ActivityExecutionContextRetryAttemptRecorder` writes the records
to the activity execution context under `RetryAttempts`. The default reader
loads that property from the activity execution store for:

```http
GET /resilience/retries/{activityInstanceId}
```

Studio shows the **Resilience** activity-properties tab only when:

- the backend reports the `Elsa.Resilience` feature as enabled, and
- the selected activity is marked resilient because it implements
  `IResilientActivity`.

The tab loads the catalog and lets a designer choose an identifier or an
expression. The activity execution view can show recorded retries when the
default recorder has persisted them.

### Add diagnostics without losing Studio history

`WithRetryAttemptRecorder<T>()` replaces the recorder. A recorder that writes
only to an external system will therefore stop populating the default
`RetryAttempts` property, and the default retry API/Studio view will be empty.
If you want both destinations, keep writing the default activity-execution
property while sending your additional diagnostic data:

```csharp
using Elsa.Resilience;
using Elsa.Resilience.Models;
using Microsoft.Extensions.Logging;

public sealed class LoggingRetryAttemptRecorder(
    ILogger<LoggingRetryAttemptRecorder> logger) : IRetryAttemptRecorder
{
    public Task RecordAsync(RecordRetryAttemptsContext context)
    {
        context.ActivityExecutionContext.Properties["RetryAttempts"] = context.Attempts;
        logger.LogInformation(
            "Recorded {Count} retry attempts for activity {ActivityInstanceId}",
            context.Attempts.Count,
            context.ActivityExecutionContext.Id);
        return Task.CompletedTask;
    }
}
```

Register the recorder before the host's existing `AddElsa` call and add the
resilience configuration to that same module callback:

```csharp
builder.Services.AddScoped<LoggingRetryAttemptRecorder>();

builder.Services.AddElsa(elsa =>
{
    elsa
        .UseResilience(resilience =>
            resilience.WithRetryAttemptRecorder<LoggingRetryAttemptRecorder>())
        // Keep the rest of the host's existing module configuration here.
        .UseHttp();
});
```

For a shell host, register the replacement after the Resilience shell feature
has registered its defaults, so this service is the last registration for the
single `IRetryAttemptRecorder` resolution:

```csharp
builder.Services.AddScoped<IRetryAttemptRecorder, LoggingRetryAttemptRecorder>();
```

Replace `IRetryAttemptReader` similarly when the records are stored outside
the activity execution properties.

If a custom recorder stores records somewhere else, pair it with a custom
`IRetryAttemptReader` using `WithRetryAttemptReader<T>()`. Otherwise the
default `GET /resilience/retries/{activityInstanceId}` endpoint has no way to
read the external records. The default recorder stores records as activity
execution properties, so their availability also follows the host's activity
execution/log persistence configuration. Elsa calls the recorder after the
pipeline completes successfully; if the pipeline ultimately throws, the
default recorder is not called for those attempts.

## Troubleshooting checklist

| Symptom | Check |
| --- | --- |
| The strategy is not listed in Studio | Confirm `UseResilience`, the `AddResilienceStrategyType<T>()` call, and a matching `$type` entry under `Resilience:Strategies`. |
| The Resilience tab is missing | Confirm the backend feature is enabled and the activity implements `IResilientActivity`. |
| The activity runs but does not retry | Confirm the definition stores `resilienceStrategy`, the identifier matches exactly, and the activity calls `IResilientActivityInvoker`. |
| The workflow still faults after retries | This is expected when the pipeline exhausts its policy; inspect the resulting incident and choose the incident or operational retry path. |
| Studio has no retry records after adding a recorder | Keep the default recorder in the chain or register a matching custom `IRetryAttemptReader`. |

## Source references

This page was checked against the `release/3.8.0` source trees:

- [`IResilienceStrategy`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Resilience.Core/Contracts/IResilienceStrategy.cs)
- [`ResilienceFeature`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Resilience/Features/ResilienceFeature.cs)
- [`ResilienceStrategySerializer`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Resilience.Core/Serialization/ResilienceStrategySerializer.cs)
- [`ResilientActivityInvoker`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Resilience.Core/Services/ResilientActivityInvoker.cs)
- [`ActivityExecutionContextRetryAttemptRecorder`](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Resilience/Recorders/ActivityExecutionContextRetryAttemptRecorder.cs)
- [`ResilienceTab`](https://github.com/elsa-workflows/elsa-studio/blob/release/3.8.0/src/modules/Elsa.Studio.Workflows/Components/WorkflowDefinitionEditor/Components/ActivityProperties/Tabs/ResilienceTab.razor)
- [`ActivityPropertiesPanel`](https://github.com/elsa-workflows/elsa-studio/blob/release/3.8.0/src/modules/Elsa.Studio.Workflows/Components/WorkflowDefinitionEditor/Components/ActivityProperties/ActivityPropertiesPanel.razor.cs)
