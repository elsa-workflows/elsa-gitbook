---
description: >-
  Source-backed guidance for importing, editing, executing, and exporting BPMN
  2.0 workflows with Elsa Studio and Elsa 3.9.0.
---

# BPMN authoring and interchange

Elsa 3.9.0 can import a BPMN 2.0 XML document, bind its work to Elsa
activities, execute the resulting process, and export the stored BPMN source
again. This is useful when process definitions must move between tools or
when BPMN XML and layout are part of the system of record.

There are two complementary modules:

| Module | Responsibility |
| --- | --- |
| `Elsa.Bpmn` | Executes a BPMN process as an Elsa `BpmnProcess` activity. |
| `Elsa.Bpmn.Interchange` | Reads and writes `.bpmn` XML, analyzes imports, and binds BPMN work to Elsa activities. |

This guide focuses on the workflow used by designers and integrators. It is
not a promise of complete BPMN-tool compatibility: behavior that has no Elsa
runtime mapping or Elsa activity binding is rejected or reported during
import.

## Choose the right authoring path

Use the BPMN interchange surface when you need to import or export BPMN 2.0
XML, retain BPMN layout and foreign XML content, or collaborate with a BPMN
tool. Use the normal Elsa designer when the workflow is Elsa-native and does
not need BPMN round-tripping.

An export is not rebuilt from the current Elsa activity graph. Elsa exports
the BPMN source retained by the most recent successful BPMN import or
document edit. A later graph edit made outside the BPMN-aware path can make
that source stale, in which case export refuses instead of returning a
misleading document.

## Import a BPMN file in Elsa Studio

The Studio workflow list provides an **Import BPMN** action. The release
3.9.0 flow is:

1. Select one file whose name ends in `.bpmn` (the check is case-insensitive).
   Studio reads at most 10 MB for this flow.
2. Studio sends the file to `POST bpmn/analyze`. Analysis reads the document
   without persisting it and shows findings before anything is imported.
3. If the document contains more than one process, choose the process to
   import in the findings dialog. Canceling this dialog leaves the server
   unchanged.
4. Confirm the import. Studio sends the same file to `POST bpmn/import`.
   Elsa creates a new workflow definition, or updates the supplied definition
   when the import targets an existing workflow. The result is a draft; it is
   not published automatically.
5. Studio opens the imported workflow in the editor and refreshes the
   workflow list.

Analysis and import are separate on purpose. Analysis can report useful
findings without writing anything, while the import step also validates
bindings and runtime capabilities before it persists the draft. A file that
analyzes successfully can still be refused during import.

The same `.bpmn` routing is used when a BPMN file is dropped on Studio's
generic workflow-file picker. When updating an already-open definition, select
only one BPMN file; Studio rejects a multi-file BPMN selection because all of
the files would target the same definition.

## What becomes an Elsa activity?

The interchange binder maps these BPMN constructs to Elsa activities:

| BPMN construct | Elsa representation |
| --- | --- |
| Timer wait event | `Delay` with an ISO-8601 duration |
| Message catch event | `Event` |
| Signal catch event | `Event` |
| Message throw event | `PublishEvent` |
| Call activity | `DispatchWorkflow` |
| Embedded subprocess or event subprocess | Nested `BpmnProcess` scope |
| Service, send, user, or script task | An Elsa activity declared with `elsa:activityBinding` |

BPMN describes the role of a task, but it does not tell Elsa which concrete
activity should perform that work. An unbound task therefore needs Elsa's
vendor extension. The binding is attached to the BPMN element it implements,
not to a separate sidecar file.

### Bind a task to an Elsa activity

The extension namespace is
`https://elsaworkflows.io/schemas/bpmn/v1`. `activityType` is Elsa's activity
type name, such as `Elsa.WriteLine`; it is not a CLR type name. Each input is
serialized with Elsa's activity serializer.

```xml
<bpmn:serviceTask id="notify">
  <bpmn:extensionElements>
    <elsa:activityBinding activityType="Elsa.WriteLine">
      <elsa:input name="text">{"typeName":"String","expression":{"type":"JavaScript","value":"getMessage()"}}</elsa:input>
    </elsa:activityBinding>
  </bpmn:extensionElements>
</bpmn:serviceTask>
```

The surrounding `<bpmn:definitions>` element must declare the `elsa` prefix
for the namespace above. Input text is JSON carried as XML text, so ordinary
XML escaping still applies when a value contains `<`, `>` or `&`.

Import refuses a binding when the activity type is not registered, an input is
named more than once, an input is not declared by that activity, or an
`elsa:activityBinding` is attached to a BPMN element whose work is not an
unbound task. Install and enable the Elsa module that provides every bound
activity before importing the document.

