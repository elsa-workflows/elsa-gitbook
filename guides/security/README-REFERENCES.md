# Security & Hardening Source References

This maintainer reference records the Elsa 3.8 source areas that ground the
Security & Hardening section. Paths are relative to the `elsa-core` repository.

## Workflow Ingress

| Source | Documentation owner |
| --- | --- |
| `src/modules/Elsa.Http/Activities/HttpEndpoint.cs` | `http-endpoint-security.md` |
| `src/modules/Elsa.Http/Middleware/HttpWorkflowsMiddleware.cs` | `http-endpoint-security.md` |
| `src/modules/Elsa.Http/Bookmarks/HttpEndpointBookmarkPayload.cs` | `http-endpoint-security.md` |

## Bookmark Resume Tokens

| Source | Documentation owner |
| --- | --- |
| `src/modules/Elsa.Http/Extensions/BookmarkExecutionContextExtensions.cs` | `bookmark-resume-tokens.md` |
| `src/modules/Elsa.Workflows.Api/Endpoints/Bookmarks/Resume/Endpoint.cs` | `bookmark-resume-tokens.md` |
| `src/modules/Elsa.Workflows.Core/Models/CreateBookmarkArgs.cs` | `bookmark-resume-tokens.md` |
| `src/modules/Elsa.Workflows.Core/Contexts/ActivityExecutionContext.cs` | `bookmark-resume-tokens.md` |
| `src/modules/Elsa.Workflows.Runtime/Services/WorkflowResumer.cs` | `bookmark-resume-tokens.md` |

## Secrets

The canonical implementation references for Elsa Secrets are maintained in
`secrets-management.md`. Authentication credential ownership is documented
separately under `../authentication/README-REFERENCES.md`.

## Maintenance Rules

- Verify behavior against the target release branch, not only the default
  branch.
- Keep authentication setup out of Security & Hardening; link to the
  Authentication & Authorization section instead.
- Keep Elsa API permissions separate from `HttpEndpoint` authorization.
- Treat callback tokens and credentials as secrets in examples, logs, and
  screenshots.
- Update navigation and cross-links whenever a canonical owner changes.
