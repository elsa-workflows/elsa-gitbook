---
description: >-
  Move Elsa Studio from direct OpenID Connect to the Elsa 3.8 preview External
  Authentication broker, with a staged rollout and rollback plan.
---

# Migrate from Direct OpenID Connect

> **Preview feature:** Brokered External Authentication is new in Elsa 3.8 preview and is currently available from Feedz while under active development. Plan this as a reversible, staged migration; do not remove the existing direct OpenID Connect registration until the brokered flow is proven in your environment.

Direct OpenID Connect remains a supported Studio authentication mode. External Authentication is an alternative architecture: Elsa Server becomes the relying party for the upstream provider, then issues Elsa credentials that Studio consumes.

This gives Elsa a central connection-management, identity-link, session-administration, and permission-mapping surface. It also changes which application owns the provider client registration and callback URL.

## Choose exactly one Studio authentication mode

Each Studio host must set `Authentication:Provider` to one mode:

- `OpenIdConnect` for the existing direct provider integration.
- `ExternalAuthentication` for brokered login through Elsa Server.
- `ElsaIdentity` for direct Elsa local-credential integration.

Do not register direct OpenID Connect and brokered External Authentication in the same Studio host. The configuration is intentionally rejected when startup would be ambiguous.

## What changes

| Direct Studio OIDC setting | Brokered External Authentication destination |
| --- | --- |
| `Authentication:OpenIdConnect:Authority` or metadata address | Configuration-owned or database-owned Elsa connection `adapterSettings.discoveryUrl` and discovery mode. |
| Studio `ClientId` for the upstream provider | Connection `adapterSettings.clientId`: Elsa Server's upstream provider registration. |
| Studio upstream `ClientSecret` | Connection `secretBindings.clientSecret`, resolved only by Elsa Server. |
| `AuthenticationScopes` | Connection `adapterSettings.scopes`. |
| Provider callback path | Elsa's fixed derived provider callback, based on the connection key. |
| Studio signed-out callback | Elsa upstream logout callback when upstream logout is enabled. |
| Name/role claim configuration | Claim projection plus explicit identity-link/unlinked policy and permission-grant mapping. |
| Backend API scopes | No direct equivalent. Studio calls Elsa with Elsa-issued credentials after broker code exchange. |

There are now **two** client registrations:

1. The **upstream provider client**, owned by the Elsa connection. It is normally confidential and belongs to Elsa Server.
2. The **Elsa Authentication Client**, owned by the deployment. It identifies Studio to the broker, owns exact Studio callback/logout/origin/return-path registrations, and grants no Elsa permissions by itself.

Never copy either client secret through Studio UI or APIs. Configure the same Studio confidential-client secret separately in Elsa Server and the Studio Server host through their respective secret stores.

## Staged migration

1. Keep Studio in `Authentication:Provider: OpenIdConnect`. Do not change or delete its current configuration.
2. Install and configure `Elsa.ExternalAuthentication` plus `Elsa.ExternalAuthentication.OpenIdConnect` on Elsa Server. For production, add one dedicated `Elsa.ExternalAuthentication.Persistence.EFCore.<Provider>` package and feature.
3. Configure Elsa Identity token signing, the deployment callback base URI, a configuration-owned OIDC connection, and an Authentication Client for the Studio host. See [Installation](installation.md) and [Configuration](configuration.md).
4. Register Elsa's derived normal callback with the upstream provider. Keep Studio's existing direct callback registered during the migration.
5. Resolve secrets independently in Elsa Server and Studio Server. The migration must never move or reveal a secret.
6. Validate and test the connection, then run an administrator preview and a normal brokered login in a non-production environment.
7. Change only `Authentication:Provider` to `ExternalAuthentication` in the Studio host, configure the broker client options, and restart the host.
8. Verify sign-in, token refresh, logout, session revocation, user/role mapping, and the break-glass/recovery path.
9. Only after a successful observation period, retire the direct provider callback and rotate/delete direct-only secrets according to your own change-control process.

