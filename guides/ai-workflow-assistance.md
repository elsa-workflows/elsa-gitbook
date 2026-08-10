---
description: >-
  Configure and use Elsa's release-backed AI Host and Weaver Studio workspace
  for grounded, reviewable workflow assistance.
---

# Weaver and AI workflow assistance

Weaver is Elsa's AI-assisted workspace for asking questions about workflow
definitions and instances, inspecting grounded runtime context, and producing
reviewable workflow proposals. It is a server-mediated feature: Studio sends
references and messages to Elsa Server, the AI Host resolves authorized
context and tools, and a configured provider performs the model interaction.

This guide describes the implementation shipped in `release/3.8.0`. Treat it
as a capability and deployment guide, not as a promise that every planned AI
API is already available.

## Where the pieces fit

- **Weaver (Studio)** — chat UI, context attachments, streamed assistant and
  tool activity, and proposal notifications.
- **AI Host (Server)** — identity, tenant resolution, grounding, tool
  filtering, redaction, audit, and the stream contract.
- **AI provider (Server)** — provider sessions and provider-to-Elsa event
  mapping.
- **AI persistence (Server)** — durable conversations, proposals, and audit
  records when a persistence implementation is installed.

Studio does not host an AI runtime, invoke a model provider, or receive
provider credentials. If the server does not advertise the AI shell feature,
the Weaver menu is hidden and the page reports that Weaver is unavailable.

## Before enabling it

Use Weaver when a person needs help understanding or shaping an Elsa workflow
with server-side context. It is a good fit for:

- a process designer asking about an attached workflow definition;
- a technical user investigating an attached workflow instance or incident;
- a domain specialist turning a described business process into a draft for
  technical review; and
- an engineering team that wants an auditable proposal workflow rather than
  direct, model-controlled changes.

Keep ordinary workflow APIs and Studio actions as the system of record. The
AI Host's proposal tools do not write workflow definitions, and the current
Studio module does not apply proposals.

## Install and compose the server

Add the Elsa AI Host module and one provider implementation to the server.
The release includes the Copilot provider in Core. Add a provider-specific AI
persistence package as well when conversations, proposals, and audit records
must survive a restart. The persistence packages follow the normal Elsa
database provider choices, including SQLite, SQL Server, PostgreSQL, MySQL,
and Oracle.

For a CShells-based host, the release sample composes these modules with
feature sections like the following. Keep tokens and other credentials in a
secret provider; do not commit them to `appsettings.json`.

```json
{
  "CShells": {
    "Shells": {
      "Default": {
        "Features": {
          "AI": {
            "StreamingEnabled": true,
            "ConversationPersistenceEnabled": true,
            "ProposalReviewEnabled": true,
            "DefaultProviderName": "copilot",
            "Providers": [
              {
                "Name": "copilot",
                "Provider": "copilot",
                "Model": "<model>",
                "Enabled": true
              }
            ],
            "Agents": [
              {
                "Name": "workflow-author",
                "DisplayName": "Workflow author",
                "Description": "Creates safe workflow proposals"
              }
            ]
          },
          "CopilotAI": {
            "ProviderName": "copilot",
            "Model": "<model>"
          },
          "AIPersistence": {
            "ConnectionString": "<configured-by-secret-provider>"
          }
        }
      }
    }
  }
}
```

The exact configuration path is host-specific, but the option names above are
the `AIHostOptions`, `CopilotOptions`, and AI persistence feature properties
used by the release sample. `Grounding` limits can be configured when calling
`AddAIHostServices` directly; the CShells shell feature currently exposes the
host's top-level AI options but not a nested `Grounding` property.

For a custom host that already registers the AI shell feature and its HTTP
endpoints, the provider boundary is provider-neutral:

```csharp
services.AddAIHostServices(options =>
{
    options.DefaultProviderName = "copilot";
    options.Providers =
    [
        new AIProviderOptions
        {
            Name = "copilot",
            Provider = "copilot",
            Model = "<model>",
            Enabled = true
        }
    ];
});

services.AddCopilotAIProvider(options =>
{
    options.Model = "<model>";
    // Configure RuntimePath or RuntimeUrl and authentication through your
    // deployment's secret/configuration provider.
});
```

If more than one enabled provider is available, set
`DefaultProviderName`. The capabilities endpoint reports streaming as
unavailable when it cannot select one provider deterministically.

