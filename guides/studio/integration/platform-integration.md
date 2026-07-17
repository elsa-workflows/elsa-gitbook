---
description: >-
  Release-backed guidance for submitting Elsa Studio workflow definitions as
  Elsa Platform artifacts in Elsa Studio 3.8.0.
---

# Submit Workflow Definitions to Elsa Platform

Use the Platform integration when a Studio host you control must register
workflow-definition artifacts with Elsa Platform. It adds submission actions to
Studio and can submit after an individual workflow is published.

This is a Studio-host module, not a custom-elements feature. The released
3.8.0 Server, WebAssembly, and custom-elements hosts do **not** call
`AddPlatformIntegrationModule`. Your host must reference the
`Elsa.Studio.PlatformIntegration` module and register it explicitly.

If you only need to embed a designer or instance view in an existing
application, use [Custom Elements Embedding](custom-elements.md) instead.

## What Is Submitted

Platform submission registers an artifact envelope and a reference to the
workflow-definition payload. It does **not** upload the workflow JSON to
Platform.

The module serializes the workflow definition, derives a SHA-256 digest, and
registers an `elsa.workflow-definition` artifact at:

```
{PlatformEndpoint}/api/workspaces/{WorkspaceId}/artifacts
```

By default, the payload reference uses provider `producer-managed` and a URI
like `studio://workflows/{definition-id}/snapshots/{digest}`. Before enabling
the integration, make sure your Platform deployment can resolve that reference
through a producer-managed payload mechanism. This module does not provide a
payload host or resolver.

## When To Use It

Use this module when a technical Studio host owns the relationship with Elsa
Platform and needs a workflow artifact catalog or handoff process.

Do not use it merely to embed Studio screens. It does not add Platform
submission to the prebuilt custom-elements bundle, and it does not deploy a
workflow from Platform to an Elsa runtime.

## Register the Module

Add the module where you compose your custom Studio host:

```csharp
using System.Net.Http.Headers;
using Elsa.Studio.PlatformIntegration.Extensions;

var platformAccessToken = builder.Configuration["Platform:AccessToken"]
    ?? throw new InvalidOperationException("Platform access token is required.");

builder.Services.AddPlatformIntegrationModule(options =>
{
    options.PlatformEndpoint = new Uri("https://platform.example.com");
    options.WorkspaceId = Guid.Parse("00000000-0000-0000-0000-000000000000");
    options.ConfigureRequestAsync = (request, _) =>
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", platformAccessToken);
        return Task.CompletedTask;
    };
});
```

`PlatformEndpoint` is the Platform base address, not the artifact route; the
client appends the workspace route shown above. A non-empty `PlatformEndpoint`
and `WorkspaceId` make the integration configured. `ConfigureRequestAsync` is
the module's deliberate request-authentication hook, so supply the Platform
credential there rather than assuming Studio's Elsa Server credential is
reused.

Keep secrets out of source code. Use your host's configuration or secret store
for the Platform credential.

## Submission Modes

`Enabled` and `SubmitOnWorkflowPublished` both default to `true`.

| Mode | What happens |
| --- | --- |
| Manual | Studio shows a **Submit to Platform** action in the workflow editor toolbar, each workflow-list row, and the workflow-list bulk actions. |
| Automatic | After Studio successfully publishes one workflow definition, the module submits its artifact when it is enabled and configured. |

Choose manual-only submission when an operator must approve each Platform
registration. Start with the complete registration above, including its
Platform authentication callback, then set:

```csharp
builder.Services.AddPlatformIntegrationModule(options =>
{
    // Configure PlatformEndpoint, WorkspaceId, and ConfigureRequestAsync.
    options.PlatformEndpoint = new Uri("https://platform.example.com");
    options.WorkspaceId = Guid.Parse("00000000-0000-0000-0000-000000000000");
    options.SubmitOnWorkflowPublished = false;
});
```

The editor action can submit its current in-memory workflow snapshot, including
unsaved designer changes. List actions load the latest version for each selected
definition ID; they are not a historical-version or published-only selector.
Bulk actions submit definitions one at a time and can report a partial failure.

Automatic submission is best effort. The publish notification handler logs a
submission failure instead of failing or rolling back the workflow publication.
It runs for individual Studio publish notifications. Studio bulk publishing
uses a separate bulk notification, so do not rely on it to automatically submit
every workflow in a bulk publish operation.

## Verify the Integration

1. Configure a Platform endpoint, a non-empty workspace ID, and request auth.
2. Open a workflow in the Studio host. The submit action remains disabled until
   the required Platform options are configured.
3. Submit a workflow manually, or publish one workflow with automatic
   submission enabled.
4. Confirm the Platform workspace contains an artifact with the expected
   workflow identity and SHA-256 digest.
5. Submit the unchanged snapshot again. A `200 OK` response is treated as a
   successful duplicate, while `201 Created` means a new submission.

For a failed submission, inspect the Studio-host logs and the Platform response:

| Response | Meaning in the module |
| --- | --- |
| `401` or `403` | The Platform credential was rejected. |
| `400` | The submission failed validation. |
| `409` | The artifact conflicts with an existing Platform record. |
| `5xx` | The failure is marked retryable. |

Also verify that Platform can resolve the registered payload URI. Seeing an
artifact record alone does not prove the workflow payload is available to a
consumer.

## Related Guidance

- [Studio Integration](README.md) explains the available Studio host models.
- [Customizing Elsa Studio](../customization.md) explains widgets and other
  host extension seams.
- [Custom Elements Embedding](custom-elements.md) is the supported path for
  embedding selected Studio surfaces without adding this host module.
