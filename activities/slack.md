---
description: >-
  Use the Elsa Slack extension to call Slack from workflows, with the
  release-specific registration and event-trigger boundaries.
---

# Slack activities

The `Elsa.Slack` extension supplies server-side activities for sending and
managing Slack messages, channels, files, reactions, reminders, saved items,
and users. It also contains six event-watching descriptors.

Use the action activities when a workflow needs to call Slack as one step in a
business process. Do not use the event-watching descriptors as triggers in
Elsa 3.8.0: their execution path is not implemented. The registration details
and this limitation are important because the release does not provide a
first-class `UseSlack()` extension method.

## Register the callable activities

Install the server-side package in the application that executes workflows:

```bash
dotnet add package Elsa.Slack --version 3.8.0
```

The release exposes `SlackFeature`, but its `Apply` method only registers the
singleton `SlackClientFactory`. It neither adds a `UseSlack` convenience method
nor scans the assembly for activities. Register the feature and scan the Slack
assembly explicitly:

```csharp
using Elsa.Extensions;
using Elsa.Slack.Activities;
using Elsa.Slack.Features;

builder.Services.AddElsa(elsa =>
{
    elsa.Use<SlackFeature>();
    elsa.AddActivitiesFrom<SlackActivity>();
});
```

`AddActivitiesFrom<SlackActivity>()` scans the assembly containing the marker
type and registers the concrete types marked with `[Activity]`. Without this
line, the Slack activities do not appear in the server's activity descriptors
and are not available to a connected Studio instance.

The package is server-side. Studio can display the generic activity
descriptors returned by the connected server, but the 3.8.0 Studio release has
no Slack-specific settings page, token store, or connection editor.

## Supply the token safely

Every concrete Slack action activity inherits a `Token` input. There is no
global token option in the release; the activity passes its resolved token to
the client factory at execution time.

Use an expression or application-owned secret mechanism rather than embedding
a token in a workflow definition or source file. For Elsa-managed secrets,
see [Secrets Management](../guides/security/secrets-management.md).

```csharp
using Elsa.Slack.Activities.Messages;
using Elsa.Workflows.Models;

var sendMessage = new CreateMessage
{
    Token = new Input<string>("token-from-your-secret-store"),
    Channel = new Input<string>("C0123456789"),
    Text = new Input<string>("The order is ready for review.")
};
```

The example shows the input shape; use the expression syntax and secret
provider configured by your host. Do not log the resolved token or put it in
workflow output.

`SlackClientFactory` is a singleton with a process-local dictionary keyed by
the raw token string. It reuses one `SlackApiClient` for each distinct token.
The release does not provide token eviction or rotation handling. If tokens
are rotated, plan the process lifetime and client-cache behavior as part of
your deployment rather than assuming a new client is created immediately.

## Choose an action activity

The activities are grouped by the category shown in their `[Activity]`
metadata. Each one calls SlackNet directly when the workflow executes.

### Messages

- **Send Message** posts text to a channel. `Channel` and `Text` are required;
  `Thread Timestamp` and `Reply Broadcast` control threaded replies. The
  activity outputs the sent message timestamp.
- **Update Message** changes the text of an existing message using its channel
  ID and timestamp.
- **Delete Message** removes a message using its channel and timestamp.
- **Pin Message** and **Unpin Message** manage a message's pinned state.

Message timestamps are Slack identifiers, not Elsa workflow instance IDs.
Carry them through an activity output or a variable when a later step needs to
update, delete, pin, or reply to a message.

### Channels

- **Create Channel** accepts a channel name, an `Is Private` flag, and an
  optional team ID; it outputs the created conversation.
- **Get Channel** returns a conversation by channel ID.
- **Get Private Channel Message** retrieves one message by channel ID and
  message timestamp. It faults when the message is not found.
- **Kick User From Channel** and **Leave Channel** change channel membership.
- **List Channels** and **List Channel Members** return paged results and a
  next cursor.
- **Set Channel Purpose** and **Set Channel Topic** update channel metadata
  and output the updated value.

Channel IDs and user IDs should come from Slack or previous activity outputs;
channel display names are not interchangeable with IDs in activities that
explicitly ask for an ID.

### Files

**Upload File** uploads a SlackNet `FileUpload`. It can share the file in a
channel, reply in a thread, and add an initial comment. When the channel is
omitted, the source describes the upload as private. The activity outputs an
`ExternalFileReference`.

The `File` input is a SlackNet object, not a URL or local-path convenience
input. Resolve and construct the file in a host activity or another supported
integration before handing it to `Upload File`; the Elsa Slack activity does
not download arbitrary URLs for you.

### Reactions and saved items

- **Add Reaction**, **List Reactions**, and **Remove Reaction** work with emoji,
  channel, timestamp, and user IDs as appropriate.
- **Save Item** and **Remove Saved Item** manage a saved message or item using
  its channel ID and timestamp.

### Reminders

**Create Reminder**, **Get Reminder**, **List Reminders**, **Complete Reminder**,
and **Delete Reminder** manage Slack reminders. `Create Reminder` accepts the
reminder text and a Slack-compatible time value, optionally targeting a user,
and outputs the reminder ID. The source documents Unix timestamps and natural
language values such as `in 15 minutes`; validate those values against the
Slack API behavior you deploy against.

### Users and search

