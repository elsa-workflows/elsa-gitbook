---
description: >-
  Configure Elsa's built-in identity system for users, roles, tokens, and API
  applications.
---

# Elsa Identity

Elsa Identity is the built-in option when Elsa should manage users, roles,
access and refresh tokens, and API applications. Roles contain Elsa permission
strings; issued JWTs and authenticated API keys expose those permissions as
`permissions` claims.

{% hint style="info" %}
The examples target Elsa 3.8.0. Install the identity package from NuGet.org:

```bash
dotnet add package Elsa.Identity --version 3.8.0
```
{% endhint %}

## Register the identity modules

Install the `Elsa.Identity` package and configure identity before the workflow
API:

```csharp
using Elsa.Extensions;

var builder = WebApplication.CreateBuilder(args);
var identitySection = builder.Configuration.GetSection("Identity");
var tokenSection = identitySection.GetSection("Tokens");

builder.Services.AddElsa(elsa =>
{
    elsa
        .UseIdentity(identity =>
        {
            identity.TokenOptions += options => tokenSection.Bind(options);
            identity.UseConfigurationBasedUserProvider(options =>
                identitySection.Bind(options));
            identity.UseConfigurationBasedApplicationProvider(options =>
                identitySection.Bind(options));
            identity.UseConfigurationBasedRoleProvider(options =>
                identitySection.Bind(options));
        })
        .UseDefaultAuthentication()
        .UseWorkflowManagement()
        .UseWorkflowRuntime()
        .UseWorkflowsApi();
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapWorkflowsApi();
app.Run();
```

`UseIdentity` registers the identity services and selected providers.
`UseDefaultAuthentication` enables Elsa JWT bearer and API-key authentication.
The authentication and authorization middleware must run before the Elsa API
endpoints.

For a new host that needs an initial administrator, prefer the 3.8.0
`DefaultAdminUser` bootstrap instead of a hard-coded `UseAdminUserProvider()`
sample. Read both values from deployment configuration:

```csharp
var adminUserName = builder.Configuration["Identity:Bootstrap:UserName"]
    ?? throw new InvalidOperationException("Identity:Bootstrap:UserName is required.");
var adminPassword = builder.Configuration["Identity:Bootstrap:Password"]
    ?? throw new InvalidOperationException("Identity:Bootstrap:Password is required.");

builder.Services.AddElsa(elsa => elsa
    .UseIdentity(identity => identity.UseDefaultAdmin(
        adminUserName,
        adminPassword,
        "admin",
        new List<string> { "*" }))
    .UseDefaultAuthentication());
```

The initializer is idempotent. The abbreviated registration above uses
in-memory identity stores; for durable users and roles, use the EF identity
provider shown in the [server setup guide](../../application-types/elsa-server.md).
Rotate the bootstrap credentials after initial access. `UseDefaultAuthentication()` does not grant the security-root
permission to localhost by default; use an authenticated administrator for
deployed hosts. An isolated local host can opt in explicitly with
`EnableLocalHostPermissionGrantForSecurityRoot()`.

## Configure tokens, users, and roles

The configuration-based providers bind the `Identity` section. A minimal
shape is:

```json
{
  "Identity": {
    "Tokens": {
      "SigningKey": "set-outside-source-control",
      "Issuer": "https://elsa.example",
      "Audience": "https://elsa.example",
      "AccessTokenLifetime": "01:00:00",
      "RefreshTokenLifetime": "7.00:00:00"
    },
    "Roles": [
      {
        "Id": "workflow-viewer",
        "Name": "Workflow Viewer",
        "Permissions": [
          "read:workflow-definitions",
          "read:workflow-instances",
          "read:activity-execution"
        ]
      }
    ],
    "Users": [
      {
        "Id": "operator-1",
        "Name": "operator",
        "HashedPassword": "set-outside-source-control",
        "HashedPasswordSalt": "set-outside-source-control",
        "Roles": ["workflow-viewer"]
      }
    ]
  }
}
```

