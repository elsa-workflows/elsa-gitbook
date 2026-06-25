---
description: >-
  In this topic, we will create an ASP.NET Core application that acts as both an
  Elsa Server and an Elsa Studio.
---

# Elsa Server + Studio (WASM)

Instead of running Elsa Server and Elsa Studio as separate ASP.NET Core applications, you can also setup an ASP.NET Core application that hosts both the workflow server and the UI. The UI will still make HTTP calls to the backend as if they were hosted separately, but the difference is that they are now served from the same application and therefore deployable as a single unit.

For Elsa Studio, we will setup the Blazor parts using Blazor WebAssembly, which static files will be served from the ASP.NET Core host application.

## Create Solution <a href="#create-solution" id="create-solution"></a>

In this chapter, we will scaffold a new solution and two projects:

* The Host
* The Client

The host will host both Elsa Server and the Blazor WebAssembly application representing Elsa Studio.

Run the following commands to create a solution with two projects:

```bash
# Create a new solution
dotnet new sln -n ElsaServerAndStudio

# Create the host project
dotnet new web -n "ElsaServer"

# Add the host project to the solution
dotnet sln add ElsaServer/ElsaServer.csproj

# Create the client project
dotnet new blazorwasm -n "ElsaStudio"

# Add the client project to the solution
dotnet sln add ElsaStudio/ElsaStudio.csproj

# Navigate to the directory where the host project is located
cd ElsaServer

# Add a reference to the client project
dotnet add reference ../ElsaStudio/ElsaStudio.csproj
```

## Setup Host <a href="#setup-host" id="setup-host"></a>

In this chapter, we will setup the host, which will host both the Elsa Server engine as well as the webassembly files for serving the Elsa Studio client assets to the browser.

1.  **Add Packages**

    Add the following packages:

    ```bash
    dotnet add package Elsa
    dotnet add package Elsa.Persistence.EFCore
    dotnet add package Elsa.Persistence.EFCore.Sqlite
    dotnet add package Elsa.Http
    dotnet add package Elsa.Identity
    dotnet add package Elsa.Scheduling
    dotnet add package Elsa.Workflows.Api
    dotnet add package Elsa.Expressions.CSharp
    dotnet add package Elsa.Expressions.JavaScript
    dotnet add package Elsa.Expressions.Liquid
    dotnet add package Microsoft.AspNetCore.Components.WebAssembly.Server
    ```
2.  **Update Program.cs**

    Open the _Program.cs_ file in your project and replace its contents with the code provided below. This code does a lot of things like setting up database connections, enabling user authentication, and preparing the server to handle workflows.

    **Program.cs**

    ```csharp
    using Elsa.Persistence.EFCore.Extensions;
    using Elsa.Persistence.EFCore.Modules.Management;
    using Elsa.Persistence.EFCore.Modules.Runtime;
    using Elsa.Extensions;
    using Microsoft.AspNetCore.Mvc;

    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseStaticWebAssets();

    var services = builder.Services;
    var configuration = builder.Configuration;

    services
        .AddElsa(elsa => elsa
            .UseIdentity(identity =>
            {
                identity.TokenOptions = options => options.SigningKey = "large-signing-key-for-signing-JWT-tokens";
                identity.UseAdminUserProvider();
            })
            .UseDefaultAuthentication()
            .UseWorkflowManagement(management => management.UseEntityFrameworkCore(ef => ef.UseSqlite()))
            .UseWorkflowRuntime(runtime => runtime.UseEntityFrameworkCore(ef => ef.UseSqlite()))
            .UseScheduling()
            .UseJavaScript()
            .UseLiquid()
            .UseCSharp()
            .UseHttp(http => http.ConfigureHttpOptions = options => configuration.GetSection("Http").Bind(options))
            .UseWorkflowsApi()
            .AddActivitiesFrom<Program>()
            .AddWorkflowsFrom<Program>()
        );

    services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().WithExposedHeaders("*")));
    services.AddRazorPages(options => options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute()));

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.MapStaticAssets();
    app.UseRouting();
    app.UseCors();
    app.UseStaticFiles();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseWorkflowsApi();
    app.UseWorkflows();
    app.MapFallbackToPage("/_Host");
    app.Run();
    ```
3.  **Update appsettings.json**

    Add the following configuration section to `appsettings.json` or `appsettings.Development.json` with the following content:

    ```json
    {
        "Http": {
            "BaseUrl": "https://localhost:5001",
            "BasePath": "/api/workflows"
        }
    }
    ```