An exported file carries these bindings, including input expressions, as
part of the XML. Treat an exported production workflow as sensitive workflow
configuration: expressions can include connection names, endpoint names, or
other implementation details.

## Start events and runtime behavior

The imported process becomes the root `BpmnProcess` scope. Its start events
have these practical consequences:

- A message or signal start registers an Elsa event stimulus using the
  resolved message or signal name.
- A recurring timer start using a positive ISO-8601 `timeCycle` registers an
  Elsa timer or cron trigger.
- A plain start event, or a start event with no supported event definition,
  does not register a workflow trigger. Start that process through the normal
  workflow execution API instead.
- Nested subprocess scopes do not register independent workflow triggers.

For a process to start from an inbound event, configure the BPMN start event
with the event definition and make sure the host has the corresponding Elsa
runtime services. A one-shot `timeDate` or `timeDuration` start is not
represented as a recurring trigger by the current binder.

## Enable BPMN support on the server

Register the interchange module in the Elsa module configuration:

```csharp
using Elsa.Extensions;

services.AddElsa(elsa =>
{
    elsa
        // Other Elsa modules.
        .UseBpmnInterchange();
});
```

If your host only constructs `BpmnProcess` activities in code, `UseBpmn()`
provides the execution module. Importing or exporting `.bpmn` XML requires
`UseBpmnInterchange()`.

`UseBpmnInterchange()` configures the interchange feature, which depends on
the BPMN runtime feature. The 3.9.0 sample server uses this registration in
[`Program.cs`](https://github.com/elsa-workflows/elsa-core/blob/4eb78f0fea903166f6049520a96331aca90e19e5/src/apps/Elsa.Server.Web/Program.cs#L101-L111).

When a custom Studio host registers the workflows module with a remote
backend, it also needs the remote `IBpmnInterchangeApi` client. The stock
Studio workflows module registers the BPMN designer provider and remote
interchange client together; verify equivalent registration when composing a
custom host.

## Edit an imported workflow in Studio

An imported workflow is BPMN-backed. In release 3.9.0, the BPMN canvas is
read-only: Studio displays the imported process and forwards selection, but it
does not yet let you edit BPMN shapes, flows, or nested-scope contents there.
Studio does support editing the Elsa activity binding for eligible unbound
tasks. It keeps those changes in a BPMN document working copy and the revision
it was read from.

1. Select a task in the BPMN designer.
2. Use the **Performed by** panel to choose the Elsa activity that performs
   the task and configure its inputs.
3. Save the binding changes. Studio writes the edited BPMN document through the
   document API and reloads the server's resulting revision.

The regular workflow-definition save path does not serialize an arbitrary
edited Elsa graph back to BPMN. If it detects that a BPMN-backed activity graph
was changed outside the BPMN document session, it refuses the save and directs
you to edit bindings through the **Performed by** panel.

The BPMN document session uses optimistic concurrency. If another save changes
the workflow after Studio read it, reload the current document, reapply the
binding change, and save again. Studio does not silently overwrite the other
write, and reloading discards unsaved local binding changes.

## API surface for integrations

The routes below are relative to the server's configured Elsa API prefix (for
example, `/elsa/api`). They use the 3.9.0 hierarchical permission resource
`workflows/definitions` with either `view` or `write`.

| Method and route | Permission | Purpose |
| --- | --- | --- |
| `POST bpmn/analyze` | `workflows/definitions:view` | Analyze one uploaded `.bpmn` file without persisting it. |
| `POST bpmn/import` | `workflows/definitions:write` | Import one uploaded file as a new or updated draft. |
| `GET bpmn/definitions/{definitionId}/export` | `workflows/definitions:view` | Download the retained BPMN source as XML. |
| `GET bpmn/definitions/{definitionId}/document` | `workflows/definitions:view` | Read the BPMN document as Elsa's `bpmnDefinitions` JSON. |
| `PUT bpmn/definitions/{definitionId}/document` | `workflows/definitions:write` | Write an edited `bpmnDefinitions` document back as a draft. |

The upload endpoints accept exactly one file. `bpmn/import` also accepts
`DefinitionId`, `Name`, and `ProcessId` form fields. `ProcessId` is needed
when the document declares multiple processes.

### Use the document API safely

The document API exists for BPMN-aware editors such as Studio; its JSON is
not the same as Elsa's general API JSON conventions.

```http
GET /elsa/api/bpmn/definitions/order-process/document
```

The response contains the `bpmnDefinitions` document and a strong `ETag`.
Send that exact ETag back on the write:

```http
PUT /elsa/api/bpmn/definitions/order-process/document
If-Match: "etag-from-the-GET"
Content-Type: application/json

Request body: the exact bpmnDefinitions JSON object returned by GET, after editing.
```

The document body must retain the shape returned by `GET`, including the
binding references needed for stored subprocess and call-activity content.
The server writes the document back to BPMN XML, re-imports it into the same
definition, preserves the definition's non-BPMN metadata, and returns a draft
result with the resulting ETag.

- Missing or `*` `If-Match` returns `428 Precondition Required`.
- A stale, weak, or otherwise non-matching ETag returns `412 Precondition
  Failed`; read the document again and reapply the edit.
- If a workflow has no retained BPMN source, the document and export routes
  cannot reconstruct one from the Elsa graph.

If the current definition is published, the document write creates a new
latest draft. Publishing remains a separate workflow-definition operation.

## Export and round-trip boundaries

Use the export route when you need the retained BPMN XML:

```http
GET /elsa/api/bpmn/definitions/order-process/export?versionOptions=Latest
```

If `versionOptions` is omitted, export selects `Latest`. The export includes
the source XML retained during import or document editing, including layout
and foreign XML content that Elsa does not interpret.

Export returns an error rather than a guessed document when:

- the definition was never imported from BPMN and has no retained source;
- its stored source is missing required source metadata; or
- its current Elsa activity graph no longer matches the graph represented by
  the retained source.

To keep export available, make graph and binding changes through the BPMN
document workflow, then save and publish the resulting draft deliberately.

## Troubleshooting checklist

| Symptom | Check |
| --- | --- |
| Import reports an unbound task | Add an `elsa:activityBinding` to the task or choose a BPMN construct with a built-in Elsa mapping. |
| Import reports an unknown activity type | Install/register the module that provides the `activityType`; use the activity registry name, not a CLR name. |
| A multi-process file cannot be imported | Supply the intended `ProcessId` in the import request or select it in Studio's findings dialog. |
| Studio refuses to save binding changes | Reload the BPMN document, reapply the change, and save against the current revision. |
| Export returns a stale-source error | The graph was changed outside the BPMN document path. Re-import or edit through the BPMN-aware document workflow. |
| The process does not start from an expected event | Check that the root start event is a message, signal, or recurring timer start; plain starts are run directly. |

For the underlying execution model, see [Execution Model](architecture/execution-model.md).
For workflow version and publish behavior, see [Workflow Definition Version Lifecycle](running-workflows/workflow-definition-lifecycle.md). For a general
Studio host setup, see [Studio Integration](studio/integration/README.md).

### Release sources

- [Core BPMN interchange registration and endpoints](https://github.com/elsa-workflows/elsa-core/tree/4eb78f0fea903166f6049520a96331aca90e19e5/src/modules/Elsa.Bpmn.Interchange)
- [Core BPMN runtime capabilities](https://github.com/elsa-workflows/elsa-core/blob/4eb78f0fea903166f6049520a96331aca90e19e5/src/modules/Elsa.Bpmn/Hosting/BpmnRuntimeCapabilities.cs)
- [Core BPMN-to-Elsa work binder](https://github.com/elsa-workflows/elsa-core/blob/4eb78f0fea903166f6049520a96331aca90e19e5/src/modules/Elsa.Bpmn.Interchange/Binding/BpmnWorkBinder.cs)
- [Core BPMN binding format](https://github.com/elsa-workflows/elsa-core/blob/4eb78f0fea903166f6049520a96331aca90e19e5/src/modules/Elsa.Bpmn.Interchange/Binding/BpmnActivityBindingFormat.cs)
- [Studio BPMN import UI](https://github.com/elsa-workflows/elsa-studio/blob/51b176b01403c2798fc64ac6c24ab2a31df56c4b/src/modules/Elsa.Studio.Workflows/Services/BpmnImportUiService.cs)
- [Studio BPMN remote API contract](https://github.com/elsa-workflows/elsa-studio/blob/51b176b01403c2798fc64ac6c24ab2a31df56c4b/src/modules/Elsa.Studio.Workflows.Core/Client/IBpmnInterchangeApi.cs)
- [Studio BPMN read-only designer](https://github.com/elsa-workflows/elsa-studio/blob/51b176b01403c2798fc64ac6c24ab2a31df56c4b/src/modules/Elsa.Studio.Workflows/DiagramDesigners/Bpmn/BpmnDiagramDesigner.cs)
- [Studio BPMN document session](https://github.com/elsa-workflows/elsa-studio/blob/51b176b01403c2798fc64ac6c24ab2a31df56c4b/src/modules/Elsa.Studio.Workflows/DiagramDesigners/Bpmn/BpmnDocumentSession.cs)
