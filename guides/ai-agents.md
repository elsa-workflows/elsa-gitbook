---
description: >-
  Configure Elsa Agents, expose agent activities and APIs, and manage agents
  from Elsa Studio.
---

# Agents activities and Studio administration

Elsa Agents is an optional extension that turns configured AI agents into
workflow activities and exposes an API and Studio module for managing them.
It is separate from [Weaver and AI workflow assistance](ai-workflow-assistance.md):
Weaver helps people inspect and shape workflows, while Agents execute an
agent as part of an Elsa workflow or an HTTP request.

This guide describes the contracts shipped in `release/3.8.0`. It focuses on
the compiled extension behavior; some examples in the extension README and
migration guide refer to types that are not present in this release.

## When to use Agents

Use Agents when a workflow needs a model-backed step with a stable input and
output contract, for example classifying a request, extracting structured
data, or drafting text for a later approval step. Use a normal activity or
Weaver when the work must be deterministic, directly inspectable, or limited
to workflow-authoring assistance.

The released module provides:

- one dynamically generated workflow activity for each configured agent;
- Handlebars prompt rendering with named input variables;
- optional JSON output conversion into the declared output type;
- provider registration for OpenAI or Azure OpenAI chat completion; and
- agent definition, skill discovery, and invocation APIs.

The release does not provide a persisted chat conversation through the invoke
endpoint. Each API invocation starts a new chat history. The workflow activity
stores the model response in its `Output` value, while the API endpoint
requires that the response content be valid JSON.

## How the pieces fit

The runtime has four distinct concerns:

1. **Agent configuration** — `AgentsOptions` supplies agents in a host-managed
   configuration, or `StoreKernelConfigProvider` reads definitions from the
   agent store when persistence is enabled.
2. **Model provider** — an `AgentsFeature` service descriptor configures a
   Semantic Kernel chat-completion service for each agent invocation.
3. **Workflow activity** — `AgentActivityProvider` creates a browsable,
   synthetic-input activity descriptor for every agent. The activity invokes
   `IAgentInvoker` and writes one `Output` value.
4. **Management surface** — `UseAgentsApi` adds CRUD, skills, and invoke
   endpoints. `AddAgentsModule` adds the corresponding remote clients and an
   **Agents** menu entry to Studio.

