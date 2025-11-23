using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Contracts;
using Elsa.Workflows.Runtime.Filters;

namespace YourApp.Workflows.CustomActivities;

/// <summary>
/// Custom trigger activity that waits for a named signal with optional correlation.
/// This demonstrates how to build a reusable trigger that can both start and resume workflows.
/// </summary>
[Activity(
    Namespace = "Custom.Triggers",
    DisplayName = "Custom Signal Trigger",
    Description = "Waits for a custom signal event with correlation support. Can be used to start or resume workflows."
)]
public class CustomSignalTrigger : Activity
{
    /// <summary>
    /// The name of the signal to wait for.
    /// This allows multiple signal types to coexist.
    /// </summary>
    [Input(
        DisplayName = "Signal Name",
        Description = "The name of the signal event to wait for (e.g., 'OrderApproved', 'PaymentReceived')",
        DefaultValue = "CustomSignal"
    )]
    public Input<string> SignalName { get; set; } = new("CustomSignal");

    /// <summary>
    /// Optional correlation value to match specific workflow instances.
    /// If provided, only signals with matching correlation will resume this workflow.
    /// </summary>
    [Input(
        DisplayName = "Correlation Value",
        Description = "Optional correlation value to match against the signal (e.g., order ID, user ID)"
    )]
    public Input<string?> CorrelationValue { get; set; } = default!;

    /// <summary>
    /// Whether this trigger can start new workflow instances.
    /// Set to true if you want this signal to create new workflows.
    /// </summary>
    [Input(
        DisplayName = "Can Start Workflow",
        Description = "Whether receiving this signal can start a new workflow instance",
        DefaultValue = false
    )]
    public Input<bool> CanStartWorkflow { get; set; } = new(false);

    /// <summary>
    /// Output containing the signal payload received when the workflow resumes.
    /// </summary>
    [Output(
        DisplayName = "Signal Payload",
        Description = "The data payload received with the signal"
    )]
    public Output<object?> SignalPayload { get; set; } = default!;

    /// <inheritdoc />
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        // Get the signal name and correlation value from inputs
        var signalName = context.Get(SignalName);
        var correlationValue = context.Get(CorrelationValue);

        // Check if we're resuming from a bookmark (signal was received)
        if (context.GetIsBookmarkResumed())
        {
            // Signal was received, extract the payload
            // NOTE: The exact API for getting bookmark input may vary by Elsa v3 version
            var payload = context.GetWorkflowInput("SignalPayload");
            context.Set(SignalPayload, payload);
            
            await context.CompleteActivityAsync();
            return;
        }

        // Create a bookmark to wait for the signal
        // NOTE: Bookmark creation API may vary by Elsa v3 version
        var bookmarkPayload = new CustomSignalBookmark
        {
            SignalName = signalName!,
            CorrelationValue = correlationValue
        };

        // Create the bookmark with a unique name
        var bookmarkName = $"CustomSignal:{signalName}";
        if (!string.IsNullOrEmpty(correlationValue))
        {
            bookmarkName += $":{correlationValue}";
        }

        // NOTE: The CreateBookmark method signature may vary by Elsa v3 version
        // Some versions may use different parameters or options
        context.CreateBookmark(
            bookmarkPayload, 
            callback: OnResumeAsync,
            includeActivityInstanceId: false);

        // The workflow will suspend here and wait for the signal
        // No need to explicitly call Suspend() - it's implicit when creating a bookmark
    }

    /// <summary>
    /// Called when the bookmark is resumed (signal received).
    /// NOTE: This callback pattern may vary by Elsa v3 version.
    /// </summary>
    private async ValueTask OnResumeAsync(ActivityExecutionContext context)
    {
        // Extract the signal payload from the resume input
        var payload = context.GetWorkflowInput("SignalPayload");
        context.Set(SignalPayload, payload);
        
        await context.CompleteActivityAsync();
    }
}

/// <summary>
/// Bookmark payload for the custom signal trigger.
/// This is persisted with the workflow instance and used for correlation.
/// </summary>
public record CustomSignalBookmark
{
    /// <summary>
    /// The signal name this bookmark is waiting for
    /// </summary>
    public string SignalName { get; set; } = default!;

    /// <summary>
    /// Optional correlation value for matching specific instances
    /// </summary>
    public string? CorrelationValue { get; set; }
}

