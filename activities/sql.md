---
description: >-
  Configure Elsa's SQL extension and choose the right SQL activity for queries,
  commands, and scalar values.
---

# SQL activities

Elsa's SQL extension lets a server-side workflow execute SQL against a
connection supplied by the workflow. Use it when a workflow needs a focused
database read or write and your team is prepared to own the SQL, permissions,
and database lifecycle.

The extension supplies three activities:

- **SQL Query** — use for a tabular `SELECT` or other result-producing query;
  it returns a `DataSet` containing one `DataTable`.
- **SQL Command** — use for an `INSERT`, `UPDATE`, `DELETE`, DDL statement, or
  other non-query command; it returns the provider's affected-row count.
- **SQL Single Value** — use for a scalar lookup such as `COUNT(*)` or an
  identifier; it returns the first column of the first row.

## Install the extension

Install the base package and the provider package for each database the host
will access. The 3.8.0 release includes these provider packages:

```bash
dotnet add package Elsa.Sql
dotnet add package Elsa.Sql.SqlServer      # replace with your provider
```

Replace `Elsa.Sql.SqlServer` with `Elsa.Sql.PostgreSql`, `Elsa.Sql.MySql`, or
`Elsa.Sql.Sqlite` as appropriate. Install more than one provider when the same
host needs to connect to more than one database type.

The provider packages bring the corresponding ADO.NET client and reference
`Elsa.Sql`. Installing a provider package does not register it with Elsa; the
server must register each client type explicitly.

## Register clients on the server

Call `UseSql` in the same `AddElsa` configuration that hosts the activities.
Register a name for every client that a workflow can select:

```csharp
using Elsa.Extensions;
using Elsa.Sql.Extensions;
using Elsa.Sql.MySql;
using Elsa.Sql.PostgreSql;
using Elsa.Sql.Sqlite;
using Elsa.Sql.SqlServer;

builder.Services.AddElsa(elsa => elsa
    .UseSql(sql =>
    {
        sql.Clients = clients =>
        {
            clients.Register<SqlServerClient>("SqlServer");
            clients.Register<PostgreSqlClient>("PostgreSql");
            clients.Register<MySqlClient>("MySql");
            clients.Register<SqliteClient>("Sqlite");
        };
    }));
```

The registration name is the runtime lookup key and the option shown in the
`Client` dropdown. Names must be unique. If the name is `null` or empty, the
registration key defaults to the client type name, for example
`SqlServerClient`.

`UseSql` registers the SQL activities, SQL expression support, the client
factory, and the provider that supplies registered client names to the
activity input UI. It does not provision a database, run migrations, or read
connection strings from `appsettings.json` for you.

## Configure an activity

Each SQL activity has the same connection inputs:

- **Client** — the name passed to `Register`. In Studio, the dropdown is
  populated from the names registered by the connected server.
- **Connection String** — the connection string passed to the selected
  ADO.NET client. It is marked as able to contain secrets, so use a secret
  reference or another protected input strategy for credentials. See
  [Secrets Management](../guides/security/secrets-management.md).
- **Query** or **Command** — SQL text. The input uses the `Sql` expression
  type and receives SQL syntax highlighting.

The activity creates and opens a connection for its execution, executes the
statement, and disposes the connection. The extension does not expose a
transaction input or share a connection between activities. If multiple
statements must be atomic, use a single provider-supported transactional
operation or implement a custom activity/client with an explicit transaction
boundary.

## Choose the activity

### SQL Query

Use **SQL Query** when the workflow needs rows and columns. The `Results`
output is a `DataSet?`, but the 3.8.0 base client adds only one `DataTable` and
reads the complete result into memory. It does not materialize multiple result
sets.

```sql
SELECT Id, Email, Status
FROM Users
WHERE Status = {{Input.Status}}
ORDER BY Id;
```

`Results` is marked `IsSerializable = false`. Do not expect it to survive a
workflow persistence boundary or to be sent through a serialized activity
output. Map the rows to a serializable value in the same execution burst, or
use a different activity/custom client that returns a serializable model.

### SQL Command

Use **SQL Command** for statements where the useful result is the affected-row
count:

```sql
UPDATE Orders
SET Status = {{Input.Status}}
WHERE Id = {{Input.OrderId}};
```

The `Result` output is `int?` and comes from the provider's
`ExecuteNonQueryAsync` implementation. It is not a workflow success/failure
indicator; database errors still fault the activity.

### SQL Single Value

Use **SQL Single Value** for one scalar value:

```sql
SELECT COUNT(*)
FROM Orders
WHERE CustomerId = {{Input.CustomerId}};
```

In the activity model, the input is named `Command` even though the activity
is intended for a scalar query. Its `Result` output is `object?`. The client
returns only the first column of the first row; additional rows and columns
are ignored, and an empty result can produce `null`.

## Bind workflow values

SQL inputs use the `Sql` expression type. A placeholder is written with
double braces and can reference these 3.8.0 roots:

```liquid
{{Input.CustomerId}}
{{Output.Lookup.Result}}
{{Variable.Customer.Email}}
{{Activity.WorkflowExecutionContext.Id}}
{{Execution.Id}}
{{Workflow.Identity.DefinitionId}}
{{LastResult}}
```

Nested properties and array/list indexes are supported for objects resolved
from workflow state, for example:

```sql
SELECT * FROM Users
WHERE Email = {{Input.Customer.Email}}
  AND Region = {{Input.Regions[0]}};
```

When the SQL text contains a `{{...}}` placeholder, Elsa resolves the value
and replaces the placeholder with a provider parameter. The built-in clients
inherit these defaults:

```text
Parameter marker: @
Parameter text:   p
Counter:          enabled
Example:          @p0, @p1
```

The values are added to the database command as parameters, including
`DBNull.Value` for a `null` value. This protects parameter values from being
treated as SQL syntax, but it does not make all SQL safe automatically.

Only recognized placeholders are parameterized. SQL text outside the
placeholders is passed through unchanged. Keep table names, column names,
sort directions, and other SQL structure under application control; do not
concatenate untrusted identifiers or raw SQL into an activity input. Use
allow-lists or separate workflow branches when the structure must vary.

If the SQL contains no `{{`, the evaluator returns the SQL unchanged and no
workflow values are parameterized. Malformed or unsupported placeholders
fail evaluation rather than becoming parameters.

## Extend the provider set

The built-in clients are thin adapters over the database-specific ADO.NET
connection and command types:

| Database | Package | Client type |
| --- | --- | --- |
| SQL Server | `Elsa.Sql.SqlServer` | `SqlServerClient` |
| PostgreSQL | `Elsa.Sql.PostgreSql` | `PostgreSqlClient` |
| MySQL | `Elsa.Sql.MySql` | `MySqlClient` |
| SQLite | `Elsa.Sql.Sqlite` | `SqliteClient` |

For another provider, implement `ISqlClient` or derive from
`BaseSqlClient`. The implementation must create the provider-specific
connection and command and accept the workflow connection string. Register
the type through `sql.Clients` so the factory and Studio's `Client` dropdown
can find it:

```csharp
using System.Data.Common;
using Elsa.Sql.Client;

// AcmeConnection and AcmeCommand come from your ADO.NET provider.
public sealed class AcmeSqlClient(string connectionString)
    : BaseSqlClient(connectionString)
{
    protected override DbConnection CreateConnection() =>
        new AcmeConnection(_connectionString);

    protected override DbCommand CreateCommand(
        string query,
        DbConnection connection) =>
        new AcmeCommand(query, (AcmeConnection)connection);
}
```

Use a distinct registration name and verify its parameter marker and naming
rules. If the provider does not use `@p0`-style parameters, override the
`ParameterMarker`, `ParameterText`, or `IncrementParameter` behavior through
the client implementation.

## Studio and hosting boundary

SQL activities are executed by the Elsa Server. When Studio connects to that
server, it can display the registered SQL activities and client names because
the server exposes their activity and property descriptors. Adding the
packages only to a Studio host does not make SQL executable, and a client
registered in one server is not automatically available to another server.

Test SQL workflows against the same provider and permissions used in
production. Give the workflow identity only the database permissions it needs,
avoid granting DDL or unrestricted write access to general-purpose workflow
authors, and keep connection strings and secrets out of committed workflow
definitions.

## Operational limitations and troubleshooting

- **The Client dropdown is empty:** confirm that `UseSql` is registered on the
  server, the provider type is registered in `sql.Clients`, and Studio is
  connected to that server.
- **No registered SQL client provider:** the activity's `Client` value does
  not match a registration name. Names are case-sensitive dictionary keys.
- **Connection string cannot be empty:** supply a non-empty evaluated value;
  the client factory rejects an empty connection string.
- **The query result cannot be persisted:** this is expected for `SQL Query`.
  Its `DataSet` output is intentionally non-serializable and should be mapped
  before a persistence boundary.
- **Large result sets use too much memory:** `SQL Query` buffers the result
  into a `DataTable`. Add restrictive predicates or paging, or return a
  smaller serializable shape through a custom activity.
- **A placeholder is not replaced:** only the documented roots and
  `LastResult` are recognized, and the placeholder must use matching `{{` and
  `}}` delimiters.

## Release source

This page is validated against Elsa Extensions `release/3.8.0` at commit
[`a44e2b0`](https://github.com/elsa-workflows/elsa-extensions/tree/a44e2b0).
The relevant files are in the
[SQL module directory](https://github.com/elsa-workflows/elsa-extensions/tree/a44e2b0/src/modules/sql):

- `Elsa.Sql/Extensions/ModuleExtensions.cs` and
  `Elsa.Sql/Services/ClientStore.cs` for registration.
- `Elsa.Sql/Activities/` for the three activity contracts.
- `Elsa.Sql/Services/SqlEvaluator.cs` for placeholder evaluation.
- `Elsa.Sql/Client/BaseSqlClient.cs` for parameter injection and result
  materialization.
