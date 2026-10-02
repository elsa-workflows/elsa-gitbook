---
description: >-
  Configure Elsa Studio to discover and switch between multiple Elsa Server
  environments while preserving the host's authentication and feature catalog.
---

# Studio environments

Use the optional Studio Environments module when one Studio host needs to work
with more than one Elsa Server backend, such as development, staging, and
production. The module adds an environment picker to the app bar and changes
the backend URL used by Studio's API clients when the user selects another
environment.

This is a Studio routing feature. It does not provision servers, create
environments, copy workflows, or store environment definitions for you. Your
configured backend must expose the `/environments` endpoint and return the
catalog that Studio should display.

## When to use it

Use environments when:

- one Studio deployment is intentionally shared across multiple Elsa Server
  backends;
- the server-side deployment or platform already owns the environment catalog;
  and
- users need to switch the backend used by workflow, dashboard, settings, or
  other remote Studio modules.

Use separate Studio deployments instead when environments need different
authentication policies, browser origins, or independent release lifecycles.
The module reuses the host's configured authentication pipeline; it is not an
isolation boundary between trust domains.

## Register the module

Reference the `Elsa.Studio.Environments` package from the same Elsa Studio
release as the rest of the Studio package family. Register the normal remote
backend first, then add the environments module with the same
`BackendApiConfig`:

```csharp
var backendApiConfig = new BackendApiConfig
{
    ConfigureBackendOptions = options =>
        configuration.GetSection("Backend").Bind(options),
    ConfigureHttpClientBuilder = options =>
        options.AuthenticationHandler = authenticationHandler
};

builder.Services.AddRemoteBackend(backendApiConfig);
builder.Services.AddEnvironmentsModule(backendApiConfig);
```

The 3.9.0 Server and WebAssembly host examples expose this as an optional
registration. The module replaces the default remote-backend accessor, but it
continues to use Studio's normal backend API client provider and the
authentication handler supplied through `BackendApiConfig`.

If you omit `AddEnvironmentsModule`, Studio uses the single backend configured
by `Backend`. If you register the module without a working `/environments`
endpoint, Studio remains usable against the configured fallback backend but the
environment list remains empty.

## Implement the `/environments` contract

Studio calls `GET /environments` against the initially configured backend. The
response has this shape:

```json
{
  "environments": [
    {
      "name": "Development",
      "url": "https://elsa-dev.example/",
      "customProperties": {
        "region": "eu-west"
      }
    },
    {
      "name": "Production",
      "url": "https://elsa.example/"
    }
  ],
  "defaultEnvironmentName": "Development"
}
```

Each entry supplies:

| Property | Meaning |
| --- | --- |
| `name` | The label shown in the Studio picker and the value used for selection. |
| `url` | The API base URL that Studio uses after selecting the environment. |
| `customProperties` | Optional metadata carried by the contract. The built-in picker does not render or interpret it. |
| `defaultEnvironmentName` | Optional name selected after the catalog loads. It must match an environment name exactly. |

The endpoint is a host/platform responsibility. Core Elsa Server does not
automatically create this catalog merely because the Studio package is
installed. Protect the endpoint with the same authentication and authorization
requirements appropriate for the users who can switch to each backend.

## How selection changes requests

The runtime sequence is:

1. Studio starts with the `Backend` URL configured by the host.
2. During feature initialization, the Environments module requests
   `/environments` from that backend.
3. Studio stores the returned catalog in its scoped environment service and
   selects `defaultEnvironmentName`. If that value is omitted, it selects the
   first returned environment when no current environment exists.
4. The selected environment URL becomes the remote backend URL used when Studio
   creates API clients.
5. Studio then loads the remote feature catalog through that selected backend.
6. The picker in the app bar lets the user select another named environment.

The feature-catalog ordering matters. Features are initialized after the
default environment has been selected, so a feature-gated Studio module is
evaluated against the selected environment's catalog rather than the fallback
backend's catalog.

The host's API client provider is reused. Switching environments changes the
URL read by that provider; it does not create a second private dependency
injection container or silently remove the configured authentication handler.

## Lifecycle and failure behavior

