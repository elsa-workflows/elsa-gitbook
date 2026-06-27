# Authentication

## Configuring Authentication for Elsa HTTP API

### Overview

This section outlines the steps for configuring different authentication modes for the Elsa HTTP API.

## Authentication Modes

### No Authentication

Authentication can be disabled if necessary.

In the application hosting the API, security requirements must be disabled in `Program.cs` using

```
Elsa.EndpointSecurityOptions.DisableSecurity();
```

to permit anonymous requests.

Additionally, when using Elsa.Studio, add the following to `Program.cs`

```
builder.Services.AddShell(x => x.DisableAuthorization = true);
```

### Using Elsa.Identity

Use Elsa's built-in identity system for authentication.

Elsa API endpoint authorization is based on permission claims. The claim type is `permissions`; each claim value must match an Elsa API permission, and `*` grants all Elsa API permissions.

Elsa Identity roles collect permission strings. When Elsa Identity issues a token, permissions from assigned roles are emitted as `permissions` claims. API-key authentication handlers should add equivalent `permissions` claims. External OIDC providers should emit these values as `permissions` claims, or the host application should map external roles, groups, or scopes into `permissions` claims.

ASP.NET Core `RequireRole(...)` policies can protect custom host endpoints, but they do not replace the `permissions` claims checked by Elsa API endpoints. Permission names come from endpoint configuration and module constants, not only from shared `PermissionNames` constants.

### Using OIDC (OpenID Connect)

OIDC integration has two parts:

* Elsa Server must validate the access tokens sent to its API endpoints.
* Elsa Studio must sign users in and attach an access token to calls to the Elsa Server API.

For Elsa Studio 3.7, configure the `Elsa.Studio.Authentication.OpenIdConnect` modules with `Authentication:Provider` set to `OpenIdConnect`:

```json
{
  "Backend": {
    "Url": "https://your-elsa-server.com/elsa/api"
  },
  "Authentication": {
    "Provider": "OpenIdConnect",
    "OpenIdConnect": {
      "Authority": "https://your-identity-provider.com",
      "ClientId": "elsa-studio",
      "AuthenticationScopes": ["openid", "profile", "offline_access"],
      "BackendApiScopes": ["elsa_api"]
    }
  }
}
```

`AuthenticationScopes` are requested during Studio sign-in. `BackendApiScopes` are requested when Studio obtains an access token for calls to the Elsa Server API. Some identity providers require the backend API scope to be part of the original sign-in grant before they allow refresh-token or incremental token acquisition for that scope. If your provider behaves this way, include the backend API scope in both arrays, for example `AuthenticationScopes: ["openid", "profile", "offline_access", "elsa_api"]` and `BackendApiScopes: ["elsa_api"]`.

Register the redirect and logout callback URIs for the Studio host model you use:

* Blazor WebAssembly Studio uses `{studio-url}/authentication/login-callback` and `{studio-url}/authentication/logout-callback`.
* Blazor Server Studio uses `{studio-url}/signin-oidc` and `{studio-url}/signout-callback-oidc` by default.

Studio initiates OIDC logout through `{studio-url}/authentication/logout`.

Use a `ClientSecret` only for confidential clients such as a Blazor Server Studio host. Do not use a client secret for WebAssembly or other browser-hosted public clients; use authorization code flow with PKCE instead.

For WebAssembly Studio hosts, the default Elsa Studio shell does not require Razor page changes, but custom hosts must include the WebAssembly authentication script in `wwwroot/index.html`:

```html
<script src="_content/Microsoft.AspNetCore.Components.WebAssembly.Authentication/AuthenticationService.js"></script>
<script src="_framework/blazor.webassembly.js"></script>
```

See the [Authentication & Authorization Guide](../guides/authentication.md#studio-authentication-configuration) for the full Studio OIDC setup.
