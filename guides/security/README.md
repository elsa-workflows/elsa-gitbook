---
description: >-
  Secure Elsa hosts, workflow ingress, bookmark resume URLs, and Studio
  deployments with focused production hardening guidance.
---

# Security & Hardening

This section covers the security controls around an Elsa deployment: how
traffic reaches it, how workflow callbacks are protected, how sensitive values
are handled, and how to operate the deployment safely in production.

Authentication and authorization answer a different question: **who may call
Elsa and what may they do?** For Elsa Identity, API keys, OpenID Connect,
permissions, and External Authentication, start with
[Authentication & Authorization](../authentication/README.md).

## Security Guides

| Guide | Use it when you need to... |
| --- | --- |
| [HTTP Endpoint Security](http-endpoint-security.md) | Protect workflow routes created by the `HttpEndpoint` activity, including public endpoints and ASP.NET Core policies. |
| [Bookmark Resume Tokens](bookmark-resume-tokens.md) | Send or receive tokenized callback URLs that resume waiting workflows. |
| [Production Hardening](production-hardening.md) | Configure browser boundaries, ingress, TLS, Studio deployment, monitoring, and operational checks. |
| [Secrets Management](secrets-management.md) | Store and resolve named values from workflows and Elsa modules. |

## Scope and Boundaries

Security controls complement authentication; they do not replace it.

- Secure **workflow ingress** separately from Elsa API access. A public
  `HttpEndpoint` does not make the Elsa API public, and Elsa API permissions
  do not automatically protect a workflow route. See [HTTP endpoint
  security](http-endpoint-security.md).
- Treat a bookmark resume URL as a bearer capability. Give it an appropriate
  lifetime, do not log its token, and apply controls appropriate to the
  callback's risk. See [Bookmark resume tokens](bookmark-resume-tokens.md).
- Keep host and infrastructure secrets outside source control. For values that
  workflows must resolve, use the [Secrets management](secrets-management.md)
  module and protect its encryption key.
- Apply network, transport, browser, and deployment controls before exposing
  Elsa or Studio to untrusted networks. See [Production
  hardening](production-hardening.md).

## Related documentation

- [Authentication & Authorization](../authentication/README.md)
- [Permissions reference](../authentication/permissions.md)
- [Direct OpenID Connect](../authentication/direct-openid-connect.md)
- [External Authentication](../authentication/external-authentication/README.md)
- [Clustering](../clustering/README.md)
- [Monitoring & Observability](../../operate/monitoring-observability.md)
