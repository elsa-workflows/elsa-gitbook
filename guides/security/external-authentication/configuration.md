---
description: >-
  Configure Elsa External Authentication connections, clients, callback URLs,
  secret bindings, identity policy, and permission mapping.
---

# Configure External Authentication

> **Elsa 3.8 preview:** This guide documents the preview External Authentication modules distributed from Feedz. The feature is under development; validate every preview upgrade before promoting it.

External Authentication has two configuration boundaries:

- **Deployment-owned configuration** defines Authentication Clients, configuration-owned connections, adapter and extension allowlists, callback base URL, provider egress policy, permission boundaries, and recovery policy.
- **Database-owned connections** are optional records managed through the APIs and Studio. They let authorized administrators create, test, preview, enable, disable, archive, and restore identity-provider connections without changing deployment configuration.

Configuration is read from `ExternalAuthentication`. In a CShells feature block, the same settings are nested below `Features:ExternalAuthentication`.

## Minimal OpenID Connect configuration

The following sample configures a confidential Elsa Studio Server client and a configuration-owned OIDC connection. Values that are secrets remain references, never literal values.

```json
{
  "ExternalAuthentication": {
    "Redirects": {
      "ExternalCallbackBaseUri": "https://elsa.example.com/elsa/api/"
    },
    "AuthenticationClients": [
      {
        "clientId": "elsa-studio-server",
        "displayName": "Elsa Studio Server",
        "clientType": "confidential",
        "callbackUris": [
          "https://studio.example.com/authentication/external/callback"
        ],
        "logoutCallbackUris": [
          "https://studio.example.com/authentication/external/logout-callback"
        ],
        "allowedReturnPathPrefixes": ["/"],
        "secretBinding": {
          "ownership": "external",
          "resolverType": "configuration",
          "reference": "Secrets:ExternalAuthentication:StudioServerClientSecret"
        },
        "isEnabled": true
      }
    ],
    "Connections": [
      {
        "id": "contoso-workforce-configuration",
        "key": "contoso-workforce",
        "adapterType": "openid-connect",
        "adapterSettingsVersion": 2,
        "adapterSettings": {
          "mode": "discovery",
          "discoveryUrl": "https://login.example.com/.well-known/openid-configuration",
          "clientId": "elsa-server-at-contoso",
          "clientAuthenticationMethod": "client_secret_basic",
          "scopes": ["profile", "email", "groups"]
        },
        "secretBindings": {
          "clientSecret": {
            "ownership": "external",
            "resolverType": "configuration",
            "reference": "Secrets:ExternalAuthentication:ContosoProviderClientSecret",
            "expectedType": "text",
            "expectedScope": "external-authentication"
          }
        },
        "displayName": "Contoso Workforce",
        "iconId": "building",
        "displayOrder": 10,
        "isPreferred": true,
        "isEnabled": true,
        "unlinkedPolicy": {
          "type": "reject",
          "settingsVersion": 1,
          "settings": {}
        },
        "claimProjection": {
          "allowedClaimTypes": ["name", "email", "groups"],
          "redactedClaimTypes": ["email"],
          "maximumClaimCount": 50,
          "maximumValueLength": 2048,
          "maximumTotalBytes": 32768
        },
        "upstreamLogoutMode": "disabled"
      }
    ]
  }
}
```

For standard .NET configuration, set the actual values outside source control, for example:

```text
Secrets__ExternalAuthentication__StudioServerClientSecret=<strong-random-client-secret>
Secrets__ExternalAuthentication__ContosoProviderClientSecret=<provider-client-secret>
```

The configuration resolver reads the key named by `reference`. It exposes only configured/resolvable status to management clients, not secret values. It can obtain values from environment variables, mounted configuration, Key Vault-style providers, or any other standard `IConfiguration` provider.

## Authentication Clients

An Authentication Client identifies Studio (or another client application) to the Elsa broker. It is **not** the upstream OIDC client registration: that upstream registration belongs in the connection's `adapterSettings` and `secretBindings`.

Each client has exact, deployment-controlled registrations:

- `clientId` and `displayName`.
- `clientType`: `confidential` for a server-side client or `public` for a browser/WASM client.
- `callbackUris`: exact broker completion-code destinations.
- `logoutCallbackUris`: exact post-logout destinations.
- `allowedReturnPathPrefixes`: safe application-relative paths such as `/` or `/workflows`.
- `allowedOrigins`: required for public/WASM clients, for example `https://studio.example.com`.
- A `secretBinding` for confidential clients. Public clients never contain a secret.
- `isEnabled`.

For a WebAssembly Studio client, use a public registration and register its exact browser origin:

```json
{
  "clientId": "elsa-studio-wasm",
  "displayName": "Elsa Studio WebAssembly",
  "clientType": "public",
  "callbackUris": ["https://studio.example.com/authentication/external/callback"],
  "logoutCallbackUris": ["https://studio.example.com/authentication/external/logout-callback"],
  "allowedOrigins": ["https://studio.example.com"],
  "allowedReturnPathPrefixes": ["/"],
  "isEnabled": true
}
```

The broker always requires S256 PKCE. A confidential client also authenticates when exchanging the broker completion code; a public client uses PKCE and must never hold a client secret.

## Callbacks and provider registration

`Redirects:ExternalCallbackBaseUri` is the public, deployment-owned base URI from which Elsa derives upstream provider callbacks. It must be an absolute HTTPS URL in production. Do not try to configure callbacks per connection.

