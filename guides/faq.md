---
description: >-
  Answers to common Elsa Workflows questions about designing, starting,
  waiting, versioning, and operating workflows.
---

# Frequently Asked Questions

This page is a route into the documentation, not a replacement for the
task-specific guides. The answers describe Elsa 3.8.0 behavior; follow the
linked guide when you need configuration or a complete example.

## Designing workflows

### What is the difference between a workflow definition and a workflow instance?

A workflow definition is the versioned plan: its activities, connections,
inputs, outputs, and triggers. A workflow instance is one execution of a
definition, with its own state, variables, bookmarks, journal, and status.

Use the definition when you are designing or publishing behavior. Use the
instance when you are investigating or changing one execution. Start with
[Core Concepts](../getting-started/concepts/README.md), then see [Workflow
Definition Version Lifecycle](running-workflows/workflow-definition-lifecycle.md)
for drafts, published versions, and Studio history.

### Should I use a trigger or a blocking activity?

Use a **trigger** when an external event should start a new workflow instance,
such as an HTTP request, timer, or message. Use a **blocking activity** when an
already-running instance must wait for a callback, approval, signal, or other
stimulus before continuing.

For example, a timer trigger can start a new instance every time its schedule
fires. An inline `Delay` or a custom bookmark pauses the current instance. See
[Using a Trigger](running-workflows/using-a-trigger.md) and [Long-Running
Workflows](running-workflows/long-running-workflows.md).

### Why does my workflow show `Running` while it is waiting?

In Elsa, `Running` also covers an instance that is waiting for external
stimulus. A blocking activity creates a bookmark; creating a bookmark causes
the workflow to suspend after pending activities have completed, and the
bookmark records how the instance can be resumed.

Do not treat `Running` alone as proof that the next activity is executing. Open
the instance journal and bookmark information to distinguish active work from
durable waiting. See [Workflow State and Journal](../operate/workflow-state-and-journal.md)
and [Bookmark Resume Tokens](security/bookmark-resume-tokens.md).

## Starting and versioning

### What is the quickest way to run a workflow?

Choose the entry point that matches the caller:

- **Elsa Studio**: a designer or domain specialist can select a definition and
  use its run/execute action.
- **`POST /elsa/api/workflow-definitions/{definitionId}/execute`**: an HTTP
  caller needs a result from a bounded workflow interaction.
- **`POST /elsa/api/workflow-definitions/{definitionId}/dispatch`**: an
  integration hands work to the runtime and continues; the response includes
  an instance ID for later observation.
- **A trigger**: a business event should start instances automatically from
  matching published definitions.

See [Running Workflows](running-workflows/README.md) for the API, Studio, and
library options, and [Bulk Dispatch Workflows](running-workflows/bulk-dispatch-workflows.md)
when one caller should start one instance per item.

### Why does the API run an older version than the one I see in Studio?

The 3.8.0 `execute` and `dispatch` endpoints use the **published** version by
default. Saving a draft in Studio does not make it the runtime version. Publish
the version you intend to run, or request a different version explicitly when
your integration is designed to do so.

Publishing a new version retracts the previous published version for that
definition. A running instance is different: the runtime resolves its graph
from the instance's stored definition-version ID, so publishing a new version
does not silently rewrite an instance that is already running.

See [Workflow Definition Version Lifecycle](running-workflows/workflow-definition-lifecycle.md)
and [Loading Workflows from JSON](loading-workflows-from-json.md).

### Why does a trigger work in a draft but not in production?

Only published workflow definitions contribute triggers to the runtime trigger
index. A draft can be visible in Studio and still be absent from the trigger
index. Publish the definition, confirm that the relevant trigger module is
registered, and verify the endpoint or message-broker configuration for the
host.

For HTTP workflows, also check the endpoint's authentication and permission
configuration. See [HTTP Workflows](http-workflows/README.md), [HTTP Endpoint
Security](security/http-endpoint-security.md), and [Using a
Trigger](running-workflows/using-a-trigger.md).

### What is the difference between `execute` and `dispatch`?

Both select a workflow definition and start an instance, but they have
different request lifecycles:

- `execute` starts through the workflow starter and keeps the request involved
  long enough to return the resulting workflow state or fault when the
  workflow completes in that request.
- `dispatch` sends a background command to the runtime and returns after the
  dispatch is accepted. Use the instance ID to observe the result later.

Use `execute` for a bounded request/response interaction. Use `dispatch` for
long-running, fire-and-forget, or integration work where the caller should not
wait for completion. See [Bulk Dispatch Workflows](running-workflows/bulk-dispatch-workflows.md)
for the related fan-out case.

## Waiting, resuming, and diagnosing

### How do I resume a waiting workflow?

