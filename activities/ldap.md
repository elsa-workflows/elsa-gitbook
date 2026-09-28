---
description: Configure LDAP connections and use LDAP directory activities in Elsa Workflows 3.9.0.
---

# LDAP activities

The `Elsa.Ldap` extension lets workflows read and change entries in an LDAP
directory through the server. It uses
`System.DirectoryServices.Protocols` and provides activities for searching,
comparing, adding, modifying, moving, and deleting entries.

Use this extension when a workflow must coordinate with an existing LDAP
directory, such as an enterprise directory or Active Directory deployment. It
does not provision a directory, manage schemas or users outside the configured
activities, or provide a general LDAP administration screen.

The extension is server-side. Elsa Studio receives the activity descriptors
from the server and can show a dropdown for configured connection names, but
Studio does not open LDAP connections itself.

## Install and register the module

Install `Elsa.Ldap` in the application that executes workflows:

```bash
dotnet add package Elsa.Ldap
```

Use the package version compatible with the Elsa packages already used by your
host. The behavior described here is the `release/3.9.0` implementation.

Register the module during Elsa startup. This example uses a default
connection, so activities can leave **Connection Name** empty:

```csharp
using Elsa.Extensions;
using Elsa.Ldap.Options;

builder.Services.AddElsa(elsa =>
{
    elsa.UseLdap(ldap =>
    {
        ldap.ConfigureOptions = options =>
        {
            options.AddDefaultConnection(new LdapConnectionOptions
            {
                Host = configuration["Ldap:Host"]!,
                Port = configuration.GetValue<int>("Ldap:Port", 389),
                UseSsl = configuration.GetValue<bool>("Ldap:UseSsl"),
                BindDn = configuration["Ldap:BindDn"],
                BindPassword = configuration["Ldap:BindPassword"]
            });
        };
    });
});
```

`UseLdap` registers the seven LDAP activities, the connection factory, and the
property UI handler that supplies connection-name choices to clients. The
package depends on `System.DirectoryServices.Protocols`.

## Configure connections

`LdapOptions` stores named connections in a case-insensitive dictionary. Use
`AddDefaultConnection` for the conventional default name `Default`, or use
`AddConnection` when a workflow needs to choose between directories:

```csharp
ldap.ConfigureOptions = options =>
{
    options.AddDefaultConnection(new LdapConnectionOptions
    {
        Host = "ldap.example.com",
        Port = 636,
        UseSsl = true,
        BindDn = configuration["Ldap:BindDn"],
        BindPassword = configuration["Ldap:BindPassword"]
    });

    options.AddConnection("regional-directory", new LdapConnectionOptions
    {
        Host = "ldap.eu.example.com",
        Port = 389,
        UseSsl = false,
        BindDn = configuration["Ldap:RegionalBindDn"],
        BindPassword = configuration["Ldap:RegionalBindPassword"]
    });
};
```

The connection settings have these behaviors:

| Setting | Behavior |
| --- | --- |
| `Host` | Required hostname or IP address. |
| `Port` | Defaults to `389`; `636` is the common TLS port. |
| `UseSsl` | Defaults to `false` and maps to `SecureSocketLayer`. Use it with the port and certificate policy required by the directory. |
| `BindDn` and `BindPassword` | When `BindDn` is supplied, the module uses a basic bind with those credentials. When it is `null`, the module uses anonymous authentication. |
| `ReferralChasing` | Defaults to `ReferralChasingOptions.None`; set it explicitly when the directory topology requires referral chasing. |

An activity with no connection name resolves `Default`. A name that is not
registered causes connection creation to throw an `InvalidOperationException`.
The extension creates and disposes a directory connection for each activity
execution; it does not add a connection pool, retry policy, or secret store.

Keep bind passwords in deployment configuration or a secret store. Do not put
them in workflow definitions, expressions, or activity inputs.

## Use the activities

All LDAP activities are in the **LDAP** category. Each activity that connects
to a directory has a **Connection Name** input. In Studio, that input is
populated from the names registered in `LdapOptions`; the dropdown does not
discover connections from LDAP or create them.

### Search a single entry

**Search single LDAP entry** uses `Base DN`, an LDAP `Filter`, `Scope`, and an
optional list of `Attributes`. Its default scope is `SearchScope.Base`, and it
limits the request to one result. It provides:

- `Found` or `Not Found` outcomes.
- A serializable `Search Result` dictionary whose keys are attribute names and
  whose values are string arrays.
- Journal data containing the directory `ResultCode` and whether a match was
  found.

Use it when the workflow needs one known entry, for example:

```csharp
using System.DirectoryServices.Protocols;
using Elsa.Ldap.Activities;

var findPerson = new SearchLdapEntry
{
    ConnectionName = new("Default"),
    BaseDn = new("uid=alice,ou=People,dc=example,dc=com"),
    Filter = new("(objectClass=person)"),
    Scope = new(SearchScope.Base),
    Attributes = new(new[] { "cn", "mail" })
};
```

### Search multiple entries

**Search all LDAP entries** uses the same inputs, but its default scope is
`SearchScope.Subtree` and it returns all entries from the response as
serializable attribute dictionaries. Its outcomes identify the result count:

- `No entries found`
- `One entry found`
- `Multiple entries found`

Use a narrow base DN, filter, and attribute list when a directory contains
many entries. The activity does not add a paged-results control, so use the
directory's expected response limits and test large searches before using them
in production.

### Compare an attribute

