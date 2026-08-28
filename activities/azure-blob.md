---
description: Upload JSON data to Azure Blob Storage from an Elsa 3.8.0 workflow.
---

# Azure Blob upload

The `Elsa.Storage.AzureStorage` extension provides one workflow activity,
**Upload blob**. It serializes a non-null value as JSON and uploads the result
to an existing Azure Blob Storage container as a block blob.

This is a server-side activity. The Elsa Server needs network access to Azure
Storage and the authority represented by the container URI. Studio only edits
the activity inputs; it does not upload the data from the browser.

## Install and register the activity

Install the package in the application that executes workflows:

```bash
dotnet add package Elsa.Storage.AzureStorage --version 3.8.0
```

In 3.8.0, this package does not provide a `UseAzureStorage()` extension method.
`AzureStorageFeature` registers the client factories, but does not register
the activity itself. Configure both explicitly:

```csharp
using Elsa.Extensions;
using Elsa.Storage.AzureStorage.Activities;
using Elsa.Storage.AzureStorage.Features;

builder.Services.AddElsa(elsa =>
{
    elsa.Use<AzureStorageFeature>();
    elsa.AddActivitiesFrom<UploadBlob>();
});
```

`AddActivitiesFrom<UploadBlob>()` scans the assembly containing `UploadBlob`
for exported activity types. If you omit either registration, the activity
cannot execute; if you omit the activity registration, it also will not appear
in the server's activity descriptors for Studio.

## Configure the activity

The activity has three inputs and no output:

| Input | Type | Release behavior |
| --- | --- | --- |
| `Content` | `object` | Serialized as JSON using the runtime type of the value. The value must be non-null and JSON-serializable. |
| `ConnectionString` | `string` | Despite its name, 3.8.0 parses this value as a `Uri` for an Azure **container**, not as a conventional `DefaultEndpointsProtocol=...` account connection string. |
| `BlobName` | `string` | Used as the blob path, with `.json` appended unconditionally. |

For example, the following targets the `workflow-files` container using a SAS
URI and creates `orders/2026/00042.json`:

```csharp
using Elsa.Storage.AzureStorage.Activities;
using Elsa.Workflows.Models;

var upload = new UploadBlob
{
    Content = new Input<object>(new
    {
        OrderId = "00042",
        Status = "Approved"
    }),
    ConnectionString = new Input<string>(
        "https://storage-account.blob.core.windows.net/workflow-files?<sas-token>"),
    BlobName = new Input<string>("orders/2026/00042")
};
```

The activity does not return the URI, ETag, or upload response. If a later
step needs the location, keep the container URI and blob-name expression as
workflow data or use a custom activity that exposes the result you need.

## What is uploaded?

The activity is a JSON uploader, not a general binary-file uploader.

- A string is uploaded as a JSON string value, including its quotes.
- A `byte[]` is serialized according to `System.Text.Json` rather than copied
  as raw binary bytes.
- Objects must be supported by the runtime JSON serializer.
- The generated JSON is split into blocks and committed to Azure. The
  activity uses the uploader's 4 MiB approximate block-size and maximum
  concurrency of four defaults; it does not expose those settings as inputs.

Serialization is piped into the uploader instead of first constructing one
complete JSON string. Pending blocks are still buffered in memory while they
are staged, so set reasonable payload limits and account for concurrent
workflow executions.

The implementation obtains a `BlobContainerClient` from the supplied URI and
gets a `BlockBlobClient` for the resulting name. It does not create the
container. Provision the container first and grant the SAS or other URI
authority permission to stage and commit blocks.

## Use it from Elsa Studio

After the server is configured and restarted, connect Studio to that server
and search for **Upload blob** in the **Storage** category. Configure:

1. `Content` with a literal or expression that evaluates to the JSON value.
2. `ConnectionString` with the server-reachable container URI.
3. `BlobName` with the path stem, without the `.json` suffix.

Studio has no Azure-specific connection picker or browser-side upload path for
this activity in the 3.8.0 release. The inputs are sent to and evaluated by
the server that runs the workflow. Installing the NuGet package in a Studio
host alone does not make the activity available or executable.

## Security and operations

- Prefer a short-lived, least-privilege SAS URI or another host-controlled way
  to supply a scoped container URI. The `ConnectionString` input is still a
  raw string input in 3.8.0; the activity does not use Elsa's Connections API.
- Do not commit account keys or long-lived SAS tokens in workflow definitions,
  examples, or logs. If the value is supplied through an expression, ensure
  the expression source is protected and that activity input logging is
  appropriate.
- Treat `BlobName` as untrusted data when it is derived from external input;
  validate naming and length rules at the workflow boundary.
- Use a unique name when previous blobs must be retained. Reusing a name
  targets the same blob path and can replace its committed block list.
- Monitor activity failures and Azure Storage diagnostics. Authentication,
  invalid URI, missing-container, network, staging, and commit errors fault
  the activity; the source does not add a separate retry or dead-letter policy.

## Troubleshooting

**The activity is missing from Studio**

Confirm that `Elsa.Storage.AzureStorage` is installed in the execution host,
that `Use<AzureStorageFeature>()` and `AddActivitiesFrom<UploadBlob>()` run
during Elsa configuration, and that Studio is connected to that server.

**The URI is rejected**

Use a valid container URI such as
`https://account.blob.core.windows.net/container?<sas-token>`. A conventional
storage-account connection-string value is not accepted by the 3.8.0 activity
implementation despite the input label.

**The upload fails with a storage resource error**

Check that the container already exists and that the URI's credentials permit
block staging and block-list commit. The activity does not provision the
container.

**The blob has an unexpected name or content**

Remember that `.json` is appended to every `BlobName`, and that `Content` is
serialized as JSON. This activity cannot be used to upload an arbitrary raw
file stream without a custom activity or a different integration.

## Release source

The behavior described here is based on the `release/3.8.0` branches:

- [`UploadBlob`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/storage/Elsa.Storage.AzureStorage/Activities/UploadBlobActivity.cs)
- [`AzureStorageFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/storage/Elsa.Storage.AzureStorage/Features/AzureStorageFeature.cs)
- [`BlobContainerClientFactory`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/storage/Elsa.Storage.AzureStorage/Services/BlobContainerClientFactory.cs)
- [`ParallelBlockBlobUploader`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/storage/Elsa.Storage.AzureStorage/Extensions/ParallelBlockBlobUploader.cs)
- [`JsonStreamer`](https://github.com/elsa-workflows/elsa-extensions/blob/a44e2b09af1202ff4936f493756e114c357eff81/src/modules/storage/Elsa.Storage.AzureStorage/Serialization/JsonStreamer.cs)
- [`IModule.Use<T>`](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/common/Elsa.Features/Extensions/DependencyInjectionExtensions.cs)
- [`AddActivitiesFrom<TMarker>`](https://github.com/elsa-workflows/elsa-core/blob/dff7d9f987394c3c2ba8003e6f9c803e97194fbc/src/modules/Elsa.Workflows.Management/Extensions/ModuleExtensions.cs)
