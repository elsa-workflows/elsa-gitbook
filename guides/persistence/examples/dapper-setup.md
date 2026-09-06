---
description: >-
  Configure Elsa Workflows 3.8.0 with the Dapper persistence provider,
  including SQL dialect selection and FluentMigrator schema setup.
---

# Dapper persistence

Elsa's Dapper extension supplies relational persistence implementations for
workflow management, workflow runtime, and (optionally) Elsa Identity. It
uses a connection provider to create database connections and select the SQL
dialect used by the stores.

This page targets the `release/3.8.0` source. The extension contains built-in
connection providers for SQLite, SQL Server, and PostgreSQL. The extension
does not contain a MySQL connection provider; choosing MySQL in an unrelated
Elsa EF Core example does not configure Dapper.

## Choose Dapper when

Dapper is a good fit when your team wants Elsa's store contracts backed by
direct SQL and already operates a supported relational database. Choose EF
Core when you want Elsa's EF migrations and provider-specific EF tooling.
Choose MongoDB when a document database is a better fit for your deployment.

Dapper is not a second workflow model: it replaces the persistence
implementations behind Elsa's existing management and runtime contracts. The
3.8.0 extension wires these stores:

| Area | Stores configured by the Dapper feature |
| --- | --- |
| Management | Workflow definitions and workflow instances |
| Runtime | Triggers, bookmarks, bookmark queue items, workflow execution logs, activity execution records, and key/value records |
| Identity (optional) | Users, applications, and roles |

## Install the packages

Add the Dapper extension to the server project:

```bash
dotnet add package Elsa.Persistence.Dapper --version 3.8.0
```

The extension references the separate `Elsa.Persistence.Dapper.Migrations`
assembly. Add that package explicitly when your host refers to the migration
types, as the examples below do:

```bash
dotnet add package Elsa.Persistence.Dapper.Migrations --version 3.8.0
```

The extension's built-in connection providers use these ADO.NET drivers:

| Database | Connection provider | Driver |
| --- | --- | --- |
| SQLite | `SqliteDbConnectionProvider` | `Microsoft.Data.Sqlite` |
| SQL Server | `SqlServerDbConnectionProvider` | `Microsoft.Data.SqlClient` |
| PostgreSQL | `PostgreSqlDbConnectionProvider` | `Npgsql` |

For automatic SQLite or SQL Server migrations, also reference the matching
FluentMigrator runner package in the host:

```bash
dotnet add package FluentMigrator.Runner.SQLite --version 7.2.0
dotnet add package FluentMigrator.Runner.SqlServer --version 7.2.0
```

Use only the runner package for the database you select. The Dapper extension
source and the 3.8.0 workbench explicitly configure `AddSQLite()` or
`AddSqlServer()`; PostgreSQL has a Dapper connection provider, but the
workbench does not ship a PostgreSQL FluentMigrator runner configuration.

## Configure a durable SQL Server store

The following is the registration shape used by the 3.8.0 workbench. Keep the
connection string in configuration or a secret store; do not commit a real
password.

```csharp
using Elsa.Extensions;
using Elsa.Persistence.Dapper.Contracts;
using Elsa.Persistence.Dapper.Extensions;
using Elsa.Persistence.Dapper.Services;
using FluentMigrator.Runner;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("SqlServer")
    ?? throw new InvalidOperationException("Connection string 'SqlServer' not found.");

builder.Services.AddElsa(elsa =>
{
    elsa.UseDapper(dapper =>
    {
        dapper.DbConnectionProvider = _ =>
            new SqlServerDbConnectionProvider(connectionString);

        dapper.UseMigrations(migrations =>
        {
            migrations.ConfigureRunner = runner => runner
                .AddSqlServer()
                .WithGlobalConnectionString(sp =>
                    sp.GetRequiredService<IDbConnectionProvider>().GetConnectionString())
                .WithMigrationsIn(
                    typeof(Elsa.Persistence.Dapper.Migrations.Management.Initial).Assembly);
        });
    });

    elsa.UseIdentity(identity => identity.UseDapper());
    elsa.UseWorkflowManagement(management => management.UseDapper());
    elsa.UseWorkflowRuntime(runtime => runtime.UseDapper());
});
```

