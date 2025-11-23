using Elsa.Extensions;
using Elsa.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace YourApp.Configuration;

/// <summary>
/// Examples for configuring timer-based workflows in Elsa.
/// Covers simple in-memory scheduling for single instances and
/// Quartz.NET integration for clustered, distributed deployments.
/// </summary>
public static class TimerTriggerConfiguration
{
    /// <summary>
    /// Basic timer configuration using in-memory scheduling.
    /// Suitable for single-instance deployments and development.
    /// </summary>
    public static IServiceCollection AddBasicTimerSupport(this IServiceCollection services)
    {
        services.AddElsa(elsa =>
        {
            // Enable workflow management
            elsa.UseWorkflowManagement();

            // Enable workflow runtime
            elsa.UseWorkflowRuntime();

            // Enable scheduling with default (in-memory) scheduler
            // This is sufficient for single-server deployments
            elsa.UseScheduling();

            // Enable timer activities (Delay, Timer, StartAt)
            // This is included by default but shown here for clarity
        });

        return services;
    }

    /// <summary>
    /// Advanced timer configuration using Quartz.NET for distributed scheduling.
    /// Recommended for production deployments with multiple servers (clustering).
    /// </summary>
    public static IServiceCollection AddQuartzTimerSupport(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddElsa(elsa =>
        {
            // Enable workflow management with persistence
            elsa.UseWorkflowManagement(management =>
            {
                management.UseEntityFrameworkCore(ef =>
                    ef.UsePostgreSql(connectionString));
            });

            // Enable workflow runtime with persistence
            elsa.UseWorkflowRuntime(runtime =>
            {
                runtime.UseEntityFrameworkCore();
            });

            // Enable Quartz.NET scheduling for distributed execution
            // NOTE: Exact configuration API may vary by Elsa v3 version
            elsa.UseQuartzScheduler(quartz =>
            {
                // Use persistent job store (required for clustering)
                quartz.UsePersistentStore(store =>
                {
                    // Configure database persistence
                    // PostgreSQL example:
                    store.UsePostgres(connectionString);

                    // Or SQL Server:
                    // store.UseSqlServer(connectionString);

                    // Configure serialization
                    store.UseJsonSerializer();

                    // Optional: Set table prefix
                    store.TablePrefix = "QRTZ_";
                });

                // Enable clustering for load balancing and high availability
                quartz.UseClustering(cluster =>
                {
                    // Set unique instance ID for this server
                    // Use machine name or a unique identifier
                    cluster.InstanceId = Environment.MachineName;

                    // Set cluster name (all nodes in the cluster must use the same name)
                    cluster.InstanceName = "ElsaWorkflowCluster";

                    // Checkin interval - how often to update this node's heartbeat
                    cluster.CheckinInterval = TimeSpan.FromSeconds(20);

                    // Misfire threshold - how late a job can be before it's considered misfired
                    cluster.CheckinMisfireThreshold = TimeSpan.FromSeconds(30);
                });

                // Configure thread pool
                quartz.UseDefaultThreadPool(pool =>
                {
                    // Maximum number of concurrent jobs
                    pool.MaxConcurrency = 10;
                });

                // Optional: Configure misfire handling
                quartz.UseMisfireThreshold(TimeSpan.FromMinutes(1));
            });
        });

        return services;
    }

    /// <summary>
    /// Configuration with timezone support and advanced scheduling options.
    /// </summary>
    public static IServiceCollection AddTimerWithTimezoneSupport(
        this IServiceCollection services,
        string connectionString,
        string defaultTimeZone = "UTC")
    {
        services.AddElsa(elsa =>
        {
            elsa.UseWorkflowManagement(management =>
            {
                management.UseEntityFrameworkCore(ef =>
                    ef.UsePostgreSql(connectionString));
            });

            elsa.UseWorkflowRuntime(runtime =>
            {
                runtime.UseEntityFrameworkCore();
            });

            elsa.UseQuartzScheduler(quartz =>
            {
                quartz.UsePersistentStore(store =>
                {
                    store.UsePostgres(connectionString);
                    store.UseJsonSerializer();
                });

                quartz.UseClustering(cluster =>
                {
                    cluster.InstanceId = Environment.MachineName;
                    cluster.InstanceName = "ElsaWorkflowCluster";
                    cluster.CheckinInterval = TimeSpan.FromSeconds(20);
                });

                // Configure default timezone for scheduled jobs
                quartz.UseTimeZone(defaultTimeZone);
            });

            // Configure scheduling options
            elsa.UseScheduling(scheduling =>
            {
                // Set default timezone for timer activities
                scheduling.DefaultTimeZone = TimeZoneInfo.FindSystemTimeZoneById(defaultTimeZone);

                // Configure sweep interval for checking scheduled workflows
                // scheduling.SweepInterval = TimeSpan.FromSeconds(30);
            });
        });

        return services;
    }
}

