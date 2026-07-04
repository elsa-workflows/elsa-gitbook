---
description: >-
  Manage Elsa Server, Elsa Studio, and modular host configuration with
  appsettings, environment overrides, and source-backed section names for
  release 3.8.0.
---

# Configuration Management

This guide shows how Elsa 3.8 hosts read configuration, which section names
they actually bind in the release source, and where environment overrides fit.
It focuses on the shipped host patterns in `release/3.8.0`:

* `Elsa.Server.Web` in `elsa-core`
* `Elsa.ModularServer.Web` in `elsa-core`
* `Elsa.Studio.Host.Server` and `Elsa.Studio.Host.Wasm` in `elsa-studio`

Use this guide when you need to answer questions such as:

* Which settings belong in `appsettings.json` versus code?
* Which keys can I override with environment variables?
* Why did a setting not change anything?
* Which settings must stay aligned across Elsa Server, Studio, and my reverse proxy?

## How Elsa Reads Configuration

Elsa does not add a custom global configuration system on top of ASP.NET Core.
The shipped server hosts start with `WebApplication.CreateBuilder(args)`, and
the WASM Studio host starts with `WebAssemblyHostBuilder.CreateDefault(args)`.
After that, each host binds specific configuration sections into options inside
`Program.cs`.

That means two things:

1. A setting only works if the host or feature actually binds it.
2. Most overrides follow normal .NET configuration rules, including
   `appsettings.json`, environment-specific JSON files, user secrets, command
   line arguments, and environment variables for server-side hosts.

## The Three Main Host Shapes

### 1. Standalone Elsa Server (`AddElsa(...)`)

The sample `Elsa.Server.Web` host binds these release-backed sections:

| Section | Used for |
| --- | --- |
| `Http` | HTTP activity base URL, base path, content types, and related HTTP activity options |
| `Identity` | configuration-based users, applications, roles, and token settings |
| `Identity:Tokens` | signing key and token lifetime settings |
| `Multitenancy` | configuration-based tenants |
| `IngressRateLimiting` | fixed-window rate limit settings for Elsa APIs and HTTP workflow ingress |
| `Scripting:CSharp` | C# expression engine options |
| `Scripting:Python` | Python engine options such as DLL path and scripts |
| `DistributedRuntime:AllowLocalLockProviderInDistributedRuntime` | development/single-host override for distributed runtime locking |

The important constraint is that this host does **not** use a single `Elsa:*`
root. The sections above are bound explicitly in code.

### 2. Elsa Studio Hosts

The standalone Studio hosts bind a different set of sections:

| Section | Used for |
| --- | --- |
| `Backend` | Elsa Server API base URL |
| `Authentication:Provider` | selects `ElsaIdentity`, `OpenIdConnect`, or `ElsaLogin` depending on host |
| `Authentication:OpenIdConnect` | OIDC authority, client IDs, scopes, and token behavior |
| `Localization` | default culture and supported cultures |
| `Shell` | server-hosted Studio shell options |
| `DesignerOptions` | flowchart designer options such as `UseReactFlow` |

Set `Authentication:Provider` explicitly. The defaults differ by host:

* `Elsa.Studio.Host.Server` falls back to `ElsaIdentity` if the setting is
  missing.
* `Elsa.Studio.Host.Wasm` falls back to `OpenIdConnect` if the setting is
  missing.

### 3. Modular Elsa Server (`CShells`)

`Elsa.ModularServer.Web` uses shell-based configuration instead of the
standalone `AddElsa(...)` pattern. Its primary sections are:

| Section | Used for |
| --- | --- |
| `CShells:Shells:*` | per-shell feature enablement and feature settings |
| `Diagnostics:OpenTelemetry:Exporter` | OTLP endpoint and protocol |
| `Nuplane` | package feeds and autoload behavior |
| `Elsa:PlatformIntegration:ShellOverlayPath` | optional JSON overlay file path loaded at startup |

In this host, persistence, authentication, HTTP, secrets, and routing settings
live under shell feature names such as `Identity`, `FastEndpoints`,
`SqliteWorkflowPersistence`, `Http`, or `Secrets`.

## Configuration Map by App Type

### Standalone Elsa Server example

The `Elsa.Server.Web` sample app ships with settings shaped like this:

```json
{
  "Http": {
    "BaseUrl": "https://localhost:5001",
    "BasePath": "/workflows"
  },
  "Identity": {
    "Tokens": {
      "SigningKey": "CHANGE_ME_TO_A_SECURE_RANDOM_KEY"
    }
  },
  "Multitenancy": {
    "Tenants": []
  },
  "IngressRateLimiting": {
    "Enabled": false
  }
}
```

Use this shape when you host Elsa directly in your own ASP.NET Core app with
`builder.Services.AddElsa(...)`.

#### Identity signing key rules in 3.8.0

`Identity:Tokens:SigningKey` is validated in the released server code:

* it is required outside the `Development` and `Demo` environments
* it must not contain leading or trailing whitespace
* it must use printable ASCII characters only
* it must be at least 32 characters long
* known sample values such as `CHANGE_ME_TO_A_SECURE_RANDOM_KEY` are rejected
  outside development-oriented environments

### Elsa Studio example

The standalone Studio hosts expect settings shaped like this:

```json
{
  "Backend": {
    "Url": "https://localhost:5001/elsa/api"
  },
  "Authentication": {
    "Provider": "OpenIdConnect",
    "OpenIdConnect": {
      "Authority": "https://login.microsoftonline.com/<tenant>/v2.0",
      "ClientId": "<client-id>"
    }
  },
  "Localization": {
    "DefaultCulture": "en-US",
    "SupportedCultures": [ "en-US" ]
  },
  "DesignerOptions": {
    "UseReactFlow": false
  }
}
```

