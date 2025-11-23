---
description: Comprehensive guide to blocking activities and triggers in Elsa Workflows
---

# Blocking & Trigger Activities

## Overview

Blocking activities and triggers are fundamental building blocks in Elsa Workflows that enable your workflows to pause execution and wait for external events. Understanding how to effectively use these features is essential for building reactive, long-running workflows that respond to real-world events.

### What are Blocking Activities?

A **blocking activity** is an activity that suspends workflow execution until an external event occurs. When a workflow reaches a blocking activity:

1. The workflow instance enters a **suspended** state
2. Execution context is persisted to storage
3. The workflow waits (potentially for days, weeks, or longer) for an external trigger
4. When the triggering event occurs, the workflow resumes from where it left off

### What are Triggers?

A **trigger** is a special type of blocking activity that can both:
- **Start** new workflow instances when triggered
- **Resume** suspended workflow instances waiting for that trigger

Triggers create bookmarks in the workflow that correlate external events to specific workflow instances.

### When to Use Blocking Activities and Triggers

Use blocking activities and triggers when you need to:
- Wait for user input or approval
- Pause for external system callbacks (webhooks, API responses)
- Schedule delayed execution (timers, cron schedules)
- React to message queue events
- Coordinate between multiple workflows
- Handle long-running business processes with human interactions

## Built-in Triggers

Elsa provides several built-in trigger activities that cover common scenarios.

### HTTP Triggers

HTTP triggers enable workflows to respond to HTTP requests. They can be used to:
- Create REST API endpoints
- Receive webhooks from external systems
- Build HTTP-based integrations

#### HTTP Endpoint (Start Trigger)

The `HttpEndpoint` activity starts a workflow when an HTTP request is received at a specific path:

```csharp
// Example: Configure HTTP endpoint in code
var workflow = new WorkflowBuilder()
    .WithHttpEndpoint("/api/orders", "POST")
    .Then<LogMessage>(log => log.Message = "Order received!")
    .Build();
```

**Properties:**
- **Path**: The URL path to listen on (e.g., `/api/orders`)
- **Methods**: HTTP methods to accept (GET, POST, PUT, DELETE, etc.)
- **Authorize**: Whether to require authentication

[Studio Screenshot Placeholder: HTTP Endpoint activity configuration]

#### Receiving HTTP Callbacks

You can pause a workflow to wait for an HTTP callback using correlation IDs:

```csharp
// 1. Create correlation ID and send to external system
var correlationId = context.CorrelationId ?? Guid.NewGuid().ToString();

// 2. Wait for HTTP callback with matching correlation ID
var httpEndpoint = new HttpEndpoint
{
    Path = $"/api/callbacks/{correlationId}",
    Methods = new[] { "POST" }
};
```

