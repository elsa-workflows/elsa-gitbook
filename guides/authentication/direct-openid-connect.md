---
description: >-
  Release-backed guide to wiring Elsa Server and Elsa Studio to external OpenID
  Connect identity providers in Elsa 3.9.
---

# Direct OpenID Connect

This guide covers the identity-provider integration points that are actually
present in `release/3.9.0` across `elsa-core` and `elsa-studio`.

{% hint style="info" %}
**Choosing an authentication path**

[External Authentication](external-authentication/README.md) is the strategic
successor for new deployments: Elsa Server brokers one or more providers and
issues the credentials consumed by Studio. Direct Studio OIDC remains
supported in Elsa 3.9 and is not formally deprecated.
{% endhint %}

## Overview

Use direct OIDC when:

- Elsa Server should trust tokens issued by an external provider instead of
  Elsa's built-in identity system.
- Elsa Studio should sign users in with the same OpenID Connect provider and
  forward bearer tokens to Elsa Server.

This page is intentionally narrower than a generic identity-platform guide.
Elsa 3.9 ships first-class Studio support for OpenID Connect, and Elsa Server
authorizes API calls based on ASP.NET Core authentication plus Elsa-specific
`permissions` claims.

If Elsa should own the upstream authorization-code flow, connection lifecycle,
identity links, or Studio SSO administration, use
[External Authentication](external-authentication/README.md) instead. The
broker is an opt-in path distinct from direct bearer-token validation.

## What Elsa 3.9 Actually Expects

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
`release/3.9.0`, the claim type is literally `permissions`, and `*` grants all
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

In `release/3.9.0`:

- Blazor Server defaults to `Authentication:Provider = ElsaIdentity`
- Blazor WebAssembly defaults to `Authentication:Provider = OpenIdConnect`
- Blazor Server uses `/signin-oidc` and `/signout-callback-oidc` unless
  overridden
- Blazor WebAssembly uses `/authentication/login-callback` and
  `/authentication/logout-callback`
- Studio logout starts at `/authentication/logout`

The WebAssembly host uses the framework's default callback paths. In the 3.9.0
Studio wrapper, setting `CallbackPath` or `SignedOutCallbackPath` in the shared
OIDC options does not change those WebAssembly routes. Blazor Server does honor
those options.

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

This host-side pattern matches Elsa's 3.9.0 authorization contract for
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
                        identity.AddClaim(new Claim(PermissionNames.ClaimType, "workflows/definitions:view"));
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

Register these redirect URIs. Blazor Server supports custom callback paths via
`CallbackPath` and `SignedOutCallbackPath`.

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

Register these default redirect URIs generated from the WebAssembly app's base
URI. The Elsa 3.9.0 WebAssembly wrapper does not apply custom
`CallbackPath` or `SignedOutCallbackPath` values from the shared OIDC options.

- `https://studio.example.com/authentication/login-callback`
- `https://studio.example.com/authentication/logout-callback`

### Authentication Scopes vs Backend API Scopes

Keep the two scope lists separate:

- `AuthenticationScopes`: scopes needed for signing the user in
- `BackendApiScopes`: scopes needed on tokens sent to Elsa Server

This separation matters for providers such as Microsoft Entra ID, where a token
request must target one resource audience at a time.

## Return Paths and Sub-path Deployments

Studio returns users to the page they were viewing before OIDC sign-in. The
Blazor Server login and logout endpoints accept a `returnUrl`, but only keep a
safe local path; missing, absolute, protocol-relative, or otherwise unsafe
values fall back to `/`. Studio's login page resolves a base-relative route
against its base URI before starting authentication. A route that already
starts with `/` remains rooted at the site root; for example, `//evil.example`
falls back to `/`.

The OIDC authorization helper captures the current path and query, including
`PathBase`, as the return path for after sign-in. The host still has to route
the provider callback correctly. For example, a Studio page at
`https://studio.example/studio/workflows/definitions?tab=active` stores
`/studio/workflows/definitions?tab=active` as its return path. A base-relative
target `workflows/definitions?tab=active` under
`https://studio.example/studio/` also resolves to
`/studio/workflows/definitions?tab=active`; an already rooted
`/workflows/definitions` stays at `/workflows/definitions`.

