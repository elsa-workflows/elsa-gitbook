---
description: >-
  Use the Elsa.DevOps.GitHub extension to call GitHub from workflows in Elsa
  3.8.0.
---

# GitHub

The `Elsa.DevOps.GitHub` extension adds server-side activities that call the
GitHub REST API through Octokit, plus one activity for sending a GraphQL query.
Use it when a workflow needs to inspect or update repositories, issues, pull
requests, comments, labels, users, organizations, milestones, or gists.

The extension is host-side functionality. It does not add a GitHub connection
editor or token-management page to Elsa Studio. If the Elsa Server connected to
Studio registers the extension, the server can expose its activity descriptors
to the generic activity picker.

> **Release boundary:** `release/3.8.0` includes three GitHub event watcher
> descriptors, but their execution path throws `NotImplementedException`. Do
> not use `WatchBranches`, `WatchIssues`, or `WatchPullRequests` as production
> triggers in this release. Use an HTTP webhook workflow and your own webhook
> validation instead.

## Install and register the extension

Install the server-side package:

```bash
dotnet add package Elsa.DevOps.GitHub
```

Register it while composing Elsa:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseGitHub();
});
```

`UseGitHub` registers the extension feature and its singleton
`GitHubClientFactory`. It does not configure a global token. Each GitHub
activity receives its token through its `Token` input.

## Provide the token safely

Every concrete GitHub activity inherits a `Token` input of type `string`. The
runtime passes that value to the client factory, which creates an Octokit
client with GitHub credentials and caches the client by the token value.

For code-first workflows, the shape is:

```csharp
using Elsa.DevOps.GitHub.Activities.Issues;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Contracts;
using Elsa.Workflows.Models;

public class CreateIssueWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new CreateIssue
                {
                    Token = new Input<string>("token-from-your-secret-store"),
                    Owner = new Input<string>("elsa-workflows"),
                    Repository = new Input<string>("elsa-extensions"),
                    Title = new Input<string>("Created by Elsa"),
                    Body = new Input<string>("Created by a workflow.")
                }
            }
        };
    }
}
```

The placeholder above is only a shape example. Do not commit a real token in a
workflow definition or source file. Resolve it from your application's secret
store and pass it as an expression or other protected input. See
[Secrets Management](../guides/security/secrets-management.md) for Elsa's
secret-reference and picker behavior.

Because the cache is process-local and keyed by the raw token, token ownership,
tenant isolation, rotation, and eviction remain application concerns. Avoid
logging the `Token` input or interpolating it into workflow output. Grant the
token only the GitHub permissions required by the activities that the workflow
can execute.

## Choose an activity

The release includes these concrete activities. Their inputs and outputs are
described by the activity descriptors, so the same names can appear in the
generic Studio picker when the server exposes them.

- **Issues:** `CreateIssue`, `GetIssue`, `UpdateIssue`, `DeleteIssue`, and
  `SearchIssues`. `DeleteIssue` closes the issue; GitHub does not permit this
  activity to delete it.
- **Comments:** `CreateComment`, `GetComment`, `UpdateComment`, and
  `DeleteComment` for issue or pull-request comments.
- **Labels:** `AddLabels` and `DeleteLabel` for issue or pull-request labels.
- **Pull requests:** `GetPullRequest` and `SearchPullRequests`.
- **Repositories:** `GetRepository`, `GetBranch`, and `SearchBranches`.
- **Users:** `AddAssignees`, `DeleteAssignees`, `GetAssignee`, `GetUser`, and
  `SearchAssignees`.
- **Organizations:** `GetOrganization` and `SearchOrganizationMembers`.
- **Milestones:** `GetMilestone` and `SearchMilestones`.
- **Gists:** `GetGist` and `SearchGists`.
- **GraphQL:** `ExecuteGraphQLQuery`.

Most activities take `Owner` and `Repository` plus a resource-specific input,
and return a typed Octokit model or search result. `GetUser` and the gist
activities use their own identifiers; inspect the activity descriptor when
binding expressions in Studio.

## Example: create an issue

`CreateIssue` sends an issue creation request and exposes the created `Issue` as
`CreatedIssue`. Its optional inputs include `Body`, `Labels`, `MilestoneId`,
and `Assignees`.

In Studio, set these inputs as literals or expressions:

1. Set `Token` to a protected secret expression.
2. Set `Owner`, `Repository`, and `Title`.
3. Add `Body`, labels, a milestone ID, or assignee usernames when needed.
4. Use the `CreatedIssue` output in a later activity or branch.

The same pattern applies to update, comment, label, and assignee activities:
the activity calls GitHub during execution and exposes the returned Octokit
model through its output.

## Search and pagination

`SearchIssues` and `SearchPullRequests` accept GitHub search syntax through
`Query`. They also expose optional `Page`, `PageSize`, `Sort`, and
`SortDirection` inputs. `SearchPullRequests` uses GitHub's issue search API and
adds `type:pr` to the query when it is not already present.

`SearchBranches` loads the repository's branches and applies its optional
`NameFilter` in memory using a case-insensitive partial match. It is not a
server-side GitHub search query.

Treat search results as paged external data. Choose a page size deliberately,
and do not assume that one activity invocation returns every matching item.

## Execute a GraphQL query

`ExecuteGraphQLQuery` sends an authorized `POST` request to GitHub's fixed
`https://api.github.com/graphql` endpoint. It accepts a query string and an
optional `Variables` string, then exposes the response root as the
`JsonElement` output `QueryResult`.

