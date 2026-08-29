---
description: Read and write files from Elsa 3.8.0 workflows with a configurable storage provider.
---

# File storage activities

The `Elsa.Storage.Files` extension adds **Open file** and **Save file** to
the `Elsa / Storage` category. Use them when a workflow needs to read or
write a file through a configured `FluentStorage.Blobs.IBlobStorage`
provider.

These activities run on the Elsa Server. Elsa Studio only edits their inputs;
it does not read the browser's filesystem or move the data itself. The file
storage provider is separate from Elsa's workflow-instance persistence: a
file written by `Save file` is not automatically a workflow variable or a
workflow-definition artifact.

## Install and register the extension

Install the package in the application that executes workflows:

```bash
dotnet add package Elsa.Storage.Files --version 3.8.0
```

Register the module during Elsa configuration:

```csharp
using Elsa.Extensions;

builder.Services.AddElsa(elsa =>
{
    elsa.UseFileStorage();
});
```

`UseFileStorage()` registers the two activities and an
`IBlobStorageProvider`. The provider wrapper keeps the file-storage
registration distinct from any other `IBlobStorage` services in the host.

### Choose the storage backend

With no configuration, the feature creates a directory-backed provider rooted
at:

```text
{temporary-directory}/Elsa/Storage/Blobs
```

For a different `FluentStorage` backend, use the overload that supplies a
factory. This example keeps the same directory provider but chooses an
application-controlled root:

```csharp
using Elsa.Extensions;
using FluentStorage;
using FluentStorage.Blobs;

var fileRoot = Path.Combine(AppContext.BaseDirectory, "workflow-files");

builder.Services.AddElsa(elsa =>
{
    elsa.UseFileStorage(_ => StorageFactory.Blobs.DirectoryFiles(fileRoot));
});
```

The factory receives the application's `IServiceProvider`, so it can select
or construct a provider from host configuration. `UseFileStorage` only wires
the provider used by these activities; it does not provision directories,
cloud containers, credentials, or an application-specific retention policy.
Those responsibilities belong to the selected storage backend and host.

## Choose an activity

- **Open file** — accepts `Path` (`string`) and returns an activity result of
  type `Stream`. Use it when a later step must consume stored contents.
- **Save file** — accepts `Data` (`object`), `Path` (`string`), and `Append`
  (`bool`), and has no output. Use it to write one value or a collection of
  values.

`Path` is the blob or provider path passed to `IBlobStorage`; its exact
mapping to a physical or remote location is defined by the selected provider.
It is not an Elsa workflow-instance ID and it is not automatically made
unique.

## Open a file

**Open file** resolves `Path` and calls the provider's `OpenReadAsync` method.
Its result is an open `Stream` that downstream activities can read. The
activity does not convert the result to text, JSON, or a byte array.

For example, a programmatic workflow can bind the result to a later activity:

```csharp
using Elsa.Storage.Files.Activities;
using Elsa.Workflows.Models;

var open = new OpenFile
{
    Path = new Input<string>("invoices/2026/00042.pdf")
};

// The activity result is a Stream. Pass it to an activity that accepts a
// stream, or copy it into an application-owned destination before disposal.
```

The underlying `IBlobStorage` contract allows a provider to return no stream
when the path does not exist, and providers can surface their own storage
errors. Treat a missing or unreadable path as an expected failure case in
workflow design rather than assuming that every path is present.

## Save a file

**Save file** resolves `Data`, converts it to a stream, and calls
`IBlobStorage.WriteAsync(Path, stream, Append, cancellationToken)`. The
release supports these `Data` shapes:

| `Data` value | Conversion in 3.8.0 |
| --- | --- |
| `Stream` | Passed directly to the storage provider. |
| `byte[]` | Wrapped in a `MemoryStream`. |
| `IFormFile` | Read through `OpenReadStream()`. |
| `string` | Encoded as UTF-8 bytes. It is treated as content, not as a path. |
| `IEnumerable` with one item | The item is converted recursively. |
| `IEnumerable` with zero or multiple items | Written as a generated ZIP stream. |

Unsupported values cause the activity to throw `NotSupportedException`.
Prefer a stream for large content when the producing activity already has one;
the byte-array and generated-ZIP paths allocate additional in-memory
representations.

### Single file example

This example writes UTF-8 text to the configured provider:

```csharp
using Elsa.Storage.Files.Activities;
using Elsa.Workflows.Models;

var save = new SaveFile
{
    Data = new Input<object>("Invoice 00042 is approved."),
    Path = new Input<string>("invoices/2026/00042.txt"),
    Append = new Input<bool>(false)
};
```

