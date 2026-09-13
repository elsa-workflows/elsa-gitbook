---
description: >-
  Protect Elsa Workflows API endpoints with an ASP.NET Core rate-limiting
  policy and Elsa's route-scoped middleware helper.
---

# Elsa API rate limiting

Use this guide when the Elsa API is reachable by untrusted clients or when a
Studio or automation client could otherwise consume too much request capacity.
The Elsa API is the management and runtime API used by Studio and integrations;
it is separate from workflow routes created by the `HttpEndpoint` activity.

In `release/3.8.0`, Elsa provides `UseWorkflowsApiRateLimiting`. The helper
does not create a rate-limiting policy and does not run the rate-limiter
middleware. It attaches a named ASP.NET Core policy to routed Elsa API
endpoints so the host can apply its own limits.

## What the helper protects

The helper takes two arguments:

```csharp
app.UseWorkflowsApiRateLimiting("elsa/api", "elsa-api");
```

- `routePrefix` is the prefix used by `MapWorkflowsApi`. It defaults to
  `elsa/api` and is normalized by trimming whitespace and `/` characters.
- `policyName` must match a policy registered with ASP.NET Core. A null,
  empty, or whitespace-only value leaves the pipeline unchanged.

The helper applies the policy only when both conditions are true:

1. the request path starts with the configured prefix, case-insensitively; and
2. endpoint routing has selected an endpoint.

Therefore, an unrelated path is not limited by this helper, and an unmatched
path such as `/elsa/api/not-found` remains a normal `404 Not Found` request.
The helper also preserves the routed endpoint's request delegate, route
pattern, display name, and other metadata while adding the selected policy.

This is API rate limiting, not workflow-ingress rate limiting. To protect an
`HttpEndpoint` activity route, configure that activity and its HTTP workflow
host settings separately. See [HTTP endpoint security](http-endpoint-security.md).

## Register a policy

Register the rate-limiting services and a named policy in `Program.cs`. This
fixed-window example permits 120 requests per minute and rejects excess
requests immediately:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("elsa-api", limiterOptions =>
    {
        limiterOptions.PermitLimit = 120;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});
```

`AddRateLimiter` is required by ASP.NET Core before `UseRateLimiter` can run.
The policy name is an application choice; Elsa does not provide a default
quota or silently register a policy for you. Choose a limiter algorithm,
partitioning strategy, quota, window, and queue behavior that match your
clients and threat model. The example uses a fixed window only to keep the
configuration small.

## Add the Elsa API middleware

The following endpoint-routed composition matches the released Elsa Server
sample. Register the Workflows API in the Elsa module configuration, map it
with the same prefix used by the limiter helper, select routes, then add the
helper before the host's single `UseRateLimiter` call:

```csharp
using Elsa.Extensions;
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowsApi();
});

var app = builder.Build();

app.MapWorkflowsApi("elsa/api");
app.UseRouting();
app.UseWorkflowsApiRateLimiting("elsa/api", "elsa-api");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.Run();
```

The order is significant:

- `MapWorkflowsApi` creates the Elsa API endpoints under the chosen prefix.
- `UseRouting` selects the endpoint for the current request.
- `UseWorkflowsApiRateLimiting` adds the named policy to that selected endpoint
  when the path is in the Elsa API prefix.
- `UseRateLimiter` evaluates the policy and can return `429 Too Many Requests`.

Keep the route prefix identical in `MapWorkflowsApi` and
`UseWorkflowsApiRateLimiting`. A mismatch can leave the API unprotected or
apply the policy to the wrong path range.

## Configure the API options instead

Hosts that already centralize Elsa API options can set the policy name on
`ApiEndpointOptions` and pass the resulting values to both calls:

```csharp
using Elsa.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Elsa.Workflows.Api.Options;
using Microsoft.Extensions.Options;

builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowsApi();
});

builder.Services.PostConfigure<ApiEndpointOptions>(options =>
{
    options.RoutePrefix = "platform/elsa-api";
    options.RateLimitingPolicyName = "elsa-api";
});

var app = builder.Build();
var apiOptions = app.Services.GetRequiredService<IOptions<ApiEndpointOptions>>().Value;

app.MapWorkflowsApi(apiOptions.RoutePrefix);
app.UseRouting();
app.UseWorkflowsApiRateLimiting(
    apiOptions.RoutePrefix,
    apiOptions.RateLimitingPolicyName);
app.UseRateLimiter();
```

In this release, `RateLimitingPolicyName` is nullable: null means the host may
assign a policy, while an empty string disables Elsa API rate limiting. The
policy still has to be registered with `AddRateLimiter` before a matching
request is processed.

## Verify the boundary

For a local smoke test, temporarily use a policy with `PermitLimit = 1` and a
long enough window to make the result obvious. Call an Elsa API endpoint twice
with the same client identity:

```bash
curl -i -H "Authorization: Bearer $ELSA_TOKEN" \
  https://localhost:5001/elsa/api/workflow-definitions

curl -i -H "Authorization: Bearer $ELSA_TOKEN" \
  https://localhost:5001/elsa/api/workflow-definitions
```

The first request should be handled by the endpoint if authentication and API
permissions allow it. Once the policy's permit is consumed, the next request
should receive the configured rejection response, commonly `429`. A `401` or
`403` indicates authentication or Elsa API permissions and should be
diagnosed separately from rate limiting.

Also verify that:

- a non-Elsa path is not counted by this helper;
- an unmatched path under the Elsa prefix still returns `404`; and
- the policy name is registered exactly as passed to
  `UseWorkflowsApiRateLimiting`.

## Operational boundaries

This helper limits requests reaching the Elsa API in the current host process.
It does not replace a reverse-proxy or gateway limit, and it does not by
itself provide distributed quota accounting across multiple Elsa nodes. In a
cluster, decide whether host-level limits are sufficient and add an
infrastructure-level control when a shared edge quota is required.

Do not use the API helper as a substitute for:

- authentication and Elsa API permissions;
- validation and authorization on public workflow ingress routes; or
- request-size and timeout controls for `HttpEndpoint` activities.

## Troubleshooting

### The application fails when a request reaches the API

Confirm both of these registrations exist:

1. `builder.Services.AddRateLimiter(...)`; and
2. a policy whose name exactly matches the value passed to
   `UseWorkflowsApiRateLimiting`.

ASP.NET Core validates the required services and policy when the rate limiter
handles a matching request. A typo can therefore remain hidden until the
first request reaches the protected route.

### Requests are never limited

Check that:

- `MapWorkflowsApi` and `UseWorkflowsApiRateLimiting` use the same prefix;
- `UseWorkflowsApiRateLimiting` runs after `UseRouting`; and
- `UseRateLimiter` runs after the Elsa helper.

If your host uses a reverse proxy, confirm that it forwards the path you
expect. The helper evaluates the ASP.NET Core request path after proxy
forwarding and path rewriting.

### Studio receives `429`

Studio uses the Elsa API, so its requests are within the helper's scope when
they use the configured prefix. Increase or partition the policy as needed,
then keep authentication, permissions, and the API quota as separate controls.

## Release-source references

- [`UseWorkflowsApiRateLimiting` and `MapWorkflowsApi`](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/common/Elsa.Api.Common/Extensions/WebApplicationExtensions.cs)
- [`ApiEndpointOptions`](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Workflows.Api/Options/ApiEndpointOptions.cs)
- [Release tests for API rate-limiting scope and middleware order](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/test/unit/Elsa.Http.UnitTests/RateLimiting/IngressRateLimitingTests.cs)
- [ASP.NET Core rate limiting middleware](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-8.0)
