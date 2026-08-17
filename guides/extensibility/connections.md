---
description: >-
  Use Elsa 3.8.0 Connections to share named configuration between activities,
  expose connection descriptors, and choose a durable persistence boundary.
---

# Connections for activities

The `Elsa.Connections.*` extension lets an activity refer to a named
connection instead of carrying the connection settings in every workflow.
The workflow stores the connection name. At execution time, Elsa loads the
matching JSON configuration and injects it into the activity's typed
`ConnectionProperties<T>` input.

Use Connections when several activities share structured configuration for an
external system, especially when operators need to manage those settings
outside workflow definitions. Use an ordinary `Input<T>` for a value that is
specific to one workflow. Connections are not an encryption or secret-store
feature: the release stores the configuration as JSON and returns it from the
read API, so protect the API and database independently.

This page describes the `release/3.8.0` extension. It is a server-side
extension; Elsa Studio does not include a generic Connections administration
module in this release.

## Install and compose the modules

The release splits the feature into packages for the core contract and
middleware, models, persistence, API endpoints, and EF Core support. Add the
packages used by your host, then compose the three features:

- `Elsa.Connections.Core`
- `Elsa.Connections.Models`
- `Elsa.Connections.Persistence`
- `Elsa.Connections.Api`
- `Elsa.Connections.Persistence.EFCore` and, for SQLite,
  `Elsa.Connections.Persistence.EFCore.Sqlite`

```csharp
using Elsa.Agents; // UseEntityFrameworkCore is declared here in 3.8.0.
using Elsa.Extensions;
using Elsa.Persistence.EFCore.Extensions;

builder.Services.AddElsa(elsa => elsa
    .UseConnections(options => options.AddConnectionsFrom<Program>())
    .UseConnectionPersistence(persistence =>
        persistence.UseEntityFrameworkCore(ef => ef.UseSqlite()))
    .UseConnectionsApi());
```

`AddConnectionsFrom<TMarker>()` scans the marker assembly for **exported**
types carrying `ConnectionPropertyAttribute`. Make the configuration types
public and put the marker in the same assembly. The release workbench uses
`Program` as its marker and registers `UseConnections`,
`UseConnectionPersistence`, and `UseConnectionsApi` separately.

For a development-only host, omit `UseConnectionPersistence` and the default
process-local in-memory store is used. Do not treat that default as durable
production storage.

## Declare a connection-backed activity

A connection configuration type is identified with `ConnectionProperty`. Its
namespace and display name become the connection descriptor returned by the
descriptor API.

```csharp
using Elsa.Connections.Attributes;

[ConnectionProperty(
    "Acme.Payments",
    "Stripe",
    "Settings used to call the Stripe API.")]
public class StripeConnection
{
    public string BaseUrl { get; set; } = "https://api.stripe.com";
    public string ApiKey { get; set; } = string.Empty;
}
```

The activity uses `ConnectionProperties<T>` as its input. Mark the input with
`ConnectionType` so the connection dropdown can list only connections of that
type. Add `NoLog` to prevent the activity state filter from writing the
connection name as ordinary state; the filter writes a masked message instead.

```csharp
using Elsa.Connections.Attributes;
using Elsa.Connections.Models;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;

namespace Acme.Payments;

[ConnectionActivity("Acme.Payments.StripeConnection")]
public class ChargePayment : CodeActivity
{
    [Input]
    [ConnectionType(typeof(StripeConnection))]
    [NoLog]
    public ConnectionProperties<StripeConnection> Connection { get; set; } = new();

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var connection = context.Get(Connection);
        var settings = connection.Properties;

        // Call the external service with settings.BaseUrl and settings.ApiKey.
        await Task.CompletedTask;
    }
}
```

`StripeConnection` and `ChargePayment` are application types; the 3.8.0
release does not ship this concrete provider or activity.

The `ConnectionActivity` attribute is a marker for the middleware. In the
3.8.0 implementation its string value is not used to select a connection.
The middleware finds the first input whose property type is
`ConnectionProperties<>`, reads its `ConnectionName`, and populates its
`Properties` value. Avoid declaring multiple connection-property inputs on
one activity unless you have verified the runtime behavior and supplied your
own resolution logic.

The persisted workflow value contains `ConnectionName`; `Properties` is
ignored by JSON serialization. If the name is null or no matching connection
exists, the middleware logs the condition and continues the pipeline. The
activity should validate the resolved settings and fail deliberately when the
connection is required.

## How the runtime resolves a connection

The execution path is:

1. `UseConnections` installs the activity-state filter, the connection
   dropdown options provider, the connection registry, and middleware in the
   default activity-execution pipeline.
2. `ConnectionOptionsProvider` reads `ConnectionType` from the activity input
   and queries the connection store by the exact `Type.ToString()` value.
   It returns the saved connection names and a refresh hint for a UI client.
3. When the activity executes, `ConnectionMiddleware` reads the selected
   name and calls `IConnectionStore.FindAsync`.
4. The stored `ConnectionConfiguration` JSON is deserialized to `T`, then
   assigned to `ConnectionProperties<T>.Properties` before the activity's
   own execution continues.

The middleware does not call an external provider, validate credentials, or
refresh a token. Those responsibilities belong to the activity or to the
application service it invokes.

## API and client integration

`UseConnectionsApi()` adds these permission-protected endpoints. The permission
names below are the exact 3.8.0 values:

- `POST /connection-configuration` — create; `connections:write`.
- `GET /connection-configuration` — list; `connections:read`.
- `GET /connection-configuration/{id}` — read one; `connections:read`.
- `PUT /connection-configuration/{id}` — update; `connections:write`.
- `DELETE /connection-configuration/{id}` — delete; `connections:delete`.
- `GET /connection-configuration/descriptors` — list configuration types;
  `connections/descriptor:read`.
