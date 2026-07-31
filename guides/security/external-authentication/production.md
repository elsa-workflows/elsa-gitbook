---
description: >-
  Operate Elsa External Authentication safely in production, including durable
  state, clustered deployments, migrations, key management, and verification.
---

# Run External Authentication in Production

> **Preview feature:** Elsa External Authentication is a 3.8 preview capability distributed through Feedz and still under development. Treat it as a controlled rollout: pin a tested preview version, rehearse upgrade and rollback, and retest sign-in, refresh, logout, and recovery paths after every update.

The default broker registration uses in-memory state. That makes setup convenient, but it is a development-only choice: a restart loses active transactions and sessions, and nodes do not share broker state. Production requires durable External Authentication persistence, shared cryptographic material, and an intentional recovery path.

## Production baseline

Before enabling external login for users, confirm all of the following:

- The selected `Elsa.ExternalAuthentication.Persistence.EFCore.<Provider>` package is installed and its External Authentication persistence feature is enabled.
- The `ExternalAuthenticationElsaDbContext` migrations have been applied by your controlled database deployment process.
- Every node uses the same database (or a shared database topology), the same External Authentication handle-hashing key, and a shared ASP.NET Core Data Protection key ring.
- `Redirects:ExternalCallbackBaseUri` is the externally reachable HTTPS Elsa API URL, including the API prefix when applicable.
- The upstream provider has the exact Elsa-derived callback URLs registered.
- Authentication Client callbacks, logout callbacks, origins, and return-path prefixes are exact registrations rather than broad wildcards.
- Elsa Identity signing material and every client/provider secret come from secure deployment configuration, never committed JSON.
- A normal local login, a separate break-glass mechanism, or a deliberate privileged recovery procedure remains available before changing login methods.

## Durable state and EF Core persistence

Enable one provider-specific persistence feature in addition to Identity persistence. The SQLite example uses the same physical database as Identity, but the contexts retain separate migration histories:

```json
{
  "CShells": {
    "Shells": {
      "Default": {
        "Features": {
          "SqliteIdentityPersistence": {
            "ConnectionString": "Data Source=/var/lib/elsa/elsa.db;Cache=Shared"
          },
          "SqliteExternalAuthenticationPersistence": {
            "ConnectionString": "Data Source=/var/lib/elsa/elsa.db;Cache=Shared"
          }
        }
      }
    }
  }
}
```

Use the matching feature for SQL Server, PostgreSQL, MySQL, or Oracle. The External Authentication schema is held in its own `ExternalAuthenticationElsaDbContext`; it can use the same database as Identity or a different one.

The durable feature replaces the in-memory implementations for:

- Database-owned connections and their revisions.
- External identity links.
- Broker transactions and opaque authorization grants.
- External Authentication sessions and rotating refresh-token state.
- Preview results, connection-test observations, and connection-registry versions.

> **Important:** Identity persistence does not imply External Authentication persistence. In 3.8 preview, omitting the dedicated feature silently leaves the broker on in-memory stores. A single node may appear to work until it restarts; multiple nodes have inconsistent sessions, transactions, grants, and registry versions.

Apply the provider's `Initial` migration before enabling traffic. The preview persistence packages use their own migration history. If you are upgrading within 3.8 preview, review the External Authentication persistence migration notes and test against a copy of your database; do not assume the Identity context migration history covers the broker tables.

## Cluster requirements

Every node in a cluster must share these values:

### Handle-hashing key

Set a base64-encoded, random value containing at least 32 bytes of entropy:

```json
{
  "ExternalAuthentication": {
    "HandleHashing": {
      "SharedKeyBase64": "<base64-encoded-32-or-more-byte-random-value>"
    }
  }
}
```

Provide it through a secret provider, not a repository. The key produces non-reversible hashes for opaque handles, external subjects, and secret-generation fingerprints. All nodes using shared persistence must use the same value.

Rotating this key invalidates outstanding broker transactions and changes persisted external-subject hashes. Plan it like an identity migration: stop or drain sign-in traffic, understand the effect on identity links, deploy atomically, and communicate the expected reauthentication impact.

### ASP.NET Core Data Protection

Share the ASP.NET Core Data Protection key ring across every node and persist it outside ephemeral containers. External Authentication protects adapter state and optional upstream logout hints with Data Protection. A node that cannot unprotect state created by another node will fail callbacks or logout continuation.

Use the key storage and protection method appropriate to your platform (for example a protected shared volume, database-backed provider, or managed key store). Keep the application name consistent across nodes that participate in the same broker deployment.

### Public routing

Every Elsa Server broker node must serve the same public base address and API
route prefix. Configure load balancing so an upstream callback may reach any
healthy broker node; correct shared persistence and Data Protection remove the
need for broker session affinity. Ensure reverse proxies forward the public
scheme and host correctly, and test the actual externally visible callback URL.
Studio Server scale-out has a separate requirement described below.

### Blazor Server Studio scale-out

Broker persistence applies to Elsa Server, not to the Studio host's own login
cookie. In this preview, the Blazor Server package stores authentication tickets
in a node-local `IMemoryCache` ticket store. A scaled-out Studio Server
deployment must therefore use session affinity, or replace the configured
ASP.NET Core `ITicketStore` with a shared implementation. Share the Studio
host's Data Protection key ring in either case. WebAssembly Studio does not use
this server-side ticket store.

