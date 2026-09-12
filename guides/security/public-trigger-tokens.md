---
description: >-
  Configure and operate Elsa's protected public event-trigger URLs, bookmark
  resume tokens, and shared Data Protection keys in a 3.8.0 deployment.
---

# Public trigger tokens

Elsa's HTTP workflow API uses protected, tokenized URLs for callbacks that
must reach a specific workflow instance without an authenticated API client.
The same token service supports two public routes:

- **Event trigger** — `GET /events/trigger?t=<token>` publishes a named event
  for the workflow instance encoded in the token.
- **Bookmark resume** — `GET` or `POST /bookmarks/resume?t=<token>` resumes a
  bookmark for the workflow instance encoded in the token.

Both routes allow anonymous access. A valid token is therefore a bearer
capability: anyone who obtains it can attempt the operation it represents.
Use HTTPS, keep tokenized URLs out of logs and analytics, and give each token
the shortest lifetime that meets the business requirement.

This guide covers the shared token and key-management boundary. For the
bookmark request body, query parameters, and 1 MiB POST limit, see
[Bookmark Resume Tokens](bookmark-resume-tokens.md).

## Choose the callback mechanism

| Need | Use | What the release does |
| --- | --- | --- |
| Resume one waiting bookmark with optional workflow input | Bookmark resume | Resolves the token to a bookmark and resumes it synchronously or through the bookmark queue. |
| Publish a named event to one workflow instance | Public event trigger | Resolves the token to an event name and workflow instance, then publishes the event. It does not accept an input body. |
| Publish an event from an authenticated integration | Authenticated event trigger | `POST /events/{eventName}/trigger` with the `trigger:event` permission. The request can include input, correlation, instance, activity, and execution-mode fields. |

Do not use the public event route when the caller needs to provide arbitrary
event input or an authenticated identity. Use the authenticated route or a
separately secured integration endpoint instead.

## Generate a public event URL

The `Elsa.Http` extensions expose `GenerateEventTriggerUrl` from an
`ExpressionExecutionContext`. The host must compose Elsa's HTTP support so the
helper can use the configured API route prefix and `IAbsoluteUrlProvider`. The
overloads accept a duration, an absolute expiration, or no expiration:

```csharp
using Elsa.Expressions.Models;
using Elsa.Extensions;

static string CreatePaymentCallback(ExpressionExecutionContext context)
{
    // Prefer an explicit lifetime for an external callback.
    return context.GenerateEventTriggerUrl(
        eventName: "PaymentReceived",
        lifetime: TimeSpan.FromMinutes(15));
}
```

The generated URL uses the configured API route prefix and has this shape:

```text
https://example.test/elsa/api/events/trigger?t=<protected-token>
```

The protected payload contains the event name and workflow instance ID. It
does not contain an authenticated caller identity or an arbitrary event input
object. The public endpoint publishes the event for the encoded workflow
instance and returns success when the publish operation completes; it is not a
general-purpose event ingestion API.

For a callback that resumes a bookmark instead, use
`GenerateBookmarkTriggerUrl(bookmarkId, lifetime)` and follow the request
contract in [Bookmark Resume Tokens](bookmark-resume-tokens.md).

### Avoid non-expiring callback URLs

The no-lifetime overload creates a non-expiring protected token. That can be
appropriate for a deliberately long-lived integration whose revocation is
handled elsewhere, but it is a poor default for email, SMS, approval, or
third-party webhook callbacks. Prefer a `TimeSpan` or `DateTimeOffset` and
make the business action idempotent where replay matters.

Expiration does not make a token single-use. For one-time bookmark callbacks,
create the bookmark with `AutoBurn = true`; an event-trigger token has no
equivalent single-use switch in this release. If an event callback must be
one-time, enforce that rule in the workflow or at the integration boundary.

## Use the authenticated event endpoint when appropriate

The authenticated endpoint is distinct from the tokenized public route:

```http
POST /elsa/api/events/PaymentReceived/trigger
Authorization: Bearer <token>
Content-Type: application/json

{
  "input": { "paymentId": "pay-123" },
  "correlationId": "order-123"
}
```

The endpoint requires the `trigger:event` permission. Its request supports an
optional workflow instance ID, correlation ID, activity instance ID, input,
and execution mode. The release defaults the execution mode to asynchronous;
set it explicitly when the caller needs a different mode. Use this endpoint
when the caller can authenticate to Elsa and must send event data. The public
event URL intentionally has a smaller contract:
the token supplies the event name and workflow instance, and the route has no
request body.

## Keep Data Protection keys continuous

The default `Elsa.SasTokens` feature registers a scoped
`DataProtectorTokenService` and configures ASP.NET Core Data Protection with
the application name `Elsa Workflows`. The Workflows API depends on this
feature, so an ordinary host that enables the Workflows API does not need a
second token registration just to use bookmark or public event URLs.

