---
description: >-
  Use Elsa's release-backed Secrets module to store, resolve, rotate, and
  reference sensitive values from workflows and Elsa Studio.
---

# Secrets management

Elsa 3.8 includes an optional Secrets module for named values that workflows
and feature modules can resolve at runtime. It gives you a stable technical
name, a store and type, lifecycle metadata, and a reference that can be saved
in a workflow definition without saving the cleartext value there.

Use this guide when you need to decide:

* whether to use an Elsa-managed encrypted value or a value already supplied by
  application configuration;
* how to configure the module and its encryption key;
* how to create, rotate, revoke, and test a secret in Elsa Studio; or
* how a workflow activity should reference a secret.

This feature is separate from OIDC client secrets, identity signing keys, and
Kubernetes `Secret` objects. Those are host or infrastructure configuration;
the module documented here exposes named values to Elsa workflows and modules.

## What the module provides

The Core package exposes the `UseSecrets` module extension. The default service
registration includes:

* an encrypted store (`encrypted`) for values managed by Elsa;
* a configuration-backed store (`configuration`) that reads a value from the
  host configuration without storing that value in Elsa;
* the `Secret` expression type and runtime resolver; and
* HTTP endpoints for management and the Studio picker whose returned secret
  models expose metadata rather than cleartext values.

The paired Studio package adds a **Secrets** page under the settings menu and a
reusable secret picker for activity inputs. Registering the Studio module does not
enable the backend feature; the Server and Studio hosts must both have the
corresponding feature available.

## Configure the Core module

For a standalone `AddElsa(...)` host, enable the module and configure the
encryption key explicitly:

```csharp
using Elsa.Extensions;

var encryptionKey = Convert.FromBase64String(
    builder.Configuration["ELSA_SECRETS_ENCRYPTION_KEY_BASE64"]
    ?? throw new InvalidOperationException("Missing Elsa secrets encryption key."));

builder.Services.AddElsa(elsa =>
{
    elsa.UseSecrets(secrets =>
    {
        secrets.ConfigureOptions = options =>
        {
            options.EncryptionKey = encryptionKey;
            options.RepositoryFilePath = Path.Combine(
                builder.Environment.ContentRootPath,
                "App_Data",
                "elsa-secrets.json");
        };
    });
});
```

The release's encrypted store uses AES-GCM and accepts an encryption key of
exactly 16, 24, or 32 bytes. Keep the key outside source control and keep it
stable for as long as encrypted values in the repository must remain
readable. Changing or losing it prevents Elsa from resolving the encrypted
payloads.

The default repository is a JSON file at:

```text
<application base directory>/App_Data/elsa-secrets.json
```

`RepositoryFilePath` changes that location for the default file repository.
If you configure a different `ISecretRepository`, use that provider's
persistence and migration instructions instead; the file-path option does not
configure every persistence provider.

## Choose a store

The release registers two built-in stores. The store is selected when the
secret is created and cannot be changed by the update endpoint.

* **`encrypted`** stores a protected payload in the Elsa repository and
  requires the configured encryption key. The Secrets API supports create,
  rotate, revoke, delete, and test for this store.
* **`configuration`** stores only the configuration-key metadata. It reads
  `Elsa:Secrets:<configuration-key>`, then the key itself, and supplies the
  value from host configuration. The store is read-only from Elsa's
  perspective.

For example, a configuration-backed secret with configuration key
`OrdersDatabase` resolves this host configuration:

```json
{
  "Elsa": {
    "Secrets": {
      "OrdersDatabase": "Host=db;Database=orders;Username=elsa;Password=provided-outside-source-control"
    }
  }
}
```

The configuration store does not copy that value into the Elsa secrets file or
database. Use it when deployment infrastructure already owns the secret. Use
the encrypted store when the value should be managed through Elsa's secret
lifecycle and the host can protect the encryption key.

The release also exposes `ISecretStore` and `ISecretRepository` extension
points. The built-in module does not claim to be an integration with Azure Key
Vault, AWS Secrets Manager, HashiCorp Vault, or another external vault. Add a
custom store/repository or project the external value into application
configuration when that is your source of truth.

## Secret identity, types, and lifecycle

### Technical names

The technical name is the reference used by workflows. It is normalized to
lowercase and must be 2–200 characters long, start with a letter, and contain
only letters, numbers, dots, dashes, underscores, or colons. The release
management API lets you edit the display name and description, but it does not
provide a rename operation. Choose a stable technical name before publishing
workflows.

The default type providers are:

* `text` for passwords, tokens, and connection strings;
* `rsa-key` for RSA key material or a configuration reference; and
* `x509-certificate` for certificate material, a thumbprint, or a
  configuration reference.

