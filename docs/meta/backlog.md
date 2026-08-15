# Prioritized Documentation Backlog

This backlog is prioritized by user impact and frequency of complaints
based on gap analysis from 161 issues across elsa-studio and
elsa-gitbook.

## Slice Inventory (2026-08-15)

This inventory reflects the current GitBook contents before selecting the
next automation slice. "Covered" means the repository now includes a
dedicated page or guide for the topic; it does not imply every
acceptance criterion below is already complete.

### Covered or substantially covered slices

- `DOC-001` V2 to V3 migration
- `DOC-002` Database configuration
- `DOC-003` Authentication and authorization
- `DOC-004` Hello World examples
- `DOC-005` HTTP workflows
- `DOC-006` Docker quickstart
- `DOC-007` Custom activities V3 rewrite
- `DOC-008` Architecture overview
- `DOC-009` Kubernetes deployment
- `DOC-010` Studio tour and troubleshooting
- `DOC-011` Testing and debugging workflows
- `DOC-012` Blocking and trigger activities
- `DOC-013` Studio integration
- `DOC-014` Clustering and distributed hosting
- `DOC-015` Monitoring and observability
- `DOC-016` Workflow context V3
- `DOC-017` Common workflow patterns
- `DOC-018` Plugins and modules development
- `DOC-019` HTTP endpoint security
- `DOC-020` EF Core migrations
- `DOC-021` Configuration management
- `DOC-023` Identity provider integrations
- `DOC-024` MassTransit communication
- `DOC-025` Long-running workflows
- `DOC-026` Error handling and retry logic
- `DOC-027` Execution model
- `DOC-022` Scaling and performance
- `DOC-038` Distributed tracing
- `DOC-039` Performance tuning
- `DOC-028` Studio customization
- `DOC-029` Custom UI hints
- `DOC-030` Custom UI components
- `DOC-040` Timer and scheduled workflows
- `DOC-041` Loading workflows from JSON
- `DOC-042` Bulk dispatch workflows activity
- `DOC-049` Studio custom-elements embedding cookbook
- `DOC-050` Studio platform integration
- `DOC-053` Alterations operational guide hardening
- `DOC-047` API reference
- `DOC-048` Activity reference
- `DOC-052` Workflow state and journal API cookbook
- `DOC-051` Activity and workflow testing cookbook
- `DOC-054` Standalone versus modular configuration matrix
- `DOC-055` Message broker topology cookbook
- `DOC-056` Custom resilience extensibility
- `DOC-057` Elsa API permission reference
- `DOC-058` Workflow dispatch outbox operations
- `DOC-059` Runtime coordination storage
- `DOC-060` Readiness and health checks
- `DOC-061` Workflow definition version lifecycle
- `DOC-062` Brokered external authentication and Studio administration
- `DOC-063` Extension package manifests and infrastructure metadata
- `DOC-064` Secrets management and Studio secret picker
- `DOC-036` Activity type providers
- `DOC-035` Webhook extensibility
- `DOC-037` Alterations
- `DOC-065` JavaScript type-definition providers
- `DOC-066` External Authentication extension authoring
- `DOC-031` Custom icons
- `DOC-032` Workflow providers
- `DOC-033` Custom types
- `DOC-043` Hangfire integration
- `DOC-034` DropIns
- `DOC-044` Community resources
- `DOC-045` Case studies
- `DOC-046` FAQ
- `DOC-067` Weaver and AI workflow assistance
- `DOC-068` Workflow-definition labels
- `DOC-069` Elasticsearch persistence provider
- `DOC-070` Runtime Agents activities and Studio administration

### Available next slices

- `DOC-072` Studio activity port providers
- `DOC-073` Connections extension and activity connection management
- `DOC-074` Workflow-trigger OpenAPI exposure

### Recommended next slice

- `DOC-072` Studio activity port providers

### Newly discovered slices

- `DOC-069` Elasticsearch persistence provider — `elsa-extensions`
  `release/3.8.0` contains `Elsa.Persistence.Elasticsearch`, including
  `UseElasticsearch`, configurable endpoint/authentication/index naming, a
  workflow-instance store, and an execution-log store. The source also leaves
  important boundaries that the guide must state: not every runtime/management
  store is replaced, several workflow-instance filters are TODO, and the
  release source has no caller for the optional index-configuration hook.
- `DOC-070` Runtime Agents activities and Studio administration —
  `elsa-extensions` release `3.8.0` contains `Elsa.Agents.*` runtime activities,
  provider registration, agent/skill APIs, persistence options, and
  `Elsa.Studio.Agents`. This is distinct from the Core/Weaver guide in
  `DOC-067`; validate the compiled contracts rather than the extension README
  examples, which contain claims not present in the release source.
- `DOC-071` ProtoActor workflow runtime and actor-cluster hosting —
  `elsa-extensions` release `3.8.0` contains the
  `Elsa.Workflows.Runtime.ProtoActor` and `Elsa.Actors.ProtoActor` modules.
  The GitBook only mentions `ProtoActorWorkflowRuntime` in the distributed
  hosting page; it does not explain `UseProtoActor`, cluster providers,
  remote addressing, persistence, tenant propagation, or the release's
  incomplete client methods.
- `DOC-073` Connections extension and activity connection management —
  `elsa-extensions` release `3.8.0` contains `Elsa.Connections.*`, including
  connection descriptors, activity middleware, API endpoints, and in-memory
  or EF Core stores. The GitBook has no focused guide explaining how an
  activity declares a connection, how the runtime resolves it, or which
  persistence and security boundaries apply.
- `DOC-072` Studio activity port providers — `elsa-studio` release `3.8.0`
  contains `IActivityPortProvider`, priority-based provider selection, and
  built-in providers for dynamic outcomes, Switch, HTTP, and related
  activities. Existing custom-activity guidance does not explain this
  Studio-only dynamic port contract or how to author a custom provider.
- `DOC-074` Workflow-trigger OpenAPI exposure — `elsa-extensions` release
  `3.8.0` contains an opt-in `Elsa.Http.OpenApi` module that exports workflow
  HTTP triggers to `/openapi.json` and `/documentation`. The workbench does
  not enable it automatically, and the mapping/security boundary needs a
  focused guide.

### Current run plan (2026-08-15)

