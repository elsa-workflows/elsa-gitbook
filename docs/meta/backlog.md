# Prioritized Documentation Backlog

This backlog is prioritized by user impact and frequency of complaints
based on gap analysis from 161 issues across elsa-studio and
elsa-gitbook.

## Slice Inventory (2026-07-10)

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
- `DOC-028` Studio customization
- `DOC-029` Custom UI hints
- `DOC-030` Custom UI components
- `DOC-040` Timer and scheduled workflows
- `DOC-041` Loading workflows from JSON
- `DOC-042` Bulk dispatch workflows activity
- `DOC-049` Studio custom-elements embedding cookbook
- `DOC-053` Alterations operational guide hardening

### Available next slices

- `DOC-039` Performance tuning
- `DOC-043` Hangfire integration
- `DOC-047` API reference
- `DOC-048` Activity reference

### Recommended next slice

- `DOC-039` Performance tuning: distributed tracing is now covered, but
  production teams still need a release-backed tuning guide that connects
  worker count, dispatcher behavior, persistence tradeoffs, and telemetry into
  concrete scale-up decisions.

### Newly discovered follow-on topics

- `DOC-050` Studio platform integration: add a focused guide for
  `AddPlatformIntegrationModule`, workflow submission widgets, and when to
  use that integration path instead of custom-elements embedding alone.
- `DOC-051` Activity and workflow testing cookbook: add a focused guide for
  `Elsa.Testing.Shared`, `ActivityTestFixture`, and `WorkflowTestFixture`
  so custom activity authors can validate behavior without reverse
  engineering test setup from source.
- `DOC-052` Workflow state and journal API cookbook: add an operations-facing
  guide for inspecting workflow state, filtered journal entries, activity
  executions, and variable mutation endpoints when diagnosing live instances.
- `DOC-054` Standalone versus modular configuration matrix: add a compact
  reference that maps `AddElsa(...)` host settings to `CShells` feature
  settings so teams can migrate between hosting models without reverse
  engineering sample apps.
- `DOC-055` Message broker topology cookbook: add an operations-facing guide
  for queue naming, temporary endpoint lifetime, consumer placement, and broker
  cleanup tradeoffs across MassTransit, Azure Service Bus activities, and
  distributed cache invalidation.
- `DOC-056` Custom resilience extensibility: add a focused guide for
  `IResilienceStrategy`, `AddResilienceStrategyType<T>()`, and
  `WithRetryAttemptRecorder(...)` so teams can persist retry diagnostics and
  introduce non-HTTP resilience policies without reverse engineering tests and
  feature registration.
- `DOC-057` Elsa API permission reference: add a compact map of common Elsa API
  and Studio operations to `permissions` claim values, plus starter role
  templates for external identity providers.

### Most recent completed slice

- `DOC-038` Distributed tracing:
  completed on 2026-07-10 with a release-backed guide that separates
  `Elsa.Workflows` instrumentation from the `Elsa.Diagnostics.OpenTelemetry`
  collector, documents OTLP routes and permissions, and clarifies the
  host-specific status of gRPC and `Elsa.OpenTelemetry`.

## Critical Priority (Must Have - Block Users)

### Current slice note

- `DOC-039` Performance tuning:
  add a release-backed guide for runtime throughput tuning, worker counts,
  dispatcher and queue considerations, persistence pressure, and the telemetry
  signals operators should watch while scaling.

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
