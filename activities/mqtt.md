---
description: Configure MQTT connections and use MQTT triggers and activities in Elsa Workflows 3.9.0.
---

# MQTT activities

The `Elsa.Mqtt` extension connects Elsa Server to MQTT brokers. It provides a
trigger that starts or resumes workflows when a topic filter matches, and an
activity that publishes a text message to a topic.

Use this module when an application already communicates through MQTT, such as
an IoT or telemetry system. It is a standalone MQTT integration; it is not a
MassTransit transport and it does not move Elsa's own workflow-dispatch traffic
onto MQTT.

Elsa Studio does not connect to the broker. The server owns the MQTT clients,
subscriptions, and message delivery. Studio receives the activity descriptors
and the configured connection names from the server.

## Install and register the module

Install the `Elsa.Mqtt` package in the application that executes workflows:

```bash
dotnet add package Elsa.Mqtt --version 3.9.0
```

Register the module during Elsa startup. This example configures one named
connection and enables TLS:

```csharp
using Elsa.Extensions;
using Elsa.Mqtt.Options;

builder.Services.AddElsa(elsa =>
{
    elsa.UseMqtt(mqtt =>
    {
        mqtt.ConfigureOptions = options =>
        {
            options.AddConnection("telemetry", new MqttConnectionOptions
            {
                Host = "mqtt.example.com",
                Port = 8883,
                Username = configuration["Mqtt:Username"],
                Password = configuration["Mqtt:Password"],
                UseTls = true
            });
        };
    });
});
```

`UseMqtt` registers the activities, the MQTT client and subscriber managers,
the Studio input handlers, the workflow trigger/bookmark handlers, and the
startup task that restores subscriptions. The module uses MQTTnet 5.1.0.1559
in the 3.9.0 Extensions release.

Keep broker credentials and client-certificate passwords in deployment
configuration or a secret store. Set `AllowUntrustedCertificates` only for
development or controlled testing; it allows a broker certificate that cannot
be validated against a trusted root.

## Configure connections

`MqttOptions` stores named connections. An activity with no `Connection Name`
uses the case-insensitive `Default` connection. Otherwise, the name must match
one registered with `AddConnection` or `AddDefaultConnection`; an unknown name
causes client creation to fail.

For more control than `MqttConnectionOptions`, pass an MQTTnet
`MqttClientOptions` instance to `AddConnection`:

```csharp
options.AddDefaultConnection(new MqttConnectionOptions
{
    Host = "localhost",
    Port = 1883,
    ClientId = "elsa-development"
});

options.AddConnection("secure", new MqttConnectionOptions
{
    Host = "mqtt.example.com",
    Port = 8883,
    UseTls = true,
    ClientCertificatePath = "/etc/elsa/mqtt-client.p12",
    ClientCertificatePassword = configuration["Mqtt:CertificatePassword"]
});
```

The convenience options configure a TCP server, a 30-second keep-alive, and a
30-second client timeout. A missing `ClientId` gets a generated `Elsa...` ID.
Username-only credentials are supported; a password is used only when a
username is also supplied.

The subscriber reconnects after an unexpected disconnection. By default,
`MaxReconnectAttempts = 0` means unlimited attempts, and
`ReconnectBaseDelay` is five seconds. The delay doubles for each attempt and
is capped at five minutes. After reconnecting, the module restores the active
topic subscriptions:

```csharp
elsa.UseMqtt(mqtt =>
{
    mqtt.ConfigureOptions = options =>
    {
        options.MaxReconnectAttempts = 10;
        options.ReconnectBaseDelay = TimeSpan.FromSeconds(2);
        options.AddDefaultConnection(new MqttConnectionOptions
        {
            Host = "mqtt.example.com",
            Port = 8883,
            UseTls = true
        });
    };
});
```

The module does not create brokers, topics, users, certificates, or access
rules. Provision those through the MQTT platform or deployment tooling.

## Receive MQTT messages

Add **MQTT Message Received** from the **MQTT** category. It has these inputs
and outputs:

| Property | Direction | Behavior |
| --- | --- | --- |
| **Connection Name** | Input | Selects a configured connection; defaults to `Default`. |
| **Topics** | Input | One or more MQTT topic filters. At least one filter is required. |
| **Received MQTT Message** | Output | An `MqttMessage` containing `Topic` and text `Message` values. |

The topic filter supports MQTT's `+` single-level and `#` multi-level
wildcards. For example, `devices/+/temperature` matches one device level,
while `devices/#` matches that prefix and any remaining levels.

At the start of a workflow, the activity is a trigger. Inside a running
workflow, it creates a bookmark and waits for a matching message. The module
passes the received message into the workflow, sets the output, and uses the
message text as the activity result.

For example, a workflow can wait for a sensor event and then publish an alert:

```csharp
using Elsa.Mqtt.Activities;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;
using MQTTnet.Protocol;

var workflow = new Sequence
{
    Activities =
    {
        new MqttMessageReceived
        {
            ConnectionName = new("telemetry"),
            Topics = new(new List<string> { "devices/+/temperature" })
        },
        new PublishMqttMessage
        {
            ConnectionName = new("telemetry"),
            Topic = new("alerts/temperature"),
            Message = new("Temperature message received"),
            QualityOfServiceLevel = new(MqttQualityOfServiceLevel.AtLeastOnce),
            Retain = new(false)
        }
    }
};
```

The module converts the MQTT payload to a string before creating
`MqttMessage`. It does not expose the original binary payload, MQTT 5 user
properties, subscription identifiers, or shared-subscription metadata through
this activity.

## Publish MQTT messages

Add **Publish MQTT message** from the **MQTT** category. Configure:

- **Connection Name** — the named broker connection.
- **Topic** — the destination topic. Blank or whitespace-only values produce
  the `Failure` outcome.
- **Message** — the text payload. A `null` value produces the `Failure`
  outcome.
- **QoS** — `0 - At Most Once`, `1 - At Least Once`, or `2 - Exactly Once`.
- **Retain** — whether the broker should retain the message.

The activity performs one MQTT publish call. A successful broker response
completes the activity with the `Success` outcome and a `true` result. A
negative broker response completes it with `Failure` and a `false` result. The
activity journals the broker reason code, reason string, topic, and message
length; it does not add a dead-letter or retry queue.

The QoS dropdown is provided by the server module, so it appears in Studio
after the MQTT package is installed and the server is restarted. The
connection-name dropdown is populated from the names registered in
`MqttOptions`.

## How subscriptions and workflow delivery work

The module keeps MQTT subscriptions aligned with persisted Elsa state:

1. At startup, the MQTT background task loads stored `MQTT Message Received`
   triggers and bookmarks.
2. Elsa creates one subscriber for each named connection that has a trigger or
   bookmark. It subscribes to the distinct topic filters currently required by
   those bindings.
3. When a workflow definition or bookmark is indexed, added, removed, or
   deleted, the module updates the broker subscriptions.
4. An incoming message is matched against the stored filters. Matching start
   triggers invoke new workflow instances; matching bookmarks are queued to
   resume their workflow instances.

The subscriber asks the broker for QoS 1 (`At Least Once`) when it subscribes,
independently of the QoS selected by **Publish MQTT message**. The module does
not provide a shared-consumer group across Elsa hosts. Each host creates its
own MQTT client, using a generated client ID by default, so a scale-out design
must account for duplicate delivery and idempotent workflow effects according
to the broker's client and subscription semantics.

## Studio and deployment boundaries

The MQTT package is server-side. Studio can display and configure the
activities after it receives their descriptors, but it does not open broker
connections, manage topics, test credentials, or provide an MQTT resource
administration page. Broker connectivity and subscription logs belong to the
Elsa Server process.

For a split deployment, run the MQTT module on the host that should receive
messages and has access to workflow persistence. Registering the module on
multiple hosts creates multiple broker clients and is not a built-in load
balancing strategy.

## Troubleshooting

### The activity is missing from Studio

