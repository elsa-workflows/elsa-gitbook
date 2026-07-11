# Release 3.8.0 source references

The performance guidance in this directory is grounded in these
`release/3.8.0` implementation points.

| Concern | Source location | What it establishes |
| --- | --- | --- |
| Commit registration and defaults | `src/modules/Elsa.Workflows.Core/CommitStates/CommitStrategiesFeature.cs` and `src/modules/Elsa.Workflows.Core/CommitStates/Extensions/ModuleExtensions.cs` | `AddStandardStrategies()`, `Add(...)`, and `WithDefaultWorkflowCommitStrategy(...)` are the supported configuration surface. |
| Standard strategy names | `src/modules/Elsa.Workflows.Core/CommitStates/Helpers/ObjectMetadataDescriber.cs` | Registry names are derived from strategy types, yielding names such as `WorkflowExecuted`. |
| Periodic behavior | `src/modules/Elsa.Workflows.Core/CommitStates/Strategies/Workflows/PeriodicWorkflowStrategy.cs` | A periodic strategy commits initially and after its configured interval. |
| Commit cost | `src/modules/Elsa.Workflows.Runtime/Services/DefaultCommitStateHandler.cs` | A commit persists bookmarks, execution logs, variables, and workflow state. |
| Per-definition selection | `src/modules/Elsa.Workflows.Core/Models/WorkflowOptions.cs` | `CommitStrategyName` selects the registered workflow strategy. |
| Studio selector | `elsa-studio/src/modules/Elsa.Studio.Workflows/Components/WorkflowDefinitionEditor/Components/WorkflowProperties/Tabs/Properties/Sections/Settings/Settings.razor` | Studio presents the workflow commit-strategy selector in Properties → Settings. |
| Mediator worker counts | `src/common/Elsa.Mediator/Options/MediatorOptions.cs` | Command, notification, and job worker counts each default to four. |
| Background dispatch | `src/modules/Elsa.Workflows.Runtime/Services/BackgroundWorkflowDispatcher.cs` | Background dispatch uses the command path and returns before execution completes. |
| Transactional outbox | `src/modules/Elsa.Workflows.Runtime/Options/WorkflowDispatcherOptions.cs` | Outbox enablement, immediate processing, and batch-size controls. |
| Workflow telemetry | `src/modules/Elsa.Workflows.Core/Telemetry/WorkflowInstrumentation.cs` | The `Elsa.Workflows` activity source and meter emit workflow/activity telemetry. |

When updating these docs, validate the examples against the latest released
source branch before changing the release label.
