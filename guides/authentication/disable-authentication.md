---
description: >-
  Disable Elsa API and Studio authorization for isolated development and test
  environments only.
---

# Disable Authentication in Development

Disabling authentication is useful for local learning, prototypes, and
isolated automated tests. It must never be used for a shared, staging, or
production environment.

{% hint style="danger" %}
Disabling security exposes Elsa management APIs to every caller that can reach
the host. Keep the host on a trusted local interface and restore
authentication before deploying it anywhere else.
{% endhint %}

## Disable Elsa API endpoint security

Call `EndpointSecurityOptions.DisableSecurity()` before mapping the Elsa API.
Guard it with the host environment so the setting cannot silently reach
production:

```csharp
using Elsa;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
    EndpointSecurityOptions.DisableSecurity();

builder.Services.AddElsa(elsa =>
{
    elsa
        .UseWorkflowManagement()
        .UseWorkflowRuntime()
        .UseWorkflowsApi();
});

var app = builder.Build();
app.UseWorkflowsApi();
app.Run();
```

`DisableSecurity()` changes the process-wide Elsa API endpoint-security
setting. Do not expose a runtime switch that allows a remote caller or a
production configuration mistake to toggle it.

## Disable Studio authorization

For a Studio host used only with the unsecured development API, disable the
Studio shell's authorization checks:

```csharp
builder.Services.AddShell(options =>
    options.DisableAuthorization = builder.Environment.IsDevelopment());
```

This affects Studio's client-side authorization behavior. It does not disable
security on Elsa Server, so configure both hosts consistently for the isolated
development environment.

## What this does not disable

- A workflow route exposed by `HttpEndpoint` still follows that activity's
  `Authorize` and `Policy` settings.
- A reverse proxy, ingress controller, or host-level authorization policy can
  still reject requests.
- CORS remains a browser-enforced cross-origin control, not an authentication
  mechanism.

## Before deployment

1. Remove or disable the development-only branch.
2. Configure [Elsa Identity](elsa-identity.md),
   [Direct OpenID Connect](direct-openid-connect.md),
   [External Authentication](external-authentication/README.md), or a
   [custom scheme](custom-authentication.md).
3. Verify anonymous calls to protected `/elsa/api/*` routes are rejected.
4. Verify Studio receives only the permissions assigned to its signed-in user.
5. Complete the [production hardening](../security/production-hardening.md)
   checklist.
