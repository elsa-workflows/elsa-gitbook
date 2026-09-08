# Current Documentation Coverage

This document summarizes the existing documentation structure and key topics covered in the Elsa Workflows GitBook.

## Overview

The documentation currently contains a broad set of markdown pages organized into the following sections:


### ACTIVITIES (19 pages)

- **>-** (`activities/common-properties.md`)
- **This section covers all built-in control flow activities.** (`activities/control-flow/README.md`)
- **Decision** (`activities/control-flow/decision.md`)
- **Covers all activities in the Diagnostics category** (`activities/diagnostics/README.md`)
- **Emits log entries to a configurable set of log targets called sinks** (`activities/diagnostics/log.md`)
- **MassTransit** (`activities/masstransit/README.md`)
- **Tutorial** (`activities/masstransit/tutorial.md`)
- **Send email from a workflow** (`activities/email.md`)
- **Read CSV data in a workflow** (`activities/csv.md`)
- **Slack activities** (`activities/slack.md`)
- **I/O and compression activities** (`activities/io-compression.md`)
- **GitHub activities** (`activities/github.md`)
- **Telnyx voice and webhook activities** (`activities/telnyx.md`)
- **Orchard Core content activities** (`activities/orchard-core.md`)
- **Azure Blob upload activity** (`activities/azure-blob.md`)
- **File storage activities** (`activities/file-storage.md`)
- **SQL activities** (`activities/sql.md`)
- **Azure Service Bus activities** (`activities/azure-service-bus.md`)
- **Kafka activities** (`activities/kafka.md`)

### APPLICATION-TYPES (3 pages)

- **>-** (`application-types/elsa-server-+-studio-wasm.md`)
- **>-** (`application-types/elsa-server.md`)
- **>-** (`application-types/elsa-studio.md`)

### AUTHENTICATION & AUTHORIZATION (9 guide entry points)

- **Authentication & Authorization** (`guides/authentication/README.md`)
- **Elsa Identity** (`guides/authentication/elsa-identity.md`)
- **API Keys** (`guides/authentication/api-keys.md`)
- **Direct OpenID Connect** (`guides/authentication/direct-openid-connect.md`)
- **External Authentication** (`guides/authentication/external-authentication/README.md`)
- **External Authentication extensibility** (`guides/authentication/external-authentication/extensibility.md`)
- **Elsa API Permissions** (`guides/authentication/permissions.md`)
- **Custom Authentication** (`guides/authentication/custom-authentication.md`)
- **Disable Authentication in Development** (`guides/authentication/disable-authentication.md`)

### EXPRESSIONS (4 pages)

- **>-** (`expressions/c.md`)
- **>-** (`expressions/javascript.md`)
- **Liquid** (`expressions/liquid.md`)
- **Python** (`expressions/python.md`)

### EXTENSIBILITY (10 pages)

- **This topic covers extending Elsa with your own custom activities.** (`extensibility/custom-activities.md`)
- **Connections for activities** (`guides/extensibility/connections.md`)
- **Activity Port Providers** (`guides/extensibility/activity-port-providers.md`)
- **Registering Custom Types** (`guides/extensibility/custom-types.md`)
- **Add custom Polly-based resilience strategies and retry diagnostics to Elsa 3.8.0 workflows.** (`guides/extensibility/custom-resilience-strategies.md`)
- **Workflow Providers** (`guides/extensibility/workflow-providers.md`)
- **Workflow Context Providers** (`guides/extensibility/workflow-contexts.md`)
- **OpenTelemetry Workflow and Activity Tracing** (`guides/extensibility/opentelemetry-tracing.md`)
- **DropIns** (`guides/extensibility/dropins.md`)
- **Reusable Triggers** (`extensibility/reusable-triggers-3.5-preview.md`)

### FEATURES (7 pages)

