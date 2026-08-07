---
description: Find official Elsa learning, support, release, and contribution resources.
---

# Community & Resources

Use this page to choose the right place to learn, ask a question, report a
problem, or contribute. The links below are the official Elsa resources
available for Elsa 3; community channels are external sites and can change.

## Start with the documentation

- [Elsa documentation](https://docs.elsaworkflows.io/) for concepts, setup,
  workflow design, Studio, and operations.
- [Elsa Workflows website](https://www.elsaworkflows.io/) for product context,
  announcements, and Elsa+ services.
- [Elsa core repository](https://github.com/elsa-workflows/elsa-core) for the
  workflow engine, server, APIs, activities, and release notes.
- [Elsa Studio repository](https://github.com/elsa-workflows/elsa-studio) for
  the modular dashboard and Studio extension points.
- [Elsa extensions repository](https://github.com/elsa-workflows/elsa-extensions)
  for integration modules developed outside the core repository. Check each
  module's status and README before adopting it.

When you are evaluating an example or answer, check its package versions and
source branch. A page written for Elsa 2 or an older Elsa 3 release can use
different namespaces, APIs, or hosting modules.

## Choose a support channel

| Your goal | Best starting point | What to include or expect |
| --- | --- | --- |
| Learn a concept or follow a setup path | [Documentation](https://docs.elsaworkflows.io/) and the [Elsa samples](https://github.com/elsa-workflows/elsa-samples) | Start with the guide that matches your host type and Elsa version. |
| Ask how to approach a design or integration | [GitHub Discussions](https://github.com/elsa-workflows/elsa-core/discussions) | Describe the business goal, hosting model, package versions, and the smallest relevant workflow. |
| Report a reproducible core or server defect | [Core issues](https://github.com/elsa-workflows/elsa-core/issues) | Include a minimal reproduction, expected behavior, actual behavior, and logs. |
| Report a Studio defect | [Studio issues](https://github.com/elsa-workflows/elsa-studio/issues) | Include the Studio host model, browser, server URL shape, and whether the issue reproduces with the release version. |
| Report an extension defect | [Extensions issues](https://github.com/elsa-workflows/elsa-extensions/issues) | Name the integration package, provider version, host configuration, and external-service response. |
| Search or ask a short technical question | [Stack Overflow questions tagged `elsa-workflows`](https://stackoverflow.com/questions/tagged/elsa-workflows) | Search first, then provide a focused question and a runnable or reproducible example. |
| Talk to people in real time | [Elsa Discord](https://discord.gg/hhChk5H472) | Use a concise summary and link back to a Discussion or issue when the answer should be searchable. |
| Check what changed or what is released | [Core releases](https://github.com/elsa-workflows/elsa-core/releases) and [NuGet](https://www.nuget.org/) | Verify the package version, release notes, and compatibility before upgrading. |
| Ask about commercial tooling or long-term support | [Elsa+](https://www.elsaworkflows.io/elsa-plus) | Use this path when your organization needs services or a supported product offering. |

Issues and Discussions are different entry points: use an Issue when there is
a concrete defect or feature request, and use a Discussion when you need
feedback on an approach or have a question that is not yet a confirmed bug.

## Find examples and integrations

Use the repositories according to the kind of example you need:

- **Code-first workflows and server behavior:** browse the [core source and
  reference apps](https://github.com/elsa-workflows/elsa-core/tree/release/3.8.0).
- **Visual design and embedded Studio:** browse the [Studio source](https://github.com/elsa-workflows/elsa-studio/tree/release/3.8.0)
  and the [Studio integration guides](studio/README.md).
- **External services:** browse the [extensions source](https://github.com/elsa-workflows/elsa-extensions/tree/release/3.8.0),
  then open the integration's README and project file before copying its
  registration code.
- **End-to-end learning examples:** browse the [Elsa samples repository](https://github.com/elsa-workflows/elsa-samples).

Treat a repository's `main` branch as development material unless the example
explicitly targets your package version. For a production upgrade, prefer a
release tag or branch and compare the package versions used by the example
with the versions in your application.

## Ask a question that can be answered

Before posting, collect the following details:

1. Elsa version and the package names involved.
2. .NET version and host shape: standalone server, embedded server, Studio
   WASM, Blazor, or another ASP.NET Core application.
3. Persistence, scheduling, messaging, or authentication modules involved.
4. A small workflow definition or code sample that demonstrates the issue.
5. Expected behavior, actual behavior, and the first relevant exception or
   log entry.
6. Whether the behavior reproduces with a clean release-based sample.

For Studio questions, also state whether the problem is in the designer, the
server API, or the connection between them. For integration questions, name
the external provider and distinguish an Elsa error from a provider response.

## Share diagnostic material safely

Remove secrets before sharing workflow JSON, configuration, HTTP traces, or
screenshots. In particular, redact API keys, JWTs, OAuth client secrets,
connection strings, cookies, personal data, and customer identifiers. Replace
them with stable placeholders so the reproduction remains understandable.

If the issue is security-sensitive, do not publish exploit details or
credentials in a public Issue, Discussion, Discord message, or Stack Overflow
question. Use the repository's private security-reporting path instead.

## Contribute back

- Improve this documentation repository when a repeated question exposes a
  missing or misleading guide.
- Open an issue before a large change so the maintainers can confirm scope.
- Use a pull request for a focused fix, include the Elsa version you validated,
  and link the relevant issue or Discussion.
- For code contributions, follow the [core contributing guidance](https://github.com/elsa-workflows/elsa-core/blob/release/3.8.0/CONTRIBUTING.md),
  [Studio contributing guidance](https://github.com/elsa-workflows/elsa-studio/blob/release/3.8.0/CONTRIBUTING.md),
  or [extensions contributing guidance](https://github.com/elsa-workflows/elsa-extensions/blob/release/3.8.0/CONTRIBUTING.md).

## Release boundary for this guide

The repository and channel map was checked against the `release/3.8.0`
source repositories on 2026-08-07:

- [Core README at the validated release commit](https://github.com/elsa-workflows/elsa-core/blob/5429008d98a56afd29b4fd11107f7760710b1a64/README.md)
- [Studio README at the validated release commit](https://github.com/elsa-workflows/elsa-studio/blob/7c6bac091d62a8f8fe8273332e65158b45b60326/README.md)
- [Extensions README at the validated release commit](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/README.md)

These source files confirm the official repository boundaries and shared
Discord and Stack Overflow channels. They do not guarantee that every
third-party sample, extension, or community answer remains maintained; check
the source and package metadata before adopting one.
