---
description: >-
  Integrate a host-provided ASP.NET Core authentication scheme with Elsa API
  permissions.
---

# Custom Authentication

Elsa Server uses ASP.NET Core authentication. A host can therefore use a
custom or organization-standard authentication scheme instead of Elsa
Identity, provided the resulting principal is authenticated and carries the
permission claims required by the Elsa API.

Use this path when the host already owns authentication or when a protocol is
not covered by Elsa Identity, Direct OpenID Connect, or External
Authentication.

## Register the host scheme

Configure the scheme through standard ASP.NET Core services, add
authorization, and place the middleware before the Elsa API:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication("CompanyScheme")
    .AddScheme<AuthenticationSchemeOptions, CompanyAuthenticationHandler>(
        "CompanyScheme",
        _ => { });

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

The handler implementation, credential validation, challenge behavior, and
key management belong to the host application.

## Map Elsa permissions

Elsa API endpoints authorize against claims whose type is `permissions`.
Translate only trusted upstream roles, groups, or scopes into the named Elsa
permissions required by the caller.

Do not treat a successful login, a generic `Admin` role, or an arbitrary
external scope as full Elsa access. ASP.NET Core role policies can protect
custom host endpoints, but they do not replace Elsa endpoint permissions.

See [Elsa API Permissions](permissions.md).

## Configure Studio separately

Studio must obtain a credential that the custom server scheme accepts and send
it with every Elsa API request. Depending on the topology, that may require a
custom Studio authentication module or HTTP message handler.

If the provider is OpenID Connect, prefer the supported
[Direct OpenID Connect](direct-openid-connect.md) or
[External Authentication](external-authentication/README.md) modules before
building a custom integration.

## Keep workflow ingress separate

The `HttpEndpoint` activity uses its own `Authorize` and `Policy` settings.
Registering a custom Elsa API scheme does not automatically secure workflow
routes. See [HTTP Endpoint Security](../security/http-endpoint-security.md).

## Validation checklist

- Anonymous API requests receive the expected challenge or denial.
- Valid credentials create an authenticated principal.
- Missing Elsa permissions produce `403 Forbidden` responses.
- The principal receives only the permissions derived from trusted claims.
- Studio refresh, logout, and expired-credential behavior are tested.
- Logs and traces do not contain credentials or token contents.
