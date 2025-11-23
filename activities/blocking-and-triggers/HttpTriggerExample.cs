using Elsa.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YourApp.Workflows.Extensions;

namespace YourApp;

/// <summary>
/// Example Program.cs demonstrating how to configure an Elsa application
/// with HTTP triggers and custom trigger activities.
/// This uses the minimal API style introduced in .NET 6+.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure Elsa with HTTP activities and workflow management
        ConfigureElsaServices(builder.Services);

        // Add controllers for webhook callbacks
        builder.Services.AddControllers();

        // Add API documentation (optional but recommended)
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() 
            { 
                Title = "Elsa Workflows API", 
                Version = "v1",
                Description = "API for managing and interacting with Elsa workflows"
            });
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline
        ConfigureApp(app);

        app.Run();
    }

    /// <summary>
    /// Configures Elsa and related services.
    /// </summary>
    private static void ConfigureElsaServices(IServiceCollection services)
    {
        // Add Elsa with workflow management and runtime
        services.AddElsa(elsa =>
        {
            // Enable workflow management (for CRUD operations on workflow definitions)
            elsa.UseWorkflowManagement(management =>
            {
                // Optional: Configure workflow definition store
                // Default is in-memory, but you should use a persistent store in production
                management.UseEntityFrameworkCore(ef =>
                {
                    // Configure your database provider (PostgreSQL, SQL Server, SQLite, etc.)
                    ef.UsePostgreSql("Host=localhost;Database=elsa;Username=elsa;Password=elsa");
                    // Or SQL Server:
                    // ef.UseSqlServer("Server=localhost;Database=Elsa;Integrated Security=true");
                    // Or SQLite for development:
                    // ef.UseSqlite("Data Source=elsa.db");
                });
            });

            // Enable workflow runtime (for executing workflows)
            elsa.UseWorkflowRuntime(runtime =>
            {
                // Configure workflow instance store
                runtime.UseEntityFrameworkCore();

                // Enable distributed locking for clustered deployments (optional)
                // runtime.UseDistributedLocking();

                // Configure workflow dispatcher options
                // runtime.WorkflowDispatcherOptions = options =>
                // {
                //     options.DispatcherChannelOptions.MaxConcurrency = 10;
                // };
            });

            // Enable HTTP activities (HttpEndpoint, SendHttpRequest, WriteHttpResponse, etc.)
            elsa.UseHttp(http =>
            {
                // Configure base URL for generating absolute URLs
                http.ConfigureHttpOptions = options =>
                {
                    options.BaseUrl = new Uri("https://your-app.com");
                    // Or read from configuration:
                    // options.BaseUrl = new Uri(builder.Configuration["Elsa:Http:BaseUrl"]!);
                };
            });

            // Enable JavaScript expressions (for workflow logic)
            elsa.UseJavaScript(js =>
            {
                // Optional: Configure allowed types and assemblies
                js.AllowClrTypes = true;
            });

            // Enable C# expressions (for workflow logic)
            elsa.UseCSharp();

            // Enable Liquid expressions (for templating)
            elsa.UseLiquid();

            // Enable scheduling for timer-based workflows
            elsa.UseScheduling();

            // Optional: Enable identity features for multi-tenancy
            // elsa.UseIdentity(identity =>
            // {
            //     identity.UseEntityFrameworkCore();
            // });

            // Optional: Enable workflow designer API
            elsa.UseWorkflowsApi(api =>
            {
                // Configure CORS if needed for Studio
                // api.ConfigureCors = cors => cors
                //     .WithOrigins("https://studio.your-app.com")
                //     .AllowAnyMethod()
                //     .AllowAnyHeader();
            });
        });

        // Register custom triggers
        // NOTE: This assumes you've created the CustomTriggerExtensions from CustomTriggerRegistration.cs
        services.AddCustomTriggers();

        // Optional: Add hosted services for background processing
        // services.AddHostedService<WorkflowMaintenanceService>();
    }

    /// <summary>
    /// Configures the HTTP request pipeline.
    /// </summary>
    private static void ConfigureApp(WebApplication app)
    {
        // Enable Swagger in development
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Elsa Workflows API v1");
                c.RoutePrefix = "swagger";
            });
        }

        // Enable HTTPS redirection in production
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        // Enable authentication/authorization if configured
        // app.UseAuthentication();
        // app.UseAuthorization();

        // Enable routing
        app.UseRouting();

        // Enable Elsa workflow middleware
        // This handles HTTP workflow execution and endpoint mapping
        app.UseWorkflows();

        // Map API controllers (for webhook callbacks, etc.)
        app.MapControllers();

        // Optional: Map Elsa API endpoints
        app.UseWorkflowsApi();

        // Optional: Health check endpoint
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

        // Optional: Map a simple endpoint to trigger a workflow manually
        app.MapPost("/api/workflows/trigger/{definitionId}", async (
            string definitionId,
            IWorkflowDispatcher dispatcher,
            ILogger<Program> logger) =>
        {
            try
            {
                logger.LogInformation("Triggering workflow: {DefinitionId}", definitionId);

                var request = new DispatchWorkflowDefinitionRequest
                {
                    DefinitionId = definitionId
                };

                var result = await dispatcher.DispatchAsync(request);

                return Results.Ok(new
                {
                    workflowInstanceId = result.WorkflowInstanceId,
                    status = "started"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error triggering workflow: {DefinitionId}", definitionId);
                return Results.Problem("Failed to trigger workflow");
            }
        });
    }
}