If the host exposes `/studio` as its path base and the default callback paths
are in use, register these externally visible callback URLs with the identity
provider:

- Blazor Server sign-in: `https://studio.example/studio/signin-oidc`
- Blazor Server sign-out: `https://studio.example/studio/signout-callback-oidc`
- Blazor WebAssembly sign-in: `https://studio.example/studio/authentication/login-callback`
- Blazor WebAssembly sign-out: `https://studio.example/studio/authentication/logout-callback`

These URLs assume the host and reverse proxy preserve the `/studio` path base
when forwarding requests. The OIDC module does not configure that hosting or
proxy setup. If a callback returns 404, compare the provider's registered URL
with the actual external URL, including scheme, host, path base, and callback
path.

The return-path check prevents open redirects; it does not establish that every
part of Studio (static assets, backend API requests, and SignalR) is configured
for a reverse-proxy sub-path. See the
[existing-app hosting guide](../onboarding/hosting-elsa-in-existing-app.md)
for host integration.

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

Your identity-provider redirect URI does not match the Studio host model or
deployment path base:

- Blazor Server: `/signin-oidc`
- Blazor WebAssembly: `/authentication/login-callback`

For a path-based deployment, include the external prefix in the registered URI
(for example, `/studio/signin-oidc` for Blazor Server). Also verify that the
proxy forwards the path base to the Studio host.

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

- [Security & Hardening](../security/README.md)
- [Authentication & Authorization](README.md)
- [Studio Designer Integration](../studio/integration/README.md)
- [Blazor Dashboard Integration](../integration/blazor-dashboard.md)
- [External Authentication](external-authentication/README.md)

## Release source

This guide was checked against the latest released source branches,
`release/3.9.0`:

- [Core default authentication feature](https://github.com/elsa-workflows/elsa-core/blob/5d3582b6309a2fd8ea33ebdb8c39fb040bfdc514/src/modules/Elsa.Identity/Features/DefaultAuthenticationFeature.cs)
- [Core permission claim names](https://github.com/elsa-workflows/elsa-core/blob/5d3582b6309a2fd8ea33ebdb8c39fb040bfdc514/src/common/Elsa.Api.Common/PermissionNames.cs)
- [Studio OIDC server authentication setup](https://github.com/elsa-workflows/elsa-studio/blob/a30ed7c997dfb3cff1d5d095a4ee19ff03c7fe42/src/modules/Elsa.Studio.Authentication.OpenIdConnect.BlazorServer/Extensions/ServiceCollectionExtensions.cs)
- [Studio OIDC WebAssembly authentication setup](https://github.com/elsa-workflows/elsa-studio/blob/a30ed7c997dfb3cff1d5d095a4ee19ff03c7fe42/src/modules/Elsa.Studio.Authentication.OpenIdConnect.BlazorWasm/Extensions/ServiceCollectionExtensions.cs)
- [Studio Server login and logout redirects](https://github.com/elsa-workflows/elsa-studio/blob/a30ed7c997dfb3cff1d5d095a4ee19ff03c7fe42/src/modules/Elsa.Studio.Authentication.OpenIdConnect.BlazorServer/Controllers/AuthenticationController.cs)
- [Safe local return paths](https://github.com/elsa-workflows/elsa-studio/blob/a30ed7c997dfb3cff1d5d095a4ee19ff03c7fe42/src/modules/Elsa.Studio.Authentication.Abstractions/LocalReturnPath.cs)
- [OIDC sign-in and return-path handling](https://github.com/elsa-workflows/elsa-studio/blob/a30ed7c997dfb3cff1d5d095a4ee19ff03c7fe42/src/modules/Elsa.Studio.Login/Services/OpenIdConnectAuthorizationService.cs)
- [Base-relative path and path-base tests](https://github.com/elsa-workflows/elsa-studio/blob/a30ed7c997dfb3cff1d5d095a4ee19ff03c7fe42/src/modules/Elsa.Studio.ExternalAuthentication.Tests/Login/LocalReturnPathTests.cs)
- [Return-path behavior tests](https://github.com/elsa-workflows/elsa-studio/blob/a30ed7c997dfb3cff1d5d095a4ee19ff03c7fe42/src/modules/Elsa.Studio.ExternalAuthentication.Tests/Compatibility/DirectOpenIdConnectLoginTests.cs)
