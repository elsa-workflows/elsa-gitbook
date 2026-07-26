---
description: >-
  Configure Elsa's optional external-authentication broker and administer
  OpenID Connect connections from Elsa Studio in Elsa 3.8.
---

# Brokered External Authentication

Elsa 3.8 includes an optional, Elsa-owned authentication broker for Studio
users who sign in through one or more upstream OpenID Connect providers. The
broker is useful when provider connections, login methods, identity links, and
session revocation must be managed by Elsa rather than hard-coded into the
Studio host.

Use the existing [External Identity Providers](external-identity-providers.md)
guide when Elsa Server only needs to validate bearer tokens issued by an
external provider. Use this guide when Elsa should broker the authorization
code flow and expose provider administration in Studio.

## How the broker fits

The broker separates responsibilities across the deployment:

<!-- markdownlint-disable MD013 -->
| Component | Responsibility |
| --- | --- |
| Elsa Server broker | Discovers login methods, validates the client and callback, runs the upstream authorization-code flow, resolves the Elsa user, and issues Elsa tokens. |
| OpenID Connect adapter | Resolves provider metadata, enforces issuer/signature/audience/nonce checks, and projects only allowlisted claims. |
| Elsa Studio | Presents local and external login methods and provides permission-gated connection, identity-link, preview, test, and session screens. |
| Upstream provider | Authenticates the person. It does not issue the Elsa API token used by Studio after the broker exchange. |
<!-- markdownlint-enable MD013 -->

Brokered authentication is opt-in. Set Studio's `Authentication:Provider` to
`ExternalAuthentication`; do not register it alongside direct OpenID Connect or
the legacy login module. Direct OIDC remains available when provider
configuration belongs entirely to the Studio host.

## Configure Elsa Server

Install the `Elsa.ExternalAuthentication` and
`Elsa.ExternalAuthentication.OpenIdConnect` packages. Register the broker and
the OpenID Connect adapter in the server composition root:

```csharp
using Elsa.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddElsa(elsa =>
{
    // Keep the server's existing UseIdentity(...), persistence, and runtime modules.
    elsa.UseExternalAuthentication(feature =>
    {
        feature.ConfigureOptions = options =>
            builder.Configuration.GetSection("ExternalAuthentication").Bind(options);
    });
    elsa.UseWorkflowsApi();
});

builder.Services.AddOpenIdConnectExternalAuthentication();
```

The broker's endpoints are under `/external-authentication`. Put the server
API's normal middleware in the pipeline as usual:

```csharp
var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseWorkflowsApi();
app.Run();
```

`ExternalAuthentication:Redirects:ExternalCallbackBaseUri` is required for
upstream callbacks. It is the public origin used by Elsa to derive callbacks;
do not configure a different callback URL per connection. With the default Elsa
API prefix, an OpenID Connect connection named `contoso` uses:

```text
https://elsa.example/elsa/api/external-authentication/callback/contoso
https://elsa.example/elsa/api/external-authentication/logout/callback/contoso
```

Register the callback that applies to the flow with the upstream provider.
Preview uses a separate callback containing the connection record ID and must
also be registered when administrator preview is enabled.

### Use durable state for multiple server nodes

`AddExternalAuthenticationServices` supplies in-memory stores suitable for
single-node development. For a multi-node deployment, add the EF Core
integration and configure every node consistently:

```csharp
builder.Services.AddExternalAuthenticationEntityFrameworkCore();
```

The durable integration stores broker transactions, authorization grants,
sessions, connection revisions, observations, identity links, and related
state in the identity database. Share ASP.NET Core Data Protection keys across
nodes and set the same `ExternalAuthentication:HandleHashing:SharedKeyBase64`
value on every node. Without shared stores, keys, and hashing configuration, a
callback or refresh request can land on a node that cannot consume the state
created by another node.

## Register the Studio broker client

The server must register each Studio host as an enabled broker client. A Server
host is confidential and keeps its secret in deployment configuration. A WASM
host is public and must not have a client secret.

The following server-side configuration illustrates the client contract. The
callback paths are fixed by the released Studio modules:

```json
{
  "ExternalAuthentication": {
    "Redirects": {
      "ExternalCallbackBaseUri": "https://elsa.example/elsa/api"
    },
    "AuthenticationClients": [
      {
        "clientId": "elsa-studio-server",
        "displayName": "Elsa Studio Server",
        "clientType": "confidential",
        "callbackUris": [
          "https://studio.example/authentication/external/callback"
        ],
        "logoutCallbackUris": [
          "https://studio.example/authentication/external/logout-callback"
        ],
        "allowedOrigins": ["https://studio.example"],
        "allowedReturnPathPrefixes": ["/"],
        "secretBinding": {
          "ownership": "external",
          "resolverType": "configuration",
          "reference": "Secrets:ExternalAuthentication:StudioServerClientSecret"
        },
        "isEnabled": true
      }
    ]
  },
  "Secrets": {
    "ExternalAuthentication": {
      "StudioServerClientSecret": "set-outside-source-control"
    }
  }
}
```

Configuration-owned clients and connections are deployment-owned. Studio can
display them, but it does not edit their binding references or secret values.
Use an environment variable, mounted configuration, or a cloud secret
provider for the referenced secret.

## Configure an OpenID Connect connection

Register the adapter and define a connection under
`ExternalAuthentication:Connections`. Discovery mode is the recommended
trust mode. The `clientSecret` value is a secret binding, not a value embedded
in `adapterSettings`:

```json
{
  "ExternalAuthentication": {
    "Connections": [
      {
        "key": "contoso",
        "adapterType": "openid-connect",
        "adapterSettingsVersion": 2,
        "adapterSettings": {
          "mode": "discovery",
          "discoveryUrl": "https://login.contoso.example/.well-known/openid-configuration",
          "clientId": "elsa-broker",
          "clientAuthenticationMethod": "client_secret_basic",
          "scopes": ["openid", "profile", "email"],
          "providerPkce": "required"
        },
        "secretBindings": {
          "clientSecret": {
            "ownership": "external",
            "resolverType": "configuration",
            "reference": "Secrets:ExternalAuthentication:ContosoClientSecret"
          }
        },
        "displayName": "Contoso",
        "isEnabled": true,
        "isPreferred": true
      }
    ]
  }
}
```

The adapter always adds the `openid` scope and always uses S256 PKCE for the
upstream authorization-code flow. Manual trust is available, but requires an
issuer, authorization endpoint, token endpoint, and either a JWKS URI or
pinned signing keys. Provider endpoints must be HTTPS unless the deployment
explicitly relaxes the egress policy.

For least privilege, configure each connection's claim projection and
permission grant sources deliberately. External claims are not automatically
Elsa permissions. The default unlinked-identity policy is `reject`; enabling
`create-user` provisions a credentialless Elsa user and can assign configured
default roles. A `match-user` policy is available only when the deployment
installs a matching extension.

## Configure Elsa Studio

Add the `Elsa.Studio.ExternalAuthentication` module and the host-specific
broker package. The stock Server and WASM hosts already register the management
module; the broker login path becomes active only when the provider is selected.

### Blazor Server Studio

Use a confidential client. The client secret stays on the server and the
browser receives an HTTP-only Studio session rather than the broker refresh
token:

```json
{
  "Authentication": {
    "Provider": "ExternalAuthentication",
    "ExternalAuthentication": {
      "ClientId": "elsa-studio-server",
      "ClientSecret": "set-via-secret-store",
      "CallbackPath": "/authentication/external/callback",
      "LogoutCallbackPath": "/authentication/external/logout-callback"
    }
  }
}
```

### Blazor WebAssembly Studio

Use a public client. Do not put a client secret in the WASM configuration. The
released host defaults to memory-only browser credentials; `Session` and
`Durable` storage are explicit alternatives that display a security warning:

```json
{
  "Authentication": {
    "Provider": "ExternalAuthentication",
    "ExternalAuthentication": {
      "ClientId": "elsa-studio-wasm",
      "CallbackPath": "/authentication/external/callback",
      "LogoutCallbackPath": "/authentication/external/logout-callback",
      "BrowserStorage": "Memory"
    }
  }
}
```

Both host models use the exact callback paths shown above. The Server host
keeps PKCE state in a protected HTTP-only cookie. The WASM host creates PKCE
state in the browser and validates that broker redirects return to the exact
configured callback and Elsa backend origin.

## Administer the broker in Studio

After the backend feature and Studio module are available, authorized users can
use these surfaces:

- **SSO connections** (`/settings/sso-connections`) to list connections, test
  current provider settings, preview sign-in, and manage database-owned
  connections. `/security/external-authentication` remains a compatibility
  alias.
- **External identity links**
  (`/security/external-authentication/identity-links`) to find users and
  prelink or unlink an issuer/subject identity within the current tenant.
- **External sessions**
  (`/security/external-authentication/sessions`) to inspect safe session
  metadata and revoke active sessions when session administration is enabled.

Menus are usability affordances; the Elsa API remains authoritative. The main
administrative permissions are:

<!-- markdownlint-disable MD013 -->
| Capability | Permission |
| --- | --- |
| View connections and safe test/descriptor data | `external-authentication:connections:read` |
| Create or update database-owned connections | `external-authentication:connections:create`, `external-authentication:connections:update` |
| Archive or restore connections | `external-authentication:connections:archive` |
| Run tests or preview sign-in | `external-authentication:connections:test`, `external-authentication:connections:preview` |
| Configure policies or assign default roles | `external-authentication:policies:manage`, `external-authentication:roles:assign` |
| Prelink or unlink external identities | `external-authentication:links:manage` |
| Read or revoke sessions | `external-authentication:sessions:read`, `external-authentication:sessions:revoke` |
<!-- markdownlint-enable MD013 -->

Connection management is host-scoped in the released API: create and update
requests must use host scope. Identity links and session queries are restricted
to the current tenant. Host-wide connections can therefore be used by each
tenant while links and sessions remain tenant-specific.

Configuration-owned connections are read-only in Studio. Database-owned
connections use revision checks for updates, and changing the final enabled
login path requires a recovery method or the privileged override permission.

## Security and operations checklist

- Keep `ExternalAuthentication:Redirects:ExternalCallbackBaseUri` and all
  registered callback URIs under deployment control.
- Leave provider HTTPS, private-network destination blocking, local return-path
  validation, and S256 PKCE enabled unless there is a reviewed exception.
- Treat the default `reject` unlinked-identity policy as the safe starting
  point; review every role assigned by `create-user`.
- Use memory-only WASM credentials on shared or unmanaged devices. Persistent
  browser storage increases exposure to XSS and browser compromise.
- Local logout is independent of upstream availability. Upstream logout is
  disabled by default and should be enabled only for providers and connections
  that support it.
- Do not expect Preview Sign-in to create a user, identity link, credential, or
  normal session. It returns a short-lived, redacted result for administrators.
- Monitor the optional `external-authentication` health check separately; it is
  not a readiness dependency by default.

## Troubleshooting

### Studio does not show external login methods

Check that the Studio provider is `ExternalAuthentication`, the matching
`AuthenticationClients` entry is enabled, the client ID matches exactly, and at
least one connection is enabled for the current tenant scope.

### The callback is rejected

Verify the exact callback URI registered in the server client, the exact Studio
callback path, the public callback base URI, and the upstream provider redirect
registration. The broker rejects non-local return paths and requires S256 PKCE.

### Studio can sign in but API calls fail

Check the server API URL, the server's Elsa authentication middleware, and the
permissions granted by the connection's configured grant sources. Provider
roles or claims do not become Elsa permissions unless a selected grant source
projects them and the deployment allow/deny boundary permits them.

### A multi-node callback or refresh fails intermittently

Verify EF Core external-authentication persistence, shared Data Protection keys,
the same `HandleHashing:SharedKeyBase64` on every node, and a common identity
database. Also confirm that all nodes use the same connection material and
adapter/secret configuration.

## Related guides and source

- [External Identity Providers](external-identity-providers.md) for direct
  bearer-token validation and non-brokered Studio OIDC.
- [Elsa API Permissions](permission-reference.md) for the broader Elsa API
  permission model.
- [Studio Integration](../studio/integration/README.md) for host models and
  custom-elements embedding.
- [Elsa External Authentication source](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.ExternalAuthentication/README.md)
- [Elsa OpenID Connect adapter source](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.ExternalAuthentication.OpenIdConnect/README.md)
- [Elsa Studio External Authentication source](https://github.com/elsa-workflows/elsa-studio/blob/release/3.8.0/src/modules/Elsa.Studio.ExternalAuthentication/README.md)
