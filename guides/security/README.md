---
description: >-
  Comprehensive guide to securing Elsa Server and workflows end-to-end, covering identity, authentication, tokenized resume URLs, CORS, secrets management, and production hardening.
---

# Security & Authentication Guide

## Executive Summary

This guide provides actionable, Elsa-specific security practices for protecting your workflow runtime, API endpoints, and Studio deployments. It focuses on configurations and patterns unique to Elsa Workflows while deferring deep platform-specific topics (OIDC provider setup, full reverse proxy hardening, WAF configuration) to official vendor documentation.

### Goals and Scope

**What This Guide Covers:**
- Configuring Identity and Authentication in Elsa Server (`UseIdentity`, `UseDefaultAuthentication`)
- Securing `HttpEndpoint` workflow routes, including public vs authenticated endpoints
- Securing tokenized bookmark resume URLs (TTL, revocation, rate limiting)
- CORS, CSRF, and rate limiting for public-facing endpoints
- Secrets management for API keys, database connections, and workflow variables
- Network security and TLS requirements
- Studio deployment security considerations
- Production hardening checklist

**What This Guide Defers to Vendor Documentation:**
- Detailed OIDC provider configuration (Azure AD, Auth0, Keycloak)
- Full reverse proxy hardening (Nginx, Traefik, Envoy)
- Web Application Firewall (WAF) setup
- Infrastructure-as-Code (IaC) security best practices
- Advanced certificate management and PKI

For clustering-specific security (distributed lock credentials, database permissions), see [Clustering Guide](../clustering/README.md) (DOC-015).

For workflow-trigger ingress security specifically, see [HTTP Endpoint Security](http-endpoint-security.md).

---

## Identity & Authentication in Elsa Server

### UseIdentity Configuration

Elsa Server's identity system is configured in `Program.cs` via the `UseIdentity` extension method. In the 3.8.0 server sample, token options are bound from `Identity:Tokens`, while users, applications, and roles are provided by configuration-based providers bound from the `Identity` section.

**Reference:** `src/apps/Elsa.Server.Web/Program.cs` in elsa-core

**Basic Identity Setup:**

```csharp
using Elsa.Extensions;

var builder = WebApplication.CreateBuilder(args);
var identitySection = builder.Configuration.GetSection("Identity");
var identityTokenSection = identitySection.GetSection("Tokens");

// Configure Elsa with identity
builder.Services.AddElsa(elsa =>
{
    elsa
        .UseIdentity(identity =>
        {
            identity.TokenOptions += options => identityTokenSection.Bind(options);
            identity.UseConfigurationBasedUserProvider(options => identitySection.Bind(options));
            identity.UseConfigurationBasedApplicationProvider(options => identitySection.Bind(options));
            identity.UseConfigurationBasedRoleProvider(options => identitySection.Bind(options));
        })
        .UseDefaultAuthentication() // Enables JWT/API key authentication
        .UseWorkflowManagement()
        .UseWorkflowRuntime()
        .UseWorkflowsApi();
});

var app = builder.Build();

// Enable authentication middleware
app.UseAuthentication();
app.UseAuthorization();

app.UseWorkflowsApi();
app.Run();
```

**Key Configuration Points:**
- **`UseIdentity`**: Registers Elsa's built-in identity system
- **`identity.TokenOptions`**: Configures JWT issuer, audience, signing key, and token lifetimes
- **Configuration-based providers**: Load `Users`, `Applications`, and `Roles` from the `Identity` section
- **`UseDefaultAuthentication`**: Enables JWT bearer and API-key authentication from the `Authorization` header

### Minimal appsettings.json Structure

See [examples/appsettings-identity.json](examples/appsettings-identity.json) for a complete example with placeholders.