/// <summary>
/// Example service for managing scheduled workflows programmatically.
/// </summary>
public class ScheduledWorkflowService
{
    private readonly IWorkflowDefinitionStore _workflowStore;
    private readonly IScheduler _scheduler;
    private readonly ILogger<ScheduledWorkflowService> _logger;

    public ScheduledWorkflowService(
        IWorkflowDefinitionStore workflowStore,
        IScheduler scheduler,
        ILogger<ScheduledWorkflowService> logger)
    {
        _workflowStore = workflowStore;
        _scheduler = scheduler;
        _logger = logger;
    }

    /// <summary>
    /// Schedules a workflow to run at a specific time.
    /// </summary>
    public async Task ScheduleWorkflowAsync(
        string workflowDefinitionId,
        DateTime executeAt,
        TimeZoneInfo? timeZone = null,
        CancellationToken cancellationToken = default)
    {
        timeZone ??= TimeZoneInfo.Utc;

        _logger.LogInformation(
            "Scheduling workflow {WorkflowId} to run at {ExecuteAt} ({TimeZone})",
            workflowDefinitionId,
            executeAt,
            timeZone.Id);

        // Create a Quartz job for the workflow
        var jobKey = new JobKey($"workflow-{workflowDefinitionId}", "scheduled-workflows");
        var triggerKey = new TriggerKey($"trigger-{Guid.NewGuid()}", "scheduled-workflows");

        var job = JobBuilder.Create<WorkflowExecutionJob>()
            .WithIdentity(jobKey)
            .UsingJobData("WorkflowDefinitionId", workflowDefinitionId)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .StartAt(new DateTimeOffset(executeAt, timeZone.GetUtcOffset(executeAt)))
            .WithSchedule(SimpleScheduleBuilder.Create())
            .Build();

        await _scheduler.ScheduleJob(job, trigger, cancellationToken);

        _logger.LogInformation(
            "Workflow {WorkflowId} scheduled successfully. Job: {JobKey}, Trigger: {TriggerKey}",
            workflowDefinitionId,
            jobKey,
            triggerKey);
    }

    /// <summary>
    /// Schedules a recurring workflow using a cron expression.
    /// </summary>
    public async Task ScheduleRecurringWorkflowAsync(
        string workflowDefinitionId,
        string cronExpression,
        TimeZoneInfo? timeZone = null,
        CancellationToken cancellationToken = default)
    {
        timeZone ??= TimeZoneInfo.Utc;

        _logger.LogInformation(
            "Scheduling recurring workflow {WorkflowId} with cron: {Cron} ({TimeZone})",
            workflowDefinitionId,
            cronExpression,
            timeZone.Id);

        var jobKey = new JobKey($"workflow-{workflowDefinitionId}", "recurring-workflows");
        var triggerKey = new TriggerKey($"trigger-{workflowDefinitionId}", "recurring-workflows");

        var job = JobBuilder.Create<WorkflowExecutionJob>()
            .WithIdentity(jobKey)
            .UsingJobData("WorkflowDefinitionId", workflowDefinitionId)
            .StoreDurably()
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .WithCronSchedule(cronExpression, builder =>
            {
                builder.InTimeZone(timeZone);
                // Handle misfire: run immediately if missed
                builder.WithMisfireHandlingInstructionFireAndProceed();
            })
            .Build();

        await _scheduler.ScheduleJob(job, trigger, cancellationToken);

        _logger.LogInformation(
            "Recurring workflow {WorkflowId} scheduled successfully",
            workflowDefinitionId);
    }

