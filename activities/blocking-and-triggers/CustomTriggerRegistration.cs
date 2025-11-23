using Elsa.Extensions;
using Microsoft.Extensions.DependencyInjection;
using YourApp.Workflows.CustomActivities;

namespace YourApp.Workflows.Extensions;

/// <summary>
/// Extension methods for registering custom trigger activities and related services.
/// This demonstrates the recommended pattern for registering custom activities in Elsa v3.
/// </summary>
public static class CustomTriggerExtensions
{
    /// <summary>
    /// Registers custom trigger activities and supporting services with the DI container.
    /// Call this method in your Program.cs or Startup.cs during service configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCustomTriggers(this IServiceCollection services)
    {
        // Register the custom trigger activity
        // NOTE: AddActivity extension method may vary by Elsa v3 version
        services.AddActivity<CustomSignalTrigger>();

        // Register the bookmark provider for custom signal triggers
        // This enables Elsa to match incoming signals to waiting workflows
        // NOTE: Registration method may vary by version
        services.AddBookmarkProvider<CustomSignalBookmarkProvider>();

        // Register the custom signal service as a singleton
        // This is the API that your application code uses to publish signals
        services.AddSingleton<CustomSignalService>();

        return services;
    }

    /// <summary>
    /// Alternative registration method that provides more configuration options.
    /// Use this if you need to configure additional settings for your custom triggers.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCustomTriggersWithOptions(
        this IServiceCollection services,
        Action<CustomTriggerOptions>? configure = null)
    {
        // Register options
        var options = new CustomTriggerOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        // Register activities and services
        services.AddCustomTriggers();

        // Register additional services based on options
        if (options.EnableSignalMonitoring)
        {
            services.AddHostedService<CustomSignalMonitoringService>();
        }

        return services;
    }
}

/// <summary>
/// Configuration options for custom triggers.
/// </summary>
public class CustomTriggerOptions
{
    /// <summary>
    /// Whether to enable monitoring of pending signals.
    /// When enabled, a background service will periodically log statistics.
    /// </summary>
    public bool EnableSignalMonitoring { get; set; } = false;

    /// <summary>
    /// How often to check for pending signals (when monitoring is enabled).
    /// </summary>
    public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum age for signals before they're considered stale.
    /// Used for monitoring and alerting.
    /// </summary>
    public TimeSpan SignalTimeout { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>
/// Optional background service for monitoring custom signals.
/// Periodically logs information about pending signals and can alert on stale ones.
/// </summary>
public class CustomSignalMonitoringService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CustomTriggerOptions _options;
    private readonly ILogger<CustomSignalMonitoringService> _logger;

    public CustomSignalMonitoringService(
        IServiceProvider serviceProvider,
        CustomTriggerOptions options,
        ILogger<CustomSignalMonitoringService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Custom signal monitoring service started. Interval: {Interval}",
            _options.MonitoringInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorSignalsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring custom signals");
            }

            await Task.Delay(_options.MonitoringInterval, stoppingToken);
        }

        _logger.LogInformation("Custom signal monitoring service stopped");
    }

    private async Task MonitorSignalsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var signalService = scope.ServiceProvider.GetRequiredService<CustomSignalService>();

        // NOTE: You would need to implement a method to get all pending signals
        // This is a simplified example
        _logger.LogInformation("Checking for pending custom signals...");

        // Example: Check for stale signals
        var staleThreshold = DateTime.UtcNow - _options.SignalTimeout;
        
        // In a real implementation, you would query bookmarks and check their age
        // For now, this is a placeholder for monitoring logic
        _logger.LogDebug(
            "Monitoring complete. Stale threshold: {StaleThreshold}",
            staleThreshold);
    }
}

/* 
 * USAGE IN PROGRAM.CS (Minimal API style):
 * 
 * var builder = WebApplication.CreateBuilder(args);
 * 
 * // Add Elsa services
 * builder.Services.AddElsa(elsa => elsa
 *     .UseWorkflowManagement()
 *     .UseWorkflowRuntime()
 * );
 * 
 * // Add custom triggers
 * builder.Services.AddCustomTriggers();
 * 
 * // Or with options
 * builder.Services.AddCustomTriggersWithOptions(options =>
 * {
 *     options.EnableSignalMonitoring = true;
 *     options.MonitoringInterval = TimeSpan.FromMinutes(10);
 *     options.SignalTimeout = TimeSpan.FromHours(48);
 * });
 * 
 * var app = builder.Build();
 * 
 * // Configure Elsa middleware
 * app.UseWorkflows();
 * 
 * app.Run();
 * 
 * 
 * USAGE IN STARTUP.CS (Classic style):
 * 
 * public class Startup
 * {
 *     public void ConfigureServices(IServiceCollection services)
 *     {
 *         // Add Elsa
 *         services.AddElsa(elsa => elsa
 *             .UseWorkflowManagement()
 *             .UseWorkflowRuntime()
 *         );
 *         
 *         // Add custom triggers
 *         services.AddCustomTriggers();
 *         
 *         // Add MVC/API controllers
 *         services.AddControllers();
 *     }
 *     
 *     public void Configure(IApplicationBuilder app)
 *     {
 *         app.UseRouting();
 *         app.UseWorkflows();
 *         app.UseEndpoints(endpoints =>
 *         {
 *             endpoints.MapControllers();
 *         });
 *     }
 * }
 * 
 * 
 * DEPENDENCY INJECTION IN APPLICATION CODE:
 * 
 * public class OrderController : ControllerBase
 * {
 *     private readonly CustomSignalService _signalService;
 *     
 *     public OrderController(CustomSignalService signalService)
 *     {
 *         _signalService = signalService;
 *     }
 *     
 *     [HttpPost("orders/{id}/approve")]
 *     public async Task<IActionResult> ApproveOrder(string id)
 *     {
 *         // ... business logic ...
 *         
 *         // Publish signal to resume waiting workflows
 *         var resumedCount = await _signalService.PublishSignalAsync(
 *             signalName: "OrderApproved",
 *             correlationValue: id,
 *             payload: new { ApprovedBy = User.Identity?.Name }
 *         );
 *         
 *         return Ok(new { resumedWorkflows = resumedCount });
 *     }
 * }
 * 
 * 
 * NOTES FOR ADAPTING TO YOUR ELSA V3 VERSION:
 * - The AddActivity<T>() method may be in a different namespace
 * - Bookmark provider registration may use a different method name
 * - Some versions may require additional configuration in AddElsa()
 * - Check if IServiceCollection extensions are in Elsa.Extensions namespace
 * - Verify that custom activities are discoverable by the workflow designer
 * - Some versions may require explicit activity descriptor registration
 */
