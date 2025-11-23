using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Contracts;
using Microsoft.Extensions.Logging;

namespace MyApp.Activities;

/// <summary>
/// A comprehensive example of a custom blocking trigger activity that demonstrates
/// correlation, timeouts, and proper bookmark management.
/// </summary>
[Activity("MyApp", "Triggers", "Waits for a callback from an external system with timeout support")]
public class ExternalSystemCallback : Activity, IBlockingActivity
{
    private readonly ILogger<ExternalSystemCallback>? _logger;

    public ExternalSystemCallback(ILogger<ExternalSystemCallback>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// The external system identifier
    /// </summary>
    [Input(
        DisplayName = "System ID",
        Description = "The external system identifier to wait for callback from",
        Category = "Settings"
    )]
    public Input<string> SystemId { get; set; } = default!;
    
    /// <summary>
    /// Correlation ID to match callback
    /// </summary>
    [Input(
        DisplayName = "Correlation ID",
        Description = "Unique ID to correlate callback with this workflow instance",
        Category = "Settings"
    )]
    public Input<string> CorrelationId { get; set; } = default!;
    
    /// <summary>
    /// Timeout duration
    /// </summary>
    [Input(
        DisplayName = "Timeout",
        Description = "Maximum time to wait for callback (optional)",
        Category = "Settings"
    )]
    public Input<TimeSpan?> Timeout { get; set; } = default!;
    
    /// <summary>
    /// Whether this trigger can start a new workflow
    /// </summary>
    [Input(
        DisplayName = "Can Start Workflow",
        Description = "Whether this trigger can start new workflow instances",
        Category = "Behavior",
        DefaultValue = false
    )]
    public Input<bool> CanStartWorkflow { get; set; } = new(false);
    
    /// <summary>
    /// Output data received from callback
    /// </summary>
    [Output(
        DisplayName = "Callback Data",
        Description = "Data received from the external system"
    )]
    public Output<object>? CallbackData { get; set; }
    
    /// <summary>
    /// Whether the activity timed out
    /// </summary>
    [Output(
        DisplayName = "Timed Out",
        Description = "True if the activity timed out waiting for callback"
    )]
    public Output<bool>? TimedOut { get; set; }

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var systemId = SystemId.Get(context);
        var correlationId = CorrelationId.Get(context);
        var timeout = Timeout.GetOrDefault(context);
        
        _logger?.LogInformation(
            "Creating bookmark for system {SystemId} with correlation {CorrelationId}",
            systemId,
            correlationId);
        
        // Create a bookmark to pause the workflow
        var bookmarkPayload = new ExternalSystemBookmark
        {
            SystemId = systemId,
            CorrelationId = correlationId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        
        var options = new CreateBookmarkArgs
        {
            Payload = bookmarkPayload,
            IncludeActivityInstanceId = true,
            Callback = OnResumeAsync
        };
        
        // If timeout is specified, schedule timeout
        if (timeout.HasValue && timeout.Value > TimeSpan.Zero)
        {
            _logger?.LogInformation(
                "Scheduling timeout of {Timeout} for correlation {CorrelationId}",
                timeout.Value,
                correlationId);
            
            var timeoutBookmark = new ExternalSystemTimeoutBookmark
            {
                CorrelationId = correlationId
            };
            
            // Create timeout bookmark
            context.CreateBookmark(
                timeoutBookmark, 
                OnTimeoutAsync,
                includeActivityInstanceId: true);
            
            // Schedule the timeout using Delay activity
            context.ScheduleActivity(
                new Delay { Duration = new(timeout.Value) },
                OnTimeoutCompletedAsync);
        }
        
        // Create the main bookmark that external systems will resume
        context.CreateBookmark(options);
        
        _logger?.LogDebug(
            "Bookmark created successfully for correlation {CorrelationId}",
            correlationId);
    }
    
    /// <summary>
    /// Called when external system sends callback
    /// </summary>
    private async ValueTask OnResumeAsync(ActivityExecutionContext context)
    {
        var bookmarkPayload = context.GetBookmarkPayload<ExternalSystemBookmark>();
        var callbackData = context.GetInput<object>("CallbackData");
        
        _logger?.LogInformation(
            "Callback received for correlation {CorrelationId} from system {SystemId}",
            bookmarkPayload?.CorrelationId,
            bookmarkPayload?.SystemId);
        
        // Set outputs
        context.Set(CallbackData, callbackData);
        context.Set(TimedOut, false);
        
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
        
        // Log to journal for debugging
        context.JournalData.Add("CallbackReceivedAt", DateTimeOffset.UtcNow);
        context.JournalData.Add("CallbackData", callbackData);
        
        // Complete with success outcome
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
    
    /// <summary>
    /// Called when timeout delay completes
    /// </summary>
    private async ValueTask OnTimeoutCompletedAsync(ActivityExecutionContext context, ActivityExecutionContext childContext)
    {
        _logger?.LogWarning(
            "Timeout elapsed for activity {ActivityId}",
            context.ActivityId);
        
        // Resume the timeout bookmark
        var correlationId = CorrelationId.Get(context);
        var timeoutBookmark = new ExternalSystemTimeoutBookmark
        {
            CorrelationId = correlationId
        };
        
        await context.ResumeBookmarkAsync(timeoutBookmark);
    }
    
    /// <summary>
    /// Called when timeout bookmark is resumed
    /// </summary>
    private async ValueTask OnTimeoutAsync(ActivityExecutionContext context)
    {
        var bookmarkPayload = context.GetBookmarkPayload<ExternalSystemTimeoutBookmark>();
        
        _logger?.LogWarning(
            "Activity timed out for correlation {CorrelationId}",
            bookmarkPayload?.CorrelationId);
        
        // Set outputs
        context.Set(CallbackData, null);
        context.Set(TimedOut, true);
        
        // Clear main bookmark
        var correlationId = bookmarkPayload?.CorrelationId;
        if (!string.IsNullOrEmpty(correlationId))
        {
            var mainBookmark = new ExternalSystemBookmark
            {
                SystemId = SystemId.Get(context),
                CorrelationId = correlationId,
                CreatedAt = DateTimeOffset.UtcNow // Not used for clearing
            };
            context.ClearBookmark(mainBookmark);
        }
        
        // Log to journal
        context.JournalData.Add("TimedOut", true);
        context.JournalData.Add("TimeoutAt", DateTimeOffset.UtcNow);
        
        // Complete with timeout outcome
        await context.CompleteActivityWithOutcomesAsync("Timeout");
    }
}

/// <summary>
/// Bookmark payload for external system callback.
/// This is serialized and stored in the database, so keep it simple and serializable.
/// </summary>
public record ExternalSystemBookmark
{
    /// <summary>
    /// The external system identifier
    /// </summary>
    public string SystemId { get; init; } = default!;
    
    /// <summary>
    /// Correlation ID for matching callbacks
    /// </summary>
    public string CorrelationId { get; init; } = default!;
    
    /// <summary>
    /// When this bookmark was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Bookmark payload for timeout handling.
/// Separate from main bookmark to allow independent resumption.
/// </summary>
public record ExternalSystemTimeoutBookmark
{
    /// <summary>
    /// Correlation ID matching the main bookmark
    /// </summary>
    public string CorrelationId { get; init; } = default!;
}

/// <summary>
/// Example usage in a workflow
/// </summary>
public class ExternalSystemWorkflowExample : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        var transactionId = builder.WithVariable<string>("TransactionId");
        var result = builder.WithVariable<object>("Result");
        var timedOut = builder.WithVariable<bool>("TimedOut");
        
        builder.Root = new Sequence
        {
            Activities =
            {
                // Generate transaction ID
                new SetVariable
                {
                    Variable = transactionId,
                    Value = new(context => $"TXN-{Guid.NewGuid()}")
                },
                
                // Log start
                new WriteLine
                {
                    Text = new(context => 
                        $"Waiting for callback for transaction {transactionId.Get(context)}")
                },
                
                // Wait for external callback with 15-minute timeout
                new ExternalSystemCallback
                {
                    SystemId = new("PaymentGateway"),
                    CorrelationId = new(transactionId),
                    Timeout = new(TimeSpan.FromMinutes(15)),
                    CanStartWorkflow = new(false),
                    CallbackData = new(result),
                    TimedOut = new(timedOut)
                },
                
                // Check if timed out
                new If
                {
                    Condition = new(timedOut),
                    Then = new Sequence
                    {
                        Activities =
                        {
                            new WriteLine
                            {
                                Text = new(context => 
                                    $"Transaction {transactionId.Get(context)} timed out")
                            }
                        }
                    },
                    Else = new Sequence
                    {
                        Activities =
                        {
                            new WriteLine
                            {
                                Text = new(context => 
                                    $"Transaction {transactionId.Get(context)} completed successfully")
                            }
                        }
                    }
                }
            }
        };
    }
}

/// <summary>
/// Example service for resuming the workflow when callback is received
/// </summary>
public class ExternalSystemCallbackService
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IBookmarkStore _bookmarkStore;
    private readonly ILogger<ExternalSystemCallbackService> _logger;