Types constrain which stores can be selected and validate the create/rotate
payload. A type is metadata about how a value is interpreted; it does not
make a configuration-backed value writable through Elsa.

### Versions and status

Creating a secret creates version 1. Rotating it creates the next version and
retires the previously active version. Runtime resolution always selects the
latest active, non-expired version. Revoking a secret marks the secret and its
active versions as revoked, so resolution stops. Deleting a secret removes the
protected payload from the encrypted store and marks the repository record as
deleted; deleted records are hidden from normal reads.

The API returns metadata such as the current version and expiration time, not
the cleartext value. A successful **Test** operation means the configured store
could resolve the latest active version; it does not display the value.

## Reference a secret from a workflow

Store a reference, not a value, in a workflow input or activity property. The
reference has a required name and optional type and scope:

```json
{
  "type": "Secret",
  "value": {
    "name": "orders:api-token",
    "typeName": "text",
    "scope": "production"
  }
}
```

At runtime, the `Secret` expression handler resolves the reference through
`ISecretResolver`. It validates the optional type and scope, then resolves the
latest active version from the selected store. The resolved value is available
to the activity while it runs; the serialized workflow definition and workflow
state retain the reference rather than the cleartext value.

In code, create the expression explicitly when an activity input supports
expressions:

```csharp
using Elsa.Secrets.Expressions;
using Elsa.Secrets.Models;

var authorization = SecretExpression.Create(
    new SecretReference("orders:api-token", SecretTypeNames.Text, "production"));
```

In Studio, the Secrets module contributes the `Secret` expression descriptor
with the `secret-picker` UI hint. When an activity input exposes that hint,
Studio lists compatible active secrets and can create one inline when the
backend reports a writable store. The picker can be constrained by type, store,
scope, and whether inline creation is allowed through the input's UI metadata.

## Use the Studio page

After the backend and Studio modules are available, open the **Secrets** page
at `/security/secrets`. The release UI supports this workflow:

1. Select **Create Secret** and choose a technical name, type, and store.
2. Enter either a value for the encrypted store or a configuration key for the
   configuration store.
3. Open the secret to edit its display name/description, rotate it, test
   resolution, or revoke it.
4. Use a compatible activity property in the workflow designer and select the
   secret from the picker.

The page displays name, type, store, status, current version, scope, and
expiration metadata. It intentionally does not provide a cleartext reveal
operation. The Studio module is registered in a custom host with:

```csharp
builder.Services.AddSecretsModule(backendApiConfig);
```

The built-in release hosts register this module for Server, WASM, and
custom-elements Studio hosts. A custom host must also register the matching
backend API client configuration.

## API and permissions

The route paths below are relative to the Elsa API base path. The endpoint
classes in the release use these permissions:

| Operation | Permission |
| --- | --- |
| List, read, descriptors, and picker | `read:secrets` |
| Create, update details, rotate, and revoke | `write:secrets` |
| Test resolution | `test:secrets` |
| Delete | `delete:secrets` |

The corresponding routes are:

* Read: `GET /secrets`, `GET /secrets/{name}`, `GET /secrets/descriptors`, and
  `POST /secrets/picker`.
* Write: `POST /secrets`, `POST /secrets/{name}`,
  `POST /secrets/{name}/rotate`, and `POST /secrets/{name}/revoke`.
* Test: `POST /secrets/{name}/test`.
* Delete: `DELETE /secrets/{name}`.

Grant the smallest set that matches the user or service's job. A workflow
designer who only needs to select existing secrets needs the read path and
the appropriate workflow-definition permissions; creating or rotating secrets
is a separate administrative capability. See [Elsa API Permissions](permission-reference.md)
for the broader permission model.

## Operational checklist

* Keep the encrypted-store key in a deployment secret manager, not in the
  repository or a workflow definition.
* Back up the encrypted repository and its encryption key together. A backup
  without the key is not a usable backup.
* Prefer the configuration store when an external deployment system already
  owns the value and its rotation process.
* Use scopes such as `production` or `staging` when the same technical name
  must resolve differently by environment or integration boundary.
* Rotate before expiry, then verify the new version with **Test** and a safe
  workflow execution path.
* Do not log resolved values, include them in activity output, or copy them into
  ordinary workflow variables unnecessarily.

For OIDC client secrets and other host authentication settings, use the
[authentication](../authentication.md) and [external identity provider](external-identity-providers.md)
guides. For deployment-managed Kubernetes or cloud secrets, use the deployment
guides rather than the Elsa Secrets API.