**Compare LDAP entry** sends a directory compare request for an `Entry DN` and
one `DirectoryAttribute`. It sets its Boolean result to `true` only when the
server returns `CompareTrue`, and completes with `True` or `False`.

Use this for a directory-side assertion, such as checking a membership or
status attribute before choosing a workflow path. It is not a general-purpose
expression evaluator.

### Add, modify, move, and delete entries

The write activities map directly to LDAP directory requests:

| Activity | Inputs | Result and outcomes |
| --- | --- | --- |
| **Add LDAP entry** | `Entry DN`, `Attributes` (`DirectoryAttribute` values) | Boolean result; `Success` or `Failure`. |
| **Modify LDAP entry** | `Entry DN`, `Modifications` (`DirectoryAttributeModification` values) | Boolean result; `Success` or `Failure`. |
| **Move LDAP entry** | `Entry DN`, optional `New Parent DN`, `New CN` | Boolean result; `Success` or `Failure`. Leaving `New Parent DN` empty renames within the current parent. |
| **Delete LDAP entry** | `Entry DN` | Boolean result; `Success` or `Failure`. |

These inputs use the native `System.DirectoryServices.Protocols` request types.
When building workflows in code, create the attributes and modifications
explicitly:

```csharp
using System.DirectoryServices.Protocols;
using Elsa.Ldap.Activities;

var addGroup = new AddLdapEntry
{
    ConnectionName = new("Default"),
    EntryDn = new("cn=workflow-users,ou=Groups,dc=example,dc=com"),
    Attributes = new(new[]
    {
        new DirectoryAttribute("objectClass", "groupOfNames"),
        new DirectoryAttribute("cn", "workflow-users")
    })
};
```

For a successful or unsuccessful LDAP response, the write activities set the
Boolean result and choose the corresponding outcome. They also journal the
directory result code and the entry involved; `Add LDAP entry` journals the
attribute count and `Modify LDAP entry` journals the modification count.
Transport or bind exceptions are not converted into a `Failure` outcome, so
handle those at the workflow or host boundary according to your error policy.

## Studio and deployment boundaries

Install and register `Elsa.Ldap` on the server that executes the workflow. If
Studio is connected to that server, it can receive the LDAP activity
descriptors and the configured connection-name dropdown. Installing the NuGet
package only in a Studio frontend does not add the activities or make Studio
able to reach the directory.

The module does not provide:

- an LDAP connection-management page in Elsa Studio;
- a standalone directory or schema administration UI, certificate management,
  or access-rule provisioning;
- a workflow API for storing bind credentials separately from application
  configuration; or
- automatic retry, paging, or transaction semantics across multiple LDAP
  activities.

Provision the directory and its access rules outside Elsa, and grant the bind
identity only the permissions required by the workflows that use it.

## Troubleshooting

### The LDAP activities are missing from Studio

Confirm that the server application references `Elsa.Ldap`, calls `UseLdap`,
and has restarted. Check the server's activity descriptors. A frontend-only
installation is not sufficient.

### The connection-name dropdown is empty

The dropdown enumerates the keys registered in `LdapOptions`. Add a default or
named connection and verify that `UseLdap` is part of the same Elsa module
configuration used by the executing server.

### Connection creation fails

Check the resolved connection name, host, port, and TLS setting. If the name is
unknown, the module reports that no LDAP connection with that name was found.
If `BindDn` is null, the module attempts an anonymous bind; otherwise it uses a
basic bind with `BindDn` and `BindPassword`.

### A search returns an unexpected result

Check the base DN, LDAP filter syntax, search scope, and requested attributes.
`Search single LDAP entry` uses `Base` scope and returns at most one entry by
design; `Search all LDAP entries` uses `Subtree` scope by default and chooses
an outcome based on the number of entries returned.

### A write activity reports `Failure`

Inspect the server logs and activity journal for the LDAP `ResultCode`, then
check the bind identity's directory permissions and the target DN. A failure
response is represented by the Boolean result and outcome; an exception during
connection or request transport follows the host's normal exception handling.

## Release source

This page describes the Elsa Extensions `release/3.9.0` implementation:

- [`UseLdap` and module registration](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/ldap/Elsa.Ldap/Extensions/ModuleExtensions.cs)
- [`LdapFeature` service and activity registration](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/ldap/Elsa.Ldap/Features/LdapFeature.cs)
- [`LdapOptions` named connections](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/ldap/Elsa.Ldap/Options/LdapOptions.cs)
- [`LdapConnectionOptions` binding and TLS settings](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/ldap/Elsa.Ldap/Options/LdapConnectionOptions.cs)
- [`LdapConnectionFactory` resolution and bind behavior](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/ldap/Elsa.Ldap/Services/LdapConnectionFactory.cs)
- [`Search single LDAP entry`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/ldap/Elsa.Ldap/Activities/SearchLdapEntry.cs)
- [`Search all LDAP entries`](https://github.com/elsa-workflows/elsa-extensions/blob/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/ldap/Elsa.Ldap/Activities/SearchLdapEntries.cs)
- [`Add`, `modify`, `move`, `delete`, and `compare` activities](https://github.com/elsa-workflows/elsa-extensions/tree/9d9049e63184a3a1ba5d259061d5b51f2456a841/src/modules/ldap/Elsa.Ldap/Activities)
- [`LDAP activity tests`](https://github.com/elsa-workflows/elsa-extensions/tree/9d9049e63184a3a1ba5d259061d5b51f2456a841/test/modules/ldap/Elsa.Ldap.UnitTests)
