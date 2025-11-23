---
description: >-
  Comprehensive guide to blocking activities and triggers in Elsa Workflows v3, including built-in trigger types, custom trigger implementation, workflow resumption patterns, and best practices.
---

# Blocking & Trigger Activities Guide

## Overview

Blocking activities and triggers are fundamental concepts in Elsa Workflows that enable powerful workflow orchestration patterns. This guide explains how blocking activities work, explores the various built-in trigger types, and provides practical examples for implementing custom triggers and managing workflow execution.

## Table of Contents

- [Conceptual Understanding](#conceptual-understanding)
- [Built-in Trigger Types](#built-in-trigger-types)
- [HTTP Trigger Tutorial](#http-trigger-tutorial)
- [Creating Custom Triggers](#creating-custom-triggers)
- [Workflow Resumption](#workflow-resumption)
- [Scheduling and Timer Triggers](#scheduling-and-timer-triggers)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

---

## Conceptual Understanding

### What are Blocking Activities?

A **blocking activity** is an activity that pauses workflow execution until an external event or condition occurs. When a workflow encounters a blocking activity, the workflow instance is persisted to storage and waits for a signal to resume execution.

Key characteristics:
- **Pauses Execution**: The workflow stops at the blocking activity and waits
- **Persisted State**: The workflow instance is saved to the database with its current state
- **Event-Driven Resume**: Execution resumes when a matching event or signal is received
- **Long-Running Support**: Enables workflows that wait for hours, days, or indefinitely

### Blocking vs Non-Blocking Activities

| Aspect | Non-Blocking Activities | Blocking Activities |
|--------|------------------------|-------------------|
| **Execution** | Completes immediately | Waits for external event |
| **Workflow State** | Continues to next activity | Pauses and persists |
| **Memory** | Remains in memory | Stored in database |
| **Duration** | Milliseconds to seconds | Minutes to indefinite |
| **Examples** | SetVariable, Log, If | HttpEndpoint, Timer, Event |

### When to Use Each Type

**Use Non-Blocking Activities when:**
- Performing calculations or data transformations
- Making synchronous API calls that complete quickly
- Implementing business logic that executes in sequence
- No external coordination is required

**Use Blocking Activities when:**
- Waiting for external HTTP requests
- Scheduling work for future execution
- Waiting for messages from external systems
- Coordinating with other workflows or services
- Implementing human approval workflows
- Handling long-running operations

### What are Triggers?

A **trigger** is a special type of blocking activity that can start a new workflow instance or resume an existing one. Triggers define how external events map to workflow execution:

- **Workflow Starters**: Activities with `CanStartWorkflow = true` can initiate new instances
- **Resumption Points**: Blocking activities that can resume paused workflows
- **Event Correlation**: Triggers use correlation logic to match events to workflow instances

---

## Built-in Trigger Types

Elsa Workflows v3 provides several built-in trigger activities for common scenarios:

### 1. HTTP Trigger (HttpEndpoint)

The `HttpEndpoint` activity creates an HTTP endpoint that can trigger or resume workflows based on incoming HTTP requests.

**Configuration Example:**

```csharp
using Elsa.Http;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using System.Net;

public class HttpTriggerWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new HttpEndpoint
                {
                    Path = new("/api/orders"),
                    SupportedMethods = new([HttpMethods.Post]),
                    CanStartWorkflow = true,
                    ParsedContent = new(true)
                },
                new WriteHttpResponse
                {
                    StatusCode = new(HttpStatusCode.Accepted),
                    Content = new("Order received and processing started")
                }
            }
        };
    }
}
```

**Key Properties:**
- `Path`: The URL path for the endpoint
- `SupportedMethods`: HTTP methods (GET, POST, PUT, DELETE, etc.)
- `CanStartWorkflow`: Whether this endpoint can start new workflow instances
- `ParsedContent`: Automatically parse request body as JSON

### 2. Timer Trigger

The `Timer` activity triggers workflows at regular intervals based on a TimeSpan.

**Configuration Example:**

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;

public class TimerWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new Timer
                {
                    Interval = new(TimeSpan.FromMinutes(5)),
                    CanStartWorkflow = true
                },
                new WriteLine
                {
                    Text = new("Timer triggered at " + DateTime.UtcNow)
                }
            }
        };
    }
}
```

**Use Cases:**
- Periodic health checks
- Scheduled data synchronization
- Regular cleanup tasks
- Heartbeat monitoring

### 3. Cron Trigger

The `Cron` activity uses CRON expressions for complex scheduling patterns.

**Configuration Example:**

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;

public class CronWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new Cron
                {
                    // Run every day at 2:00 AM
                    CronExpression = new("0 2 * * *"),
                    CanStartWorkflow = true
                },
                new WriteLine
                {
                    Text = new("Daily backup started")
                }
            }
        };
    }
}
```

**Common CRON Patterns:**
- `0 * * * *` - Every hour
- `0 0 * * *` - Daily at midnight
- `0 0 * * 0` - Weekly on Sunday
- `0 0 1 * *` - Monthly on the 1st
- `*/5 * * * *` - Every 5 minutes

### 4. Event Trigger

The `Event` activity listens for named events published through Elsa's event bus.

**Configuration Example:**

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;

public class EventWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new Event
                {
                    EventName = new("OrderPlaced"),
                    CanStartWorkflow = true
                },
                new WriteLine
                {
                    Text = new(context => 
                        $"Processing order: {context.GetInput<string>("OrderId")}")
                }
            }
        };
    }
}
```

**Publishing Events:**

```csharp
// In your application code
public class OrderService
{
    private readonly IEventPublisher _eventPublisher;

