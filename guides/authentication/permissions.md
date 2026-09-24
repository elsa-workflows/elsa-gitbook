---
description: >-
  Release-backed reference for Elsa API permission claims, the deployed
  permission catalog, Studio capabilities, and least-privilege role templates
  in Elsa 3.9.0.
---

# Elsa API permissions

Elsa API authorization uses claims with the type `permissions`. In the 3.9.0
Core API, each claim value has the form `{resource}:{verb}`, for example:
`workflows/definitions:view` or `identity/roles:update`.

This page is a practical reference for Elsa Identity roles, API-key clients,
external identity providers, Studio users, and operators. The deployed API
catalog is authoritative because installed modules can add resources and
verbs. The tables here cover the common Core resources; they are not a
replacement for querying the catalog of the host you deploy.

For authentication middleware, tokens, and external provider setup, see
[Authentication & Authorization](README.md). For workflow ingress policies,
see [HTTP Endpoint Security](../security/http-endpoint-security.md). For the
.NET API client, see [API & Client](../api-client/README.md).

## The permission grammar

| Part | Meaning | Examples |
| --- | --- | --- |
| Resource | A registered API capability. Resource paths use `/` hierarchy. | `workflows/definitions`, `identity/roles` |
| Verb | The operation required for that resource. Core recommends `view`, `create`, `update`, `write`, `delete`, and `execute`; modules can add verbs. | `view`, `publish`, `revert`, `control` |
| Claim value | One resource and one verb separated by `:`. | `workflows/definitions:publish` |

The 3.9.0 Core parser and matcher apply these rules:

- A bare `*` means the complete permission vocabulary and is normalized to
  `*:*` internally.
- A resource wildcard such as `workflows/*:view` covers the named resource
  and every descendant below it. It can also cover the `workflows` resource
  itself.
- A verb wildcard such as `workflows/definitions:*` covers every supported
  verb for that resource.
- Wildcards are only valid as the whole resource, a trailing `/*` resource
  segment, or the whole verb. Values such as `workflows*:view` and
  `workflows/*/definitions:view` do not match anything and are rejected when
  Core validates a role grant.
- Verbs do not imply one another. `workflows/definitions:write` does not grant
  `workflows/definitions:publish`, and `view` does not grant `execute`.