For the example above, register these exact URLs at the upstream provider:

```text
https://elsa.example.com/elsa/api/external-authentication/callback/contoso-workforce
https://elsa.example.com/elsa/api/external-authentication/previews/callback/contoso-workforce-configuration
```

The normal callback uses the immutable logical connection **key**. The preview callback uses the stable connection **ID**. Register the preview callback only when administrators will use connection preview. Elsa's callback routes, correlation state, S256 PKCE, and validation cannot be overridden by a connection.

## OIDC adapter settings

The `openid-connect` adapter uses an authorization-code flow. Its safe default is discovery mode:

- `mode`: `discovery` or `manual`; use `discovery` whenever possible.
- `discoveryUrl`: exact HTTPS discovery-document URL in discovery mode.
- `clientId`: the Elsa Server registration at the upstream provider.
- `clientAuthenticationMethod`: `client_secret_basic` (default) or `client_secret_post`.
- `scopes`: optional requested scopes. `openid` is always included.
- `clientSecret`: a required secret binding field, never a value inside `adapterSettings`.
- `endSessionEndpoint`: optional explicit upstream logout endpoint.

Manual trust additionally requires `issuer`, `authorizationEndpoint`, and `tokenEndpoint`, plus either `jwksUri` or pinned `signingKeys`. Treat it as an exception: explicit trust overrides require both deployment allowance and the `external-authentication:provider-trust:unsafe` permission, plus an explicit confirmation when saved. Discovery-derived issuer, endpoints, and signing keys are the recommended configuration.

The adapter validates the authorization response and ID token, including correlation state, issuer, signature, audience/authorized party, expiry, nonce, and S256 PKCE. It projects only allowlisted claims and does not return provider tokens through broker or management APIs.

## Identity resolution, claims, and permissions

External claims do not automatically become Elsa permissions.

### Unlinked identities

The default unlinked identity policy is `reject`: an identity that has no link to an Elsa user cannot sign in. This is the safest starting point.

`create-user` can provision an Elsa user on first sign-in. For example, `defaultRoleIds` assigns existing Elsa roles only when a new user is created:

```json
{
  "type": "create-user",
  "settingsVersion": 1,
  "settings": {
    "defaultRoleIds": ["workflow-user"]
  }
}
```

The deployment controls which policies are selectable with `AllowedUnlinkedIdentityPolicyTypes`; it can also prevent database-owned connections from overriding `UnlinkedIdentityPolicy:DefaultType`. A matcher-based policy can select a deployed external-user matcher, but matchers do not assign roles or permissions.

### Claim projection and grant sources

`claimProjection` is an allowlist and size boundary for the claims retained after upstream validation. Configure only the claims required by identity matching or permission mapping. Use `redactedClaimTypes` to prevent sensitive values from appearing in management/preview output.

The built-in grant-source allowlist contains `elsa-roles`, `claim-mapping`, `group-mapping`, and `claim-pass-through`. `AllowedPermissionGrantSourceTypes` controls which deployed sources a connection may select. `PermissionGrants:AllowedPermissions` and `PermissionGrants:DeniedPermissions` are deployment-wide final boundaries, applied after grant sources calculate candidate permissions.

Keep `claim-pass-through` tightly bounded. Explicit mappings and Elsa roles are easier to audit than passing provider claim values through as Elsa permission names.

## Connection ownership and overrides

`Connections` creates configuration-owned, immutable connections. They are ideal when operators deploy a known identity provider configuration and want it reviewed through source-controlled deployment configuration.

When `EnableDatabaseConnections` is `true` (the default), authorized administrators can create database-owned connections through Studio or the management API. The connection source rules are:

- A configuration-owned connection takes precedence when it has the same effective key and scope as a database connection.
- Studio marks the database connection as **shadowed**; it does not silently overwrite configuration.
- With `AllowConfigurationConnectionOverrides: true`, an administrator can create or promote a complete database-owned override. The override preserves its own lifecycle and secret bindings.
- A disabled override still shadows the configuration baseline. Archiving the override reveals the configuration connection; restoring it resumes shadowing in a disabled state.
- The final-login-path guard rejects a change that would remove the last normal sign-in option without an approved recovery method or privileged override.

Configuration-owned rows are inspect-only. Database-owned mutations use optimistic concurrency: read the ETag and send it as `If-Match` when updating, enabling, disabling, archiving, restoring, or changing secret bindings.

## Operational configuration

The broker has secure defaults that are suitable for most deployments:

- Local Elsa username/password login is enabled as a normal chooser option; set `LocalLogin:IsEnabled` to `false` to remove it.
- Provider HTTPS is required, private/loopback/link-local destinations are denied, and redirects are revalidated (maximum three by default).
- Broker transactions last 10 minutes; completion codes last one minute; preview state lasts 10 minutes; external sessions are bounded to eight hours.
- Anonymous discovery/initiation/callback/token operations use named rate-limit policies.
- Upstream logout is disabled unless a connection enables it.
- Session administration is enabled. The ASP.NET Core health-check bridge is opt-in via `AddExternalAuthenticationHealthCheck()` and is tagged `external-authentication` and `optional`.

Tune `ProviderEgress`, `Lifetimes`, `RateLimits`, `Logout`, `FinalLoginPathGuard`, and `Operations` only with a clear operational requirement. See [Production](production.md) for clustered hosts, key sharing, egress restrictions, and safe secret rotation.