- Select and complete `DOC-071` ProtoActor workflow runtime and actor-cluster
  hosting from the fresh `origin/main` worktree.
- Add a concise architecture and operations guide covering the runtime
  boundary, host registration, cluster provider and remote configuration,
  persistence, tenant propagation, and release limitations.
- Update navigation, coverage metadata, and this slice inventory. Validate all
  claims and examples against the exact `release/3.8.0` source refs in Core,
  Studio, and Extensions.

### Current run selection (2026-08-15)

- The previous inventory completed `DOC-070`; release-source review found two
  new gaps, and `DOC-071` is selected because ProtoActor changes the workflow
  runtime and cluster topology for architects and operators.
- The Extensions release ships `UseProtoActor` on both the module and workflow
  runtime features, a default in-memory cluster provider, configurable remote
  addressing, optional actor persistence, and tenant-aware actor middleware.
  The release client still throws `NotImplementedException` for delete and
  instance-existence operations, so the guide must state that boundary.
- Presentation target: a decision-oriented architecture guide with a minimal
  host setup, production configuration checklist, and explicit limitations;
  it should distinguish ProtoActor runtime coordination from workflow data
  persistence, distributed locking, and MassTransit dispatch.

### Current run completion (2026-08-15)

- Added `guides/architecture/protoactor-workflow-runtime.md`, linked it from
  `SUMMARY.md`, the architecture overview, and distributed-hosting guidance,
  and updated current coverage.
- Documented the release-backed `UseProtoActor` registrations, cluster and
  remote configuration, default providers, actor versus workflow persistence,
  tenant propagation, Studio boundary, and the unsupported 3.8.0 client
  operations.
- Delegated inventory also found Studio activity port providers, generic
  connection-backed activities, and workflow-trigger OpenAPI exposure. These
  are recorded as `DOC-072` through `DOC-074`; `DOC-072` is recommended next.

### Current run completion (2026-08-14)

- Added `guides/ai-agents.md`, linked it from `SUMMARY.md`, and updated the
  coverage inventory.
- Documented the compiled 3.8.0 Agents runtime, dynamic workflow activities,
  provider registration, API routes and permissions, in-memory and EF Core
  persistence, Studio administration, and security/operational boundaries.
- Explicitly separated Agents from Weaver and avoided unreleased multi-agent
  and automatic configuration APIs described by the extension README.
- Self-review and release-source assertions found no remaining high-priority
  factual, source-grounding, navigation, link, structure, regression, or
  formatting issues. No additional distinct follow-on topic was discovered.

### Current run plan (2026-08-11)

- Select and complete `DOC-068` Workflow-definition labels from the fresh
  `origin/main` worktree.
- Cover the user-facing purpose and boundaries, release-backed Core and
  Studio module setup, label CRUD and assignment APIs, definition-list
  filtering, persistence choices, permissions, and tenant-aware cautions.
- Validate all claims and examples against the exact `release/3.8.0` source
  commits in Core, Studio, and Extensions; update the coverage inventory and
  slice plan after the documentation change.

### Current run selection (2026-08-11)

- The previous inventory completed `DOC-067` and left no named slices.
- Source review found the distinct `DOC-068` gap: Core exposes the Labels
  feature and definition-label/filter endpoints, Studio exposes the Labels
  administration module and workflow-definition editor widget, and the
  GitBook has no dedicated label guide.
- Presentation target: a task-oriented guide that starts with the Studio
  user path, then explains host/API/persistence setup and the definition-level
  filtering contract.

### Current run completion (2026-08-10)

- Added `guides/ai-workflow-assistance.md`, linked it from `SUMMARY.md` and
  the Studio user guide, and updated `docs/meta/current-coverage.md`.
- Documented the release-backed AI Host, Weaver module, Copilot provider,
  optional durable AI persistence, grounding/tool governance, tenant and
  permission boundaries, and the current absence of proposal action routes.
- No additional distinct follow-on topic was discovered; the next run should
  inventory the source again instead of assuming a new slice.

### Current run completion (2026-08-11)

- Added `guides/workflow-definition-labels.md`, linked it from `SUMMARY.md`,
  and updated current coverage metadata.
- Documented Core and Studio opt-in setup, definition-version associations,
  label CRUD and assignment routes, repeatable definition-list filtering,
  EF Core and MongoDB persistence, permissions, tenant boundaries, and
  troubleshooting.
- The next run should re-inventory the source rather than assume another
  named slice is available.

### Previous run plan (2026-08-08)

- Selected and completed `DOC-045` Case studies after re-inventorying the
  published `origin/main` contents. The remaining candidates were `DOC-046`
  FAQ and the source-backed `DOC-067` Weaver and AI workflow assistance topic.
- Added `guides/case-studies.md`, linking it from `SUMMARY.md`, and updated
  the coverage inventory. Because no named production customer evidence is
  published in the current official documentation, the guide labels the
  anonymous ERP discussion and release reference hosts accurately instead of
  presenting them as customer case studies.

### Current run plan (2026-08-09)

- Selected and completed `DOC-046` FAQ after re-inventorying the published
  `origin/main` contents. `DOC-067` Weaver and AI workflow assistance remains
  available for a later run.
- Added `guides/faq.md`, a concise, decision-oriented FAQ that routes common
  process-designer, domain-specialist, technical-user, and operator questions
  to the existing release-backed guides. Linked it from `SUMMARY.md` and the
  troubleshooting guide, and updated current coverage.
- Validation target: the exact `release/3.8.0` commits in `elsa-core`,
  `elsa-studio`, and `elsa-extensions`, plus repository navigation and link
  checks.
- Presentation target met: a symptom/question map with short answers and clear
  next actions, not a duplicate reference manual.
- Source review has not identified another distinct topic beyond the already
  tracked `DOC-067` Weaver and AI workflow assistance slice.

### Previous run plan (2026-08-07)

- Selected and completed `DOC-044` Community resources after re-inventorying
  the published `origin/main` contents. The remaining candidates are
  `DOC-045` Case studies, `DOC-046` FAQ, and the newly discovered `DOC-067`
  Weaver and AI workflow assistance topic.
- Added `guides/community-resources.md`, a concise cross-persona support and
  discovery guide that routes users to the right official channel for
  learning, release checking, questions, bug reports, feature proposals,
  extension discovery, and contributions.