In a multi-node deployment, every node that may receive a callback must be
able to unprotect tokens issued by every other node. Configure a shared,
durable key ring and keep the application name and compatible Data Protection
stack consistent across nodes. A node-local key ring can make a token appear
invalid when a load balancer sends the callback to a different node.

Elsa registers Data Protection but does not choose a shared durable key-ring
backend for your deployment. Selecting and protecting shared storage, keeping
old keys available for valid token lifetimes, and granting each node access to
the ring are host and platform responsibilities.

For example, the feature exposes the standard Data Protection builder through
`UseSasTokens`:

```csharp
using System.IO;
using Elsa.Extensions;
using Microsoft.AspNetCore.DataProtection;

builder.Services.AddElsa(elsa =>
{
    elsa.UseSasTokens(tokens =>
    {
        tokens.ConfigureDataProtectionBuilder = dataProtection =>
            dataProtection
                .PersistKeysToFileSystem(new DirectoryInfo("/var/lib/elsa/keys"))
                .SetApplicationName("Elsa Workflows");
    });
});
```

Use a shared storage provider and deployment-specific key protection for your
platform. The path above is only an example; do not place a writable key ring
on an ephemeral container filesystem. Preserve old keys for at least the
longest lifetime of tokens that may still be used. Deleting the key ring is an
effective way to invalidate protected tokens, but it also invalidates other
ASP.NET Core data protected with that key material and should be treated as a
deployment/security event.

For the ASP.NET Core key-ring and application-isolation rules, see
[Configure ASP.NET Core Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview)
and [Host ASP.NET Core in a web farm](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/web-farm).

## Custom token services in conventional hosts

In a conventional `AddElsa` composition, the standard `SasTokensFeature`
exposes a `TokenService` factory. A host can replace the default
`ITokenService`, but this is an advanced compatibility boundary:

```csharp
using Elsa.SasTokens.Contracts;
using Elsa.SasTokens.Features;
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddElsa(elsa =>
{
    elsa.UseSasTokens(tokens =>
    {
        tokens.TokenService = services =>
            ActivatorUtilities.CreateInstance<MyTokenService>(services);
    });
});
```

Keep the replacement compatible across all nodes and preserve the payload
types required by the built-in routes (`BookmarkTokenPayload` and
`EventTokenPayload`). A custom service does not make either route authenticated
or add replay protection; it only changes how the token is created and
validated. If you do not need a custom token format, keep Elsa's default
Data Protection implementation.

The CShells/Modular Server shell feature also registers the default token
service and the `Elsa Workflows` application name, but its release-3.8.0
shell configuration does not expose the same replacement factory. Treat a
custom token service as a conventional-host extension point unless you own a
modular host integration that registers and verifies the replacement itself.

## Operational checklist

- Prefer a finite lifetime for every external callback.
- Treat the full URL and its `t` value as secrets; redact them from logs,
  tracing, analytics, and support captures.
- Share and durably protect the Data Protection key ring before enabling
  multi-node routing.
- Keep the application name stable across nodes and compatible deployments.
- Use the authenticated event route when the caller must provide input or an
  authenticated identity.
- Add ingress rate limiting and monitoring for invalid-token and repeated-use
  attempts.
- Make the business operation idempotent when a callback can be replayed.
- Test token generation, cross-node validation, expiry, and key rotation in
  the same deployment shape used in production.

## Release source

The 3.8.0 behavior described here is implemented by:

- [Workflows API feature and its SAS-token dependency](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Workflows.Api/Features/WorkflowsApiFeature.cs)
- [`SasTokensFeature`](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.SasTokens/Features/SasTokensFeature.cs)
- [CShells `SasTokensFeature`](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.SasTokens/ShellFeatures/SasTokensFeature.cs)
- [`DataProtectorTokenService`](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.SasTokens/Contracts/DataProtectorTokenService.cs)
- [`GenerateEventTriggerUrl`](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Http/Extensions/EventExpressionExecutionContextExtensions.cs)
- [`EventTokenPayload`](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Workflows.Runtime/Models/EventTokenPayload.cs)
- [Public event-trigger endpoint](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Workflows.Api/Endpoints/Events/TriggerPublic/Endpoint.cs)
- [Authenticated event-trigger endpoint](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Workflows.Api/Endpoints/Events/TriggerAuthenticated/Endpoint.cs) and [request model](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Workflows.Api/Endpoints/Events/TriggerAuthenticated/Models.cs)
- [Bookmark token URL generation](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Http/Extensions/BookmarkExecutionContextExtensions.cs)
- [`BookmarkTokenPayload`](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Workflows.Runtime/Models/BookmarkTokenPayload.cs)
