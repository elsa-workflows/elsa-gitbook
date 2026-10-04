---
description: >-
  What's new in Elsa 3.9.0 and how to upgrade from 3.8.
---

# Upgrade to Elsa 3.9.0

Elsa 3.9.0 ships Core, Studio and Extensions together. Upgrade all three at
once: Studio 3.9 requires Core 3.9, and mixing versions can fail at
`AddElsa()` with a `TypeLoadException`.

| Repository | Tag | Commit | Release notes |
| --- | --- | --- | --- |
| Core | `3.9.0` | [`6436609`](https://github.com/elsa-workflows/elsa-core/tree/6436609a1a3874d3fea7ccf690897792f5a1f702) | [Core 3.9.0](https://github.com/elsa-workflows/elsa-core/releases/tag/3.9.0) |
| Studio | `3.9.0` | [`a30ed7c`](https://github.com/elsa-workflows/elsa-studio/tree/a30ed7c997dfb3cff1d5d095a4ee19ff03c7fe42) | [Studio 3.9.0](https://github.com/elsa-workflows/elsa-studio/releases/tag/3.9.0) |
| Extensions | `3.9.0` | [`89d4eb9`](https://github.com/elsa-workflows/elsa-extensions/tree/89d4eb9b739ae135604aac2a1de18653a289bdb0) | [Extensions 3.9.0](https://github.com/elsa-workflows/elsa-extensions/releases/tag/3.9.0) |

## What's new

- **Permission-based access everywhere.** Permissions use a structured
  `resource:verb` model, and Studio hides what a user can't use. See
  [Elsa API permissions](../guides/authentication/permissions.md).
- **A dashboard for every user**, gated per section. See
  [Dashboard access](../guides/authentication/permissions.md#dashboard-access).
- **Sign-out for Elsa Identity** that revokes the session. See
  [Sign out](../guides/authentication/elsa-identity.md#sign-out).
- **Simpler Roles screens** in Studio. See
  [Manage roles in Studio](../guides/authentication/elsa-identity.md#manage-roles-in-studio).
- **Tenant isolation fixes** in Core, MongoDB and Dapper, and a bookmark queue
  that resumes in every tenant.
- **Dapper migrations work on fresh databases**, including PostgreSQL.
- **New modules:** [User Tasks](../guides/running-workflows/user-tasks.md),
  [BPMN](../guides/bpmn-authoring-and-interchange.md),
  [MQTT](../activities/mqtt.md), [LDAP](../activities/ldap.md) and
  [Proto.Actor Redis Pub/Sub](../guides/architecture/protoactor-workflow-runtime.md).

## Update packages

Set every `Elsa.*` and `Elsa.Studio.*` package to `3.9.0`. The Studio npm
packages are `@elsa-workflows/elsa-studio-wasm` and
`@elsa-workflows/elsa-studio-wasm-react`.

`Elsa.Templates` is still `3.8.0`. Generate from it, then update the package
references and follow this page.

## Re-author role permissions

This is the biggest change. Permissions are now `{resource}:{verb}`, for
example `workflows/definitions:view`. Legacy strings such as
`read:workflow-definitions` no longer authorize anything.

- `*` keeps working, so the seeded admin role still has full access.
- At startup, Elsa logs every stored permission that no longer resolves. The
  Studio Roles editor lists them under **Review and repair**.
- Some legacy permissions map to several new ones. Use the
  [full mapping](https://github.com/elsa-workflows/elsa-core/blob/3.9.0/doc/migrations/authorization-model.md#full-mapping)
  rather than a one-for-one rename.
- `read:*` and `exec:*` become `*:view` and `*:execute`, which now cover every
  resource. Review roles that hold them.
- `exec:csharp-expressions` and `exec:python-expressions` are removed.
  `CSharpOptions.AllowHostCodeExecution` and
  `PythonOptions.AllowHostCodeExecution` are the only control.
- `manage:user-tasks` becomes `user-tasks:supervise`.
- Setting the default roles of an unlinked-identity policy needs
  `external-authentication/policies/default-roles:update`.
- The `<Module>Permissions` constant classes (`DashboardPermissions`,
  `SecretsPermissions` and others) are removed. Use
  `<Module>ResourcePermissions` with a verb.

Descriptor catalogs, `GET /features/installed` and `GET /resilience/strategies`
are now readable by any signed-in user. The activity catalog includes
workflows marked as usable as activities, so every signed-in user in the
tenant can see their names, descriptions and inputs.

See the Core
[authorization model migration guide](https://github.com/elsa-workflows/elsa-core/blob/3.9.0/doc/migrations/authorization-model.md)
for details.

## Identity and tokens

- The default `AccessTokenLifetime` is 15 minutes (was 1 hour). Permission
  changes apply at the next token refresh. `PermissionStamp` has no effect in
  3.9.
- Refresh with a missing, blank, conflicting or unknown subject returns `401`
  (was `200` with `isAuthenticated: false`). Refresh tokens without `sub`, which
  only 3.8.0-preview1 issued, are rejected; those users sign in again. See
  [Refresh-token subject requirements](../guides/authentication/elsa-identity.md#refresh-token-subject-requirements).
- Elsa-issued tokens always carry `permissions`. A user with no grants gets
  `none`.
- The `SecurityRoot` policy and the localhost permission grant are removed,
  including `EnableLocalHostPermissionGrantForSecurityRoot()`. Bootstrap with
  `UseDefaultAdmin` or `UseAdminApiKey`.
- `POST /identity/secrets/hash` requires `identity/users:create`.
- A custom `IAccessTokenIssuer` must implement
  `IssueTokensAsync(User, SignInSession?, CancellationToken)`.
- `DefaultIdentityRefreshTokenService`'s constructor requires a
  `SessionRevoker`. Resolve it from DI.

## Database migrations

Elsa's startup migration applies these unless `RunMigrations` is off.
Otherwise apply them yourself, and apply `RevokedSessions` before upgraded hosts
refresh tokens.

| Module | New EF Core migrations |
| --- | --- |
| Identity | `PerTenantIdentityUniqueness`, `RevokedSessions` |
| Labels | `PerTenantLabelUniqueness` |
| Secrets | `SecretTenancy`, `SecretDefaultTenantUniqueness` |
| User Tasks | `Initial` |

### MongoDB

- 3.9 treats an empty-string `TenantId` as `null`. 3.9 previews stored `""`
  for default-tenant roles and users; migrate those rows to `null`.
- Multitenant 3.8 installs: set code-defined workflow definition rows to
  `TenantId: "*"`, or delete them and let the populator recreate them.
  Otherwise other tenants log `E11000` at startup.

### Dapper

- Migration `20008` runs on the next migrate. It creates `KeyValues` and adds
  `BookmarkQueueItems.SerializedOptions` where they are missing.
- PostgreSQL databases on 3.6–3.8 stuck at migration `10002`: run
  `DELETE FROM "VersionInfo" WHERE "Version" = 10001;`, then migrate up.
- Hand-made lowercase, unquoted PostgreSQL schemas are not supported.

## Studio hosts

- **OIDC Blazor Server:** sign-out is now an antiforgery-protected
  `POST /authentication/logout`. A custom `_Host.cshtml` needs
  `<persist-component-state />` before `_framework/blazor.server.js`. See
  [Studio Integration](../guides/studio/integration/README.md#openid-connect-configuration).
- The Elsa Identity and OpenID Connect sign-in modules add a user menu with
  **Sign out**. Remove your own user menu if you added one.
- External Authentication interfaces are renamed to
  `IExternalAuthenticationConnectionManagementApi` and
  `IExternalIdentityLinkManagementApi`, and their old permission constants are
  removed.
- `DefaultMenuService` has a new optional `IPermissionService` parameter, and
  `ElsaIdentityRefreshTokenService` takes an `ElsaIdentitySessionGate`. Resolve
  them from DI; if you construct or subclass them, pass the new arguments.
- If a third-party OIDC token has no `permissions` claim, Studio hides nothing
  and the server decides.

## Behaviour changes

- Named `WithVariable(name, value)` now uses workflow storage, so the value
  survives suspend and resume.
- When a referenced workflow is published, ElsaScript and other source-based
  consumers are skipped with a warning and keep the old version.
- `PauseAsync` and `ResumeAsync` throw when the token is already cancelled.
- `MemoryKeyValueStore.SaveAsync` throws `InvalidOperationException` on a
  cross-tenant key conflict.

## API and extensibility

- `IKeyValueStore.TryDeleteAsync` is new, with a default implementation.
  Shared stores should override it; decorators must forward it.
- `ISqlDialect` has new members with defaults: `Update`, `QuoteIdentifier`,
  `BooleanLiteral`.
- `MemoryKeyValueStore` and `KeyValueWorkflowDispatchOutboxStore` take an
  optional `ITenantAccessor`.
- `EndpointPermissionRegistry.Find` and `All` leave out endpoints that accept
  any of several permissions. Use `FindRequirement` and `AllRequirements`.
- `TryMarkInterruptedAsync` ignores `allowFinishedCancelled`.

## Multitenant rolling upgrades

The bookmark queue lock, the administrative pause key and the outbox scan
marker are now per tenant. The first 3.9 node moves a 3.8 pause to the new key,
so finish the upgrade before relying on `AcrossReactivations`. See the
[Core release notes](https://github.com/elsa-workflows/elsa-core/releases/tag/3.9.0)
for details and an optional cleanup query.

## Known limitations

- Access tokens stay valid until they expire after sign-out.
- With MongoDB or Dapper, session revocation is held in memory per node.
- Password reset and user deletion don't end sessions; there is no "sign out
  everywhere".
- Studio shows permission changes, or a disabled user, after a page reload.
  Other tabs and Blazor Server circuits stay signed in until their next call.
- Drain force-cancel doesn't reach running activities through their
  cancellation token (fix planned for 3.10).
