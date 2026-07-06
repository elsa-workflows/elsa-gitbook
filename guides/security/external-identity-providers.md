---
description: >-
  Release-backed guide to wiring Elsa Server and Elsa Studio to external OpenID
  Connect identity providers in Elsa 3.8.
---

# External Identity Providers

This guide covers the identity-provider integration points that are actually
present in `release/3.8.0` across `elsa-core` and `elsa-studio`.

Use it when:

- Elsa Server should trust tokens issued by an external provider instead of
  Elsa's built-in identity system.
- Elsa Studio should sign users in with the same OpenID Connect provider and
  forward bearer tokens to Elsa Server.

This page is intentionally narrower than a generic identity-platform guide.
Elsa 3.8 ships first-class Studio support for OpenID Connect, and Elsa Server
authorizes API calls based on ASP.NET Core authentication plus Elsa-specific
`permissions` claims.

## What Elsa 3.8 Actually Expects

### Elsa Server

In the standalone `Elsa.Server.Web` host, the built-in path is:

```csharp
elsa
    .UseIdentity(...)
    .UseDefaultAuthentication();
```

That helper configures JWT bearer validation from Elsa `IdentityTokenOptions`
and also adds API-key support. It is the correct choice when Elsa itself issues
the JWTs or API keys.

For external identity providers, Elsa does not ship a provider-specific server
module. Your host application is responsible for:

- configuring ASP.NET Core authentication and authorization
- validating the external bearer tokens
- mapping external roles, groups, or scopes into Elsa `permissions` claims

Elsa API endpoints then authorize against those `permissions` claims. In
`release/3.8.0`, the claim type is literally `permissions`, and `*` grants all
Elsa permissions.

### Elsa Studio

Elsa Studio ships first-class OpenID Connect support for both default hosts:

- `Elsa.Studio.Host.Server`
- `Elsa.Studio.Host.Wasm`

Both hosts read:

```json
{
  "Authentication": {
    "Provider": "OpenIdConnect",
    "OpenIdConnect": {
      "Authority": "https://your-idp",
      "ClientId": "your-client-id",
      "AuthenticationScopes": ["openid", "profile", "offline_access"],
      "BackendApiScopes": ["api://your-api/elsa-server-api"]
    }
  }
}
```

In `release/3.8.0`:

- Blazor Server defaults to `Authentication:Provider = ElsaIdentity`
- Blazor WebAssembly defaults to `Authentication:Provider = OpenIdConnect`
- Blazor Server uses `/signin-oidc` and `/signout-callback-oidc` unless
  overridden
- Blazor WebAssembly uses `/authentication/login-callback` and
  `/authentication/logout-callback`
- Studio logout starts at `/authentication/logout`

## Recommended Topology

Use a single OpenID Connect provider for both Studio and Server when you want
SSO and centralized authorization:

1. Register an API or resource for Elsa Server in your identity provider.
2. Configure Elsa Server to validate bearer tokens for that audience.
3. Map the provider's roles, groups, or scopes to Elsa `permissions` claims.
4. Configure Elsa Studio `Authentication:OpenIdConnect` to request sign-in
   scopes plus the backend API scope.

If you only need machine-to-machine access, you can stop at step 2 and issue
tokens directly to service clients without using Studio.

## Server Setup Pattern

This host-side pattern matches Elsa's 3.8.0 authorization contract for
external bearer tokens:

```csharp
using System.Security.Claims;
using Elsa;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Oidc:Authority"];
        options.Audience = builder.Configuration["Oidc:Audience"];
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "role",
            ValidateIssuer = true,
            ValidateAudience = true
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var identity = (ClaimsIdentity)context.Principal!.Identity!;

                // Map provider-specific claims into Elsa permissions.
                foreach (var scope in context.Principal.FindAll("scope").Select(x => x.Value))
                {
                    if (scope == "elsa.admin")
                        identity.AddClaim(new Claim(PermissionNames.ClaimType, PermissionNames.All));
                }

                foreach (var role in context.Principal.FindAll("role").Select(x => x.Value))
                {
                    if (role == "elsa-operator")
                        identity.AddClaim(new Claim(PermissionNames.ClaimType, "read:workflow-definitions"));
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddElsa(elsa =>
{
    elsa
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

### Why the `permissions` Claim Matters

Elsa endpoint permissions are not expressed as ASP.NET Core policies. They are
checked as permission claims on the authenticated principal.

That means your external identity provider integration is only complete when one
of these is true:

- the provider issues `permissions` claims with Elsa permission values
- your ASP.NET Core host maps other claims into `permissions`

## Studio Setup Pattern

### Blazor Server Studio

Use a confidential client when the provider requires a client secret:

```json
{
  "Backend": {
    "Url": "https://elsa.example.com/elsa/api"
  },
  "Authentication": {
    "Provider": "OpenIdConnect",
    "OpenIdConnect": {
      "Authority": "https://login.example.com/realms/acme",
      "ClientId": "elsa-studio-server",
      "ClientSecret": "set-via-secret-store",
      "AuthenticationScopes": ["openid", "profile", "offline_access"],
      "BackendApiScopes": ["elsa-api"],
      "SaveTokens": true
    }
  }
}
```

Register these redirect URIs unless you override the defaults:

- `https://studio.example.com/signin-oidc`
- `https://studio.example.com/signout-callback-oidc`

### Blazor WebAssembly Studio

Use a public SPA client and do not configure a client secret:

```json
{
  "Backend": {
    "Url": "https://elsa.example.com/elsa/api"
  },
  "Authentication": {
    "Provider": "OpenIdConnect",
    "OpenIdConnect": {
      "Authority": "https://login.example.com/realms/acme",
      "ClientId": "elsa-studio-wasm",
      "AuthenticationScopes": ["openid", "profile", "offline_access"],
      "BackendApiScopes": ["elsa-api"]
    }
  }
}
```

Register these redirect URIs unless you override the defaults:

- `https://studio.example.com/authentication/login-callback`
- `https://studio.example.com/authentication/logout-callback`

### Authentication Scopes vs Backend API Scopes

Keep the two scope lists separate:

- `AuthenticationScopes`: scopes needed for signing the user in
- `BackendApiScopes`: scopes needed on tokens sent to Elsa Server

This separation matters for providers such as Microsoft Entra ID, where a token
request must target one resource audience at a time.

## Provider Notes

### Microsoft Entra ID

- Prefer a tenant-specific authority such as
  `https://login.microsoftonline.com/{tenant-id}/v2.0`
- Register Studio WASM as a SPA/public client
- Register Studio Server as a confidential web app if you need a client secret
- Put the Elsa API scope in `BackendApiScopes`
- Leave `GetClaimsFromUserInfoEndpoint` disabled unless your app registration
  explicitly supports `userinfo`

### Auth0

- Set `Authority` to your tenant URL such as `https://acme.us.auth0.com/`
- Define an API for Elsa Server and request that audience or scope from Studio
- If Auth0 already emits a `permissions` array, map those values directly to
  Elsa permissions where possible

### Keycloak, Okta, OpenIddict, IdentityServer, Generic OIDC

- Use discovery-based OpenID Connect metadata through `Authority`
- Use authorization code flow for Studio
- Use PKCE for public/browser clients
- Make sure the API access token audience matches Elsa Server
- Add explicit mappers if your provider emits roles or groups but not Elsa
  `permissions` claims

## Troubleshooting

### Studio signs in, but Elsa API calls return 401

Check these first:

- `Backend:Url` points to the actual Elsa API base URL
- the token audience matches the Elsa API registration
- the token presented to Elsa Server contains `permissions` claims, or your host
  maps other claims into `permissions`
- `app.UseAuthentication()` runs before `app.UseAuthorization()`

### Login callback returns 404

Your identity-provider redirect URI does not match the Studio host model:

- Blazor Server: `/signin-oidc`
- Blazor WebAssembly: `/authentication/login-callback`

### User is authenticated, but actions are still forbidden

The common cause is missing Elsa permission claims. Inspect the final
authenticated principal on the server and verify claim type `permissions`
contains either:

- the specific permission required by the endpoint
- `*` for full access

### OIDC `userinfo` calls fail with 401

The shipped Studio hosts already default `GetClaimsFromUserInfoEndpoint` to
`false`. Keep it that way unless your provider specifically requires and allows
that extra call.

## Related Guides

- [Security & Authentication Guide](README.md)
- [Authentication & Authorization Guide](../authentication.md)
- [Studio Designer Integration](../studio/integration/README.md)
- [Blazor Dashboard Integration](../integration/blazor-dashboard.md)
