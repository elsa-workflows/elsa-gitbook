# Source File References (DOC-020)

This file lists the exact source file paths referenced in the Security & Authentication Guide for maintainers to verify grounding. All paths are relative to the respective repository roots.

---

## elsa-core Repository

| File Path | Purpose | Referenced In |
|-----------|---------|---------------|
| `src/apps/Elsa.Server.Web/Program.cs` | Demonstrates `UseIdentity`, `identity.TokenOptions`, configuration-based user/application/role providers, and `UseDefaultAuthentication`. | Identity & Authentication in Elsa Server |
| `src/modules/Elsa.Http/Extensions/BookmarkExecutionContextExtensions.cs` | Provides `GenerateBookmarkTriggerUrl(bookmarkId)`, plus lifetime and absolute-expiration overloads, for creating tokenized HTTP resume URLs. | Tokenized Bookmark Resume URLs |
| `src/modules/Elsa.Workflows.Api/Endpoints/Bookmarks/Resume/Endpoint.cs` | Exposes the anonymous `GET`/`POST /bookmarks/resume?t=...` endpoint and resumes using the decrypted bookmark token payload. | Tokenized Bookmark Resume URLs |
| `src/modules/Elsa.Workflows.Core/Contexts/ActivityExecutionContext.cs` | Contains `CreateBookmark(CreateBookmarkArgs)` method for creating bookmarks with payloads, callbacks, and auto-burn settings. Central to bookmark lifecycle. | Tokenized Bookmark Resume URLs (Single-Use Semantics) |
| `src/modules/Elsa.Workflows.Runtime/Services/WorkflowResumer.cs` | Implements distributed locking for bookmark resume operations and resumes matching bookmarks. | Tokenized Bookmark Resume URLs (Revocation Strategy, Distributed Locking) |
| `src/modules/Elsa.Workflows.Runtime/Models/BookmarkTokenPayload.cs` | Defines the bookmark token payload as `BookmarkId` and `WorkflowInstanceId`. | Tokenized Bookmark Resume URLs |
| `src/modules/Elsa.Identity/*` | Identity module containing JWT/API-key authentication, token options, and user/application/role providers. | Identity & Authentication in Elsa Server |

---

## elsa-extensions Repository

| File Path | Purpose | Referenced In |
|-----------|---------|---------------|
| `src/modules/diagnostics/Elsa.OpenTelemetry/*` | OpenTelemetry integration package containing tracing middleware and `ActivitySource` definitions for distributed tracing. Adds spans for workflow and activity execution. | Observability & Monitoring (Tracing with Elsa.OpenTelemetry) |
| `src/Elsa.OpenTelemetry/Middleware/TracingMiddleware.cs` | HTTP middleware that adds tracing spans for workflow API calls. Useful for tracking security-relevant events like authentication attempts and resume token validation. | Observability & Monitoring |
| `src/Elsa.OpenTelemetry/Extensions/ServiceCollectionExtensions.cs` | Provides `UseOpenTelemetry()` extension for configuration. Enables tracing integration for security auditing and monitoring. | Observability & Monitoring |

---

## Key Security-Related Interfaces and Types

### Authentication & Identity

| Interface/Type | Location | Purpose |
|----------------|----------|---------|
| `IUserProvider` | `Elsa.Identity.Contracts` | Abstraction for loading users from stores or configuration |
| `IApplicationProvider` | `Elsa.Identity.Contracts` | Abstraction for loading API clients/applications from stores or configuration |
| `IRoleProvider` | `Elsa.Identity.Contracts` | Abstraction for loading roles and permissions from stores or configuration |
| `IdentityTokenOptions` | `Elsa.Identity.Options` | Configuration for token lifetimes, issuer, audience, signing key, and tenant claim type |
| `ApiKeyOptions` | `AspNetCore.Authentication.ApiKey` | Configuration for API-key authentication in the `Authorization` header |

### Bookmark & Resume

| Interface/Type | Location | Purpose |
|----------------|----------|---------|
| `CreateBookmarkArgs` | `Elsa.Workflows.Core.Models` | Arguments for bookmark creation including AutoBurn, payload, and callbacks |
| `BookmarkTokenPayload` | `Elsa.Workflows.Runtime` | Token payload containing `BookmarkId` and `WorkflowInstanceId` |
| `ResumeBookmarkResult` | `Elsa.Workflows.Runtime.Models` | Result of bookmark resume operation with success/failure status |
| `IWorkflowResumer` | `Elsa.Workflows.Runtime.Contracts` | Runtime service used by Elsa internals to resume bookmarked workflows |

### Distributed Locking

| Interface/Type | Location | Purpose |
|----------------|----------|---------|
| `IDistributedLockProvider` | `Medallion.Threading` (external) | Acquires distributed locks for concurrent access control during resume operations |

---

## Related Documentation Files

| This Repository Path | Description |
|---------------------|-------------|
| `guides/authentication.md` | Detailed authentication and authorization guide with OIDC provider setup |
| `guides/clustering/README.md` | DOC-015: Clustering configuration, distributed locking, and multi-node security |
| `guides/troubleshooting/README.md` | DOC-017: Troubleshooting guide for diagnosing auth, token, and endpoint issues |
| `guides/patterns/README.md` | DOC-018: Workflow patterns including secure resumption patterns |
| `guides/security/examples/appsettings-identity.json` | Example identity configuration with API key and JWT setup |
| `guides/security/examples/resume-endpoint-notes.md` | Detailed security best practices for tokenized resume URLs |
| `guides/security/examples/ingress-cors-snippet.md` | Ingress and CORS configuration examples for production |

---