### Choose durable storage for production

`AddAIHostServices` supplies an in-memory conversation store by default. That
is suitable for development and tests, but it is process-local. Install and
configure a provider-specific AI persistence module for a production server
so that the required proposal and audit stores are also available. A durable
conversation store is required for Studio reconnect after a server restart;
the default reconnect grace window is five minutes.

The default retention and size limits are:

| Option | Default |
| --- | ---: |
| Conversation retention | 30 days |
| Reconnect grace | 5 minutes |
| Maximum tool result | 64 KiB |
| Maximum resolved context | 128 KiB |
| Maximum items per grounding query | 25 |
| Maximum grounding result | 64 KiB |

Tune these limits for the provider context window and the sensitivity of the
data you allow the AI Host to inspect.

## Add Weaver to Studio

Register the Studio module with the same authenticated backend configuration
used by the other remote modules:

```csharp
builder.Services.AddRemoteBackend(backendApiConfig);
builder.Services.AddWeaverModule(backendApiConfig);
```

The release Server and WASM hosts both use `AddWeaverModule`. The module uses
the backend authentication handler, calls the metadata endpoints, and streams
`POST /ai/chat` as server-sent events. It does not need an OpenAI, Copilot, or
other provider credential in the browser.

Weaver appears only when the backend advertises
`Elsa.AI.Host.ShellFeatures.AIFeature`. If the menu is missing, check the
server feature composition and the authenticated backend URL before changing
Studio code.

## Use Weaver in Studio

1. Open **Weaver** from the Studio menu.
2. Attach a workflow definition or workflow instance by its reference ID.
3. Select an advertised agent when the server exposes more than the default.
4. Ask a focused question, such as what caused an attached instance to fail
   or which activities are available for a particular process step.
5. Review the assistant response, tool activity, warnings, and any proposal
   notification before taking a normal Studio or API action.

The server resolves attachments. Send a reference and scope, not a copied
workflow or runtime database dump. Supported attachment kinds are advertised
by `GET /ai/capabilities`; the default kinds are workflow definitions,
workflow instances, activities, diagnostics scopes, and time ranges.

## What the AI Host can do

The built-in tools are grouped by purpose:

- **Activities**: search installed activity descriptors and retrieve a
  descriptor.
- **Workflow definitions**: search definitions, retrieve a definition or
  graph, and find usages.
- **Draft validation and proposals**: validate a draft, propose a new
  workflow, or propose an update against a baseline version.
- **Runtime inspection**: search instances, inspect an instance, read execution
  history or activity state, and search or inspect incidents.

Read-only tools are enabled by default. The create and update proposal tools
are disabled until the host explicitly enables them through the AI feature's
governed tool-enablement path. Draft validation is read-only and does not
persist the draft.

If a required server source is not registered—for example, the activity
registry, workflow definition store, workflow instance store, or proposal
store—the tool returns an unavailable result and the capabilities response
reports the reason. This is useful when a modular host intentionally omits a
feature, but it is not a model/provider failure.

## Proposal lifecycle and current release boundary

`workflows.proposeCreate` and `workflows.proposeUpdate` write an `AIProposal`
record containing the draft payload, rationale, validation diagnostics,
warnings, graph diff, actor, tenant, and (for updates) the baseline workflow
version. They produce either a validated or blocked proposal; they do not
persist a workflow definition.

The current `release/3.8.0` implementation exposes these server endpoints:

- `GET /ai/capabilities` — advertises AI capabilities and attachment kinds to
  Studio; requires `ai:capabilities:view`.
- `GET /ai/tools?agent=...` — lists tools visible to the current actor, tenant,
  and agent; requires `ai:tools:view`.
- `POST /ai/chat` — starts or reconnects a streamed chat turn; requires
  `ai:chat`.

The release does not yet expose proposal detail, approve, reject, or apply
endpoints. Studio can display proposal events, but its Approve, Reject, and
Apply buttons remain disabled. Do not document or build an integration around
`/ai/proposals/{id}/approve` or `/ai/proposals/{id}/apply` until those endpoints
land in a released backend.

The code also defines proposal permission names (`ai:proposals:view`,
`ai:proposals:approve`, and `ai:proposals:apply`) for the governed action
surface. Their presence does not mean that the corresponding action routes
exist in this release.

