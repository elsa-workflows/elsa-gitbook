# Workflow Definition Version Lifecycle

Elsa keeps a workflow definition's stable **definition ID** separate from the
individual **version ID** and numeric **version**. This lets a designer save a
new draft without changing the identity used by callers, while operators can
choose whether an API operation uses the latest, published, draft, or a
specific version.

This page describes the release `3.8.0` behavior and how it appears in Elsa
Studio.

## The three identities to keep straight

| Value | Meaning | Changes when you save a new version? |
| --- | --- | --- |
| `DefinitionId` | Stable identity of the workflow definition | No |
| `Id` / definition version ID | Identity of one stored version | Yes |
| `Version` | Human-readable, increasing version number | Yes |

Each stored version also has `IsLatest` and `IsPublished` flags. The latest
version is the current editable version. The published version is the version
used by default when the REST API starts a workflow.

Do not treat a version number as the definition ID. Use the definition ID to
address the workflow family, and use a version option or version ID when you
need a particular stored version.

## Lifecycle at a glance

```text
New or edit draft
      |
      v
Save draft  --->  latest, not published
      |
      +---- Publish ---> latest + published
      |                         |
      |                         +---- Edit ---> new latest draft
      |                         |
      |                         +---- Unpublish/retract ---> latest, not published
      |
      +---- Roll back a history entry ---> new latest draft copied from that version
```

Publishing a new version does not mutate the previous version's workflow graph.
It moves the published marker to the selected latest version and retracts the
previous published version. Retraction only clears the published state; it
does not delete the version.

## Drafts, latest versions, and publishing

When Elsa creates a workflow definition, it starts as version 1, marked latest
and not published. Saving a draft keeps it latest. When a published definition
is edited, Elsa creates a shallow copy with a new version ID and the next
version number, then marks that copy as the latest unpublished draft.

Saving the same draft record does not necessarily create another version on
every save. A new record receives the next version number; saving the existing
latest record preserves its version number.

Publishing performs validation first. If validation succeeds, Elsa:

1. clears `IsLatest` and `IsPublished` on the previous latest/published
   records;
2. marks the selected latest version as both latest and published; and
3. emits the publish/retract notifications used by runtime and integration
   features.

If validation is configured to fail the publish, no new published state is
written. A publish operation therefore means “validate and make this latest
version active”, not “create a separate immutable copy”.

## Which version is selected?

Core exposes these selectors through `VersionOptions`:

| Selector | Use |
| --- | --- |
| `Latest` | Current latest version, including an unpublished draft |
| `Published` | Current published version only |
| `LatestOrPublished` | Latest when available, otherwise published |
| `LatestAndPublished` | Latest version only when it is also published |
| `Draft` | An unpublished version |
| `AllVersions` | Complete version history |
| a number such as `3` | One specific numeric version |

The default matters:

- REST `GET`/`POST /workflow-definitions/{definitionId}/execute` and `POST
  /workflow-definitions/{definitionId}/dispatch` use `Published` when the
  request omits `versionOptions`.
- The Studio workflow list's **Run** action explicitly uses `Latest`, so it
  can run the current draft/latest version.
- Studio's workflow editor loads `Latest` for editing and its **Version
  history** tab loads `AllVersions` for inspection.

For production callers, leave the REST default in place unless you have a
deliberate reason to run a draft or pin a specific version.

### Select a version in the REST API

The read endpoints accept `versionOptions` as a query-string value. The
version list is ordered from newest to oldest:

```http
GET /elsa/api/workflow-definitions/order-approval?versionOptions=Published
GET /elsa/api/workflow-definitions/order-approval?versionOptions=3
GET /elsa/api/workflow-definitions/order-approval/versions
```

Execution and dispatch accept the same selector in the JSON body. A numeric
JSON value selects that version; the string form is also accepted by Elsa's
JSON converter:

```http
POST /elsa/api/workflow-definitions/order-approval/dispatch
Content-Type: application/json
Authorization: ApiKey YOUR_API_KEY

{
  "versionOptions": 3,
  "input": {
    "orderId": "A-1042"
  }
}
```

Use a specific version for controlled replay or compatibility testing. Avoid
pinning a version in normal business traffic unless the caller owns the
upgrade policy; otherwise published-version promotion remains the deployment
switch.

## Publish and retract from Studio

In the Studio workflow list, the table shows **Latest version** and
**Published version** separately. A dash in the published column means that no
version is currently published. The row menu and bulk-actions menu provide
**Publish** and **Unpublish** actions when the backend exposes the required
links and the application is not in read-only mode.

Inside the editor:

- use **Save** or enable **Auto-save** to persist the current draft;
- use **Publish workflow** to validate and publish the latest version; and
- use the editor menu's **Unpublish** action to retract the current published
  version.

