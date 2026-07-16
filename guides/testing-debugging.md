---
description: Test custom Elsa activities and workflows with the released Elsa testing fixtures, then diagnose real workflow instances with the journal and runtime tools.
---

# Testing & Debugging Workflows

Test the smallest useful unit first, then test the workflow's routing and
runtime configuration. This guide uses the testing helpers in Elsa Core
`release/3.8.0`; their source is the best reference when your test needs a
feature that is not shown here.

<!-- markdownlint-disable MD013 -->
| What you need confidence in | Start with | What to assert |
| --- | --- | --- |
| A custom activity's inputs, outputs, or service call | `ActivityTestFixture` | The service interaction, output, or execution context |
| Branching, sequencing, outcomes, and registered workflow behavior | `WorkflowTestFixture` | Workflow status and the activity journal |
| HTTP, persistence, authentication, or a message broker | Your application's integration test host | The public boundary and persisted result |
| A workflow already running outside a test | Elsa Studio and the runtime APIs | Instance state, journal, incidents, and logs |
<!-- markdownlint-enable MD013 -->

## Add the testing helpers

Create an xUnit test project and keep the fixture packages on the same Elsa
version as the packages used by the workflow application. Do not mix fixture
and runtime versions.

```bash
dotnet add package Elsa.Testing.Shared --version x.y.z
dotnet add package Elsa.Testing.Shared.Integration --version x.y.z
dotnet add package NSubstitute
```

`Elsa.Testing.Shared` supplies the focused activity fixture.
`Elsa.Testing.Shared.Integration` supplies the workflow fixture and its
journal-oriented assertion helpers. Both expose their types through the
`Elsa.Testing.Shared` namespace. If the matching version is prerelease, add
`--prerelease` or specify that prerelease version explicitly.

## Unit-test a custom activity

Use `ActivityTestFixture` when the behavior belongs to one activity. It builds
a minimal workflow execution context, registers the activity type, evaluates
its input properties, and executes the activity. It already includes the core
workflow services; register only the dependencies specific to the activity.

The following test follows the released Elsa Core test pattern for `WriteLine`:

{% code title="WriteLineTests.cs" %}

```csharp
using Elsa.Testing.Shared;
using Elsa.Workflows;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

public class WriteLineTests
{
    [Fact]
    public async Task Writes_the_configured_text()
    {
        var writer = Substitute.For<TextWriter>();
        var streamProvider = Substitute.For<IStandardOutStreamProvider>();
        streamProvider.GetTextWriter().Returns(writer);

        var activity = new WriteLine("Order accepted");

        await new ActivityTestFixture(activity)
            .ConfigureServices(services => services.AddSingleton(streamProvider))
            .ExecuteAsync();

        writer.Received(1).WriteLine("Order accepted");
    }
}
```

{% endcode %}

For your own activity, replace the standard-output provider with the narrow
interface the activity depends on and assert its call. This keeps tests
deterministic and avoids starting a server or database just to test business
logic.

### Configure state deliberately

`ConfigureServices(...)` adds fakes, options, or application services before
the fixture builds its service provider. `ConfigureContext(...)` receives the
`ActivityExecutionContext` immediately before the activity runs. Use the latter
only when the behavior depends on workflow state, variables, or correlation
that cannot be expressed through normal activity inputs.

```csharp
var context = await new ActivityTestFixture(activity)
    .ConfigureContext(
        context => context.WorkflowExecutionContext.CorrelationId = "order-42")
    .ExecuteAsync();
```

Assert an observable result: a call to a substituted dependency, an output on
`context`, or a state change. Avoid asserting the fixture's implementation
details.

## Test routing with a workflow fixture

`WorkflowTestFixture` is the next level up. Its baseline configuration adds
core activities, scheduling, C#, JavaScript, Liquid, workflow management, and
an xUnit-backed output stream. It builds and activates the test services on
first use.

For an in-memory workflow, run an activity or workflow and inspect the returned
journal. The helper methods below are provided by the released fixture package.