## Security and governance

The AI Host derives the actor from the authenticated user and the tenant from
the active tenant accessor or supported tenant claims. It ignores a
client-supplied provider name and accepts a requested agent only when that
agent is configured and its required permissions are present. Tool discovery
and execution are filtered for the current actor and tenant.

Grounded results are bounded and redacted before they are returned to the
model or Studio. Chat, tool, and provider activity goes through the AI Host's
audit path. Even with these controls, treat model output as untrusted:

- keep AI endpoints behind the same authentication and authorization boundary
  as the rest of Elsa Server;
- grant only the permissions needed for the user and agent;
- leave proposal tools disabled until a human review process exists;
- use tenant-aware persistence and verify database isolation; and
- avoid attaching broad runtime scopes or sensitive data when a narrower
  workflow, instance, activity, or time-range reference is enough.

### Do not confuse Weaver with the Agents extension

The `Elsa.Agents.*` packages in `elsa-extensions` provide agent activities and
workflow-oriented agent composition. Their `UseAgents` and OpenAI/Azure
OpenAI registration APIs are separate from Weaver's provider-neutral
`IAIProvider` boundary. Installing an Agents package does not configure the
Weaver menu or the `/ai/*` endpoints.

## Troubleshooting

### Weaver is missing from Studio

Confirm that the server composes `Elsa.AI.Host.ShellFeatures.AIFeature`, that
the Studio backend URL is correct, and that the authenticated caller can read
`GET /ai/capabilities`.

### Capabilities show no streaming

Check that at least one enabled provider is registered. If several providers
are enabled, set `AIHostOptions.DefaultProviderName` to a provider name that
the host can resolve.

### A tool is unavailable

Read the grounding capability's disabled reason. It usually means the related
workflow, runtime, activity, or proposal store is not registered, or that the
grounding family was disabled.

### A proposal never appears

Confirm that proposal tools were explicitly enabled and that an AI proposal
store is registered. A blocked proposal can still be recorded; inspect its
validation diagnostics rather than treating it as a provider error.

### Studio cannot reconnect

Conversation persistence must be enabled and backed by a durable conversation
store. Reconnect is accepted only for an authorized conversation during the
configured grace window.

## Release-source validation

This page was checked against the following `release/3.8.0` snapshots:

- Core [`5429008d`](https://github.com/elsa-workflows/elsa-core/tree/5429008d98a56afd29b4fd11107f7760710b1a64):
  - [AI Host README](https://github.com/elsa-workflows/elsa-core/blob/5429008d98a56afd29b4fd11107f7760710b1a64/src/modules/Elsa.AI.Host/README.md)
  - [Host options](https://github.com/elsa-workflows/elsa-core/blob/5429008d98a56afd29b4fd11107f7760710b1a64/src/modules/Elsa.AI.Host/Options/AIHostOptions.cs)
  - [AI endpoints](https://github.com/elsa-workflows/elsa-core/tree/5429008d98a56afd29b4fd11107f7760710b1a64/src/modules/Elsa.AI.Host/Endpoints/AI)
  - [Copilot provider](https://github.com/elsa-workflows/elsa-core/tree/5429008d98a56afd29b4fd11107f7760710b1a64/src/modules/Elsa.AI.Copilot)
- Studio [`d25f0aae`](https://github.com/elsa-workflows/elsa-studio/tree/d25f0aaeb5f14af6c5938d173aae828d87ebad5c):
  - [Weaver module](https://github.com/elsa-workflows/elsa-studio/tree/d25f0aaeb5f14af6c5938d173aae828d87ebad5c/src/modules/Elsa.Studio.AI)
  - [Weaver page](https://github.com/elsa-workflows/elsa-studio/blob/d25f0aaeb5f14af6c5938d173aae828d87ebad5c/src/modules/Elsa.Studio.AI/UI/Pages/Weaver.razor)
- Extensions [`335a2649`](https://github.com/elsa-workflows/elsa-extensions/tree/335a26495318f6ee1528bf2723b7333c753ce9a2):
  - [Agents module](https://github.com/elsa-workflows/elsa-extensions/tree/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/agents)

The latest released branch remained `release/3.8.0` in all three source
repositories; the refs had advanced since the previous inventory, so this page
uses the commits above.