    public ExternalSystemCallbackService(
        IWorkflowRuntime workflowRuntime,
        IBookmarkStore bookmarkStore,
        ILogger<ExternalSystemCallbackService> logger)
    {
        _workflowRuntime = workflowRuntime;
        _bookmarkStore = bookmarkStore;
        _logger = logger;
    }

    /// <summary>
    /// Handle callback from external system
    /// </summary>
    public async Task<bool> HandleCallbackAsync(
        string systemId, 
        string correlationId, 
        object callbackData,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing callback from {SystemId} for correlation {CorrelationId}",
            systemId,
            correlationId);
        
        try
        {
            // Find workflows waiting for this callback
            var bookmarkPayload = new ExternalSystemBookmark
            {
                SystemId = systemId,
                CorrelationId = correlationId,
                CreatedAt = DateTimeOffset.UtcNow // Value doesn't matter for lookup
            };
            
            var filter = new BookmarkFilter
            {
                Hash = BookmarkHasher.Hash(bookmarkPayload)
            };
            
            var bookmarks = await _bookmarkStore.FindManyAsync(filter, cancellationToken);
            
            if (!bookmarks.Any())
            {
                _logger.LogWarning(
                    "No bookmarks found for system {SystemId} and correlation {CorrelationId}",
                    systemId,
                    correlationId);
                return false;
            }
            
            _logger.LogInformation(
                "Found {Count} workflow(s) to resume",
                bookmarks.Count);
            
            // Resume each workflow
            foreach (var bookmark in bookmarks)
            {
                var input = new Dictionary<string, object>
                {
                    ["CallbackData"] = callbackData
                };
                
                await _workflowRuntime.ResumeWorkflowAsync(
                    bookmark.WorkflowInstanceId,
                    bookmark.Id,
                    input,
                    cancellationToken);
                
                _logger.LogInformation(
                    "Resumed workflow {WorkflowInstanceId}",
                    bookmark.WorkflowInstanceId);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling callback from {SystemId} for correlation {CorrelationId}",
                systemId,
                correlationId);
            throw;
        }
    }
}