See the [HTTP Callback Tutorial](#http-callback-tutorial) section below for a complete example.

### Timer Triggers

Timer triggers enable time-based workflow execution:

#### Delay Activity

Pauses workflow execution for a specified duration:

```csharp
// Wait for 5 minutes
.Then<Delay>(delay => delay.Duration = TimeSpan.FromMinutes(5))
```

#### Timer (Recurring)

Triggers workflow execution on a schedule using cron expressions:

```csharp
// Run every day at 9 AM
var workflow = new WorkflowBuilder()
    .WithTimer("0 9 * * *")  // Cron expression
    .Then<ProcessDailyReports>()
    .Build();
```

**Cron Expression Examples:**
- `0 * * * *` - Every hour
- `0 9 * * 1-5` - Weekdays at 9 AM
- `*/5 * * * *` - Every 5 minutes
- `0 0 1 * *` - First day of each month

[Studio Screenshot Placeholder: Timer activity configuration with cron expression]

#### StartAt (One-time Scheduled Execution)

Schedules a workflow to start at a specific date and time:

```csharp
.WithStartAt(new DateTime(2024, 12, 31, 23, 59, 0))
```

### Signal and Message Triggers

Signals and messages provide in-process communication between workflows or application components.

#### Event (Signal Trigger)

Waits for or publishes named signals:

```csharp
// Workflow A: Wait for signal
.Then<Event>(evt => 
{
    evt.EventName = "OrderApproved";
    evt.CorrelationId = orderId;
})

// Workflow B or Application Code: Publish signal
await workflowRuntime.PublishEventAsync("OrderApproved", orderId);
```

#### Message Received

Waits for strongly-typed messages:

```csharp
// Wait for OrderApprovedMessage
.Then<MessageReceived>(msg => 
{
    msg.MessageType = typeof(OrderApprovedMessage);
    msg.CorrelationId = context.GetVariable<string>("OrderId");
})
```

[Studio Screenshot Placeholder: Event activity configuration]

## HTTP Callback Tutorial

This tutorial demonstrates how to create a workflow that waits for an external HTTP callback with proper correlation and timeout handling.

### Step 1: Create the Workflow

Create a workflow that:
1. Generates an order ID
2. Sends the order to an external system
3. Waits for a webhook callback (or times out after 1 hour)
4. Processes the result

See the complete workflow definition in [workflow-wait-for-webhook.json](workflow-wait-for-webhook.json).

**Workflow Structure:**
```
Start
  → SetVariable (Generate OrderId)
  → SendHttpRequest (Submit order to external system)
  → Fork (Parallel)
      ├─ HttpEndpoint (Wait for webhook callback)
      └─ Delay (1 hour timeout)
  → Join
  → Decision (Check which completed first)
      ├─ Success branch → Process order
      └─ Timeout branch → Handle timeout
```

[Studio Screenshot Placeholder: Workflow diagram showing fork/join pattern with HTTP callback and timer]

### Step 2: External System Calls the Webhook

When the external system completes processing, it calls your webhook:

```bash
# External system POSTs to the callback URL
curl -X POST https://your-app.com/api/callbacks/resume \
  -H "Content-Type: application/json" \
  -d '{
    "correlationId": "order-12345",
    "status": "completed",
    "result": {
      "orderNumber": "ORD-2024-001",
      "total": 299.99
    }
  }'
```

### Step 3: Resume the Workflow

Your application receives the callback and resumes the workflow. See [ResumeWebhookController.cs](ResumeWebhookController.cs) for implementation details.

**Controller code snippet:**
```csharp
[HttpPost("callbacks/resume")]
public async Task<IActionResult> ResumeWorkflow([FromBody] CallbackPayload payload)
{
    // Find suspended workflow instance by correlation ID
    var instances = await _workflowInstanceStore.FindManyAsync(
        new WorkflowInstanceFilter { CorrelationId = payload.CorrelationId }
    );
    
    var instance = instances.FirstOrDefault(x => x.Status == WorkflowStatus.Suspended);
    
    if (instance == null)
        return NotFound("No suspended workflow found");
    
    // Resume the workflow with the callback data
    var input = new Dictionary<string, object>
    {
        ["CallbackData"] = payload
    };
    
    await _workflowDispatcher.DispatchAsync(
        new DispatchWorkflowInstanceRequest
        {
            InstanceId = instance.Id,
            Input = input
        }
    );
    
    return Ok("Workflow resumed");
}
```

### Step 4: Test with curl

```bash
# 1. Start the workflow (returns correlation ID)
curl -X POST https://your-app.com/api/workflows/start/order-workflow \
  -H "Content-Type: application/json" \
  -d '{"productId": "PROD-123", "quantity": 2}'

# Response: { "workflowInstanceId": "...", "correlationId": "order-12345" }

# 2. Verify workflow is suspended
curl https://your-app.com/api/workflows/instances/order-12345

# 3. Send callback to resume
curl -X POST https://your-app.com/api/callbacks/resume \
  -H "Content-Type: application/json" \
  -d '{"correlationId": "order-12345", "status": "completed"}'

# 4. Verify workflow completed
curl https://your-app.com/api/workflows/instances/order-12345
```

## Custom Trigger Implementation

You can create custom triggers for domain-specific scenarios. This section shows how to build a custom trigger from scratch.

### Creating a Custom Trigger Activity

See [CustomTrigger.cs](CustomTrigger.cs) for a complete example.

**Key components:**
```csharp
[Activity(
    Namespace = "Custom",
    DisplayName = "Custom Signal Trigger",
    Description = "Waits for a custom signal with correlation"
)]
public class CustomSignalTrigger : Activity
{
    [Input(Description = "The signal name to wait for")]
    public Input<string> SignalName { get; set; } = default!;
    
    [Input(Description = "Optional correlation value")]
    public Input<string?> CorrelationValue { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        // Create a bookmark to resume this workflow when signal is received
        var signalName = context.Get(SignalName);
        var correlationValue = context.Get(CorrelationValue);
        
        // Create bookmark with correlation
        var bookmarkPayload = new CustomSignalBookmark
        {
            SignalName = signalName,
            CorrelationValue = correlationValue
        };
        
        // NOTE: Adapt to exact Elsa v3 API - bookmark creation may vary by version
        context.CreateBookmark(bookmarkPayload);
        
        // Suspend workflow execution
        // NOTE: In Elsa v3, suspension is implicit when creating a bookmark
    }
}

// Bookmark payload for correlation
public record CustomSignalBookmark
{
    public string SignalName { get; set; } = default!;
    public string? CorrelationValue { get; set; }
}
```

### Registering the Custom Trigger

See [CustomTriggerRegistration.cs](CustomTriggerRegistration.cs) for DI registration:

```csharp
public static class CustomTriggerExtensions
{
    public static IServiceCollection AddCustomTriggers(this IServiceCollection services)
    {
        // Register the custom activity
        services.AddActivity<CustomSignalTrigger>();
        
        // Register bookmark handlers and other supporting services
        services.AddBookmarkProvider<CustomSignalBookmarkProvider>();
        
        return services;
    }
}
```

### Resuming Custom Triggers

To resume workflows waiting for your custom trigger:

```csharp
public class CustomSignalService
{
    private readonly IWorkflowDispatcher _dispatcher;
    private readonly IBookmarkStore _bookmarkStore;
    
    public async Task PublishSignalAsync(string signalName, string? correlationValue, object? payload)
    {
        // Find bookmarks matching the signal
        var filter = new BookmarkFilter
        {
            // NOTE: Adapt filter properties to match Elsa v3 API
            PayloadType = typeof(CustomSignalBookmark).Name,
            // Additional filtering by signal name and correlation
        };
        
        var bookmarks = await _bookmarkStore.FindManyAsync(filter);
        
        // Filter bookmarks in memory if needed
        var matchingBookmarks = bookmarks
            .Where(b => /* match signal name and correlation */)
            .ToList();
        
        // Resume each matching workflow
        foreach (var bookmark in matchingBookmarks)
        {
            await _dispatcher.DispatchAsync(new DispatchWorkflowInstanceRequest
            {
                InstanceId = bookmark.WorkflowInstanceId,
                BookmarkId = bookmark.Id,
                Input = new Dictionary<string, object>
                {
                    ["SignalPayload"] = payload ?? new { }
                }
            });
        }
    }
}
```

## Resuming Workflows

There are two primary methods for resuming suspended workflows:

### Method 1: Using Elsa Dispatcher (Recommended)

Resume workflows programmatically from your application code:

```csharp
public class WorkflowResumeService
{
    private readonly IWorkflowDispatcher _dispatcher;
    
    public async Task ResumeByCorrelationIdAsync(
        string correlationId, 
        Dictionary<string, object>? input = null)
    {
        var request = new DispatchWorkflowInstanceRequest
        {
            CorrelationId = correlationId,
            Input = input ?? new Dictionary<string, object>()
        };
        
        await _dispatcher.DispatchAsync(request);
    }
    
    public async Task ResumeByInstanceIdAsync(
        string instanceId,
        string? bookmarkId = null,
        Dictionary<string, object>? input = null)
    {
        var request = new DispatchWorkflowInstanceRequest
        {
            InstanceId = instanceId,
            BookmarkId = bookmarkId,
            Input = input ?? new Dictionary<string, object>()
        };
        
        await _dispatcher.DispatchAsync(request);
    }
}
```

### Method 2: Using REST API

Resume workflows via HTTP API calls:

```bash
# Resume by correlation ID
curl -X POST https://your-app.com/elsa/api/workflow-instances/resume \
  -H "Content-Type: application/json" \
  -d '{
    "correlationId": "order-12345",
    "input": {
      "status": "approved",
      "approvedBy": "john.doe@example.com"
    }
  }'

# Resume by instance ID and bookmark
curl -X POST https://your-app.com/elsa/api/workflow-instances/{instanceId}/resume \
  -H "Content-Type: application/json" \
  -d '{
    "bookmarkId": "bookmark-abc-123",
    "input": {
      "result": "success"
    }
  }'
```

**API Endpoints:** (Exact paths may vary by Elsa v3 version)
- `POST /elsa/api/workflow-instances/resume` - Resume by correlation
- `POST /elsa/api/workflow-instances/{id}/resume` - Resume by instance ID
- `GET /elsa/api/workflow-instances?status=Suspended` - List suspended instances

## Long-Running Workflows

### Timeout Handling

Always implement timeouts for external dependencies:

```csharp
// Pattern: Fork with timeout
workflow
    .Fork(
        // Branch 1: Wait for event
        fork => fork.Then<Event>(evt => evt.EventName = "ExternalCallback"),
        
        // Branch 2: Timeout after 1 hour
        fork => fork.Then<Delay>(delay => delay.Duration = TimeSpan.FromHours(1))
    )
    .Join()
    .Then<Decision>(decision => decision.Condition = /* Check which completed first */)
```

[Studio Screenshot Placeholder: Fork/Join pattern with timeout branch]

### Retry Logic

Implement retry logic for transient failures:

```csharp
// Pattern: Retry with exponential backoff
var maxRetries = 3;
var retryDelays = new[] { 
    TimeSpan.FromSeconds(5), 
    TimeSpan.FromSeconds(15), 
    TimeSpan.FromSeconds(45) 
};

for (int attempt = 0; attempt < maxRetries; attempt++)
{
    try
    {
        // Attempt operation
        await externalService.CallAsync();
        break; // Success
    }
    catch when (attempt < maxRetries - 1)
    {
        await Task.Delay(retryDelays[attempt]);
    }
}
```

### Cancellation Tokens

Support workflow cancellation:

```csharp
// Check for cancellation
if (context.CancellationToken.IsCancellationRequested)
{
    return OutcomeNames.Cancelled;
}

// Long-running operation with cancellation support
await longRunningTask.WaitAsync(context.CancellationToken);
```

### Idempotency

Ensure resuming workflows multiple times doesn't cause duplicate processing:

```csharp
// Check if already processed
var processedOrderIds = context.GetVariable<HashSet<string>>("ProcessedOrderIds") 
    ?? new HashSet<string>();

var currentOrderId = context.GetVariable<string>("OrderId");

if (processedOrderIds.Contains(currentOrderId))
{
    return OutcomeNames.Done; // Already processed
}

// Process the order
await ProcessOrderAsync(currentOrderId);

// Mark as processed
processedOrderIds.Add(currentOrderId);
context.SetVariable("ProcessedOrderIds", processedOrderIds);
```

### Data Retention

Configure retention policies for completed workflow instances:

```csharp
services.AddElsa(elsa => elsa
    .UseWorkflowManagement(management => management
        .UseRetention(retention => retention
            .WithRetentionPolicy<CompletedWorkflowRetentionPolicy>(
                policy => policy.RetentionPeriod = TimeSpan.FromDays(30)
            )
        )
    )
);
```

## Scheduling and Timer Triggers at Scale

### Single Instance Deployments

For single-server deployments, the in-memory scheduler is sufficient:

```csharp
// In Program.cs or Startup.cs
services.AddElsa(elsa => elsa
    .UseScheduling() // Uses in-memory scheduler by default
);
```

### Clustered Deployments with Quartz

For multi-server deployments, use Quartz.NET for distributed scheduling:

See [TimerTriggerExample.cs](TimerTriggerExample.cs) for complete configuration.

```csharp
// Install Quartz integration
// dotnet add package Elsa.Quartz

services.AddElsa(elsa => elsa
    .UseQuartzScheduler(quartz => quartz
        .UsePersistence(persistence => persistence
            .UseSqlServer("YourConnectionString") // Or Postgres, MySQL, etc.
        )
        .UseCluster(cluster => cluster
            .InstanceId = Environment.MachineName
            .InstanceName = "ElsaWorkflowScheduler"
        )
    )
);
```

**Quartz Benefits:**
- Distributed locking prevents duplicate execution
- Survives server restarts
- Load balancing across cluster
- Misfired job handling

### Timezone Considerations

Always specify timezone for scheduled workflows:

```csharp
// Use UTC for consistency
.WithTimer("0 9 * * *", TimeZoneInfo.Utc)

// Or specific timezone
var timezone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
.WithTimer("0 9 * * *", timezone)
```

**Best Practices:**
- Store all dates in UTC in the database
- Convert to local time only for display
- Be aware of Daylight Saving Time transitions
- Document which timezone schedules use

### Monitoring Scheduled Workflows

```csharp
// Query upcoming scheduled workflows
var scheduledInstances = await workflowInstanceStore.FindManyAsync(
    new WorkflowInstanceFilter
    {
        Status = WorkflowStatus.Suspended,
        // Filter for timer bookmarks
    }
);

// Check for missed schedules
foreach (var instance in scheduledInstances)
{
    var bookmark = instance.Bookmarks
        .FirstOrDefault(b => b.ActivityTypeName == "Timer");
    
    if (bookmark?.Payload is TimerBookmarkPayload timer)
    {
        if (timer.ExecuteAt < DateTime.UtcNow.AddMinutes(-5))
        {
            // Schedule is overdue by more than 5 minutes
            Logger.LogWarning(
                "Workflow {InstanceId} timer is overdue: {ExecuteAt}",
                instance.Id, timer.ExecuteAt
            );
        }
    }
}
```

## Best Practices

### 1. Always Use Correlation IDs

Correlation IDs are essential for resuming the correct workflow instance:

```csharp
// Generate correlation ID early
var correlationId = context.CorrelationId ?? Guid.NewGuid().ToString();
context.CorrelationId = correlationId;

// Include in all external communications
var callbackUrl = $"https://your-app.com/api/callbacks/{correlationId}";
```

### 2. Implement Timeout Protection

Never wait indefinitely for external events:

```csharp
// Always use Fork with timeout branch
.Fork(
    mainBranch => mainBranch.Then<WaitForEvent>(),
    timeoutBranch => timeoutBranch.Then<Delay>(d => d.Duration = TimeSpan.FromHours(24))
)
.Join()
.Then<Decision>(/* Handle timeout case */)
```

### 3. Handle Concurrency

Prevent race conditions when multiple events arrive simultaneously:

```csharp
// Use optimistic concurrency in workflow instance updates
// Elsa handles this automatically, but be aware in custom code

// Check workflow status before resuming
if (instance.Status != WorkflowStatus.Suspended)
{
    // Already resumed or completed
    return;
}
```

### 4. Log and Monitor

Add comprehensive logging:

```csharp
.Then<LogMessage>(log => log.Message = 
    $"Waiting for webhook callback. CorrelationId: {context.CorrelationId}")
.Then<Event>(evt => evt.EventName = "WebhookReceived")
.Then<LogMessage>(log => log.Message = 
    $"Webhook received. Resuming workflow. CorrelationId: {context.CorrelationId}")
```

### 5. Validate Input Data

Always validate data from external triggers:

```csharp
var payload = context.GetInput<CallbackPayload>();

if (string.IsNullOrEmpty(payload?.OrderId))
{
    return OutcomeNames.Invalid;
}

if (payload.Total <= 0)
{
    return OutcomeNames.Invalid;
}

// Proceed with valid data
return OutcomeNames.Valid;
```

### 6. Design for Idempotency

External systems may retry callbacks:

```csharp
// Check if already processed
var isProcessed = context.GetVariable<bool>("IsProcessed");
if (isProcessed)
{
    Logger.LogInformation("Callback already processed, skipping");
    return OutcomeNames.Done;
}

// Process once
await ProcessCallback(payload);

// Mark as processed
context.SetVariable("IsProcessed", true);
```

### 7. Use Descriptive Activity Names

Make workflows easy to understand:

```csharp
.Then<HttpEndpoint>(endpoint => 
{
    endpoint.Name = "WaitForOrderConfirmation";
    endpoint.Path = "/api/orders/confirm";
})
```

### 8. Document Webhook Contracts

Maintain clear documentation of webhook payloads:

```csharp
/// <summary>
/// Expected webhook payload for order confirmation
/// </summary>
public class OrderConfirmationWebhook
{
    /// <example>order-12345</example>
    public string CorrelationId { get; set; } = default!;
    
    /// <example>confirmed</example>
    public string Status { get; set; } = default!;
    
    public OrderDetails Details { get; set; } = default!;
}
```

## Troubleshooting

### Workflow Not Resuming

**Symptoms:** Webhook received, but workflow stays suspended

**Checklist:**
1. Verify correlation ID matches exactly
   ```csharp
   Logger.LogInformation("Looking for correlation: {CorrelationId}", correlationId);
   var instances = await store.FindManyAsync(
       new WorkflowInstanceFilter { CorrelationId = correlationId }
   );
   Logger.LogInformation("Found {Count} instances", instances.Count);
   ```

2. Check workflow status
   ```csharp
   if (instance.Status != WorkflowStatus.Suspended)
   {
       Logger.LogWarning("Workflow is {Status}, expected Suspended", instance.Status);
   }
   ```

3. Verify bookmark exists
   ```csharp
   var bookmarks = instance.Bookmarks;
   Logger.LogInformation("Workflow has {Count} bookmarks", bookmarks.Count);
   foreach (var bookmark in bookmarks)
   {
       Logger.LogInformation("Bookmark: {Type} - {Name}", 
           bookmark.ActivityTypeName, bookmark.Name);
   }
   ```

4. Check for exceptions in logs
   ```bash
   # Search application logs
   grep -i "error\|exception" /var/log/elsa/*.log
   ```

### Timer Not Firing

**Symptoms:** Scheduled workflow doesn't execute at expected time

**Checklist:**
1. Verify scheduler is running
   ```csharp
   // Check if Quartz scheduler is active
   var scheduler = serviceProvider.GetRequiredService<IScheduler>();
   var isStarted = await scheduler.IsStarted();
   Logger.LogInformation("Scheduler started: {IsStarted}", isStarted);
   ```

2. Check timezone configuration
   ```csharp
   var timerBookmark = bookmark.Payload as TimerBookmarkPayload;
   Logger.LogInformation("Timer execute at: {ExecuteAt} (UTC: {UtcTime})",
       timerBookmark.ExecuteAt, timerBookmark.ExecuteAt.ToUniversalTime());
   ```

3. Inspect Quartz triggers (if using Quartz)
   ```sql
   -- Check Quartz triggers table
   SELECT * FROM QRTZ_TRIGGERS WHERE TRIGGER_STATE = 'WAITING';
   ```

4. Verify no clock skew in cluster
   ```bash
   # On each server, check system time
   date -u
   ```

### Duplicate Workflow Executions

**Symptoms:** Workflow resumes multiple times for single event

**Solutions:**
1. Implement idempotency checks (see Best Practices #6)
2. Use distributed locks in clustered environments
3. Remove bookmark after successful resume
4. Check for retry logic in external systems

### Memory Leaks with Long-Running Workflows

**Symptoms:** Memory usage grows over time with suspended workflows

**Solutions:**
1. Implement retention policies
2. Regularly archive completed workflows
3. Monitor workflow instance count
   ```csharp
   var suspendedCount = await store.CountAsync(
       new WorkflowInstanceFilter { Status = WorkflowStatus.Suspended }
   );
   Logger.LogInformation("Suspended workflows: {Count}", suspendedCount);
   ```

### Bookmark Not Found

**Symptoms:** Error "Bookmark not found" when resuming

**Causes:**
- Bookmark already consumed (workflow continued)
- Workflow instance was cancelled
- Bookmark expired (if using TTL)

**Solution:**
```csharp
// Check if bookmark still exists before resuming
var bookmark = await bookmarkStore.FindAsync(bookmarkId);
if (bookmark == null)
{
    Logger.LogWarning("Bookmark {BookmarkId} not found", bookmarkId);
    return NotFound();
}
```

## Example Files

This guide includes the following example files:

1. **[workflow-wait-for-webhook.json](workflow-wait-for-webhook.json)** - Complete workflow definition demonstrating HTTP callback pattern with timeout handling

2. **[ResumeWebhookController.cs](ResumeWebhookController.cs)** - ASP.NET Core controller for receiving webhooks and resuming workflows

3. **[CustomTrigger.cs](CustomTrigger.cs)** - Full implementation of a custom trigger activity

4. **[CustomTriggerRegistration.cs](CustomTriggerRegistration.cs)** - Service registration for custom triggers

5. **[HttpTriggerExample.cs](HttpTriggerExample.cs)** - Application startup configuration for HTTP triggers

6. **[TimerTriggerExample.cs](TimerTriggerExample.cs)** - Configuration for timer triggers with Quartz clustering

## Additional Resources

- [Elsa Workflows Documentation](https://elsa-workflows.github.io/elsa-core/)
- [Correlation ID Concept](../../getting-started/concepts/correlation-id.md)
- [HTTP Workflows Guide](../../guides/http-workflows/README.md)
- [Custom Activities Guide](../../extensibility/custom-activities.md)
- [Reusable Triggers](../../extensibility/reusable-triggers-3.5-preview.md)

## Next Steps

1. Review the example workflow JSON file
2. Study the resume controller implementation
3. Experiment with the custom trigger example
4. Build your first blocking workflow
5. Test with the provided curl commands

For questions or issues, please visit the [Elsa Workflows GitHub Discussions](https://github.com/elsa-workflows/elsa-core/discussions).