- Validation target: the exact remote `origin/release/3.8.0` commits in
  `elsa-core`, `elsa-studio`, and `elsa-extensions`, plus live checks for the
  official external links included in the guide.
- Presentation target: a decision-oriented resource map with a support
  etiquette checklist and source/release hygiene guidance, rather than a
  catalogue of unverified third-party projects.
- Source review found no additional distinct topic beyond the already tracked
  `DOC-067` Weaver and AI workflow assistance slice.

### Current run selection (2026-08-06)

- Selected and completed `DOC-034` DropIns after re-inventorying the published
  `origin/main` contents. The remaining candidates are `DOC-044` through
  `DOC-046`; `DOC-067` is a newly discovered source-backed
  AI/Weaver topic reserved for a later run.
- The current GitBook has no focused guide for the DropIns module. This slice
  will explain when to use DropIns instead of ordinary package references or
  explicit module registration, how the release discovers assemblies, and
  which host and deployment constraints matter.
- Validation target: the exact remote `origin/release/3.8.0` commits in
  `elsa-core`, `elsa-studio`, and `elsa-extensions`.
- Presentation target: a concise developer guide with a minimal DropIn
  package example, host configuration, discovery lifecycle, and
  troubleshooting/operational boundaries.
- Completed by adding `guides/extensibility/dropins.md`, linking it from the
  extensibility and plugins/modules navigation, and updating coverage metadata.

### Current run selection (2026-08-05)

- Selected `DOC-033` Custom types after re-inventorying the published
  `origin/main` contents. The remaining candidates are `DOC-034` and
  `DOC-044` through `DOC-046`; the GitBook explains activity and workflow
  registration in several places but has no focused guide for registering
  custom CLR types for serialization and expression/runtime use.
- Initial inventory found no additional distinct slice. Source review will
  distinguish runtime serializer type registration from Studio JavaScript
  type-definition providers and from activity-type providers.
- Validation target: the exact remote `origin/release/3.8.0` commits in
  `elsa-core`, `elsa-studio`, and `elsa-extensions`.
- Presentation target: a concise developer guide that explains when custom
  type registration is needed, shows the registration path and a safe
  example, and clarifies trust, persistence, expression, and Studio
  boundaries.
- Completed by adding `guides/extensibility/custom-types.md`, linking it from
  extensibility, workflow-provider, and JavaScript expression documentation,
  and validating the separate Studio, expression, serialization, and Jint
  contracts against the 3.8.0 release source.
- Source review also found a new distinct `DOC-067` Weaver/AI workflow
  assistance slice spanning Core's AI host and proposal APIs and Studio's
  Weaver module. It is available for a later run; `DOC-034` remains the next
  recommended slice.

### Current run selection (2026-08-04)

- Selected `DOC-032` Workflow providers after re-inventorying the published
  `origin/main` contents. The remaining candidates are `DOC-033`, `DOC-034`,
  and `DOC-044` through `DOC-046`; the GitBook explains workflow-definition
  import and registration in several places but has no focused guide for
  implementing or composing `IWorkflowsProvider` sources.
- Initial inventory found no additional distinct slice. Source review confirmed
  that provider precedence, reload/refresh behavior, and the boundary between
  providers and workflow stores belong in this slice rather than a new one.
- Validation target: the exact remote `origin/release/3.8.0` commits in
  `elsa-core`, `elsa-studio`, and `elsa-extensions`.
- Presentation target: a concise developer and architect guide that explains
  when to use a workflow provider, shows a minimal provider and registration
  path, and covers reload/refresh and operational trade-offs.
- Completed by adding `guides/extensibility/workflow-providers.md`, linking it
  from the extensibility navigation and JSON-loading guide, and documenting
  the release-backed provider, materializer, tenant, version, Studio, and
  reload/refresh contracts.
- No additional distinct follow-on topic was discovered during this source
  review; `DOC-033` remains the next recommended slice.

### Current run selection (2026-08-03)

- Selected `DOC-031` Custom icons after re-inventorying the published
  `origin/main` contents. The remaining candidates are `DOC-032` through
  `DOC-034` and `DOC-044` through `DOC-046`; custom activity metadata and
  Studio customization are documented, but the release-backed icon asset and
  registration path is not.
- Initial inventory found no additional distinct slice. Source review confirmed
  that this topic is a Studio display-settings provider rather than a runtime
  metadata or package-asset feature.
- Validation target: `release/3.8.0` in `elsa-core`, `elsa-studio`, and
  `elsa-extensions`.
- Presentation target: a concise developer guide with the end-to-end asset
  path, a minimal registration example, and troubleshooting for missing icons.
- Completed by adding `guides/extensibility/custom-icons.md`, linking it from
  Studio customization, custom activities, and `SUMMARY.md`, and validating
  the provider contract against the release source.

### Current run selection (2026-08-02)

- Selected and completed `DOC-066` External Authentication extension authoring
  after re-inventorying the latest `origin/main` contents. The existing
  operations guide intentionally deferred custom adapters, policies, matchers,
  grant sources, descriptors, migrations, and Studio custom editors.
- Added a focused developer/Studio guide and linked it from the External
  Authentication section.
- Validated the contracts against `elsa-core` `origin/release/3.8.0` at
  `edb5f7c` and `elsa-studio` `origin/release/3.8.0` at `456794c`. The
  extensions checkout has no upstream `origin/release/3.8.0` ref; no
  extension-repository source was needed for these contracts.
- No additional distinct follow-on topic was found during this source review.

### Current run selection (2026-08-01)

- Selected and completed `DOC-065` JavaScript type-definition providers after
  re-inventorying the remaining candidates against the GitBook and
  `release/3.8.0` source. The existing JavaScript and Studio expression pages
  explained runtime syntax but did not explain how custom TypeScript
  declarations reach Studio's Monaco editor.
- Added a focused developer/Studio guide, linked it from the expression
  documentation, and included release-source references for the Core provider
  contract, endpoint, and Studio Monaco handler.
- Core's remote `release/3.8.0` ref advanced from `7e82a55` to `edb5f7c` during
  review; the cited JavaScript type-definition paths were unchanged. Studio
  remains at `ef6a39d`, and Extensions has no advertised upstream
  `release/3.8.0` branch.