Publishing can also update workflows that consume the published definition.
Studio reports the count when the backend returns affected consuming
workflows. This is separate from migrating already-running instances; use the
[alterations guide](../../features/alterations/README.md) when existing
instances must move to a newer published version.

## Inspect and roll back version history

Open a workflow in Studio and use the **Version history** section in the
workflow properties. Studio loads every stored version and shows whether each
one is published, its version number, and its creation time. From a history
entry you can:

- **View** the stored version without replacing the current latest version;
- **Delete** a version when editing is allowed and more than one version
  remains; or
- choose **Rollback to this version** for a non-latest version.

Rollback is implemented as a new version. Elsa copies the selected historical
definition into a new record, assigns the next version number, and marks that
record latest. The historical record remains available. Rollback does not
publish the new version automatically; publish it after reviewing the copied
draft.

The rollback endpoint is:

```http
POST /elsa/api/workflow-definitions/{definitionId}/revert/{version}
```

It uses the `publish:workflow-definitions` permission and returns the newly
created version summary. This endpoint is a version-history operation, not a
runtime migration. If instances already run an older version, plan their
migration separately with alterations.

## API operations and permissions

The main lifecycle operations are:

| Operation | Endpoint | Permission |
| --- | --- | --- |
| List definitions or versions | `GET /workflow-definitions` and `GET /workflow-definitions/{definitionId}/versions` | `read:workflow-definitions` |
| Save a draft | `POST /workflow-definitions` or the version update endpoint exposed by the API links | `write:workflow-definitions` |
| Publish latest | `POST /workflow-definitions/{definitionId}/publish` | `publish:workflow-definitions` |
| Retract published version | `POST /workflow-definitions/{definitionId}/retract` | `retract:workflow-definitions` |
| Roll back to a version | `POST /workflow-definitions/{definitionId}/revert/{version}` | `publish:workflow-definitions` |
| Start a workflow | `GET`/`POST .../execute` or `POST .../dispatch` | `exec:workflow-definitions` |

Use the API's hypermedia links to determine whether a specific definition is
read-only or whether an operation is available. Studio uses those links to
disable edit, publish, retract, and delete actions; a user seeing a missing
action may be in read-only mode or lack the corresponding permission.

## Deployment guidance

Treat publishing as the boundary between design-time and runtime use:

1. edit and test the latest draft in Studio;
2. inspect the version number and definition ID;
3. publish after validation succeeds;
4. verify that the published-version column shows the intended version; and
5. let normal REST callers use the published default.

If you need to test an unpublished version through an API, make the selector
explicit and keep that path out of production traffic. If you need to change
the behavior of instances that are already running, use an alteration plan or
targeted alteration rather than assuming that publishing changes their stored
definition version.

## Release source

The behavior on this page is based on the current `release/3.8.0` refs:

- [Core workflow definition publisher](https://github.com/elsa-workflows/elsa-core/blob/f1e2a092f916c41d1949bf36efa16a90abda664d/src/modules/Elsa.Workflows.Management/Services/WorkflowDefinitionPublisher.cs)
- [Core version selectors](https://github.com/elsa-workflows/elsa-core/blob/f1e2a092f916c41d1949bf36efa16a90abda664d/src/clients/Elsa.Api.Client/Shared/Models/VersionOptions.cs)
- [Core execute version default](https://github.com/elsa-workflows/elsa-core/blob/f1e2a092f916c41d1949bf36efa16a90abda664d/src/modules/Elsa.Workflows.Api/Endpoints/WorkflowDefinitions/Execute/WorkflowExecutionHelper.cs)
- [Core publish endpoint](https://github.com/elsa-workflows/elsa-core/blob/f1e2a092f916c41d1949bf36efa16a90abda664d/src/modules/Elsa.Workflows.Api/Endpoints/WorkflowDefinitions/Publish/Endpoint.cs)
- [Core rollback endpoint](https://github.com/elsa-workflows/elsa-core/blob/f1e2a092f916c41d1949bf36efa16a90abda664d/src/modules/Elsa.Workflows.Api/Endpoints/WorkflowDefinitions/Version/Revert.cs)
- [Studio workflow list](https://github.com/elsa-workflows/elsa-studio/blob/release/3.8.0/src/modules/Elsa.Studio.Workflows/Components/WorkflowDefinitionList/WorkflowDefinitionList.razor)
- [Studio version history](https://github.com/elsa-workflows/elsa-studio/blob/release/3.8.0/src/modules/Elsa.Studio.Workflows/Components/WorkflowDefinitionEditor/Components/WorkflowProperties/Tabs/VersionHistory/VersionHistoryTab.razor.cs)