    public OrderService(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public async Task PlaceOrderAsync(Order order)
    {
        // Business logic...
        
        // Trigger workflow
        await _eventPublisher.PublishAsync("OrderPlaced", new
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Amount = order.TotalAmount
        });
    }
}
```

### 5. Delay Activity

The `Delay` activity pauses workflow execution for a specified duration.

**Configuration Example:**

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;

public class DelayWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new WriteLine { Text = new("Starting process...") },
                new Delay
                {
                    Duration = new(TimeSpan.FromMinutes(30))
                },
                new WriteLine { Text = new("Continuing after delay...") }
            }
        };
    }
}
```

---

## HTTP Trigger Tutorial

This tutorial demonstrates creating a workflow that waits for an external HTTP event and shows how to resume execution.

### Step 1: Create the Workflow

Create a workflow that accepts an HTTP request, performs some processing, waits for approval, and then completes:

```csharp
using Elsa.Http;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;
using System.Net;

namespace MyApp.Workflows;

public class OrderApprovalWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        // Create workflow variables
        var orderId = builder.WithVariable<string>("OrderId");
        var amount = builder.WithVariable<decimal>("Amount");
        var approvalToken = builder.WithVariable<string>("ApprovalToken");

        builder.Root = new Sequence
        {
            Activities =
            {
                // Step 1: Receive order via HTTP
                new HttpEndpoint
                {
                    Path = new("/api/orders/submit"),
                    SupportedMethods = new([HttpMethods.Post]),
                    CanStartWorkflow = true,
                    ParsedContent = new(true)
                },
                
                // Step 2: Extract order data
                new SetVariable
                {
                    Variable = orderId,
                    Value = new(context => 
                        context.GetInput<dynamic>("Body").orderId)
                },
                new SetVariable
                {
                    Variable = amount,
                    Value = new(context => 
                        context.GetInput<dynamic>("Body").amount)
                },
                
                // Step 3: Generate approval token
                new SetVariable
                {
                    Variable = approvalToken,
                    Value = new(context => Guid.NewGuid().ToString())
                },
                
                // Step 4: Send initial response
                new WriteHttpResponse
                {
                    StatusCode = new(HttpStatusCode.Accepted),
                    Content = new(context => 
                        $"Order {orderId.Get(context)} submitted. " +
                        $"Approval token: {approvalToken.Get(context)}")
                },
                
                // Step 5: Wait for approval via HTTP
                new HttpEndpoint
                {
                    Path = new(context => 
                        $"/api/orders/approve/{approvalToken.Get(context)}"),
                    SupportedMethods = new([HttpMethods.Post]),
                    CanStartWorkflow = false // This resumes existing workflow
                },
                
                // Step 6: Process approval
                new WriteLine
                {
                    Text = new(context => 
                        $"Order {orderId.Get(context)} approved!")
                },
                
                // Step 7: Send approval confirmation
                new WriteHttpResponse
                {
                    StatusCode = new(HttpStatusCode.OK),
                    Content = new(context => 
                        $"Order {orderId.Get(context)} has been approved")
                }
            }
        };
    }
}
```

### Step 2: Register the Workflow

In your `Program.cs`, register the workflow with Elsa:

```csharp
using Elsa.Extensions;
using MyApp.Workflows;

var builder = WebApplication.CreateBuilder(args);

// Add Elsa services
builder.Services.AddElsa(elsa =>
{
    // Configure workflow management
    elsa.UseWorkflowManagement(management =>
    {
        management.AddWorkflow<OrderApprovalWorkflow>();
    });
    
    // Configure HTTP activities
    elsa.UseHttp(http =>
    {
        http.ConfigureHttpOptions = options =>
        {
            options.BaseUrl = new Uri("https://localhost:5001");
            options.BasePath = "/workflows";
        };
    });
    
    // Use in-memory or database storage
    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.UseDefaultRuntime();
    });
});

var app = builder.Build();

// Configure HTTP pipeline
app.UseRouting();
app.UseWorkflows(); // Enable workflow HTTP endpoints
app.Run();
```

### Step 3: Workflow Definition JSON

Here's the equivalent workflow definition in JSON format for Elsa Studio:

See [HttpTriggerWorkflow.json](HttpTriggerWorkflow.json) for the complete JSON definition.

### Step 4: Testing the Workflow

**Submit Order:**
```bash
curl -X POST https://localhost:5001/workflows/api/orders/submit \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "ORD-12345",
    "amount": 599.99
  }'
```

**Response:**
```
Order ORD-12345 submitted. Approval token: a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

**Approve Order:**
```bash
curl -X POST https://localhost:5001/workflows/api/orders/approve/a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

**Response:**
```
Order ORD-12345 has been approved
```

### Understanding Correlation

In this example, the workflow uses the approval token in the URL path to correlate the approval request with the correct workflow instance. When the second HTTP request arrives:

1. Elsa matches the URL path to find workflow instances waiting at the approval endpoint
2. The workflow instance is loaded from storage
3. Execution resumes from the waiting `HttpEndpoint` activity
4. The workflow continues to completion

---

## Creating Custom Triggers

