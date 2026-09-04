---
description: >-
  Load ElsaScript workflow files from local or cloud blob storage in Elsa
  3.8.0, including the format boundary, registration, and limitations.
---

# Loading workflows from ElsaScript

ElsaScript is a JavaScript-inspired textual DSL for defining an Elsa workflow.
In `release/3.8.0`, the Blob Storage provider can load it from `.elsa` files,
compile it on the server, and import the resulting workflow definition into
Elsa's normal management store.

This is a server-side import path. Elsa Studio does not read `.elsa` files or
compile ElsaScript in the browser. After import, Studio can work with the
definition through the ordinary workflow-definition API.

## How the pieces fit

The release uses this path for an ElsaScript blob:

1. `BlobStorageWorkflowsProvider` recursively lists files from the configured
   `IBlobStorage` and filters them by registered handler extensions.
2. `ElsaScriptBlobWorkflowFormatHandler` accepts the `elsa` extension and reads
   the blob as text.
3. `IElsaScriptCompiler` parses and compiles the text into an Elsa `Workflow`.
4. The provider returns the workflow with provider name `FluentStorage`,
   materializer name `ElsaScript`, and the original source text.
5. Elsa imports the definition into the workflow-definition store. Published
   definitions are then available to runtime and Studio through their normal
   APIs.

The base Blob Storage package also registers a JSON handler for `.json` files.
Adding ElsaScript support extends that provider with `.elsa`; it does not make
JSON and ElsaScript interchangeable.

## Install and register the server modules

Install the ElsaScript Blob Storage package in the application that hosts and
executes workflows:

```bash
dotnet add package Elsa.WorkflowProviders.BlobStorage.ElsaScript --version 3.8.0
```

Register both the base provider and its ElsaScript format handler:

```csharp
using Elsa.Extensions;
using Elsa.WorkflowProviders.BlobStorage.ElsaScript.Extensions;

builder.Services.AddElsa(elsa => elsa
    // Add the normal Elsa management/runtime modules here.
    .UseWorkflowManagement()
    .UseWorkflowRuntime()
    .UseFluentStorageProvider()
    .UseElsaScriptBlobStorage());
```

`UseElsaScriptBlobStorage` declares dependencies on the base Blob Storage and
ElsaScript features. The base provider's default storage is a local `Workflows`
directory beside the entry assembly:

```text
<application-output-directory>/Workflows/**/*.elsa
```

The provider scans recursively, so subdirectories are allowed. File extension
matching is case-insensitive.

### Use another blob storage implementation

The provider accepts a callback that returns a FluentStorage
`IBlobStorage`. Keep the storage client and its credentials in application
configuration or a secret provider:

```csharp
using Elsa.Extensions;
using Elsa.WorkflowProviders.BlobStorage.ElsaScript.Extensions;
using FluentStorage.Blobs;

IBlobStorage workflowStorage = CreateApplicationBlobStorage();

builder.Services.AddElsa(elsa => elsa
    .UseFluentStorageProvider(_ => workflowStorage)
    .UseElsaScriptBlobStorage());
```

`CreateApplicationBlobStorage` represents the FluentStorage implementation
chosen by the host. Elsa only calls `ListFilesAsync` and `ReadTextAsync` on the
returned storage; cloud container creation, credentials, permissions, and
deployment of the files remain application and infrastructure concerns.

## Write an `.elsa` workflow

The release parser accepts an identifier after `workflow`, optional metadata in
parentheses, `use` statements, variables, activity invocations, and control
flow constructs. This small file is suitable for the server sample above:

```elsa
use Elsa.Activities.Console;
use expressions js;

workflow ApprovalReminder(
  DefinitionId: "approval-reminder",
  DisplayName: "Approval reminder",
  Description: "Writes a reminder for an approval process",
  DefinitionVersionId: "approval-reminder-v1",
  Version: 1
) {
  var message = "Approval required";
  WriteLine(=> message);
}
```

The `use Elsa.Activities.Console;` line is part of the ElsaScript syntax, but
it does not load an assembly or register an activity. `WriteLine` must already
be present in Elsa's activity registry on the server.

### Metadata and defaults

The compiler maps these metadata names to the workflow definition:

| Metadata | Behavior when omitted |
| --- | --- |
| `DefinitionId` | Uses the workflow identifier. |
| `DisplayName` | Uses the workflow identifier. |
| `Description` | Uses an empty string. |
| `DefinitionVersionId` | Uses `<DefinitionId>-v1`. |
| `Version` | Uses `1`. |
| `UsableAsActivity` | Remains unset. |

Use stable `DefinitionId`, `DefinitionVersionId`, and `Version` values when
the file is deployed repeatedly. They determine whether a source file updates
an existing definition version or introduces a new one.

### Expressions are a separate concern

ElsaScript is the workflow authoring format. Expressions inside it still use
Elsa's expression providers:

- `use expressions js;` sets JavaScript as the default expression language.
- `js => ...`, `cs => ...`, `py => ...`, and `liquid => ...` select a language
  for an individual expression.
- If no expression directive is used, the compiler defaults to JavaScript.

Register the expression provider used by the file in the host. The ElsaScript
package does not install C#, JavaScript, Python, or Liquid runtime support for
you.

## Reload after changing blob files

The provider is read when Elsa populates workflow definitions. After adding or
changing an `.elsa` file, call the workflow-definition reload action:

```bash
curl --request POST \
  --url https://localhost:5001/elsa/api/actions/workflow-definitions/reload \
  --header 'Authorization: ApiKey {your-api-key}'
```

Use the route and authentication scheme configured by your host. The action
requires `actions:workflow-definitions:reload`.

Do not use the `refresh` action for this purpose. Refresh re-indexes triggers
for definitions already in the store; it does not read the external blob
source again. See [Workflow Providers](extensibility/workflow-providers.md)
for the reload/refresh distinction and distributed-hosting behavior.