{% code title="RoutingTests.cs" %}

```csharp
using Elsa.Testing.Shared;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Xunit;
using Xunit.Abstractions;

public class RoutingTests(ITestOutputHelper output)
{
    private readonly WorkflowTestFixture _fixture = new(output);

    [Fact]
    public async Task Runs_the_selected_branch()
    {
        var selected = new WriteLine("selected");
        var skipped = new WriteLine("skipped");
        var flow = new If
        {
            Condition = new(() => true),
            Then = selected,
            Else = skipped
        };

        var result = await _fixture.RunActivityAsync(flow);

        result.AssertWorkflowCompleted();
        result.AssertActivitiesCompleted(flow, selected);
        result.AssertActivityNotExecuted(skipped);
        Assert.Contains("selected", _fixture.CapturingTextWriter.Lines);
    }
}
```

{% endcode %}

The result's journal records the activity execution contexts. Use
`AssertActivityCompleted`, `AssertActivityNotExecuted`, and
`AssertActivityExecutionCount` to assert control flow rather than relying only
on output text. `GetActivityStatus(...)` and `GetOutcomes(...)` on the fixture
are useful when a test needs the raw status or named outcomes.

### Test registered definitions and custom activity assemblies

When a test must exercise registration rather than an in-memory activity,
configure the fixture before its first run:

```csharp
private readonly WorkflowTestFixture _fixture = new(output)
    .AddActivitiesFrom<MyCustomActivity>()
    .AddWorkflow<OrderWorkflow>();

var result = await _fixture.RunWorkflowAsync<OrderWorkflow>();
result.AssertWorkflowCompleted();
```

`ConfigureElsa(...)` adds Elsa features needed by the behavior under test, and
`ConfigureServices(...)` adds application services. The fixture can also load
workflow definitions from a relative directory with
`WithWorkflowsFromDirectory(...)`. Use those options when testing a workflow
definition or activity package as it is registered, not merely its code path.

## Test the boundary that can fail in production

The fixtures do not replace integration tests for a host. Add a small number of
tests through your application's actual boundary when a workflow depends on:

- HTTP routing, authentication, or request/response behavior
- persistence, transactions, or a distributed cache
- timers, queues, broker consumers, or background workers
- external service contracts

Keep these tests scenario-focused: start or resume the workflow through the
same boundary production uses, then assert the durable workflow state and the
external effect. For workflows that wait, test both sides: bookmark creation
and the stimulus that resumes it. See [Long-running Workflows](running-workflows/long-running-workflows.md)
for the waiting and resumption model.

## Diagnose a workflow that is not a test failure

Automated tests explain expected behavior; the execution journal explains what
happened to a particular instance. For an issue found in Studio or production:

1. Reproduce with representative, non-sensitive inputs if possible.
2. Find the instance and inspect its status, journal entries, activity records,
   incidents, and variables using [Investigate a Workflow Instance](../operate/workflow-state-and-journal.md).
3. Compare the executed activity path with the workflow test that covers the
   same rule; add a regression test before changing the workflow.
4. Use [Troubleshooting](troubleshooting/README.md) for host logs, database,
   scheduler, and clustered-runtime checks. Add [Distributed Tracing](../operate/distributed-tracing.md)
   when a request crosses services.

For interactive designer checks, the Studio tour explains the execution journal
and supported activity testing in [Studio Tour & Troubleshooting](../studio/studio-tour-troubleshooting.md).

## Release-backed references

- [ActivityTestFixture source](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/common/Elsa.Testing.Shared/ActivityTestFixture.cs)
- [WorkflowTestFixture source](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/common/Elsa.Testing.Shared.Integration/WorkflowTestFixture.cs)
- [Journal assertion helpers](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/src/common/Elsa.Testing.Shared.Integration/RunWorkflowResultAssertions.cs)
- [Released activity-fixture test example](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/test/unit/Elsa.Activities.UnitTests/Console/WriteLineTests.cs)
