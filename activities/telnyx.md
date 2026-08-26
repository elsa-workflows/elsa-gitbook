---
description: Use the Elsa Telnyx extension for voice workflows and webhook triggers in Elsa 3.8.0.
---

# Telnyx voice and webhook activities

The `Elsa.Telnyx` extension adds server-side activities for Telnyx Call
Control and triggers for Telnyx webhook events. Use it when a workflow needs
to receive an inbound call, place or control a call, play audio, collect DTMF
input, speak text, record a call, look up a number, or react to a call event.

The extension runs in the Elsa Server process. Elsa Studio can design a
workflow with the descriptors supplied by that server, but Studio does not
store Telnyx credentials, receive webhooks, or call the Telnyx API itself.

## Install and configure the server

Install the package in the application that executes workflows:

```bash
dotnet add package Elsa.Telnyx --version 3.8.0
```

Enable the module and configure the API key. `ApiUrl` defaults to
`https://api.telnyx.com`; `CallControlAppId` is required by activities that
create an outbound call, such as **Dial** and **Dial and Wait**.

```csharp
using Elsa.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddElsa(elsa =>
{
    elsa.UseTelnyx(telnyx =>
    {
        telnyx.ConfigureTelnyxOptions = options =>
        {
            options.ApiKey = builder.Configuration["Telnyx:ApiKey"]!;
            options.CallControlAppId = builder.Configuration["Telnyx:CallControlAppId"];
        };
    });
});
```

Keep `ApiKey` and the Call Control App ID in deployment configuration or a
secret store. The module sends the API key as a bearer token on its Refit HTTP
clients. `ConfigureHttpClientBuilder` and `HttpClientFactory` are available on
the feature when the host needs custom HTTP handlers, proxies, or transport
behavior.

## Map the Telnyx webhook endpoint

The extension does not map an endpoint automatically. Map a POST endpoint in
the Elsa Server application after building the app:

```csharp
var app = builder.Build();

app.UseTelnyxWebhooks("/webhooks/telnyx");
app.Run();
```

`UseWorkflows()` is not required for this endpoint; add it separately only
when the same host also uses Elsa's HTTP workflow activities.

The default route is `telnyx-hook`; pass a route pattern to choose a clearer
public URL. The endpoint reads the JSON body as a `TelnyxWebhook`, deserializes
the event payload into the release's known payload type, and publishes a
`TelnyxWebhookReceived` notification using Elsa's background notification
strategy. Matching handlers then start or resume workflows through stimuli.

The released endpoint has no built-in authentication, authorization, replay
protection, or Telnyx signature verification. Put the route behind the
authentication and network controls required by your deployment, or add a
front-door component that verifies the provider's webhook authenticity before
the request reaches `UseTelnyxWebhooks`. Do not expose the sample route
publicly without that additional boundary.

## Choose an activity

The activity picker receives the descriptors from the server after
`UseTelnyx` runs. Choose the activity based on the call lifecycle you need.

### Start and control calls

- **Incoming Call** is a trigger. Match one or more `From` or `To` phone
  numbers, or set `Catch All` to match any inbound call. Its result is a
  `CallInitiatedPayload`.
- **Dial** places an outbound call and returns a `DialResponse`. Its required
  `To` input accepts a phone number or SIP URI. `From`, caller display name,
  answering-machine detection, and recording are optional inputs.
- **Dial and Wait** places an outbound call and waits for either `Answered` or
  `Hangup`. Use its result when the next workflow step depends on that event.
- **Answer Call** answers an incoming call and waits for the `call.answered`
  event. Leave `Call Control ID` empty only when the ambient inbound call is
  the call you intend to answer.
- **Hangup Call** ends a call. The flow variant exposes `Done` and
  `Disconnected` outcomes.
- **Transfer Call** transfers a call to another destination and waits through
  the initiated, answered, or hangup events. It accepts destination, caller
  identity, optional audio, answering-machine, timeout, and time-limit inputs.
- **Bridge Calls** joins two call legs. Both call control IDs are required; the
  activity waits for bridged events for both legs before completing.
- **Get Call Status** returns a Boolean and exposes `Alive`, `Dead`, and `Done`
  outcomes.
