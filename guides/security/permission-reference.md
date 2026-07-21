---
description: >-
  Release-backed reference for Elsa API permission claims, Studio capabilities,
  and least-privilege role templates in Elsa 3.8.0.
---

# Elsa API permissions

Elsa API authorization is claim-based. When API security is enabled, Elsa
endpoints look for claims with the type `permissions`. Each claim value is an
Elsa permission string, such as `read:workflow-definitions`. The `*` value
grants all Elsa API permissions.

This page is a practical map for configuring Elsa Identity roles, external
identity providers, API-key applications, and Studio users. It covers the
common routes in the `release/3.8.0` API; modules can add more permissions of
their own.

For authentication middleware, tokens, and external provider setup, see the
[Authentication & Authorization Guide](../authentication.md) and [External
Identity Providers](external-identity-providers.md). For the .NET API client,
see [API & Client](../api-client/README.md).

## How permissions are applied

Elsa's API endpoint base classes call `ConfigurePermissions(...)`. With
security enabled, this registers the endpoint with the supplied permissions
and the `*` wildcard. With security disabled, the same helper permits
anonymous access. The permission strings are therefore part of the API
contract, not ASP.NET Core role names.

Elsa Identity obtains permissions from roles:

- JWT access tokens include the permissions of the user's assigned roles.
- API keys include the permissions of the roles assigned to the application
  that owns the key.
- An external OIDC or JWT host must emit `permissions` claims itself or map
  provider roles, groups, or scopes into that claim type during token
  validation.

An ASP.NET Core policy such as `RequireRole("WorkflowOperator")` can protect a
custom controller or page, but it does not satisfy an Elsa endpoint that is
checking `permissions`.

### A permission is one claim value

Emit one claim for each permission. For example, a service identity that can
read definitions and execute them has claims equivalent to:

```csharp
using System.Security.Claims;

var claims = new[]
{
    new Claim("permissions", "read:workflow-definitions"),
    new Claim("permissions", "exec:workflow-definitions")
};
```

When using Elsa Identity, configure the values on a role instead of creating
these claims manually. Avoid using `*` for user-facing or machine identities
unless full administrative access is genuinely intended.

## Common API permissions

The following table maps the most frequently used management operations to the
permissions declared by the release API.

| Operation | Permission | Typical routes |
| --- | --- | --- |
| Read workflow definitions and versions | `read:workflow-definitions` | `/workflow-definitions`, `/workflow-definitions/{id}`, `/workflow-definitions/{id}/versions` |
| Create or update a definition | `write:workflow-definitions` | `POST /workflow-definitions`, import routes |
| Publish a definition or update references | `publish:workflow-definitions` | `/workflow-definitions/{id}/publish`, reference updates, version revert |
| Retract a definition | `retract:workflow-definitions` | `/workflow-definitions/{id}/retract` |
| Delete definitions or versions | `delete:workflow-definitions` | delete and bulk-delete routes |
| Execute or dispatch a definition | `exec:workflow-definitions` | `/workflow-definitions/{id}/execute`, `/dispatch`, bulk dispatch |
| Read workflow instances, variables, and journal entries | `read:workflow-instances` | `/workflow-instances` and its journal, variables, and execution-state routes |
| Update root-workflow variables or import an instance | `write:workflow-instances` | `/workflow-instances/{id}/variables`, instance import |
| Cancel an instance | `cancel:workflow-instances` | `/cancel/workflow-instances/{id}` and bulk cancel |
| Delete instances | `delete:workflow-instances` | delete and bulk-delete routes |
| Read activity executions and call stacks | `read:activity-execution` | `/activity-executions` and summary routes |
| Read activity and designer metadata | See the metadata table below | `/descriptors/*`, `/features`, `/storage-drivers`, and related routes |

The route prefix is host-configurable. These route examples are relative to
the Elsa API base, which is commonly `/elsa/api`.

### Designer metadata

Give a Studio designer the metadata permissions required by the modules and
expression languages installed by the host. The release API declares these
permissions for the corresponding endpoints:

| Capability | Permission |
| --- | --- |
| Activity descriptors | `read:activity-descriptors` |
| Activity descriptor options | `read:activity-descriptors-options` |
| Expression descriptors | `read:expression-descriptors` |
| Storage drivers | `read:storage-drivers` |
| Variable descriptors | `read:variable-descriptors` |
| Installed feature list | `read:installed-features` |
| Workflow activation strategies | `read:workflow-activation-strategies` |
| Commit strategies | `read:commit-strategies` |
| Incident strategies | `read:incident-strategies` |
| Log persistence strategies | `read:log-persistence-strategies` |

The expression-descriptor endpoint additionally filters C# and Python
descriptors unless the caller has `exec:csharp-expressions` or
`exec:python-expressions`, respectively. Grant those execution permissions
only when the role is allowed to author or execute those expression types.

The descriptor and feature endpoints also accept the broader `read:*` value in
release 3.8.0. Prefer the named permissions for least privilege.

## Runtime and operational permissions

| Operation | Permission | Notes |
| --- | --- | --- |
| Read runtime status | `read:workflow-runtime` or `ManageWorkflowRuntime` | The status endpoint advertises both values. |
| Pause the runtime | `ManageWorkflowRuntime` | Administrative state change. |
| Resume the runtime | `ManageWorkflowRuntime` | Administrative state change. |
| Force-drain the runtime | `ManageWorkflowRuntime` | Administrative state change. |
| Read bookmark queue dead letters | `read:bookmark-queue:dead-letters` | Lists or reads dead-letter items. |
| Replay bookmark queue dead letters | `replay:bookmark-queue:dead-letters` | Requeues failed bookmark work. |
| Delete bookmark queue dead letters | `delete:bookmark-queue:dead-letters` | Destructive cleanup. |
| Read alterations | `read:alterations` | Inspect alteration requests. |
| Run or submit alterations | `run:alterations` | Applies or evaluates alteration work. |

`ManageWorkflowRuntime` is intentionally a distinct claim from
`read:workflow-runtime`; do not give it to a user who only needs status
visibility.

## Module-specific permissions

Install only the modules the host needs, then add their claims to the relevant
role. These are the permission values declared by common release modules:

| Module or capability | Read/view | Write or action |
| --- | --- | --- |
| Dashboard | `read:dashboard` | — |
| Console logs | `read:diagnostics:console-logs` | — |
| Structured logs | `read:diagnostics:structured-logs` | — |
| OpenTelemetry diagnostics | `read:diagnostics:opentelemetry` | — |
| Secrets | `read:secrets` | `write:secrets`, `delete:secrets`, `test:secrets` |
| Labels | `read:labels` | `create:labels`, `update:labels`, `delete:labels` |
| Workflow-definition labels | `read:workflow-definition-labels` | `update:workflow-definition-labels` |
| Tenants | `read:tenants` | `write:tenants`, `delete:tenants`, `execute:tenants:refresh` |
| Identity users | `read:user` | `create:user`, `update:user`, `delete:user` |
| Identity roles | `read:role` | `create:role`, `update:role`, `delete:role` |
| Identity applications | — | `create:application` |
| Resilience diagnostics | `read:resilience:strategies`, `read:resilience:retries` | `exec:resilience:simulate-response` |
| Authenticated event trigger | — | `trigger:event` |

Some module endpoints also advertise namespace wildcards such as `read:*` or
`exec:*`. Check the exact endpoint in the version you deploy before replacing
named claims with a wildcard.

The release also defines an `ingest:diagnostics:opentelemetry` constant, but
the checked collector and diagnostics API endpoints do not declare it through
`ConfigurePermissions(...)`. Do not assume that claim gates ingestion in
3.8.0; the HTTP collector instead checks its configured API key header or an
explicitly allowed loopback request. Verify the host and collector path you
deploy.

REST and live-connection checks are not identical for every module. The
Console Logs, OpenTelemetry, and Workflow Instance hubs explicitly accept
their read permission (plus the relevant wildcards). The Structured Logs hub
in 3.8.0 has an authentication requirement but no module-specific permission
authorizer, even though its REST endpoints require `read:diagnostics:structured-logs`.