/// <summary>
/// Alternative: Classic Startup.cs pattern for .NET Core 3.1 / .NET 5
/// Use this if your project doesn't use the minimal API style.
/// </summary>
public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Add Elsa services
        services.AddElsa(elsa =>
        {
            elsa.UseWorkflowManagement(management =>
            {
                management.UseEntityFrameworkCore(ef =>
                    ef.UsePostgreSql(_configuration.GetConnectionString("Elsa")));
            });

            elsa.UseWorkflowRuntime(runtime =>
            {
                runtime.UseEntityFrameworkCore();
            });

            elsa.UseHttp();
            elsa.UseJavaScript();
            elsa.UseCSharp();
            elsa.UseLiquid();
            elsa.UseScheduling();
            elsa.UseWorkflowsApi();
        });

        // Add custom triggers
        services.AddCustomTriggers();

        // Add MVC/API
        services.AddControllers();
        services.AddCors();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseHttpsRedirection();
        }

        app.UseRouting();
        app.UseCors(policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());

        app.UseWorkflows();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.UseWorkflowsApi();
        });
    }
}

/* 
 * APPSETTINGS.JSON EXAMPLE:
 * 
 * {
 *   "ConnectionStrings": {
 *     "Elsa": "Host=localhost;Database=elsa;Username=elsa;Password=elsa"
 *   },
 *   "Elsa": {
 *     "Http": {
 *       "BaseUrl": "https://your-app.com"
 *     },
 *     "Server": {
 *       "BaseUrl": "https://your-app.com"
 *     }
 *   },
 *   "Logging": {
 *     "LogLevel": {
 *       "Default": "Information",
 *       "Elsa": "Debug",
 *       "Microsoft.AspNetCore": "Warning"
 *     }
 *   }
 * }
 * 
 * 
 * RUNNING THE APPLICATION:
 * 
 * dotnet run
 * 
 * # Or with specific environment
 * dotnet run --environment Production
 * 
 * 
 * TESTING HTTP ENDPOINTS:
 * 
 * # Trigger a workflow by definition ID
 * curl -X POST https://localhost:5001/api/workflows/trigger/my-workflow-definition-id
 * 
 * # Call an HTTP endpoint activity in a workflow
 * curl -X POST https://localhost:5001/api/orders \
 *   -H "Content-Type: application/json" \
 *   -d '{"productId": "PROD-123", "quantity": 2}'
 * 
 * # Send a webhook callback to resume a workflow
 * curl -X POST https://localhost:5001/api/callbacks/resume \
 *   -H "Content-Type: application/json" \
 *   -d '{"correlationId": "order-12345", "status": "completed"}'
 * 
 * 
 * REQUIRED NUGET PACKAGES:
 * 
 * dotnet add package Elsa
 * dotnet add package Elsa.EntityFrameworkCore
 * dotnet add package Elsa.EntityFrameworkCore.PostgreSql
 * # Or for SQL Server: Elsa.EntityFrameworkCore.SqlServer
 * # Or for SQLite: Elsa.EntityFrameworkCore.Sqlite
 * dotnet add package Elsa.Http
 * dotnet add package Elsa.Workflows.Api
 * dotnet add package Elsa.Scheduling
 * dotnet add package Elsa.JavaScript
 * dotnet add package Elsa.CSharp
 * dotnet add package Elsa.Liquid
 * 
 * 
 * DOCKER COMPOSE EXAMPLE:
 * 
 * version: '3.8'
 * services:
 *   elsa-app:
 *     build: .
 *     ports:
 *       - "5001:80"
 *     environment:
 *       - ASPNETCORE_ENVIRONMENT=Production
 *       - ConnectionStrings__Elsa=Host=postgres;Database=elsa;Username=elsa;Password=elsa
 *       - Elsa__Http__BaseUrl=https://your-app.com
 *     depends_on:
 *       - postgres
 *   
 *   postgres:
 *     image: postgres:15
 *     environment:
 *       - POSTGRES_DB=elsa
 *       - POSTGRES_USER=elsa
 *       - POSTGRES_PASSWORD=elsa
 *     volumes:
 *       - postgres-data:/var/lib/postgresql/data
 * 
 * volumes:
 *   postgres-data:
 * 
 * 
 * NOTES FOR ADAPTING TO YOUR ELSA V3 VERSION:
 * - Method names in the fluent configuration API may vary slightly between versions
 * - Some options may be in different places or have different names
 * - Check the Elsa v3 documentation for your specific version
 * - The UseHttp() configuration options may have evolved
 * - Database provider setup may have different syntax in newer versions
 * - Some features like UseWorkflowsApi() may be separated into different packages
 */
