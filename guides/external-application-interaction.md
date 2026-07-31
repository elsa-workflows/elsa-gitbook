---
description: >-
  Configure Elsa 3.8.0 inbound webhook events and outbound webhook sinks,
  including custom activities, payloads, and endpoint security.
---

# Webhook extensibility

Elsa's Webhooks extension supports two different directions of communication:

- **Inbound events**: an external system posts a `WebhookEvent` to Elsa. A
  configured source maps the event to a trigger activity and resumes matching
  workflows.
- **Outbound events**: Elsa broadcasts events such as `Elsa.RunTask` to
  configured HTTP sinks. A separate application can receive the event and
  complete the work.

These paths share the `UseWebhooks` module, but they have different security
and design concerns. Choose the inbound path when the external system is the
source of a business event. Choose an outbound sink when Elsa is notifying
another application that work should be performed.

This guide describes the implementation in the `release/3.8.0` snapshot of
`elsa-extensions`.

## Install and enable the module

Add the Elsa HTTP Webhooks package that matches the rest of your Elsa 3.8.0
packages:

```bash
dotnet add package Elsa.Http.Webhooks --version 3.8.0
```

Enable it in the Elsa module configuration:

```csharp
using Elsa.Extensions;

builder.Services.AddElsa(elsa =>
{
    elsa.UseWebhooks();
});
```

`UseWebhooks` registers the WebhooksCore services, the dynamic activity
provider, and the notification handlers used by both directions.

## Inbound webhook events

An inbound source describes the event types that an external system may send.
Register sources through `WebhooksFeature.RegisterWebhookSource(s)` or the
equivalent `IServiceCollection.RegisterWebhookSource(s)` extension. A source
has a name and event types. An event type can include an `ActivityBinding` that
provides the activity type name, display name, description, and payload type.

The binding is what makes an event appear as a usable activity in Studio. An
event type without an activity binding can still be received and notified to
the Webhooks extension, but it does not produce a browsable trigger activity.

The source object is supplied by WebhooksCore, so keep its definition in the
integration package that owns the external event contract. The Elsa-side
registration looks like this:

```csharp
using Elsa.Extensions;
using WebhooksCore;

var orderEvents = CreateOrderWebhookSource();

builder.Services.AddElsa(elsa =>
{
    elsa.UseWebhooks(webhooks =>
    {
        webhooks.RegisterWebhookSource(orderEvents);
    });
});
```

Replace `CreateOrderWebhookSource` with the factory from your integration. Do
not invent event names in the workflow and assume they will match incoming
requests: the endpoint matches the posted `eventType` against the registered
source event types.

### What the runtime does

The released inbound path is:

1. `POST /webhooks` reads a JSON `WebhookEvent`.
2. Elsa finds the first registered source containing the posted `eventType`.
3. If no source matches, Elsa returns `200 OK` without starting a workflow.
4. For a match, Elsa sends a `WebhookEventReceived` notification.
5. The notification handler sends a stimulus whose activity type is derived
   from the source name and event type.
6. Matching trigger activities resume their workflows with the webhook event
   as workflow input.

The generated activity is based on `WebhookEventReceived`. The activity hides
its internal event-type inputs from Studio and exposes the configured payload
type as its output. At runtime it converts the received payload to that type
when a payload type was configured.

The event's activity type is normally derived as:

```text
Webhooks.{dehumanized source name}.{dehumanized event type}
```

When an `ActivityBinding.TypeName` is supplied, that explicit type name is used
by the descriptor provider. Keep the type name stable after workflows have
been saved; changing it makes existing workflow definitions unable to resolve
the trigger activity.

There is an important `release/3.8.0` compatibility detail: the inbound
stimulus handler derives the stimulus type from the source and event names,
while the descriptor provider uses `ActivityBinding.TypeName`. Keep an explicit
binding type name aligned with the derived
`Webhooks.{dehumanized source}.{dehumanized event}` value, or the designer may
show an activity that does not receive the matching stimulus.

An inbound request has the following shape:

```http
POST /webhooks
Content-Type: application/json

{
  "eventType": "Order.Approved",
  "payload": {
    "orderId": "order-123",
    "approvedBy": "alice@example.com"
  }
}
```

The exact payload schema belongs to the external integration. Elsa matches the
event type; it does not authenticate, validate a provider-specific signature,
or decide whether an unknown event should be a client error.

## Secure the inbound endpoint

In `release/3.8.0`, the Webhooks extension maps `POST /webhooks` with
`AllowAnonymous()`. This is deliberate for a generic webhook receiver, but it
means the extension does not protect the endpoint for you.

Put authentication and request validation in front of the endpoint or in a
host-level endpoint filter/middleware. At minimum, validate:

- the provider signature or shared secret;
- timestamp and replay limits;
- the request body and content type;
- the event type and payload schema; and
- tenant or source identity before a workflow is resumed.

