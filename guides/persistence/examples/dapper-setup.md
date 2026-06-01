---
description: >-
  Minimal example to enable Dapper persistence for Elsa Workflows, including connection provider setup and migrations.
---

# Dapper Setup Example

This document provides a minimal, copy-pasteable example for configuring Elsa Workflows with Dapper persistence.

## Prerequisites

- .NET 8.0 or later
- Database server (PostgreSQL or SQL Server)
- Elsa v3.x packages
- Elsa Dapper migrations enabled, or schema managed externally

## NuGet Packages

**For PostgreSQL:**
```bash
dotnet add package Elsa
dotnet add package Elsa.Persistence.Dapper
dotnet add package Npgsql
```

**For SQL Server:**
```bash
dotnet add package Elsa
dotnet add package Elsa.Persistence.Dapper
dotnet add package Microsoft.Data.SqlClient
```

## When to Use Dapper

Dapper is ideal for:
- **Performance-critical scenarios** requiring minimal ORM overhead
- **Fine-grained SQL control** for custom query optimization
- **Existing database schemas** where you want to integrate Elsa
- **Teams with strong SQL expertise** who prefer direct control

Consider EF Core instead if you need:
- Higher-level abstractions
- Simpler configuration

## Minimal Configuration

### Program.cs

```csharp
using Elsa.Extensions;
using Elsa.Persistence.Dapper.Extensions;
using Elsa.Persistence.Dapper.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PostgreSql")
    ?? throw new InvalidOperationException("Connection string 'PostgreSql' not found.");

builder.Services.AddElsa(elsa =>
{
    // Configure the shared Dapper connection provider and migrations.
    elsa.UseDapper(dapper =>
    {
        dapper.DbConnectionProvider = _ => new PostgreSqlDbConnectionProvider(connectionString);
        dapper.UseMigrations();
    });
    
    // Configure workflow management with Dapper
    elsa.UseWorkflowManagement(management => management.UseDapper());

    // Configure workflow runtime with Dapper
    elsa.UseWorkflowRuntime(runtime => runtime.UseDapper());
    
    // Enable HTTP activities (optional)
    elsa.UseHttp();
    
    // Enable scheduling activities (optional)
    elsa.UseScheduling();
    
    // Enable API endpoints
    elsa.UseWorkflowsApi();
});

var app = builder.Build();

// Map Elsa API endpoints
app.UseWorkflows();

app.Run();
```

### SQL Server Example

```csharp
using Elsa.Persistence.Dapper.Extensions;
using Elsa.Persistence.Dapper.Services;

builder.Services.AddElsa(elsa =>
{
    elsa.UseDapper(dapper =>
    {
        dapper.DbConnectionProvider = _ => new SqlServerDbConnectionProvider(connectionString);
        dapper.UseMigrations();
    });

    elsa.UseWorkflowManagement(management => management.UseDapper());
    elsa.UseWorkflowRuntime(runtime => runtime.UseDapper());
});
```

### appsettings.json

**PostgreSQL:**
```json
{
  "ConnectionStrings": {
    "PostgreSql": "Host=localhost;Database=elsa;Username=elsa;Password=YOUR_PASSWORD;Port=5432"
  }
}
```

**SQL Server:**
```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=localhost;Database=Elsa;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=true"
  }
}
```

## Schema Creation

Use `dapper.UseMigrations()` to let Elsa create and update the Dapper schema for supported databases. The migrations are included with `Elsa.Persistence.Dapper` and use PascalCase table and column names.

Core workflow tables created by the migrations include:

- `WorkflowDefinitions`
- `WorkflowInstances`
- `Triggers`
- `Bookmarks`
- `WorkflowExecutionLogRecords`
- `ActivityExecutionRecords`
- `WorkflowInboxMessages`

If you manage the schema externally, mirror the 3.7.0 migrations from `src/modules/persistence/Elsa.Persistence.Dapper.Migrations` and keep the PascalCase names. Do not use the snake_case MongoDB collection names or older hand-written SQL snippets for Dapper.

## Transactions

Dapper operations participate in ambient transactions. For explicit control:

```csharp
using System.Transactions;

public class MyWorkflowService
{
    private readonly IWorkflowInstanceStore _store;
    
    public async Task PerformTransactionalOperation()
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        
        // Multiple operations in a single transaction
        await _store.SaveAsync(instance1);
        await _store.SaveAsync(instance2);
        
        scope.Complete();  // Commit
    }
}
```

## Performance Tuning

### Connection Pool Configuration

```csharp
// PostgreSQL with connection pool settings
var connectionString = new NpgsqlConnectionStringBuilder
{
    Host = "localhost",
    Database = "elsa",
    Username = "elsa",
    Password = "YOUR_PASSWORD",
    MaxPoolSize = 100,
    MinPoolSize = 10,
    ConnectionIdleLifetime = 300,
    CommandTimeout = 60
}.ToString();

dapper.DbConnectionProvider = _ => new PostgreSqlDbConnectionProvider(connectionString);
```

### Batch Operations

Dapper excels at batch operations with low overhead:

```csharp
// Example: Batch delete with Dapper
using var connection = new NpgsqlConnection(connectionString);
await connection.ExecuteAsync(
    @"DELETE FROM ""WorkflowInstances""
      WHERE ""Status"" = @Status
      AND ""FinishedAt"" < @Threshold",
    new { Status = "Finished", Threshold = DateTime.UtcNow.AddDays(-30) }
);
```

## Migration Strategy

For supported databases, prefer Elsa's Dapper migrations:

```csharp
elsa.UseDapper(dapper =>
{
    dapper.DbConnectionProvider = _ => new PostgreSqlDbConnectionProvider(connectionString);
    dapper.UseMigrations();
});
```

For production deployments, run the application once in a controlled deployment step or environment where migrations are allowed to apply. If your organization requires separately reviewed SQL scripts, generate or maintain those scripts from the 3.7.0 Dapper migration definitions and preserve the PascalCase table and column names.

## Troubleshooting

### Connection Issues

**Error:** `Connection refused` or `timeout`

**Solutions:**
- Verify database server is running
- Check connection string format
- Ensure network connectivity

### Schema Mismatch

**Error:** `relation "WorkflowInstances" does not exist`

**Solution:** Enable `dapper.UseMigrations()` or apply the equivalent 3.7.0 Dapper migration scripts before running the application.

### Performance Issues

**Slow queries:**
1. Verify indexes exist
2. Use database query analyzer (EXPLAIN ANALYZE in PostgreSQL)
3. Check connection pool metrics

## Related Documentation

- [Persistence Guide](../README.md) — Overview and provider comparison
- [Indexing Notes](indexing-notes.md) — Detailed indexing guidance
- [EF Core Setup](efcore-setup.md) — Alternative with migration support

---

**Last Updated:** 2025-11-28