```json
{
  "Identity": {
    "Tokens": {
      "SigningKey": "${ELSA_IDENTITY_SIGNING_KEY}",
      "Issuer": "https://your-elsa-server.com",
      "Audience": "https://your-elsa-server.com",
      "AccessTokenLifetime": "01:00:00",
      "RefreshTokenLifetime": "7.00:00:00"
    },
    "Roles": [
      {
        "Id": "admin",
        "Name": "Administrator",
        "Permissions": ["*"]
      }
    ],
    "Users": [
      {
        "Id": "admin-user",
        "Name": "admin",
        "HashedPassword": "${ELSA_ADMIN_HASHED_PASSWORD}",
        "HashedPasswordSalt": "${ELSA_ADMIN_PASSWORD_SALT}",
        "Roles": ["admin"]
      }
    ],
    "Applications": [
      {
        "Id": "service-client",
        "Name": "Service Client",
        "ClientId": "service-client",
        "HashedApiKey": "${ELSA_SERVICE_HASHED_API_KEY}",
        "HashedApiKeySalt": "${ELSA_SERVICE_API_KEY_SALT}",
        "HashedClientSecret": "${ELSA_SERVICE_HASHED_CLIENT_SECRET}",
        "HashedClientSecretSalt": "${ELSA_SERVICE_CLIENT_SECRET_SALT}",
        "Roles": ["admin"]
      }
    ]
  }
}
```

API-key clients authenticate with:

```bash
Authorization: ApiKey YOUR_API_KEY
```

Elsa validates the API key against configured applications; store hashed API keys and salts in configuration, not raw API keys.

### Elsa API Permission Claims

Elsa API endpoints check the `permissions` claim. Each claim value must match a permission required by the endpoint, and `*` grants all Elsa API permissions.

Elsa Identity roles collect permission strings. When Elsa Identity issues a JWT or validates an Elsa API key, permissions from the assigned roles are emitted as `permissions` claims. External IdPs should emit the same claim type or the host should map external roles, groups, or scopes into `permissions` claims during token validation.

ASP.NET Core policies such as `RequireRole("Admin")` protect custom host endpoints, pages, or controllers. They do not replace Elsa endpoint permission claims. Elsa endpoint permissions come from endpoint configuration and module constants, not only from shared `PermissionNames` constants.

Common read-only workflow access uses `read:workflow-definitions`, `read:workflow-instances`, and `read:activity-execution`. Use `*` only for full administrative access. For workflow ingress routes handled by the `HttpEndpoint` activity, see [HTTP Endpoint Security](http-endpoint-security.md).

For the route-by-route permission map, Studio capability notes, and least-privilege role templates, see [Elsa API Permissions](permission-reference.md).

**Important:**
- **Never commit secrets** to source control
- Use environment variables or secret managers (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault)
- Rotate API keys regularly (every 90 days or per your security policy)

### API Key vs JWT/OIDC

**API Keys:**
- Best for: Machine-to-machine communication, CI/CD pipelines, internal services
- Pros: Simple to implement, no external dependencies
- Cons: No fine-grained scopes, harder to rotate safely

**JWT/OIDC:**
- Best for: User authentication, web applications, mobile apps
- Pros: Industry standard, fine-grained scopes, short-lived tokens, refresh token support
- Cons: Requires OIDC provider setup and maintenance

**Recommendation:** For production systems, prefer JWT/OIDC for user-facing endpoints and reserve API keys for trusted service-to-service communication.

---

## Tokenized Bookmark Resume URLs

### How Bookmark Tokens Work

Elsa generates tokenized URLs for resuming workflows at bookmarks (e.g., HTTP callbacks, webhooks). These URLs are used in scenarios like:
- Sending approval requests via email
- Webhook callbacks from external systems
- Multi-step form submissions with state preservation

**Reference:** `src/modules/Elsa.Http/Extensions/BookmarkExecutionContextExtensions.cs` (elsa-core)

The `GenerateBookmarkTriggerUrl` method creates URLs in this format:
```
GET or POST /elsa/api/bookmarks/resume?t=<encrypted_token>
```

The token contains:
- Bookmark ID
- Workflow instance ID

`BookmarkTokenPayload` contains only `BookmarkId` and `WorkflowInstanceId`. Token lifetime is controlled by the token generated by `GenerateBookmarkTriggerToken` / `GenerateBookmarkTriggerUrl`, including overloads that accept a `TimeSpan` or `DateTimeOffset`.