- No additional distinct follow-on topic was found during this source review;
  re-inventory before selecting the next slice.

### Current run selection (2026-07-31)

- Selected `DOC-035` Webhook extensibility after re-inventorying the remaining
  candidates against the GitBook and `release/3.8.0` source. The existing
  external-application guide is stale: the release exposes inbound custom
  webhook sources and dynamic event activities, while `POST /webhooks` is
  anonymous and needs an explicit application/front-door security warning.
- Added `DOC-065` JavaScript type-definition providers as a newly discovered
  follow-on topic. The release has a distinct `ITypeDefinitionProvider` and
  Studio IntelliSense path that should not be conflated with CLR type
  registration or workflow-provider imports.
- Completed the guide and self-review with no remaining high-priority
  documentation findings. No additional distinct follow-on topic was found.

### Current run selection (2026-07-30)

- Selected and completed `DOC-037` Alterations. Added a task-oriented,
  release-backed guide that connects immediate execution, filtered plans,
  Studio staging, retry behavior, durability, and custom handlers. Corrected
  the existing retry reference for the `GET`/`POST` contract and the
  multi-instance batching caveat in `release/3.8.0`.
- Core's remote `release/3.8.0` ref advanced from `7e82a55` to `edb5f7c`
  during review; the alteration implementation paths used here were
  unchanged. Studio remains at `ef6a39d`, and Extensions has no advertised
  upstream `release/3.8.0` branch, so its local release snapshot remains
  pinned to `d407e96`.
- No additional distinct follow-on topic was identified during the source
  review; re-inventory before selecting the next slice.

### Current run selection (2026-07-29)

- Selected and completed `DOC-036` Activity type providers. The release-backed
  guide covers the provider contract, descriptor registration and refresh
  behavior, Studio's catalog consumption, dynamic-provider patterns, and the
  stability requirements for custom activity types used by persisted workflows.
- No additional distinct follow-on topic was identified during the source
  review; re-inventory the release source and GitBook before the next slice.

### Current run selection (2026-07-27)

- Selected and completed `DOC-063` Extension package manifests and
  infrastructure metadata. Added a release-backed guide covering generated
  `elsa-package.json` output, Server/Studio runtime compatibility hints,
  infrastructure requirements, deploy-time settings, packaging checks, and
  the boundary between package metadata and Studio provisioning.
- No additional distinct follow-on topic was discovered during the source
  review; re-inventory the GitBook and release source before selecting the next
  slice.

### Current run selection (2026-07-26)

- Selected and completed `DOC-062` Brokered external authentication and Studio administration.
  Release-source review found a distinct optional broker for upstream OIDC
  connections, local-login and external-login flows, tenant-scoped identity
  links, permission mapping, and separate Server/WASM hosting behavior. The
  existing external identity provider guide covers direct bearer-token
  validation but not this brokered Studio administration path.
- No additional distinct follow-on topic was discovered during this source
  review; `DOC-063` remains the next available slice.

### Current run selection (2026-07-25)

- Selected and completed `DOC-061` Workflow definition version lifecycle.
  Release-source
  review found that Core exposes distinct `Latest`, `Published`, `Draft`,
  `AllVersions`, and specific-version selectors, while Studio presents latest
  and published versions separately and lets users inspect, delete, or roll
  back version history. The GitBook had no focused lifecycle guide.
- Added a release-backed guide grounded in the Core management/API
  implementation and Studio version-history/editor behavior, with links to
  the existing alterations guide for migrating already-running instances.

### Current run selection (2026-07-24)

- Selected and completed `DOC-060` Readiness and health checks with a
  release-backed operator guide for runtime, persistence, distributed-lock,
  liveness, and readiness behavior.
- No new distinct follow-on topic was discovered during this run's source
  review; the existing monitoring, deployment, and runtime-coordination guides
  now link to the focused health-check contract.

### Current run selection (2026-07-23)

- Selected and completed `DOC-059` Runtime coordination storage with a
  release-backed guide covering the key-value and distributed-lock providers
  used by outbox, worker, and multi-node runtime coordination.
- Added `DOC-060` during source review because the release exposes a useful
  distributed-lock readiness probe, but the GitBook had no focused guide for
  composing it with persistence and host-readiness checks.

### Current run selection (2026-07-22)

- Selected and completed `DOC-058`, the workflow dispatch outbox operations
  guide. The release source confirms a distinct runtime path for dispatches
  made during workflow execution, with post-commit processing, retry limits,
  orphan cleanup, and batch controls that are not covered by the existing
  performance guidance.
- Added `DOC-059` during source review: the default key-value store and local
  file lock are unsafe durability/coordination defaults for production, and
  the existing docs do not bring those runtime-provider choices together for
  the outbox and other multi-node coordination paths.

### Current run selection (2026-07-21)

- Selected and completed `DOC-057`, the Elsa API permission reference.
- Added a compact permission-to-operation map, Studio-facing capability
  guidance, and least-privilege role templates grounded in the remote
  `origin/release/3.8.0` source branches.
- `DOC-058` remains available for the next run; no new topic was added during
  this source review.

### Current run selection (2026-07-20)

- Selected and completed `DOC-056`, the recommended custom-resilience
  follow-on slice.
- Added a release-backed guide for custom Polly strategies, serializer type
  registration versus catalog configuration, resilient activity integration,
  Studio behavior, and retry recorder/reader persistence tradeoffs.
- `DOC-057` was selected for the 2026-07-21 run; `DOC-058` remains available.

### Newly discovered follow-on topics

- `DOC-065` JavaScript IntelliSense and custom type-definition providers:
  explain `ITypeDefinitionProvider`, `AddTypeDefinitionProvider<T>()`, the
  type-definition endpoint, and Studio's Monaco integration separately from
  runtime CLR registration and variable type aliases.

- `DOC-063` Extension package manifests and infrastructure metadata: explain
  the release extension annotations for runtime kind, infrastructure hints,
  secret/advanced settings, and restart requirements without implying that
  Studio provisions infrastructure automatically.
- `DOC-064` Secrets management and Studio secret picker: document the Core
  Secrets module, protected references and expressions, lifecycle operations,
  permissions, and Studio's secret-management and activity-property picker
  behavior. Clarify that the release's built-in repositories are not a claim
  of integration with an external vault.