4.  **Create \_Host.cshtml**

    To conclude the setup, create new folder called `Pages` and add a new file called `_Host.cshtml` and copy in the code showcased below:

    **Pages/\_Host.cshtml**

    ```cshtml
    @page "/"
    @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
    @{
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var apiUrl = baseUrl + Url.Content("~/elsa/api");
        var basePath = "";
    }

    <!DOCTYPE html>
    <html>

    <head>
        <meta charset="utf-8"/>
        <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"/>
        <title>Elsa Studio 3.0</title>
        <base href="/"/>
        <link rel="apple-touch-icon" sizes="180x180" href="@basePath/_content/Elsa.Studio.Shell/apple-touch-icon.png">
        <link rel="icon" type="image/png" sizes="32x32" href="@basePath/_content/Elsa.Studio.Shell/favicon-32x32.png">
        <link rel="icon" type="image/png" sizes="16x16" href="@basePath/_content/Elsa.Studio.Shell/favicon-16x16.png">
        <link rel="manifest" href="@basePath/_content/Elsa.Studio.Shell/site.webmanifest">
        <link rel="preconnect" href="https://fonts.googleapis.com">
        <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
        <link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet"/>
        <link href="https://fonts.googleapis.com/css2?family=Ubuntu:wght@300;400;500;700&display=swap" rel="stylesheet">
        <link href="https://fonts.googleapis.com/css2?family=Montserrat:wght@400;500;600;700&display=swap" rel="stylesheet">
        <link href="https://fonts.googleapis.com/css2?family=Grandstander:wght@100&display=swap" rel="stylesheet">
        <link href="@basePath/_content/MudBlazor/MudBlazor.min.css" rel="stylesheet"/>
        <link href="@basePath/_content/CodeBeam.MudBlazor.Extensions/MudExtensions.min.css" rel="stylesheet"/>
        <link href="@basePath/_content/Radzen.Blazor/css/material-base.css" rel="stylesheet">
        <link href="@basePath/_content/Elsa.Studio.Shell/css/shell.css" rel="stylesheet">
        <link href="ElsaStudio.styles.css" rel="stylesheet">
    </head>

    <body>
    <div id="app">
        <div class="loading-splash mud-container mud-container-maxwidth-false">
            <h5 class="mud-typography mud-typography-h5 mud-primary-text my-6">Loading...</h5>
        </div>
    </div>

    <div id="blazor-error-ui">
        An unhandled error has occurred.
        <a href="" class="reload">Reload</a>
        <a class="dismiss">🗙</a>
    </div>
    <script src="@basePath/_content/BlazorMonaco/jsInterop.js"></script>
    <script src="@basePath/_content/BlazorMonaco/lib/monaco-editor/min/vs/loader.js"></script>
    <script src="@basePath/_content/BlazorMonaco/lib/monaco-editor/min/vs/editor/editor.main.js"></script>
    <script src="@basePath/_content/MudBlazor/MudBlazor.min.js"></script>
    <script src="@basePath/_content/CodeBeam.MudBlazor.Extensions/MudExtensions.min.js"></script>
    <script src="@basePath/_content/Radzen.Blazor/Radzen.Blazor.js"></script>
    <script>
        window.getClientConfig = function() { return {
            "apiUrl": "@apiUrl",
            "basePath": "@basePath"
         } };
    </script>
    <script src="_framework/blazor.webassembly.js"></script>
    </body>

    </html>
    ```

## Setup Client <a href="#setup-client" id="setup-client"></a>

Next, we will modify the client project.

1.  **Add Elsa Studio Packages**

    Navigate to the root directory of the client project and add the following Elsa Studio packages:

    ```bash
    cd ../ElsaStudio
    dotnet add package Elsa.Studio
    dotnet add package Elsa.Studio.Core.BlazorWasm
    dotnet add package Elsa.Studio.Authentication.ElsaIdentity.BlazorWasm
    dotnet add package Elsa.Studio.Authentication.ElsaIdentity.UI
    dotnet add package Elsa.Studio.Authentication.OpenIdConnect.BlazorWasm
    dotnet add package Elsa.Studio.Localization.BlazorWasm
    dotnet add package Elsa.Api.Client
    ```