Custom triggers allow you to create domain-specific blocking activities tailored to your application's needs.

### Custom Trigger Implementation

Here's a complete example of a custom trigger that waits for external system callbacks:

See [CustomTriggerActivity.cs](CustomTriggerActivity.cs) for the full implementation.

**Custom Trigger Activity:**

```csharp
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Contracts;

namespace MyApp.Activities;

/// <summary>
/// A custom trigger that waits for external system callbacks
/// </summary>
[Activity("MyApp", "Triggers", "Waits for a callback from an external system")]
public class ExternalSystemCallback : Activity, IBlockingActivity
{
    /// <summary>
    /// The external system identifier
    /// </summary>
    [Input(
        DisplayName = "System ID",
        Description = "The external system identifier to wait for callback from"
    )]
    public Input<string> SystemId { get; set; } = default!;
    
    /// <summary>
    /// Correlation ID to match callback
    /// </summary>
    [Input(
        DisplayName = "Correlation ID",
        Description = "Unique ID to correlate callback with this workflow instance"
    )]
    public Input<string> CorrelationId { get; set; } = default!;
    
    /// <summary>
    /// Timeout duration
    /// </summary>
    [Input(
        DisplayName = "Timeout",
        Description = "Maximum time to wait for callback"
    )]
    public Input<TimeSpan?> Timeout { get; set; } = default!;
    
    /// <summary>
    /// Whether this trigger can start a new workflow
    /// </summary>
    [Input(
        DisplayName = "Can Start Workflow",
        Description = "Whether this trigger can start new workflow instances"
    )]
    public Input<bool> CanStartWorkflow { get; set; } = new(false);
    
    /// <summary>
    /// Output data received from callback
    /// </summary>
    [Output(Description = "Data received from the external system")]
    public Output<object>? CallbackData { get; set; }

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var systemId = SystemId.Get(context);
        var correlationId = CorrelationId.Get(context);
        var timeout = Timeout.GetOrDefault(context);
        
        // Create a bookmark to pause the workflow
        var bookmarkPayload = new ExternalSystemBookmark
        {
            SystemId = systemId,
            CorrelationId = correlationId
        };
        
        var options = new CreateBookmarkArgs
        {
            Payload = bookmarkPayload,
            IncludeActivityInstanceId = true,
            Callback = OnResumeAsync
        };
        
        // If timeout is specified, schedule timeout
        if (timeout.HasValue)
        {
            var timeoutBookmark = new ExternalSystemTimeoutBookmark
            {
                CorrelationId = correlationId
            };
            
            context.CreateBookmark(timeoutBookmark, OnTimeoutAsync);
            context.ScheduleTimeout(timeout.Value, timeoutBookmark);
        }
        
        // Create the main bookmark
        context.CreateBookmark(options);
    }
    
    private async ValueTask OnResumeAsync(ActivityExecutionContext context)
    {
        // Extract callback data from bookmark
        var bookmarkPayload = context.GetBookmarkPayload<ExternalSystemBookmark>();
        var callbackData = context.GetInput<object>("CallbackData");
        
        // Set output
        context.Set(CallbackData, callbackData);
        
        // Clear timeout if one was set
        var correlationId = bookmarkPayload?.CorrelationId;
        if (!string.IsNullOrEmpty(correlationId))
        {
            var timeoutBookmark = new ExternalSystemTimeoutBookmark
            {
                CorrelationId = correlationId
            };
            context.ClearBookmark(timeoutBookmark);
        }
        
        // Complete the activity
        await context.CompleteActivityAsync();
    }
    
    private async ValueTask OnTimeoutAsync(ActivityExecutionContext context)
    {
        // Handle timeout
        context.JournalData.Add("Timeout", true);
        
        // Complete with timeout outcome
        await context.CompleteActivityWithOutcomesAsync("Timeout");
    }
}

/// <summary>
/// Bookmark payload for external system callback
/// </summary>
public record ExternalSystemBookmark
{
    public string SystemId { get; init; } = default!;
    public string CorrelationId { get; init; } = default!;
}

/// <summary>
/// Bookmark payload for timeout
/// </summary>
public record ExternalSystemTimeoutBookmark
{
    public string CorrelationId { get; init; } = default!;
}
```

### Registering the Custom Trigger

Register your custom trigger in `Program.cs`:

```csharp
using Elsa.Extensions;
using MyApp.Activities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddElsa(elsa =>
{
    elsa.AddActivitiesFrom<ExternalSystemCallback>();
    
    // Configure workflow runtime to handle bookmarks
    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.UseDefaultRuntime();
    });
});

var app = builder.Build();
app.Run();
```

### Using the Custom Trigger in a Workflow

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using MyApp.Activities;