- `DOC-068` Workflow-definition labels: document the Core Labels feature,
  definition-label endpoints and list filtering, Studio's optional Labels
  module and editor widget, persistence providers, permissions, and the
  distinction between definition metadata and runtime-instance state.

### Current run selection (2026-07-28)

- Selected and completed `DOC-064` Secrets management and Studio secret picker. Release
  source review found a distinct cross-persona gap: Core exposes versioned
  secrets, protected references, secret expressions, lifecycle endpoints, and
  a file/in-memory repository, while Studio exposes a secret-management page
  and secret picker for activity inputs. The GitBook had no focused guide for
  using or securing this feature.
- No further distinct follow-on topic was added during implementation; `DOC-036`
  Activity type providers is now the recommended next slice.

### Most recent completed slice

- `DOC-045` Adoption evidence and case studies: completed on 2026-08-08 with
  an evidence-first guide that distinguishes named public references,
  anonymous architecture descriptions, official reference hosts, and
  generic adoption patterns. It also adds a permission-aware contribution
  template for future named case studies.

- `DOC-035` Webhook extensibility: completed on 2026-07-31 with a
  release-backed guide for inbound event sources, generated trigger
  activities, typed payloads, outbound `RunTask` sinks, and the anonymous
  endpoint's application-owned security boundary.

- `DOC-037` Alterations: completed on 2026-07-30 with a release-backed
  operational guide for execution-mode choice, immediate and planned
  alterations, Studio staging, retry behavior, durability, and extension
  boundaries.

- `DOC-036` Activity type providers: completed on 2026-07-29 with a
  release-backed guide covering the provider contract, descriptor registry
  refresh, server API and Studio consumption, dynamic-provider patterns,
  versioning, tenant scope, and troubleshooting.

- `DOC-064` Secrets management and Studio secret picker:
  completed on 2026-07-28 with a release-backed guide for encrypted and
  configuration-backed stores, secret references and expressions, lifecycle
  operations, permissions, Studio management, and the secret picker.

- `DOC-063` Extension package manifests and infrastructure metadata:
  completed on 2026-07-27 with a release-backed guide for runtime kind,
  infrastructure, setting, build/pack, and Studio-boundary metadata.

- `DOC-062` Brokered external authentication and Studio administration:
  completed on 2026-07-26 with a release-backed guide for the optional broker,
  upstream OIDC connection management, Studio Server/WASM setup, permissions,
  tenant-scoped identity links, and multi-node security requirements.

- `DOC-060` Readiness and health checks:
  completed on 2026-07-24 with a release-backed operator guide for runtime,
  persistence, distributed-lock, liveness, and readiness behavior.

- `DOC-057` Elsa API permission reference:
  completed on 2026-07-21 with a release-backed map of core and module
  permissions, Studio capability behavior, role templates, and authorization
  boundary caveats.

- `DOC-059` Runtime coordination storage:
  completed on 2026-07-23 with a release-backed guide separating durable
  key-value storage from cross-node distributed locking, mapping both to
  outbox and runtime consumers, and documenting readiness validation.

- `DOC-058` Workflow dispatch outbox operations:
  completed on 2026-07-22 with release-backed configuration, delivery,
  retry, cleanup, locking, durability, and diagnosis guidance.

- `DOC-056` Custom resilience extensibility:
  completed on 2026-07-20 with a release-backed guide for custom Polly
  strategies, `Resilience:Strategies` configuration, `IResilientActivity`
  integration, Studio gating, and retry-attempt recording limitations.

- `DOC-055` Message broker topology cookbook:
  completed on 2026-07-19 with a release-backed guide comparing the MassTransit
  message-activity, workflow-dispatcher, Azure Service Bus activity, and
  distributed-cache topologies, including naming, placement, lifetime, and
  cleanup tradeoffs.

- `DOC-054` Standalone versus modular configuration matrix:
  completed on 2026-07-18 with a release-backed guide mapping code-first
  `AddElsa(...)` composition to CShells feature activation and shell-scoped
  settings, including identity and configuration-shape caveats.
- `DOC-050` Studio platform integration:
  completed on 2026-07-17 with a release-backed guide for opt-in Studio-host
  registration, manual and automatic artifact submission, payload-reference
  constraints, and the separation from custom-elements embedding.
- `DOC-051` Activity and workflow testing cookbook:
  completed on 2026-07-16 with a compact, release-backed path for unit-testing
  activities, testing workflow routing through the journal, and escalating to
  host integration tests and runtime investigation when appropriate.
- `DOC-052` Workflow state and journal API cookbook:
  completed on 2026-07-15 with a release-backed operational playbook for
  listing, reading, and diagnosing live instances through Studio and the API,
  including journal filters, activity records, and controlled variable changes.
- `DOC-048` Activity reference:
  completed on 2026-07-14 with a release-backed task map of core workflow,
  scheduling, HTTP, composition, and extensibility activities, including the
  module-aware activity-picker behavior in Elsa Studio.
- `DOC-043` Hangfire integration:
  completed on 2026-07-12 with a release-backed setup and operations guide that
  documents durable storage, worker settings, tenancy, optional background
  activity scheduling, and scheduler cleanup limitations.
- `DOC-047` API reference:
  completed on 2026-07-13 with a release-backed API & Client map covering
  management routes, client interfaces, permission claims, and the anonymous
  bookmark-resume callback contract.
- `DOC-038` Distributed tracing:
  completed on 2026-07-10 with a release-backed guide that separates
  `Elsa.Workflows` instrumentation from the `Elsa.Diagnostics.OpenTelemetry`
  collector, documents OTLP routes and permissions, and clarifies the
  host-specific status of gRPC and `Elsa.OpenTelemetry`.
- `DOC-039` Performance tuning:
  completed on 2026-07-11 with release-backed commit-strategy, mediator-worker,
  transactional-outbox, and telemetry guidance. The prior non-existent
  commit-strategy APIs and unverified tuning recipes were removed.

- `DOC-066` External Authentication extension authoring: document custom
  protocol adapters, unlinked-identity policies, user matchers, permission
  grant sources, descriptor schemas, and custom Studio editors as the next
  follow-on to the Elsa 3.8 External Authentication operations guide.

