---
description: Normalize workflow content and create ZIP archives with Elsa 3.8.0.
---

# I/O and compression activities

The `Elsa.IO` and `Elsa.IO.Compression` extensions provide server-side content
handling for workflows. `Elsa.IO` resolves common input representations to a
binary stream; `Elsa.IO.Compression` adds **Create Zip Archive**, which writes
those representations into an in-memory ZIP archive.

Use this extension when a workflow needs to package files or generated content
before passing the result to another activity. The archive is a `Stream`, not a
file saved to a storage provider. Elsa Studio configures the activity through
descriptors returned by the server; it does not read local files or perform the
compression in the browser.

## Install and register the modules

Install the package in the Elsa Server that executes the workflow:

```bash
dotnet add package Elsa.IO.Compression --version 3.8.0
```

Register the compression feature during Elsa startup:

```csharp
using Elsa.Extensions;

builder.Services.AddElsa(elsa =>
{
    elsa.UseCompression();
});
```

`UseCompression()` registers the `CreateZipArchive` activity and its
`ZipEntry` variable type. The compression feature depends on `Elsa.IO`, so
the content resolver used by the activity is installed as part of this setup.
You only need to call `UseIO()` separately when another feature or custom
activity uses `IContentResolver` without enabling compression.

### Enable HTTP URL inputs separately

The base I/O module does not download URLs. To let the resolver fetch `http://`
and `https://` inputs, install `Elsa.IO.Http` and explicitly configure its
feature as well:

```bash
dotnet add package Elsa.IO.Http --version 3.8.0
```

```csharp
using Elsa.Extensions;
using Elsa.IO.Http.Features;

builder.Services.AddElsa(elsa =>
{
    elsa.UseCompression();
    elsa.Use<IOHttpFeature>();
});
```

The HTTP feature adds an `HttpClient` and a URL content-resolution strategy.
It is an outbound server request made with the URL supplied to the workflow;
it is not a Studio upload and has no built-in allowlist, authentication, or
response-size policy. Apply your host's outbound-network and SSRF controls
before accepting URLs from users or external events.

## What content can be resolved?

`IContentResolver` chooses the first registered strategy that recognizes the
input. The forms available in the release are:

| Input | Default name | Behavior |
| --- | --- | --- |
| `Stream` | `data.bin`, or the file name for a `FileStream` | Uses the supplied stream without taking ownership of it. |
| `byte[]` | `data.bin` | Wraps the bytes in a memory stream. |
| Base64 `string` | `data.bin`, or a type-derived name for a `data:` URI | Decodes the value. `data:` Base64 URIs can provide a MIME type. |
| Existing file-path `string` | The file name | Opens the file on the Elsa Server. Relative paths are resolved against the process working directory after path normalization. |
| Other `string` | `text.txt` | Encodes the string as UTF-8 text. |
| HTTP/HTTPS URL `string` with `Elsa.IO.Http` | Response or URL-derived name | Downloads the response and uses its `Content-Disposition`, URL path, or content type to choose a name. |
| `ZipEntry` | The wrapped content's name, or the supplied name | Resolves the wrapped content and optionally overrides its archive entry name. |

The order matters for strings. An existing path is treated as a file, a
recognised Base64 value is decoded, and everything else becomes UTF-8 text.
Without `IOHttpFeature`, an HTTP URL is just ordinary text because the base
I/O package has no URL strategy.

## Create a ZIP archive

`CreateZipArchive` appears in the **Elsa / Compression** category. It has two
inputs and one output:

| Property | Type | Behavior |
| --- | --- | --- |
| `Entries` | `object?` | One content value or an array of content values. The release resolves each value through `IContentResolver`. |
| `CompressionLevel` | `System.IO.Compression.CompressionLevel` | Controls ZIP entry compression. Defaults to `Optimal`. |
| `Result` | `Stream` | A readable, position-reset ZIP archive held in a `MemoryStream`. |

Use `ZipEntry` when the archive needs stable names rather than resolver-derived
names:

```csharp
using System.IO.Compression;
using Elsa.IO.Compression.Models;
using Elsa.IO.Compression.Activities;
using Elsa.Workflows.Models;

var createArchive = new CreateZipArchive
{
    Entries = new Input<object?>(new object[]
    {
        new ZipEntry("Order approved\n", "notice.txt"),
        new ZipEntry(new byte[] { 0x01, 0x02, 0x03 }, "payload.bin")
    }),
    CompressionLevel = new Input<System.IO.Compression.CompressionLevel>(System.IO.Compression.CompressionLevel.Optimal)
};
```

In a designer, provide an expression or literal that evaluates to the same
content forms and use the activity's `Result` output in a later activity that
accepts a stream. The output has not been written to disk or object storage;
choose and configure a storage or transfer activity separately.

