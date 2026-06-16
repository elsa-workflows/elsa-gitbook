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

Use a `ClientSecret` only for confidential clients such as a Blazor Server Studio host. Do not use a client secret for WebAssembly or other browser-hosted public clients; use authorization code flow with PKCE instead.

See the [Authentication & Authorization Guide](../guides/authentication.md#studio-authentication-configuration) for the full Studio OIDC setup.
