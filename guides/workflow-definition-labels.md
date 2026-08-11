---
description: >-
  Release-backed guide to organizing workflow definitions with labels in Elsa
  3.8.0, including Studio setup, API usage, filtering, persistence, and
  permissions.
---

# Workflow-definition labels

Labels are optional metadata for organizing workflow definitions. Use them for
business or operational categories such as `Orders`, `Human review`, or
`Production`. They are not workflow variables, activity labels, or runtime
instance state.

In Elsa 3.8.0, the feature has two parts:

- Core stores labels and the associations between labels and workflow
  definition versions.
- Studio provides a Labels administration page and a label editor in the
  workflow-definition properties surface.

The feature is opt-in in a custom host. A project reference alone does not
register the Core or Studio module.

## When to use labels

Use labels when people need to find or group definitions without changing how a
workflow executes. They work well for:

- business ownership, such as `Finance` or `Customer support`;
- lifecycle or review state, such as `Draft review` or `Approved`;
- operational classification, such as `High priority` or `Nightly`.

Use workflow inputs, variables, or instance metadata when the value belongs to
an execution. A label does not automatically appear on every instance created
from a definition and does not change triggers, activities, or execution
behavior.

## Enable the Core feature

Register the Labels feature in the Elsa host that serves the API. For a
programmatic EF Core host, the release exposes this shape:

```csharp
using Elsa.Extensions;
using Elsa.Persistence.EFCore.Modules.Labels;

builder.Services.AddElsa(elsa =>
{
    elsa.UseLabels(labels => labels.UseEntityFrameworkCore());
});
```

`UseLabels` uses in-memory stores by default. That is useful for a test or
short-lived process, but it is not durable across restarts. For production,
select a persistence provider explicitly:

```csharp
using Elsa.Persistence.MongoDb.Modules.Labels;

builder.Services.AddElsa(elsa =>
{
    elsa.UseLabels(labels => labels.UseMongoDb());
});
```

The EF Core provider uses a separate `LabelsElsaDbContext` containing the
`Labels` and `WorkflowDefinitionLabels` sets. Use the provider's released
migrations or your normal Elsa migration process for that context. MongoDB
stores the corresponding `labels` and `workflow_definition_labels`
collections and creates label-association indexes.