## Critical Priority (Must Have - Block Users)

### Latest slice note

- `DOC-050` Studio platform integration: completed on 2026-07-17 with a
  release-backed guide for opt-in Studio-host registration, workflow-artifact
  submission, payload-reference constraints, and its separation from
  custom-elements embedding.

### DOC-001: V2 to V3 Migration Guide
- **Persona**: Backend Integrator, Architect
- **Lifecycle Stage**: Install, Configure
- **Description**: Complete migration guide from Elsa V2 to V3, covering breaking changes, custom activities, workflows, and concepts
- **References**: Issues #23, #86 (elsa-gitbook)
- **Estimated Effort**: 3-5 days
- **Impact**: Unblocks existing users from upgrading
- **Acceptance Criteria**:
  - Migration checklist for all major components
  - Breaking changes documented with examples
  - Custom activity migration patterns
  - Workflow JSON migration guide
  - Code examples before/after
  - Common migration pitfalls & solutions

### DOC-002: Database Configuration Guide
- **Persona**: Backend Integrator, Platform/DevOps
- **Lifecycle Stage**: Configure
- **Description**: Complete guide for configuring SQL Server, PostgreSQL, MongoDB with EF Core
- **References**: Issues #2, #6, #11 (elsa-gitbook)
- **Estimated Effort**: 2-3 days
- **Impact**: Resolves #1 setup blocker
- **Acceptance Criteria**:
  - Step-by-step for each database provider
  - Connection string examples
  - Migration command examples
  - Multi-database scenarios
  - Troubleshooting section
  - Performance optimization tips

### DOC-003: Authentication & Authorization Setup
- **Persona**: All personas
- **Lifecycle Stage**: Configure
- **Description**: Comprehensive authentication guide covering OIDC, API keys, Azure AD, Auth0, custom providers
- **References**: Issues #16, #29, #327, #568, #334, #595 (elsa-studio)
- **Estimated Effort**: 3-4 days
- **Impact**: Resolves authentication confusion
- **Acceptance Criteria**:
  - OIDC configuration with multiple providers
  - API key setup and management
  - Custom authentication provider example
  - Studio authentication configuration
  - Troubleshooting auth errors (401, 404)
  - Security best practices

### DOC-004: Fix Hello World Examples
- **Persona**: All personas
- **Lifecycle Stage**: Discover, Install
- **Description**: Update Hello World examples to work with Elsa V3
- **References**: Issue #72 (elsa-gitbook)
- **Estimated Effort**: 0.5 days
- **Impact**: First impression for new users
- **Acceptance Criteria**:
  - Console example works
  - ASP.NET Core example works
  - All using statements correct
  - Code compiles and runs
  - Output verified

### DOC-005: Fix HTTP Workflows Guide
- **Persona**: Workflow Designer, Backend Integrator
- **Lifecycle Stage**: Design
- **Description**: Fix broken HTTP workflows designer tutorial
- **References**: Issues #69, #71, #85 (elsa-gitbook)
- **Estimated Effort**: 1-2 days
- **Impact**: Critical learning path broken
- **Acceptance Criteria**:
  - API endpoints work (fix regres.in issue)
  - Correct URLs throughout
  - C# syntax examples work
  - LIQUID examples work
  - All steps verified
  - Screenshots updated

### DOC-006: Docker Quickstart
- **Persona**: Platform/DevOps, Backend Integrator
- **Lifecycle Stage**: Install
- **Description**: Quick Docker deployment guide for Elsa Server + Studio
- **References**: Multiple user requests
- **Estimated Effort**: 1-2 days
- **Impact**: Fast path to evaluation
- **Acceptance Criteria**:
  - Single docker-compose.yml
  - Database persistence configured
  - Environment variables documented
  - Health checks included
  - Troubleshooting section
  - Production considerations noted

### DOC-007: Custom Activities V3 Complete Rewrite
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Extend
- **Description**: Complete rewrite of custom activities guide for V3
- **References**: Issues #30, #80 (elsa-gitbook)
- **Estimated Effort**: 3-4 days
- **Impact**: Core extensibility blocked
- **Acceptance Criteria**:
  - Basic activity example
  - Activity with inputs/outputs
  - Blocking activity example
  - Trigger activity example
  - Dependency injection
  - Registration patterns
  - UI hints usage
  - All code verified for V3

## High Priority (Major Impact)

### DOC-008: Architecture Overview
- **Persona**: Architect, Backend Integrator
- **Lifecycle Stage**: Discover
- **Description**: High-level and detailed architecture documentation
- **References**: Issue #14 (elsa-gitbook)
- **Estimated Effort**: 2-3 days
- **Impact**: Understanding system design
- **Acceptance Criteria**:
  - High-level architecture diagram
  - Execution flow from start to end
  - Execute vs Dispatch explained
  - Bookmarks + Triggers + Stimuli
  - Workflow Execution internals
  - Component interaction diagrams
  - Multi-tenancy architecture

### DOC-009: Kubernetes Deployment
- **Persona**: Platform/DevOps
- **Lifecycle Stage**: Deploy/Scale
- **Description**: Complete Kubernetes deployment guide
- **References**: Issues #35, #75 (elsa-gitbook)
- **Estimated Effort**: 3-4 days
- **Impact**: Production deployment
- **Acceptance Criteria**:
  - K8s manifests/Helm charts
  - Horizontal scaling configuration
  - Distributed locking setup
  - Database integration
  - Secrets management
  - Health checks & readiness probes
  - Monitoring integration
  - Troubleshooting guide

### DOC-010: Studio Tour & Onboarding
- **Persona**: Workflow Designer
- **Lifecycle Stage**: Design
- **Description**: Guided tour of Elsa Studio UI with best practices
- **References**: Multiple user requests
- **Estimated Effort**: 2 days
- **Impact**: New user onboarding
- **Acceptance Criteria**:
  - Annotated screenshots of every screen
  - Workflow creation walkthrough
  - Instance management tour
  - Settings & configuration
  - Keyboard shortcuts
  - Tips & tricks
  - Common workflows

