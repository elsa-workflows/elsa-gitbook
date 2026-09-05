---
description: >-
  Install the Elsa 3.8.0 External Authentication broker, its OpenID Connect
  adapter, optional secret bridge, and durable persistence providers.
---

# Install External Authentication

External Authentication is available in the stable Elsa 3.8.0 Core and Studio
packages on NuGet.org. Keep the Core, Studio, and Extensions package families
on the same `3.8.0` version.

External Authentication makes Elsa Server a broker between Elsa Studio and one or more upstream identity providers. Elsa owns the sign-in transaction, issues the Elsa access and refresh tokens that Studio consumes, and can centrally manage connections, identity links, sessions, and permission mapping. The first supplied provider adapter is OpenID Connect.

This is different from Studio's existing direct OpenID Connect mode, in which Studio talks directly to the identity provider. See [Migration from Direct OpenID Connect](migration-from-direct-oidc.md) before changing an existing Studio host.

## Prerequisites

- An Elsa **3.8.0** application with Elsa Identity enabled. The broker issues Elsa credentials, so it needs Elsa Identity token signing and a user/role provider.
- An upstream OpenID Connect provider with a confidential client registration for Elsa Server.
- A public HTTPS address for Elsa Server. It is used to derive the fixed provider callbacks.
- For production or multiple nodes, a relational database supported by the selected persistence provider and a shared ASP.NET Core Data Protection key store. See [Production](production.md).

Use a real secret manager, environment variables, or another configuration provider for keys and client secrets. The examples deliberately use placeholders only.

## Install stable packages

Stable Elsa 3.8.0 packages are available from NuGet.org; no preview feed is
required. Pin the version explicitly for repeatable restores:

## Choose packages

Install the foundation and at least one protocol adapter:

```bash
dotnet add package Elsa.ExternalAuthentication --version 3.8.0
dotnet add package Elsa.ExternalAuthentication.OpenIdConnect --version 3.8.0
```

The available packages are:

| Package | When to install it |
| --- | --- |
| `Elsa.ExternalAuthentication` | Required foundation: broker, connection model, management and broker APIs, in-memory development stores, permission mapping, and local-login support. |
| `Elsa.ExternalAuthentication.OpenIdConnect` | Installs the `openid-connect` upstream provider adapter. |
| `Elsa.ExternalAuthentication.Secrets` | Optional bridge to Elsa Secrets for Elsa-managed client secrets. It is not needed for secrets read from standard .NET configuration. |
| `Elsa.ExternalAuthentication.Persistence.EFCore` | Shared EF Core persistence base. It is normally brought in transitively by a provider package. |
| `Elsa.ExternalAuthentication.Persistence.EFCore.Sqlite` | EF Core persistence for SQLite. |
| `Elsa.ExternalAuthentication.Persistence.EFCore.SqlServer` | EF Core persistence for SQL Server. |
| `Elsa.ExternalAuthentication.Persistence.EFCore.PostgreSql` | EF Core persistence for PostgreSQL. |
| `Elsa.ExternalAuthentication.Persistence.EFCore.MySql` | EF Core persistence for MySQL. |
| `Elsa.ExternalAuthentication.Persistence.EFCore.Oracle` | EF Core persistence for Oracle. |

For example, a SQL Server deployment adds:

```bash
dotnet add package Elsa.ExternalAuthentication.Persistence.EFCore.SqlServer --version 3.8.0
```

The provider-specific package is required for its migrations and, in a CShells host, to make the corresponding shell feature discoverable. Identity persistence and External Authentication persistence are separate features: adding `Elsa.Persistence.EFCore.*` or enabling an Identity persistence feature does **not** make broker state durable.

## Classic `AddElsa` host

Register Elsa Identity, the broker, and the OIDC adapter. Use `BindExternalAuthenticationOptions` rather than plain configuration binding: it also reconstructs the JSON settings carried by adapters, policies, and permission grant sources.

