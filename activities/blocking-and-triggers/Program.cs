using Elsa.EntityFrameworkCore.Extensions;
using Elsa.EntityFrameworkCore.Modules.Runtime;
using Elsa.Extensions;
using Elsa.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using MyApp.Activities;
using MyApp.Workflows;

namespace MyApp;

/// <summary>
/// Example Program.cs showing how to configure Elsa Workflows v3 with 
/// blocking activities, triggers, and proper service registration.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure Elsa Workflows
        ConfigureElsaServices(builder.Services, builder.Configuration);

        // Add other services
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthorization();
        
        // Enable Elsa HTTP workflow endpoints
        app.UseWorkflows();
        
        app.MapControllers();

        app.Run();
    }

    private static void ConfigureElsaServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddElsa(elsa =>
        {
            // Configure workflow management (for designing and managing workflows)
            elsa.UseWorkflowManagement(management =>
            {
                // Register workflows
                management.AddWorkflow<OrderApprovalWorkflow>();
                management.AddWorkflow<PaymentProcessingWorkflow>();
                management.AddWorkflow<ExternalSystemWorkflowExample>();
                
                // Add workflow from JSON
                management.AddWorkflowsFrom<Program>();
            });

            // Configure workflow runtime (for executing workflows)
            elsa.UseWorkflowRuntime(runtime =>
            {
                // Use Entity Framework Core for persistence
                runtime.UseEntityFrameworkCore(ef =>
                {
                    // Configure database provider
                    if (configuration.GetValue<bool>("UseInMemoryDatabase"))
                    {
                        // For development/testing
                        ef.UseInMemoryDatabase();
                    }
                    else
                    {
                        // For production - use SQL Server
                        ef.UseSqlServer(
                            configuration.GetConnectionString("Elsa"),
                            sql => sql
                                .MigrationsAssembly(typeof(Program).Assembly.FullName)
                                .CommandTimeout(60)
                                .EnableRetryOnFailure(3));
                                
                        // Alternative: PostgreSQL
                        // ef.UsePostgreSql(
                        //     configuration.GetConnectionString("Elsa"),
                        //     pg => pg
                        //         .MigrationsAssembly(typeof(Program).Assembly.FullName)
                        //         .CommandTimeout(60)
                        //         .EnableRetryOnFailure(3));
                    }
                });

                // Configure runtime options
                runtime.WorkflowDispatcherOptions = options =>
                {
                    options.MaxConcurrentWorkflows = 10;
                };
                
                // Enable distributed locking for multi-instance deployments
                if (!configuration.GetValue<bool>("UseInMemoryDatabase"))
                {
                    runtime.UseDistributedLockProvider();
                }
            });

            // Configure HTTP activities
            elsa.UseHttp(http =>
            {
                http.ConfigureHttpOptions = options =>
                {
                    options.BaseUrl = new Uri(configuration["Elsa:Http:BaseUrl"] 
                        ?? "https://localhost:5001");
                    options.BasePath = configuration["Elsa:Http:BasePath"] 
                        ?? "/workflows";
                };
            });

            // Configure scheduling for timer-based triggers
            elsa.UseScheduling(scheduling =>
            {
                // Set timezone for scheduled workflows
                scheduling.TimeZone = TimeZoneInfo.FindSystemTimeZoneById(
                    configuration["Elsa:Scheduling:TimeZone"] ?? "UTC");
                
                // Configure sweep interval (how often to check for scheduled work)
                scheduling.SweepInterval = TimeSpan.FromSeconds(
                    configuration.GetValue<int>("Elsa:Scheduling:SweepIntervalSeconds", 30));
                
                // Enable distributed locking for scheduling in multi-instance scenarios
                scheduling.UseDistributedLocking = !configuration.GetValue<bool>("UseInMemoryDatabase");
            });

            // Configure retention policies
            elsa.UseRetention(retention =>
            {
                retention.CompletedWorkflowRetention = TimeSpan.FromDays(
                    configuration.GetValue<int>("Elsa:Retention:CompletedWorkflowDays", 30));
                
                retention.SuspendedWorkflowRetention = TimeSpan.FromDays(
                    configuration.GetValue<int>("Elsa:Retention:SuspendedWorkflowDays", 90));
                
                retention.CancelledWorkflowRetention = TimeSpan.FromDays(
                    configuration.GetValue<int>("Elsa:Retention:CancelledWorkflowDays", 7));
                
                retention.SweepInterval = TimeSpan.FromHours(24);
            });

            // Register custom activities
            elsa.AddActivitiesFrom<ExternalSystemCallback>();
            
            // Add custom activity providers if needed
            // elsa.AddActivityProvider<MyCustomActivityProvider>();

            // Configure identity for multi-tenancy (optional)
            // elsa.UseIdentity(identity =>
            // {
            //     identity.UseEntityFrameworkCore();
            // });
            
            // Add telemetry (optional)
            // elsa.UseTelemetry();
        });

        // Register application services
        services.AddScoped<ExternalSystemCallbackService>();
        services.AddScoped<CallbackHandler>();
        services.AddScoped<IdempotentCallbackHandler>();
        
        // Add distributed caching for idempotency
        if (configuration.GetValue<bool>("UseRedis"))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
                options.InstanceName = "Elsa:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        // Add logging
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.AddDebug();
            
            // Set log levels
            logging.AddFilter("Elsa", LogLevel.Information);
            logging.AddFilter("Elsa.Workflows.Runtime", LogLevel.Debug);
        });
    }
}

