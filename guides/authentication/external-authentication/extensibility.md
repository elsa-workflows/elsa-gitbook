---
description: >-
  Extend Elsa External Authentication with custom adapters, policies, matchers,
  permission grant sources, and Studio connection editors.
---

# External Authentication extensibility

Elsa External Authentication is protocol-neutral. The 3.8 release supplies a
built-in OpenID Connect adapter, but applications can add other protocol
adapters and customize how unlinked identities, permissions, and connection
settings are handled.

This page is for developers who own a trusted Elsa deployment and, optionally,
an Elsa Studio package. It documents the extension contracts in the Elsa 3.8
preview release; the feature is still preview-only, so pin and test the exact
package versions you deploy.

## Choose the right extension point

| Requirement | Contract | Runs where |
| --- | --- | --- |
| Add a provider protocol such as SAML or a proprietary OAuth variant | `IExternalAuthenticationAdapter` | Elsa Server |
| Decide what happens when no external identity link exists | `IUnlinkedIdentityPolicy` | Elsa Server |
| Match an external identity to an existing Elsa user | `IExternalUserMatcher` | Elsa Server |
| Convert trusted provider data into explicit Elsa grants | `IPermissionGrantSource` | Elsa Server |
| Replace the generic connection form for one adapter | `IConnectionCustomEditor` | Elsa Studio |

An adapter owns the provider protocol. Policies, matchers, and grant sources
should remain separate: a matcher answers *which user*, while a grant source
answers *which Elsa permissions*. An upstream role or group never becomes an
Elsa permission merely because it appears in a provider response.

## Register a server extension

Register the implementation with dependency injection and register its stable
type with the External Authentication module. The second call does not create
the implementation; it records the trusted extension type for startup
validation and allowlist checks.

```csharp
using Elsa.ExternalAuthentication.Contracts;
using Elsa.ExternalAuthentication.Options;

services.AddExternalAuthenticationServices();

services.AddScoped<IExternalAuthenticationAdapter, ContosoAdapter>();
services.AddExternalAuthenticationExtension(
    ExternalAuthenticationExtensionKind.Adapter,
    ContosoAdapter.AdapterType);
```

Use the matching extension kind for the other server contracts:

```csharp
services.AddScoped<IExternalUserMatcher, ContosoUserMatcher>();
services.AddExternalAuthenticationExtension(
    ExternalAuthenticationExtensionKind.ExternalUserMatcher,
    ContosoUserMatcher.MatcherType);

services.AddScoped<IPermissionGrantSource, ContosoGrantSource>();
services.AddExternalAuthenticationExtension(
    ExternalAuthenticationExtensionKind.PermissionGrantSource,
    ContosoGrantSource.SourceType);
```

`AddExternalAuthenticationServices()` installs the protocol-neutral foundation
and built-in in-memory stores. Configure the dedicated External Authentication
persistence feature for durable or multi-node deployments; registering an
extension does not make broker state durable.

### Restrict what the deployment can use

The registries reject duplicate types and can apply deployment allowlists. The
option names are:

```json
{
  "ExternalAuthentication": {
    "AllowedAdapterTypes": ["openid-connect", "contoso"],
    "AllowedUnlinkedIdentityPolicyTypes": ["reject", "create-user"],
    "AllowedExternalUserMatcherTypes": ["contoso-directory"],
    "AllowedPermissionGrantSourceTypes": ["elsa-roles", "contoso-groups"]
  }
}
```

An allowlist entry must refer to an installed extension. An empty adapter or
matcher allowlist permits every installed type; the built-in policy and grant
source lists are non-empty by default. Selecting the built-in `match-user`
policy requires at least one installed and allowed user matcher.

Treat these lists as a security boundary. Do not register an adapter, matcher,
or grant source solely to make its descriptor visible if the deployment should
not be able to select it.

## Implement a protocol adapter

`IExternalAuthenticationAdapter` has one stable `Type` and six responsibilities:

| Member | Responsibility |
| --- | --- |
| `Describe()` | Return the adapter settings schema, capabilities, and optional Studio editor contract |
| `ValidateAsync(...)` | Validate the effective connection and resolved secrets before use |
| `CreateAuthorizationRequestAsync(...)` | Build the upstream authorization redirect and protected transaction state |
| `AuthenticateCallbackAsync(...)` | Validate the callback and return the external identity and projected claims |
| `TestAsync(...)` | Run an on-demand connection diagnostic without creating a login session |
| `CreateLogoutRequestAsync(...)` | Optionally create an upstream logout redirect |

The built-in `OpenIdConnectExternalAuthenticationAdapter` is the reference
implementation. It uses the context's resolved secrets, validates issuer,
audience, signature, lifetime, nonce, and callback correlation, then returns an
`ExternalIdentity` plus the claims permitted by the connection's projection.
Keep provider tokens and client secrets inside the adapter boundary; do not
place them in descriptors, Studio models, journal entries, or diagnostic
results.

For an adapter that changes its settings shape, implement
`IAdapterSettingsMigration` for each forward version step and register those
migrations with DI:

```csharp
services.AddScoped<IAdapterSettingsMigration, ContosoSettingsV1ToV2>();
```

Elsa chains migrations from the stored version to the adapter's current
`Describe().SettingsVersion`. A missing step, duplicate source version, invalid
target version, or migration cycle fails rather than silently interpreting old
settings.

## Implement policies, matchers, and grant sources

### Unlinked identity policies

Implement `IUnlinkedIdentityPolicy` when the deployment needs a decision other
than the built-in `reject`, `create-user`, or `match-user` behavior. The policy
returns one of these decisions:

- `Reject`, with a safe reason;
- `CreateUser`, with a `UserCreationProposal`; or
- `LinkExistingUser`, with the user ID and an authorization basis.

