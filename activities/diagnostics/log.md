---
description: Notes about logging from workflows in Elsa 3.7.0
---

# Log

Elsa 3.7.0 does not include a built-in workflow activity named **Log** in the core activity set.

For simple diagnostic output from a workflow, use the built-in **WriteLine** activity. `WriteLine` is implemented in `Elsa.Workflows.Activities.WriteLine` and writes text to the configured standard output stream.

For application logging and workflow execution logging, use the platform logging and persistence features instead of adding a Log activity to the workflow canvas:

* ASP.NET Core logging for host and module logs.
* Workflow execution logs for activity execution history.
* Log persistence configuration for controlling which execution log records are stored.

Example workflow diagnostic output:

```csharp
new WriteLine("Workflow started")
```

## Related Topics

* [Logging Framework](../../features/logging-framework.md)
* [Log Persistence](../../optimize/log-persistence.md)
