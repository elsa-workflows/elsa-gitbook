---
description: Use the Elsa Orchard Core extension for content management and content-item events in Elsa 3.8.0.
---

# Orchard Core content activities

The `Elsa.OrchardCore` extension lets a workflow call an Orchard Core tenant
over HTTP and wait for selected content-item events. It provides six fixed
task activities—content creation, patching, localization, tag resolution,
media upload, and GraphQL—as well as dynamically generated content-item event
triggers.

The extension runs in the Elsa Server process. Elsa Studio can display the
descriptors returned by that server, but Studio does not call Orchard Core,
store the client secret, or provide an Orchard Core administration UI.

## Install and configure the server

Install the package in the application that executes the workflow:

```bash
dotnet add package Elsa.OrchardCore --version 3.8.0
```

Enable the module and configure both option types. `BaseAddress` is the
Orchard Core tenant address used by the REST and GraphQL clients. The client
credentials are sent to the tenant's `/connect/token` endpoint using the
OAuth 2.0 client-credentials grant.

```csharp
using Elsa.Extensions;
using Elsa.OrchardCore.Client.Options;
using Elsa.OrchardCore.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddElsa(elsa =>
{
    elsa.UseOrchardCore();
});

builder.Services.Configure<OrchardCoreClientOptions>(options =>
{
    options.BaseAddress = new Uri(builder.Configuration["OrchardCore:BaseAddress"]!);
    options.ClientId = builder.Configuration["OrchardCore:ClientId"]!;
    options.ClientSecret = builder.Configuration["OrchardCore:ClientSecret"]!;
});

builder.Services.Configure<OrchardCoreOptions>(options =>
{
    options.ContentTypes.Add("BlogPost");
    options.ContentTypes.Add("Product");
});
```

`UseOrchardCore` registers the fixed activities, the dynamic content-event
provider, the webhook notification handler, and the authenticated REST and
GraphQL clients. It does not install or configure Orchard Core itself. Store
the client secret in deployment configuration or a secret store, not in a
workflow definition.

The client caches the access token until its reported expiry. If a REST or
GraphQL call returns `401 Unauthorized`, the released handler requests a new
token and retries that request once. It does not make an external API call
transactional or deduplicate a mutation.

## Choose a content activity

All fixed activities are in the **Orchard Core** category. Their results are
JSON objects unless noted otherwise.

| Activity | Inputs | Behavior |
| --- | --- | --- |
| **Create Content Item** | `Content Type`, `Properties`, `Publish` | Posts a new item to `api/content-items`; `Publish` controls whether it is published immediately. |
| **Patch Content Item** | `Content Item ID`, `Patch`, `Publish` | Sends a partial JSON update to `api/content-items/{id}` and optionally publishes it. |
| **Localize Content Item** | `Content Item ID`, `Culture Code` | Posts to `api/content-items/{id}/localize` to create a localized version. |
| **Resolve Tags** | `Tags` | Posts a collection of tag names to `api/tags/resolve`; missing tags are created by the Orchard Core API. |
| **Upload Media** | `Files`, optional `Folder Path` | Uploads `HttpFile` values as multipart form data to `api/media/upload`. Omitting the folder targets the media-library root. |
| **GraphQL Query** | `Query` | Posts the GraphQL document as `application/graphql` to `api/graphql` and converts the response to the result's target type, or a JSON object when no target type is supplied. |

The extension also exposes `IRestApiClient.GetContentItemAsync` for custom
server-side services, but 3.8.0 does not wrap that method in a separate
designer activity.

### Create and patch content

`Create Content Item` accepts an Orchard Core content type and an object that
is converted to a JSON object. The property shape must match the content
definition in the tenant. For example, a code-first workflow can pass a
content-type-specific object or JSON-compatible value:

```csharp
using System.Text.Json.Nodes;
using Elsa.OrchardCore.Activities;
using Elsa.Workflows.Models;

var create = new CreateContentItem
{
    ContentType = new Input<string>("BlogPost"),
    Properties = new Input<object>(new JsonObject
    {
        ["DisplayText"] = "A release note",
        ["AutoroutePart"] = new JsonObject { ["Path"] = "/release-note" }
    }),
    Publish = new Input<bool>(true)
};
```

The `Patch` input of `Patch Content Item` is a `JsonObject` representing the
patch expected by the Orchard Core endpoint; the extension does not validate
that document against a particular content definition before sending it.
Both activities return the JSON response from Orchard Core, so bind that
result to later expressions or activities when the new or updated item's ID
is needed.

