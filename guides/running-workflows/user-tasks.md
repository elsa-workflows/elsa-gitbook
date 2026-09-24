---
description: Build workflow-bound human tasks, operate them in Elsa Studio, and integrate the User Tasks API.
---

# User Tasks

Use a **User Task** when a workflow must pause for a person to review,
approve, complete, or otherwise act on work. Elsa materializes a task record
from the activity, suspends the workflow at a bookmark, and resumes it when a
permitted completion action is submitted. Choose a persistent User Tasks
provider when that record must survive process restarts.

This page describes the User Tasks implementation in the 3.9.0 Core and
Studio releases. It is a Core/Studio feature; the 3.9.0 Extensions repository
does not contain a corresponding User Tasks module.

## The model in one minute

The runtime flow is:

1. The workflow reaches the `UserTask` activity.
2. Elsa materializes a task with its title, participants, actions, optional
   form, due date, and workflow identity.
3. The activity creates a bookmark and the workflow waits.
4. A worker claims, assigns, or completes the task through Studio or the API.
5. Elsa records the outcome and resumes the workflow from the bookmark.

The task is not a separate workflow instance. It is a projection of a
workflow-bound wait point. The record carries the workflow definition,
workflow instance, activity instance, and bookmark identifiers so operators
can connect human work back to the workflow that created it.

## Add the activity to a workflow

The activity is `Elsa.UserTasks.Activities.UserTask`. The smallest useful
workflow is:

```csharp
using Elsa.Workflows;
using Elsa.UserTasks.Activities;
using Elsa.Workflows.Activities;

public class PurchaseApproval : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new UserTask("Approve purchase")
            }
        };
    }
}
```

Configure the activity's inputs in code or in Elsa Studio. The main inputs
are:

| Input | Use it for |
| --- | --- |
| `Title`, `Summary`, `Instructions`, `Reference`, `Tags`, `TaskType` | The work item's identity and worker-facing context. |
| `Requester`, `Assignee` | The actor who requested the work or its current owner. |
| `CandidateUsers`, `CandidateGroups`, `ExcludedUsers` | Who may receive or claim the task. |
| `MembershipResolutionMode` | Resolve group membership live or snapshot it when the task is created. |
| `Priority`, `DueAt` | Queue ordering and overdue/timeout behavior. Priority is validated from `0` through `100`. |
| `Form` and `TaskData` | A provider-owned form reference and the data shown or submitted by the form. |
| `ActionDefinitions` | The completion choices, such as `Approve` and `Reject`. Keys are stable literals; labels may be expressions. |
| `EnableTimeoutOutcome`, `EnableCancellationOutcome` | Whether timeout and cancellation are valid workflow outcomes. |
| `Invitations` | Which guest-invitation verifier and completion actions may be used. |

If no action definitions are supplied, the runtime normalizes the task to one
`Complete` action. `Timeout` and `Cancelled` are reserved action keys, and
action keys must be unique.

### Live and snapshot membership

`Live` membership evaluates the configured participant directory when access
is checked. `Snapshot` stores the resolved group members with the task. Use a
snapshot when the assignment must not change as directory membership changes;
use live membership when the current organization roster should control access.

Snapshot resolution requires a participant directory when candidate groups are
used. If that directory is missing or cannot resolve a group, the task is
created with a blocking health issue and needs operational attention.

### Forms and protected data

Forms are provider-neutral references. A form provider resolves the reference
and returns field descriptors, actions, and a pinned version. The task stores
the resolved form projection so a later provider change does not silently
change an already-created task.

`TaskData` and `Instructions` are protected task content. The server omits
masked form values from the normal detail response. A caller with the required
permission can request one explicitly revealable field; that reveal is audited.

## Enable Core and persistence

The User Tasks feature registers the API, task services, bookmark projection,
and background workers. A host must also choose a persistence provider for
tasks that need to survive restarts or run on more than one server node.

In the standard modular server, the feature and SQLite persistence shell are
registered together:

```csharp
shell.WithFeatures(
    typeof(UserTasksFeature),
    typeof(SqliteUserTasksPersistenceShellFeature));
```

For a custom host, compose `UserTasksFeature` with one of the provider-specific
shell features. The 3.9.0 Core source includes SQLite, SQL Server, PostgreSQL,
MySQL, and Oracle User Tasks persistence packages. Use the provider matching
the rest of the host's database setup and apply its migrations before serving
tasks.

The Core service registration has in-memory defaults for the repository,
guest sessions, invitation outbox, and related services. Those defaults are
useful for a small development host but are process-local. They are not a
durable production configuration. The default invitation dispatcher also
drops deliveries; register a real dispatcher if the host uses guest links.

The module starts three periodic workers:

- the due worker marks overdue tasks and, when enabled on the task, applies the
  reserved timeout outcome;
- the reconciliation worker repairs task projections after an interrupted
  bookmark projection; and
- the invitation-delivery worker drains the encrypted invitation outbox and
  retries failed delivery with back-off.

The relevant defaults are a one-minute due sweep, a fifteen-minute
reconciliation sweep, a five-second invitation-delivery sweep, and a
256-KB maximum for protected task payloads. Configure `UserTasksOptions` when
these operational limits do not fit the host.

## Use User Tasks in Elsa Studio

Add the Studio module to the host that connects to the Elsa backend:

```csharp
builder.Services.AddUserTasksModule(backendApiConfig);
```

The menu is capability-gated. When the backend feature is enabled and the
caller can list and read tasks, open **Workflows → User Tasks**.

The queue provides these scopes:

- **Assigned to me**: tasks the current actor may work on;
- **Available**: tasks available to the current actor;
- **History**: terminal work such as completed, cancelled, or timed-out tasks;
- **All** and **Needs Attention**: manager views, shown only when the server
  grants manager access.

Use the search, status, priority, and due-date filters. Select a row to open
the detail page. Depending on the server-issued `allowedActions`, the page can
let a worker claim or release a task, submit a form action, or let a manager
assign it, change its schedule, retry participant/form resolution, or cancel it.

The detail page separates the evidence a worker needs:

- **Task** shows the summary, instructions, form, actions, assignee, and
  protected-data affordances;
- **Workflow** links back to the source definition and instance when the
  caller may see that context; and
- **History** shows the audited task events when the caller may see history.

Studio first attempts the User Tasks SignalR hub at
`/hubs/user-tasks`. The hub sends invalidations, not task data; Studio reloads
the authorized REST representation after an invalidation. If the hub is
unavailable or unauthorized, Studio falls back to polling. The default polling
interval is 30 seconds, and a hidden browser tab skips polling ticks until it
becomes visible again.

## Integrate through the API

The route prefix is host-configurable. The User Tasks routes below are shown
without the usual prefix:

| Method and route | Purpose |
| --- | --- |
| `GET /user-tasks/capabilities` | Discover feature and caller capabilities. |
| `GET /user-tasks` | List tasks by scope, status, priority, due date, search, workflow, and cursor. |
| `GET /user-tasks/{taskId}` | Read one task. Unauthorized or invisible tasks are concealed as not found. |
| `GET /user-tasks/{taskId}/events` | Read the task's audit history. |
| `POST /user-tasks/{taskId}/claim` and `/release` | Claim or release work. |
| `POST /user-tasks/{taskId}/assign` | Assign work to a participant. |
| `PATCH /user-tasks/{taskId}` | Update priority or due date. |
| `POST /user-tasks/{taskId}/complete` | Submit an action and optional form data. |
| `POST /user-tasks/{taskId}/cancel` | Cancel work with a reason. |
| `POST /user-tasks/{taskId}/retry-resolution` | Retry a failed participant or form resolution. |
| `POST /user-tasks/{taskId}/reveal` | Request one masked, revealable field. |
| `GET /user-task-participants` | Search a host-provided participant directory. |
| `POST`, `GET`, `DELETE /user-tasks/{taskId}/invitations...` | Issue, inspect, or revoke guest invitations. |

Mutation requests carry `expectedRevision`. Completion and cancellation also
carry an `operationId`. Generate one operation ID per user submission and
reuse it when retrying the same request; this makes a retry idempotent instead
of creating a second command. If another actor changed the task first, reload
the task and submit the command against the new revision.

Accepted terminal commands return `202 Accepted` because workflow resumption
happens out of band. Requery the task or consume the Studio invalidation before
assuming that the workflow has finished its continuation.

## Permissions and least privilege

Core 3.9.0 permissions use `{resource}:{verb}`. User Tasks contributes:

| Permission | Allows |
| --- | --- |
| `user-tasks:view` | List and read tasks, capabilities, events, and reveal permitted fields. |
| `user-tasks:update` | Change scheduling fields such as priority and due date. |
| `user-tasks:claim` | Claim and release tasks. |
| `user-tasks:complete` | Complete a task with an allowed action. |
| `user-tasks:assign` | Assign a task. |
| `user-tasks:cancel` | Cancel a task. |
| `user-tasks:invite` | Issue, list, and revoke guest invitations. |
| `user-tasks:supervise` | Use tenant-wide management scopes and retry resolution. |
| `user-tasks/participants:view` | Search users and groups for assignment. |

The server still applies task-level policy after the route permission check.
For example, a caller with `user-tasks:view` does not automatically see every
task in the tenant. The `all` and `needs-attention` scopes require manager
access and `user-tasks:supervise`, while participant access depends on the
task's assignee/candidate policy.

