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

For WebAssembly Studio hosts, the default Elsa Studio shell does not require Razor page changes, but custom hosts must include the WebAssembly authentication script in `wwwroot/index.html`:

```html
<script src="_content/Microsoft.AspNetCore.Components.WebAssembly.Authentication/AuthenticationService.js"></script>
<script src="_framework/blazor.webassembly.js"></script>
```

See the [Authentication & Authorization Guide](../guides/authentication.md#studio-authentication-configuration) for the full Studio OIDC setup.
