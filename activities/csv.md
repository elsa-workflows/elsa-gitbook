---
description: Read and map CSV data in Elsa workflows with the 3.8.0 CSV extension.
---

# Read CSV data in a workflow

The `Elsa.Data.Csv` extension adds the `ReadCsv` activity. Use it when
a workflow needs to turn delimited text or an uploaded file into rows that
later activities can inspect, validate, transform, or dispatch.

The activity runs on the Elsa Server. Studio can design a workflow containing
it when the connected server registers the extension, but Studio does not
parse CSV data itself.

## Install and register the extension

Install the package in the server project that executes the workflow:

```bash
dotnet add package Elsa.Data.Csv --version 3.8.0
```

Register the feature during Elsa startup:

```csharp
using Elsa.Extensions;

builder.Services.AddElsa(elsa =>
{
    elsa.UseCsv();
});
```

`UseCsv` registers the extension feature, which discovers the `ReadCsv`
activity in the `Elsa / Data` category. There is no CSV connection, database,
or provider to configure.

## Configure `ReadCsv`

The activity has one data input, three parsing inputs, and one output:

| Property | Type | Behavior |
| --- | --- | --- |
| `CsvData` | `object?` | `string`, UTF-8 `byte[]`, `Stream`, or `IFormFile`; other values use `ToString()`. |
| `HasHeaderRecord` | `bool` | Treats the first row as column names. Defaults to `true`. |
| `RecordType` | `Type?` | Optional CLR type used for strongly typed row mapping through CsvHelper. |
| `Delimiter` | `string?` | Separates fields. Defaults to `,`; empty, `\\t`, and `tab` have defined fallbacks. |
| `Records` | `IList<object>` | Contains dictionaries when `RecordType` is empty, or mapped objects when it is set. |

Choose the input form based on where the data already lives:

- Use a `string` when a previous activity or expression already produced CSV
  text.
- Use a UTF-8 `byte[]` when the workflow receives encoded file content.
- Use a `Stream` or `IFormFile` for server-side file or upload handling.

The activity does not download URLs. A URL supplied as a string is parsed as
the literal CSV text of that string. This is also true even though the release
package README lists URLs among the supported string inputs; the implementation
does not contain a URL-fetch path. If the workflow must retrieve a remote file,
download it with an HTTP or storage integration first, then pass the resulting
bytes or stream to `ReadCsv`.

## Example: parse header-based rows

Given this input:

```csv
OrderId,Customer,Total
1001,Acme,12.50
1002,Contoso,7.25
```

configure these activity inputs:

| Input | Value |
| --- | --- |
| `CsvData` | The CSV text above, or an expression that returns it |
| `HasHeaderRecord` | `true` |
| `Delimiter` | `,` |
| `RecordType` | Leave empty |

`Records` contains dictionaries similar to:

```json
[
  { "OrderId": "1001", "Customer": "Acme", "Total": "12.50" },
  { "OrderId": "1002", "Customer": "Contoso", "Total": "7.25" }
]
```

Without `RecordType`, field values are read as strings. With headers enabled,
the dictionary keys are the header values returned by CsvHelper. If the input
has no header row, set `HasHeaderRecord` to `false`; rows then use keys such as
`Field1`, `Field2`, and so on.

## Example: map rows to a CLR type

Set `RecordType` to a type whose members match the CSV columns:

```csharp
public sealed class OrderRow
{
    public int OrderId { get; set; }
    public string Customer { get; set; } = "";
    public decimal Total { get; set; }
}
```

When `RecordType` is `typeof(OrderRow)`, the activity asks CsvHelper to map
each row to `OrderRow` and returns the mapped objects in `Records`. Use a
CsvHelper class map or supported mapping attributes when column names do not
match the type members. Conversion and mapping errors are not converted into
an alternate activity outcome; they fault the activity and should be handled
through the workflow's normal fault and incident strategy.

Typed mapping is useful when later activities need numeric, date, or domain
properties. Keep the input as dictionaries when the columns are dynamic or
when the workflow only needs to route or validate raw values.

## Delimiters and headers

The release passes the normalized delimiter and header setting to CsvHelper:

- Comma-separated data works with the default `,`.
- Semicolon-separated data can use `;`.
- Tab-separated data can use either the two-character value `\\t` or `tab`.
- Any non-empty delimiter string is passed through to CsvHelper.
- Set `HasHeaderRecord` to `false` for positional rows. Do not leave it at its
  default when the first row is data rather than column names.

The parser uses `CultureInfo.InvariantCulture`. Test typed numeric and date
columns with representative input from the producing system, especially when
that system formats values using locale-specific conventions.

## Runtime and memory behavior

`ReadCsv` reads the complete input into text, parses every row, and stores the
complete result in a list before completing. It is therefore best suited to
bounded files and batch-sized workflow inputs, not unbounded data streams.

The stream lifetime is an important server-side detail: the activity wraps a
supplied `Stream` in a `StreamReader` and disposes the reader after reading,
which also closes the supplied stream. Pass a stream whose ownership can end
at this activity, or create a separate stream when the caller must keep it
open. `IFormFile` streams are opened and disposed by the activity.

There is no built-in size limit, pagination, or incremental output. Enforce
upload and request limits at the host, validate the expected encoding and
delimiter before processing, and reject files that are too large for the
workflow worker's memory budget.

## Studio and security boundaries

- Register `Elsa.Data.Csv` on the server that executes workflows. Registering
  the package only in a Studio host does not make the activity executable.
- In Studio, search the **Data** category for `ReadCsv` after the server has
  registered the feature and exposed its activity descriptors.
- The activity does not fetch URLs, but it can process bytes, streams, and
  uploaded files supplied by the server. Apply authentication and authorization
  before accepting untrusted uploads or workflow inputs.
- Treat CSV content as untrusted data. Validate size, encoding, expected
  columns, and values before using them to construct requests, file paths, or
  database commands.
- If parsed values are later exported to a spreadsheet, apply your export
  layer's protections against formula injection; `ReadCsv` itself only parses
  the input.

## Troubleshooting checklist

1. Confirm `Elsa.Data.Csv` is installed in the workflow execution host and
   `.UseCsv()` runs during startup.
2. If Studio cannot find the activity, verify it is connected to that server
   and that the server exposes the registered activity descriptors.
3. Check `HasHeaderRecord` and `Delimiter` before changing the input data.
4. If rows contain `Field1` keys, the activity is running without a header
   row; set `HasHeaderRecord` to `true` when the first row contains names.
5. For typed mapping failures, verify column names, member types, and the
   source system's number/date formatting.
6. For empty output, check that `CsvData` is not null or an unintended URL
   string, and confirm that the input contains at least one data row.

## Release source

This page is validated against the `release/3.8.0` Extensions source:

- [`UseCsv` and feature registration](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/data/Elsa.Data.Csv/Extensions/ModuleExtensions.cs)
- [`CsvFeature`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/data/Elsa.Data.Csv/Features/CsvFeature.cs)
- [`ReadCsv` inputs, parsing, and output](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/data/Elsa.Data.Csv/Activities/ReadCsv.cs)
- [`Elsa.Data.Csv` package README](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/data/Elsa.Data.Csv/README.md)