`UseIdentity(identity => identity.UseDapper())` is needed only when the Elsa
Identity stores should use Dapper. Omit it when the host uses another identity
store or an external identity system.

The management and runtime calls are the important workflow registrations.
They select Dapper implementations for the existing Elsa store contracts;
they do not configure activity-specific data stores.

### SQLite variant

Use a file-backed SQLite connection for persistence that must survive process
restarts:

```csharp
using Elsa.Persistence.Dapper.Contracts;
using Elsa.Persistence.Dapper.Services;

var connectionString = "Data Source=App_Data/elsa.db";

builder.Services.AddElsa(elsa =>
{
    elsa.UseDapper(dapper =>
    {
        dapper.DbConnectionProvider = _ =>
            new SqliteDbConnectionProvider(connectionString);

        dapper.UseMigrations(migrations =>
        {
            migrations.ConfigureRunner = runner => runner
                .AddSQLite()
                .WithGlobalConnectionString(sp =>
                    sp.GetRequiredService<IDbConnectionProvider>().GetConnectionString())
                .WithMigrationsIn(
                    typeof(Elsa.Persistence.Dapper.Migrations.Management.Initial).Assembly);
        });
    });

    elsa.UseWorkflowManagement(management => management.UseDapper());
    elsa.UseWorkflowRuntime(runtime => runtime.UseDapper());
});
```

The feature's default provider is an in-memory SQLite provider with the
connection string `Data Source=:memory:;Cache=Shared`. Always set an explicit
provider for a deployed application. The in-memory default is useful for
short-lived tests, not durable workflow execution.

## PostgreSQL and custom providers

PostgreSQL data access is supported through
`PostgreSqlDbConnectionProvider`:

```csharp
elsa.UseDapper(dapper =>
{
    dapper.DbConnectionProvider = _ =>
        new PostgreSqlDbConnectionProvider(postgresConnectionString);
});

elsa.UseWorkflowManagement(management => management.UseDapper());
elsa.UseWorkflowRuntime(runtime => runtime.UseDapper());
```

The provider supplies both an `NpgsqlConnection` and the matching
`PostgreSqlDialect`. If you use a database that is not represented by a
built-in provider, implement `IDbConnectionProvider` and return an
`ISqlDialect` whose SQL is valid for that database. Register that provider via
`dapper.DbConnectionProvider`.

Do not assume that a connection provider alone makes the bundled migrations
portable. Automatic migrations require a FluentMigrator runner provider and
the migration SQL must be verified against the target database.

## Connection lifetime and transactions

Each built-in provider creates a new ADO.NET connection when an Elsa Dapper
store needs one, and the store disposes it after the operation. Configure the
provider with the connection string appropriate for your driver; do not keep a
single open connection in application-wide state.

The Elsa Dapper store methods do not expose a shared transaction parameter.
Do not assume that several calls across different Elsa stores are one atomic
transaction. If an application needs a cross-store transaction, design and
test that integration explicitly against the selected database provider.

## Migrations and schema ownership

Calling `UseMigrations()` adds FluentMigrator and registers
`RunMigrationsStartupTask`. The task calls `MigrateUp()` when the host starts.
The configured runner must therefore include all of the following:

1. A database-specific runner such as `AddSQLite()` or `AddSqlServer()`.
2. The connection string from the registered `IDbConnectionProvider`.
3. The assembly containing `Elsa.Persistence.Dapper.Migrations`.

The bundled migration assembly groups versions by module prefix:

| Module | Prefix | 3.8.0 source currently includes |
| --- | ---: | --- |
| Management | 1000 | `Initial` through `V3_4` |
| Runtime | 2000 | `Initial` through `V3_7` |
| Identity | 3000 | `Initial` through `V3_3` |

Migration versions are not the Elsa package version. For example,
Management `Initial` is migration `10001`, and Runtime `V3_7` is migration
`20007`.

Choose one schema owner for each environment:

- Let the controlled application startup run the bundled migrations, or
- apply reviewed migrations from the same migration assembly before
  starting the application.

