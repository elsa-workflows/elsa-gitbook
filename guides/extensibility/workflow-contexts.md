---
description: >-
  Register application-specific workflow context providers and configure their
  use in Elsa workflows and Elsa Studio.
---

# Workflow Context Providers

Elsa's conceptual [Workflow Context](../../getting-started/concepts/workflow-context.md)
page describes the state that Elsa keeps while a workflow executes. This guide
covers a different feature: a workflow context provider loads application-owned
data for a workflow, exposes it to activities and expressions, and can save it
back through your own provider implementation.

Use this feature when several activities need the same external or
application-specific object, such as a customer session, a tenant-scoped
record, or a business transaction context. It is not a replacement for
workflow variables, inputs, outputs, or persisted workflow state.

## How the feature fits together

The release has five separate integration points:

- `Elsa.WorkflowContexts` provides the provider contract, the
  `SetWorkflowContextParameter` activity, the provider-descriptor endpoint, and
  the runtime middleware.
- `UseWorkflowContexts()` on the Elsa module enables the feature and its
  JavaScript integration when JavaScript is also enabled.
- `AddWorkflowContextProvider<T>()` registers a provider with dependency
  injection. A workflow must separately opt into the provider with
  `AddWorkflowContextProvider<T>()` on `IWorkflowBuilder`.
- The workflow and activity execution pipeline overloads of
  `UseWorkflowContexts()` load and save selected providers at runtime.
- `Elsa.Studio.WorkflowContexts` is an optional Studio module. It consumes the
  provider-descriptor API and adds workflow-definition and activity-editor UI.

The module, DI registration, workflow opt-in, pipeline middleware, and Studio
module are independent concerns. Enabling one does not imply that the others
are configured.

## Add the server package

Reference the `Elsa.WorkflowContexts` package in the server that owns workflow
execution. The package includes the workflow-context feature and depends on
the runtime and JavaScript expression infrastructure.

Enable the module when registering Elsa:

```csharp
services.AddElsa(elsa =>
{
    elsa.UseWorkflowContexts();
});
```

This call is the module extension on `IModule`. It registers the feature and
the `SetWorkflowContextParameter` activity. It is different from the two
pipeline extension methods described below.

## Implement and register a provider

Implement `IWorkflowContextProvider`, or inherit from the typed
`WorkflowContextProvider<T>` base class. The base class adapts typed `Load` and
`Save` methods to Elsa's `object`-based contract:

```csharp
using Elsa.Extensions;
using Elsa.WorkflowContexts.Abstractions;
using Elsa.Workflows;

public sealed record CustomerContext(string CustomerId, string Segment);

public sealed class CustomerWorkflowContextProvider
    : WorkflowContextProvider<CustomerContext>
{
    protected override ValueTask<CustomerContext?> LoadAsync(
        WorkflowExecutionContext workflowExecutionContext)
    {
        var parameterKey = typeof(CustomerWorkflowContextProvider)
            .GetScopedParameterName("CustomerId");
        var customerId = workflowExecutionContext.GetProperty<string>(parameterKey);

        // Use customerId to load the value from your application store.
        CustomerContext? context = customerId == null
            ? null
            : new CustomerContext(customerId, "Standard");
        return ValueTask.FromResult(context);
    }

    protected override ValueTask SaveAsync(
        WorkflowExecutionContext workflowExecutionContext,
        CustomerContext? context)
    {
        // Persist context through your application store when appropriate.
        return ValueTask.CompletedTask;
    }
}
```

Register the provider with the server's service collection:

```csharp
services.AddWorkflowContextProvider<CustomerWorkflowContextProvider>();
```

The release registration is scoped. The provider can therefore use scoped
application services, but it remains responsible for authorization, tenant
partitioning, and data-store behavior.

Provider names are derived from the provider type name by removing
`WorkflowContextProvider`. For example,
`CustomerWorkflowContextProvider` is presented as `Customer` by the provider
descriptor endpoint and is exposed to JavaScript as `getCustomer()`.

## Opt a workflow into a provider

Registering a provider makes it available to the host and Studio. It does not
install it into every workflow. Add the provider to the workflow definition:

```csharp
public class CustomerReviewWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Name = "Customer review";
        builder.AddWorkflowContextProvider<CustomerWorkflowContextProvider>();
        builder.Root = new Sequence
        {
            Activities =
            {
                // Activities that use CustomerContext go here.
            }
        };
    }
}
```