public class PaymentProcessingWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        var transactionId = builder.WithVariable<string>("TransactionId");
        
        builder.Root = new Sequence
        {
            Activities =
            {
                new SetVariable
                {
                    Variable = transactionId,
                    Value = new(context => Guid.NewGuid().ToString())
                },
                
                new WriteLine
                {
                    Text = new(context => 
                        $"Waiting for payment callback: {transactionId.Get(context)}")
                },
                
                new ExternalSystemCallback
                {
                    SystemId = new("PaymentGateway"),
                    CorrelationId = new(transactionId),
                    Timeout = new(TimeSpan.FromMinutes(15)),
                    CanStartWorkflow = new(false)
                },
                
                new WriteLine
                {
                    Text = new("Payment confirmed!")
                }
            }
        };
    }
}
```

### Handling Correlation IDs

Correlation IDs are critical for matching external events to the correct workflow instance:

**Best Practices:**
1. **Generate Unique IDs**: Use GUIDs or other guaranteed unique identifiers
2. **Store Correlation IDs**: Save them in workflow variables for later use
3. **Pass to External Systems**: Include correlation IDs in API calls, webhooks, etc.
4. **Validate on Resume**: Verify correlation IDs match when resuming workflows

**Example with Persistence:**

```csharp
public class OrderWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        var orderId = builder.WithVariable<string>("OrderId");
        var correlationId = builder.WithVariable<string>("CorrelationId");
        
        builder.Root = new Sequence
        {
            Activities =
            {
                // Generate correlation ID
                new SetVariable
                {
                    Variable = correlationId,
                    Value = new(context => $"ORDER-{Guid.NewGuid()}")
                },
                
                // Store in workflow correlation
                new Correlate
                {
                    CorrelationId = new(correlationId)
                },
                
                // Send to external system
                new SendHttpRequest
                {
                    Url = new("https://external-api.com/orders"),
                    Method = new(HttpMethod.Post),
                    Content = new(context => new
                    {
                        orderId = orderId.Get(context),
                        callbackUrl = $"https://myapp.com/callback/{correlationId.Get(context)}"
                    })
                },
                
                // Wait for callback
                new ExternalSystemCallback
                {
                    SystemId = new("ExternalAPI"),
                    CorrelationId = new(correlationId),
                    Timeout = new(TimeSpan.FromHours(24))
                }
            }
        };
    }
}
```

---

## Workflow Resumption

Resuming blocked workflows is accomplished through the Elsa workflow dispatcher and bookmark system.

### Using the Workflow Dispatcher

The `IWorkflowDispatcher` service is used to dispatch events that can resume workflows:

See [WorkflowResume.cs](WorkflowResume.cs) for complete examples.

**Basic Resume Pattern:**

```csharp
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Contracts;
using Elsa.Workflows.Runtime.Filters;

namespace MyApp.Services;

public class CallbackHandler
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IBookmarkStore _bookmarkStore;

    public CallbackHandler(
        IWorkflowRuntime workflowRuntime,
        IBookmarkStore bookmarkStore)
    {
        _workflowRuntime = workflowRuntime;
        _bookmarkStore = bookmarkStore;
    }

    public async Task HandleCallbackAsync(string correlationId, object data)
    {
        // Find bookmarks matching the correlation ID
        var filter = new BookmarkFilter
        {
            CorrelationId = correlationId
        };
        
        var bookmarks = await _bookmarkStore.FindManyAsync(filter);
        
        foreach (var bookmark in bookmarks)
        {
            // Resume workflow with input data
            var input = new Dictionary<string, object>
            {
                ["CallbackData"] = data
            };
            
            await _workflowRuntime.ResumeWorkflowAsync(
                bookmark.WorkflowInstanceId,
                bookmark.Id,
                input);
        }
    }
}
```

### Safe Resume Patterns

**1. Idempotency:**

Ensure callback handlers are idempotent to handle duplicate events:

```csharp
public class IdempotentCallbackHandler
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IDistributedCache _cache;

    public async Task<bool> HandleCallbackAsync(string eventId, string correlationId, object data)
    {
        // Check if already processed
        var cacheKey = $"callback:{eventId}";
        var processed = await _cache.GetStringAsync(cacheKey);
        
        if (processed != null)
        {
            // Already processed, skip
            return false;
        }
        
        // Process callback
        await ResumeWorkflowAsync(correlationId, data);
        
        // Mark as processed (with expiration)
        await _cache.SetStringAsync(
            cacheKey, 
            "processed", 
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            });
        
        return true;
    }
}
```

**2. Timeout Handling:**

Implement timeouts to prevent workflows from waiting indefinitely:

```csharp
public class TimeoutWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        var result = builder.WithVariable<string>("Result");
        
        builder.Root = new Sequence
        {
            Activities =
            {
                new Fork
                {
                    Branches =
                    {
                        // Primary path: wait for event
                        new Sequence
                        {
                            Activities =
                            {
                                new Event
                                {
                                    EventName = new("DataReceived")
                                },
                                new SetVariable
                                {
                                    Variable = result,
                                    Value = new("Success")
                                }
                            }
                        },
                        
                        // Timeout path
                        new Sequence
                        {
                            Activities =
                            {
                                new Delay
                                {
                                    Duration = new(TimeSpan.FromMinutes(5))
                                },
                                new SetVariable
                                {
                                    Variable = result,
                                    Value = new("Timeout")
                                }
                            }
                        }
                    }
                },
                
                new If
                {
                    Condition = new(context => result.Get(context) == "Timeout"),
                    Then = new WriteLine
                    {
                        Text = new("Operation timed out")
                    },
                    Else = new WriteLine
                    {
                        Text = new("Operation completed successfully")
                    }
                }
            }
        };
    }
}
```

**3. Retry Logic:**

Implement retry patterns for transient failures:

```csharp
public class RetryableCallbackHandler
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly ILogger<RetryableCallbackHandler> _logger;

    public async Task<bool> HandleCallbackWithRetryAsync(
        string correlationId, 
        object data,
        int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await ResumeWorkflowAsync(correlationId, data);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, 
                    "Attempt {Attempt} failed for correlation {CorrelationId}", 
                    attempt, correlationId);
                
                if (attempt == maxRetries)
                {
                    _logger.LogError(ex, 
                        "All {MaxRetries} attempts failed for correlation {CorrelationId}", 
                        maxRetries, correlationId);
                    throw;
                }
                
                // Exponential backoff
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }
        
        return false;
    }
}
```

**4. Cancellation Support:**

Handle cancellation tokens to stop long-running operations:

```csharp
public class CancellableHandler
{
    private readonly IWorkflowRuntime _workflowRuntime;