### Security Best Practices for Resume Tokens

See [examples/resume-endpoint-notes.md](examples/resume-endpoint-notes.md) for detailed guidance.

**Critical Security Controls:**

1. **Time-to-Live (TTL):**
   - Bookmark resume tokens can be generated with a lifetime or absolute expiration
   - Use short-lived tokens for webhooks (minutes) and longer-lived tokens for email approvals (hours/days)
   - Tokens become invalid when the bookmark is consumed (AutoBurn) or the workflow is cancelled

2. **Single-Use Semantics:**
   - Bookmarks are "burned" (deleted) after successful resume by default
   - Use `AutoBurn = true` in bookmark creation for one-time-use tokens
   - Check for duplicate resume attempts via distributed locking

3. **Revocation Strategy:**
   - Implement a token revocation list for compromised tokens
   - Workflow cancellation automatically invalidates associated bookmarks
   - Manual revocation: delete bookmark from database or cancel workflow

4. **Audit Logging:**
   ```csharp
   // Log resume attempts
   _logger.LogInformation(
       "Bookmark resume attempted. BookmarkId={BookmarkId}, WorkflowInstanceId={InstanceId}, Success={Success}",
       bookmarkId, instanceId, success);
   ```
   - Log all resume attempts (success and failure)
   - Include source IP, timestamp, and workflow context
   - Integrate with SIEM for anomaly detection

5. **Rate Limiting:**
   - Apply rate limits at ingress (Nginx, Traefik) or application middleware
   - Recommended limits:
     - Per IP: 100 requests/minute for resume endpoints
     - Per token: 3 attempts before soft-block (requires investigation)
   - See [examples/ingress-cors-snippet.md](examples/ingress-cors-snippet.md) for configuration

### Example: Resuming a Workflow via Bookmark

**Request:**
```bash
curl -X POST "https://your-elsa-server.com/elsa/api/bookmarks/resume?t=eyJhbGc..." \
  -H "Content-Type: application/json" \
  -d '{"input":{"approvalStatus":"approved","comments":"LGTM"}}'
```

**Expected Responses:**
- **200 OK**: Workflow resumed successfully
- **400 Bad Request**: Token invalid or input query-string JSON is malformed
- **429 Too Many Requests**: Rate limit exceeded

**Security Notes:**
- Always use HTTPS for resume URLs to prevent token interception
- Validate payload schema to prevent injection attacks
- Consider IP allowlisting for internal webhooks

---

## CORS, CSRF, and Rate Limiting

### CORS for Resume Endpoints

When exposing resume endpoints to browser-based applications or external systems, configure CORS carefully to prevent unauthorized access.

**Configuration Example:**

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("ElsaCorsPolicy", policy =>
    {
        policy
            .WithOrigins(
                "https://your-app.com",
                "https://approved-partner.com"
            )
            .WithMethods("POST")
            .WithHeaders("Content-Type", "Authorization")
            .AllowCredentials();  // Only if using cookies
    });
});

app.UseCors("ElsaCorsPolicy");
```

**Best Practices:**
- **Never use `AllowAnyOrigin()` in production**
- Whitelist only necessary origins
- Prefer POST over GET for resume operations (prevents token leakage in logs)
- For public webhooks, avoid CORS altogether (server-to-server only)

### CSRF Considerations

**Bookmark Resume Endpoints:**
- Resume endpoints are designed for server-to-server or webhook callbacks
- If resume tokens are embedded in web pages, add CSRF protection:
  ```csharp
  builder.Services.AddAntiforgery(options =>
  {
      options.HeaderName = "X-CSRF-TOKEN";
  });
  ```

**Studio and API:**
- Elsa Studio uses token-based auth (immune to CSRF)
- For cookie-based sessions, enable `SameSite=Strict` or `SameSite=Lax`

### Rate Limiting

**Application-Level:**

```csharp
using AspNetCoreRateLimit;

builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "POST:/elsa/api/bookmarks/resume",
            Period = "1m",
            Limit = 100
        },
        new RateLimitRule
        {
            Endpoint = "GET:/elsa/api/bookmarks/resume",
            Period = "1m",
            Limit = 100
        }
    };
});
builder.Services.AddInMemoryRateLimiting();

