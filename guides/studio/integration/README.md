---
description: >-
  Source-backed guide to the Elsa Studio host models available in release 3.8.0,
  including standalone hosts, custom elements, backend configuration, and
  authentication.
---

# Studio Integration

This guide is based on the `release/3.8.0` source code in `elsa-studio` and `elsa-core`.

In Elsa 3.8.0, Elsa Studio is shipped in three integration shapes:

| Host model | Source | Best for |
| --- | --- | --- |
| Standalone Blazor Server host | `src/hosts/Elsa.Studio.Host.Server` | Server-rendered Studio deployments with server-side auth handling |
| Standalone Blazor WebAssembly host | `src/hosts/Elsa.Studio.Host.Wasm` | Browser-hosted Studio deployments |
| Custom-elements host | `src/hosts/Elsa.Studio.Host.CustomElements` | Embedding Studio components into another web app |

If you are integrating Elsa Studio into React, Angular, MVC, Razor Pages, or another host application, the supported route in the source tree is the custom-elements host. The repository also includes a React wrapper around those custom elements.

## Choose the Right Host Model

Use the standalone hosts when Studio should run as its own application.

Use the custom-elements host when you want to embed selected Studio capabilities, such as the workflow editor or workflow lists, inside an existing application shell.

If you also need to change shell composition, branding, menus, widgets, or
workflow-editor extension points, continue with
[Customizing Elsa Studio](../customization.md).

## Shared Configuration

All current host models connect to Elsa Server through the `Backend` section.

```json
{
  "Backend": {
    "Url": "https://localhost:5001/elsa/api"
  }
}
```

This value is bound in the Studio hosts through `AddRemoteBackend`.

### Authentication Provider

The standalone hosts read `Authentication:Provider` and configure the HTTP and SignalR authentication handlers from that setting.

Supported modern values are:

- `ElsaIdentity`
- `OpenIdConnect`

Current host defaults differ:

- Blazor Server defaults to `ElsaIdentity` when the setting is omitted.
- Blazor WebAssembly defaults to `OpenIdConnect` when the setting is omitted.

The source still contains a legacy `ElsaLogin` path for backward compatibility, but new integrations should use `ElsaIdentity` or `OpenIdConnect`.

### Localization

Both standalone hosts bind the `Localization` section and register the localization module.

```json
{
  "Localization": {
    "DefaultCulture": "en-US",
    "SupportedCultures": [
      "en-GB",
      "nl-NL"
    ]
  }
}
```

In the server host, the default culture is forced to the front of `SupportedCultures` before registration.

## Standalone Blazor Server Host

The server host lives in `src/hosts/Elsa.Studio.Host.Server` and wires up:

- `AddRemoteBackend`
- dashboard, workflows, alterations, diagnostics, secrets, and localization modules
- `UseElsaLocalization()`
- `UseAuthentication()` and `UseAuthorization()`

Use this host when you want a dedicated Studio application with server-side authentication handling.

### Elsa Identity Configuration

```json
{
  "Backend": {
    "Url": "https://localhost:5001/elsa/api"
  },
  "Authentication": {
    "Provider": "ElsaIdentity"
  }
}
```

With this provider, the server host registers `AddElsaIdentity()` and `AddElsaIdentityUI()`. The login UI is provided by the `Elsa.Studio.Authentication.ElsaIdentity.UI` module.

### OpenID Connect Configuration

```json
{
  "Backend": {
    "Url": "https://localhost:5001/elsa/api"
  },
  "Authentication": {
    "Provider": "OpenIdConnect",
    "OpenIdConnect": {
      "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
      "ClientId": "elsa-studio",
      "ClientSecret": "",
      "AuthenticationScopes": [
        "openid",
        "profile",
        "offline_access"
      ],
      "BackendApiScopes": [
        "api://{backend-api-app-id}/elsa-server-api"
      ],
      "SaveTokens": true
    }
  }
}
```

Use `ClientSecret` only for confidential clients such as the server host. In the OpenID Connect modules, bearer tokens for Elsa API calls are attached by `OidcAuthenticatingApiHttpMessageHandler`, and SignalR connections are configured through `OidcHttpConnectionOptionsConfigurator`.

`AuthenticationScopes` are requested during sign-in. `BackendApiScopes` are requested when Studio obtains an access token for the Elsa Server API. Some identity providers require the backend API scope in the original sign-in grant before refresh-token or incremental token acquisition can return backend API tokens; in that case, include the backend API scope in both arrays.

For Blazor Server Studio, register `{studio-url}/signin-oidc` as the redirect URI and `{studio-url}/signout-callback-oidc` as the signed-out callback URI unless you override the defaults. Studio initiates logout at `{studio-url}/authentication/logout`.

## Standalone Blazor WebAssembly Host

The WebAssembly host lives in `src/hosts/Elsa.Studio.Host.Wasm`. It registers the same backend-facing modules, but the application runs in the browser and uses the WebAssembly-specific authentication and localization packages.

Use this host when you want a dedicated SPA-style Studio deployment.

### OpenID Connect Configuration