    public async Task HandleWithCancellationAsync(
        string correlationId,
        object data,
        CancellationToken cancellationToken)
    {
        try
        {
            await _workflowRuntime.ResumeWorkflowAsync(
                correlationId,
                data,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation
            // Could mark workflow as cancelled or log
        }
    }
}
```

---

## Scheduling and Timer Triggers

Timer-based triggers enable scheduled workflow execution and are crucial for batch processing and maintenance tasks.

### Timer Configuration

**Basic Timer:**

```csharp
public class PeriodicCleanupWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new Timer
                {
                    Interval = new(TimeSpan.FromHours(6)),
                    CanStartWorkflow = true
                },
                new WriteLine
                {
                    Text = new("Running cleanup tasks...")
                }
                // Cleanup activities...
            }
        };
    }
}
```

### Recurrence Patterns

**Cron Expressions:**

```csharp
public class ScheduledReportWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new Cron
                {
                    // Monday-Friday at 9:00 AM
                    CronExpression = new("0 9 * * 1-5"),
                    CanStartWorkflow = true
                },
                new WriteLine
                {
                    Text = new("Generating daily report...")
                }
                // Report generation...
            }
        };
    }
}
```

**Complex Scheduling:**

```csharp
public class MultiScheduleWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Fork
        {
            Branches =
            {
                // Hourly checks
                new Sequence
                {
                    Activities =
                    {
                        new Cron
                        {
                            CronExpression = new("0 * * * *"),
                            CanStartWorkflow = true
                        },
                        new WriteLine { Text = new("Hourly check") }
                    }
                },
                
                // Daily summary
                new Sequence
                {
                    Activities =
                    {
                        new Cron
                        {
                            CronExpression = new("0 0 * * *"),
                            CanStartWorkflow = true
                        },
                        new WriteLine { Text = new("Daily summary") }
                    }
                },
                
                // Weekly report
                new Sequence
                {
                    Activities =
                    {
                        new Cron
                        {
                            CronExpression = new("0 0 * * 0"),
                            CanStartWorkflow = true
                        },
                        new WriteLine { Text = new("Weekly report") }
                    }
                }
            }
        };
    }
}
```

### Timezone Handling

Configure timezone for scheduled tasks:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseScheduling(scheduling =>
    {
        // Set default timezone
        scheduling.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    });
});
```

**Per-Workflow Timezone:**

```csharp
public class TimezoneAwareWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new Cron
                {
                    CronExpression = new("0 9 * * *"),
                    CanStartWorkflow = true,
                    // Configure timezone in activity metadata
                }
            }
        };
    }
}
```

### Running Timers at Scale

**Distributed Environments:**

When running Elsa in distributed environments (multiple server instances), configure proper coordination:

```csharp
// Program.cs
builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowRuntime(runtime =>
    {
        // Use database for distributed locking
        runtime.UseEntityFrameworkCore();
        
        // Configure distributed lock provider
        runtime.DistributedLockProvider = sp => 
            sp.GetRequiredService<IDistributedLockProvider>();
    });
    
    elsa.UseScheduling(scheduling =>
    {
        // Enable distributed scheduling
        scheduling.UseDistributedLocking = true;
        
        // Configure polling interval
        scheduling.SweepInterval = TimeSpan.FromSeconds(30);
    });
});
```

**Scaling Considerations:**

1. **Distributed Locking**: Prevent multiple servers from triggering the same scheduled workflow
2. **Database Performance**: Ensure proper indexes on bookmark and trigger tables
3. **Polling Interval**: Balance between responsiveness and database load
4. **Partition Strategy**: Consider partitioning workflows across different server pools

**Example Configuration for High-Scale:**

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.UseEntityFrameworkCore(ef =>
        {
            ef.UsePostgreSql("connection-string");
        });
        
        // Configure for high throughput
        runtime.WorkerCount = Environment.ProcessorCount * 2;
        
        // Batch workflow starts
        runtime.MaxBatchSize = 100;
    });
    
    elsa.UseScheduling(scheduling =>
    {
        scheduling.UseDistributedLocking = true;
        scheduling.SweepInterval = TimeSpan.FromSeconds(10);
        
        // Limit concurrent scheduled executions
        scheduling.MaxConcurrentScheduledJobs = 50;
    });
});
```

---

## Best Practices

### Database Connection Pooling

When working with blocking activities, proper database connection management is critical:

**Configuration:**

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.UseEntityFrameworkCore(ef =>
        {
            ef.UseSqlServer(
                "connection-string",
                sql => sql
                    .MaxBatchSize(100)
                    .CommandTimeout(60)
                    .EnableRetryOnFailure(3));
        });
    });
});
```

**Connection String Settings:**

```
Server=localhost;Database=Elsa;User Id=elsa;Password=***;
Min Pool Size=10;Max Pool Size=100;
Connection Timeout=30;
```

