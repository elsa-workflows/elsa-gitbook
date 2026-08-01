---
description: >-
  Authenticate machine-to-machine Elsa API clients with application API keys.
---

# API Keys

Elsa API keys are intended for applications, automation, command-line tools,
and other machine-to-machine clients. They are not a replacement for user
sign-in or delegated OpenID Connect access.

API-key authentication is enabled by `UseDefaultAuthentication()` together
with Elsa Identity. Each key belongs to an Elsa Identity application. The
application's roles determine the `permissions` claims granted to the caller.

## Create an application

The identity application endpoint creates a client ID, client secret, and API
key, stores their hashes, and returns the generated credentials. The caller
must have `create:application` and satisfy the identity security-root policy.

With the default Elsa API prefix, submit:

```http
POST /elsa/api/identity/applications
Authorization: Bearer <administrator-token>
Content-Type: application/json

{
  "name": "Order automation",
  "roles": ["workflow-runner"]
}
```

Capture the returned API key securely. Treat it as a secret and do not log it,
place it in source control, or expose it to browser code.

Configuration-owned applications can instead supply `HashedApiKey` and
`HashedApiKeySalt` in the `Identity:Applications` section. Store the original
key in a secret manager and deploy only the hash and salt to Elsa Server.

## Call the Elsa API

Send the key through the `Authorization` header:

```http
GET /elsa/api/workflow-definitions
Authorization: ApiKey <api-key>
```

The Elsa .NET API client can configure the same scheme with
`AddDefaultApiClientsUsingApiKey(...)`.

## Use least-privilege roles

Create a role for the exact operations performed by the client. For example,
a service that only starts published workflows generally needs definition read
and execute permissions, not workflow editing, identity administration, or
runtime administration.

See [Elsa API Permissions](permissions.md) for current permission names and
starter role templates.

## Rotate and revoke keys

Plan rotation before issuing a production key:

1. Create a replacement application or credential.
2. Deploy the new key to the client through its secret store.
3. Verify requests use the new credential.
4. Remove or disable the old application credential.
5. Investigate any use of the revoked key.

Do not send API keys in query strings. Avoid embedding them in WebAssembly,
JavaScript bundles, mobile packages, or other distributable clients where the
secret cannot be protected.

## Related guides

- [Elsa Identity](elsa-identity.md)
- [Elsa API Permissions](permissions.md)
- [Secrets Management](../security/secrets-management.md)
- [Production Hardening](../security/production-hardening.md)
