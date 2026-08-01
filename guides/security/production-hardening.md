---
description: >-
  Harden an Elsa and Elsa Studio deployment with explicit browser boundaries,
  TLS and proxy controls, monitoring, incident diagnostics, and a production checklist.
---

# Production Hardening

Use this guide to prepare an Elsa Server and Elsa Studio deployment for
production. It focuses on host, network, browser, and operational controls.
Configure identities, permissions, and sign-in providers separately in
[Authentication & Authorization](../authentication/README.md).

## Browser boundaries: CORS and CSRF

Define a CORS policy for the exact browser origins that need to call the Elsa
API. Do not use `AllowAnyOrigin()` in production, and only enable credentials
when the application actually uses cookie-based authentication.

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("ElsaCorsPolicy", policy => policy
        .WithOrigins("https://studio.example.com", "https://app.example.com")
        .WithMethods("GET", "POST", "PUT", "DELETE")
        .WithHeaders("Content-Type", "Authorization"));
});

app.UseCors("ElsaCorsPolicy");
```

For a cookie-based browser flow, add `.AllowCredentials()` explicitly and
keep the origin allowlist exact. Do not enable credentials for a wildcard
origin or for a bearer-token client that does not need cookies.

When cookies are used, configure antiforgery protection and appropriate
`SameSite`, `Secure`, and `HttpOnly` cookie settings. Token-based browser
clients have different CSRF characteristics, but still require tightly scoped
CORS.

Do not use CORS as protection for a server-to-server webhook or bookmark
resume URL. Apply caller validation, allowlists where feasible, and rate
limits instead. See [Bookmark resume tokens](bookmark-resume-tokens.md).

## Rate limiting and request boundaries

Apply rate limits at the reverse proxy or ingress so abusive traffic is
rejected before it consumes application resources. Use separate policies for:

- public workflow or webhook routes;
- bookmark resume routes; and
- authenticated Elsa API traffic.

Set limits from observed traffic and capacity rather than copying example
numbers unchanged. Return `429 Too Many Requests`, monitor the responses, and
provide burst capacity only where it cannot be abused.

For `HttpEndpoint` workflow routes, use its request timeout, request-size,
file-size, MIME-type, and file-extension settings as applicable. Details are
in [HTTP endpoint security](http-endpoint-security.md).

## TLS, proxies, and network placement

Terminate TLS using trusted certificates, redirect HTTP to HTTPS, and enable
HSTS after the public HTTPS configuration is proven. Require TLS 1.2 or later
according to your platform policy.

At the proxy boundary:

- forward the original host, client address, and scheme only from trusted
  proxies;
- configure the host to process forwarded headers correctly, so redirect URLs,
  cookies, and audit data use the public scheme and host;
- restrict firewall and security-group rules to required ports;
- keep database, cache, and distributed-lock stores off public networks; and
- use network controls or mTLS for sensitive service-to-service paths where
  required by the threat model.

Workflow runtime operations use persistence and distributed locking; they do
not require sticky sessions. If Studio host behavior or another application
component requires affinity, scope it to that component rather than treating it
as an Elsa runtime requirement. See [Clustering](../clustering/README.md).

The [ingress and CORS examples](examples/ingress-cors-snippet.md) are starting
points, not a substitute for proxy-vendor hardening guidance.

## Deploy Studio safely

Serve Studio over HTTPS and give it an explicit backend API URL. Register only
the production origins and callback URLs required by the Studio host model;
do not carry localhost development callbacks into production registrations.

Keep Studio and backend configuration aligned across environments, especially
the public URLs, proxy headers, CORS origins, and logout/callback paths. Limit
Studio access through the authorization model documented in
[Authentication & Authorization](../authentication/README.md), and avoid
placing secrets in browser-delivered configuration.

For host-model integration details, see [Studio designer
integration](../studio/integration/README.md).

## Observe and investigate safely

Monitor authentication failures, rejected workflow ingress requests, invalid
or repeated bookmark resume attempts, rate-limit responses, workflow
cancellations, and unexpected administrative changes. Correlate the events
with safe identifiers, source information, and timestamps.

Redact tokens, credentials, personally identifiable information, and sensitive
workflow input/output from logs and traces. Establish a retention policy that
meets both operational and privacy requirements.

Use [Monitoring & Observability](../../operate/monitoring-observability.md)
for tracing and telemetry setup. During an incident, confirm the public route,
TLS/proxy headers, CORS response, and rate-limit decision before changing
authentication or workflow configuration. General diagnostic steps are in the
[Troubleshooting guide](../troubleshooting/README.md).

## Production checklist

- [ ] Public Elsa and Studio endpoints enforce HTTPS with trusted certificates.
- [ ] Reverse proxies, forwarded headers, firewall rules, and network exposure
  have been reviewed for the production topology.
- [ ] CORS allows only required HTTPS origins; cookie-based flows have CSRF and
  secure-cookie controls.
- [ ] Public workflow and bookmark-resume routes have appropriate validation,
  rate limits, and allowlists where possible.
- [ ] `HttpEndpoint` request and file limits match the accepted workload.
- [ ] Infrastructure credentials, signing keys, and connection strings are
  stored outside source control; workflow-resolvable values use [Secrets
  management](secrets-management.md) where appropriate.
- [ ] Logs and traces redact credentials, tokens, PII, and sensitive workflow
  data, with alerting for suspicious failures and rate-limit events.
- [ ] Database and distributed-lock credentials follow least privilege, and
  production dependencies/images have a patching and vulnerability-review
  process.
- [ ] The authentication and authorization design has been reviewed using
  [Authentication & Authorization](../authentication/README.md).