/// <summary>
/// Bookmark provider for the custom signal trigger.
/// This helps Elsa match incoming signals to the correct bookmarks.
/// NOTE: Interface and implementation may vary by Elsa v3 version.
/// </summary>
public class CustomSignalBookmarkProvider : IBookmarkProvider
{
    /// <summary>
    /// Gets the bookmark type name for this provider
    /// </summary>
    public string GetBookmarkName() => nameof(CustomSignalBookmark);

    /// <summary>
    /// Determines if a bookmark matches the given filter.
    /// This is called when a signal is published to find matching workflows.
    /// </summary>
    public async ValueTask<bool> MatchesAsync(
        IBookmark bookmark,
        BookmarkMatchContext matchContext,
        CancellationToken cancellationToken = default)
    {
        // Extract signal information from the match context
        // NOTE: The way to access match context data may vary by Elsa v3 version
        var signalName = matchContext.GetInput<string>("SignalName");
        var correlationValue = matchContext.GetInput<string?>("CorrelationValue");

        // Deserialize the bookmark payload
        // NOTE: Payload deserialization API may vary by version
        var bookmarkPayload = bookmark.GetPayload<CustomSignalBookmark>();

        // Match signal name
        if (!string.Equals(bookmarkPayload.SignalName, signalName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Match correlation value if provided
        if (!string.IsNullOrEmpty(bookmarkPayload.CorrelationValue))
        {
            if (!string.Equals(bookmarkPayload.CorrelationValue, correlationValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Service for publishing custom signals to resume workflows.
/// This is the external API that other parts of your application use to trigger workflows.
/// </summary>
public class CustomSignalService
{
    private readonly IWorkflowDispatcher _dispatcher;
    private readonly IBookmarkStore _bookmarkStore;
    private readonly IWorkflowInstanceStore _workflowInstanceStore;
    private readonly ILogger<CustomSignalService> _logger;

    public CustomSignalService(
        IWorkflowDispatcher dispatcher,
        IBookmarkStore bookmarkStore,
        IWorkflowInstanceStore workflowInstanceStore,
        ILogger<CustomSignalService> logger)
    {
        _dispatcher = dispatcher;
        _bookmarkStore = bookmarkStore;
        _workflowInstanceStore = workflowInstanceStore;
        _logger = logger;
    }

    /// <summary>
    /// Publishes a custom signal that resumes all matching workflows.
    /// </summary>
    /// <param name="signalName">The signal name (must match the trigger's SignalName)</param>
    /// <param name="correlationValue">Optional correlation value for targeting specific workflows</param>
    /// <param name="payload">Data payload to pass to the resuming workflow</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of workflows resumed</returns>
    public async Task<int> PublishSignalAsync(
        string signalName,
        string? correlationValue = null,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Publishing custom signal: {SignalName}, Correlation: {Correlation}",
            signalName,
            correlationValue ?? "none");

        // Find bookmarks matching the custom signal type
        // NOTE: Filter API may vary by Elsa v3 version
        var bookmarkFilter = new BookmarkFilter
        {
            // Filter by bookmark type/name
            // The exact property names may vary by version
        };

        var bookmarks = await _bookmarkStore.FindManyAsync(bookmarkFilter, cancellationToken);

        // Filter bookmarks in memory to match signal and correlation
        var matchingBookmarks = bookmarks
            .Where(b =>
            {
                // NOTE: Payload access may vary by version
                if (b.Payload is not CustomSignalBookmark bookmarkPayload)
                    return false;

                // Match signal name
                if (!string.Equals(bookmarkPayload.SignalName, signalName, StringComparison.OrdinalIgnoreCase))
                    return false;

                // Match correlation if provided
                if (!string.IsNullOrEmpty(bookmarkPayload.CorrelationValue) &&
                    !string.Equals(bookmarkPayload.CorrelationValue, correlationValue, StringComparison.Ordinal))
                    return false;

                return true;
            })
            .ToList();

        _logger.LogInformation(
            "Found {Count} matching bookmarks for signal {SignalName}",
            matchingBookmarks.Count,
            signalName);

        // Resume each matching workflow
        var resumedCount = 0;
        foreach (var bookmark in matchingBookmarks)
        {
            try
            {
                // Prepare input data for the resuming workflow
                var input = new Dictionary<string, object>
                {
                    ["SignalPayload"] = payload ?? new { },
                    ["SignalName"] = signalName,
                    ["ReceivedAt"] = DateTime.UtcNow
                };

                // Dispatch the workflow resume
                // NOTE: Request properties may vary by Elsa v3 version
                var dispatchRequest = new DispatchWorkflowInstanceRequest
                {
                    InstanceId = bookmark.WorkflowInstanceId,
                    BookmarkId = bookmark.Id,
                    Input = input
                };

                await _dispatcher.DispatchAsync(dispatchRequest, cancellationToken);

                _logger.LogInformation(
                    "Resumed workflow instance {InstanceId} for signal {SignalName}",
                    bookmark.WorkflowInstanceId,
                    signalName);

                resumedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error resuming workflow instance {InstanceId} for signal {SignalName}",
                    bookmark.WorkflowInstanceId,
                    signalName);
            }
        }

        _logger.LogInformation(
            "Successfully resumed {ResumedCount} of {TotalCount} workflows for signal {SignalName}",
            resumedCount,
            matchingBookmarks.Count,
            signalName);

        return resumedCount;
    }

    /// <summary>
    /// Gets all workflows currently waiting for a specific signal.
    /// Useful for monitoring and debugging.
    /// </summary>
    public async Task<List<WaitingWorkflowInfo>> GetWorkflowsWaitingForSignalAsync(
        string signalName,
        string? correlationValue = null,
        CancellationToken cancellationToken = default)
    {
        var bookmarkFilter = new BookmarkFilter();
        var bookmarks = await _bookmarkStore.FindManyAsync(bookmarkFilter, cancellationToken);

        var matchingBookmarks = bookmarks
            .Where(b =>
            {
                if (b.Payload is not CustomSignalBookmark bookmarkPayload)
                    return false;

                if (!string.Equals(bookmarkPayload.SignalName, signalName, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.IsNullOrEmpty(correlationValue) &&
                    !string.Equals(bookmarkPayload.CorrelationValue, correlationValue, StringComparison.Ordinal))
                    return false;

                return true;
            })
            .ToList();

        var result = new List<WaitingWorkflowInfo>();

        foreach (var bookmark in matchingBookmarks)
        {
            var instance = await _workflowInstanceStore.FindAsync(bookmark.WorkflowInstanceId, cancellationToken);
            if (instance != null)
            {
                result.Add(new WaitingWorkflowInfo
                {
                    WorkflowInstanceId = instance.Id,
                    DefinitionId = instance.DefinitionId,
                    CorrelationId = instance.CorrelationId,
                    BookmarkId = bookmark.Id,
                    CreatedAt = bookmark.CreatedAt,
                    SignalName = signalName,
                    SignalCorrelation = correlationValue
                });
            }
        }

        return result;
    }
}

/// <summary>
/// Information about a workflow waiting for a signal
/// </summary>
public class WaitingWorkflowInfo
{
    public string WorkflowInstanceId { get; set; } = default!;
    public string DefinitionId { get; set; } = default!;
    public string? CorrelationId { get; set; }
    public string BookmarkId { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
    public string SignalName { get; set; } = default!;
    public string? SignalCorrelation { get; set; }
}

/* 
 * USAGE EXAMPLE IN A WORKFLOW:
 * 
 * var workflow = new WorkflowBuilder()
 *     .Then<CustomSignalTrigger>(trigger => 
 *     {
 *         trigger.SignalName = new Input<string>("OrderApproved");
 *         trigger.CorrelationValue = new Input<string?>(context => context.GetVariable<string>("OrderId"));
 *     })
 *     .Then<WriteLineActivity>(log => log.Text = new Input<string>("Order was approved!"))
 *     .Build();
 * 
 * 
 * USAGE EXAMPLE FOR PUBLISHING A SIGNAL:
 * 
 * public class OrderService
 * {
 *     private readonly CustomSignalService _signalService;
 *     
 *     public async Task ApproveOrderAsync(string orderId)
 *     {
 *         // ... business logic ...
 *         
 *         // Resume any workflows waiting for this order approval
 *         await _signalService.PublishSignalAsync(
 *             signalName: "OrderApproved",
 *             correlationValue: orderId,
 *             payload: new { OrderId = orderId, ApprovedAt = DateTime.UtcNow }
 *         );
 *     }
 * }
 * 
 * 
 * NOTES FOR ADAPTING TO YOUR ELSA V3 VERSION:
 * - The Activity base class and attributes may have different properties
 * - Bookmark creation API (CreateBookmark) signature may vary
 * - Input/Output property declarations may use different syntax
 * - ActivityExecutionContext methods (Get, Set, CreateBookmark) may have different names
 * - IBookmarkProvider interface may have additional or different methods
 * - Payload serialization/deserialization may work differently
 * - Check your version's documentation for the exact API surface
 */