    /// <summary>
    /// Cancels a scheduled workflow.
    /// </summary>
    public async Task CancelScheduledWorkflowAsync(
        string workflowDefinitionId,
        string group = "scheduled-workflows",
        CancellationToken cancellationToken = default)
    {
        var jobKey = new JobKey($"workflow-{workflowDefinitionId}", group);
        var deleted = await _scheduler.DeleteJob(jobKey, cancellationToken);

        if (deleted)
        {
            _logger.LogInformation(
                "Cancelled scheduled workflow {WorkflowId}",
                workflowDefinitionId);
        }
        else
        {
            _logger.LogWarning(
                "Scheduled workflow {WorkflowId} not found",
                workflowDefinitionId);
        }
    }

    /// <summary>
    /// Lists all scheduled workflow jobs.
    /// </summary>
    public async Task<List<ScheduledJobInfo>> GetScheduledWorkflowsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new List<ScheduledJobInfo>();
        var groupNames = await _scheduler.GetJobGroupNames(cancellationToken);

        foreach (var groupName in groupNames)
        {
            var jobKeys = await _scheduler.GetJobKeys(
                GroupMatcher<JobKey>.GroupEquals(groupName),
                cancellationToken);

            foreach (var jobKey in jobKeys)
            {
                var jobDetail = await _scheduler.GetJobDetail(jobKey, cancellationToken);
                if (jobDetail == null) continue;

                var triggers = await _scheduler.GetTriggersOfJob(jobKey, cancellationToken);

                foreach (var trigger in triggers)
                {
                    var nextFireTime = trigger.GetNextFireTimeUtc();
                    var previousFireTime = trigger.GetPreviousFireTimeUtc();

                    result.Add(new ScheduledJobInfo
                    {
                        JobKey = jobKey.ToString(),
                        TriggerKey = trigger.Key.ToString(),
                        WorkflowDefinitionId = jobDetail.JobDataMap.GetString("WorkflowDefinitionId"),
                        NextFireTime = nextFireTime?.DateTime,
                        PreviousFireTime = previousFireTime?.DateTime,
                        State = (await _scheduler.GetTriggerState(trigger.Key, cancellationToken)).ToString()
                    });
                }
            }
        }

        return result;
    }
}

/// <summary>
/// Information about a scheduled job.
/// </summary>
public class ScheduledJobInfo
{
    public string JobKey { get; set; } = default!;
    public string TriggerKey { get; set; } = default!;
    public string? WorkflowDefinitionId { get; set; }
    public DateTime? NextFireTime { get; set; }
    public DateTime? PreviousFireTime { get; set; }
    public string State { get; set; } = default!;
}

/// <summary>
/// Quartz job that executes a workflow.
/// NOTE: Implementation details may vary by Elsa v3 version.
/// </summary>
public class WorkflowExecutionJob : IJob
{
    private readonly IWorkflowDispatcher _dispatcher;
    private readonly ILogger<WorkflowExecutionJob> _logger;

    public WorkflowExecutionJob(
        IWorkflowDispatcher dispatcher,
        ILogger<WorkflowExecutionJob> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var workflowDefinitionId = context.JobDetail.JobDataMap.GetString("WorkflowDefinitionId");

        _logger.LogInformation(
            "Executing scheduled workflow: {WorkflowDefinitionId}",
            workflowDefinitionId);

        try
        {
            var request = new DispatchWorkflowDefinitionRequest
            {
                DefinitionId = workflowDefinitionId
            };

            var result = await _dispatcher.DispatchAsync(request, context.CancellationToken);

            _logger.LogInformation(
                "Workflow executed successfully. Instance ID: {InstanceId}",
                result.WorkflowInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error executing scheduled workflow: {WorkflowDefinitionId}",
                workflowDefinitionId);
            throw;
        }
    }
}