Do not copy the old 3.7.0 snippets from this repository or mix Dapper
migrations with EF Core migrations. If you manage PostgreSQL or another
custom database externally, verify the generated SQL, indexes, data types,
and migration history against your exact database engine.

## SQL dialect behavior

Every `IDbConnectionProvider` exposes a matching `ISqlDialect`. The stores use
that dialect for filtering, ordering, pagination, inserts, updates, and
upserts. The built-in differences include:

- SQL Server uses `OFFSET ... ROWS` and `FETCH NEXT ... ROWS ONLY`.
- SQLite uses `LIMIT ... OFFSET ...` and `INSERT OR REPLACE`.
- PostgreSQL uses `ON CONFLICT (...) DO UPDATE` for upserts.

This is why changing only the connection string is insufficient when adding a
custom database. The provider and dialect must agree, and the migration
runner must target the same engine.

## Operational checklist

- Use a durable database and an explicit `DbConnectionProvider` outside tests.
- Keep the Elsa Dapper package, migration assembly, and runner package on
  compatible versions.
- Give the startup identity permission to apply migrations, or run migrations
  in a separate deployment step with an appropriately privileged identity.
- Coordinate migration startup when multiple application instances can start
  simultaneously; the source registers a startup task but does not provide a
  distributed migration lock.
- Test workflow resume, bookmarks, triggers, logs, and tenant-scoped data
  after changing providers or dialects.
- Parameterize application SQL. Dapper's own guidance recommends passing
  values as parameters rather than concatenating them into SQL; Elsa's
  internal Dapper query builder follows the same pattern.

## Elsa Studio boundary

Dapper configuration is server-side. The 3.8.0 Studio source has no
Dapper-specific persistence implementation or provider-selection screen. Once
the server's management and runtime APIs use Dapper, Studio consumes those
existing APIs; migrations, database credentials, dialect selection, and schema
ownership remain responsibilities of the server host.

## Release source references

- [Dapper feature and default provider](https://github.com/elsa-workflows/elsa-extensions/blob/66861ae0828e2c15459e8d9981f5ce1080bd3269/src/modules/persistence/Elsa.Persistence.Dapper/Features/DapperFeature.cs#L14-L32)
- [Dapper migration runner contract](https://github.com/elsa-workflows/elsa-extensions/blob/66861ae0828e2c15459e8d9981f5ce1080bd3269/src/modules/persistence/Elsa.Persistence.Dapper/Features/DapperMigrationsFeature.cs#L15-L35)
- [Built-in connection providers](https://github.com/elsa-workflows/elsa-extensions/tree/66861ae0828e2c15459e8d9981f5ce1080bd3269/src/modules/persistence/Elsa.Persistence.Dapper/Services)
- [Dapper workbench registration](https://github.com/elsa-workflows/elsa-extensions/blob/66861ae0828e2c15459e8d9981f5ce1080bd3269/src/workbench/Elsa.Server.Web/Program.cs#L128-L151)
- [Dapper workflow management stores](https://github.com/elsa-workflows/elsa-extensions/blob/66861ae0828e2c15459e8d9981f5ce1080bd3269/src/modules/persistence/Elsa.Persistence.Dapper/Modules/Management/Features/DapperWorkflowManagementPersistenceFeature.cs#L15-L43)
- [Dapper workflow runtime stores](https://github.com/elsa-workflows/elsa-extensions/blob/66861ae0828e2c15459e8d9981f5ce1080bd3269/src/modules/persistence/Elsa.Persistence.Dapper/Modules/Runtime/Features/DapperWorkflowRuntimePersistenceFeature.cs#L20-L55)
- [Generic Dapper store connection usage](https://github.com/elsa-workflows/elsa-extensions/blob/66861ae0828e2c15459e8d9981f5ce1080bd3269/src/modules/persistence/Elsa.Persistence.Dapper/Services/Store.cs#L77-L87)
- [Migration version prefixes](https://github.com/elsa-workflows/elsa-extensions/blob/66861ae0828e2c15459e8d9981f5ce1080bd3269/src/modules/persistence/Elsa.Persistence.Dapper.Migrations/Readme.md)
- [Dapper parameterized queries](https://github.com/DapperLib/Dapper/blob/main/Readme.md#parameterized-queries)