Do not make a policy trust an arbitrary client-supplied user ID. The policy
receives the target tenant, effective connection, normalized external identity,
projected claims, and policy settings.

### External user matchers

`IExternalUserMatcher` is the trusted lookup used by the built-in `match-user`
policy. It receives the target tenant, effective connection, external identity,
the required projected claims, and matcher settings. It must return either:

- `Match` with an Elsa user ID and a non-empty `AuthorizationBasis`; or
- `NoMatch`.

The `match-user` policy rejects if the matcher is missing, not allowed, has a
settings-version mismatch, or returns an invalid result. If the matcher returns
`NoMatch`, the policy's `noMatchAction` can reject or create a credential-less
user with the configured default roles.

### Permission grant sources

Implement `IPermissionGrantSource` when provider information should produce
explicit Elsa grants. Its `GetGrantsAsync` context includes the target tenant,
user ID, effective connection, external identity, projected claims, and the
selected source settings.

Return `PermissionGrant` values only for permissions the deployment intends to
delegate. The deployment-level `PermissionGrants.AllowedPermissions` and
`DeniedPermissions` boundaries still apply after source resolution. Keep the
source mapping explicit and reviewable; do not pass through all upstream group
names as permission names.

## Design the descriptor contract

Every server extension describes its settings using the same shape:

- stable type, display name, description, and positive settings version;
- zero or more `SettingFieldDescriptor` values;
- optional `CustomEditorContract` with a key and positive contract version.

The released validator rejects descriptors that violate these rules:

- extension and descriptor types must match;
- extension types use lowercase identifiers with optional `-` or `.` segments,
  such as `contoso-directory`;
- field names start with a lowercase letter and then use letters or digits;
- field names must be unique and have display text, a description, a supported
  value type, and a UI hint;
- supported value types are `string`, `secret`, `boolean`, `integer`, `number`,
  `uri`, `string-array`, and `json`;
- allowed values cannot be blank or duplicated;
- length and regular-expression validation must be valid; and
- a secret-binding field must use value type `secret` and set `IsRedacted`.

Use `VisibleWhen` for fields that depend on another field. The referenced field
must exist, must not be the field being conditioned, and must have a non-empty
expected value. Keep unsafe provider-trust fields marked `IsUnsafe`; Studio uses
that metadata to require the corresponding permission before editing them.

Example descriptor shape:

```csharp
public ExternalAuthenticationAdapterDescriptor Describe() => new(
    AdapterType,
    "Contoso Directory",
    "Authenticates users with the Contoso directory protocol.",
    1,
    [
        new SettingFieldDescriptor(
            "endpoint",
            "Endpoint",
            "The HTTPS authorization endpoint.",
            "uri",
            true,
            "uri",
            null,
            [],
            new SettingFieldValidation(),
            false,
            false,
            null,
            null,
            false),
        new SettingFieldDescriptor(
            "clientSecret",
            "Client secret",
            "The secret used by the confidential provider client.",
            "secret",
            true,
            "secret",
            null,
            [],
            new SettingFieldValidation(),
            true,
            false,
            null,
            null,
            true)
    ],
    new ExternalAuthenticationAdapterCapabilities(true, true, false),
    null);
```

The exact record constructor is release-specific; keep descriptor creation
centralized in `Describe()` so server validation and Studio rendering see the
same contract.

## Add a custom Studio connection editor

Use a custom editor only when the generic descriptor-driven editor cannot
express the provider's configuration UX. The server advertises a
`CustomEditorContract`:

```csharp
new CustomEditorContract("contoso-connection", 1)
```

The Studio package registers the matching component and exact contract version:

```csharp
services.AddExternalAuthenticationCustomEditor<ContosoConnectionEditor>(
    "contoso-connection",
    1);
```

`ContosoConnectionEditor` must be a Blazor component implementing
`IConnectionCustomEditor`. If it needs to notify the host about unsaved changes,
also implement `IConnectionCustomEditorWithChangeTracking` and expose its
`Changed` parameter.

Studio resolves an editor only when both the key and contract version match. A
missing match uses the generic editor. Duplicate client registrations, an empty
key, a non-positive version, or a component that does not implement the marker
interface fails registration.

When a custom editor is selected, Studio passes these parameters:

- `Connection`, `Adapter`, and `Model`;
- `ReadOnly`, `CanConfigureUnsafeSettings`, and `CanCreateOverride`;
- managed secret resolver data and its error message;
- `Saved`, `ManagedSecretChanged`, `SecretBindingRemoved`, and
  `FullOverrideRequested` callbacks; and
- `Changed` when the component implements change tracking.

The custom editor replaces the generic General and Provider editors, so it must
render the fields it owns and invoke `Saved` with a `ConnectionMutation`. The
Diagnostics tab remains available. Preserve the generic editor's read-only and
secret-handling behavior rather than exposing deployment-owned settings or
secret values directly in the browser.

## Verification checklist

Before shipping an extension:

1. Register the implementation and its `ExternalAuthenticationExtensionKind`.
2. Set the allowlist explicitly in each deployment environment.
3. Start the host and confirm duplicate, missing, or invalid registrations fail
   during options validation.
4. Call the descriptor endpoint and verify field types, visibility conditions,
   unsafe flags, secret redaction, and settings versions.
5. Validate normal, rejected, expired, and replayed authentication callbacks.
6. Test identity-link and permission behavior with a tenant other than the
   default tenant.
7. If Studio has a custom editor, verify exact key/version matching, read-only
   mode, save callbacks, secret replacement, and the generic-editor fallback.
8. Add adapter settings migrations before changing a persisted settings shape.

Related operational guidance is in [External Authentication](README.md),
[Configuration](configuration.md), [Administration](administration.md), and
[Production and Security](production.md).