Resume the bookmark created by the waiting activity. Elsa exposes
`GET /bookmarks/resume` and `POST /bookmarks/resume`; both require a valid
bookmark resume token, and `POST` can carry input in the request body. The
endpoint accepts an `async` option when the resume should be queued.

Treat the token as a bearer credential. The release endpoint is intentionally
anonymous because the token authorizes the resume; protect token generation,
transport, logging, and any public entry point around it. See [Bookmark Resume
Tokens](security/bookmark-resume-tokens.md) and [Long-Running
Workflows](running-workflows/long-running-workflows.md).

### My workflow failed or appears stuck. What should I inspect first?

Use this order:

1. Open the workflow instance and check its status, sub-status, and incidents.
2. Read the journal to find the last transition and the activity that was
   executing.
3. Inspect activity execution records and host logs for the exception,
   external call, or missing configuration.
4. If the instance is waiting, inspect its bookmarks and the stimulus or token
   used to resume it.
5. In a multi-node deployment, check readiness, persistence, distributed
   coordination, and worker/broker health before retrying side effects.

Start with [Investigate a Workflow Instance](../operate/workflow-state-and-journal.md),
then use [Monitoring & Observability](../operate/monitoring-observability.md),
[Incidents](../operate/incidents/README.md), and the [Troubleshooting
Guide](troubleshooting/README.md).

### How should I test a workflow before deploying it?

Test the smallest boundary that can prove the behavior: use an activity fixture
for one custom activity, a workflow fixture for routing and outcomes, and an
integration host for persistence, authentication, HTTP, or broker behavior.
Then inspect a real instance through Studio or the runtime APIs when the
failure depends on deployment configuration. See [Testing & Debugging
Workflows](testing-debugging.md).

## Further help

If the question is about an official release, source behavior, or a reproducible
bug, include the Elsa package versions, host shape, workflow definition or
minimal reproduction, and relevant logs/journal entries when asking for help.
Use [Community & Resources](community-resources.md) to choose the appropriate
channel.

## Release-source checks

The runtime-specific statements on this page were checked against the 3.8.0
release source:

- [Workflow execution helper](https://github.com/elsa-workflows/elsa-core/blob/5429008d98a56afd29b4fd11107f7760710b1a64/src/modules/Elsa.Workflows.Api/Endpoints/WorkflowDefinitions/Execute/WorkflowExecutionHelper.cs)
- [Workflow dispatch endpoint](https://github.com/elsa-workflows/elsa-core/blob/5429008d98a56afd29b4fd11107f7760710b1a64/src/modules/Elsa.Workflows.Api/Endpoints/WorkflowDefinitions/Dispatch/Endpoint.cs)
- [Background workflow dispatcher](https://github.com/elsa-workflows/elsa-core/blob/5429008d98a56afd29b4fd11107f7760710b1a64/src/modules/Elsa.Workflows.Runtime/Services/BackgroundWorkflowDispatcher.cs)
- [Trigger indexer](https://github.com/elsa-workflows/elsa-core/blob/5429008d98a56afd29b4fd11107f7760710b1a64/src/modules/Elsa.Workflows.Runtime/Services/TriggerIndexer.cs)
- [Bookmark creation](https://github.com/elsa-workflows/elsa-core/blob/5429008d98a56afd29b4fd11107f7760710b1a64/src/modules/Elsa.Workflows.Core/Contexts/ActivityExecutionContext.cs)
- [Bookmark resume endpoint](https://github.com/elsa-workflows/elsa-core/blob/5429008d98a56afd29b4fd11107f7760710b1a64/src/modules/Elsa.Workflows.Api/Endpoints/Bookmarks/Resume/Endpoint.cs)
- [Local workflow client](https://github.com/elsa-workflows/elsa-core/blob/5429008d98a56afd29b4fd11107f7760710b1a64/src/modules/Elsa.Workflows.Runtime/Services/LocalWorkflowClient.cs)
- [Workflow status](https://github.com/elsa-workflows/elsa-core/blob/5429008d98a56afd29b4fd11107f7760710b1a64/src/clients/Elsa.Api.Client/Resources/WorkflowInstances/Enums/WorkflowStatus.cs)
- [Studio workflow definition actions](https://github.com/elsa-workflows/elsa-studio/blob/d25f0aaeb5f14af6c5938d173aae828d87ebad5c/src/modules/Elsa.Studio.Workflows/Components/WorkflowDefinitionList/WorkflowDefinitionList.razor.cs)
- [Studio workflow-instance journal](https://github.com/elsa-workflows/elsa-studio/blob/d25f0aaeb5f14af6c5938d173aae828d87ebad5c/src/modules/Elsa.Studio.Workflows/Components/WorkflowInstanceViewer/Components/Journal.razor.cs)