## Secret management and rotation

External Authentication distinguishes two binding ownership models:

- `ownership: external` with `resolverType: configuration` points to a key in deployment configuration. The value is managed by the platform, never by Elsa Studio.
- `ownership: managed` with `resolverType: elsa-secrets` points to an active Elsa Secret by name. It requires `Elsa.ExternalAuthentication.Secrets` and Elsa Secrets.

In both models, management responses reveal only whether a binding is configured and resolvable. They never return the value or its generation fingerprint.

Use separate secrets for the upstream provider client and each confidential Authentication Client. Do not reuse the Studio-to-broker client secret as the Elsa-to-provider secret.

Rotating an upstream secret changes a connection's effective material revision. Any sign-in transaction started with the old secret generation is invalidated rather than completing against changed credentials. Rotate during a maintenance window or warn users to restart sign-in if a preview/authorization flow is interrupted.

## Network, trust, and endpoint hardening

The broker defaults to HTTPS-only provider endpoints, blocks private-network destinations, validates each redirect, follows no more than three redirects, and limits provider connection/request times to ten seconds. Keep these defaults unless you have an explicit, reviewed reason to change them.

For a development-only local provider, an operator may allow `localhost` and private destinations explicitly. Do not carry that exception into production. In production:

- Keep `ProviderEgress:RequireHttps` enabled.
- Keep `AllowPrivateNetworkDestinations` disabled.
- Use `AllowedHosts` when the deployment knows the permitted provider host names.
- Do not put credentials in proxy URLs; configure any approved outbound proxy separately.
- Prefer OIDC discovery. Manual issuer, endpoint, and signing-key trust requires explicit unsafe-provider-trust authorization and confirmation.

The adapter validates state, nonce, issuer, signature, audience/authorized party, token lifetime, and upstream S256 PKCE. Still protect the surrounding host: enforce TLS, place Elsa behind an appropriately configured reverse proxy, protect administrative access, and monitor failed callback/token attempts.

## Authorization and administrative safety

Grant administrative permissions narrowly. The important External Authentication permission families are connection read/create/update/archive/test/preview, policy management, role assignment, provider unsafe-trust management, permission delegation, identity-link management, and session read/revoke.

Connection edits are security-sensitive. Database-owned updates require `If-Match` with the current ETag to avoid overwriting concurrent changes. Configuration-owned connections are intentionally read-only. Allow configuration overrides only when the deployment has a reviewed need; configuration wins by default when a database row and configuration row have the same effective key and scope.

The final-login-path guard is enabled by default. It prevents an administrator from removing the last normal sign-in path unless another recovery method or an explicitly authorized override is present. Keep it enabled, retain an audited break-glass procedure, and test that procedure before maintenance windows.

## Runbook: validate before enabling a connection

Use this sequence for each new provider connection:

1. Create the connection as a disabled draft. Use discovery URL, exact scopes, secret binding, claim projection, and the least-privilege unlinked policy.
2. Validate the configuration and run a connection test. The broker retains only the latest redacted observation; it becomes stale after material changes.
3. Register the displayed normal callback at the provider. Register the preview callback only if administrators need preview.
4. Use preview to verify provider authentication, claims, identity resolution, and permission outcome. Preview is short-lived, administrator-bound, one-time, and does not create a user, identity link, normal session, or Elsa credential.
5. Enable the connection, use Studio's login chooser, and complete the normal flow.
6. Verify that the browser/client receives only an Elsa completion code then Elsa tokens; provider access/refresh tokens must not appear in URLs, API responses, or Studio.
7. Verify a refresh token rotates successfully, replay of the superseded refresh token revokes the session, connection disable stops new or pending broker flows, and session revocation works.
8. Test local and optional upstream logout, then validate the configured recovery path.

## Monitoring and troubleshooting

The management and security endpoints provide connection observations, previews, identity links, and session administration. Keep access to those endpoints restricted. The optional `AddExternalAuthenticationHealthCheck()` bridge registers a degraded health check tagged `external-authentication` and `optional`; it does not become a readiness dependency unless your host deliberately includes that tag in readiness.

Useful symptoms and first checks:

| Symptom | First checks |
| --- | --- |
| Callback fails after load balancing | Shared Data Protection keys, durable broker state, same `SharedKeyBase64`, and public callback URL on every node. |
| Sign-in works only until restart | Dedicated External Authentication persistence feature and migrations are missing. |
| Connection does not appear in Studio | The connection is disabled, invalid, archived, shadowed, or outside the tenant context; alternatively, the Authentication Client is unavailable. |
| Provider rejects redirect URI | Register Elsa's derived callback exactly; do not use Studio's direct-OIDC callback for the upstream provider. |
| `flow_changed` after a configuration edit | A connection material revision or secret generation changed during a pending login; restart sign-in. |
| User is authenticated but lacks access | Review the identity link/unlinked policy, configured grant sources, and final permission allow/deny boundaries. |

Keep audit logs for connection changes, provider-trust overrides, secret binding changes, session revocations, and successful/failed broker outcomes. The broker uses safe public error categories; do not expose upstream provider response bodies or user-existence details in support-facing logs.
