---
description: >-
  Minimal example to enable MongoDB persistence for Elsa Workflows, including connection configuration and indexing notes.
---

# MongoDB Setup Example

This document provides a minimal, copy-pasteable example for configuring Elsa Workflows with MongoDB persistence.

## Prerequisites

- .NET 8.0 or later
- MongoDB 4.4 or later
- Elsa v3.x packages

## NuGet Packages

```bash
dotnet add package Elsa
dotnet add package Elsa.Persistence.MongoDb
```

## Minimal Configuration

### Program.cs

```csharp
using Elsa.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Get connection string from configuration
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb")
    ?? throw new InvalidOperationException("Connection string 'MongoDb' not found.");

builder.Services.AddElsa(elsa =>
{
    // Configure the shared MongoDB connection.
    elsa.UseMongoDb(mongoConnectionString);

    // Configure workflow management (definitions, instances)
    elsa.UseWorkflowManagement(management =>
    {
        management.UseMongoDb();
    });
    
    // Configure workflow runtime (bookmarks, inbox, execution logs)
    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.UseMongoDb();
    });
    
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

### appsettings.json

```json
{
  "ConnectionStrings": {
    "MongoDb": "mongodb://localhost:27017/elsa"
  }
}
```

**With Authentication:**
```json
{
  "ConnectionStrings": {
    "MongoDb": "mongodb://username:password@localhost:27017/elsa?authSource=admin"
  }
}
```

**Replica Set:**
```json
{
  "ConnectionStrings": {
    "MongoDb": "mongodb://node1:27017,node2:27017,node3:27017/elsa?replicaSet=rs0"
  }
}
```

## Index Creation

Elsa creates the MongoDB indexes it needs on startup. You do not need to run a separate index creation script for the built-in workflow management and runtime stores.

Elsa uses snake_case collection names, including:

- `workflow_definitions`
- `workflow_instances`
- `triggers`
- `bookmarks`
- `bookmark_queue_items`
- `workflow_execution_logs`
- `activity_execution_logs`
- `key_value_pairs`

Use MongoDB shell commands only for verification or for additional workload-specific indexes:

```javascript
use elsa;

db.workflow_definitions.getIndexes();
db.workflow_instances.getIndexes();
db.bookmarks.getIndexes();
db.workflow_execution_logs.getIndexes();
```

## Advanced Configuration

### Custom Database and Collection Names

```csharp
var mongoConnectionString = "mongodb://localhost:27017/my_workflows";

builder.Services.AddElsa(elsa =>
{
    elsa.UseMongoDb(mongoConnectionString);

    elsa.UseWorkflowManagement(management =>
    {
        management.UseMongoDb();
    });
});
```

The database name is read from the connection string. For custom collection names, replace the `CollectionNamingStrategy` on the MongoDB feature.

### Connection Pool Settings

Configure MongoDB driver settings such as pool sizes in the connection string:

```json
{
  "ConnectionStrings": {
    "MongoDb": "mongodb://localhost:27017/elsa?maxPoolSize=100&minPoolSize=10&waitQueueTimeoutMS=30000&connectTimeoutMS=10000"
  }
}
```

### Read Preference for Replicas

For read-heavy workloads with replica sets:

```csharp
using MongoDB.Driver;

elsa.UseMongoDb(mongoConnectionString, options =>
{
    options.ReadPreference = ReadPreference.SecondaryPreferred;
});
```

## Mapping Considerations

### Custom Activity Data

When storing custom data in activities, ensure it's BSON-serializable:

```csharp
// Good: Simple types serialize automatically
public class MyActivityData
{
    public string Name { get; set; }
    public int Count { get; set; }
    public DateTime Timestamp { get; set; }
}

// For complex types, ensure serialization works
public class ComplexData
{
    public Dictionary<string, object> Properties { get; set; }
    
    [BsonIgnore]  // Exclude from persistence
    public Func<Task> Callback { get; set; }
}
```

### BSON Serialization Settings

Elsa uses MongoDB driver conventions. For custom serialization needs:

```csharp
// Register custom serializers before AddElsa
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// Or use convention packs
var pack = new ConventionPack
{
    new CamelCaseElementNameConvention(),
    new IgnoreExtraElementsConvention(true)
};
ConventionRegistry.Register("ElsaConventions", pack, t => true);
```

## TTL Collections

MongoDB supports TTL (Time-To-Live) indexes for automatic document expiration:

```javascript
// Auto-delete workflow execution logs after 30 days
db.workflow_execution_logs.createIndex(
    { "Timestamp": 1 },
    { expireAfterSeconds: 2592000 }  // 30 days
);

// Auto-delete completed workflow instances after 60 days
// Note: Only works if you have a dedicated TTL field
db.workflow_instances.createIndex(
    { "ExpiresAt": 1 },
    { expireAfterSeconds: 0 }  // Expire at the ExpiresAt time
);
```

## Troubleshooting

### Connection Issues

**Error:** `Unable to connect to server`

**Solutions:**
- Verify MongoDB is running: `mongosh --eval "db.runCommand({ping: 1})"`
- Check connection string format
- Verify network connectivity and firewall rules
- For replica sets, ensure all nodes are accessible

**Error:** `Authentication failed`

**Solutions:**
- Verify username/password
- Check `authSource` parameter in connection string
- Ensure user has appropriate roles: `readWrite` on the elsa database

### Performance Issues

**Slow queries:**
1. Verify indexes are created: `db.collection.getIndexes()`
2. Use explain plans: `db.collection.find({...}).explain("executionStats")`
3. Check for collection scans in slow query logs

**High memory usage:**
- Review connection pool settings
- Consider adding a TTL index for log collections
- Implement retention policies for old workflow instances

### Logging

Enable MongoDB driver logging:

```json
{
  "Logging": {
    "LogLevel": {
      "MongoDB": "Debug"
    }
  }
}
```

## Related Documentation

- [Persistence Guide](../README.md) — Overview and provider comparison
- [Indexing Notes](indexing-notes.md) — Detailed indexing guidance
- [MongoDB Documentation](https://www.mongodb.com/docs/manual/) — Official MongoDB docs

---

**Last Updated:** 2025-11-28