Use `GET /identity/permissions` on the deployed host for the authoritative
catalog. Installed modules can add permissions, and a role grant does not
replace the participant or task-level policy.

## Optional guest invitations

Guest access is an explicit extension of a task, not the default worker path.
The workflow designer declares a verifier and the action keys a guest may use;
an authorized manager then issues an invitation. Core stores only a hash of
the invitation token. The raw secret leaves through the configured dispatcher,
not in the invitation API response.

The anonymous flow is:

1. `GET /user-task-invitations/{token}` describes the challenge.
2. `POST /user-task-invitations/{token}/verify` verifies the challenge and
   issues a task-scoped guest session.
3. `GET /user-task-sessions/current` reads that one task.
4. `POST /user-task-sessions/current/complete` submits an allowed completion
   action.

Guest sessions are bounded by both the invitation expiry and the configured
guest-session lifetime. They are revoked when the task closes or the
invitation is revoked. The default verifier rejects every challenge, so a host
must register a real verifier before enabling this surface. Treat invitation
URLs and verification codes as credentials and provide a shared rate limiter
when the host has multiple replicas.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| User Tasks is absent from Studio | Confirm `AddUserTasksModule` is registered, the backend exposes `UserTasksFeature`, and the caller has `user-tasks:view`. Studio hides the menu when capabilities cannot be loaded. |
| The workflow does not continue | Confirm the workflow reached `UserTask`, the task has a valid completion action, and the runtime/bookmark persistence is durable. A successful `202` means the continuation was accepted, not that it has already finished. |
| A task is available but nobody can claim it | Check candidate users/groups, exclusions, the participant directory, and whether live or snapshot membership is appropriate. |
| A task has a blocking health issue | Inspect `healthCode`; common release-source cases are missing form providers and failed snapshot group resolution. |
| Studio reports a revision conflict | Reload the task. Another actor won the optimistic-concurrency race; resubmit using the current revision and a new command only when it is a new user action. |
| Guest links are created but never arrive | Configure a real invitation dispatcher. The default dispatcher intentionally drops deliveries, and delivery is retried by the encrypted outbox worker. |
| Updates are not immediate | Check the `/hubs/user-tasks` connection. Studio falls back to polling when SignalR is unavailable; inspect the configured polling interval. |

## Release-source references

These links are pinned to the 3.9.0 source used for this guide:

- [Core `UserTask` activity](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.UserTasks/Activities/UserTask.cs)
- [Core task contracts and models](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.UserTasks/Contracts/UserTaskContracts.cs)
- [Core task options](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.UserTasks/Options/UserTasksOptions.cs)
- [Core permissions](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.UserTasks/Permissions/UserTasksResourcePermissions.cs)
- [Core API endpoints](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.UserTasks/Endpoints/UserTasksEndpoints.cs)
- [Core service registration](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.UserTasks/Extensions/ServiceCollectionExtensions.cs)
- [Core background workers](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.UserTasks/HostedServices/UserTaskWorkers.cs)
- [Core SQLite User Tasks shell](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.UserTasks.Persistence.EFCore.Sqlite/ShellFeatures/SqliteUserTasksPersistenceShellFeature.cs)
- [Core modular server feature registration](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/apps/Elsa.ModularServer.Web/Program.cs)
- [Studio User Tasks module registration](https://github.com/elsa-workflows/elsa-studio/blob/f68d5b05a9c88ccd7db160af5503ce751aacb966/src/modules/Elsa.Studio.UserTasks/Extensions/ServiceCollectionExtensions.cs)
- [Studio queue page](https://github.com/elsa-workflows/elsa-studio/blob/f68d5b05a9c88ccd7db160af5503ce751aacb966/src/modules/Elsa.Studio.UserTasks/Pages/UserTasks.razor)
- [Studio task detail page](https://github.com/elsa-workflows/elsa-studio/blob/f68d5b05a9c88ccd7db160af5503ce751aacb966/src/modules/Elsa.Studio.UserTasks/Pages/UserTask.razor)
- [Studio API client and guest surface](https://github.com/elsa-workflows/elsa-studio/blob/f68d5b05a9c88ccd7db160af5503ce751aacb966/src/modules/Elsa.Studio.UserTasks/Client/IUserTasksApi.cs)
- [Studio realtime fallback](https://github.com/elsa-workflows/elsa-studio/blob/f68d5b05a9c88ccd7db160af5503ce751aacb966/src/modules/Elsa.Studio.UserTasks/Services/SignalRUserTaskRealtimeClient.cs)
- [3.9.0 Extensions module tree](https://github.com/elsa-workflows/elsa-extensions/tree/9d0e6fa4ec07818d60dd3e65ccaa84491eac94cf/src/modules)