### DOC-011: Testing & Debugging Workflows
- **Persona**: Workflow Designer, Backend Integrator
- **Lifecycle Stage**: Run/Debug
- **Description**: Comprehensive troubleshooting guide for failed workflows
- **References**: High user demand
- **Estimated Effort**: 2-3 days
- **Impact**: Reduce support burden
- **Acceptance Criteria**:
  - Reading workflow logs
  - Understanding execution journal
  - Common error patterns
  - Debug techniques
  - Testing strategies
  - Breakpoint equivalent
  - Data loss prevention (depth 3+ issue)

### DOC-012: Blocking & Trigger Activities
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Extend
- **Description**: How to create blocking activities and triggers
- **References**: Issue #18 (elsa-gitbook), Issue #80 (elsa-gitbook)
- **Estimated Effort**: 2 days
- **Impact**: Advanced extensibility
- **Acceptance Criteria**:
  - Blocking activity pattern
  - Bookmark creation
  - Trigger implementation
  - Resume mechanisms
  - Complete examples
  - Best practices

### DOC-013: Studio Integration Guide
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Install, Extend
- **Description**: Embedding Studio in React, Angular, Blazor, MVC
- **References**: Issue #13 (elsa-gitbook), #661, #656 (elsa-studio)
- **Estimated Effort**: 3 days
- **Impact**: Integration scenarios
- **Acceptance Criteria**:
  - React integration example
  - Angular integration example
  - Blazor integration example
  - MVC/Razor Pages example
  - Shadow DOM support (#550)
  - Authentication flow
  - Custom branding

### DOC-014: Clustering & Distributed Hosting
- **Persona**: Platform/DevOps, Architect
- **Lifecycle Stage**: Deploy/Scale
- **Description**: Distributed hosting patterns, clustering, distributed locking
- **References**: Issues #22, #41 (elsa-gitbook)
- **Estimated Effort**: 2-3 days
- **Impact**: Multi-instance deployment
- **Acceptance Criteria**:
  - Distributed locking configuration
  - Load balancing strategies
  - Session affinity considerations
  - Database clustering
  - Cache distribution
  - Quartz.NET clustering
  - Troubleshooting

### DOC-015: Monitoring & Observability
- **Persona**: Platform/DevOps
- **Lifecycle Stage**: Observe
- **Description**: Production monitoring with Prometheus, Grafana, distributed tracing
- **References**: High user demand
- **Estimated Effort**: 2-3 days
- **Impact**: Operational readiness
- **Acceptance Criteria**:
  - Prometheus metrics endpoint
  - Grafana dashboard examples
  - Key metrics to monitor
  - Alerting rules
  - Distributed tracing setup
  - Log aggregation patterns
  - Performance baselines

## Medium Priority (Significant Value)

### DOC-016: Workflow Context V3
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Design
- **Description**: V3 version of Workflow Context documentation
- **References**: Issue #20 (elsa-gitbook)
- **Estimated Effort**: 1-2 days

### DOC-017: Common Workflow Patterns
- **Persona**: All personas
- **Lifecycle Stage**: Design
- **Description**: Library of common workflow patterns
- **Estimated Effort**: 2 days

### DOC-018: Plugins & Modules Development
- **Persona**: Backend Integrator, Architect
- **Lifecycle Stage**: Extend
- **Description**: Guide for developing Elsa plugins and modules
- **References**: Issue #73 (elsa-gitbook)
- **Estimated Effort**: 3 days

### DOC-019: HTTP Endpoint Security
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Configure, Design
- **Description**: Securing HTTP endpoints exposed by workflows
- **References**: Issue #37 (elsa-gitbook)
- **Estimated Effort**: 1-2 days

### DOC-020: EF Core Migrations
- **Persona**: Backend Integrator, Platform/DevOps
- **Lifecycle Stage**: Configure, Deploy/Scale
- **Description**: Custom EF Core migrations guide
- **References**: Issue #74 (elsa-gitbook)
- **Estimated Effort**: 1 day

### DOC-021: Configuration Management
- **Persona**: Platform/DevOps
- **Lifecycle Stage**: Configure
- **Description**: Production configuration management (secrets, env vars, etc.)
- **Estimated Effort**: 1-2 days

### DOC-022: Scaling & Performance
- **Persona**: Platform/DevOps, Architect
- **Lifecycle Stage**: Deploy/Scale
- **Description**: Horizontal scaling patterns and performance optimization
- **Estimated Effort**: 2 days

### DOC-023: Identity Provider Integrations
- **Persona**: Backend Integrator, Architect
- **Lifecycle Stage**: Configure
- **Description**: Azure AD, Auth0, IdentityServer4, OpenIddict integration
- **References**: Issue #16 (elsa-gitbook)
- **Estimated Effort**: 2-3 days

### DOC-024: MassTransit Communication
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Extend
- **Description**: Communication between Elsa Server and other services via MassTransit
- **References**: Issues #19, #24 (elsa-gitbook)
- **Estimated Effort**: 1-2 days

### DOC-025: Long-Running Workflows
- **Persona**: Backend Integrator, Workflow Designer
- **Lifecycle Stage**: Design
- **Description**: Patterns for long-running workflows
- **Estimated Effort**: 1-2 days

### DOC-026: Error Handling & Retry Logic
- **Persona**: Backend Integrator, Workflow Designer
- **Lifecycle Stage**: Design
- **Description**: Error handling patterns and retry strategies
- **Estimated Effort**: 1-2 days

### DOC-027: Execution Model
- **Persona**: Backend Integrator, Architect
- **Lifecycle Stage**: Discover
- **Description**: Detailed execution model documentation
- **References**: Part of issue #14 (elsa-gitbook)
- **Estimated Effort**: 1-2 days

### DOC-028: Studio Customization
- **Persona**: Workflow Designer, Backend Integrator
- **Lifecycle Stage**: Extend
- **Description**: Customizing Studio UI, themes, branding
- **References**: Issues #12, #33, #34 (elsa-gitbook), multiple studio issues
- **Estimated Effort**: 2 days

### DOC-029: Custom UI Hints
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Extend
- **Description**: Creating custom UI hints for activities
- **References**: Issue #33 (elsa-gitbook), #309 (elsa-studio)
- **Estimated Effort**: 1 day

### DOC-030: Custom UI Components
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Extend
- **Description**: Creating custom UI components for Studio
- **References**: Issue #12 (elsa-gitbook), #434 (elsa-studio)
- **Estimated Effort**: 2 days

## Low Priority (Nice to Have)

### DOC-031: Custom Icons
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Extend
- **Description**: Providing custom icons for activities
- **References**: Issue #34 (elsa-gitbook)
- **Estimated Effort**: 0.5 days

### DOC-032: Workflow Providers
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Extend
- **Description**: Dynamic workflow providers
- **References**: Issue #39 (elsa-gitbook)
- **Estimated Effort**: 1 day

### DOC-033: Registering Custom Types
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Extend
- **Description**: How to register custom types with Elsa
- **References**: Issue #7 (elsa-gitbook)
- **Estimated Effort**: 0.5 days

### DOC-034: DropIns Module
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Extend
- **Description**: Using the DropIns module
- **References**: Issue #31 (elsa-gitbook)
- **Estimated Effort**: 1 day

### DOC-035: Webhook Extensibility
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Extend
- **Description**: Extending Webhooks module with custom events
- **References**: Issue #10 (elsa-gitbook)
- **Estimated Effort**: 1 day

### DOC-036: Activity Type Providers
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Extend
- **Description**: Creating activity type providers
- **Estimated Effort**: 1 day

### DOC-037: Alterations
- **Persona**: Backend Integrator, Platform/DevOps
- **Lifecycle Stage**: Observe
- **Description**: Port and expand Alterations documentation
- **References**: Issue #9 (elsa-gitbook)
- **Estimated Effort**: 1 day

### DOC-038: Distributed Tracing
- **Persona**: Platform/DevOps
- **Lifecycle Stage**: Observe
- **Description**: Setting up distributed tracing
- **Estimated Effort**: 1-2 days

### DOC-039: Performance Tuning
- **Persona**: Platform/DevOps, Architect
- **Lifecycle Stage**: Observe
- **Description**: Performance tuning guide
- **Estimated Effort**: 1-2 days

### DOC-040: Timer & Scheduled Workflows
- **Persona**: Workflow Designer, Backend Integrator
- **Lifecycle Stage**: Design
- **Description**: Timer-triggered and scheduled workflows guide
- **Estimated Effort**: 1 day

### DOC-041: Loading Workflows from JSON
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Design
- **Description**: Update existing guide (minor fixes)
- **References**: Issue #5 (elsa-gitbook)
- **Estimated Effort**: 0.5 days

### DOC-042: Bulk Dispatch Workflows Activity
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Design
- **Description**: Document bulk dispatch functionality
- **References**: Issue #17 (elsa-gitbook)
- **Estimated Effort**: 0.5 days

### DOC-043: Hangfire Integration
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Configure
- **Description**: Hangfire background scheduling integration
- **Estimated Effort**: 1 day

### DOC-044: Community Resources
- **Persona**: All personas
- **Lifecycle Stage**: Discover
- **Description**: Reference community projects and resources
- **References**: Issue #36 (elsa-gitbook)
- **Estimated Effort**: 1 day

### DOC-045: Case Studies
- **Persona**: Architect, all personas
- **Lifecycle Stage**: Discover
- **Description**: Organizations using Elsa in production
- **References**: Issue #82 (elsa-gitbook)
- **Estimated Effort**: Ongoing

### DOC-046: FAQ
- **Persona**: All personas
- **Lifecycle Stage**: All
- **Description**: Comprehensive FAQ from issues and support
- **Estimated Effort**: 1-2 days

### DOC-047: API Reference
- **Persona**: Backend Integrator
- **Lifecycle Stage**: Reference
- **Description**: Complete API reference documentation
- **Estimated Effort**: 3-5 days

### DOC-048: Activity Reference
- **Persona**: All personas
- **Lifecycle Stage**: Reference
- **Description**: Complete reference for all built-in activities
- **References**: Issue #88 (elsa-gitbook)
- **Estimated Effort**: 3-4 days

### DOC-049: Studio Custom Elements Embedding
- **Persona**: Backend Integrator, Workflow Designer
- **Lifecycle Stage**: Install, Extend
- **Description**: Dedicated cookbook for embedding Elsa Studio components into existing web apps using the custom-elements host, including backend credentials, tenant headers, and framework wrappers.
- **Estimated Effort**: 1-2 days

### DOC-067: Weaver and AI Workflow Assistance

- **Persona**: Workflow Designer, Backend Integrator, CTO
- **Lifecycle Stage**: Explore, Govern, Operate
- **Description**: Explain Elsa's release-backed AI host, Weaver Studio module,
  grounding and proposal APIs, permissions, provider configuration, audit
  boundaries, and safe workflow-draft review.
- **Evidence**: Core `Elsa.AI.Host` and Studio `Elsa.Studio.AI` modules in
  `release/3.8.0`.
- **Estimated Effort**: 2-3 days

## Implementation Roadmap

### Week 1-2: Critical Blockers
- DOC-001: V2 to V3 Migration
- DOC-002: Database Configuration
- DOC-003: Authentication Setup
- DOC-004: Fix Hello World
- DOC-005: Fix HTTP Workflows Guide
- DOC-006: Docker Quickstart

### Week 3-4: Major Impact
- DOC-007: Custom Activities Rewrite
- DOC-008: Architecture Overview
- DOC-009: Kubernetes Deployment
- DOC-010: Studio Tour
- DOC-011: Testing & Debugging
- DOC-012: Blocking & Trigger Activities

### Week 5-6: High Value
- DOC-013: Studio Integration
- DOC-014: Clustering & Distributed Hosting
- DOC-015: Monitoring & Observability
- DOC-016: Workflow Context V3
- DOC-017: Common Workflow Patterns
- DOC-018: Plugins & Modules

### Week 7-8: Polish & Complete
- DOC-019 through DOC-030 (medium priority)
- DOC-031 through DOC-048 (low priority, as time permits)
- Final QA, link checks, visual polish

## Success Metrics

- **Issues Resolved**: 40+ documentation-related issues closed
- **Coverage Improvement**: 80%+ of persona-stage cells with ✅ rating
- **Time to First Workflow**: < 30 minutes (down from hours)
- **Support Reduction**: 40% fewer configuration questions
- **User Satisfaction**: NPS score > 40 for documentation