- **Alterations** (`features/alterations/README.md`)
- **Alteration Plans** (`features/alterations/alteration-plans/README.md`)
- **REST API** (`features/alterations/alteration-plans/rest-api.md`)
- **Applying Alterations** (`features/alterations/applying-alterations/README.md`)
- **Extensibility** (`features/alterations/applying-alterations/extensibility.md`)
- **REST API** (`features/alterations/applying-alterations/rest-api.md`)
- **Logging Framework** (`features/logging-framework.md`)

### GETTING-STARTED (14 pages)

- **>-** (`getting-started/concepts/README.md`)
- **Workflow Context** (`getting-started/concepts/workflow-context.md`)
- **Correlation ID** (`getting-started/concepts/correlation-id.md`)
- **Outcomes** (`getting-started/concepts/outcomes.md`)
- **Containers** (`getting-started/containers/README.md`)
- **>-** (`getting-started/containers/docker-compose/README.md`)
- **>-** (`getting-started/containers/docker-compose/elsa-server-+-studio-single-image.md`)
- **>-** (`getting-started/containers/docker-compose/elsa-server-+-studio.md`)
- **Persistent Database** (`getting-started/containers/docker-compose/persistent-database.md`)
- **Traefik** (`getting-started/containers/docker-compose/traefik.md`)
- **Docker** (`getting-started/containers/docker.md`)
- **>-** (`getting-started/hello-world.md`)
- **Packages** (`getting-started/packages.md`)
- **Prerequisites** (`getting-started/prerequisites.md`)

### GUIDES (37 pages)

- **External Application Interaction** (`guides/external-application-interaction.md`)
- **HTTP Workflows** (`guides/http-workflows/README.md`)
- **Designer** (`guides/http-workflows/designer.md`)
- **Programmatic** (`guides/http-workflows/programmatic.md`)
- **Loading Workflows from JSON** (`guides/loading-workflows-from-json.md`)
- **Loading Workflows from ElsaScript** (`guides/loading-workflows-from-elsascript.md`)
- **Hangfire Integration** (`guides/running-workflows/hangfire-integration.md`)
- **Quartz Scheduling** (`guides/running-workflows/quartz-scheduling.md`)
- **Workflow Providers** (`guides/extensibility/workflow-providers.md`)
- **Workflow Context Providers** (`guides/extensibility/workflow-contexts.md`)
- **Connections for activities** (`guides/extensibility/connections.md`)
- **Standalone and Modular Hosting** (`guides/architecture/standalone-and-modular-hosting.md`)
- **Workflow Dispatch Outbox** (`guides/architecture/workflow-dispatch-outbox.md`)
- **Runtime Coordination Storage** (`guides/architecture/runtime-coordination-storage.md`)
- **Proto.Actor Workflow Runtime** (`guides/architecture/protoactor-workflow-runtime.md`)
- **Security & Hardening** (`guides/security/README.md`)
- **Production Hardening** (`guides/security/production-hardening.md`)
- **HTTP Endpoint Security** (`guides/security/http-endpoint-security.md`)
- **Bookmark Resume Tokens** (`guides/security/bookmark-resume-tokens.md`)
- **Secrets Management** (`guides/security/secrets-management.md`)
- **Elasticsearch Setup** (`guides/persistence/examples/elasticsearch-setup.md`)
- **Dapper persistence, provider selection, SQL dialects, and migrations** (`guides/persistence/examples/dapper-setup.md`)
- **Running Workflows** (`guides/running-workflows/README.md`)
- **Dispatch Workflow Activity** (`guides/running-workflows/dispatch-workflow-activity.md`)
- **Using a Trigger** (`guides/running-workflows/using-a-trigger.md`)
- **Altering a Running Workflow Instance** (`guides/running-workflows/altering-workflow-instances.md`)
- **Workflow Definition Version Lifecycle** (`guides/running-workflows/workflow-definition-lifecycle.md`)
- **Using Elsa Studio** (`guides/running-workflows/using-elsa-studio.md`)
- **OpenAPI for HTTP workflow triggers** (`guides/http-workflows/openapi.md`)
- **Weaver and AI Workflow Assistance** (`guides/ai-workflow-assistance.md`)
- **Agents Activities and Studio Administration** (`guides/ai-agents.md`)
- **JavaScript IntelliSense Type Definitions** (`guides/studio/javascript-type-definition-providers.md`)
- **Package Manifests for Extensions** (`guides/plugins-modules/package-manifests.md`)
- **Community & Resources** (`guides/community-resources.md`)
- **Frequently Asked Questions** (`guides/faq.md`)
- **Adoption Evidence and Case Studies** (`guides/case-studies.md`)
- **Workflow-definition Labels** (`guides/workflow-definition-labels.md`)

