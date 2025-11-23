using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Contracts;
using Elsa.Workflows.Runtime.Filters;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MyApp.Services;

/// <summary>
/// Examples of safe workflow resumption patterns with retry logic, 
/// idempotency, and error handling.
/// </summary>

#region Basic Resume Pattern

/// <summary>
/// Basic callback handler that resumes workflows based on correlation ID
/// </summary>
public class CallbackHandler
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IBookmarkStore _bookmarkStore;
    private readonly ILogger<CallbackHandler> _logger;

    public CallbackHandler(
        IWorkflowRuntime workflowRuntime,
        IBookmarkStore bookmarkStore,
        ILogger<CallbackHandler> logger)
    {
        _workflowRuntime = workflowRuntime;
        _bookmarkStore = bookmarkStore;
        _logger = logger;
    }

    /// <summary>
    /// Handle callback and resume workflow
    /// </summary>
    public async Task HandleCallbackAsync(string correlationId, object data)
    {
        _logger.LogInformation("Processing callback for correlation {CorrelationId}", correlationId);

        // Find bookmarks matching the correlation ID
        var filter = new BookmarkFilter
        {
            CorrelationId = correlationId
        };

        var bookmarks = await _bookmarkStore.FindManyAsync(filter);

        if (!bookmarks.Any())
        {
            _logger.LogWarning("No bookmarks found for correlation {CorrelationId}", correlationId);
            return;
        }

        foreach (var bookmark in bookmarks)
        {
            // Resume workflow with input data
            var input = new Dictionary<string, object>
            {
                ["CallbackData"] = data
            };

            try
            {
                await _workflowRuntime.ResumeWorkflowAsync(
                    bookmark.WorkflowInstanceId,
                    bookmark.Id,
                    input);

                _logger.LogInformation(
                    "Resumed workflow {WorkflowInstanceId} for correlation {CorrelationId}",
                    bookmark.WorkflowInstanceId,
                    correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error resuming workflow {WorkflowInstanceId}",
                    bookmark.WorkflowInstanceId);
                throw;
            }
        }
    }
}

#endregion

#region Idempotent Pattern

/// <summary>
/// Idempotent callback handler that prevents duplicate processing
/// </summary>
public class IdempotentCallbackHandler
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IBookmarkStore _bookmarkStore;
    private readonly IDistributedCache _cache;
    private readonly ILogger<IdempotentCallbackHandler> _logger;

    public IdempotentCallbackHandler(
        IWorkflowRuntime workflowRuntime,
        IBookmarkStore bookmarkStore,
        IDistributedCache cache,
        ILogger<IdempotentCallbackHandler> logger)
    {
        _workflowRuntime = workflowRuntime;
        _bookmarkStore = bookmarkStore;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Handle callback with idempotency check
    /// </summary>
    public async Task<bool> HandleCallbackAsync(
        string eventId, 
        string correlationId, 
        object data)
    {
        // Check if already processed
        var cacheKey = $"callback:{eventId}";
        var processed = await _cache.GetStringAsync(cacheKey);

        if (processed != null)
        {
            _logger.LogInformation(
                "Callback {EventId} already processed, skipping",
                eventId);
            return false;
        }

        _logger.LogInformation(
            "Processing callback {EventId} for correlation {CorrelationId}",
            eventId,
            correlationId);

        // Find and resume workflows
        var filter = new BookmarkFilter
        {
            CorrelationId = correlationId
        };

        var bookmarks = await _bookmarkStore.FindManyAsync(filter);

        if (!bookmarks.Any())
        {
            _logger.LogWarning(
                "No bookmarks found for correlation {CorrelationId}",
                correlationId);

            // Still mark as processed to avoid repeated lookups
            await MarkAsProcessedAsync(cacheKey);
            return false;
        }

        foreach (var bookmark in bookmarks)
        {
            var input = new Dictionary<string, object>
            {
                ["CallbackData"] = data,
                ["EventId"] = eventId
            };

            try
            {
                await _workflowRuntime.ResumeWorkflowAsync(
                    bookmark.WorkflowInstanceId,
                    bookmark.Id,
                    input);

                _logger.LogInformation(
                    "Resumed workflow {WorkflowInstanceId}",
                    bookmark.WorkflowInstanceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error resuming workflow {WorkflowInstanceId}",
                    bookmark.WorkflowInstanceId);
                throw;
            }
        }

        // Mark as processed (with expiration)
        await MarkAsProcessedAsync(cacheKey);
        return true;
    }

    private async Task MarkAsProcessedAsync(string cacheKey)
    {
        var options = new DistributedCacheEntryOptions
        {
            // Keep for 24 hours to handle late duplicates
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        };

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(new { ProcessedAt = DateTimeOffset.UtcNow }),
            options);
    }
}

