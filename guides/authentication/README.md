---
description: >-
  Choose and configure authentication and authorization for Elsa Server,
  Elsa Studio, users, and API clients.
---

# Authentication & Authorization

Authentication establishes who a caller is. Authorization determines what
that caller may do. In an Elsa deployment, those decisions span Elsa Server,
Elsa Studio, and any workflow endpoints exposed by the `HttpEndpoint`
activity.

Use this section to choose an authentication topology and configure access to
the Elsa API. Use [Security & Hardening](../security/README.md) for secrets,
workflow ingress, bearer-style resume URLs, TLS, rate limiting, and production
controls.

## Choose an authentication path

| Scenario | Start here |
| --- | --- |
| Elsa manages users, roles, tokens, and API applications | [Elsa Identity](elsa-identity.md) |
| A service or automation client calls the Elsa API | [API Keys](api-keys.md) |
| Studio signs in directly with an upstream OpenID Connect provider | [Direct OpenID Connect](direct-openid-connect.md) |
| Elsa brokers one or more upstream providers and manages connections, identity links, and sessions | [External Authentication](external-authentication/README.md) |
| The host supplies another ASP.NET Core authentication scheme | [Custom Authentication](custom-authentication.md) |
| Authentication must be disabled for an isolated local environment | [Disable Authentication in Development](disable-authentication.md) |

{% hint style="info" %}
External Authentication is available in the stable Elsa 3.8.0 Core and Studio
packages. Install the matching package family from NuGet.org.
{% endhint %}

Direct OpenID Connect and External Authentication are different topologies.
External Authentication is the strategic successor for new deployments that
need Elsa-managed provider connections and sessions. Direct OIDC remains
supported throughout Elsa 3.x and is not formally deprecated in Elsa 3.8.

## Understand the security boundaries

### Elsa Server API

Elsa API endpoints authenticate through ASP.NET Core and authorize through
Elsa `permissions` claims. Elsa Identity issues those claims from assigned
roles. External schemes must provide them directly or map trusted upstream
roles, groups, or scopes into them.

See [Elsa API Permissions](permissions.md) for the permission model, endpoint
families, and starter role templates.

### Elsa Studio

Studio is an API client, not a second authorization authority. Its selected
authentication provider signs the user in and obtains credentials for Elsa
Server. The server still decides whether each API operation is allowed.

A successful Studio login therefore does not guarantee access to workflow
definitions, instances, designer metadata, or administration screens. Missing
permissions normally surface as `403 Forbidden` responses from the API.

### Workflow HTTP endpoints

Routes exposed by the `HttpEndpoint` activity have their own `Authorize` and
`Policy` settings. They are separate from Elsa API permissions. A public
workflow route does not make `/elsa/api/*` public, and API permissions do not
secure a workflow route automatically.

See [HTTP Endpoint Security](../security/http-endpoint-security.md).

## Recommended reading paths

For an Elsa-managed deployment:

1. Configure [Elsa Identity](elsa-identity.md).
2. Define least-privilege roles with [Elsa API Permissions](permissions.md).
3. Add [API keys](api-keys.md) for service clients when needed.
4. Complete the [production hardening](../security/production-hardening.md)
   checklist.

For an external identity provider:

1. Choose [Direct OpenID Connect](direct-openid-connect.md) or
   [External Authentication](external-authentication/README.md).
2. Configure the Elsa API permission claims required by Studio and operators.
3. Review provider secrets, redirects, TLS, and deployment controls under
   [Security & Hardening](../security/README.md).

## Related sections

- [Security & Hardening](../security/README.md)
- [Elsa API & Client](../api-client/README.md)
- [Elsa Studio integration](../studio/integration/README.md)
- [Hosting Elsa in an existing app](../onboarding/hosting-elsa-in-existing-app.md)