For Blazor WebAssembly, these values usually live in
`wwwroot/appsettings.json`. Browser clients do not read server environment
variables directly, so production overrides usually happen during build,
publish, or host-page generation.

#### OIDC defaults in 3.8.0 Studio hosts

When `Authentication:Provider` is `OpenIdConnect`, the released Studio hosts
apply a few defaults that are easy to miss:

* if `AuthenticationScopes` is empty, Studio restores `openid`, `profile`, and
  `offline_access`
* `GetClaimsFromUserInfoEndpoint` defaults to `false`
* the Blazor Server host defaults callback paths to `/signin-oidc` and
  `/signout-callback-oidc` when you do not set them explicitly

### Modular server example

The modular server sample uses shell feature configuration like this:

```json
{
  "CShells": {
    "Shells": {
      "Default": {
        "Features": {
          "Identity": {
            "SigningKey": "CHANGE_ME_TO_A_SECURE_RANDOM_KEY"
          },
          "FastEndpoints": {
            "GlobalRoutePrefix": "elsa/api"
          },
          "SqliteWorkflowPersistence": {
            "ConnectionString": "Data Source=elsa_workflows.db;Cache=Shared"
          },
          "Http": {
            "HttpActivityOptions": {
              "BaseUrl": "https://localhost:5001"
            }
          }
        }
      }
    }
  }
}
```

Use this model when you need multiple shells, per-shell routing, or feature
configuration that can vary by shell.

## Environment Variables and Overrides

For server-side hosts, normal .NET environment variable mapping applies:

* `:` in configuration paths becomes `__`
* keys are case-insensitive on the .NET side
* later providers override earlier providers

Examples:

```bash
Http__BaseUrl=https://workflows.example.com
Http__BasePath=/workflows
Identity__Tokens__SigningKey=<strong-random-key>
IngressRateLimiting__Enabled=true
IngressRateLimiting__ApiPermitLimit=300
Backend__Url=https://workflows.example.com/elsa/api
Authentication__Provider=OpenIdConnect
ConnectionStrings__PostgreSql=Host=db;Database=elsa;Username=elsa;Password=secret
```

Prefer environment variables, user secrets, or a secret manager for:

* signing keys
* API keys and client secrets
* database credentials
* secrets encryption keys

Do not commit production secrets into `appsettings.json`.

## Settings That Must Stay Aligned

### Public URLs

If Elsa is exposed behind a reverse proxy or ingress, keep these consistent:

* `Http:BaseUrl` should reflect the public Elsa Server URL.
* `Backend:Url` in Studio should point to the public Elsa API URL, usually
  ending in `/elsa/api`.
* Reverse proxy rules must preserve the same public API path that Studio uses.

If these drift apart, generated links, callbacks, or Studio API calls can fail
even when the application itself starts correctly.

### HTTP workflow base path versus Elsa API path

These are separate concerns:

* `Http:BasePath` controls where `HttpEndpoint` activity routes are mounted.
* Elsa API endpoints use `ApiEndpointOptions.RoutePrefix`, whose default is
  `elsa/api`.

In 3.8, `HttpActivityOptions.ApiRoutePrefix` is obsolete. Do not treat
`Http:ApiRoutePrefix` as the source of truth for Elsa API routing in new host
code.

### Authentication mode

Studio and Server must agree on the authentication story:

* If Studio uses `OpenIdConnect`, Elsa Server must trust the same identity
  provider and scopes.
* If Studio uses `ElsaIdentity` or `ElsaLogin`, Elsa Server must expose the
  corresponding identity endpoints and configured users or applications.

## Migrations and Persistence Toggles

Database connection strings alone do not switch persistence providers. The host
must also call the provider-specific Elsa persistence configuration methods.

For EF Core persistence, automatic migrations are controlled on the persistence
features through `RunMigrations`, not through a built-in global
`Elsa:AutoMigrate` setting.

That distinction matters because:

* changing only `ConnectionStrings:*` does not move Elsa off in-memory stores
* migration behavior belongs to the configured persistence feature
* different hosts can expose migration toggles differently

See [Database Configuration](../../getting-started/database-configuration.md)
and [EF Core Migrations](../persistence/ef-migrations.md) for provider-specific
examples.

## Common Pitfalls

### A config key exists in docs but nothing reads it

Check the host's `Program.cs`. Elsa settings are often bound explicitly, so an
unbound section does nothing.

### Studio can load, but API calls fail

Check `Backend:Url`, the API route prefix, CORS, and auth provider alignment
before debugging the workflow engine itself.

### WASM settings do not change after updating server environment variables

Blazor WASM reads client configuration from static assets such as
`wwwroot/appsettings.json`. Updating server environment variables alone does not
rewrite those files.

### Reverse proxy deployments generate wrong absolute links

Set `Http:BaseUrl` to the public URL seen by clients, not only the internal
container or pod address.

### Distributed runtime warns about local locking

If you enable the distributed runtime and keep a local-only lock provider,
Elsa logs a warning because that setup does not coordinate across nodes. Treat
`DistributedRuntime:AllowLocalLockProviderInDistributedRuntime=true` as a
single-host acknowledgement for development or tests, not as a clustering
solution.

## Related Guides

* [Database Configuration](../../getting-started/database-configuration.md)
* [Authentication & Authorization](../authentication.md)
* [Security & Authentication](../security/README.md)
* [Deployment](README.md)
* [Studio Integration](../studio/integration/README.md)
