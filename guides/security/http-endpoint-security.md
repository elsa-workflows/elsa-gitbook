---
description: >-
  Secure Elsa HTTP workflow endpoints in 3.9.0 by separating workflow ingress
  authorization from Elsa API permissions and Studio access.
---

# HTTP Endpoint Security

`HttpEndpoint` security in Elsa has two separate layers:

1. Workflow ingress security for routes handled by the `HttpEndpoint` activity, usually under `/workflows/*`.
2. Elsa API security for routes under `/elsa/api/*`, which Studio and automation clients use.

These layers are independent. A public workflow endpoint does not make the Elsa API public, and Elsa API permissions do not secure a workflow endpoint unless the `HttpEndpoint` itself requires authorization.

## How Elsa exposes `HttpEndpoint`

In `release/3.9.0`, the HTTP module combines the configured HTTP base path with the activity path. By default, the base path is `/workflows`, so a workflow path such as `orders/{id}` is exposed as `/workflows/orders/{id}`.

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

## Actual 3.9.0 Response Behavior

One important release-specific detail: in `release/3.9.0`, failed `HttpEndpoint` authorization currently results in `401 Unauthorized` from the HTTP workflows middleware, even when the failure comes from a policy check.

That means these cases all surface as `401` at the workflow endpoint:

- no authenticated user
- an authenticated user that fails the configured policy

If you are troubleshooting a protected endpoint, do not assume `401` means "not logged in" only. It can also mean the named policy rejected the authenticated caller.

## Elsa API Permissions Are Separate

Elsa API endpoints use `permissions` claims, not the `HttpEndpoint` `Authorize` flag.

Common examples:

| Scenario | Typical Elsa API permissions |
| --- | --- |
| View workflow definitions | `workflows/definitions:view` |
| Edit workflow definitions | `workflows/definitions:write` |
| Publish or retract definitions | `workflows/definitions:publish`, `workflows/definitions:retract` |
| View workflow instances and journal-backed data | `workflows/instances:view` |
| View activity execution summaries | `workflows/activity-executions:view` |
| Load Studio designer metadata | `workflows/descriptors/activities:view`, `workflows/descriptors/expressions:view`, `workflows/descriptors/storage-drivers:view`, `workflows/descriptors/variables:view`, `system/features:view` |
| Administer workflow runtime | `workflows/runtime:control` |
| Full Elsa API access | `*` |

These are Core 3.9.0 examples. See [Elsa API Permissions](../authentication/permissions.md)
for the catalog, workflow-definition version permissions, and extension-module
boundaries. This is why a user can successfully call a protected
`HttpEndpoint` and still fail to use Elsa Studio, or vice versa.

## Studio and Operator Troubleshooting

### `404 Not Found` on a workflow route

Check the effective URL first. With default HTTP settings, `Path = orders/{id}` is served from `/workflows/orders/{id}`, not `/orders/{id}`.

### `401 Unauthorized` on `/workflows/...`

In `release/3.9.0`, this usually means one of:

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

## Release source checked

The release-specific HTTP authorization behavior above was checked against
[the 3.9.0 workflow middleware](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.Http/Middleware/HttpWorkflowsMiddleware.cs)
and [the authentication-based HTTP endpoint authorization handler](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.Http/Handlers/AuthenticationBasedHttpEndpointAuthorizationHandler.cs).

## Related Guides

- [Authentication & Authorization](../authentication/README.md)
- [Security & Hardening](README.md)
- [Direct OpenID Connect](../authentication/direct-openid-connect.md)
- [HTTP Workflows](../http-workflows/README.md)