When the activity receives a `Stream`, the stream is what the provider reads.
Make sure a stream produced by an earlier activity is still open and positioned
for reading when `Save file` runs. The activity does not expose a saved URI,
byte count, ETag, or other provider metadata as an output.

### Multiple values become a ZIP

When `Data` is an enumerable with more than one item, `Save file` creates a
ZIP archive in memory and writes that archive to the single `Path`. Entries
are named `file-0.bin`, `file-1.bin`, and so on; the original item filenames
are not retained by this implementation. An empty enumerable follows the same
archive path. A one-item enumerable is unwrapped and written using that item's
normal conversion rules.

There is an important 3.8.0 limitation: the implementation does not dispose
or otherwise finalize its `ZipArchive` before returning the `MemoryStream`.
The generated collection archive can therefore be incomplete when the storage
provider reads it. Do not use this collection form for a portable ZIP contract
without verifying the resulting archive. When archive validity or entry names
matter, build and finalize the archive in a dedicated/custom step, then pass
the completed stream or byte array to `Save file`.

Use an explicit archive-building activity when entry names, compression
settings, archive layout, or archive validity are part of the business
contract. Use `Save file`'s collection behavior only when its 3.8.0 generated
archive limitation is acceptable.

### Append behavior

`Append` defaults to `false` when it is not supplied. The value is passed to
the provider as the `append` argument: `false` requests a normal write and
`true` requests append behavior where the selected provider supports it.
Confirm the provider's overwrite and append semantics before relying on them
for audit logs, retries, or concurrent workflow instances.

## Use the activities in Elsa Studio

After the server is configured and restarted, connect Studio to that server
and search the activity picker for **Open file** or **Save file** in
**Storage**. Configure:

1. `Path` with a provider path, preferably derived from controlled workflow
   data.
2. For `Save file`, `Data` with a literal, expression, stream-producing
   activity result, byte array, form file, string, or supported collection.
3. For `Save file`, `Append` explicitly when the workflow depends on its
   behavior.

Studio gets activity descriptors from the connected server. Installing the
package only in a Studio host does not make these activities executable; the
Elsa Server must register the extension and provide the storage provider.

## Security and operations

- Treat `Path` as untrusted when it is derived from HTTP input, messages, or
  user-entered workflow data. The Elsa activities do not add authorization or
  path validation; validate allowed prefixes, names, and lengths at the
  workflow boundary and choose a provider with suitable isolation.
- The default root is under the process temporary-directory path. It is useful
  for development, but it is not a durable or shared-storage policy for a
  multi-node deployment. Select a durable, shared provider when instances can
  resume on another node or when files must survive host replacement.
- Storage access happens on the server. Keep local paths, provider endpoints,
  and credentials in server configuration; do not put secrets into workflow
  definitions or expose them through Studio inputs.
- A stream returned by **Open file** is an execution-time resource. If a
  workflow waits or dispatches work, persist a provider path or durable file
  identifier rather than assuming the open stream remains usable.
- Plan for retries and duplicate execution. Choose deterministic or unique
  paths deliberately, and decide whether overwriting or appending is safe for
  the workflow's idempotency requirements.

## Troubleshooting

**The activities are missing from Studio**

Confirm that `Elsa.Storage.Files` is installed in the execution host, that
`UseFileStorage()` runs during Elsa configuration, and that Studio is
connected to that server.

**A file cannot be opened**

Check the resolved provider path, the server's access to the selected backend,
and the provider's missing-file behavior. A path in Studio is not a path on
the Studio user's computer.

**The saved file is a ZIP or has unexpected content**

Check the runtime type of `Data`. Multiple enumerable items intentionally
become a generated ZIP, and a string is written as UTF-8 content rather than
being interpreted as a source filename. In 3.8.0, the generated ZIP is not
explicitly finalized; use a dedicated/custom archive step when the consumer
requires a valid portable ZIP.

**A retry changes the file unexpectedly**

Review `Append`, path uniqueness, and the selected provider's write semantics.
The activity does not add a separate deduplication or transaction policy.

## Release source

The behavior described here is based on `release/3.8.0` in the following
repositories:

- [`OpenFile`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/storage/Elsa.Storage.Files/Activities/OpenFile.cs)
- [`SaveFile`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/storage/Elsa.Storage.Files/Activities/SaveFile.cs)
- [`FileStorageFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/storage/Elsa.Storage.Files/Features/BlobStorageFeature.cs)
- [`UseFileStorage`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/storage/Elsa.Storage.Files/Extensions/ModuleExtensions.cs)
- [`IBlobStorageProvider`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/storage/Elsa.Storage.Files/Contracts/IStorageProvider.cs)
- [`FluentStorage` package reference](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/storage/Elsa.Storage.Files/Elsa.Storage.Files.csproj)
