---
description: Notes about logging from workflows in Elsa 3.8.0
---

# Log

Elsa 3.8.0 does not include **Log** in the core activity set. The optional
`Elsa.Logging` extension from `elsa-extensions` provides the activity.

For simple diagnostic output from a workflow, use the built-in **WriteLine** activity. `WriteLine` is implemented in `Elsa.Workflows.Activities.WriteLine` and writes text to the configured standard output stream.

For host application logging and workflow execution history, use the platform
diagnostics and persistence features instead of treating the `Log` activity as
the complete host log stream:

* [Structured Logs](../../operate/structured-logs.md) for host `ILogger`
  events and Studio diagnostics.
* Workflow execution logs for activity execution history.
* Log persistence configuration for controlling which execution log records are stored.

The `Elsa.Logging` `Log` activity is a separate workflow-emission path that
writes to configured sinks. It can be enabled alongside Structured Logs, but
the two features have different packages and configuration.

Example workflow diagnostic output:

```csharp
new WriteLine("Workflow started")
```

## Related Topics

* [Logging Framework](../../features/logging-framework.md)
* [Log Persistence](../../optimize/log-persistence.md)