2.  **Modify Program.cs**

    Open `Program.cs` and replace its existing content with the code provided below:

    Program.cs

    ```csharp
    using System.Text.Json;
    using Elsa.Studio.Authentication.ElsaIdentity.BlazorWasm.Extensions;
    using Elsa.Studio.Authentication.ElsaIdentity.HttpMessageHandlers;
    using Elsa.Studio.Authentication.ElsaIdentity.UI.Extensions;
    using Elsa.Studio.Authentication.OpenIdConnect.BlazorWasm.Extensions;
    using Elsa.Studio.Authentication.OpenIdConnect.HttpMessageHandlers;
    using Elsa.Studio.Contracts;
    using Elsa.Studio.Core.BlazorWasm.Extensions;
    using Elsa.Studio.Dashboard.Extensions;
    using Elsa.Studio.Extensions;
    using Elsa.Studio.Localization.BlazorWasm.Extensions;
    using Elsa.Studio.Localization.Models;
    using Elsa.Studio.Models;
    using Elsa.Studio.Options;
    using Elsa.Studio.Shell;
    using Elsa.Studio.Shell.Extensions;
    using Elsa.Studio.Workflows.Designer.Extensions;
    using Elsa.Studio.Workflows.Extensions;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
    using Microsoft.Extensions.Options;
    using Microsoft.JSInterop;

    // Build the host.
    var builder = WebAssemblyHostBuilder.CreateDefault(args);
    var configuration = builder.Configuration;

    // Register root components.
    builder.RootComponents.Add<App>("#app");
    builder.RootComponents.Add<HeadOutlet>("head::after");
    builder.RootComponents.RegisterCustomElsaStudioElements();

    // Choose authentication provider.
    // Supported values: "OpenIdConnect" or "ElsaIdentity".
    var authProvider = configuration["Authentication:Provider"];
    if (string.IsNullOrWhiteSpace(authProvider))
        authProvider = "ElsaIdentity";

    Type authenticationHandler;

    if (authProvider.Equals("ElsaIdentity", StringComparison.OrdinalIgnoreCase))
    {
        // Elsa Identity (username/password against Elsa backend) + login UI at /login.
        builder.Services.AddElsaIdentity();
        builder.Services.AddElsaIdentityUI();
        authenticationHandler = typeof(ElsaIdentityAuthenticatingApiHttpMessageHandler);
    }
    else if (authProvider.Equals("OpenIdConnect", StringComparison.OrdinalIgnoreCase))
    {
        // OpenID Connect.
        builder.Services.AddOpenIdConnectAuth(options =>
        {
            configuration.GetSection("Authentication:OpenIdConnect").Bind(options);
        });
        authenticationHandler = typeof(OidcAuthenticatingApiHttpMessageHandler);
    }
    else
    {
        throw new InvalidOperationException($"Unsupported Authentication:Provider value '{authProvider}'. Supported values are 'OpenIdConnect' and 'ElsaIdentity'.");
    }

    // Register shell services and modules.
    var localizationConfig = new LocalizationConfig
    {
        ConfigureLocalizationOptions = options => configuration.GetSection("Localization").Bind(options)
    };

    builder.Services.AddCore();
    builder.Services.AddShell();
    builder.Services.AddRemoteBackend(new()
    {
        ConfigureHttpClientBuilder = options => options.AuthenticationHandler = authenticationHandler
    });

    builder.Services.AddDashboardModule();
    builder.Services.AddWorkflowsModule();
    builder.Services.AddLocalizationModule(localizationConfig);

    // Build the application.
    var app = builder.Build();

    await app.UseElsaLocalization();

    // Apply client config.
    var js = app.Services.GetRequiredService<IJSRuntime>();
    var clientConfig = await js.InvokeAsync<JsonElement>("getClientConfig");
    var apiUrl = clientConfig.GetProperty("apiUrl").GetString() ?? throw new InvalidOperationException("No API URL configured.");
    app.Services.GetRequiredService<IOptions<BackendOptions>>().Value.Url = new(apiUrl);

    // Run each startup task.
    var startupTaskRunner = app.Services.GetRequiredService<IStartupTaskRunner>();
    await startupTaskRunner.RunStartupTasksAsync();

    // Run the application.
    await app.RunAsync();
    ```
3.  **Configure Client Authentication and Localization**

    The hosted page still supplies the backend API URL through `window.getClientConfig`, but the client can use `wwwroot/appsettings.json` to select the Studio authentication provider and localization settings:

    ```json
    {
        "Authentication": {
            "Provider": "OpenIdConnect",
            "OpenIdConnect": {
                "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
                "ClientId": "{client-id}",
                "AuthenticationScopes": [
                    "openid",
                    "profile",
                    "offline_access"
                ],
                "BackendApiScopes": [
                    "api://{backend-api-client-id}/elsa-server-api"
                ]
            }
        },
        "Localization": {
            "DefaultCulture": "en-US",
            "SupportedCultures": [
                "en-US"
            ]
        }
    }
    ```

    `AuthenticationScopes` are requested during sign-in. `BackendApiScopes` are requested when Studio obtains an access token for the Elsa Server API. Some identity providers require the backend API scope in the original sign-in grant as well; if token acquisition or refresh fails for the backend API scope, include the same scope in both `AuthenticationScopes` and `BackendApiScopes`.

    Because this client is Blazor WebAssembly, register `{studio-url}/authentication/login-callback` as the redirect URI and `{studio-url}/authentication/logout-callback` as the logout callback URI. Studio initiates logout at `{studio-url}/authentication/logout`.
4.  **Modify MainLayout.razor**

    Update `Layout/MainLayout.razor` with the following code listing:

    **MainLayout.razor**

    ```cshtml
    @inherits LayoutComponentBase

    <main>
        @Body
    </main>
    ```

## Launch the Application <a href="#run-application" id="run-application"></a>

To see your application in action, navigate back to the root directory containing the host project:

```bash
cd ../ElsaServer
```

Then execute the following command:

```bash
dotnet run --urls https://localhost:5001
```

Your application is now accessible at [https://localhost:5001](https://localhost:5001/).

By default, you can log in using:

```
username: admin
password: password
```

## Source Code <a href="#source-code" id="source-code"></a>

The source code for this chapter can be found [here](https://github.com/elsa-workflows/elsa-guides/tree/main/src/installation/elsa-server-and-studio)