#endregion

#region Retry Pattern

/// <summary>
/// Callback handler with retry logic for transient failures
/// </summary>
public class RetryableCallbackHandler
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IBookmarkStore _bookmarkStore;
    private readonly ILogger<RetryableCallbackHandler> _logger;

    public RetryableCallbackHandler(
        IWorkflowRuntime workflowRuntime,
        IBookmarkStore bookmarkStore,
        ILogger<RetryableCallbackHandler> logger)
    {
        _workflowRuntime = workflowRuntime;
        _bookmarkStore = bookmarkStore;
        _logger = logger;
    }

    /// <summary>
    /// Handle callback with exponential backoff retry
    /// </summary>
    public async Task<bool> HandleCallbackWithRetryAsync(
        string correlationId,
        object data,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await ResumeWorkflowAsync(correlationId, data, cancellationToken);
                
                _logger.LogInformation(
                    "Successfully processed callback for {CorrelationId} on attempt {Attempt}",
                    correlationId,
                    attempt);
                
                return true;
            }
            catch (Exception ex) when (IsTransientError(ex))
            {
                _logger.LogWarning(ex,
                    "Attempt {Attempt} failed for correlation {CorrelationId}",
                    attempt,
                    correlationId);

                if (attempt == maxRetries)
                {
                    _logger.LogError(ex,
                        "All {MaxRetries} attempts failed for correlation {CorrelationId}",
                        maxRetries,
                        correlationId);
                    throw;
                }

                // Exponential backoff: 2^attempt seconds
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogInformation(
                    "Waiting {Delay} before retry {NextAttempt}",
                    delay,
                    attempt + 1);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                // Non-transient error, don't retry
                _logger.LogError(ex,
                    "Non-transient error for correlation {CorrelationId}",
                    correlationId);
                throw;
            }
        }

        return false;
    }

    private async Task ResumeWorkflowAsync(
        string correlationId,
        object data,
        CancellationToken cancellationToken)
    {
        var filter = new BookmarkFilter
        {
            CorrelationId = correlationId
        };

        var bookmarks = await _bookmarkStore.FindManyAsync(filter, cancellationToken);

        if (!bookmarks.Any())
        {
            throw new InvalidOperationException(
                $"No bookmarks found for correlation {correlationId}");
        }

        foreach (var bookmark in bookmarks)
        {
            var input = new Dictionary<string, object>
            {
                ["CallbackData"] = data
            };

            await _workflowRuntime.ResumeWorkflowAsync(
                bookmark.WorkflowInstanceId,
                bookmark.Id,
                input,
                cancellationToken);
        }
    }

    private static bool IsTransientError(Exception ex)
    {
        // Check for transient database errors, network errors, etc.
        return ex is TimeoutException
            || ex is TaskCanceledException
            || (ex.InnerException != null && IsTransientError(ex.InnerException))
            || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase);
    }
}

#endregion

#region Cancellation Pattern

/// <summary>
/// Callback handler with cancellation token support
/// </summary>
public class CancellableCallbackHandler
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IBookmarkStore _bookmarkStore;
    private readonly ILogger<CancellableCallbackHandler> _logger;

    public CancellableCallbackHandler(
        IWorkflowRuntime workflowRuntime,
        IBookmarkStore bookmarkStore,
        ILogger<CancellableCallbackHandler> logger)
    {
        _workflowRuntime = workflowRuntime;
        _bookmarkStore = bookmarkStore;
        _logger = logger;
    }

    /// <summary>
    /// Handle callback with cancellation support
    /// </summary>
    public async Task HandleWithCancellationAsync(
        string correlationId,
        object data,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Processing callback for {CorrelationId}",
                correlationId);

            var filter = new BookmarkFilter
            {
                CorrelationId = correlationId
            };

            var bookmarks = await _bookmarkStore.FindManyAsync(filter, cancellationToken);

            foreach (var bookmark in bookmarks)
            {
                // Check cancellation before each operation
                cancellationToken.ThrowIfCancellationRequested();

                var input = new Dictionary<string, object>
                {
                    ["CallbackData"] = data
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
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Callback processing cancelled for {CorrelationId}",
                correlationId);
            
            // Could mark workflow as cancelled or perform cleanup
            // For now, just log and rethrow
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing callback for {CorrelationId}",
                correlationId);
            throw;
        }
    }
}

#endregion

#region Batch Resume Pattern