For example, a query without variables can be shaped like this:

```graphql
query {
  repository(owner: "elsa-workflows", name: "elsa-extensions") {
    name
    stargazerCount
  }
}
```

The release implementation serializes `Variables` as a JSON string property in
the request object; it does not parse that input into a JSON object. Verify the
payload against GitHub's GraphQL contract before relying on variables in a
production workflow. The activity also does not expose a configurable GraphQL
base URL.

## Webhook watchers: visible, but not executable in 3.8.0

The package contains these trigger descriptors:

- `WatchBranches` with `Owner`, `Repository`, and `BranchEvent`.
- `WatchIssues` with `Owner`, `Repository`, and `IssueEvent`.
- `WatchPullRequests` with `Owner`, `Repository`, and `PullRequestEvent`.

They inherit from `GitHubTriggerActivity`, whose `ExecuteAsync` method throws
`NotImplementedException("Event subscription requires WebHook implementation.")`.
The descriptors and README therefore do not represent a working webhook
subscription implementation in this release. For a working inbound design,
receive the GitHub webhook through an Elsa HTTP Endpoint, authenticate and
validate the GitHub signature in your host or front door, then map the payload
into workflow inputs. Review [HTTP endpoint security](../guides/security/http-endpoint-security.md)
before publishing that endpoint.

## Errors, rate limits, and retries

The built-in activities await Octokit calls directly and do not catch or
translate GitHub API errors. Authentication failures, missing permissions,
missing resources, validation errors, network failures, and rate-limit responses
therefore surface through normal Elsa activity fault handling.

Use workflow error handling and an explicit retry policy for transient failures.
Do not blindly retry mutations such as issue creation or comment creation:
combine retries with an application-level idempotency strategy when a duplicate
GitHub change would be harmful. Monitor GitHub's rate-limit response headers and
keep search page sizes within the limits of your GitHub plan.

## Studio and deployment boundaries

- The extension is registered on the Elsa Server, not in a standalone Studio
  client.
- The release contains no GitHub-specific Studio administration or connection
  editor.
- A server that does not register `UseGitHub` cannot execute these activity
  types, even if a workflow definition contains them.
- The package's `GitHubDevOpsShellFeature` is not a working Studio setup path
  in this release; its service configuration throws `NotImplementedException`.
- The release package contains no `GetRelease` or `SearchReleases` activity,
  despite those names appearing in the bundled README. Do not build a 3.8.0
  workflow around those names.

## Troubleshooting

### The activities are missing from Studio

Confirm that `Elsa.DevOps.GitHub` is installed and `UseGitHub()` is called by
the Elsa Server. Then check the server's activity descriptor response and
ensure Studio is connected to that server. Registering a package only in the
Studio client does not make the activity executable.

### GitHub returns 401 or 403

Check that the token is resolved to a non-empty value at runtime, has access to
the target repository, and has the permissions required by the operation. For
organization-owned repositories, also check organization policies and token
approval requirements.

### A watcher faults immediately

That is expected for `release/3.8.0`: the watcher base class is intentionally
unimplemented. Replace it with an authenticated HTTP webhook workflow or
implement and register a separate custom trigger.

### A search misses results

Check the GitHub search syntax, `Page`, `PageSize`, sort inputs, and rate-limit
responses. For pull requests, remember that the activity adds `type:pr` to the
query when needed.

## Release source

This guide is validated against `elsa-extensions` `release/3.8.0` at
[`335a264`](https://github.com/elsa-workflows/elsa-extensions/tree/335a26495318f6ee1528bf2723b7333c753ce9a2). The key implementation points are:

- [`UseGitHub`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/devops/Elsa.DevOps.GitHub/Extensions/ModuleExtensions.cs) and [`GitHubFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/devops/Elsa.DevOps.GitHub/Features/GitHubFeature.cs)
- [`GitHubClientFactory`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/devops/Elsa.DevOps.GitHub/Services/GitHubClientFactory.cs)
- [`GitHubActivity` token input](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/devops/Elsa.DevOps.GitHub/Activities/GitHubActivity.cs)
- [`ExecuteGraphQLQuery`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/devops/Elsa.DevOps.GitHub/Activities/GraphQL/ExecuteGraphQLQuery.cs)
- [`GitHubTriggerActivity`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/devops/Elsa.DevOps.GitHub/Activities/GitHubTriggerActivity.cs)
- [`GitHub activity source tree`](https://github.com/elsa-workflows/elsa-extensions/tree/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/devops/Elsa.DevOps.GitHub/Activities)