### Localize and resolve tags

Use **Localize Content Item** with the existing content item's ID and a culture
code such as `fr-FR`. The result is the JSON response from Orchard Core; the
activity does not translate or copy field values itself.

Use **Resolve Tags** when a workflow needs Orchard Core tag content items. It
sends all supplied tag names together and relies on the tenant API to return
the corresponding items and create missing tags.

### Upload media

**Upload Media** expects a collection of `Elsa.Http.HttpFile` values, not file
paths or raw strings. An `HttpFile` wraps a stream and may carry a filename,
content type, and ETag:

```csharp
using Elsa.Http;
using Elsa.OrchardCore.Activities;
using Elsa.Workflows.Models;

var stream = File.OpenRead("./assets/cover.jpg");

var upload = new UploadMedia
{
    Files = new Input<ICollection<HttpFile>>(
        new[] { new HttpFile(stream, "cover.jpg", "image/jpeg") }),
    FolderPath = new Input<string?>("blog-assets")
};
```

The activity creates multipart form data and sends the files to Orchard Core.
Keep the stream alive until the workflow has consumed it and dispose it when
your host no longer needs it. The server worker, not the Studio browser, must
be able to read the stream.
For untrusted paths or uploads, validate the source, size, media type, and
destination before constructing the `HttpFile` values.

### Send a GraphQL query

**GraphQL Query** sends the query text exactly as an `application/graphql`
request. Orchard Core determines the available schema and authorization. The
activity can convert the response to a type selected by the result binding;
otherwise it uses `JsonObject`.

```graphql
query {
  blogPosts {
    displayText
  }
}
```

Treat query text as a server-side input. Do not assume that a field is
available merely because it exists in another Orchard Core deployment, and do
not log client credentials or sensitive response data while troubleshooting.

## React to Orchard Core content events

The module generates trigger descriptors from the configured content types.
For every value in `OrchardCoreOptions.ContentTypes`, it creates four event
activities:

- **Created** for `content-item.created`
- **Published** for `content-item.published`
- **Unpublished** for `content-item.unpublished`
- **Removed** for `content-item.removed`

For example, configuring `BlogPost` creates activities with stable type names
such as `OrchardCore.ContentItem.BlogPost.Published`. The trigger's internal
content-type and event inputs are hidden from Studio; choose the generated
activity whose name matches the content type and event.

The output is a `ContentItemEventPayload` with these fields:

| Field | Meaning |
| --- | --- |
| `ContentType` | Orchard Core content type, such as `BlogPost`. |
| `DisplayText` | The content item's display text. |
| `Author` | The author value supplied in the event payload. |
| `Owner` | The owner value supplied in the event payload. |
| `ContentItemId` | The content item's identifier. |

The trigger is fed by Elsa's generic Webhooks pipeline. `Elsa.OrchardCore`
registers a handler for the received webhook notification; it does not create
a separate Orchard Core webhook route or automatically configure an Orchard
Core webhook subscription. Before sending events, register a matching
`WebhooksCore.WebhookSource` in the Elsa host. Its event types must include the
four external names listed above. `UseOrchardCore()` does not call
`RegisterWebhookSource` for you.

The generic `/webhooks` endpoint only sends a notification when a registered
source matches the posted `eventType`; an unknown or unregistered event can
return `200 OK` without starting a workflow. Configure Orchard Core to send
the supported event types to that ingress, then use the generic [webhook
extensibility guide](../guides/external-application-interaction.md) for source
registration, request shape, and endpoint security.

The runtime maps an incoming event by its external event name, converts the
payload to the release's `ContentItemEventPayload`, and sends a stimulus for
the configured content type and event. A content type that is not in
`OrchardCoreOptions.ContentTypes` has no generated descriptor and cannot match
one of these triggers.

## Studio and operational boundaries

- Install and configure `Elsa.OrchardCore` in every Elsa Server process that
  executes these activities or receives webhook events. Installing it only in
  Studio does not make the activities executable.
- The 3.8.0 Studio source has no Orchard Core-specific client, credentials
  page, content browser, or activity module. Studio displays server-supplied
  descriptors; the server performs the HTTP calls.
- The extension relies on the Orchard Core tenant's REST, GraphQL, token, and
  webhook contracts. Confirm that the configured tenant exposes the endpoints
  and that the client has permission to use them.
- Persist workflow instances and bookmarks using the durable storage and
  runtime coordination configured for the Elsa host. The Orchard Core client
  does not provide a separate workflow state store.
