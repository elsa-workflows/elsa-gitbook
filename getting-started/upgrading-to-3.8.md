---
description: >-
  Upgrade an Elsa 3.7 application to the Elsa 3.8.0 stable Core, Studio, and
  Extensions packages.
---

# Upgrade to Elsa 3.8.0

Elsa 3.8.0 is the stable release of Core, Studio, and Extensions. Align the
Elsa package family first, then review the security and module-composition
changes below before starting the upgraded host.

## Release sources

The release and source references used by this guide are:

| Repository | Stable tag | Verified commit | Release notes |
| --- | --- | --- | --- |
| Core | `3.8.0` | [`8191ae3`](https://github.com/elsa-workflows/elsa-core/tree/8191ae30554ea001b38bb44902dd90dc98c7a106) | [Core 3.8.0 release](https://github.com/elsa-workflows/elsa-core/releases/tag/3.8.0) |
| Studio | `3.8.0` | [`8539524`](https://github.com/elsa-workflows/elsa-studio/tree/85395246140c6b192a2457992da8704b0cdec3d0) | [Studio 3.8.0 release](https://github.com/elsa-workflows/elsa-studio/releases/tag/3.8.0) |
| Extensions | `3.8.0` | [`66861ae`](https://github.com/elsa-workflows/elsa-extensions/tree/66861ae0828e2c15459e8d9981f5ce1080bd3269) | [Extensions 3.8.0 release](https://github.com/elsa-workflows/elsa-extensions/releases/tag/3.8.0) |

This page describes the stable tags above. Pages that describe an older
release or a preview remain version-specific and should be read with their
version label.

## Align the package family

Update every Elsa Core, Studio, and Extensions package in an application to
`3.8.0`. Keep `Elsa.Api.Client` at `3.8.0` when using Studio 3.8.0. Do not mix
3.8.0 packages with an RC or preview package from another build.

For a code-first host, pin the packages you use explicitly while upgrading:

```bash
dotnet add package Elsa --version 3.8.0
dotnet add package Elsa.Api.Client --version 3.8.0
```

Add the matching `--version 3.8.0` to each additional `Elsa.*` package in the
application. If a custom solution referenced the Webhooks projects by source
path, replace those project references with the published `Elsa.Http.Webhooks`
and `Elsa.Studio.Http.Webhooks` packages. The Webhooks modules moved out of the
Extensions source repository in 3.8.0; the release hosts and Orchard Core
integration consume the packages instead.

The .NET 10 package set also updates `FastEndpoints`,
`FastEndpoints.Security`, and `FastEndpoints.Swagger` to `8.2.0`. The .NET 8
and .NET 9 package sets remain on `7.1.1`. If an application targets .NET 10,
review custom endpoint wrappers and resume integrations: `ResumeRequest` is
obsolete and the runtime resume endpoint uses `FastEndpoints.EmptyRequest`.

## Start from the Elsa templates

For a new .NET 10 application, install the stable `Elsa.Templates` package
from NuGet.org:

```bash
dotnet new install Elsa.Templates@3.8.0
```

The [Elsa.Templates 3.8.0 GitHub release](https://github.com/elsa-workflows/elsa-templates/releases/tag/3.8.0)
lists the package release and its source changes. Generate the template that
matches your deployment shape:

```bash
# Server: static configuration-backed features.
dotnet new elsa-server -n MyElsaServer --feature-model static

# Server: CShells feature and persistence composition.
dotnet new elsa-server -n MyElsaServer --feature-model shell

# Studio: choose server, wasm, or hybrid hosting.
dotnet new elsa-studio -n MyElsaStudio --hosting server
dotnet new elsa-studio -n MyElsaStudio --hosting wasm
dotnet new elsa-studio -n MyElsaStudio --hosting hybrid

# Combined server and Studio application.
dotnet new elsa-combined -n MyElsaApp --feature-model static --studio-hosting server
```

The server and combined templates also accept `--persistence sqlite`,
`sqlserver`, `postgresql`, or `oracle`. Studio and combined templates accept
`--auth-provider elsa-identity`, `open-id-connect`, or the legacy
`elsa-login`; add `--with-labels` when the Labels module is needed. The
generated project files target `net10.0`.

For local Development only, the generated server and combined configurations
provide an `admin` user with the password `password`. The `static` feature
model reads its user and role from `Identity:Users` and `Identity:Roles`; the
`shell` model uses the CShells `DefaultAdminUser` feature. Replace the
development credentials and signing key before deployment, and keep all
deployment values outside source control.

## Configure identity before startup

The 3.8.0 reference server no longer supplies production-usable admin
credentials or API keys. Configure users, applications, and roles through
deployment-owned configuration or a secret manager, and provide the JWT
signing key before the process starts.

The code-first configuration path is `Identity:Tokens:SigningKey` (the
environment-variable form is `Identity__Tokens__SigningKey`). In 3.8.0 the
key must:

* be present;
* contain at least 32 printable ASCII characters;
* contain no leading or trailing whitespace; and
* be different from the known public sample values.

Known sample values are accepted only in the `Development` or `Demo`
environment. Generate a random production value and keep it outside source
control. The [Elsa Identity guide](../guides/authentication/elsa-identity.md)
shows the configuration-based user, application, and role providers.

For a new host that needs an initial administrator, the 3.8.0 Identity module
recommends the `DefaultAdminUser` bootstrap. Read the username and password
from deployment configuration and never put the real values in source control:

```csharp
var identitySection = builder.Configuration.GetRequiredSection("Identity");
var tokenSection = identitySection.GetRequiredSection("Tokens");
var adminUserName = builder.Configuration["Identity:Bootstrap:UserName"]
    ?? throw new InvalidOperationException("Identity:Bootstrap:UserName is required.");
var adminPassword = builder.Configuration["Identity:Bootstrap:Password"]
    ?? throw new InvalidOperationException("Identity:Bootstrap:Password is required.");

builder.Services.AddElsa(elsa => elsa
    .UseIdentity(identity =>
    {
        identity.TokenOptions += options => tokenSection.Bind(options);
        identity.UseDefaultAdmin(adminUserName, adminPassword, "admin", new List<string> { "*" });
    })
    .UseDefaultAuthentication());
```

The initializer is idempotent. This abbreviated registration uses in-memory
identity stores; add a durable identity provider before deployment, as shown
in the [server setup guide](../application-types/elsa-server.md). After the
first bootstrap, rotate the credentials according to your deployment policy. If the host intentionally uses
configuration-backed users, applications, and roles instead, keep those
`UseConfigurationBased*Provider` registrations and configure the values
through deployment-owned configuration.

`UseDefaultAuthentication()` no longer grants the security-root permission to
localhost requests by default. A deliberately local development host that
needs that bootstrap can opt in explicitly:

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseIdentity(identity =>
    {
        identity.TokenOptions += options =>
            builder.Configuration.GetSection("Identity:Tokens").Bind(options);
        identity.UseConfigurationBasedUserProvider(options =>
            builder.Configuration.GetSection("Identity").Bind(options));
        identity.UseConfigurationBasedApplicationProvider(options =>
            builder.Configuration.GetSection("Identity").Bind(options));
        identity.UseConfigurationBasedRoleProvider(options =>
            builder.Configuration.GetSection("Identity").Bind(options));
    })
    .UseDefaultAuthentication(auth =>
        auth.EnableLocalHostPermissionGrantForSecurityRoot()));
```

Use that opt-in only for an intentionally local development bootstrap. For a
deployed host, configure an authenticated administrator or another explicit
bootstrap path instead.

## Review C# and Python expressions

C# and Python workflow expressions are host-code execution in 3.8.0. They are
disabled by default and are not sandboxes. Enable them only for trusted
workflow authors, and assign the corresponding permission to those authors:

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseCSharp(options => options.AllowHostCodeExecution = true)
    .UsePython(python => python.PythonOptions += options =>
        options.AllowHostCodeExecution = true));
```

The required permissions are `exec:csharp-expressions` and
`exec:python-expressions`. Treat enabling either option as granting access to
the host process. If the application does not need one of these engines,
remove its package and registration during the upgrade.

## Update custom Studio hosts

For Elsa Identity and direct OpenID Connect hosts, register the shared
`Elsa.Studio.Authentication.UI` module at version `3.8.0` and call
`AddAuthenticationUI()`. `AddElsaIdentityUI()` registers the identity provider
and redirect; it does not register the shared `/login` page. Select the host's
authentication provider with `AddStudioAuthenticationMode(...)` as shown in
the [Studio setup guide](../application-types/elsa-studio.md). Legacy
`ElsaLogin` hosts use their own login module.

WebAssembly hosts that select a culture during startup need
`<BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>`
in their client project. A hosted WebAssembly application also needs
`UseBlazorFrameworkFiles()` before `UseStaticFiles()` to serve its boot
resources. Verify that the browser displays the sign-in form; a successful
build or an HTTP 200 for the host page does not prove that WebAssembly started.

## Wire optional modules explicitly

Diagnostics and platform features introduced in 3.8.0 are separate modules.
Existing hosts do not opt into them automatically. Install the relevant Core
package, call its `Use...` registration, and map its endpoint or SignalR hub
where the module requires one. Examples include:

* structured logs and console logs;
* OpenTelemetry diagnostics;
* Secrets;
* HTTP Webhooks;
* the Dashboard API; and
* Weaver.

For example, the reference server enables the Dashboard API explicitly and
only adds structured-log capture when its host setting is enabled:

```csharp
builder.Services.AddElsa(elsa => elsa
    .UseDashboardApi()
    .UseStructuredLogs()
    .UseStructuredLogsDashboard());

var app = builder.Build();
app.UseStructuredLogs();
```

Use the corresponding module guide for its package, route, permissions, and
storage requirements. Do not assume that installing a package alone exposes
its API or Studio experience.

## Validate the upgrade

Run these checks before promoting the upgraded host:

1. Restore with only the intended 3.8.0 Elsa package family and inspect the
   resolved dependency graph.
2. Start the host with a real signing key and verify that an anonymous request
   cannot use the security-root API.
3. Verify the configured administrator and application credentials, token
   refresh, Studio login, and logout.
4. If C# or Python is enabled, verify both the host option and the matching
   `exec:*` permission. Keep an author without that permission unable to run
   host-code expressions.
5. Exercise each optional module that the deployment expects, including its
   mapped routes or hub and its persistence migration where applicable.
6. For a custom Webhooks integration, verify package references and endpoint
   discovery after removing any source project reference to the former
   Extensions location.

The [Core 3.8.0 release notes](https://github.com/elsa-workflows/elsa-core/releases/tag/3.8.0)
and the [Studio 3.8.0 release notes](https://github.com/elsa-workflows/elsa-studio/releases/tag/3.8.0)
list the complete feature and dependency changes. Docker image and template
publication is tracked separately; this guide intentionally does not invent a
container or template version.
