# Logging Framework

The **Elsa.Logging** module provides a flexible and extensible way to capture, structure, and route log entries to various sinks. You can configure logging programmatically or via configuration files, and extend the framework with custom sinks for complete control.

## Programmatic Configuration

You can configure logging sinks directly in your application code. For example, in your `Program.cs`:

```csharp
// Example 1: Console target via built-in provider.
var consoleLogger = LoggerFactory.Create(lb =>
{
    lb.ClearProviders();
    lb.AddConsole();
    lb.AddFilter("Demo", LogLevel.Debug);
    lb.SetMinimumLevel(LogLevel.Information);
});

// Example 2: Pretty File target via Serilog (text template).
var filePrettyFactory = LoggerFactory.Create(lb =>
{
    var serilogConfig = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.File("App_Data/logs/activity-pretty-.log",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

    lb.ClearProviders();
    lb.AddFilter("Demo", LogLevel.Debug);
    lb.AddSerilog(serilogConfig, dispose: true);
});

// Example 3. JSON File target via Serilog (compact JSON).
var fileJsonFactory = LoggerFactory.Create(lb =>
{
    var serilogJson = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File(new CompactJsonFormatter(), "App_Data/logs/activity-json-.log",
            rollingInterval: RollingInterval.Day)
        .CreateLogger();

    lb.ClearProviders();
    lb.AddSerilog(serilogJson, dispose: true);
});

// Enable the Logging Framework.
elsa.UseLoggingFramework(logging =>
{
    // Add sinks manually using factories defined in the 3 examples above.
    logging.AddLogSink(new LoggerSink("Console (via code)", consoleLogger));
    logging.AddLogSink(new LoggerSink("File (pretty)", filePrettyFactory));
    logging.AddLogSink(new LoggerSink("File (JSON)", fileJsonFactory));
    
    // Alternative to defining sinks hardcoded as done above, we can get sinks from configuration.
    logging.UseConsole(); // Installs the Console Log Sink Factory.
    logging.UseSerilog(); // Installs the Serilog Log Sink Factory.
    
    // Bind the "LoggingFramework:Defaults" section. Default sinks are used by emitters that do not specify sinks explicitly.
    logging.ConfigureDefaults(options => configuration.GetSection("LoggingFramework").Bind(options));
});
```

This example demonstrates how to create custom sinks, register built-in sink factories, bind configuration from `appsettings.json`, and manually add sinks.

## Configuration via appsettings.json

You can also configure logging sinks declaratively in your `appsettings.json` file:

```json
{
  "LoggingFramework": {
    "Defaults": [
      "Console",
      "FilePretty",
      "FileJson"
    ],
    "Sinks": [
      {
        "Type": "Console",
        "Name": "Console",
        "Options": {
          "MinLevel": "Information",
          "CategoryFilters": {
            "Process": "Information",
            "Process.Nested": "Debug",
            "Process.Nested.Inner": "Information"
          },
          "Formatter": "Default",
          "TimestampFormat": "HH:mm:ss ",
          "DisableColors": true
        }
      },
      {
        "Type": "Serilog",
        "Name": "FilePretty",
        "Options": {
          "Path": "App_Data/logs/activity-pretty-.log",
          "RollingInterval": "Day",
          "Template": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
          "MinLevel": "Information"
        }
      },
      {
        "Type": "Serilog",
        "Name": "FileJson",
        "Options": {
          "Path": "App_Data/logs/activity-json-.log",
          "RollingInterval": "Day",
          "Formatter": "CompactJson",
          "MinLevel": "Debug"
        }
      }
    ]
  }
}
```

Each sink specifies the factory type, name, and options.

## Log Levels and Categories

Log sinks follow the same filtering semantics as the built-in ASP.NET Core logging system. Each sink defines a minimum log level and may specify category-specific overrides. When custom code emits a log entry through the logging framework, the log category is used to evaluate these filters.

For example, the following .NET logging configuration allows `Warning` and higher by default, but only `Information` and higher for `Microsoft.Hosting.Lifetime`:

```
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

You can achieve the same behavior with the `LoggingFramework` section when configuring sinks. A sink with the configuration shown earlier emits entries only if the log level for the specified category is enabled. This means a custom emitter using category `Process.Nested` will use the `Debug` level override from the example configuration, while category `Process.Nested.Inner` will drop entries below `Information`.

## Workflow Diagnostic Output

Elsa 3.7.0 does not include a built-in workflow activity named **Log** in the core activity set. Use the built-in **WriteLine** activity for simple workflow diagnostic output, and use Elsa's execution logging and log persistence features for activity execution history.

Example workflow diagnostic output:

```csharp
new WriteLine("Workflow started")
```

## Extending with Custom Sinks

For complete control over logging, implement your own `ILogSinkFactory`. A factory can construct sinks in code and from configuration (for example via `appsettings.json`), enabling reusable and configurable logging targets. Elsa provides examples such as `ConsoleLogSinkFactory` and `SerilogLogSinkFactory` that you can use as references.

To implement a custom sink:

1. Create a class that implements `ILogSinkFactory<TOptions>`.
2. Register your factory in the DI container.
3. Reference your sink type in configuration or code.

Example:

```csharp
public class MyCustomLogSinkFactory : ILogSinkFactory<MyCustomOptions>
{
    public string Type => "MyCustom";
    public ILogSink Create(string name, MyCustomOptions options)
    {
        // Create and return your custom sink.
    }
}

// Register in DI:
services.AddScoped<ILogSinkFactory, MyCustomLogSinkFactory>();
```

Once registered, the factory can be used from configuration:

```csharp
{
  "LoggingFramework": {
    "Sinks": [
      {
        "Type": "MyCustom",
        "Name": "MySink",
        "Options": {
          // Custom option values
        }
      }
    ]
  }
}
```

## References

* See `ConsoleLogSinkFactory` and `SerilogLogSinkFactory` for implementation examples.
* Configure sinks in code or via configuration for maximum flexibility.
* Use the `Log` activity in workflows to emit structured log entries.