- `GET /connection-configuration/input-descriptor/{ActivityType}` — return
  the input descriptors for a registered connection type;
  `connections/descriptor:read`.

Although the route parameter is named `ActivityType`, the 3.8.0 registry
resolves it as the registered connection type name, such as
`Acme.Payments.StripeConnection`.

Create a connection with the type name returned by the descriptor list. For a
class named `Acme.Payments.StripeConnection`, `ConnectionType` is the value
returned by `typeof(StripeConnection).ToString()`:

```http
POST /connection-configuration
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "payments-primary",
  "description": "Primary payments account",
  "connectionType": "Acme.Payments.StripeConnection",
  "connectionConfiguration": {
    "baseUrl": "https://api.stripe.com",
    "apiKey": "use-a-secret-reference-or-protected-value"
  }
}
```

The API checks name uniqueness when creating and updating a connection. The
configuration object is otherwise application-defined; the runtime later
deserializes it to the registered CLR type.

### Elsa Studio boundary

The 3.8.0 Studio source contains no generic Connections module or client for
these routes. Registering `UseConnectionsApi()` therefore does not add a
Connections page to Studio. A custom host can build an administration screen
using the CRUD endpoints and can use the descriptor and input-descriptor
endpoints when implementing an activity editor. A generic activity editor
can use the `ConnectionType` metadata and the returned refresh hint for a
dropdown, but connection CRUD still belongs to the custom client.

## Persistence, tenancy, and operations

The default `ConnectionPersistenceFeature` selects `InMemoryConnectionStore`.
It is process-local, is lost on restart, and its implementation does not
apply tenant filtering. Use a durable store for shared or multi-tenant
deployments.

The release EF Core provider stores `ConnectionDefinition` in a dedicated
`ConnectionDbContext`. The entity has `Id`, `TenantId`, `Name`,
`Description`, `ConnectionType`, and the JSON `ConnectionConfiguration`.
The SQLite provider is available through `UseSqlite`; include the matching
EF Core provider package and run the provider's migrations as part of your
deployment process.

When Elsa multitenancy is enabled, the EF Core persistence base installs a
tenant-aware context, applies the current tenant ID to new entities, and adds
a query filter that selects the current tenant or the agnostic tenant (`*`).
The connection module itself does not add a tenant field to its request model,
so the host must configure Elsa tenancy and authentication correctly. Test
cross-tenant reads and writes with the same identity and tenant middleware
used in production.

Treat `ConnectionConfiguration` as sensitive application data. In the
3.8.0 implementation:

- the configuration is persisted as JSON rather than encrypted by this
  module;
- the read and list endpoints map the full JSON configuration back to the
  response model; and
- `NoLog` masks execution-state logging, but does not protect the database or
  API response.

Prefer a secret reference or an application-managed protected value where
possible. Restrict `connections:read` and `connections:write` separately,
protect the endpoints with the host's authentication and authorization
configuration, and avoid putting raw credentials in workflow JSON or source
control.

## Troubleshooting

**The connection type is not in the descriptor list.** Confirm that the type
is public, has `ConnectionPropertyAttribute`, and is in the assembly scanned
by `AddConnectionsFrom<TMarker>()`. The registry scans exported types only.

**The activity input is a text field instead of a connection dropdown.**
Confirm that the input is `ConnectionProperties<T>` and that the property has
`ConnectionType(typeof(T))`. The type must match the `ConnectionType` stored
by the API exactly.

**The activity sees default settings.** Confirm that the workflow persisted a
non-null `ConnectionName`, that the name exists, and that the connection type
was registered. A missing connection is logged and does not automatically
stop the activity.

**Connections disappear after restart.** The host is using the default
in-memory store. Configure `UseConnectionPersistence` with a durable provider
and apply its migrations.

**A multi-tenant test sees unexpected data.** Do not rely on the in-memory
store for tenant isolation. Use the EF Core provider or an `IConnectionStore`
implementation that applies the current tenant to every read, write, update,
and delete operation.

## Release source

This guide was checked against `release/3.8.0` in `elsa-extensions` at
[`335a2649`](https://github.com/elsa-workflows/elsa-extensions/tree/335a26495318f6ee1528bf2723b7333c753ce9a2),
and the Core release at [`5fd3a074`](https://github.com/elsa-workflows/elsa-core/tree/5fd3a074c950fd539a06ef5407a7ae9d879a3afc).
The key implementation points are:

- [`ConnectionsFeatures`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/connections/Elsa.Connections.Core/Features/ConnectionsFeatures.cs)
  and [`ConnectionMiddleware`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/connections/Elsa.Connections.Core/Middleware/ConnectionMiddleware.cs)
- [`ConnectionOptionsProvider`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/connections/Elsa.Connections.Core/ServiceProvider/ConnectionOptionsProvider.cs)
  and the [connection attributes](https://github.com/elsa-workflows/elsa-extensions/tree/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/connections/Elsa.Connections.Core/Attributes)
- [Connections API endpoints](https://github.com/elsa-workflows/elsa-extensions/tree/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/connections/Elsa.Connections.Api/Endpoints)
  and [persistence feature](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/connections/Elsa.Connections.Persistence/Features/ConnectionPersistenceFeature.cs)
- Core [tenant filter](https://github.com/elsa-workflows/elsa-core/blob/5fd3a074c950fd539a06ef5407a7ae9d879a3afc/src/modules/Elsa.Persistence.EFCore.Common/EntityHandlers/SetTenantIdFilter.cs)
  and [tenant-aware EF persistence](https://github.com/elsa-workflows/elsa-core/blob/5fd3a074c950fd539a06ef5407a7ae9d879a3afc/src/modules/Elsa.Persistence.EFCore.Common/PersistenceFeatureBase.cs)