/* 
 * USAGE IN PROGRAM.CS:
 * 
 * // For development (single instance):
 * builder.Services.AddBasicTimerSupport();
 * 
 * // For production (clustered):
 * builder.Services.AddQuartzTimerSupport(
 *     builder.Configuration.GetConnectionString("Elsa")!
 * );
 * 
 * // With timezone support:
 * builder.Services.AddTimerWithTimezoneSupport(
 *     builder.Configuration.GetConnectionString("Elsa")!,
 *     defaultTimeZone: "America/New_York"
 * );
 * 
 * // Register the scheduling service
 * builder.Services.AddScoped<ScheduledWorkflowService>();
 * 
 * 
 * PROGRAMMATIC SCHEDULING EXAMPLE:
 * 
 * public class ReportController : ControllerBase
 * {
 *     private readonly ScheduledWorkflowService _schedulingService;
 *     
 *     [HttpPost("reports/schedule")]
 *     public async Task<IActionResult> ScheduleReport(ScheduleReportRequest request)
 *     {
 *         // Schedule a one-time workflow
 *         await _schedulingService.ScheduleWorkflowAsync(
 *             workflowDefinitionId: "generate-monthly-report",
 *             executeAt: new DateTime(2024, 12, 1, 9, 0, 0),
 *             timeZone: TimeZoneInfo.FindSystemTimeZoneById("America/New_York")
 *         );
 *         
 *         // Schedule a recurring workflow
 *         await _schedulingService.ScheduleRecurringWorkflowAsync(
 *             workflowDefinitionId: "daily-cleanup",
 *             cronExpression: "0 2 * * *", // 2 AM daily
 *             timeZone: TimeZoneInfo.Utc
 *         );
 *         
 *         return Ok();
 *     }
 *     
 *     [HttpGet("reports/scheduled")]
 *     public async Task<IActionResult> GetScheduledReports()
 *     {
 *         var scheduled = await _schedulingService.GetScheduledWorkflowsAsync();
 *         return Ok(scheduled);
 *     }
 * }
 * 
 * 
 * REQUIRED NUGET PACKAGES:
 * 
 * dotnet add package Elsa
 * dotnet add package Elsa.Scheduling
 * dotnet add package Elsa.Quartz
 * dotnet add package Quartz
 * dotnet add package Quartz.Serialization.Json
 * dotnet add package Quartz.Plugins
 * # For PostgreSQL:
 * dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
 * # For SQL Server:
 * dotnet add package Microsoft.EntityFrameworkCore.SqlServer
 * 
 * 
 * DATABASE SETUP FOR QUARTZ:
 * 
 * Quartz requires database tables for clustering. Run the appropriate SQL script:
 * 
 * PostgreSQL: https://github.com/quartznet/quartznet/blob/main/database/tables/tables_postgres.sql
 * SQL Server: https://github.com/quartznet/quartznet/blob/main/database/tables/tables_sqlServer.sql
 * MySQL: https://github.com/quartznet/quartznet/blob/main/database/tables/tables_mysql_innodb.sql
 * 
 * Or let Quartz create tables automatically (development only):
 * store.UsePostgres(connectionString, options => options.AutoCreateSchemaObjects = true);
 * 
 * 
 * MONITORING SCHEDULED JOBS:
 * 
 * -- PostgreSQL: Check scheduled jobs
 * SELECT * FROM qrtz_triggers WHERE trigger_state = 'WAITING';
 * 
 * -- Check trigger details
 * SELECT t.trigger_name, t.trigger_group, t.next_fire_time, t.trigger_state, jd.job_data
 * FROM qrtz_triggers t
 * JOIN qrtz_job_details jd ON t.job_name = jd.job_name AND t.job_group = jd.job_group
 * WHERE t.trigger_state = 'WAITING'
 * ORDER BY t.next_fire_time;
 * 
 * -- Check cluster nodes
 * SELECT * FROM qrtz_scheduler_state;
 * 
 * 
 * COMMON CRON EXPRESSIONS:
 * 
 * "0 0 * * * ?"     - Every hour
 * "0 0/30 * * * ?"  - Every 30 minutes
 * "0 0 9 * * ?"     - Every day at 9:00 AM
 * "0 0 9 * * MON-FRI" - Weekdays at 9:00 AM
 * "0 0 0 1 * ?"     - First day of every month at midnight
 * "0 0 0 L * ?"     - Last day of every month at midnight
 * "0 0 9 ? * 2#1"   - First Monday of every month at 9:00 AM
 * 
 * 
 * NOTES FOR ADAPTING TO YOUR ELSA V3 VERSION:
 * - Quartz integration API may vary between Elsa versions
 * - Some versions may use UseQuartz() instead of UseQuartzScheduler()
 * - Persistence configuration may be in a different location
 * - Check if IScheduler is provided by Elsa or needs to be injected from Quartz directly
 * - Workflow execution job implementation may need adjustment based on dispatcher API
 * - Bookmark and trigger matching for timer activities may work differently
 */
