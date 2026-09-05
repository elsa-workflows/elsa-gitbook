---
description: >-
  In this topic, we'll create an ASP.NET Core application that acts as a
  workflow server.
---

# Elsa Server

An Elsa Server is an ASP.NET Core web application that lets you manage workflows using a REST API and execute them. You can store your workflows in various places like databases, file systems, or even cloud storage.

{% hint style="warning" %}
This page targets Elsa 3.8.0. The 3.8.0 server requires a configured JWT
signing key and no longer supplies production-usable admin credentials or API
keys. Use deployment-owned configuration for identity values; do not copy
secrets into `Program.cs`.
{% endhint %}

## Setup﻿ <a href="#setup" id="setup"></a>

The following is a step-by-step guide to setting up a new ASP.NET Core Web Application that serves as an Elsa Server.

1.  **Create a new ASP.NET project**

    Open your command line tool and run these commands:

    ```bash
    dotnet new web -n "ElsaServer"
    ```
2.  **CD into the project's directory**

    Run the following command to go into the project's directory.

    ```bash
    cd ElsaServer
    ```
3.  **Add Packages**

    Add some commonly used Elsa packages.

    ```bash
    dotnet add package Elsa --version 3.8.0
    dotnet add package Elsa.Persistence.EFCore --version 3.8.0
    dotnet add package Elsa.Persistence.EFCore.Sqlite --version 3.8.0
    dotnet add package Elsa.Http --version 3.8.0
    dotnet add package Elsa.Identity --version 3.8.0
    dotnet add package Elsa.Scheduling --version 3.8.0
    dotnet add package Elsa.Workflows.Api --version 3.8.0
    dotnet add package Elsa.Expressions.CSharp --version 3.8.0
    dotnet add package Elsa.Expressions.JavaScript --version 3.8.0
    dotnet add package Elsa.Expressions.Liquid --version 3.8.0
    ```
4.  We need to add some code to make our server work. Open the `Program.cs` file in your project and replace its contents with the code provided below. This code does a lot of things like setting up database connections, enabling user authentication, and preparing the server to handle workflows.

    \
    **Program.cs**

    ```csharp
    using Elsa.Persistence.EFCore.Extensions;
    using Elsa.Persistence.EFCore.Modules.Identity;
    using Elsa.Persistence.EFCore.Modules.Management;
    using Elsa.Persistence.EFCore.Modules.Runtime;
    using Elsa.Extensions;

    var builder = WebApplication.CreateBuilder(args);
    var identityTokenSection = builder.Configuration.GetSection("Identity:Tokens");
    var adminUserName = builder.Configuration["Identity:Bootstrap:UserName"]
        ?? throw new InvalidOperationException("Identity:Bootstrap:UserName is required.");
    var adminPassword = builder.Configuration["Identity:Bootstrap:Password"]
        ?? throw new InvalidOperationException("Identity:Bootstrap:Password is required.");

    builder.Services.AddElsa(elsa =>
    {
        // Configure Management layer to use EF Core.
        elsa.UseWorkflowManagement(management => management.UseEntityFrameworkCore(ef => ef.UseSqlite()));

        // Configure Runtime layer to use EF Core.
        elsa.UseWorkflowRuntime(runtime => runtime.UseEntityFrameworkCore(ef => ef.UseSqlite()));

        // Default Identity features for authentication/authorization.
        elsa.UseIdentity(identity =>
        {
            identity.TokenOptions += options => identityTokenSection.Bind(options);
            identity.UseEntityFrameworkCore(ef => ef.UseSqlite());
            identity.UseDefaultAdmin(admin => admin
                .WithAdminUserName(adminUserName)
                .WithAdminPassword(adminPassword)
                .WithAdminRoleName("admin")
                .WithAdminRolePermissions(new List<string> { "*" }));
        });

        // Configure ASP.NET authentication/authorization.
        elsa.UseDefaultAuthentication();

        // Expose Elsa API endpoints.
        elsa.UseWorkflowsApi();

        // Setup a SignalR hub for real-time updates from the server.
        elsa.UseRealTimeWorkflows();

        // Enable JavaScript workflow expressions
        elsa.UseJavaScript(options => options.AllowClrAccess = true);

        // Enable HTTP activities.
        elsa.UseHttp(options => options.ConfigureHttpOptions = httpOptions => httpOptions.BaseUrl = new("https://localhost:5001"));

        // Use timer activities.
        elsa.UseScheduling();

        // Register custom activities from the application, if any.
        elsa.AddActivitiesFrom<Program>();

        // Register custom workflows from the application, if any.
        elsa.AddWorkflowsFrom<Program>();
    });

    // Configure CORS to allow designer app hosted on a different origin to invoke the APIs.
    builder.Services.AddCors(cors => cors
        .AddDefaultPolicy(policy => policy
            .AllowAnyOrigin() // For demo purposes only. Use a specific origin instead.
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("x-elsa-workflow-instance-id"))); // Required for Elsa Studio in order to support running workflows from the designer. Alternatively, you can use the `*` wildcard to expose all headers.

    // Add Health Checks.
    builder.Services.AddHealthChecks();

    // Build the web application.
    var app = builder.Build();

    // Configure web application's middleware pipeline.
    app.UseCors();
    app.UseRouting(); // Required for SignalR.
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapWorkflowsApi(); // Map Elsa API endpoints.
    app.UseWorkflows(); // Use Elsa middleware to handle HTTP requests mapped to HTTP Endpoint activities.
    app.UseWorkflowsSignalRHubs(); // Optional SignalR integration. Elsa Studio uses SignalR to receive real-time updates from the server. 

    app.Run();
    ```

The example stores identity data in the same SQLite database as the workflow
data and creates an administrator on first startup. Set
`Identity:Bootstrap:UserName` and `Identity:Bootstrap:Password` (or their
double-underscore environment-variable forms) through deployment-owned
configuration. Set `Identity:Tokens:SigningKey` (or
`Identity__Tokens__SigningKey`) to a random printable-ASCII value of at least
32 characters. The known sample signing keys are accepted only in
`Development` or `Demo`. For configuration-backed users and roles instead,
replace `UseDefaultAdmin` with the configuration-based providers described in
[Elsa Identity](../guides/authentication/elsa-identity.md).

For a local run, supply the bootstrap values through user secrets or environment
variables before `dotnet run`:

```bash
dotnet user-secrets init
dotnet user-secrets set "Identity:Tokens:SigningKey" "$(openssl rand -hex 32)"
dotnet user-secrets set "Identity:Bootstrap:UserName" "admin"
dotnet user-secrets set "Identity:Bootstrap:Password" "replace-with-a-local-bootstrap-password"
```

The C# and Python expression engines are host-code execution and are disabled
by default. If trusted authors need them, opt in explicitly and grant the
matching `exec:csharp-expressions` or `exec:python-expressions` permission;
neither engine is a sandbox. See [Upgrade to Elsa 3.8.0](../getting-started/upgrading-to-3.8.md)
for the complete migration checklist.

## Launch the Application﻿ <a href="#run-application" id="run-application"></a>

To see the application in action, execute the following command:

```bash
dotnet run --urls "https://localhost:5001"
```

## Source Code﻿ <a href="#source-code" id="source-code"></a>

The source code for this chapter can be found [here](https://github.com/elsa-workflows/elsa-guides/tree/main/src/installation/elsa-server)