The builder stores the provider's simple assembly-qualified type name in the
workflow's `Elsa:WorkflowContextProviderTypes` custom property. At runtime,
Elsa resolves those type names and loads or saves only the providers installed
on that workflow. Renaming or moving a provider type can therefore prevent an
existing definition from resolving its provider; treat provider type names as
part of the definition's compatibility surface.

## Add runtime pipeline middleware

For a custom host, add the workflow and activity middleware to the execution
pipelines that run your workflows:

```csharp
services.AddElsa(elsa =>
{
    elsa.UseWorkflowContexts();

    elsa.UseWorkflows(workflows =>
    {
        workflows
            .WithDefaultWorkflowExecutionPipeline(pipeline => pipeline.UseWorkflowContexts())
            .WithDefaultActivityExecutionPipeline(pipeline => pipeline.UseWorkflowContexts());
    });
});
```

The exact pipeline-builder composition varies with the host, so keep the
important distinction in mind: the module method enables the feature, while
the `IWorkflowExecutionPipelineBuilder` and
`IActivityExecutionPipelineBuilder` overloads insert execution middleware.
The release workbench calls the module method in its `AddElsa` configuration;
the pipeline overloads are separate extension methods. Verify that your host
has both execution pipelines wired when you rely on provider load/save
behavior.

At workflow scope, the middleware loads every provider installed on the
workflow before the workflow runs and saves every provider after the workflow
pipeline completes. The provider value is held in the workflow execution
context while the workflow runs; persistence outside that execution is the
provider's responsibility.

## Set provider parameters from a workflow

`SetWorkflowContextParameter` passes a named or unnamed parameter to a
provider. Its inputs are:

- **Provider type** — the selected provider.
- **Parameter name** — optional; if omitted, the provider name is used.
- **Parameter value** — the value supplied to the provider.

The activity stores the parameter under a provider-scoped property name and
then loads the provider context. A provider can read those values from the
`WorkflowExecutionContext` while implementing `LoadAsync` or `SaveAsync`.
The scoped names follow this form:

```text
ProviderName
ProviderName:ParameterName
```

In code-first workflows, the same activity can be created with its generic
factory:

```csharp
var setCustomerId = SetWorkflowContextParameter.For<CustomerWorkflowContextProvider>(
    parameterName: "CustomerId",
    parameterValue: "customer-123");
```

The activity's provider picker is populated from the providers installed on
the current workflow, not from every provider registered in the server.

## Control context around an activity

Activities can opt into provider loading and saving with the release extension
methods:

```csharp
someActivity
    .LoadContext(typeof(CustomerWorkflowContextProvider))
    .SaveContext(typeof(CustomerWorkflowContextProvider));
```

Elsa Studio exposes the same settings in the activity's **Workflow Context**
tab. The tab lists only providers selected for the workflow and stores the
settings in the activity's custom properties. **Load** requests a provider
load before the activity; **Save** requests a provider save after it.

There is an important Elsa `3.8.0` behavior to account for: in the activity
middleware, the save decision checks the activity's `Load` setting instead of
its `Save` setting. As a result, a checked **Save** box alone does not cause an
activity-level save in this release. Background activity execution bypasses
the per-activity flags and loads and saves all providers installed on the
workflow. If activity-level persistence is important, verify the behavior in
your host and consider using workflow-level provider lifecycle behavior or a
custom activity/host workaround rather than assuming the **Save** flag is
independent.

## Use workflow contexts in JavaScript

When both the Workflow Contexts and JavaScript features are enabled, Elsa
adds a function for each provider installed on the workflow. A provider named
`CustomerWorkflowContextProvider` is available as:

```javascript
const customer = getCustomer();
customer.Segment;
```

The release also supplies JavaScript type definitions based on the provider's
generic context type. These functions are scoped to the current workflow's
installed providers; registering a provider on the server is not enough to
make its function available in every workflow.

## Enable the Studio module

The optional `Elsa.Studio.WorkflowContexts` package adds the workflow-definition
editor, provider picker, and activity **Workflow Context** tab. Register it in
the Studio host:

```csharp
builder.Services.AddWorkflowContextsModule();
```