app.UseIpRateLimiting();
```

**Ingress-Level (Preferred):**

See [examples/ingress-cors-snippet.md](examples/ingress-cors-snippet.md) for Nginx/Traefik examples.

Ingress rate limiting is more scalable and protects against DDoS before traffic reaches the application.

---

## Secrets Management

### Storing Sensitive Configuration

**Do:**
- Store API keys, database connections, and JWT signing keys in:
  - Environment variables
  - Azure Key Vault
  - AWS Secrets Manager
  - HashiCorp Vault
- Encrypt secrets at rest in configuration stores
- Rotate credentials regularly (quarterly minimum)

**Don't:**
- Commit secrets to Git (use `.gitignore` and secret scanning)
- Log sensitive data in plaintext
- Share API keys via email or chat

**Example: Loading Secrets from Environment Variables:**

```csharp
builder.Configuration.AddEnvironmentVariables(prefix: "ELSA_");
```

```bash
# Set environment variables
export ELSA_Identity__Tokens__SigningKey="your-signing-key"
export ELSA_Identity__Applications__0__HashedApiKey="..."
export ELSA_Identity__Applications__0__HashedApiKeySalt="..."
export ELSA_ConnectionStrings__DefaultConnection="Server=db;Database=elsa;..."
```

### Workflow Variable Security

**Sensitive Workflow Data:**
- Mark sensitive workflow variables as "secret" (not logged or displayed)
- Avoid passing PII through workflow variables when possible
- Use encrypted storage for workflow instance data containing secrets

**Logging Best Practices:**
- Scrub PII and secrets from structured logs
- Use safe fields only: WorkflowInstanceId, ActivityType, Status
- Example log configuration:
  ```json
  {
    "Logging": {
      "LogLevel": {
        "Elsa": "Information"
      },
      "SafeFields": ["WorkflowInstanceId", "ActivityType", "Status"],
      "RedactedFields": ["Input", "Output", "Variables"]
    }
  }
  ```

---

## Network & TLS

### TLS Requirements

**Minimum Requirements:**
- **TLS 1.2 or higher** for all HTTP endpoints
- Valid, trusted certificates (not self-signed in production)
- HTTPS redirect enforced:
  ```csharp
  app.UseHttpsRedirection();
  app.UseHsts();
  ```

**Certificate Configuration:**
- Obtain certificates from Let's Encrypt, DigiCert, or your organization's CA
- Configure certificate renewal automation
- Use wildcard certificates for multi-subdomain deployments

### Mutual TLS (mTLS)

For service-to-service communication in zero-trust environments:

```csharp
builder.Services.AddHttpClient<IWorkflowClient>(client =>
{
    client.BaseAddress = new Uri("https://other-service.internal");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ClientCertificates = { LoadClientCertificate() }
});
```

**Use Cases:**
- Internal microservices mesh
- Sensitive data workflows
- Compliance requirements (PCI-DSS, HIPAA)

### Session Affinity (Sticky Sessions)

**Important Note:** Sticky sessions are **not required** for Elsa workflow runtime. Workflows use distributed locking and database persistence, making them resilient to node switching mid-execution.

**When Sticky Sessions May Help:**
- Studio UI interactions only (for caching workflow definitions client-side)
- Not needed for API calls or runtime operations

See [Clustering Guide](../clustering/README.md) (DOC-015) for more on distributed runtime architecture.

---

## Studio Security Notes

### Deploying Studio Behind Ingress

**Ingress Configuration:**

```yaml
# Example Kubernetes Ingress (YAML)
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: elsa-studio
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/force-ssl-redirect: "true"
spec:
  tls:
  - hosts:
    - studio.your-domain.com
    secretName: elsa-studio-tls
  rules:
  - host: studio.your-domain.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: elsa-studio
            port:
              number: 80