### HOSTING (1 pages)

- **>-** (`hosting/distributed-hosting.md`)

### MULTITENANCY (2 pages)

- **Introduction** (`multitenancy/introduction.md`)
- **Setup** (`multitenancy/setup.md`)

### OPERATE (11 pages)

- **Monitoring & Observability** (`operate/monitoring-observability.md`)
- **Structured Logs** (`operate/structured-logs.md`)
- **Console Logs** (`operate/console-logs.md`)
- **Readiness and Health Checks** (`operate/readiness-and-health-checks.md`)
- **Distributed Tracing** (`operate/distributed-tracing.md`)
- **Investigate a Workflow Instance** (`operate/workflow-state-and-journal.md`)
- **Incidents** (`operate/incidents/README.md`)
- **Configuration** (`operate/incidents/configuration.md`)
- **Strategies** (`operate/incidents/strategies.md`)
- **Workflow Activation Strategies** (`operate/workflow-activation-strategies.md`)
- **>-** (`operate/workflow-instance-variables.md`)

### OPTIMIZE (3 pages)

- **Log Persistence** (`optimize/log-persistence.md`)
- **Retention policies and cleanup** (`optimize/retention.md`)
- **>-** (`optimize/workers.md`)

### ROOT (2 pages)

- **Introducing Elsa Workflows 3** (`README.md`)
- **Table of contents** (`SUMMARY.md`)

### STUDIO (10 pages)

- **This section displays the available customization options for Elsa Studio.** (`studio/design/README.md`)
- **Activity Pickers** (`studio/design/activity-pickers-3.7-preview.md`)
- **Workflow Editor Design Notes** (`studio/design/workflow-editor-3.5-preview.md`)
- **Localization** (`studio/localization.md`)
- **This section shows the various Elsa Studio customisation options available** (`studio/workflow-editor/README.md`)
- **Content Visualisers** (`studio/workflow-editor/content-visualisers-3.6-preview.md`)
- **Field Extensions** (`studio/workflow-editor/field-extensions.md`)
- **UI Hints** (`studio/workflow-editor/ui-hints.md`)
- **Activity Port Providers** (`guides/extensibility/activity-port-providers.md`)
- **Custom Activity Icons** (`guides/extensibility/custom-icons.md`)

## Key Concepts Documented

Based on the current structure, the following core concepts are documented:

### Workflows
- ✅ Basic workflow concepts (Concepts section)
- ✅ Workflow context, state, variables, inputs, outputs, bookmarks, and incidents
- ✅ Provider-backed workflow context and Studio configuration
- ✅ Outcomes and correlation IDs
- ✅ Running workflows (multiple methods)
- ✅ Loading workflows from JSON
- ✅ Workflow definition version lifecycle and Studio history operations

### Activities
- ✅ Common activity properties
- ✅ Control flow activities (Decision)
- ✅ MassTransit integration
- ✅ Email activity, SMTP configuration, attachments, and send-failure handling
- ✅ CSV activity, input representations, delimiter/header handling, typed mapping, and memory behavior
- ✅ GitHub server-side activities, token handling, GraphQL, and the 3.8.0 watcher boundary
- ✅ Slack server-side actions, token/client behavior, activity registration, and the 3.8.0 watcher boundary
- ✅ Orchard Core content operations, authenticated REST/GraphQL clients, media uploads, and dynamic content-item event triggers
- ✅ Azure Blob JSON upload activity, explicit 3.8.0 registration, container-URI behavior, block upload semantics, and Studio/server boundaries
- ✅ File storage activities, configurable blob-provider registration, input
  conversion, collection-to-ZIP behavior, and Studio/server boundaries