- **Get User** retrieves a user by ID.
- **List Users** returns users in the workspace.
- **Search User By Email** looks up a user by email address.
- **Set Status** updates status text, emoji, and expiration.
- **Search Messages** accepts a Slack search query and optional page/count
  inputs. The release defaults count to `100` and page to `1` when those
  inputs are not supplied.

Search modifiers such as `in:#channel` and `from:@user` are passed to Slack;
they are not parsed or validated by Elsa before the API call.

## Example: send a threaded message

This code-first example sends a root message and then replies to it. In a
designer, use the `Message Timestamp` output from **Send Message** as the
`Thread Timestamp` input of a second instance.

```csharp
using Elsa.Slack.Activities.Messages;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;

public sealed class NotifyOrderWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        var sendRoot = new CreateMessage
        {
            Token = new Input<string>("token-from-your-secret-store"),
            Channel = new Input<string>("C0123456789"),
            Text = new Input<string>("Order 1042 needs review.")
        };

        var sendReply = new CreateMessage
        {
            Token = new Input<string>("token-from-your-secret-store"),
            Channel = new Input<string>("C0123456789"),
            Text = new Input<string>("The review task is assigned.")
        };

        sendReply.ThreadTimestamp =
            new Input<string?>(sendRoot.MessageTimestamp);
        sendReply.ReplyBroadcast = new Input<bool>(false);

        builder.Root = new Sequence
        {
            Activities = { sendRoot, sendReply }
        };
    }
}
```

Replace the token placeholder with the expression or input binding used by
your host. The important dependency in this example is the root message's
timestamp, which becomes the reply's thread timestamp.

## Event activities are not working triggers in 3.8.0

The package includes these annotated descriptors:

- **Watch Direct Messages**
- **Watch Files**
- **Watch Multiparty Direct Messages**
- **Watch New Events**
- **Watch Public Channel Messages**
- **Watch Users**

They inherit from `SlackEventActivity`, which exposes `Token` and `Bot User
Id`; **Watch Public Channel Messages** also exposes `Channel Id`. In the
release, each concrete `ExecuteAsync` throws:

```text
NotImplementedException: Event subscription requires WebSocket implementation.
```

The provided `SlackTriggerActivity` base class is not used by any of the six
watcher classes, and the package contains no WebSocket/event-subscription
implementation. Treat these descriptors as incomplete release surface, not
as production-ready Slack triggers. For inbound Slack events, receive and
verify Slack requests in an application-owned HTTP endpoint, then start or
resume a workflow through an explicitly secured integration. Do not publish a
workflow that depends on one of the watcher activities in 3.8.0.

## Errors, scopes, and operations

The action activities await SlackNet calls directly. They do not add a retry
policy or translate Slack API errors into Elsa-specific result objects. Invalid
tokens, missing Slack scopes, inaccessible channels, invalid IDs, rate limits,
network failures, and other Slack errors surface through normal Elsa activity
fault handling.

For production workflows:

- grant the bot only the Slack scopes required by the activities it can run;
- keep tokens in a protected deployment or secret store and avoid placing them
  in persisted workflow JSON;
- treat message posting, deletion, channel membership, status changes, and
  reminders as external side effects when designing retries;
- add application-level idempotency when repeating a mutation could create a
  duplicate message or reminder; and
- test the exact workspace permissions and channel visibility with a
  non-production bot before publishing the workflow.

The release's Slack test project contains only a skipped `CreateChannel` test,
so a successful build does not prove that the Slack API calls or watcher
integration work against a real workspace.

## Troubleshooting

1. **Activities are missing from Studio.** Confirm the server references
   `Elsa.Slack`, calls `Use<SlackFeature>()`, and calls
   `AddActivitiesFrom<SlackActivity>()`. Restart the server so its descriptors
   are rebuilt.
2. **The activity faults with an authorization error.** Check the token,
   token type, required Slack scopes, workspace membership, and channel access.
3. **A channel or message cannot be found.** Use the Slack channel ID and
   message timestamp expected by the specific activity; display names and
   message text are not substitutes.
4. **A watcher faults immediately.** This is expected in 3.8.0. Use an
   application-owned, authenticated Slack event ingress instead.
5. **Rotated tokens behave unexpectedly.** Remember that the singleton client
   factory caches clients by raw token for the process lifetime; plan a restart
   or an application-owned replacement when rotating credentials.

## Release source

This page is validated against the `release/3.8.0` source snapshots. The
Extensions branch advanced to `a44e2b09af1202ff4936f493756e114c357eff81`
since the prior documentation run.

- [`SlackFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/communication/Elsa.Slack/Features/SlackFeature.cs)
  registers the client factory but does not register the activity assembly.
- [`SlackActivity` and token input](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/communication/Elsa.Slack/Activities/SlackActivity.cs)
- [`SlackClientFactory` cache](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/communication/Elsa.Slack/Services/SlackClientFactory.cs)
- [`CreateMessage`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/communication/Elsa.Slack/Activities/Messages/CreateMessage.cs)
- [`UploadFile`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/communication/Elsa.Slack/Activities/Files/UploadFile.cs)
- [`Slack event activity base`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/communication/Elsa.Slack/Activities/Events/SlackEventActivity.cs)
- [`Watch New Events` unimplemented execution](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/communication/Elsa.Slack/Activities/Events/WatchNewEvents.cs)
- [`Elsa module Use<T>`](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/common/Elsa.Features/Extensions/DependencyInjectionExtensions.cs)
- [`AddActivitiesFrom<T>`](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/modules/Elsa.Workflows.Management/Extensions/ModuleExtensions.cs)
