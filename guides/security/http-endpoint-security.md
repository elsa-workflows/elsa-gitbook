---
description: >-
  Secure Elsa HTTP workflow endpoints in 3.8.0 by separating workflow ingress
  authorization from Elsa API permissions and Studio access.
---

# HTTP Endpoint Security

`HttpEndpoint` security in Elsa has two separate layers:

1. Workflow ingress security for routes handled by the `HttpEndpoint` activity, usually under `/workflows/*`.
2. Elsa API security for routes under `/elsa/api/*`, which Studio and automation clients use.

These layers are independent. A public workflow endpoint does not make the Elsa API public, and Elsa API permissions do not secure a workflow endpoint unless the `HttpEndpoint` itself requires authorization.

## How Elsa exposes `HttpEndpoint`

In `release/3.8.0`, the HTTP module combines the configured HTTP base path with the activity path. By default, the base path is `/workflows`, so a workflow path such as `orders/{id}` is exposed as `/workflows/orders/{id}`.

`HttpEndpoint` defaults that matter for security:

- `SupportedMethods` defaults to `GET`.
- `Authorize` defaults to `false`, so the endpoint is public unless you turn authorization on.
- `Policy` is optional and only applies when the endpoint is authorized.

The route, allowed methods, authorization flag, and optional policy are stored in the generated HTTP bookmark payload. Elsa uses that payload both when starting a workflow from a trigger and when resuming a waiting workflow through the same endpoint.

## Public Endpoints

Leave `Authorize` disabled when the endpoint must be callable anonymously, for example:

- public webhooks
- callback URLs protected by their own signed token
- anonymous form posts that perform their own validation

Public endpoints still need normal HTTP hardening. For `HttpEndpoint`, the built-in knobs are:

- `RequestTimeout`
- `RequestSizeLimit`
- `FileSizeLimit`
- `AllowedFileExtensions`
- `BlockedFileExtensions`
- `AllowedMimeTypes`

You should still apply TLS, ingress rate limits, and payload validation in the host application or reverse proxy.

## Authenticated Endpoints

Set `Authorize` to `true` when the caller must be authenticated before the workflow can start or resume.

With `Authorize = true` and no policy configured:

- Elsa requires an authenticated `HttpContext.User`.
- Any authenticated principal is accepted.

This depends on the host having ASP.NET Core authentication and authorization enabled. If the host omits `app.UseAuthentication()` or `app.UseAuthorization()`, the request reaches Elsa as anonymous and the endpoint authorization fails.

## Policy-Based Endpoints

Set both `Authorize = true` and `Policy = "YourPolicyName"` when the endpoint should use a named ASP.NET Core authorization policy.

Elsa evaluates that policy through `IAuthorizationService.AuthorizeAsync(...)` and passes the current workflow as the protected resource. This lets you use standard ASP.NET Core policies based on roles, claims, groups, or custom authorization handlers.

Example host policy:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("WorkflowOperators", policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole("WorkflowOperator"));
});
```

Then configure the `HttpEndpoint` activity with:

- `Authorize`: enabled
- `Policy`: `WorkflowOperators`

## Actual 3.8.0 Response Behavior

One important release-specific detail: in `release/3.8.0`, failed `HttpEndpoint` authorization currently results in `401 Unauthorized` from the HTTP workflows middleware, even when the failure comes from a policy check.

That means these cases all surface as `401` at the workflow endpoint:

- no authenticated user
- an authenticated user that fails the configured policy

If you are troubleshooting a protected endpoint, do not assume `401` means "not logged in" only. It can also mean the named policy rejected the authenticated caller.

## Elsa API Permissions Are Separate

Elsa API endpoints use `permissions` claims, not the `HttpEndpoint` `Authorize` flag.

Common examples:

| Scenario | Typical Elsa API permissions |
| --- | --- |
| View workflow definitions | `read:workflow-definitions` |
| Edit workflow definitions | `write:workflow-definitions` |
| Publish or retract definitions | `publish:workflow-definitions`, `retract:workflow-definitions` |
| View workflow instances and journal-backed data | `read:workflow-instances` |
| View activity execution summaries | `read:activity-execution` |
| Load Studio designer metadata | `read:activity-descriptors`, `read:activity-descriptors-options`, `read:expression-descriptors`, `read:storage-drivers`, `read:variable-descriptors`, `read:installed-features` |
| Administer workflow runtime | `ManageWorkflowRuntime` |
| Full Elsa API access | `*` |

This is why a user can successfully call a protected `HttpEndpoint` and still fail to use Elsa Studio, or vice versa.

## Studio and Operator Troubleshooting

### `404 Not Found` on a workflow route

Check the effective URL first. With default HTTP settings, `Path = orders/{id}` is served from `/workflows/orders/{id}`, not `/orders/{id}`.

### `401 Unauthorized` on `/workflows/...`

In `release/3.8.0`, this usually means one of:

- `Authorize` is enabled and the request is anonymous
- `Authorize` is enabled, a `Policy` is configured, and the authenticated user failed that policy
- the host did not enable ASP.NET Core authentication/authorization middleware

### Studio signs in but cannot load definitions, instances, or designer metadata

That is usually an Elsa API permissions problem, not a workflow ingress problem. Inspect the bearer token or API key identity and confirm it contains the required `permissions` claims for the `/elsa/api/*` endpoints Studio is calling.

### Public webhook works locally but not behind a gateway

Check the reverse proxy or ingress configuration for:

- path rewriting
- request size limits
- TLS termination
- forwarded authentication headers
- rate limiting rules

## Related Guides

- [Authentication & Authorization](../authentication.md)
- [Security & Authentication](README.md)
- [External Identity Providers](external-identity-providers.md)
- [HTTP Workflows](../http-workflows/README.md)