### Long-Running Transactions

Avoid keeping database transactions open during blocking operations:

**Good Practice:**

```csharp
public class GoodPracticeActivity : Activity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        // 1. Complete database work first
        await SaveDataAsync();
        
        // 2. Then create bookmark (new transaction)
        context.CreateBookmark();
    }
}
```

**Bad Practice:**

```csharp
public class BadPracticeActivity : Activity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        // DON'T: Keep transaction open while blocked
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        
        context.CreateBookmark(); // WRONG: Transaction held open!
        
        // This code may never execute if workflow is blocked
        await transaction.CommitAsync();
    }
}
```

### Retention of Blocked Instances

Implement retention policies to clean up old workflow instances:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseRetention(retention =>
    {
        // Delete completed workflows after 30 days
        retention.CompletedWorkflowRetention = TimeSpan.FromDays(30);
        
        // Delete suspended workflows after 90 days
        retention.SuspendedWorkflowRetention = TimeSpan.FromDays(90);
        
        // Keep cancelled workflows for 7 days
        retention.CancelledWorkflowRetention = TimeSpan.FromDays(7);
        
        // Run cleanup daily at 2 AM
        retention.SweepInterval = TimeSpan.FromHours(24);
    });
});
```

### Concurrent Resume Handling

Protect against race conditions when multiple events might resume the same workflow:

```csharp
public class ConcurrentSafeHandler
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IDistributedLock _distributedLock;

    public async Task HandleCallbackAsync(string workflowInstanceId, object data)
    {
        var lockKey = $"workflow:{workflowInstanceId}";
        
        await using var @lock = await _distributedLock.AcquireLockAsync(
            lockKey,
            TimeSpan.FromSeconds(30));
        
        if (@lock == null)
        {
            // Another process is handling this workflow
            return;
        }
        
        // Safe to resume workflow
        await _workflowRuntime.ResumeWorkflowAsync(workflowInstanceId, data);
    }
}
```

### Idempotency

Design workflow activities and resume handlers to be idempotent:

**Idempotent Activity:**

```csharp
public class IdempotentPaymentActivity : Activity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var transactionId = context.Get<string>("TransactionId");
        
        // Check if already processed
        var existing = await _paymentService.GetTransactionAsync(transactionId);
        if (existing != null)
        {
            // Already processed, return existing result
            context.Set("PaymentResult", existing);
            await context.CompleteActivityAsync();
            return;
        }
        
        // Process payment
        var result = await _paymentService.ProcessPaymentAsync(transactionId);
        context.Set("PaymentResult", result);
        await context.CompleteActivityAsync();
    }
}
```

### Resource Cleanup

Properly clean up resources when workflows are cancelled or fail:

```csharp
public class ResourceAwareActivity : Activity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        IDisposable? resource = null;
        
        try
        {
            resource = await AcquireResourceAsync();
            
            // Do work with resource
            await ProcessAsync(resource);
            
            // Create bookmark if needed
            context.CreateBookmark();
        }
        catch (Exception ex)
        {
            context.JournalData.Add("Error", ex.Message);
            throw;
        }
        finally
        {
            // Always cleanup
            resource?.Dispose();
        }
    }
}
```

### Monitoring and Observability

Implement comprehensive logging and monitoring:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowRuntime(runtime =>
    {
        // Add telemetry
        runtime.AddTelemetryProvider<OpenTelemetryProvider>();
        
        // Configure logging
        runtime.ConfigureLogging(logging =>
        {
            logging.LogLevel = LogLevel.Information;
            logging.IncludeActivityData = true;
        });
    });
});
```

**Custom Telemetry:**

```csharp
public class CustomTriggerActivity : Activity
{
    private readonly ILogger<CustomTriggerActivity> _logger;
    private readonly IMetrics _metrics;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        using var activity = Activity.StartActivity("CustomTrigger");
        activity?.SetTag("workflow.id", context.WorkflowExecutionContext.Id);
        
        _metrics.IncrementCounter("custom_trigger_executions");
        
        try
        {
            context.CreateBookmark();
            _logger.LogInformation(
                "Workflow {WorkflowId} blocked at {ActivityId}",
                context.WorkflowExecutionContext.Id,
                context.ActivityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bookmark");
            _metrics.IncrementCounter("custom_trigger_errors");
            throw;
        }
    }
}
```

---

## Troubleshooting

### Workflows Not Resuming

**Problem**: Workflow remains blocked even after sending resume signal.

**Solutions**:

1. **Check Bookmark Exists:**

```csharp
// Verify bookmarks in database
var bookmarks = await _bookmarkStore.FindManyAsync(new BookmarkFilter
{
    WorkflowInstanceId = workflowInstanceId
});

Console.WriteLine($"Found {bookmarks.Count} bookmarks");
foreach (var bookmark in bookmarks)
{
    Console.WriteLine($"  - {bookmark.Name}: {bookmark.Hash}");
}
```

2. **Verify Correlation Match:**

Ensure the correlation ID or bookmark hash matches exactly:

```csharp
// When creating bookmark
var bookmarkPayload = new MyBookmark
{
    CorrelationId = correlationId // Must be exact
};

// When resuming
var filter = new BookmarkFilter
{
    Hash = BookmarkHasher.Hash(typeof(MyBookmark).Name, correlationId)
};
```