- ✅ SQL activities, named provider registration, parameterized workflow values,
  query/command/scalar contracts, and result/serialization boundaries
- ✅ Azure Service Bus activities, queue/topic setup, resource initialization,
  hosted workers, message serialization, and Studio/server boundaries
- ✅ Kafka activities, consumer/producer definitions, string/JSON/Avro
  factories, message matching, correlation, and hosted worker lifecycle
- ✅ Diagnostics activities

### Elsa Studio
- ✅ Application type overview
- ✅ Workflow editor and UI hints
- ✅ Content visualizers (preview)
- ✅ Activity pickers (preview)
- ✅ Localization
- ✅ External Authentication administration and Server/WASM client setup
- ✅ External Authentication server extension contracts and Studio custom editors
- ✅ JavaScript IntelliSense type-definition providers
- ✅ Custom activity icons through Studio display-settings providers
- ✅ Studio activity port providers for dynamic outcomes and embedded activities
- ✅ Optional Workflow Context module with provider selection and activity settings
- ✅ Weaver AI workspace integration, grounded context, tool governance, and
  current proposal-review boundary
- ✅ External Authentication adapters, policies, matchers, grant sources, and Studio editor extensibility

### Hosting & Operations
- ✅ Distributed hosting
- ✅ Standalone and CShells-based modular host configuration matrix
- ✅ Docker and Docker Compose examples
- ✅ Workflow instance variables
- ✅ Activation strategies
- ✅ Incidents and strategies
- ✅ Monitoring and distributed tracing guidance
- ✅ Elsa.OpenTelemetry workflow/activity middleware, span contracts, error handlers, and Studio/collector boundaries
- ✅ Runtime, persistence, and distributed-lock readiness guidance
- ✅ Elasticsearch workflow-instance and execution-log persistence setup,
  provider boundaries, index lifecycle, and release-backed limitations
- ✅ Workflow-instance state, journal, activity-execution, and variable investigation guidance

### HTTP Workflows
- ✅ Programmatic approach
- ✅ Designer approach

### Extensibility
- ✅ Custom activities
- ✅ Named connection configuration for activities, runtime injection, API
  descriptors, persistence choices, and tenant/security boundaries
- ✅ Workflow providers for code-first, external, and file-backed definitions
- ✅ Workflow Context providers, lifecycle, JavaScript access, and Studio integration
- ✅ Custom CLR type registration and Studio integration
- ✅ Activity type providers and descriptor refresh
- ✅ Server-side DropIns packaging, discovery, lifecycle, and deployment boundaries
- ✅ Inbound webhook event activities, outbound webhook sinks, and the endpoint
  security boundary
- ✅ Extension package manifests, runtime compatibility, infrastructure hints, and deploy-time settings
- ✅ Custom resilience strategies and retry-attempt recording
- ✅ JavaScript IntelliSense type-definition providers
- ✅ Reusable triggers (preview)

### Community and support

- ✅ Official documentation, repositories, samples, support-channel routing,
  release checking, and contribution guidance
- ✅ Public adoption evidence boundaries, reference architectures, and a
  permission-aware case-study contribution template

### Advanced Features

- ✅ Multitenancy setup
- ✅ Authentication
- ✅ External Authentication and Studio SSO administration
- ✅ Elsa API permission reference with Studio capability and role templates
- ✅ Alterations and alteration plans
- ✅ Release-backed operational guide for immediate alterations, filtered
  plans, Studio staging, retry behavior, and durability
- ✅ Log persistence and release-backed retention policies, cleanup batches,
  related-record deletion, and clustered-host safeguards
- ✅ Logging framework
- ✅ Structured Logs diagnostics, Studio integration, redaction, bounded
  buffering, SQLite persistence, retention, and dropped-write diagnostics
- ✅ Console Logs diagnostics, managed stdout/stderr capture, redaction,
  bounded buffering, workflow metadata, REST/SignalR access, and Studio
  operations