See the focused [example configuration](examples/appsettings-identity.json).
Do not commit signing keys, passwords, API keys, client secrets, or their
production source material.

Configuration-based providers are convenient for small or deployment-owned
sets of identities. Elsa also exposes store-based providers for applications
that manage users, applications, and roles through durable stores. Choose one
provider for each identity type and configure persistence appropriate to the
deployment.

## Configure Studio

Select Elsa Identity in the Studio host:

```json
{
  "Backend": {
    "Url": "https://elsa.example/elsa/api"
  },
  "Authentication": {
    "Provider": "ElsaIdentity"
  }
}
```

Both the Blazor Server and WebAssembly Studio hosts support Elsa Identity.
Studio signs in against the Elsa backend and sends the resulting bearer token
with API requests.

## Refresh an access token

When an access token expires, send the refresh token as a bearer token to
`POST /identity/refresh-token`:

```bash
curl -X POST https://elsa.example/elsa/api/identity/refresh-token \
  -H "Authorization: Bearer $REFRESH_TOKEN"
```

For a valid Elsa Identity refresh token, the endpoint returns `200 OK` with a
new access token and refresh token. The replacement refresh token continues
the same session, so revoking that session also revokes tokens issued by the
refresh operation. Replace the stored refresh token when the response
contains a new one.

### Refresh-token subject requirements

In Elsa 3.9.0, the refresh endpoint resolves the user by the stable user ID in
the token, not by the user's name. Every `sub` and inbound-mapped
`NameIdentifier` claim must contain the same non-blank ID. A missing, blank,
conflicting, or unknown subject is rejected with `401 Unauthorized`.

This prevents a still-valid token from following a deleted account to a new
account that happens to reuse its name. Clients should treat this `401` like
any other refresh failure: discard the unusable refresh token and require the
user or service to authenticate again. Do not interpret a successful response
with `isAuthenticated: false` as the normal refresh-failure signal for the
3.9.0 contract.

This behavior applies to Elsa Identity's local credential flow. It is separate
from External Authentication session and refresh-token handling, which uses
the provider connection and session configuration documented in that
authentication topology.

## Production guidance

- Store signing keys and credential material outside source control.
- Use HTTPS for Studio, Elsa Server, and every token exchange.
- Prefer short-lived access tokens and protect refresh tokens.
- Give roles named permissions; reserve `*` for tightly controlled
  administrators.
- Use durable providers and shared cryptographic configuration in scaled-out
  deployments.
- Rotate signing keys and credentials through a planned process that accounts
  for already issued tokens.

## Source references

This guide is validated against the advertised Core `release/3.9.0` branch at
commit `e606a53723370ba7ee80670e5f5c935535aa42d6`:

- [Refresh-token subject resolution](https://github.com/elsa-workflows/elsa-core/blob/e606a53723370ba7ee80670e5f5c935535aa42d6/src/modules/Elsa.Identity/Services/RefreshTokenSubject.cs)
- [Refresh-token endpoint](https://github.com/elsa-workflows/elsa-core/blob/e606a53723370ba7ee80670e5f5c935535aa42d6/src/modules/Elsa.Identity/Endpoints/RefreshToken/Endpoint.cs)
- [Refresh-token migration note](https://github.com/elsa-workflows/elsa-core/blob/e606a53723370ba7ee80670e5f5c935535aa42d6/doc/migrations/refresh-token-user-resolution.md)
- [Refresh-token endpoint tests](https://github.com/elsa-workflows/elsa-core/blob/e606a53723370ba7ee80670e5f5c935535aa42d6/test/unit/Elsa.Identity.UnitTests/Endpoints/RefreshTokenEndpointTests.cs)

## Related guides

- [API Keys](api-keys.md)
- [Elsa API Permissions](permissions.md)
- [Security & Hardening](../security/README.md)
- [Secrets Management](../security/secrets-management.md)