## Studio and editing boundaries

Studio receives the imported definition through the server API. In the
`release/3.8.0` Studio source there is no ElsaScript editor, parser, or blob
provider module. Keep `.elsa` files in source control or the configured blob
deployment pipeline when they are the source of truth.

The ordinary workflow-definition save endpoint accepts a workflow model and
sets its materializer to `Json`. It does not emit `.elsa` source or provide an
ElsaScript round-trip writer. If designers make changes in Studio, update the
`.elsa` source separately when that file must remain authoritative, then
reload it from blob storage.

Every server that can materialize the imported definition needs the
ElsaScript package and the same activity/expression registrations. A server
that only has the persisted record but not the `ElsaScript` materializer cannot
materialize the workflow for execution or editing.

## Failure behavior and limitations

The format handler catches parse and compile failures, logs a warning with the
blob path, and returns no workflow for that file. One invalid file therefore
does not abort the entire provider enumeration, but it also does not create an
incident or a persisted definition automatically. Monitor server logs and
validate `.elsa` files before deployment.

Keep these release-specific constraints in mind:

- Use one workflow declaration per file. The compiler consumes the single
  workflow represented by the parsed program. A source containing only regular
  statements is accepted as a fallback workflow named `DefaultWorkflow`, but
  explicit workflow declarations are clearer for deployed definitions.
- Activity invocations resolve against the server's activity registry. Missing
  activities, unsupported argument properties, and ambiguous positional
  constructors cause compilation to fail and the file to be skipped.
- The handler supports `.elsa` files. It does not parse `.json`, YAML, or an
  arbitrary text file; use the JSON handler or a custom format handler for
  those formats.
- Reload reads what the provider returns. If a source file is deleted, reload
  does not act as a general garbage collector for the previously persisted
  definition. Delete or retract that definition through the management API or
  reconcile the source explicitly.
- The 3.8.0 ElsaScript compiler stamps generated workflow metadata with tool
  version `3.6.0`. Treat that field as compiler metadata, not as the Elsa
  package or runtime version deployed by your application.
- The Core module describes ElsaScript as an experimental authoring path. Test
  the syntax, activity registrations, and deployment process against the exact
  Elsa version you ship.

## Troubleshooting checklist

1. **No definitions appear:** verify that the server package is installed,
   `UseFluentStorageProvider` and `UseElsaScriptBlobStorage` are registered,
   the storage callback can list the container/directory, and the files end in
   `.elsa`.
2. **The file is skipped:** inspect the warning containing the blob path. Check
   the workflow identifier, activity names, named properties, expression
   provider, and metadata values.
3. **The definition appears but cannot run:** confirm that every runtime node
   has the ElsaScript materializer, the required activity descriptors, and the
   expression providers registered.
4. **Changes are not visible:** call `POST
   /actions/workflow-definitions/reload` with a principal that has
   `actions:workflow-definitions:reload`; `refresh` does not reload blobs.
5. **A deleted file still appears:** this is expected provider behavior. Remove
   or retract the persisted definition explicitly.

## Release-source references

This page was checked against the requested `release/3.8.0` refs:

- Core [`ElsaScriptParser`](https://github.com/elsa-workflows/elsa-core/blob/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.Dsl.ElsaScript/Parser/ElsaScriptParser.cs), [`ElsaScriptCompiler`](https://github.com/elsa-workflows/elsa-core/blob/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.Dsl.ElsaScript/Compiler/ElsaScriptCompiler.cs), and [`ElsaScriptWorkflowMaterializer`](https://github.com/elsa-workflows/elsa-core/blob/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.Dsl.ElsaScript/Materializers/ElsaScriptWorkflowMaterializer.cs)
- Core [`BlobStorageWorkflowsProvider`](https://github.com/elsa-workflows/elsa-core/blob/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.WorkflowProviders.BlobStorage/Providers/BlobStorageWorkflowsProvider.cs) and [`BlobStorageFeature`](https://github.com/elsa-workflows/elsa-core/blob/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.WorkflowProviders.BlobStorage/Features/BlobStorageFeature.cs)
- Core [`ElsaScriptBlobWorkflowFormatHandler`](https://github.com/elsa-workflows/elsa-core/blob/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.WorkflowProviders.BlobStorage.ElsaScript/Handlers/ElsaScriptBlobWorkflowFormatHandler.cs) and [`ElsaScriptBlobStorageFeature`](https://github.com/elsa-workflows/elsa-core/blob/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.WorkflowProviders.BlobStorage.ElsaScript/Features/ElsaScriptBlobStorageFeature.cs)
- Core [`WorkflowDefinitionMapper`](https://github.com/elsa-workflows/elsa-core/blob/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.Workflows.Management/Mappers/WorkflowDefinitionMapper.cs) and workflow-definition [`POST`](https://github.com/elsa-workflows/elsa-core/blob/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.Workflows.Api/Endpoints/WorkflowDefinitions/Post/Endpoint.cs), [`reload`](https://github.com/elsa-workflows/elsa-core/blob/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.Workflows.Api/Endpoints/WorkflowDefinitions/Reload/Endpoint.cs), and [`refresh`](https://github.com/elsa-workflows/elsa-core/blob/01db86ec213e952e186cdada945a70c917f302f1/src/modules/Elsa.Workflows.Api/Endpoints/WorkflowDefinitions/Refresh/Endpoint.cs) endpoints
- Studio [`workflow-definition editor`](https://github.com/elsa-workflows/elsa-studio/tree/a9f7b70ae36b9b81c16f327a8187df6cc77b1503/src/modules/Elsa.Studio.Workflows) at the same release boundary; no ElsaScript-specific Studio source was found.