## Starter role templates

These are starting points, not built-in Elsa roles. Put the listed strings in
the `Permissions` array of an Elsa Identity role, then assign that role to a
user or application. Add module-specific claims only when that role uses the
module.

| Role | Start with |
| --- | --- |
| **Studio viewer** | `read:workflow-definitions`, `read:workflow-instances`, `read:activity-execution`, and the required designer metadata claims |
| **Workflow designer** | Studio viewer claims plus `write:workflow-definitions`, `publish:workflow-definitions`, `retract:workflow-definitions`, and `delete:workflow-definitions` |
| **Workflow operator** | Studio viewer claims plus `cancel:workflow-instances`, `delete:workflow-instances`, and `read:workflow-runtime` |
| **Runtime administrator** | `read:workflow-runtime` and `ManageWorkflowRuntime` |
| **Workflow service runner** | Only the definition permissions needed by the service, commonly `read:workflow-definitions` and `exec:workflow-definitions`; add instance read/write claims only if it inspects or changes instances |
| **Identity administrator** | The `read/create/update/delete` claims for users and roles, plus `create:application` if it provisions API-key applications |

The Studio viewer and designer templates are intentionally separate from the
runtime administrator template. A user can design workflows without being
able to pause or force-drain the runtime.

### Role-management safeguard

Elsa Identity checks the caller's `permissions` claims when roles are created,
updated, or assigned. A caller cannot use its identity-management access to
grant permissions that it does not itself possess. Keep this in mind when
delegating role administration: the administrator must already have every
permission it needs to assign.

## What Studio users see

Studio is a client of the Elsa API; it is not a second authorization system.
The server remains the authority for each operation:

- In the workflow editor, release Studio treats a workflow as editable when
  the returned workflow resource includes a `publish` link. Without that
  capability, version-management actions are read-only or disabled.
- Diagnostics modules are gated by both the corresponding backend feature and
  permission. For example, missing `read:diagnostics:console-logs` hides the
  Console navigation item and makes direct navigation unavailable or
  unauthorized.
- A successful Studio login therefore does not prove that the token can load
  definitions, instances, designer metadata, or diagnostics. Those calls can
  still return `403 Forbidden` when the required Elsa claim is missing.

When a Studio screen is incomplete, identify the failing API request first and
add the permission for that operation. Do not grant `*` just to make a single
screen load.

## Boundaries that use different authorization

Elsa API permissions do not apply to every URL hosted by an Elsa application:

- Workflow routes handled by the `HttpEndpoint` activity use the activity's
  `Authorize` and `Policy` settings. See [HTTP Endpoint
  Security](http-endpoint-security.md).
- Bookmark-resume endpoints are intentionally anonymous in the release API;
  the encrypted resume token is the capability. Treat it as a secret and
  generate it with a bounded lifetime. See [API & Client](../api-client/README.md)
  and [resume-token guidance](examples/resume-endpoint-notes.md).
- Custom host controllers and pages can use ordinary ASP.NET Core policies,
  roles, or claims independently of Elsa API permissions.

## Troubleshooting missing access

1. Confirm API security is enabled or disabled as intended. When disabled,
   Elsa endpoint permission checks are bypassed.
2. Identify the exact API route returning `401` or `403`.
3. Check the final authenticated principal on the Elsa Server and verify that
   its claims use the literal type `permissions`.
4. Add the exact permission declared by that endpoint, or map the external
   provider's role/group/scope to it.
5. If the endpoint is a Studio metadata route, check the corresponding
   designer permission table above. If it is a module route, check the
   module-specific table and whether the module is installed.

`401` usually means the request was not authenticated. `403` usually means it
was authenticated but lacked an accepted permission or failed another policy.

### Release source checked

This reference was checked against the remote `origin/release/3.8.0` commits in
`elsa-core` and `elsa-studio`. The local `elsa-core` branch with the same name
had diverged, so it was not used as release evidence.
