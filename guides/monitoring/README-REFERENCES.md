# Elsa Core Source File References

This document lists all elsa-core source files referenced in the Monitoring & Observability Guide for easy maintainer review and verification.

## Application Entry Points

### Elsa.Server.Web Program.cs
- **Path:** `src/apps/Elsa.Server.Web/Program.cs`
- **Description:** Main entry point for Elsa Server web application. Demonstrates service registration patterns where OpenTelemetry, Prometheus exporters, and other monitoring infrastructure can be configured. This is the reference point for showing developers where to add observability instrumentation in their own Elsa Server deployments.
- **Referenced In:** Main README (OpenTelemetry Integration section), OpenTelemetry Setup Example
- **Key Patterns:** ASP.NET Core host builder, Elsa service registration, middleware pipeline configuration
- **Permalink (GitHub):** https://github.com/elsa-workflows/elsa-core/blob/main/src/apps/Elsa.Server.Web/Program.cs

## Docker Configuration

### ElsaServer-Datadog.Dockerfile
- **Path:** `docker/ElsaServer-Datadog.Dockerfile`
- **Description:** Reference Dockerfile demonstrating Datadog APM integration with OpenTelemetry auto-instrumentation. Shows environment variable patterns for enabling distributed tracing, service identification (DD_SERVICE, DD_ENV, DD_VERSION), and agent connectivity configuration. Serves as a template for containerized Elsa deployments with observability.
- **Referenced In:** Main README (OpenTelemetry Integration section, Datadog Integration section), OpenTelemetry Setup Example, Datadog Notes
- **Key Patterns:** OTel environment variables (OTEL_SERVICE_NAME, OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_TRACES_SAMPLER), Datadog Unified Service Tagging, agent host configuration
- **Permalink (GitHub):** https://github.com/elsa-workflows/elsa-core/blob/main/docker/ElsaServer-Datadog.Dockerfile

## Runtime and Workflow Execution

### WorkflowResumer
- **Path:** `src/modules/Elsa.Workflows.Runtime/Services/WorkflowResumer.cs`
- **Description:** Service responsible for resuming workflows based on bookmarks. Uses distributed locking to prevent concurrent resume attempts. Key location for monitoring bookmark resume operations, including metrics for resume success/failure rates and resume duration.
- **Referenced In:** Main README (Recommended Metrics section - bookmark resume metrics)
- **Key Patterns:** Distributed lock acquisition, bookmark-based workflow resumption, error handling
- **Monitoring Relevance:** Instrument with metrics for bookmark resume attempts, success/failure tracking, and resume duration histograms
- **Permalink (GitHub):** https://github.com/elsa-workflows/elsa-core/blob/main/src/modules/Elsa.Workflows.Runtime/Services/WorkflowResumer.cs

### DefaultBookmarkScheduler
- **Path:** `src/modules/Elsa.Scheduling/Services/DefaultBookmarkScheduler.cs`
- **Description:** Service that schedules bookmarks (timers, delays, cron triggers) for future execution. Enqueues bookmark resume tasks into Quartz.NET or configured scheduling backend. Important for monitoring scheduled job metrics and timer fire rates.
- **Referenced In:** Main README (Recommended Metrics section - scheduling metrics)
- **Key Patterns:** Bookmark scheduling, Quartz.NET integration, timer and cron trigger management
- **Monitoring Relevance:** Track scheduled job counts, timer fire success/failure rates, scheduling latency
- **Permalink (GitHub):** https://github.com/elsa-workflows/elsa-core/blob/main/src/modules/Elsa.Scheduling/Services/DefaultBookmarkScheduler.cs

### ResumeWorkflowTask
- **Path:** `src/modules/Elsa.Scheduling/Tasks/ResumeWorkflowTask.cs`
- **Description:** Quartz job implementation that handles scheduled bookmark resumption. This task is triggered by Quartz.NET at scheduled times and invokes the workflow runtime client to resume workflows. Critical for monitoring timer-based workflow resumption.
- **Referenced In:** Main README (Recommended Metrics section - timer fires)
- **Key Patterns:** Quartz job execution, scheduled bookmark resume, workflow runtime client interaction
- **Monitoring Relevance:** Instrument with metrics for timer fire attempts, successes, failures, and execution duration
- **Permalink (GitHub):** https://github.com/elsa-workflows/elsa-core/blob/main/src/modules/Elsa.Scheduling/Tasks/ResumeWorkflowTask.cs

## Distributed Locking