- **Lookup Number** returns Telnyx number, carrier, caller-name, and
  portability information for a phone number.

The `Flow...` variants of Answer, Bridge, Hangup, Play Audio, Speak Text,
Start Recording, and Stop Audio Playback are the flowchart-oriented forms.
They expose explicit outcomes such as `Connected`, `Bridged`, `Done`, or
`Disconnected` so a Flowchart can branch on the call result.

### Play audio, speak, and gather DTMF

- **Play Audio** takes a call control ID and an audio URL. `Loop` accepts a
  number or `infinity`; `Overlay` mixes audio with existing playback; and
  `Target Legs` can be `self`, `opposite`, or `both`. The activity waits for
  `call.playback.started`.
- **Speak Text** converts a text or SSML payload to speech. Configure
  `Language`, `Voice`, `Payload`, optional `Payload Type` (`text` or `ssml`),
  and optional `Service Level`. It waits for `call.speak.ended`.
- **Gather Using Audio** plays an audio prompt and collects DTMF digits. Use
  its audio URL, valid digits, minimum and maximum digits, terminating digit,
  retry count, and timeout inputs. It branches to `Valid input`, `Invalid
  input`, or `Disconnected`.
- **Gather Using Speak** speaks a prompt and collects DTMF digits. It has the
  same gathering controls as the audio form plus language, voice, payload,
  payload type, and service level.
- **Start Recording** starts a call recording and waits for
  `call.recording.saved`; the flow form exposes `Recording finished` and
  `Disconnected`.
- **Stop Recording** stops recording for a call and exposes `Recording
  stopped` or `Disconnected`.
- **Stop Audio Playback** stops playback for a call. Its `Stop` input defaults
  to `all`; the flow form exposes `Done` and `Disconnected`.

Audio URLs are resolved by Telnyx, not downloaded by the Elsa activity. Make
the URL reachable by Telnyx and use HTTPS where appropriate. The server still
needs network access to the Telnyx API for every call-control operation.

## Design an inbound call workflow

A common designer sequence is:

1. Add **Incoming Call** and match the Telnyx number in `To`, the caller in
   `From`, or all calls with `Catch All`.
2. Add **Answer Call** and bind its `Call Control ID` to the incoming trigger
   result when it is not using the ambient call.
3. Add **Speak Text**, **Play Audio**, or a gather activity and bind the same
   call control ID.
4. Use the activity outcomes to branch, then finish with **Hangup Call** or
   another call-control action.
5. Publish the workflow only after the webhook route is reachable from Telnyx
   and protected by the host's webhook security boundary.

The extension creates a base64-encoded Telnyx `client_state` containing the
workflow execution ID; operations that pass an activity ID include that as
well. Later webhook handlers use the available client state and call control
ID to target the corresponding workflow or bookmark. This is why a
call-control activity should normally be allowed to create and wait for its
own callback before a later step tries to use the resulting call state.

## Use webhook event triggers

The module supplies three direct call triggers:

- **Incoming Call**, which filters `call.initiated` events by phone number;
- **Call Answered**, which waits for `call.answered` for one or more call
  control IDs; and
- **Call Hangup**, which waits for `call.hangup` for one or more call control
  IDs.

It also creates browsable, typed trigger descriptors from the payload types
annotated by the release. These event triggers are:

- **Call Bridged** — `call.bridged`
- **Call DTMF Received** — `call.dtmf.received`
- **Call Gather Ended** — `call.gather.ended`
- **Call Machine Greeting Ended** — `call.machine.greeting.ended`
- **Call Machine Premium Detection Ended** — `call.machine.premium.detection.ended`
- **Call Machine Premium Greeting Ended** — `call.machine.premium.greeting.ended`
- **Call Playback Ended** — `call.playback.ended`
- **Call Playback Started** — `call.playback.started`
- **Call Recording Saved** — `call.recording.saved`

The typed event triggers expose the corresponding payload as their result.
Activities such as **Dial and Wait**, **Play Audio**, **Speak Text**, and the
gathering activities use the same webhook pipeline internally: they create a
bookmark and resume it when a matching event arrives. An event without a
known payload type is deserialized as the release's unsupported payload and
does not create one of the typed event descriptors.

