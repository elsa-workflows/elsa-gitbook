---
description: >-
  Endpoint groups, concurrency rules, permissions, and safe automation patterns
  for the Elsa External Authentication REST API.
---

# External Authentication REST API

{% hint style="warning" %}
These APIs first appear in the Elsa 3.8 preview and remain under active development. Generate clients against the exact preview build you deploy and keep Core and Studio package versions aligned.
{% endhint %}

All paths below are relative to Elsa's configured API prefix, commonly `/elsa/api`. JSON uses camel case. IDs and flow handles are opaque.

Use the Studio management experience for interactive administration. Use these APIs for controlled automation, deployment validation, and custom administrative clients.

## Broker Endpoints

| Purpose | Method and path | Authentication |
| --- | --- | --- |
| Discover login methods | `GET /external-authentication/login-methods?clientId=...` | Anonymous, registered client context |
| Begin external sign-in | `GET /external-authentication/authorize/{connectionKey}` | Anonymous, exact callback + PKCE |
| Begin local sign-in | `POST /external-authentication/local/authorize` | Anonymous, exact callback + PKCE |
| Provider callback | `GET /external-authentication/callback/{connectionKey}` | Provider redirect |
| Exchange or refresh | `POST /external-authentication/token` | Client and grant dependent |
| Begin logout | `POST /external-authentication/logout` | Elsa access token |

The login-method response deliberately omits provider authority, adapter configuration, upstream client identifiers, tenant data, health, and secrets. A preferred method affects display only; clients must not auto-redirect.

The authorization-code exchange requires S256 PKCE. Confidential clients also authenticate with a deployment-resolved secret. Public clients must not have a client secret. Authorization and refresh codes are single-use; refresh-token replay revokes the External Authentication Session.

## Descriptor Endpoints

These endpoints let Studio render installed adapters and policies without hard-coding provider-specific forms:

```http
GET /external-authentication/descriptors/adapters
GET /external-authentication/descriptors/policies
GET /external-authentication/descriptors/user-matchers
GET /external-authentication/descriptors/permission-sources
GET /external-authentication/descriptors/managed-secret-resolvers
GET /external-authentication/descriptors/permissions
```

They require `external-authentication:connections:read`. A missing or
incompatible custom Studio editor falls back to the descriptor-driven generic
editor. Studio obtains Elsa role options from the Identity role API; reading
that list requires `read:role`, while assigning default create-user roles is
governed by `external-authentication:roles:assign` and delegation boundaries.

## Connection Management

| Purpose | Method and path |
| --- | --- |
| List and filter | `GET /external-authentication/connections` |
| Create disabled draft or full override | `POST /external-authentication/connections` |
| Read detail | `GET /external-authentication/connections/{connectionId}` |
| Replace document | `PUT /external-authentication/connections/{connectionId}` |
| Validate locally | `POST /external-authentication/connections/{connectionId}/validate` |
| Test provider | `POST /external-authentication/connections/{connectionId}/test` |
| Begin preview | `POST /external-authentication/connections/{connectionId}/preview` |
| Enable or disable | `POST /external-authentication/connections/{connectionId}/enable` or `/disable` |
| Archive or restore | `DELETE /external-authentication/connections/{connectionId}` or `POST .../restore` |
| Replace managed secret | `PUT /external-authentication/connections/{connectionId}/secret-bindings/{fieldName}/managed` |
| Remove managed secret | `DELETE /external-authentication/connections/{connectionId}/secret-bindings/{fieldName}` |

### Optimistic Concurrency

Connection detail responses include an `ETag`, for example:

```http
ETag: "17"
```

Every database-owned mutation must send that value in `If-Match`. A stale revision returns `412 precondition_failed`; reload the resource, review the current document, and deliberately retry. Configuration-owned resources reject mutation because configuration remains deployment-owned.

### Safe Validation, Test, and Preview

- **Validate** checks structure and binding state without provider traffic.
- **Test** contacts the provider using the exact connection revision and records a safe observation.
- **Preview** performs an administrator-bound, one-time sign-in and returns only an allowlisted result. It does not create a user, identity link, normal completion code, credential, or normal session.

The provider must register the `callbackUri` and, when preview is used, the distinct `previewCallbackUri` returned by the connection resource.

### Lifecycle Safeguards

Disabling or archiving the final normal login method returns a conflict unless another local, external, or deployment-owned recovery path remains. A privileged override requires explicit confirmation. Restored connections remain disabled until they are reviewed and enabled.

## External Identity Links

```http
GET    /external-authentication/user-options?search=&cursor=&pageSize=25
GET    /external-authentication/identity-links?userId=&connectionKey=&cursor=&pageSize=100
POST   /external-authentication/identity-links
POST   /external-authentication/identity-links/{linkId}/replace
DELETE /external-authentication/identity-links/{linkId}
```

These operations require `external-authentication:links:manage`. The issuer and subject are accepted only over TLS. Elsa normalizes and immediately transforms the subject to a keyed hash; the raw subject is never returned.

Create and replace requests identify the Elsa User, immutable connection key, validated issuer, and provider subject. Replacing a link is atomic and gives the replacement a new ID.

## External Authentication Sessions

When session administration is enabled:

```http
GET    /external-authentication/sessions?userId=&connectionKey=&status=&cursor=&pageSize=100
DELETE /external-authentication/sessions/{sessionId}
```

Read requires `external-authentication:sessions:read`; revoke requires `external-authentication:sessions:revoke`. Responses contain only safe metadata—never tokens, token hashes, external subjects, or claim snapshots.

## Permissions

| Area | Permissions |
| --- | --- |
| Read/create/update/archive connections | `external-authentication:connections:read`, `:create`, `:update`, `:archive` |
| Test and preview | `external-authentication:connections:test`, `:preview` |
| Unsafe provider trust | `external-authentication:provider-trust:unsafe` |
| Policies and roles | `external-authentication:policies:manage`, `external-authentication:roles:assign` |
| Identity links | `external-authentication:links:manage` |
| Sessions | `external-authentication:sessions:read`, `external-authentication:sessions:revoke` |

The Elsa API is the authorization boundary. Hidden menus and disabled buttons in Studio are usability affordances, not security controls.

## Error Handling

Public errors use safe categories such as `invalid_request`, `method_unavailable`, `authentication_failed`, `identity_unlinked`, `flow_expired`, `flow_changed`, `access_denied`, `rate_limited`, and `temporarily_unavailable`. Management APIs add categories such as `validation_failed`, `conflict`, and `precondition_failed`.

Provider response bodies, secret material, tokens, raw subjects, and tenant/user existence details are never included. Log the returned `correlationId` and use server-side diagnostics to investigate.

## Automation Checklist

- Use TLS for every browser, provider, and management endpoint.
- Treat all IDs, codes, state, ETags, and handles as opaque.
- Honor `Retry-After` on `429` responses.
- Never log authorization codes, refresh tokens, client secrets, or raw subjects.
- Send `If-Match` for every database-owned connection mutation.
- Do not copy configuration-owned connections or external secret values through the API.
- Re-read state after conflicts instead of retrying blindly.
- Give automation clients only the permissions required for their task.

## Related Guides

- [Configuration Reference](configuration.md)
- [Administration in Studio](administration.md)
- [Production and Security](production.md)
- [Troubleshooting](troubleshooting.md)
