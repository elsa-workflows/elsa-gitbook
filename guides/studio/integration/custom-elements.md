---
description: >-
  Source-backed cookbook for embedding Elsa Studio custom elements in release
  3.8.0, including backend configuration, authentication, tenant headers, and
  React usage.
---

# Custom Elements Embedding

This guide is based on `release/3.8.0` in `elsa-studio` and `elsa-core`.

Use this integration model when your application already owns the shell and
you want to embed selected Elsa Studio surfaces such as the workflow editor,
workflow definition list, workflow instance list, or workflow instance viewer.

The source-backed host for this model is
`src/hosts/Elsa.Studio.Host.CustomElements` in `elsa-studio`.

## What The Host Registers

The custom-elements host registers these elements:

- `elsa-backend-provider`
- `elsa-workflow-definition-editor`
- `elsa-workflow-definition-list`
- `elsa-workflow-instance-list`
- `elsa-workflow-instance-viewer`

Each workflow element inherits `BackendComponentBase`, so every one of them can
accept backend-related attributes directly. The `elsa-backend-provider` element
exists to set that configuration once and reuse it across embedded surfaces.

## Supported Backend Attributes

The custom-elements host maps these attributes into the singleton
`BackendService`:

- `remote-endpoint`
- `api-key`
- `access-token`
- `tenant-id`
- `tenant-id-header-name`

`tenant-id-header-name` defaults to `X-Tenant-Id` when you do not provide it.

## How Credentials Flow

The host applies backend settings to both request paths used by Studio:

- Normal HTTP API calls go through `AuthHttpMessageHandler`.
- SignalR connections go through
  `BackendServiceHttpConnectionOptionsConfigurator`.

That matters because embedded Studio uses both. For example, the workflow
instance viewer relies on SignalR for live updates.

Authentication behavior is:

- If `api-key` is set, the host sends `Authorization: ApiKey <key>`.
- Otherwise, if `access-token` is set, the host sends
  `Authorization: Bearer <token>`.
- If `tenant-id` is set, the host also sends that value using
  `tenant-id-header-name`.

## Backend Requirements

Your Elsa Server backend still needs to allow the browser application to call
it.

At minimum, verify:

1. `remote-endpoint` points to the Elsa API base URL, for example
   `https://server.example.com/elsa/api`.
2. CORS allows the origin hosting your embedded Studio shell.
3. The credential type you supply is accepted by Elsa Server.
4. If you use tenant headers, the backend tenant resolution pipeline is
   configured to read the same header name.

The sample `Elsa.Server.Web` host in `elsa-core` enables CORS with
`app.UseCors()`, but production deployments should restrict allowed origins to
the hosts you actually trust.

## Recommended Pattern: Shared Backend Provider

Use `elsa-backend-provider` when several embedded Studio surfaces share the same
backend and authentication settings.

```html
<elsa-backend-provider
  remote-endpoint="https://localhost:5001/elsa/api"
  access-token="YOUR_ACCESS_TOKEN"
  tenant-id="acme"
  tenant-id-header-name="X-Tenant-Id">
</elsa-backend-provider>

<elsa-workflow-definition-list></elsa-workflow-definition-list>

<elsa-workflow-definition-editor
  definition-id="order-approval">
</elsa-workflow-definition-editor>
```

This is the most reliable pattern when you need bearer tokens or tenant headers,
because the host stores those values in `BackendService` once and both HTTP and
SignalR paths reuse them.

## Per-Element Configuration

Because each workflow element inherits `BackendComponentBase`, you can also pass
backend settings on the element itself:

```html
<elsa-workflow-instance-viewer
  instance-id="3f4972f8a5d54d93a5dfc3b6db7d39ae"
  remote-endpoint="https://localhost:5001/elsa/api"
  api-key="YOUR_API_KEY">
</elsa-workflow-instance-viewer>
```

Use this pattern when a page renders only one embedded surface or when
individual surfaces must target different backends.

## Choosing Between API Keys And Bearer Tokens

Use an API key when your host application authenticates as a technical client
instead of a signed-in end user.

Use a bearer access token when your host application already has a user-facing
authentication flow and you want Elsa Studio requests to run as that user.

Do not provide both unless you intentionally want the API key to win. The host
prefers `api-key` over `access-token`.

## React Wrapper

`elsa-studio` includes a React wrapper in
`src/wrappers/wrappers/react-wrapper`.

The wrapper exposes these components:

- `BackendProvider`
- `WorkflowDefinitionEditor`
- `WorkflowDefinitionList`
- `WorkflowInstanceList`
- `WorkflowInstanceViewer`

Example:

```tsx
<BackendProvider
  remoteEndpoint="https://localhost:5001/elsa/api"
  accessToken={token}
/>

<WorkflowDefinitionEditor definitionId="order-approval" />
```

Use `BackendProvider` for the wrapper's built-in shared backend settings. In the
current wrapper sources, `BackendProvider` explicitly forwards
`remote-endpoint`, `api-key`, and `access-token`, while the workflow element
wrappers primarily forward `remote-endpoint` and `api-key`.

If you also need `tenant-id` or `tenant-id-header-name`, prefer rendering the
raw custom elements directly or extending the wrapper in your host application.

## Which Surface To Embed

Pick the smallest surface that matches your host application's job:

| Element | Use it when |
| --- | --- |
| `elsa-workflow-definition-list` | You want a browse-and-open experience for definitions |
| `elsa-workflow-definition-editor` | You want to edit one workflow definition by ID |
| `elsa-workflow-instance-list` | You want operators to browse workflow runs |
| `elsa-workflow-instance-viewer` | You want to inspect one workflow instance in detail |

## Common Failure Modes

If the elements render but data does not load:

- Check that `remote-endpoint` includes `/elsa/api`.
- Check browser network traffic for CORS failures.
- Check whether your backend expects `Authorization: Bearer` or
  `Authorization: ApiKey`.
- Check whether the tenant header name matches the backend tenant resolver.

If list views work but live instance updates do not:

- Check that the same credential reaches SignalR, not just normal HTTP calls.
- Check whether reverse proxies allow WebSockets or SignalR fallback transports.

If you need a dedicated Studio application instead of embedded surfaces, return
to the main [Studio Integration](README.md) guide and use one of the standalone
hosts instead.