- A retry after an ambiguous timeout can repeat a create, patch, tag, or media
  mutation. Design reconciliation around the returned content-item IDs and
  the idempotency behavior of the Orchard Core deployment.
- Event delivery is an external callback path. Add authentication, signature
  or shared-secret validation, replay protection, rate limiting, body limits,
  and tenant validation at the webhook ingress required by your deployment.

## Troubleshooting

1. If the activities are missing, confirm `Elsa.OrchardCore` is installed in
   the execution host and `.UseOrchardCore()` runs during startup.
2. If Studio cannot find them, verify that it is connected to that server and
   that the server exposes the registered activity descriptors.
3. If REST or GraphQL calls return `401`, check the base address, client ID,
   client secret, token endpoint, and the Orchard Core application's scopes.
   The released client refreshes and retries once after a 401; repeated
   failures are not silently recovered.
4. If a create or patch fails, compare the input JSON with the Orchard Core
   content definition and inspect the response status from the tenant API.
5. If media upload fails, verify that each item is an `HttpFile`, its stream is
   readable by the server worker, and the target folder is valid.
6. If an event trigger never resumes, verify the generic webhook route,
   external event name, configured content type, payload shape, and the
   workflow's persisted bookmark. The four supported external names are the
   `content-item.*` values listed above.
7. If a content-event activity is absent, add its content type to
   `OrchardCoreOptions.ContentTypes` and refresh the server's activity
   descriptors before editing the workflow.

## Release source

This page is validated against the `release/3.8.0` source: Extensions
`a44e2b09af1202ff4936f493756e114c357eff81`, Core
`dff7d9f987394c3c2ba8003e6f9c803e97194fbc`, and Studio
`b008a52cc02840928824018056ca8299518f04b9`.

- [`Elsa.OrchardCore` project and package description](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/Elsa.OrchardCore.csproj)
- [`UseOrchardCore` module extension](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/Extensions/ModuleExtensions.cs)
- [`OrchardCoreFeature` registration and Webhooks dependency](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/Features/OrchardCoreFeature.cs)
- [`OrchardCoreClientOptions` and `OrchardCoreOptions`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/Client/Options/OrchardCoreClientOptions.cs)
- [`OrchardCoreOptions` content-type configuration](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/Options/OrchardCoreOptions.cs)
- [`AddOrchardCoreClient` service registration](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/Client/Extensions/OrchardCoreClientServiceCollectionExtensions.cs)
- [`IRestApiClient` and Orchard REST paths](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/Client/Contracts/IRestApiClient.cs)
- [`DefaultRestApiClient` request methods](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/Client/Services/DefaultRestApiClient.cs)
- [`DefaultGraphQLClient` request and result conversion](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/Client/Services/DefaultGraphQLClient.cs)
- [`DefaultSecurityTokenClient` client-credentials request](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/Client/Services/DefaultSecurityTokenClient.cs)
- [`AuthenticatingDelegatingHandler` token caching boundary](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/Client/DelegatingHandlers/AuthenticatingDelegatingHandler.cs)
- [`OrchardContentItemsEventActivityProvider` dynamic descriptors](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/ActivityProviders/OrchardContentItemsEventActivityProvider.cs)
- [`WebhookEventTypes` and content-event payload](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/WebhookEventTypes.cs)
- [`InvokeOrchardWebhookEventActivities` notification handler](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/Handlers/InvokeOrchardWebhookEventActivities.cs)
- [`ContentItemEventPayload`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/WebhookPayloads/ContentItemEventPayload.cs)
- [`ContentItemEvent` trigger implementation](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/cms/Elsa.OrchardCore/Activities/ContentItemEvent.cs)
- [`HttpFile` input model](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/modules/Elsa.Http/Models/HttpFile.cs)
- [`WebhooksFeature` source registration](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/modules/Elsa.Http.Webhooks/Features/WebhooksFeature.cs)
- [`POST /webhooks` matching behavior](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/modules/Elsa.Http.Webhooks/Endpoints/Webhooks/Endpoint.cs)
- [`WebhookEventReceived` generic trigger](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/modules/Elsa.Http.Webhooks/Activities/WebhookEventReceived.cs)

See also the [Activity Reference](activity-reference.md), [Plugins & Modules
Guide](../guides/plugins-modules/README.md), [Webhook extensibility
guide](../guides/external-application-interaction.md), and [Secrets
Management](../guides/security/secrets-management.md).