/// <summary>
/// Handler for resuming multiple workflows efficiently
/// </summary>
public class BatchResumeHandler
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IBookmarkStore _bookmarkStore;
    private readonly ILogger<BatchResumeHandler> _logger;

    public BatchResumeHandler(
        IWorkflowRuntime workflowRuntime,
        IBookmarkStore bookmarkStore,
        ILogger<BatchResumeHandler> logger)
    {
        _workflowRuntime = workflowRuntime;
        _bookmarkStore = bookmarkStore;
        _logger = logger;
    }

    /// <summary>
    /// Resume multiple workflows in batch with throttling
    /// </summary>
    public async Task<BatchResumeResult> ResumeBatchAsync(
        List<string> correlationIds,
        object data,
        int maxConcurrency = 10,
        CancellationToken cancellationToken = default)
    {
        var result = new BatchResumeResult();

        _logger.LogInformation(
            "Starting batch resume for {Count} correlation IDs",
            correlationIds.Count);

        // Batch load all bookmarks
        var allBookmarks = await _bookmarkStore.FindManyAsync(
            new BookmarkFilter
            {
                // Note: This might need to be done in chunks for large batches
            },
            cancellationToken);

        // Filter bookmarks by correlation IDs
        var relevantBookmarks = allBookmarks
            .Where(b => correlationIds.Contains(b.CorrelationId))
            .ToList();

        _logger.LogInformation(
            "Found {Count} workflows to resume",
            relevantBookmarks.Count);

        // Group by workflow instance
        var grouped = relevantBookmarks.GroupBy(b => b.WorkflowInstanceId);

        // Use semaphore for throttling
        using var semaphore = new SemaphoreSlim(maxConcurrency);

        var tasks = grouped.Select(async group =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var workflowInstanceId = group.Key;
                var bookmark = group.First();

                var input = new Dictionary<string, object>
                {
                    ["CallbackData"] = data
                };

                await _workflowRuntime.ResumeWorkflowAsync(
                    workflowInstanceId,
                    bookmark.Id,
                    input,
                    cancellationToken);

                result.SuccessCount++;
                
                _logger.LogDebug(
                    "Resumed workflow {WorkflowInstanceId}",
                    workflowInstanceId);
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Errors.Add(new BatchResumeError
                {
                    WorkflowInstanceId = group.Key,
                    Error = ex.Message
                });

                _logger.LogError(ex,
                    "Error resuming workflow {WorkflowInstanceId}",
                    group.Key);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Batch resume completed: {Success} succeeded, {Failures} failed",
            result.SuccessCount,
            result.FailureCount);

        return result;
    }
}

/// <summary>
/// Result of batch resume operation
/// </summary>
public class BatchResumeResult
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<BatchResumeError> Errors { get; set; } = new();
}

/// <summary>
/// Error details for failed resume
/// </summary>
public class BatchResumeError
{
    public string WorkflowInstanceId { get; set; } = default!;
    public string Error { get; set; } = default!;
}

#endregion

#region Timeout Pattern

/// <summary>
/// Handler with timeout for resume operations
/// </summary>
public class TimeoutResumeHandler
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IBookmarkStore _bookmarkStore;
    private readonly ILogger<TimeoutResumeHandler> _logger;

    public TimeoutResumeHandler(
        IWorkflowRuntime workflowRuntime,
        IBookmarkStore bookmarkStore,
        ILogger<TimeoutResumeHandler> logger)
    {
        _workflowRuntime = workflowRuntime;
        _bookmarkStore = bookmarkStore;
        _logger = logger;
    }

    /// <summary>
    /// Handle callback with timeout
    /// </summary>
    public async Task<bool> HandleWithTimeoutAsync(
        string correlationId,
        object data,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            var filter = new BookmarkFilter
            {
                CorrelationId = correlationId
            };

            var bookmarks = await _bookmarkStore.FindManyAsync(filter, cts.Token);

            if (!bookmarks.Any())
            {
                _logger.LogWarning(
                    "No bookmarks found for correlation {CorrelationId}",
                    correlationId);
                return false;
            }

            foreach (var bookmark in bookmarks)
            {
                var input = new Dictionary<string, object>
                {
                    ["CallbackData"] = data
                };

                await _workflowRuntime.ResumeWorkflowAsync(
                    bookmark.WorkflowInstanceId,
                    bookmark.Id,
                    input,
                    cts.Token);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError(
                "Resume operation timed out after {Timeout} for correlation {CorrelationId}",
                timeout,
                correlationId);
            return false;
        }
    }
}

#endregion