/// <summary>
/// Example appsettings.json configuration
/// </summary>
public static class AppSettingsExample
{
    public const string Json = @"{
  ""ConnectionStrings"": {
    ""Elsa"": ""Server=localhost;Database=Elsa;User Id=elsa;Password=***;TrustServerCertificate=true;MultipleActiveResultSets=true"",
    ""Redis"": ""localhost:6379""
  },
  ""Elsa"": {
    ""Http"": {
      ""BaseUrl"": ""https://localhost:5001"",
      ""BasePath"": ""/workflows""
    },
    ""Scheduling"": {
      ""TimeZone"": ""UTC"",
      ""SweepIntervalSeconds"": 30
    },
    ""Retention"": {
      ""CompletedWorkflowDays"": 30,
      ""SuspendedWorkflowDays"": 90,
      ""CancelledWorkflowDays"": 7
    }
  },
  ""UseInMemoryDatabase"": false,
  ""UseRedis"": false,
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning"",
      ""Elsa"": ""Information"",
      ""Elsa.Workflows.Runtime"": ""Debug""
    }
  }
}";
}

/// <summary>
/// Example database connection string configurations for different providers
/// </summary>
public static class DatabaseConnectionExamples
{
    // SQL Server
    public const string SqlServer = 
        "Server=localhost;Database=Elsa;User Id=elsa;Password=***;" +
        "TrustServerCertificate=true;MultipleActiveResultSets=true;" +
        "Min Pool Size=10;Max Pool Size=100;Connection Timeout=30;";

    // PostgreSQL
    public const string PostgreSql = 
        "Host=localhost;Database=elsa;Username=elsa;Password=***;" +
        "Minimum Pool Size=10;Maximum Pool Size=100;Timeout=30;";

    // MySQL
    public const string MySql = 
        "Server=localhost;Database=elsa;User=elsa;Password=***;" +
        "Min Pool Size=10;Max Pool Size=100;Connection Timeout=30;";

    // SQLite (not recommended for production)
    public const string Sqlite = 
        "Data Source=elsa.db;Cache=Shared;";
}

/// <summary>
/// Example controller for manually triggering workflows
/// </summary>
[Microsoft.AspNetCore.Mvc.ApiController]
[Microsoft.AspNetCore.Mvc.Route("api/[controller]")]
public class WorkflowController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    private readonly Elsa.Workflows.Runtime.Contracts.IWorkflowRuntime _workflowRuntime;
    private readonly Microsoft.Extensions.Logging.ILogger<WorkflowController> _logger;

    public WorkflowController(
        Elsa.Workflows.Runtime.Contracts.IWorkflowRuntime workflowRuntime,
        Microsoft.Extensions.Logging.ILogger<WorkflowController> logger)
    {
        _workflowRuntime = workflowRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Manually start a workflow
    /// </summary>
    [Microsoft.AspNetCore.Mvc.HttpPost("start/{definitionId}")]
    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> StartWorkflow(
        string definitionId,
        [Microsoft.AspNetCore.Mvc.FromBody] Dictionary<string, object>? input = null)
    {
        try
        {
            var result = await _workflowRuntime.StartWorkflowAsync(
                definitionId,
                input ?? new Dictionary<string, object>());

            return Ok(new
            {
                workflowInstanceId = result.WorkflowInstanceId,
                status = result.Status.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting workflow {DefinitionId}", definitionId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Resume a workflow by correlation ID
    /// </summary>
    [Microsoft.AspNetCore.Mvc.HttpPost("resume/{correlationId}")]
    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> ResumeWorkflow(
        string correlationId,
        [Microsoft.AspNetCore.Mvc.FromBody] object? data = null)
    {
        try
        {
            var callbackService = HttpContext.RequestServices
                .GetRequiredService<ExternalSystemCallbackService>();
            
            var result = await callbackService.HandleCallbackAsync(
                "API",
                correlationId,
                data ?? new { });

            if (result)
            {
                return Ok(new { message = "Workflow resumed successfully" });
            }
            else
            {
                return NotFound(new { error = "No workflow found with that correlation ID" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming workflow {CorrelationId}", correlationId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