Environment state is scoped to the Studio host scope: a Blazor Server circuit
or the running WebAssembly application. The module does not persist the
selected environment in browser storage or in Elsa workflow storage, so a new
scope starts by loading the catalog again.

The catalog is loaded once per scope after a successful response. A failed
attempt is not cached, so a later feature-initialization attempt can retry. The
module logs and leaves the picker empty when the request times out, cannot
reach the backend, or receives `404 Not Found`, `401 Unauthorized`, or `403
Forbidden`. These failures do not crash the Studio circuit. Caller-requested
cancellation is allowed to propagate and is also not treated as a successful
load.

Check these points when the picker is empty:

1. The module package and `AddEnvironmentsModule(backendApiConfig)` are present
   in the actual host being run.
2. The initial `Backend:Url` points to the backend that owns the catalog.
3. `GET /environments` is reachable at that API base URL.
4. The response property names and URL values match the contract above.
5. The authentication handler attached to `BackendApiConfig` can authorize both
   the initial catalog request and requests to the selected environment URLs.
6. `defaultEnvironmentName` exactly matches one of the returned `name` values.

An environment URL is not validated by Studio beyond using it as the base for
remote API clients. The target must therefore expose the Elsa APIs and
permissions required by the Studio modules you enable.

## Boundaries for teams and operators

- The module switches where Studio sends API calls; it does not synchronize
  workflow definitions, instances, secrets, users, or databases between
  environments.
- The catalog is not a tenant list. If your backends are tenant-aware, tenant
  selection and authorization remain server concerns.
- The picker is not an environment administration screen. Add, remove, and
  update catalog entries through the platform or host endpoint that owns them.
- A successful catalog request proves only that Studio can read the catalog. It
  does not prove that every selected backend is healthy or exposes every module
  enabled in the Studio host.

## Release source

This guide describes the Elsa Studio `release/3.9.0` implementation:

- [`AddEnvironmentsModule`](https://github.com/elsa-workflows/elsa-studio/blob/bd443662bb5f8a3e07ca1c489f39a37527526200/src/modules/Elsa.Studio.Environments/Extensions/ServiceCollectionExtensions.cs)
- [`IEnvironmentsClient` and response contract](https://github.com/elsa-workflows/elsa-studio/blob/bd443662bb5f8a3e07ca1c489f39a37527526200/src/modules/Elsa.Studio.Environments/Contracts/IEnvironmentsClient.cs)
- [`ServerEnvironment`](https://github.com/elsa-workflows/elsa-studio/blob/bd443662bb5f8a3e07ca1c489f39a37527526200/src/modules/Elsa.Studio.Environments/Models/ServerEnvironment.cs)
- [`EnvironmentLoader`](https://github.com/elsa-workflows/elsa-studio/blob/bd443662bb5f8a3e07ca1c489f39a37527526200/src/modules/Elsa.Studio.Environments/Services/EnvironmentLoader.cs)
- [`EnvironmentRemoteBackendAccessor`](https://github.com/elsa-workflows/elsa-studio/blob/bd443662bb5f8a3e07ca1c489f39a37527526200/src/modules/Elsa.Studio.Environments/Services/EnvironmentRemoteBackendAccessor.cs)
- [`EnvironmentAwareFeatureService`](https://github.com/elsa-workflows/elsa-studio/blob/bd443662bb5f8a3e07ca1c489f39a37527526200/src/modules/Elsa.Studio.Environments/Services/EnvironmentAwareFeatureService.cs)
- [`EnvironmentPicker`](https://github.com/elsa-workflows/elsa-studio/blob/bd443662bb5f8a3e07ca1c489f39a37527526200/src/modules/Elsa.Studio.Environments/Components/EnvironmentPicker.razor)
- [Server host registration point](https://github.com/elsa-workflows/elsa-studio/blob/bd443662bb5f8a3e07ca1c489f39a37527526200/src/hosts/Elsa.Studio.Host.Server/Program.cs)
- [WebAssembly host registration point](https://github.com/elsa-workflows/elsa-studio/blob/bd443662bb5f8a3e07ca1c489f39a37527526200/src/hosts/Elsa.Studio.Host.Wasm/Program.cs)