```

### SSO/Identity Integration

**Studio Authentication:**
- Configure Studio to use the same OIDC provider as Elsa Server through the current Studio OIDC configuration
- Use the Blazor host pattern in [Studio Designer Integration](../studio/integration/README.md), then configure:
  ```json
  {
    "Backend": {
      "Url": "https://api.your-domain.com/elsa/api"
    },
    "Authentication": {
      "Provider": "OpenIdConnect",
      "OpenIdConnect": {
        "Authority": "https://your-idp.com",
        "ClientId": "elsa-studio",
        "AuthenticationScopes": ["openid", "profile", "offline_access"],
        "BackendApiScopes": ["elsa_api"]
      }
    }
  }
  ```

  `AuthenticationScopes` are used for Studio sign-in. `BackendApiScopes` are used when Studio requests access tokens for Elsa Server API calls.

  Register Studio redirect and logout callback URIs according to the host model: Blazor WebAssembly uses `/authentication/login-callback` and `/authentication/logout-callback`; Blazor Server uses `/signin-oidc` and `/signout-callback-oidc` by default. Studio initiates logout at `/authentication/logout`.

**Authorization:**
- Grant Studio users only the Elsa `permissions` claims needed for the screens and actions they can use
- Use ASP.NET Core role policies for custom host controllers or pages; Elsa API endpoints still require Elsa permission claims

### Session Affinity for Studio

While not required for runtime, Studio benefits from session affinity to:
- Cache workflow definitions in browser memory
- Reduce API round-trips during editing

**Configuration (Optional):**
- Enable sticky sessions at load balancer for Studio endpoints only
- Use cookie-based or IP-hash affinity
- Not needed for `/elsa/api/` endpoints (runtime operations)

---

## Production Hardening Checklist

Use this checklist to verify your Elsa deployment is production-ready from a security perspective.

### Identity & Authentication
- [ ] Identity configured (`UseIdentity` and `UseDefaultAuthentication`)
- [ ] JWT/OIDC preferred over API keys for user-facing endpoints
- [ ] API keys stored in secret manager (Azure Key Vault, AWS Secrets Manager, etc.)
- [ ] Token lifetimes configured appropriately (access: 1h, refresh: 7d)
- [ ] API keys rotated regularly (quarterly or per policy)

### Network Security
- [ ] HTTPS enforced on all endpoints (`UseHttpsRedirection()`)
- [ ] TLS 1.2+ configured with strong ciphers
- [ ] Valid, trusted certificates installed
- [ ] mTLS configured for service-to-service (if applicable)
- [ ] Ingress/reverse proxy properly configured (see vendor docs)

### CORS & CSRF
- [ ] CORS policy locked down (no `AllowAnyOrigin()`)
- [ ] Only whitelisted origins allowed
- [ ] CSRF protection enabled for cookie-based sessions
- [ ] SameSite cookie attribute set to Strict or Lax

### Resume Tokens
- [ ] Bookmark URLs generated with an appropriate lifetime or absolute expiration
- [ ] Single-use semantics enforced (`AutoBurn = true`)
- [ ] Audit logging enabled for all resume attempts
- [ ] Rate limiting in place (100 req/min per IP recommended)
- [ ] IP allowlisting configured for internal webhooks (if applicable)

### Secrets Management
- [ ] Database connection strings stored in secret manager
- [ ] No secrets committed to source control (`.gitignore` verified)
- [ ] Environment variables used for runtime secrets
- [ ] Workflow variables marked as secret (not logged)
- [ ] Logs scrubbed of PII and sensitive data

### Observability & Logging
- [ ] Structured logging configured with safe fields only
- [ ] PII redacted from logs (Input, Output, Variables)
- [ ] Trace sampling configured appropriately (see DOC-016)
- [ ] Log retention policy defined (90 days recommended)
- [ ] Security events logged (auth failures, token usage, resume attempts)

### Clustering & Distributed Runtime
- [ ] Distributed locking configured (`UseDistributedRuntime()`)
- [ ] Lock provider credentials secured (Redis/PostgreSQL)
- [ ] Database user permissions follow least-privilege principle
- [ ] Quartz clustering enabled for scheduled tasks (multi-node)
- [ ] All nodes connect to same lock provider and database

### Authorization
- [ ] Role-based access control (RBAC) implemented
- [ ] Studio access restricted to authorized users
- [ ] API endpoints protected with appropriate policies
- [ ] Service accounts use least-privileged roles

### Infrastructure
- [ ] Firewall rules configured (allow only necessary ports)
- [ ] Database network isolated (not publicly accessible)
- [ ] Redis/lock store network isolated
- [ ] Container images scanned for vulnerabilities
- [ ] Dependencies updated regularly (security patches)

---

## Observability & Monitoring

### Security-Relevant Metrics

Monitor these metrics for security anomalies:
- **Authentication Failures:** Spike indicates brute-force attempts
- **Resume Token Errors:** High rate of invalid-token or no-op resume attempts may indicate token enumeration
- **Rate Limit Hits:** Track which IPs/tokens are rate-limited
- **Workflow Cancellations:** Unusual patterns may indicate attacks

### Tracing and diagnostics

Enable distributed tracing to track security-relevant events:

**Reference:** `Elsa.Workflows.Core/Telemetry/WorkflowInstrumentation.cs` and `Elsa.Diagnostics.OpenTelemetry/*`

```csharp
using Elsa.Workflows.Telemetry;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
        tracing.AddSource(WorkflowInstrumentation.ActivitySourceName);
        tracing.AddOtlpExporter(options => options.Endpoint = new Uri("http://jaeger:4317"));
    });
```

**Security Event Tracing:**
- Authentication attempts (success/failure)
- Resume token validation
- Workflow instance modifications
- Permission checks

For full monitoring setup, see [Monitoring & Observability](../../operate/monitoring-observability.md).

---

## Troubleshooting Security Issues

### Common Issues and Solutions

#### Token Invalid or Expired

**Symptom:** Resume requests fail validation or return a client error

**Diagnosis:**
1. Check whether the resume URL contains the `t` query-string token
2. Verify clock synchronization across nodes (use NTP)
3. Confirm token signing key matches between issue and validation

**Fix:**
- Generate bookmark URLs with an appropriate lifetime or absolute expiration
- Ensure all nodes use same signing key
- Regenerate token if signing key rotated

#### CORS Blocked

**Symptom:** Browser console shows CORS error, API call fails

**Diagnosis:**
1. Check browser DevTools Network tab for preflight (OPTIONS) request
2. Verify response headers include `Access-Control-Allow-Origin`
3. Confirm origin is in whitelist

**Fix:**
```csharp
// Add origin to CORS policy
policy.WithOrigins("https://new-app.com");
```

#### Rate Limit Exceeded

**Symptom:** Resume requests return 429 Too Many Requests

**Diagnosis:**
1. Check rate limit configuration in ingress or middleware
2. Identify source IP in logs
3. Determine if legitimate spike or attack

**Fix:**
- Whitelist trusted IPs if legitimate traffic
- Adjust rate limits based on actual usage patterns
- Implement token bucket algorithm for burst tolerance

For more troubleshooting guidance, see [Troubleshooting Guide](../troubleshooting/README.md) (DOC-017).

---

## Related Documentation

- [Authentication & Authorization Guide](../authentication.md) - Detailed OIDC and API key setup
- [Clustering Guide](../clustering/README.md) (DOC-015) - Distributed runtime security
- [Troubleshooting Guide](../troubleshooting/README.md) (DOC-017) - Diagnosing security issues
- [Monitoring & Observability](../../operate/monitoring-observability.md) - Security metrics and alerting
- [Workflow Patterns Guide](../patterns/README.md) (DOC-018) - Secure workflow design

---

## Diagram Placeholders

_Note: Diagrams to be added in future updates. Suggested diagrams:_
- Identity flow: Client → Elsa API → OIDC Provider
- Resume token lifecycle: Generation → Validation → Bookmark resume
- Network architecture: Ingress → Load Balancer → Elsa Nodes → Database
- Studio SSO flow: Browser → Studio → OIDC Provider → Elsa API

---

**Last Updated:** 2025-11-25  
**Document ID:** DOC-020