Confirm that `Elsa.Mqtt` is installed in the server application, `UseMqtt` is
registered, and the server has restarted. Studio receives the activity list
from the server; installing a package only in the Studio frontend does not add
the server activity.

### The connection name is not available

The dropdown lists the keys in `MqttOptions.Connections`. Register the name
with `AddConnection` or use `AddDefaultConnection` and leave the activity
input empty. Check spelling and casing; resolution is case-insensitive, but a
name still must exist.

### A trigger never receives messages

Check that the server can connect to the configured broker, the topic filter is
valid, and the incoming topic matches the filter. Inspect server logs for the
subscription and reconnect messages. Remember that an inline
`MQTT Message Received` activity creates a bookmark only after the workflow
reaches that activity.

### The workflow receives duplicate events

Inspect whether more than one Elsa host is connected with an active
subscription. The module creates a client per host and does not coordinate a
shared consumer group. Make downstream effects idempotent and verify the
broker's subscription and client configuration.

## Release source

This page describes the Elsa Extensions `release/3.9.0` implementation:

- [`UseMqtt` and module registration](https://github.com/elsa-workflows/elsa-extensions/blob/3a7ae6b7ad689be2f5849920c6b36fe3ea59b9aa/src/modules/mqtt/Elsa.Mqtt/Extensions/ModuleExtensions.cs)
- [`MqttFeature` service and activity registration](https://github.com/elsa-workflows/elsa-extensions/blob/3a7ae6b7ad689be2f5849920c6b36fe3ea59b9aa/src/modules/mqtt/Elsa.Mqtt/Features/MqttFeature.cs)
- [`MqttOptions` connection and reconnect settings](https://github.com/elsa-workflows/elsa-extensions/blob/3a7ae6b7ad689be2f5849920c6b36fe3ea59b9aa/src/modules/mqtt/Elsa.Mqtt/Options/MqttOptions.cs)
- [`MqttConnectionOptions` TLS and client options](https://github.com/elsa-workflows/elsa-extensions/blob/3a7ae6b7ad689be2f5849920c6b36fe3ea59b9aa/src/modules/mqtt/Elsa.Mqtt/Options/MqttConnectionOptions.cs)
- [`MQTTnet` package version](https://github.com/elsa-workflows/elsa-extensions/blob/3a7ae6b7ad689be2f5849920c6b36fe3ea59b9aa/Directory.Packages.props)
- [`MQTT Message Received`](https://github.com/elsa-workflows/elsa-extensions/blob/3a7ae6b7ad689be2f5849920c6b36fe3ea59b9aa/src/modules/mqtt/Elsa.Mqtt/Activities/MqttMessageReceived.cs)
- [`Publish MQTT message`](https://github.com/elsa-workflows/elsa-extensions/blob/3a7ae6b7ad689be2f5849920c6b36fe3ea59b9aa/src/modules/mqtt/Elsa.Mqtt/Activities/PublishMqttMessage.cs)
- [`Subscription and reconnect behavior`](https://github.com/elsa-workflows/elsa-extensions/blob/3a7ae6b7ad689be2f5849920c6b36fe3ea59b9aa/src/modules/mqtt/Elsa.Mqtt/Services/MqttSubscriber.cs)
- [`Workflow trigger and bookmark delivery`](https://github.com/elsa-workflows/elsa-extensions/blob/3a7ae6b7ad689be2f5849920c6b36fe3ea59b9aa/src/modules/mqtt/Elsa.Mqtt/Handlers/TriggerMqttWorkflows.cs)
- [`Topic wildcard matching`](https://github.com/elsa-workflows/elsa-extensions/blob/3a7ae6b7ad689be2f5849920c6b36fe3ea59b9aa/src/modules/mqtt/Elsa.Mqtt/Utilities/MqttTopicMatcher.cs)
- [`Startup subscription restoration`](https://github.com/elsa-workflows/elsa-extensions/blob/3a7ae6b7ad689be2f5849920c6b36fe3ea59b9aa/src/modules/mqtt/Elsa.Mqtt/Tasks/StartMqttSubscriptionsTask.cs)