- ✅ Weaver AI Host and Studio workflow assistance guide, including provider,
  persistence, permissions, and audit boundaries
- ✅ Workflow-definition labels, Studio management, API assignment, filtering,
  persistence, and permission boundaries
- ✅ Agents runtime activities, provider/API composition, persistence choices,
  Studio administration, and security boundaries
- ✅ Proto.Actor workflow runtime, actor-cluster hosting, separate persistence,
  tenant propagation, and 3.8.0 client limitations

### Expressions
- ✅ C#, JavaScript, Python, Liquid

## Gaps and Weaknesses Identified

This audit was refreshed on 2026-09-08. The older version of this section
listed several topics as missing even though dedicated pages had since been
published. The entries below describe remaining improvement opportunities;
they are not claims that the related subject is undocumented.

### Remaining improvement opportunities

1. **Getting started and concepts**
   - The repository has Hello World, prerequisites, database, Docker, and
     workflow-concept pages. A single end-to-end first-workflow path could
     still connect those pages more directly for new users.
   - Activities, triggers, and bookmarks are explained across concept,
     activity, and running-workflow pages, but could be easier to discover as
     dedicated concept entry points.

2. **Elsa Studio**
   - Studio tour, integration, customization, designer, and operations pages
     exist. They could still be consolidated into a clearer task-oriented
     walkthrough for connecting a Studio host to a server.
   - The 3.8.0 release does not provide a built-in generic Connections
     administration module; custom hosts must provide the client UI when they
     use the Connections extension.

3. **Deployment and scaling**
   - Distributed hosting, Kubernetes, production hardening, readiness, and
     performance guidance exist. A single production deployment checklist and
     a compatibility matrix would make those operational decisions easier to
     apply.

4. **Observability and troubleshooting**
   - Monitoring, structured logs, console logs, distributed tracing, incidents,
     readiness, and workflow investigation are documented. Cross-guide
     troubleshooting paths and a single symptom-to-diagnostic map remain useful
     follow-up work.

5. **Reference completeness**
   - API/client and activity-reference entry points exist, but they do not yet
     cover every module and activity at the same depth.
   - Configuration management is documented, while a generated or exhaustive
     option-by-option reference is still a separate opportunity.

6. **Examples and migration detail**
   - HTTP workflows, testing, patterns, provider-specific persistence, and
     security guidance are present. More end-to-end examples, version
     compatibility notes, and release-specific breaking-change summaries would
     improve discoverability and upgrade confidence.

## Documentation Quality Notes

- **Structure**: Generally well-organized with clear categories
- **Navigation**: SUMMARY.md provides clear hierarchy
- **Completeness**: Coverage is broad; the remaining gaps are mostly consolidation,
  reference depth, and release-specific operational detail
- **Examples**: Some code examples present but not consistently throughout
- **Screenshots**: Present in some sections (e.g., README) but limited overall
- **Cross-linking**: Limited cross-references between related topics
- **Versioning**: Some features marked as preview (3.5, 3.6, 3.7) indicating rapid evolution

## Recommendations

1. **Priority 1 (High)**: Connect the existing entry points
   - Add a single first-workflow path across prerequisites, hosting, database,
     Studio, and the Hello World example.
   - Add clearer concept landing pages or cross-links for activities, triggers,
     bookmarks, and workflow lifecycle.

2. **Priority 2 (High)**: Improve operational decision support
   - Add a production deployment checklist and a release/version compatibility
     matrix.
   - Connect symptoms to the appropriate console-log, structured-log, tracing,
     incident, readiness, and workflow-investigation pages.

3. **Priority 3 (Medium)**: Expand reference depth
   - Extend activity and API/client reference coverage consistently across
     modules.
   - Add release-specific breaking-change summaries and more end-to-end
     examples for common hosting and integration paths.

4. **Priority 4 (Polish)**: Improve discoverability
   - Add focused diagrams or screenshots where they clarify Studio and
     operations workflows.
   - Continue reconciling cross-links and source pins when release branches
     advance.
   - Add FAQ section