3. **Check Workflow State:**

```csharp
var instance = await _workflowInstanceStore.FindAsync(workflowInstanceId);
Console.WriteLine($"Status: {instance.Status}");
Console.WriteLine($"Sub-status: {instance.SubStatus}");

// Workflow must be in Suspended or Running state to resume
if (instance.Status != WorkflowStatus.Running)
{
    Console.WriteLine("Workflow cannot be resumed from current state");
}
```

4. **Enable Debug Logging:**

```json
{
  "Logging": {
    "LogLevel": {
      "Elsa": "Debug",
      "Elsa.Workflows.Runtime": "Trace"
    }
  }
}
```

### Duplicate Triggers

**Problem**: Multiple workflow instances created when only one expected.

**Solutions**:

1. **Singleton Workflows:**

```csharp
public class SingletonWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.WorkflowOptions.ActivationStrategy = 
            WorkflowActivationStrategy.Singleton;
        
        builder.Root = new HttpEndpoint
        {
            Path = new("/api/singleton"),
            CanStartWorkflow = true
        };
    }
}
```

2. **Correlation-Based Deduplication:**

```csharp
public class DeduplicatedWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        var orderId = builder.WithVariable<string>("OrderId");
        
        builder.Root = new Sequence
        {
            Activities =
            {
                new HttpEndpoint
                {
                    Path = new("/api/orders"),
                    CanStartWorkflow = true
                },
                
                // Set correlation early
                new Correlate
                {
                    CorrelationId = new(context => 
                        context.GetInput<dynamic>("Body").orderId)
                },
                
                // Subsequent triggers won't create new instances
                // if correlation ID matches
            }
        };
    }
}
```

3. **Distributed Lock on Start:**

```csharp
public class LockedStartHandler
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IDistributedLock _distributedLock;

    public async Task<string?> StartWorkflowAsync(string correlationId)
    {
        var lockKey = $"workflow:start:{correlationId}";
        
        await using var @lock = await _distributedLock.AcquireLockAsync(
            lockKey,
            TimeSpan.FromSeconds(10));
        
        if (@lock == null)
        {
            return null; // Another process is starting this workflow
        }
        
        // Check if workflow already exists
        var existing = await FindExistingWorkflowAsync(correlationId);
        if (existing != null)
        {
            return existing.Id;
        }
        
        // Start new workflow
        var result = await _workflowRuntime.StartWorkflowAsync(
            "MyWorkflow",
            new { CorrelationId = correlationId });
        
        return result.WorkflowInstanceId;
    }
}
```

### Missing Correlation IDs

**Problem**: Cannot correlate external events with workflow instances.

**Solutions**:

1. **Generate and Store Early:**

```csharp
public class CorrelationWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        var correlationId = builder.WithVariable<string>("CorrelationId");
        
        builder.Root = new Sequence
        {
            Activities =
            {
                // Generate correlation ID immediately
                new SetVariable
                {
                    Variable = correlationId,
                    Value = new(context => Guid.NewGuid().ToString())
                },
                
                // Apply correlation to workflow instance
                new Correlate
                {
                    CorrelationId = new(correlationId)
                },
                
                // Include in all external communications
                new SendHttpRequest
                {
                    Url = new("https://api.example.com/process"),
                    Content = new(context => new
                    {
                        data = "...",
                        callbackToken = correlationId.Get(context)
                    })
                }
            }
        };
    }
}
```

2. **Use Workflow Instance ID:**

If no custom correlation ID is needed, use the workflow instance ID:

```csharp
public class InstanceIdWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new SendHttpRequest
                {
                    Url = new("https://api.example.com/process"),
                    Content = new(context => new
                    {
                        workflowId = context.WorkflowExecutionContext.Id,
                        callbackUrl = $"https://myapp.com/callback/{context.WorkflowExecutionContext.Id}"
                    })
                }
            }
        };
    }
}
```

3. **Validation:**

```csharp
public class CallbackController : ControllerBase
{
    private readonly IWorkflowRuntime _workflowRuntime;

    [HttpPost("callback/{correlationId}")]
    public async Task<IActionResult> HandleCallback(string correlationId, [FromBody] object data)
    {
        if (string.IsNullOrEmpty(correlationId))
        {
            return BadRequest("Correlation ID is required");
        }
        
        // Find workflow by correlation
        var bookmarks = await _bookmarkStore.FindManyAsync(new BookmarkFilter
        {
            CorrelationId = correlationId
        });
        
        if (!bookmarks.Any())
        {
            return NotFound($"No workflow found for correlation ID: {correlationId}");
        }
        
        // Resume workflow
        foreach (var bookmark in bookmarks)
        {
            await _workflowRuntime.ResumeWorkflowAsync(
                bookmark.WorkflowInstanceId,
                bookmark.Id,
                new Dictionary<string, object> { ["Data"] = data });
        }
        
        return Ok();
    }
}
```

### Performance Issues

**Problem**: Slow workflow resumption or high database load.

**Solutions**:

1. **Add Database Indexes:**

```sql
-- Bookmark indexes
CREATE INDEX IX_Bookmarks_Hash ON Bookmarks(Hash);
CREATE INDEX IX_Bookmarks_CorrelationId ON Bookmarks(CorrelationId);
CREATE INDEX IX_Bookmarks_WorkflowInstanceId ON Bookmarks(WorkflowInstanceId);

-- Workflow instance indexes
CREATE INDEX IX_WorkflowInstances_CorrelationId ON WorkflowInstances(CorrelationId);
CREATE INDEX IX_WorkflowInstances_Status ON WorkflowInstances(Status);
CREATE INDEX IX_WorkflowInstances_DefinitionId ON WorkflowInstances(DefinitionId);
```