## External Dependencies (Security-Relevant)

| Package | Purpose | Security Considerations |
|---------|---------|-------------------------|
| `Medallion.Threading` | Distributed locking abstraction (Redis, PostgreSQL, SQL Server) | Lock provider credentials must be secured; use encrypted connections |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT token validation middleware | Configure appropriate token lifetimes and signing algorithms (RS256 recommended) |
| `Microsoft.AspNetCore.Authentication.OpenIdConnect` | OIDC integration | Validate OIDC provider certificates; use PKCE for authorization code flow |
| `AspNetCoreRateLimit` | Application-level rate limiting | Configure appropriate limits to prevent DoS while allowing legitimate traffic |
| `OpenTelemetry` | Observability and tracing framework | Ensure traces don't contain PII; configure sampling for production |

---

## Configuration File References

### appsettings.json Structure

**Identity Configuration:**
```json
{
  "Identity": {
    "Tokens": {
      "SigningKey": "...",
      "AccessTokenLifetime": "01:00:00",
      "RefreshTokenLifetime": "7.00:00:00"
    },
    "Users": [],
    "Applications": [],
    "Roles": []
  }
}
```

**Note:** `Program.cs` binds `Identity:Tokens` to `identity.TokenOptions` and the full `Identity` section to configuration-based user, application, and role providers. Bookmark resume token payloads contain only bookmark and workflow instance IDs; generate them with a lifetime or absolute expiration when time-boxing is required.

See [examples/appsettings-identity.json](examples/appsettings-identity.json) for complete example.

---

## Security Best Practices Grounding

### TTL and Token Expiration

**Grounded in:** `src/modules/Elsa.Identity/Options/IdentityTokenOptions.cs` and `src/modules/Elsa.Http/Extensions/BookmarkExecutionContextExtensions.cs`

Identity access and refresh tokens use `IdentityTokenOptions`. Bookmark resume URLs can be generated with lifetime or absolute-expiration overloads. Best practices:
- Access tokens: 1 hour (short-lived, refresh as needed)
- Refresh tokens: 7 days (balance security and UX)
- Bookmark resume tokens: shortest lifetime that fits the workflow use case

### Distributed Locking for Resume

**Grounded in:** `src/modules/Elsa.Workflows.Runtime/Services/WorkflowResumer.cs`

The `WorkflowResumer` acquires a distributed lock before resuming a workflow:

```csharp
// Conceptual flow (simplified)
var lockKey = $"workflow:{workflowInstanceId}";
await using var lockHandle = await _distributedLockProvider.AcquireLockAsync(lockKey, timeout);

if (lockHandle == null)
{
    // Another node is already processing this resume
    return ResumeWorkflowResult.AlreadyInProgress();
}

// Safe to resume - we hold the lock
```

This prevents concurrent resume attempts from corrupting workflow state.

### Bookmark AutoBurn

**Grounded in:** `src/modules/Elsa.Workflows.Core/Middleware/Activities/DefaultActivityInvokerMiddleware.cs`

The `DefaultActivityInvokerMiddleware` implements "bookmark burning":
- When a bookmark is consumed successfully, it's deleted from the store
- This prevents the same bookmark from being used twice (replay protection)
- Controlled by the `AutoBurn` property in `CreateBookmarkArgs`

---

## Repository Links

- **elsa-core**: https://github.com/elsa-workflows/elsa-core
- **elsa-extensions**: https://github.com/elsa-workflows/elsa-extensions

---

## Maintenance Notes

When updating the Security & Authentication Guide, verify:

1. **File paths remain accurate** after elsa-core or elsa-extensions refactoring
2. **Method signatures match** current implementation (e.g., `UseIdentity`, `GenerateBookmarkTriggerUrl`)
3. **Configuration structure is current** (`Identity:Tokens`, `Users`, `Applications`, `Roles`)
4. **New security features are documented** (e.g., new authentication providers, token types)
5. **Deprecated patterns are removed** or marked as such

### Verification Steps

To verify references are still valid:

```bash
# Clone elsa-core
git clone https://github.com/elsa-workflows/elsa-core.git
cd elsa-core

# Check file exists
test -f src/apps/Elsa.Server.Web/Program.cs && echo "✅ Program.cs exists"
test -f src/modules/Elsa.Http/Extensions/BookmarkExecutionContextExtensions.cs && echo "✅ BookmarkExecutionContextExtensions.cs exists"

# Check for key methods
grep -q "UseIdentity" src/apps/Elsa.Server.Web/Program.cs && echo "✅ UseIdentity found"
grep -q "GenerateBookmarkTriggerUrl" src/modules/Elsa.Http/Extensions/BookmarkExecutionContextExtensions.cs && echo "✅ GenerateBookmarkTriggerUrl found"
grep -q "Routes(\"/bookmarks/resume\")" src/modules/Elsa.Workflows.Api/Endpoints/Bookmarks/Resume/Endpoint.cs && echo "✅ Bookmark resume route found"
```

---

## Cross-References to Other DOC-* Guides

| Guide | Document ID | Security-Relevant Content |
|-------|-------------|---------------------------|
| Clustering Guide | DOC-015 | Distributed lock provider security, database permissions, Redis/lock store credentials |
| Troubleshooting Guide | DOC-017 | Diagnosing authentication failures, token invalid/expired, CORS issues |
| Workflow Patterns | DOC-018 | Secure workflow design patterns, bookmark usage patterns |
| Monitoring Guide | DOC-016 (TBD) | Security metrics, authentication event tracking, anomaly detection |

---

**Last Updated:** 2025-11-25  
**Maintained By:** Elsa Documentation Team