Do not confuse a webhook trigger with a webhook endpoint. The trigger is a
workflow activity; `UseTelnyxWebhooks` is the server route that receives the
provider request and feeds the trigger pipeline.

## Operational boundaries

- **Server ownership:** install and configure `Elsa.Telnyx` on every process
  that executes these activities or receives the webhook route. Installing it
  only in Studio does not make the activities executable.
- **Studio:** the release Studio repositories contain no Telnyx-specific
  client, settings page, credential store, webhook administration, or
  provider integration. Studio displays descriptors returned by the connected
  server.
- **Persistence and scaling:** call bookmarks and workflow instances use the
  persistence and runtime coordination configured for the Elsa host. Use the
  same durable stores and coordination model required by the rest of a
  multi-node deployment; the Telnyx module does not provide a separate call
  state store.
- **Retries:** Telnyx requests and call-control mutations are external side
  effects. A retry after an ambiguous timeout can repeat a dial, transfer, or
  media command. Design idempotency and reconciliation around the provider's
  call identifiers.
- **Sensitive data:** call payloads can contain phone numbers, caller data,
  client state, and recording URLs. Avoid writing raw webhook bodies or API
  keys to logs and limit who can inspect workflow inputs and outputs.
- **Release scope:** the module contains the call-control and webhook
  activities described here; it does not provide a Studio phone-number
  provisioning UI or a general Telnyx resource-management API.

## Troubleshooting checklist

1. Confirm `Elsa.Telnyx` is installed in the workflow execution host and
   `.UseTelnyx(...)` runs during startup.
2. Check that `ApiKey` is present and that `ApiUrl` points to the intended
   Telnyx API endpoint.
3. For **Dial** and **Dial and Wait**, confirm `CallControlAppId` is set and
   the Telnyx application is configured for the webhook URL you mapped.
4. Confirm Telnyx can reach the exact route passed to
   `UseTelnyxWebhooks(...)`, including any reverse-proxy path prefix.
5. If Studio cannot find an activity, verify that it is connected to the
   server where `UseTelnyx` is enabled and that the server exposes activity
   descriptors.
6. If a workflow remains waiting, inspect the received event type, call
   control ID, and the bookmark's correlation/client-state values. A
   `call.answered` or `call.hangup` event must match the call ID stored by the
   waiting activity.
7. If an audio or speech operation fails, verify the call is still active, the
   audio URL is reachable by Telnyx, and the input values satisfy Telnyx's
   constraints.
8. Add provider-aware webhook authentication and replay protection before
   diagnosing application behavior with a publicly exposed endpoint.

## Release source

This page is validated against the `release/3.8.0` source: Extensions
`a44e2b09af1202ff4936f493756e114c357eff81`, Core
`dff7d9f987394c3c2ba8003e6f9c803e97194fbc`, and Studio
`b008a52cc02840928824018056ca8299518f04b9`.

- [`TelnyxFeature` and module registration](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/telecom/Elsa.Telnyx/Features/TelnyxFeature.cs)
- [`AddTelnyx` services, API clients, and bearer authentication](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/telecom/Elsa.Telnyx/Extensions/DependencyInjectionExtensions.cs)
- [`UseTelnyx` module extension](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/telecom/Elsa.Telnyx/Extensions/ModuleExtensions.cs)
- [`UseTelnyxWebhooks` endpoint mapping](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/telecom/Elsa.Telnyx/Extensions/EndpointsExtensions.cs)
- [`Dial`, `DialAndWait`, and `IncomingCall`](https://github.com/elsa-workflows/elsa-extensions/tree/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/telecom/Elsa.Telnyx/Activities)
- [`WebhookEventActivityProvider` and typed webhook descriptors](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/telecom/Elsa.Telnyx/Providers/WebhookEventActivityProvider.cs)
- [`WebhookHandler` deserialization and notification flow](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/telecom/Elsa.Telnyx/Services/WebhookHandler.cs)
- [`TelnyxOptions`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/telecom/Elsa.Telnyx/Options/TelnyxOptions.cs)
- [`Telnyx` webhook event names](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/telecom/Elsa.Telnyx/WebhookEventTypes.cs)