```csharp
using Elsa.Extensions;
using Microsoft.Extensions.Configuration;

builder.Services.AddElsa(elsa =>
{
    elsa.UseIdentity(identity =>
    {
        identity.TokenOptions = options =>
        {
            options.SigningKey = builder.Configuration["Identity:SigningKey"]
                ?? throw new InvalidOperationException("Identity signing key is required.");
            options.Issuer = "https://elsa.example.com";
            options.Audience = "https://elsa.example.com";
        };
    });

    elsa.UseExternalAuthentication(feature =>
    {
        feature.ConfigureOptions = options =>
            builder.Configuration.GetSection("ExternalAuthentication")
                .BindExternalAuthenticationOptions(options);
    });
});

builder.Services.AddOpenIdConnectExternalAuthentication();
```

This registration uses in-memory stores and is appropriate only for local, single-node development. Add the selected EF Core feature for a durable host. The SQLite form illustrates the classic feature composition:

```csharp
using Elsa.ExternalAuthentication.Persistence.EFCore.Sqlite.Extensions;

builder.Services.AddElsa(elsa =>
{
    elsa.UseExternalAuthentication(externalAuthentication =>
    {
        externalAuthentication.UseEntityFrameworkCore(ef =>
            ef.UseSqlite("Data Source=external-authentication.db;Cache=Shared"));
    });
});
```

Keep this in the same Elsa configuration that enables the broker. Replace `UseSqlite` with `UseSqlServer`, `UsePostgreSql`, `UseMySql`, or `UseOracle` when using the matching provider package. Apply that provider's `ExternalAuthenticationElsaDbContext` migration through your normal migration deployment process.

If a connection or authentication client uses Elsa-managed secrets, register the optional bridge as well:

```csharp
builder.Services.AddElsaSecretsExternalAuthentication();
```

## CShells / Modular Server

CShells hosts discover External Authentication through shell features. Ensure the application references the adapter package and, for durable state, the matching persistence package. Enable the feature names in the shell configuration:

```json
{
  "CShells": {
    "Shells": {
      "Default": {
        "Features": {
          "ExternalAuthentication": {
            "Redirects": {
              "ExternalCallbackBaseUri": "https://elsa.example.com/elsa/api/"
            }
          },
          "OpenIdConnectExternalAuthentication": {},
          "SqlServerExternalAuthenticationPersistence": {
            "ConnectionString": "<external-authentication-database-connection-string>"
          }
        }
      }
    }
  }
}
```

Replace `Default` with the configured shell name when the host uses another
shell. The feature-local settings path is therefore
`CShells:Shells:<shell-name>:Features:ExternalAuthentication`.

The persistence feature names are `SqliteExternalAuthenticationPersistence`, `SqlServerExternalAuthenticationPersistence`, `PostgreSqlExternalAuthenticationPersistence`, `MySqlExternalAuthenticationPersistence`, and `OracleExternalAuthenticationPersistence`.

To resolve managed bindings from Elsa Secrets, also enable `ElsaSecretsExternalAuthentication`; it depends on both `ExternalAuthentication` and the Elsa `Secrets` feature. The OIDC feature depends on `ExternalAuthentication`. A Modular Server reference host demonstrates these packages and features.

## Confirm the installation

After startup, the broker APIs live below Elsa's API route prefix, for example `https://elsa.example.com/elsa/api/external-authentication`.

1. Configure an Authentication Client and at least one connection as described in [Configuration](configuration.md).
2. Request `GET /external-authentication/login-methods?clientId=<your-client-id>`.
3. Confirm that the response lists the local method (unless disabled) and your enabled connection, without provider settings or secrets.
4. For a persisted setup, verify that the selected External Authentication persistence feature is enabled and its migrations are applied.

The management UI is provided by the companion Elsa Studio External Authentication modules. Once those modules are installed, open **Administration → Identity & access → Identity provider connections** (`/security/external-authentication/connections`) to manage database-owned connections; configuration-owned connections are inspect-only.