Also consider rate limiting, maximum body size, idempotency keys, and a
dedicated ingress route. Do not expose the anonymous endpoint directly to the
public internet without an application-owned verification layer. Elsa's normal
API permissions do not turn this anonymous route into a signed webhook
receiver.

Because an unknown event returns `200 OK`, monitor rejected or unmatched
events in the validation layer rather than relying on the Elsa endpoint status
alone.

## Outbound webhook sinks

An outbound sink broadcasts an Elsa webhook event to another HTTP endpoint.
Register a simple endpoint in code:

```csharp
using Elsa.Extensions;

builder.Services.AddElsa(elsa =>
{
    elsa.UseWebhooks(webhooks =>
    {
        webhooks.RegisterWebhookSink(
            new Uri("https://onboarding.example.com/api/webhooks"));
    });
});
```

For a fully configured `WebhookSink`, use `RegisterSink` or bind
`WebhookSinksOptions` from configuration. Use the sink's event filters to
limit which event types are delivered. Keep the endpoint private or protect
it with the authentication and signature scheme expected by the receiving
application.

`RunTaskHandler` is the built-in outbound example. When a `RunTask` activity
executes, it broadcasts an `Elsa.RunTask` event whose payload includes:

- workflow instance ID;
- workflow definition ID and name;
- tenant ID and correlation ID;
- task ID and task name; and
- the task payload.

The receiving application should acknowledge quickly, authenticate the
request, persist the task idempotently, and perform long-running work outside
the request if necessary. The webhook is a notification path; it is not a
distributed transaction or a replacement for a durable task queue.

## Workflow design with inbound events

For an approval callback or external business event:

1. register a source event with a stable `eventType` and activity binding;
2. in Studio, add the generated webhook trigger activity;
3. use its typed payload output in the following activities;
4. publish the workflow; and
5. send a signed test request through the protected ingress route.

The trigger activity creates a bookmark when the workflow is not being started
by its trigger. When the matching stimulus arrives, Elsa resumes the waiting
workflow and sets the payload output. This makes webhook events suitable for
long-running workflows that wait for an external decision.

If a source has multiple event types, give each event an intentional activity
binding and payload type. Avoid using `object` unless the workflow really
needs untyped payloads; a concrete payload type gives Studio and the runtime a
more useful contract.

## Troubleshooting

### The generated activity is missing from Studio

- Confirm `UseWebhooks` is enabled in the Elsa Server, not only in Studio.
- Confirm the source is registered and the event type has an
  `ActivityBinding`.
- Refresh the server activity registry after changing source configuration.
- Check that the binding's type name is stable and that the server can resolve
  the payload type.

### The request returns `200 OK` but no workflow starts

- Check the exact case and spelling of `eventType`.
- Confirm the matching source is registered in the server process receiving the
  request.
- Confirm the workflow is published and uses the generated activity for the
  same source/event binding.
- Inspect the ingress validation layer and the workflow trigger/journal logs.

An unknown event also returns `200 OK` in the released endpoint, so do not use
that response alone as proof that a workflow was resumed.

### A payload is present but has the wrong type

Check the event's configured `PayloadType` and the JSON shape sent by the
external system. The activity converts the payload to that type when a type is
configured; conversion errors must be handled by the host's normal request and
workflow error policies.

### The outbound application does not receive `RunTask`

Confirm that:

- the sink is registered and its event filter includes `Elsa.RunTask`;
- the receiver URL is reachable from the Elsa Server process;
- the receiver accepts the payload and authentication scheme; and
- the receiving application is not treating retries as new tasks.

## Release source

This page was checked against `release/3.8.0` in `elsa-extensions` at
[`d407e962`](https://github.com/elsa-workflows/elsa-extensions/tree/d407e9621770a55427ac6c2315bd779da08d5fea):

- [`UseWebhooks` and `WebhooksFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/http/Elsa.Http.Webhooks/Extensions/ModuleExtensions.cs)
- [`WebhooksFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/http/Elsa.Http.Webhooks/Features/WebhooksFeature.cs)
- [`WebhookEventActivityProvider`](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/http/Elsa.Http.Webhooks/ActivityProviders/WebhookEventActivityProvider.cs)
- [`POST /webhooks`](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/http/Elsa.Http.Webhooks/Endpoints/Webhooks/Endpoint.cs)
- [`WebhookEventReceived` activity](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/http/Elsa.Http.Webhooks/Activities/WebhookEventReceived.cs)
- [`InvokeWebhookActivities`](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/http/Elsa.Http.Webhooks/Handlers/InvokeWebhookActivities.cs)
- [`RunTaskHandler`](https://github.com/elsa-workflows/elsa-extensions/blob/d407e9621770a55427ac6c2315bd779da08d5fea/src/modules/http/Elsa.Http.Webhooks/Handlers/RunTaskHandler.cs)