The provider packages must match the rest of the Elsa release. The MongoDB
extension is implemented in
[`Elsa.Persistence.MongoDb.Modules.Labels`](https://github.com/elsa-workflows/elsa-extensions/tree/release/3.8.0/src/modules/persistence/Elsa.Persistence.MongoDb/Modules/Labels).

## Enable the Studio module

Register the Studio module in the host that serves the Studio UI:

```csharp
using Elsa.Studio.Labels;

builder.Services.AddLabelsModule(backendApiConfig);
```

With the module enabled, Studio adds a **Labels** item under **Administration**
and a label editor to workflow-definition properties. From the Labels page,
users can create, edit, and delete labels. From a workflow definition, users
can add labels through the selection dialog or remove them from the displayed
chips.

The module calls the backend through the currently configured
`IBackendApiClientProvider`. If the module is missing, the menu and editor are
not registered. If the backend feature or its permissions are missing, the
Studio calls fail or the feature cannot load its data.

## Manage labels in Studio

1. Create a label from **Administration → Labels**.
2. Give it a required name. Optionally set a description and color.
3. Open a workflow definition in the designer.
4. In the workflow-definition properties, choose **Add Label**.
5. Select one or more existing labels and confirm.
6. Remove a label from the properties surface by closing its chip.

The Studio editor associates labels with the definition version currently being
edited. Publishing a definition does not turn labels into execution data; use
the label for cataloging and filtering definitions.

## Use the HTTP API

The Core feature exposes these endpoints. The exact API prefix depends on the
host's route configuration.

Label catalog operations:

- `GET /labels` and `GET /labels/{id}` — `read:labels`.
- `POST /labels` — `create:labels`.
- `POST /labels/{id}` — `update:labels`.
- `DELETE /labels/{id}` — `delete:labels`.

Definition-association operations:

- `GET /workflow-definitions/{id}/labels` —
  `read:workflow-definition-labels`.
- `POST /workflow-definitions/{id}/labels` —
  `update:workflow-definition-labels`.

Create a label with a name, description, and optional color:

```http
POST /labels
Content-Type: application/json

{
  "name": "Human review",
  "description": "Definitions that require an approval step",
  "color": "#7E57C2"
}
```

The assignment endpoint takes the workflow definition version ID in the route
and replaces the selected association set:

```http
POST /workflow-definitions/my-definition-version/labels
Content-Type: application/json

{
  "id": "my-definition-version",
  "labelIds": ["label-1", "label-2"]
}
```

The server keeps only label IDs that exist, calculates the difference from the
current associations, and returns the label IDs that remain assigned. Sending
an empty `labelIds` collection removes all labels from that definition
version.

## Filter workflow definitions

The workflow-definition list request accepts a repeatable `label` query
parameter. Use a label ID to filter definitions:

```http
GET /workflow-definitions?label=label-1
```

For multiple values, send the parameter repeatedly according to the API
client's query-string conventions:

```http
GET /workflow-definitions?label=label-1&label=label-2
```

This filters the definition catalog. It does not filter workflow instances and
does not add labels to a running workflow.

## Permissions and tenants

Grant only the operations each role needs. A catalog viewer generally needs
`read:labels` and, when loading associations, `read:workflow-definition-labels`.
Label administrators need the create, update, and delete permissions as well
as the association update permission.

Labels and workflow-definition label associations inherit Elsa's common entity
model, including `TenantId`. The EF Core mapping also indexes the association
tenant ID. In a multi-tenant host, send requests through the same tenant
resolution path used by the rest of the Elsa API and test that labels cannot
cross tenant boundaries. Do not treat the label ID alone as a tenant-isolation
mechanism.

## Troubleshooting

- **The Labels menu is missing:** register `AddLabelsModule` in the Studio
  host. A project reference does not register the module.
- **The editor cannot load labels:** verify that Core has `UseLabels`, the
  labels persistence store is configured, the backend URL is correct, and the
  caller has both label-read permissions.
- **Labels disappear after restart:** the host is using the default in-memory
  stores; configure EF Core or MongoDB persistence.
- **A definition list is empty:** confirm the query uses label IDs, not label
  names, and that the request is scoped to the intended tenant.
- **An assignment silently omits a label:** the update endpoint ignores IDs
  that do not resolve to existing labels. Create the label first or inspect
  the returned `labelIds` collection.

## Release source checked

This guide was checked against `release/3.8.0` at Core `5429008d98a`, Studio
`d25f0aae`, and Extensions `335a2649`:

- [Core Labels feature](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Labels/Features/LabelsFeature.cs)
- [Core label endpoints](https://github.com/elsa-workflows/elsa-core/tree/release/3.8.0/src/modules/Elsa.Labels/Endpoints)
- [Core definition list label filter](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/modules/Elsa.Workflows.Api/Endpoints/WorkflowDefinitions/List/Models.cs)
- [Core EF Core label persistence](https://github.com/elsa-workflows/elsa-core/tree/release/3.8.0/src/modules/Elsa.Persistence.EFCore/Modules/Labels)
- [Studio Labels module](https://github.com/elsa-workflows/elsa-studio/tree/release/3.8.0/src/modules/Elsa.Studio.Labels)
- [Extensions MongoDB label persistence](https://github.com/elsa-workflows/elsa-extensions/tree/release/3.8.0/src/modules/persistence/Elsa.Persistence.MongoDb/Modules/Labels)

## Related guides

- [API Reference](api-client/README.md)
- [Authentication and permissions](authentication/permissions.md)
- [Multitenancy](../multitenancy/introduction.md)
- [Studio integration](studio/integration/README.md)
