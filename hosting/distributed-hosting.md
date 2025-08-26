---
description: >-
  Guide to configuring Elsa Workflows for distributed environments, covering
  runtime, locking, caching, and Quartz.NET clustering.
---

# Distributed Hosting

When deploying Elsa Workflows in a distributed environment, proper configuration is essential to ensure reliability and consistency across multiple nodes. There are four key components to configure:

1. **Distributed Runtime**
2. **Distributed Locking**
3. **Distributed Caching**
4. **Quartz.NET Clustered Mode**

### 1. Distributed Runtime

By default, Elsa uses the `LocalWorkflowRuntime`, which is not suitable for distributed environments like Kubernetes or Azure App Services when running multiple instances. Instead, use either:

* `DistributedWorkflowRuntime`
* `ProtoActorWorkflowRuntime`

Both options ensure that workflow instances are executed in a synchronized manner, preventing multiple processes from acting on the same instance concurrently.

To enable the **Distributed Workflow Runtime**, configure Elsa as follows:

```csharp
elsa.UseWorkflowRuntime(runtime =>
{
   runtime.UseDistributedRuntime();
});
```

When enabling the **Distributed Runtime**, a distributed locking provider must also be configured.

### 2. Distributed Locking

To prevent multiple nodes from executing and updating the same workflow instance simultaneously, Elsa employs **distributed locking**. This mechanism ensures that access to a workflow instance is synchronized across nodes.

Distributed locking is achieved by acquiring a lease (lock) on a shared resource such as a **database**, **Redis**, or **blob storage**.

For example, to configure distributed locking using **PostgreSQL**, use:

```csharp
elsa.UseWorkflowRuntime(runtime =>
{
   runtime.UseDistributedRuntime();
   runtime.DistributedLockProvider = serviceProvider => 
      new PostgresDistributedSynchronizationProvider(postgresConnectionString, options =>
      {
         options.KeepaliveCadence(TimeSpan.FromMinutes(5));
         options.UseMultiplexing();
      });
});
```

### 3. Distributed Caching

Each node in the cluster maintains a local in-memory cache for workflow definitions and other critical data. To ensure consistency, cache invalidation must be propagated across all nodes when changes occur.

This is achieved using **event-driven pub/sub messaging**, enabled through the `DistributedCacheFeature`.

Example configuration:

```csharp
elsa.UseDistributedCache(distributedCaching =>
{
   distributedCaching.UseMassTransit();
});
```

#### Setting Up MassTransit

When using **MassTransit** for distributed caching, configure it with a message broker like **RabbitMQ**:

```csharp
elsa.UseMassTransit(massTransit =>
{
   massTransit.UseRabbitMq(rabbitMqConnectionString, rabbit => rabbit.ConfigureTransportBus = (context, bus) =>
   {
      bus.PrefetchCount = 50;
      bus.Durable = true;
      bus.AutoDelete = false;
      bus.ConcurrentMessageLimit = 32;
   });
});
```

### 4. Quartz.NET Clustered Mode

To ensure that scheduled jobs execute only once across the cluster, configure **Quartz.NET** with a persistent store and enable cluster mode. This is necessary if using Quartz as the scheduling provider instead of an in-memory scheduler or Hangfire.

#### Configuration Example:

```csharp
// Set Quartz as the scheduling provider.
elsa.UseScheduling(scheduling =>
{
   scheduling.UseQuartzScheduler();
});

// Configure Quartz with a persistent store.
elsa.UseQuartz(quartz =>
{
   // This extension enables cluster mode automatically.
   quartz.ConfigureQuartz = config =>
   {
       config.UsePersistentStore(store => {
           store.UsePostgres(pg =>
           {
               pg.ConnectionString = postgresConnectionString;
           });
       });
   };
});
```

### Conclusion

By properly configuring these four components, Elsa Workflows can operate reliably in distributed environments, ensuring that workflow execution remains synchronized, cache updates propagate correctly, and scheduled jobs run efficiently without conflicts.