The main runtime entry point is [`UseAgents`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/agents/Elsa.Agents/ModuleExtensions.cs),
which installs the Core and activity features through
[`AgentsFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/agents/Elsa.Agents/AgentsFeature.cs).

## Install and configure a server

Install the packages for the capabilities you use:

- `Elsa.Agents` for the runtime and workflow activities;
- `Elsa.Agents.OpenAI` or `Elsa.Agents.AzureOpenAI` for a chat-completion
  provider;
- `Elsa.Agents.Api` for agent management and invocation endpoints; and
- `Elsa.Agents.Persistence.EFCore` plus one provider-specific package when
  agent definitions must survive a restart.

The provider packages expose `AddOpenAIChatCompletion` and
`AddAzureOpenAIChatCompletion`. The release source does not expose the
`UseOpenAI` method shown in the extension README.

A minimal server composition using the persisted management path looks like
this:

```csharp
services.AddElsa(elsa =>
{
    elsa.UseAgents(agents =>
    {
        agents.AddOpenAIChatCompletion(
            modelId: "<model>",
            apiKey: "<secret>");
    });

    elsa.UseAgentPersistence(persistence =>
        persistence.UseEntityFrameworkCore(ef =>
            ef.UseSqlServer(connectionString)));

    elsa.UseAgentsApi();
});
```

Use the corresponding `UsePostgreSql`, `UseMySql`, or `UseSqlite` extension
from the provider package when needed. Apply the agent persistence migrations
using the same EF Core deployment process as the rest of the host.

`UseAgentsApi` depends on agent persistence. Persistence uses an in-memory
store unless an EF Core (or another `IAgentStore`) implementation replaces
it. The in-memory store is process-local and is suitable for development or
tests, not a production deployment.

### Configuration-only activities

If the host only needs workflow activities and does not use the API or
persistence, register `AgentsOptions` yourself. The release does not bind an
`appsettings.json` `Agents` section automatically:

```csharp
services.Configure<AgentsOptions>(options =>
{
    options.Agents.Add(new AgentConfig
    {
        Name = "ClassifyRequest",
        Description = "Classifies an incoming request",
        PromptTemplate = "Classify this request: {{request}}",
        InputVariables =
        [
            new InputVariableConfig
            {
                Name = "request",
                Type = "string",
                Description = "The request text"
            }
        ],
        OutputVariable = new OutputVariableConfig
        {
            Type = "string",
            Description = "The classification"
        }
    });
});
```

When `UseAgentPersistence` is enabled, the persistence feature replaces the
options-backed provider with `StoreKernelConfigProvider`. Create and update
definitions through the API, Studio, or your own `IAgentStore`; adding an
agent to `AgentsOptions` is not enough for persisted invocation.

## Define an agent for a workflow

An `AgentConfig` contains the agent name, prompt template, input variables,
output variable, execution settings, and selected skills. The activity
provider turns each input variable into a synthetic activity input and exposes
the output as `Output`.

In Studio, select the generated activity from the **Agents** category. Its
activity type is generated from the agent name and optional function name, so
renaming an agent can produce a different activity type. Prefer stable agent
names once workflow definitions are published.

The prompt is rendered as Handlebars. Inputs are inserted into the prompt,
then the agent is invoked with automatic function-choice behavior. Treat
workflow inputs as untrusted model input: validate data before invocation and
do not put secrets into prompts.

Set `ExecutionSettings.ResponseFormat` to `json_object` when the next
activity needs structured output or when the agent will be invoked through the
API. The runtime adds a JSON-only system message, removes code fences for the
workflow activity, and converts its response to the declared output type. If
the declared output type is `object`, a JSON response is deserialized as an
`ExpandoObject`; invalid or unexpected model output still fails at runtime, so
validate it before making business decisions. The API endpoint deserializes
the response directly as JSON and does not remove code fences.

The activity is marked asynchronous and returns one output. It does not
persist the `ChatHistory` used internally by the invoker. If a process needs
human review, durable state, or repeatable compensation, put those concerns in
ordinary workflow activities around the agent step.

## Manage agents through the API

Enable `UseAgentsApi()` on the server and use the authenticated backend from
Studio or another client. The release routes and permission claims are:

- `GET /ai/agents` and `GET /ai/agents/{id}` — `ai/agents:read`;
- `POST /ai/agents` and `POST /ai/agents/{id}` — `ai/agents:write`;
- `DELETE /ai/agents/{id}` and `POST /ai/bulk-actions/agents/delete` —
  `ai/agents:delete`;
- `POST /ai/actions/agents/generate-unique-name` — `ai/agents:write`;
- `POST /ai/queries/agents/is-unique-name` — `ai/agents:read`;
- `POST /ai/agents/{agent}/invoke` — `ai/agents:invoke`; and
- `GET /ai/skills` — `ai/skills:read`.

The invoke request contains an agent name and an `inputs` object. Configure the
target agent with `ResponseFormat` set to `json_object`; otherwise the endpoint
may reject a plain-text model response when it deserializes the result:

```http
POST /ai/agents/ClassifyRequest/invoke
Authorization: Bearer <token>
Content-Type: application/json

{"inputs":{"request":"The customer cannot sign in."}}
```

The API returns the serialized JSON agent response and fails when the model
returns content that is not valid JSON. It does not accept a chat history in
this release. Protect `ai/agents:invoke` separately from configuration write
permissions when callers should run agents but not edit their prompts or
skills.

Agent definitions inherit Elsa's entity and tenant model, but the agent
filter itself only selects by ID or name. Do not use a globally unique name as
your tenant boundary; configure authentication and tenancy for the host and
test that every management and invocation route is isolated as intended.

## Add Agents to Elsa Studio

Register the optional Studio module in the same client that registers the
authenticated remote backend. In the release WASM host, the Agents line comes
after the standard shell, login, dashboard, and Workflows modules:

```csharp
builder.Services.AddCore();
builder.Services.AddShell();
builder.Services.AddRemoteBackend(backendApiConfig);
builder.Services.AddLoginModule();
builder.Services.UseElsaIdentity();
builder.Services.AddDashboardModule();
builder.Services.AddWorkflowsModule();
builder.Services.AddAgentsModule(backendApiConfig);
```

`AddAgentsModule` registers the agent and skills API clients, the Agents
activity display-settings provider, and a remote **Agents** menu item. The
Studio feature is marked as remote `Elsa.AgentsApi`, so the server must expose
the corresponding feature for the module to be useful. Studio does not hold
provider credentials or invoke the model directly.

The Agents page can list, create, edit, and delete definitions. The editor
loads available skills from `GET /ai/skills`, lets a user define input and
output variables, selects JSON response mode, and saves the definition through
the API. After a persisted definition changes, the server refreshes its
activity registry. Studio explicitly marks its local activity and
display-settings registries stale after delete and bulk-delete; after create
or update, reopen or refresh an already-open designer if it does not show the
new activity.

## Security and production checklist

- Keep provider API keys in a secret provider. The release provider helpers
  accept raw key strings; they do not create a secret-management boundary.
- Grant `ai/agents:invoke` only to callers that may send data to the model.
  Grant write/delete claims only to administrators or a controlled authoring
  role.
- Review every selected skill. The built-in skill registry includes image
  generation and document-query skills, and custom skill providers can add
  more model-callable functions.
- Treat prompts and workflow inputs as data that can contain instructions.
  The runtime enables Handlebars dangerous-content rendering and automatic
  function choice, so add application-level validation and tool governance.
- Use EF Core or another durable `IAgentStore` for production definitions and
  apply its migrations. The default store loses definitions on process
  restart.
- Test tenant isolation, model failure, malformed JSON, timeouts, rate limits,
  and provider outages before allowing an agent to make irreversible changes.

## Troubleshooting

**The Agents activity category is empty.** Confirm that `UseAgents()` is
enabled, that at least one agent is available from the active configuration or
store, and that the configured model provider is registered. With persistence,
check the agent store rather than only `AgentsOptions`.

**Studio has no Agents menu.** Confirm that the WASM host calls
`AddAgentsModule(backendApiConfig)`, the backend URL and authentication are
correct, and the server exposes `UseAgentsApi()`.

**An invoke call is unauthorized.** Check the caller's `ai/agents:invoke`
permission. Listing and editing agents use different claims.

**The API invoke response cannot be deserialized.** Set the stored agent's
`ResponseFormat` to `json_object`, and instruct the model to return one JSON
object without code fences. The API path does not accept plain text; diagnose
workflow activity failures through the workflow journal or host logs.

**A changed definition is not visible in an open designer.** Reload the
activity registry or reopen the designer after the definition notification has
been processed.

## Release-source references

This page was checked against the requested `release/3.8.0` refs:

- Core: [`c58fe877`](https://github.com/elsa-workflows/elsa-core/tree/c58fe8770ff7ba39be74b58cd4b1e6017ee5e140)
- Studio: [`d25f0aae`](https://github.com/elsa-workflows/elsa-studio/tree/d25f0aaeb5f14af6c5938d173aae828d87ebad5c)
- Extensions: [`335a2649`](https://github.com/elsa-workflows/elsa-extensions/tree/335a26495318f6ee1528bf2723b7333c753ce9a2)

The most relevant implementations are [`AgentActivityProvider`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/agents/Elsa.Agents.Activities/ActivityProviders/AgentActivityProvider.cs),
[`AgentInvoker`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/agents/Elsa.Agents.Core/Services/AgentInvoker.cs),
[`AgentPersistenceFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/agents/Elsa.Agents.Persistence/Features/AgentPersistenceFeature.cs),
[`Agents API endpoints`](https://github.com/elsa-workflows/elsa-extensions/tree/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/agents/Elsa.Agents.Api/Endpoints),
and [`AddAgentsModule`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/agents/Elsa.Studio.Agents/Extensions/ServiceCollectionExtensions.cs).