### IDistributedLockProvider Interface
- **Path:** `src/modules/Elsa.DistributedLocking/Contracts/IDistributedLockProvider.cs`
- **Description:** Interface for distributed lock providers implemented by Redis, PostgreSQL, and other lock providers via Medallion.Threading. Core abstraction for preventing concurrent workflow modifications in clustered deployments.
- **Referenced In:** Main README (Recommended Metrics section - distributed locking metrics)
- **Key Patterns:** Lock acquisition, timeout handling, lock release
- **Monitoring Relevance:** Track lock acquisition success rate, timeouts, wait duration, and current locks held
- **Permalink (GitHub):** https://github.com/elsa-workflows/elsa-core/blob/main/src/modules/Elsa.DistributedLocking/Contracts/IDistributedLockProvider.cs

## Additional Monitoring Touchpoints

While the above files are explicitly referenced in the monitoring guide, the following areas in elsa-core are relevant for comprehensive observability:

### Activity Execution Context
- **Path:** `src/modules/Elsa.Workflows.Core/Contexts/ActivityExecutionContext.cs`
- **Description:** Context for activity execution, including bookmark creation methods. Relevant for instrumenting activity-level traces and metrics.
- **Monitoring Relevance:** Activity execution duration, bookmark creation events
- **Permalink (GitHub):** https://github.com/elsa-workflows/elsa-core/blob/main/src/modules/Elsa.Workflows.Core/Contexts/ActivityExecutionContext.cs

### HTTP Workflow Extensions
- **Path:** `src/modules/Elsa.Http/Extensions/BookmarkExecutionContextExtensions.cs`
- **Description:** Extensions for generating bookmark trigger URLs for HTTP-triggered workflows. Relevant for monitoring HTTP endpoint activity.
- **Monitoring Relevance:** HTTP request rates, endpoint latency, external trigger monitoring
- **Permalink (GitHub):** https://github.com/elsa-workflows/elsa-core/blob/main/src/modules/Elsa.Http/Extensions/BookmarkExecutionContextExtensions.cs

### Quartz Scheduling Feature
- **Path:** `src/modules/Elsa.Scheduling/Features/QuartzSchedulingFeature.cs`
- **Description:** Feature configuration for Quartz.NET integration, including clustering support when enabled. Configuration point for monitoring scheduled jobs.
- **Monitoring Relevance:** Scheduled job configuration, Quartz.NET health monitoring
- **Permalink (GitHub):** https://github.com/elsa-workflows/elsa-core/blob/main/src/modules/Elsa.Scheduling/Features/QuartzSchedulingFeature.cs

## Notes for Maintainers

### Permalink Maintenance
- All permalinks point to the `main` branch and may need updating for specific releases
- When referencing code for a specific Elsa version, consider using release tags (e.g., `/blob/v3.0.0/`)
- Verify links periodically as file paths may change with refactoring

### Source File Path Conventions
- All paths are relative to the elsa-core repository root
- Paths follow the convention: `src/modules/{ModuleName}/{FileType}/{FileName}.cs`
- Application entry points are in: `src/apps/{AppName}/`
- Docker files are in: `docker/`

### Monitoring Guide Accuracy
- The monitoring guide references specific patterns and methods from these files
- When elsa-core is updated, verify that the monitoring guide remains accurate:
  - Check if referenced service registration patterns change
  - Verify environment variable names remain consistent
  - Confirm OpenTelemetry integration points are still valid
  - Update metric instrumentation examples if APIs change

### Integration Testing
- Consider adding integration tests that validate monitoring instrumentation:
  - Verify metrics are exposed at `/metrics` endpoint
  - Validate trace context propagation through workflow execution
  - Test structured logging output includes correlation IDs
  - Confirm distributed tracing spans are created for key operations

### Documentation Synchronization
- When adding new observability features to elsa-core:
  - Update this README-REFERENCES.md with new source files
  - Add examples to relevant monitoring guide sections
  - Update metric names and labels if conventions change
  - Document new environment variables or configuration options

### Versioning
- This monitoring guide is written for Elsa Workflows 3.x
- If instrumentation patterns change significantly in future versions:
  - Create version-specific documentation
  - Maintain compatibility notes for migration
  - Update code examples and screenshots

## Community Contributions

This monitoring guide and references are maintained by the Elsa Workflows community. Contributions are welcome:

- **Report Issues**: If referenced files move or patterns change, please open an issue
- **Submit Updates**: Pull requests to update this guide are appreciated
- **Share Examples**: If you've implemented custom metrics or dashboards, consider contributing examples
- **Improve Documentation**: Suggestions for clarity and completeness are valued

## External Observability Resources

While this document focuses on elsa-core references, also consult:

- **OpenTelemetry .NET**: https://github.com/open-telemetry/opentelemetry-dotnet
- **Prometheus .NET**: https://github.com/prometheus-net/prometheus-net
- **Serilog**: https://github.com/serilog/serilog
- **ASP.NET Core Diagnostics**: https://learn.microsoft.com/en-us/aspnet/core/log-mon/

These external libraries provide the instrumentation foundation that complements Elsa's workflow-specific monitoring.