### Naming and duplicate entries

When a content value has a name, the activity keeps that name and adds an
extension when one is missing. Unnamed byte arrays and streams use names such
as `data.bin`; plain text uses `text.txt`; an unnamed entry otherwise falls
back to `entry_1`, `entry_2`, and so on. If two entries resolve to the same
name, later entries are renamed using the Windows-style pattern
`name(1).ext`, `name(2).ext`, and so on.

For deterministic archive contents, pass an array or an enumerable of
`ZipEntry` values with explicit names. The 3.8.0 parser expands `Array` and
`IEnumerable<object>` inputs; a single value is treated as one archive entry.

### Failures and resource behavior

Each entry is processed independently. If resolution or copying fails, the
activity logs a warning for that entry and continues; the workflow can still
complete with a partial or empty archive. A failure creating the archive as a
whole faults the activity. Monitor server logs when archive completeness is
important rather than treating successful activity completion as proof that
every entry was included.

The archive is built entirely in memory and is rewound before it is returned.
The archive grows with every output entry, so bound input size and entry count
at the host or earlier in the workflow. The
activity disposes resolver-created streams such as file, byte-array, Base64,
and downloaded-content streams. It does not dispose a caller-supplied
`Stream`, so the owner of that stream remains responsible for its lifetime.

## Studio and security boundaries

- Install and register the packages in the server that executes workflows.
  Installing them only in a Studio host does not make the activity executable.
- After the server exposes its activity descriptors, search the **Compression**
  category for **Create Zip Archive**. Studio does not add a separate archive
  browser, file picker, storage provider, or URL security policy.
- Treat file paths and URLs as privileged server inputs. A workflow that can
  control them may read local files or make outbound requests from the worker.
- Validate archive size, entry count, allowed paths, URL schemes, destinations,
  and content types before processing untrusted input. Do not place credentials
  in URLs or log resolved secret content.
- If archive data must survive a restart or be downloaded by a user, explicitly
  persist or transfer the `Stream` using a storage or HTTP response activity;
  the compression activity itself provides no durable file and no download
  endpoint.

## Troubleshooting

1. If the activity is absent, confirm `Elsa.IO.Compression` is installed in
   the execution host and `.UseCompression()` runs during startup.
2. If Studio cannot find it, verify that Studio is connected to that server
   and that the server exposes the registered activity descriptors.
3. If a URL is packaged as text, install `Elsa.IO.Http` and configure
   `IOHttpFeature`; `UseCompression()` alone does not enable downloads.
4. If an archive is missing entries, inspect server warnings for failed entry
   resolution and use explicit `ZipEntry` names to remove naming ambiguity.
5. If the worker runs out of memory, reduce the input batch and archive size or
   stream the data through a storage/transfer design instead of building one
   in-memory archive.
6. If a file path cannot be opened, check the path from the worker's point of
   view, its permissions, and its process working directory. Studio's local
   filesystem is not the worker's filesystem.

## Release source

This page is validated against the `release/3.8.0` source at the following
refs:

- [`Elsa.IO` module registration and resolver](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/io/Elsa.IO/Features/IOFeature.cs)
- [`UseIO` module extension](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/io/Elsa.IO/Extensions/ModuleExtensions.cs)
- [`UseCompression` module extension](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/io/Elsa.IO.Compression/Extensions/ModuleExtensions.cs)
- [`IContentResolver` contract](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/io/Elsa.IO/Contracts/IContentResolver.cs)
- [`ContentResolver` strategy selection](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/io/Elsa.IO/Services/ContentResolver.cs)
- [`CompressionFeature` registration and `ZipEntry` alias](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/io/Elsa.IO.Compression/Features/CompressionFeature.cs)
- [`CreateZipArchive` inputs, output, naming, and execution](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/io/Elsa.IO.Compression/Activities/CreateZipArchive.cs)
- [`ZipEntry` model and content strategy](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/io/Elsa.IO.Compression/Models/ZipEntry.cs)
- [`IOHttpFeature` and URL strategy](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/io/Elsa.IO.Http/Features/IOHttpFeature.cs)
- [`UrlContentStrategy`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/io/Elsa.IO.Http/Services/Strategies/UrlContentStrategy.cs)
- [`IModule.Use<T>` generic feature registration](https://github.com/elsa-workflows/elsa-core/blob/f6c35cf1eb5558348a61352fc4eba5925731118d/src/common/Elsa.Features/Extensions/DependencyInjectionExtensions.cs)

See also [Activity Reference](activity-reference.md), [Common Properties](common-properties.md), and [Secrets Management](../guides/security/secrets-management.md).