## Studio Server configuration

Studio Server is a confidential Authentication Client. It exchanges the broker completion code on the server, keeps Elsa access and refresh credentials on the server, and gives the browser a secure HTTP-only Studio session cookie.

```json
{
  "Authentication": {
    "Provider": "ExternalAuthentication",
    "ExternalAuthentication": {
      "ClientId": "elsa-studio-server",
      "ClientSecret": "<read-from-server-secret-provider>",
      "CallbackPath": "/authentication/external/callback",
      "LogoutCallbackPath": "/authentication/external/logout-callback"
    }
  }
}
```

Register the Studio host integration:

```csharp
builder.Services.AddExternalAuthenticationBroker(options =>
    builder.Configuration.GetSection("Authentication:ExternalAuthentication").Bind(options));

builder.Services.AddExternalAuthenticationModule(backendApiConfig);
```

The `ClientId`, callback path, and logout callback path must exactly match the Authentication Client registered in Elsa Server. Supply `ClientSecret` through deployment configuration, for example `Authentication__ExternalAuthentication__ClientSecret`; never place it in a browser-delivered configuration file.

## Studio WebAssembly configuration

Studio WebAssembly is a public Authentication Client. It has no secret and always uses S256 PKCE. Register its exact browser origin in the Elsa Authentication Client.

```json
{
  "Authentication": {
    "Provider": "ExternalAuthentication",
    "ExternalAuthentication": {
      "ClientId": "elsa-studio-wasm",
      "CallbackPath": "/authentication/external/callback",
      "LogoutCallbackPath": "/authentication/external/logout-callback",
      "BrowserStorage": "Memory"
    }
  }
}
```

`Memory` is the secure default and requires a new sign-in after browser reload. `Session` and `Durable` storage are explicit deployment choices that retain credentials in browser-accessible storage and should produce a visible security warning. Never add a client secret to a WASM configuration file.

## Callback registration

Studio's callback is where Elsa sends an opaque completion code after successful broker authentication. The **upstream provider** must instead redirect to Elsa Server:

```text
https://elsa.example.com/elsa/api/external-authentication/callback/<connection-key>
```

If previews are enabled for administrators, register the additional preview callback:

```text
https://elsa.example.com/elsa/api/external-authentication/previews/callback/<connection-id>
```

Do not substitute `/signin-oidc`, `/authentication/login-callback`, or the Studio broker callback as the provider callback. Elsa validates the provider response and redirects Studio only with an opaque code and client state; provider and Elsa tokens do not appear in the URL.

## Compatibility and rollback

The direct `Authentication:OpenIdConnect` section is not rewritten or consumed by brokered configuration. Keeping it intact is the compatibility guarantee that makes rollback simple.

To roll back a Studio Server or WASM host:

1. Restore `Authentication:Provider` to `OpenIdConnect`.
2. Restart the Studio host.
3. Confirm Studio is using its retained direct client registration and callback.

The Elsa broker configuration, provider callback, and secrets can remain in place while you investigate. Do not remove them or revoke the direct client until you decide to complete the migration. A broker rollback does not require moving secrets back because the staged migration kept them separate.

## Verification checklist

- The login-method endpoint lists the expected connection without disclosing provider URL, adapter settings, client ID, remote icon URL, health details, or secrets.
- Selecting the connection redirects first to the provider and returns only to Elsa's connection-key callback.
- Elsa redirects Studio with a one-time completion code; replay fails.
- A confidential Studio Server exchanges the code server-side; a WASM client exchanges it with PKCE and no secret.
- The Elsa access token contains permissions produced by Elsa roles or explicit configured grant sources—not unbounded upstream claims.
- Disabling a connection blocks new initiation, pending callback, and external refresh. Existing Elsa access tokens follow their configured expiry.
- Local logout works. If upstream logout is enabled, confirm the provider callback returns only to the exact registered Studio logout callback.
- Restoring `OpenIdConnect` and restarting Studio successfully returns to direct authentication before you retire the old registration.