2. **Optimize Bookmark Queries:**

```csharp
// Use specific filters instead of loading all bookmarks
var filter = new BookmarkFilter
{
    Hash = bookmarkHash, // Most specific
    WorkflowInstanceId = workflowId
};

// Better than
var filter = new BookmarkFilter(); // Loads everything
```

3. **Batch Operations:**

```csharp
public async Task ResumeMultipleWorkflowsAsync(List<string> correlationIds)
{
    // Batch load bookmarks
    var allBookmarks = await _bookmarkStore.FindManyAsync(new BookmarkFilter
    {
        CorrelationId = correlationIds.ToArray()
    });
    
    // Group by workflow instance
    var grouped = allBookmarks.GroupBy(b => b.WorkflowInstanceId);
    
    // Resume in parallel (with throttling)
    var semaphore = new SemaphoreSlim(10); // Max 10 concurrent
    var tasks = grouped.Select(async group =>
    {
        await semaphore.WaitAsync();
        try
        {
            await _workflowRuntime.ResumeWorkflowAsync(
                group.Key,
                group.First().Id);
        }
        finally
        {
            semaphore.Release();
        }
    });
    
    await Task.WhenAll(tasks);
}
```

### Timeout Not Working

**Problem**: Workflows don't timeout as expected.

**Solutions**:

1. **Check Scheduler Running:**

```csharp
// Ensure scheduling is enabled
builder.Services.AddElsa(elsa =>
{
    elsa.UseScheduling(); // Must be called
    
    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.UseDefaultRuntime();
    });
});
```

2. **Verify Timer Service:**

```csharp
// Check if timer service is running
public class StartupHealthCheck
{
    private readonly IScheduler _scheduler;

    public async Task<bool> IsSchedulerHealthyAsync()
    {
        // Implementation depends on Elsa version
        // Check if scheduler is processing jobs
        return true;
    }
}
```

3. **Explicit Timeout Management:**

```csharp
public class TimeoutManagedActivity : Activity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var timeout = TimeSpan.FromMinutes(5);
        
        // Create main bookmark
        var mainBookmark = new MainBookmark { Id = Guid.NewGuid().ToString() };
        context.CreateBookmark(mainBookmark, OnMainResumeAsync);
        
        // Create timeout bookmark
        var timeoutBookmark = new TimeoutBookmark { Id = mainBookmark.Id };
        context.CreateBookmark(timeoutBookmark, OnTimeoutAsync);
        
        // Schedule timeout
        context.ScheduleActivity(
            new Delay { Duration = new(timeout) },
            OnTimeoutCompleted);
    }
    
    private async ValueTask OnMainResumeAsync(ActivityExecutionContext context)
    {
        // Clear timeout
        await context.CompleteActivityAsync();
    }
    
    private async ValueTask OnTimeoutAsync(ActivityExecutionContext context)
    {
        // Handle timeout
        await context.CompleteActivityWithOutcomesAsync("Timeout");
    }
}
```

---

## Example Files

This guide includes several example files to help you implement blocking activities and triggers:

### Supporting Files

- **[CustomTriggerActivity.cs](CustomTriggerActivity.cs)** - Complete implementation of a custom blocking trigger activity with correlation and timeout support
- **[HttpTriggerWorkflow.json](HttpTriggerWorkflow.json)** - Workflow definition in JSON format showing HTTP trigger configuration
- **[Program.cs](Program.cs)** - Example service registration and configuration for Elsa with blocking activities
- **[WorkflowResume.cs](WorkflowResume.cs)** - Examples of safe workflow resumption patterns with retry logic and idempotency

### Example Screenshots

<!-- Placeholder for Elsa Studio workflow designer showing blocking activity -->
![Workflow Designer with Blocking Activities](placeholder-workflow-designer.png)

<!-- Placeholder for workflow execution showing blocked state -->
![Blocked Workflow Instance in Studio](placeholder-blocked-instance.png)

<!-- Placeholder for bookmark inspection view -->
![Bookmark Inspection View](placeholder-bookmarks.png)

---

## Additional Resources

- [Custom Activities Guide](../../extensibility/custom-activities.md)
- [Reusable Triggers (3.5-preview)](../../extensibility/reusable-triggers-3.5-preview.md)
- [Correlation ID Concepts](../../getting-started/concepts/correlation-id.md)
- [HTTP Workflows Guide](../../guides/http-workflows/README.md)
- [Running Workflows - Using a Trigger](../../guides/running-workflows/using-a-trigger.md)

---

## Summary

Blocking activities and triggers are powerful features in Elsa Workflows v3 that enable:

- **Long-running workflows** that can wait for external events
- **Event-driven orchestration** with HTTP, Timer, and custom triggers
- **Scalable workflow execution** across distributed systems
- **Reliable workflow resumption** with correlation and idempotency

By following the patterns and best practices in this guide, you can build robust, production-ready workflows that handle complex business processes with confidence.

For questions or issues, visit the [Elsa Workflows GitHub repository](https://github.com/elsa-workflows/elsa-core) or join the community discussions.