The old `read:workflow-definitions` style must not be mechanically rewritten
as `view:workflows/definitions`. The Core 3.9.0 form is
`workflows/definitions:view`: the resource is first and the verb is second.
Some older or separately shipped extension endpoints still declare their
legacy strings; see [Extension modules](#extension-modules-and-the-catalog).

## How Elsa applies permissions

Core endpoints declare a resource and verb, then evaluate the authenticated
principal's `permissions` claims against that requirement. If Elsa API
security is disabled, Core's permission helper allows the endpoint to be
anonymous; that is a host-wide security decision, not a grant that should be
copied into a role.

Elsa Identity expands roles into permission claims for Elsa-issued JWTs and
API-key clients. An external authentication host must emit the `permissions`
claim itself or map trusted provider roles, groups, or scopes into that claim
type. One claim value represents one permission:

```csharp
using System.Security.Claims;

var claims = new[]
{
    new Claim("permissions", "workflows/definitions:view"),
    new Claim("permissions", "workflows/definitions:execute")
};
```

An ASP.NET Core policy such as `RequireRole("WorkflowOperator")` protects a
custom host endpoint, but it does not satisfy an Elsa endpoint that checks
`permissions`. Likewise, a permission on an Elsa API route does not protect a
workflow route exposed by the `HttpEndpoint` activity; that activity uses its
own `Authorize` and `Policy` settings.

## Discover the deployed catalog

Use these endpoints when building a role editor, diagnosing a Studio screen,
or generating a least-privilege client grant. The route prefix is
host-configurable and is commonly `/elsa/api`.

### `GET /identity/permissions`

Returns the registered permission catalog. The caller needs
`identity/roles:view`, because the catalog describes what a role can contain.
The response includes:

- `coreVerbs`: the recommended verbs: `view`, `create`, `update`, `write`,
  `delete`, and `execute`;
- `resources`: every descriptor registered by the installed modules;
- each resource's supported verbs, display name, description, category, and
  whether its descriptor is verified; and
- `nonCoreVerbs`, so role administrators can distinguish module-specific verbs
  from the common vocabulary.

The endpoint is the right source for a custom role-management UI. Do not
hard-code a complete permission list into Studio integrations.

### `GET /identity/permissions/reach?resource=workflows/*`

Returns the catalog resources covered by a resource pattern and a count. It
also requires `identity/roles:view`. Studio uses this shape when it explains
which concrete resources a wildcard grant reaches.

### `GET /identity/me/permissions`

Returns the effective concrete verbs held by the current authenticated caller
for catalog resources. It requires an authenticated caller but no separate
permission grant. This is useful when a Studio user can sign in but a feature
is missing: compare the caller's effective grants with the API request that
failed.

### Role validation

Core validates permissions submitted when a role is created or updated. It
checks the grammar, meaningful wildcard placement, registered resources, and
supported verbs. Wildcard grants are allowed even when a matching module is
not installed yet, which lets a role survive a later module installation.

## Common Core resources in 3.9.0

These resources come from Core modules when those modules are installed. Use
the deployed catalog for the definitive list and for any additional module.

### Workflow runtime and authoring

| Resource | Supported verbs | Typical use |
| --- | --- | --- |
| `workflows/definitions` | `view`, `write`, `delete`, `execute`, `publish`, `retract`, `refresh`, `reload` | Browse, author, publish, run, refresh, or reload workflow definitions. |
| `workflows/definitions/versions` | `view`, `delete`, `revert` | Inspect, delete, or revert individual definition versions. |
| `workflows/instances` | `view`, `write`, `delete`, `cancel` | Inspect, import, delete, or cancel workflow instances. |
| `workflows/activity-executions` | `view` | Inspect persisted activity execution records and summaries. |
| `workflows/runtime` | `view`, `control` | Read runtime status or pause, resume, and drain the runtime. |
| `workflows/bookmark-queue/dead-letters` | `view`, `replay`, `delete` | Inspect, replay, or delete bookmark dead-letter items. |
| `workflows/events` | `trigger` | Trigger workflow events. |
| `workflows/tasks` | `complete` | Complete external workflow tasks. |
| `workflows/tests` | `execute` | Execute activity tests. |

Definition versions are a separate resource. For example, a Studio user who
can list definitions with `workflows/definitions:view` still needs
`workflows/definitions/versions:view` to load version history and
`workflows/definitions/versions:revert` to roll a definition back.

### Studio and designer metadata

| Resource | Verb | What it provides |
| --- | --- | --- |
| `workflows/descriptors/activities` | `view` | Activity types and options for the designer. |
| `workflows/descriptors/expressions` | `view` | Available expression types. |
| `workflows/descriptors/storage-drivers` | `view` | Storage-driver metadata. |
| `workflows/descriptors/variables` | `view` | Variable-type metadata. |
| `workflows/descriptors/commit-strategies` | `view` | Commit-strategy metadata. |
| `workflows/descriptors/incident-strategies` | `view` | Incident-strategy metadata. |
| `workflows/descriptors/log-persistence-strategies` | `view` | Log-persistence strategy metadata. |
| `workflows/descriptors/output-converters` | `view` | Output-converter metadata. |
| `workflows/descriptors/activation-strategies` | `view` | Workflow activation-strategy metadata. |
| `system/features` | `view` | Installed feature information. |

Expression descriptors do not by themselves grant code execution. Expression
modules can also have host configuration and runtime prerequisites. For
example, the 3.9.0 Python.NET evaluator refuses execution unless
`PythonOptions.AllowHostCodeExecution` is enabled. Check the installed
expression module and the failed request rather than granting broad API
permissions to every designer.

### Identity and common modules

| Resource | Supported verbs | Typical use |
| --- | --- | --- |
| `identity/users` | `view`, `create`, `update`, `delete` | Manage user accounts. |
| `identity/roles` | `view`, `create`, `update`, `delete` | Manage roles and their grants. |
| `identity/applications` | `create` | Create API client applications. |
| `dashboard` | `view` | View operational dashboard data. |
| `diagnostics/console-logs` | `view` | Read live and recent console logs. |
| `diagnostics/structured-logs` | `view` | Read structured log records and sources. |
| `diagnostics/opentelemetry` | `view` | Search traces, logs, metrics, and resources. |
| `labels` | `view`, `create`, `update`, `delete` | Manage labels. |
| `workflows/definitions/labels` | `view`, `update` | Read or change labels assigned to definitions. |
| `secrets` | `view`, `write`, `delete`, `test` | Manage secret records, including rotation and revocation. |
| `tenants` | `view`, `write`, `delete`, `refresh` | Manage tenants and refresh the tenant registry. |
| `resilience/retries` | `view` | Inspect retry attempts. |
| `resilience/strategies` | `view` | Browse resilience strategies. |
| `resilience/simulation` | `execute` | Simulate a resilience response. |
| `alterations` | `view`, `execute` | Inspect and run alteration plans. |
| `user-tasks` | `view`, `update`, `claim`, `complete`, `assign`, `cancel`, `invite`, `supervise` | Work on human tasks or supervise a tenant-wide task queue. |
| `user-tasks/participants` | `view` | Search users and groups for task assignment. |

External Authentication, User Tasks, AI, and other Core modules contribute
additional resources and module-specific verbs. Query the catalog instead of
assuming that this abbreviated table is exhaustive.

## Extension modules and the catalog

The Core catalog is built from `IPermissionDescriptorProvider` registrations.
The checked 3.9.0 Extensions source tree does not register permission
descriptors for its endpoints; several endpoints still call the compatibility
`ConfigurePermissions(...)` helper with literal permission strings. Therefore
an extension permission may not appear in `/identity/permissions`, and you
must use the exact value declared by the endpoint until that module publishes
a descriptor-backed contract.

Examples from the 3.9.0 Extensions source include:

| Extension endpoint family | Declared permission values |
| --- | --- |
| Connections | `connections:read`, `connections:write`, `connections:delete`, `connections/descriptor:read` |
| Agents | `ai/agents:read`, `ai/agents:write`, `ai/agents:delete`, `ai/agents:invoke`, `ai/skills:read` |
| Workflow Context provider descriptors | `read:workflow-context-provider-descriptors` |

These examples are deliberately shown exactly as declared. Do not invert the
resource and verb or assume that an extension's `read` literal is equivalent
to a Core `:view` grant. When a package is upgraded, re-check its endpoint
source or package documentation because extension contracts can migrate
independently from Core.

For custom modules, prefer Core's `RequirePermission(resource, verb)` contract
and register a `PermissionDescriptor` provider. That lets the endpoint and
the catalog describe the same resource, supported verbs, and display metadata.

## Starter role templates

These are starting points, not built-in Elsa roles. Put the listed values in
the role's `Permissions` collection, then add only the module-specific claims
needed by that role.

| Role | Start with |
| --- | --- |
| Studio viewer | `workflows/definitions:view`, `workflows/definitions/versions:view`, `workflows/instances:view`, `workflows/activity-executions:view`, and the designer metadata grants such as `workflows/descriptors/activities:view`, `workflows/descriptors/expressions:view`, `workflows/descriptors/storage-drivers:view`, and `workflows/descriptors/variables:view`. |
| Workflow designer | Studio viewer plus `workflows/definitions:write`, `workflows/definitions:publish`, `workflows/definitions:retract`, and `workflows/definitions:delete`; add `workflows/definitions/versions:revert` only for rollback. |
| Workflow operator | Studio viewer plus `workflows/instances:cancel`, `workflows/instances:delete`, and `workflows/runtime:view`. |
| Runtime administrator | `workflows/runtime:view` and `workflows/runtime:control`. |
| Workflow service runner | `workflows/definitions:view` and `workflows/definitions:execute`; add instance grants only if the service inspects or changes instances. |
| Identity administrator | The required `identity/users:*` and `identity/roles:*` grants, plus `identity/applications:create` when it provisions API clients. Prefer named verbs over `*` where possible. |

The Studio viewer and runtime administrator templates are intentionally
separate. Designing workflows does not require authority to pause or drain a
running host.

## What Studio users see

Studio is an API client, not a second authorization authority. In 3.9.0 its
security module reads the current caller's permissions and the server's
permission catalog to tailor role and navigation affordances, but the server
still authorizes every API request.

When a Studio screen is incomplete:

1. Identify the exact `/elsa/api/...` request that returned `401` or `403`.
2. Check the caller's effective grants with `/identity/me/permissions` when
   that endpoint is available.
3. Look up the endpoint's resource and verb in `/identity/permissions` or the
   module's source.
4. Add the smallest matching grant to the role or external-claim mapping.
5. Avoid granting `*` just to make one screen load.

A visible Studio menu item is not proof that every downstream API request is
authorized. Feature registration, client-side permission checks, and server
authorization are separate layers.

## Troubleshooting missing access

1. Confirm whether Elsa API security is enabled as intended. Disabled security
   changes endpoint behavior for the whole host.
2. Identify the exact route and status code. `401` usually means the request
   was not authenticated; `403` usually means the principal lacked an
   accepted grant or failed another policy.
3. Confirm that the final principal contains claims with the literal type
   `permissions`, not only ASP.NET Core role claims.
4. Check the permission orientation. A Core claim is `resource:verb`, such as
   `workflows/definitions:view`, not `view:workflows/definitions`.
5. Check the installed catalog. An unknown resource or unsupported verb can
   be rejected by Core role validation, while an extension may use a literal
   compatibility value that is not catalog-backed.
6. If the failing route is a workflow ingress URL, inspect its `Authorize`
   and `Policy` settings instead of adding an Elsa API claim.

## Release source checked

This page was checked against the remote `release/3.9.0` refs:

- [Core permission grammar and matching](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/common/Elsa.Api.Common/Authorization/Permission.cs)
- [Core permission matcher](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/common/Elsa.Api.Common/Authorization/PermissionMatcher.cs)
- [Core endpoint security](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/common/Elsa.Api.Common/Abstractions/EndpointSecurity.cs)
- [Core workflow permission catalog](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.Workflows.Api/Permissions/WorkflowPermissions.cs)
- [Core Identity permission catalog](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.Identity/Permissions/IdentityPermissions.cs)
- [Core permission catalog endpoint](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.Identity/Endpoints/Permissions/List/Endpoint.cs)
- [Core current-caller permission endpoint](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.Identity/Endpoints/Me/Permissions/Endpoint.cs)
- [Core Python.NET execution guard](https://github.com/elsa-workflows/elsa-core/blob/457f94e0f68d761387972ec447fc439ee2d5d576/src/modules/Elsa.Expressions.Python/Services/PythonNetPythonEvaluator.cs)
- [Studio permission authoring model](https://github.com/elsa-workflows/elsa-studio/blob/f68d5b05a9c88ccd7db160af5503ce751aacb966/src/modules/Elsa.Studio.Security/Models/RolePermissionAuthoring.cs)
- [Studio permission API client](https://github.com/elsa-workflows/elsa-studio/blob/f68d5b05a9c88ccd7db160af5503ce751aacb966/src/modules/Elsa.Studio.Security/Client/IPermissionsApi.cs)
- [Extensions Connections permissions](https://github.com/elsa-workflows/elsa-extensions/blob/9d0e6fa4ec07818d60dd3e65ccaa84491eac94cf/src/modules/connections/Elsa.Connections.Api/Endpoints/List/Endpoint.cs)
- [Extensions Agents permissions](https://github.com/elsa-workflows/elsa-extensions/blob/9d0e6fa4ec07818d60dd3e65ccaa84491eac94cf/src/modules/agents/Elsa.Agents.Api/Endpoints/Agents/List/Endpoint.cs)
- [Extensions Workflow Context permissions](https://github.com/elsa-workflows/elsa-extensions/blob/9d0e6fa4ec07818d60dd3e65ccaa84491eac94cf/src/modules/workflows/Elsa.WorkflowContexts/Endpoints/ProviderTypes/List/Endpoint.cs)
