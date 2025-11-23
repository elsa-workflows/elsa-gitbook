using Microsoft.AspNetCore.Mvc;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Management;
using Elsa.Workflows.Runtime.Entities;
using Elsa.Workflows.Runtime.Filters;

namespace YourApp.Controllers;

/// <summary>
/// Controller that demonstrates receiving webhook callbacks and resuming suspended workflow instances.
/// This example shows both the dispatcher method (recommended) and REST API approach.
/// </summary>
[ApiController]
[Route("api/callbacks")]
public class ResumeWebhookController : ControllerBase
{
    private readonly IWorkflowDispatcher _workflowDispatcher;
    private readonly IWorkflowInstanceStore _workflowInstanceStore;
    private readonly IBookmarkStore _bookmarkStore;
    private readonly ILogger<ResumeWebhookController> _logger;

    public ResumeWebhookController(
        IWorkflowDispatcher workflowDispatcher,
        IWorkflowInstanceStore workflowInstanceStore,
        IBookmarkStore bookmarkStore,
        ILogger<ResumeWebhookController> logger)
    {
        _workflowDispatcher = workflowDispatcher;
        _workflowInstanceStore = workflowInstanceStore;
        _bookmarkStore = bookmarkStore;
        _logger = logger;
    }

    /// <summary>
    /// Receives webhook callback and resumes the suspended workflow using correlation ID.
    /// This is the recommended approach for resuming workflows programmatically.
    /// </summary>
    /// <param name="payload">The webhook callback payload</param>
    /// <returns>Result indicating success or failure</returns>
    [HttpPost("resume")]
    public async Task<IActionResult> ResumeWorkflowByCorrelation([FromBody] CallbackPayload payload)
    {
        try
        {
            // Validate the incoming payload
            if (string.IsNullOrEmpty(payload?.CorrelationId))
            {
                _logger.LogWarning("Received callback with missing correlation ID");
                return BadRequest(new { error = "CorrelationId is required" });
            }

            _logger.LogInformation(
                "Received webhook callback for CorrelationId: {CorrelationId}",
                payload.CorrelationId);

            // Find suspended workflow instances with the given correlation ID
            // NOTE: The exact filter properties may vary depending on your Elsa v3 version
            var filter = new WorkflowInstanceFilter
            {
                CorrelationId = payload.CorrelationId,
                WorkflowStatus = WorkflowStatus.Running // Suspended workflows may be marked as Running
            };

            var instances = await _workflowInstanceStore.FindManyAsync(filter);
            var suspendedInstance = instances.FirstOrDefault(x => 
                x.SubStatus == WorkflowSubStatus.Suspended);

            if (suspendedInstance == null)
            {
                _logger.LogWarning(
                    "No suspended workflow found for CorrelationId: {CorrelationId}",
                    payload.CorrelationId);
                
                return NotFound(new
                {
                    error = "No suspended workflow found",
                    correlationId = payload.CorrelationId
                });
            }

            _logger.LogInformation(
                "Found suspended workflow instance: {InstanceId}",
                suspendedInstance.Id);

            // Prepare input data to pass to the resuming workflow
            // This data will be available to the workflow as input variables
            var input = new Dictionary<string, object>
            {
                ["CallbackData"] = payload,
                ["WebhookPayload"] = payload.Data ?? new { },
                ["ReceivedAt"] = DateTime.UtcNow
            };

            // Resume the workflow using the dispatcher
            // NOTE: Adjust DispatchWorkflowInstanceRequest properties based on your Elsa v3 version
            var dispatchRequest = new DispatchWorkflowInstanceRequest
            {
                InstanceId = suspendedInstance.Id,
                Input = input,
                // Optional: Specify bookmark ID if you need to resume at a specific bookmark
                // BookmarkId = "specific-bookmark-id"
            };

            await _workflowDispatcher.DispatchAsync(dispatchRequest);

            _logger.LogInformation(
                "Successfully dispatched resume request for workflow instance: {InstanceId}",
                suspendedInstance.Id);

            return Ok(new
            {
                status = "resumed",
                workflowInstanceId = suspendedInstance.Id,
                correlationId = payload.CorrelationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error resuming workflow for CorrelationId: {CorrelationId}",
                payload?.CorrelationId);
            
            return StatusCode(500, new
            {
                error = "Internal server error",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Alternative approach: Resume workflow by instance ID and optional bookmark ID.
    /// Useful when you have the exact instance and bookmark identifiers.
    /// </summary>
    /// <param name="instanceId">The workflow instance ID</param>
    /// <param name="request">Resume request with optional bookmark ID and input data</param>
    [HttpPost("resume/{instanceId}")]
    public async Task<IActionResult> ResumeWorkflowByInstanceId(
        string instanceId,
        [FromBody] ResumeByInstanceRequest request)
    {
        try
        {
            _logger.LogInformation(
                "Resuming workflow instance: {InstanceId} with bookmark: {BookmarkId}",
                instanceId,
                request.BookmarkId ?? "auto");

            // Verify the workflow instance exists and is suspended
            var instance = await _workflowInstanceStore.FindAsync(instanceId);
            
            if (instance == null)
            {
                return NotFound(new { error = "Workflow instance not found" });
            }

            if (instance.SubStatus != WorkflowSubStatus.Suspended)
            {
                return BadRequest(new
                {
                    error = "Workflow is not suspended",
                    status = instance.Status.ToString(),
                    subStatus = instance.SubStatus.ToString()
                });
            }

            // Optional: Verify the bookmark exists if specified
            if (!string.IsNullOrEmpty(request.BookmarkId))
            {
                var bookmark = await _bookmarkStore.FindAsync(request.BookmarkId);
                if (bookmark == null)
                {
                    return NotFound(new { error = "Bookmark not found" });
                }
            }

            // Dispatch the resume request
            // NOTE: Adjust based on your Elsa v3 API
            var dispatchRequest = new DispatchWorkflowInstanceRequest
            {
                InstanceId = instanceId,
                BookmarkId = request.BookmarkId,
                Input = request.Input ?? new Dictionary<string, object>()
            };

            await _workflowDispatcher.DispatchAsync(dispatchRequest);

            return Ok(new
            {
                status = "resumed",
                workflowInstanceId = instanceId,
                bookmarkId = request.BookmarkId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error resuming workflow instance: {InstanceId}",
                instanceId);
            
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Example: Query suspended workflows by various criteria.
    /// Useful for monitoring and debugging.
    /// </summary>
    [HttpGet("suspended")]
    public async Task<IActionResult> GetSuspendedWorkflows(
        [FromQuery] string? correlationId = null,
        [FromQuery] string? definitionId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var filter = new WorkflowInstanceFilter
            {
                WorkflowStatus = WorkflowStatus.Running,
                CorrelationId = correlationId,
                DefinitionId = definitionId
                // NOTE: Adjust filter properties based on your Elsa v3 version
            };

            var instances = await _workflowInstanceStore.FindManyAsync(filter);
            
            // Filter for suspended workflows
            var suspendedInstances = instances
                .Where(x => x.SubStatus == WorkflowSubStatus.Suspended)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    id = x.Id,
                    definitionId = x.DefinitionId,
                    version = x.Version,
                    correlationId = x.CorrelationId,
                    createdAt = x.CreatedAt,
                    updatedAt = x.UpdatedAt,
                    status = x.Status.ToString(),
                    subStatus = x.SubStatus.ToString(),
                    bookmarkCount = x.Bookmarks?.Count ?? 0
                })
                .ToList();

            return Ok(new
            {
                page,
                pageSize,
                total = suspendedInstances.Count,
                items = suspendedInstances
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying suspended workflows");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Example: Get details about bookmarks for a specific workflow instance.
    /// Useful for understanding what the workflow is waiting for.
    /// </summary>
    [HttpGet("instance/{instanceId}/bookmarks")]
    public async Task<IActionResult> GetInstanceBookmarks(string instanceId)
    {
        try
        {
            var instance = await _workflowInstanceStore.FindAsync(instanceId);
            
            if (instance == null)
            {
                return NotFound(new { error = "Workflow instance not found" });
            }

            // NOTE: Bookmark structure may vary by Elsa v3 version
            var bookmarks = instance.Bookmarks?.Select(b => new
            {
                id = b.Id,
                name = b.Name,
                activityTypeName = b.ActivityTypeName,
                activityInstanceId = b.ActivityInstanceId,
                createdAt = b.CreatedAt,
                payload = b.Payload // May contain correlation data
            }).ToList();

            return Ok(new
            {
                workflowInstanceId = instanceId,
                status = instance.Status.ToString(),
                subStatus = instance.SubStatus.ToString(),
                correlationId = instance.CorrelationId,
                bookmarks
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bookmarks for instance: {InstanceId}", instanceId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

/// <summary>
/// Webhook callback payload model
/// </summary>
public class CallbackPayload
{
    /// <summary>
    /// Correlation ID to identify the workflow instance
    /// </summary>
    public string CorrelationId { get; set; } = default!;

    /// <summary>
    /// Status from the external system
    /// </summary>
    public string Status { get; set; } = default!;

    /// <summary>
    /// Additional data from the webhook
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Optional: Order number or external reference
    /// </summary>
    public string? ExternalReference { get; set; }
}

/// <summary>
/// Request model for resuming by instance ID
/// </summary>
public class ResumeByInstanceRequest
{
    /// <summary>
    /// Optional: Specific bookmark to resume
    /// If not provided, the workflow will resume from the first available bookmark
    /// </summary>
    public string? BookmarkId { get; set; }

    /// <summary>
    /// Optional: Input data to pass to the resuming workflow
    /// </summary>
    public Dictionary<string, object>? Input { get; set; }
}

/* 
 * USAGE EXAMPLES:
 * 
 * 1. Resume by correlation ID:
 * curl -X POST https://your-app.com/api/callbacks/resume \
 *   -H "Content-Type: application/json" \
 *   -d '{"correlationId": "order-12345", "status": "completed", "data": {"total": 299.99}}'
 * 
 * 2. Resume by instance ID:
 * curl -X POST https://your-app.com/api/callbacks/resume/abc-instance-id-123 \
 *   -H "Content-Type: application/json" \
 *   -d '{"bookmarkId": "bookmark-xyz", "input": {"result": "success"}}'
 * 
 * 3. Query suspended workflows:
 * curl https://your-app.com/api/callbacks/suspended?correlationId=order-12345
 * 
 * 4. Get bookmarks for instance:
 * curl https://your-app.com/api/callbacks/instance/abc-instance-id-123/bookmarks
 * 
 * NOTES FOR ADAPTING TO YOUR ELSA V3 VERSION:
 * - Check the exact properties available on WorkflowInstanceFilter
 * - Verify DispatchWorkflowInstanceRequest properties (may vary by version)
 * - Confirm WorkflowStatus and WorkflowSubStatus enum values
 * - Review bookmark structure and payload access patterns
 * - Some versions may use different method names (e.g., FindManyAsync vs QueryAsync)
 */