The module calls the server endpoint
`GET /workflow-contexts/provider-descriptors` through the backend API client.
The endpoint requires the `read:workflow-context-provider-descriptors`
permission and returns the registered provider names and type names. If the
endpoint is unavailable, unauthorized, or the provider is not registered, the
Studio picker cannot offer it.

In the workflow-definition properties editor, select the providers that the
workflow may use. The activity picker and the activity **Workflow Context**
tab then limit their choices to that selection. Studio writes the selected
provider types to the workflow definition; it does not load or save provider
data itself.

The `Elsa.Studio.WorkflowContexts` module ships in the Extensions release
source. It is optional and is not a feature of the base Studio application in
the `elsa-studio` repository. Add it explicitly when assembling a modular
Studio host.

## Security and operations

- Treat provider data as application data. Do not place secrets or sensitive
  records in workflow definitions or untrusted activity inputs merely because
  a provider can expose them to expressions.
- Enforce tenant and user boundaries in the provider's load/save implementation.
  The descriptor endpoint lists types; it does not authorize access to the
  provider's underlying data.
- Protect the descriptor endpoint with the normal Elsa API authentication and
  permission model. Studio needs the descriptor permission to render the
  provider picker.
- Keep provider type names stable across deployments. The workflow definition
  stores the type name, and the runtime resolves it with `Type.GetType`.
- Design `SaveAsync` to be idempotent where workflows can retry or resume.
  Elsa may execute a workflow more than once, and the provider owns the
  external write semantics.

## Troubleshooting

### A provider does not appear in Studio

Confirm that the server package is referenced, the provider is registered with
DI, the server exposes the descriptor endpoint to the Studio identity, and
the Studio host calls `AddWorkflowContextsModule()`. Then select the provider
on the workflow definition before opening the activity picker.

### The provider never loads or saves

Confirm all of the following:

1. The workflow contains the provider type in
   `Elsa:WorkflowContextProviderTypes`.
2. The module registration and the matching workflow/activity pipeline
   middleware are present.
3. The provider can be constructed from the current DI scope.
4. The activity is configured with **Load** when using activity middleware.
5. For activity-level saves on `3.8.0`, account for the release bug described
   above; a **Save** checkbox alone is not sufficient.

### A JavaScript function is missing

Check that JavaScript is enabled, Workflow Contexts is enabled, and the
provider is installed on the workflow. The generated function name is based on
the provider type name after removing `WorkflowContextProvider`.

## Release source

The behavior above is grounded in the `release/3.8.0` source:

- [`IWorkflowContextProvider`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/workflows/Elsa.WorkflowContexts/Contracts/IWorkflowContextProvider.cs)
  and [`WorkflowContextProvider<T>`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/workflows/Elsa.WorkflowContexts/Abstractions/WorkflowContextProvider.cs)
- [`UseWorkflowContexts`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/workflows/Elsa.WorkflowContexts/Extensions/WorkflowContextModuleExtensions.cs)
  and the [pipeline extensions](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/workflows/Elsa.WorkflowContexts/Extensions/WorkflowExecutionBuilderExtensions.cs)
- [Default workflow and activity pipeline helpers](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/modules/Elsa.Workflows.Runtime/Extensions/PipelineWorkflowsFeatureExtensions.cs)
- [`SetWorkflowContextParameter`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/workflows/Elsa.WorkflowContexts/Activities/SetWorkflowContextParameter.cs)
  with [scoped-name helpers](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/workflows/Elsa.WorkflowContexts/Extensions/WorkflowContextProviderTypeExtensions.cs)
  and [context storage extensions](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/workflows/Elsa.WorkflowContexts/Extensions/TransientPropertiesExecutionContextExtensions.cs)
- [Workflow and activity middleware](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/workflows/Elsa.WorkflowContexts/Middleware/WorkflowContextActivityExecutionMiddleware.cs)
- [Provider descriptor endpoint](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/workflows/Elsa.WorkflowContexts/Endpoints/ProviderTypes/List/Endpoint.cs)
- [`Elsa.Studio.WorkflowContexts`](https://github.com/elsa-workflows/elsa-extensions/tree/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/workflows/Elsa.Studio.WorkflowContexts)
- [Studio API contract](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/clients/Elsa.Api.Client/Resources/WorkflowExecutionContexts/Contracts/IWorkflowContextProviderDescriptorsApi.cs)