```json
{
  "Backend": {
    "Url": "https://localhost:5001/elsa/api"
  },
  "Authentication": {
    "Provider": "OpenIdConnect",
    "OpenIdConnect": {
      "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
      "ClientId": "elsa-studio-wasm",
      "AuthenticationScopes": [
        "openid",
        "profile",
        "offline_access"
      ],
      "BackendApiScopes": [
        "api://{backend-api-app-id}/elsa-server-api"
      ]
    }
  }
}
```

Do not configure a client secret in WebAssembly. The browser host is a public client.

`AuthenticationScopes` are requested during sign-in. `BackendApiScopes` are requested when Studio obtains an access token for the Elsa Server API. Some identity providers require the backend API scope in the original sign-in grant before refresh-token or incremental token acquisition can return backend API tokens; in that case, include the backend API scope in both arrays.

For Blazor WebAssembly Studio, register `{studio-url}/authentication/login-callback` as the redirect URI and `{studio-url}/authentication/logout-callback` as the logout callback URI. Studio initiates logout at `{studio-url}/authentication/logout`.

### Elsa Identity Configuration

```json
{
  "Backend": {
    "Url": "https://localhost:5001/elsa/api"
  },
  "Authentication": {
    "Provider": "ElsaIdentity"
  }
}
```

With this provider, the WASM host registers `AddElsaIdentity()` and `AddElsaIdentityUI()`.

## Embedding Studio with Custom Elements

The custom-elements host is the supported source-backed path for integrating Studio into another frontend shell.

The host registers these custom elements:

- `elsa-backend-provider`
- `elsa-workflow-definition-editor`
- `elsa-workflow-instance-viewer`
- `elsa-workflow-instance-list`
- `elsa-workflow-definition-list`

For the full source-backed setup, including tenant headers, bearer tokens,
per-element configuration, and the React wrapper, see
[Custom Elements Embedding](custom-elements.md).

### Why Use It

Use the custom-elements host when:

- your application already owns the main layout and navigation
- you want to embed only specific Studio screens
- your host application already has its own authentication flow

### Passing Backend Settings

The custom-elements host accepts backend settings as element attributes and stores them in `BackendService`.

Supported attributes include:

- `remote-endpoint`
- `api-key`
- `access-token`
- `tenant-id`
- `tenant-id-header-name`

The host then applies those values to both:

- HTTP API calls through `AuthHttpMessageHandler`
- SignalR connections through `BackendServiceHttpConnectionOptionsConfigurator`

### HTML Example

```html
<elsa-backend-provider
  remote-endpoint="https://localhost:5001/elsa/api"
  access-token="YOUR_ACCESS_TOKEN"
  tenant-id="acme"
  tenant-id-header-name="X-Tenant-Id">
</elsa-backend-provider>

<elsa-workflow-definition-editor
  definition-id="order-approval">
</elsa-workflow-definition-editor>
```

If you use an API key instead of a bearer token, provide `api-key` and omit `access-token`. The host sends API keys as `Authorization: ApiKey <key>`.

### React Integration

The `elsa-studio` repository includes a React wrapper in `src/wrappers/wrappers/react-wrapper`.

Its `BackendProvider` component renders `elsa-backend-provider`, and its `WorkflowDefinitionEditor` component renders `elsa-workflow-definition-editor`.

```tsx
<BackendProvider
  remoteEndpoint="https://localhost:5001/elsa/api"
  accessToken={token}
/>

<WorkflowDefinitionEditor definitionId="order-approval" />
```

The same custom-elements host model can be used from Angular or other frontend frameworks that can render standard browser custom elements.

## Backend Considerations

If Studio runs on a different origin from Elsa Server, the backend must allow cross-origin browser traffic. The sample `Elsa.Server.Web` application in `elsa-core` enables CORS globally with `app.UseCors()`, but production deployments should restrict allowed origins to the Studio hosts you actually use.

If your backend secures workflow, dashboard, or diagnostic endpoints, make sure the same credential type is available to both normal HTTP requests and SignalR connections:

- `ElsaIdentity` and `OpenIdConnect` standalone hosts do this through their host-specific authentication handlers.
- the custom-elements host does this through `AuthHttpMessageHandler` and `BackendServiceHttpConnectionOptionsConfigurator`.

## What This Guide Does Not Assume

This guide does not assume that:

- Studio must run as a standalone application
- your host application uses Blazor
- your backend uses only cookie authentication
- tenant context always comes from the access token

If you are embedding Studio surfaces into another frontend shell, use
[Custom Elements Embedding](custom-elements.md) as the implementation guide.

This guide does not assume a first-class Angular, MVC, or Razor Pages Studio host in the Elsa 3.8.0 source tree. For those scenarios, use one of these patterns instead:

- run the standalone Studio host as a separate application
- embed the custom-elements host inside your existing application

## Related Guides

- [Studio User Guide](../README.md)
- [Expressions](../expressions.md)
- [Authentication & Authorization](../../authentication.md)
- [External Identity Providers](../../security/external-identity-providers.md)
