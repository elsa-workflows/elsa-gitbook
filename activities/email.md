---
description: Send email from Elsa workflows with the 3.8.0 Email extension.
---

# Send email from a workflow

The `Elsa.Email` extension adds the **Send Email** activity. Use it when a
workflow needs to notify people, deliver a generated document, or send the
result of an external-system step through an SMTP server.

The extension is server-side. Elsa Studio can design a workflow containing the
activity when the connected server registers `Elsa.Email`, but Studio does not
send mail or configure SMTP credentials.

## Install and register the extension

Install the package in the Elsa Server project:

```bash
dotnet add package Elsa.Email --version 3.8.0
```

Register the feature in the Elsa builder. The `ConfigureOptions` callback is
where the host binds its configuration to `SmtpOptions`:

```csharp
using Elsa.Extensions;

builder.Services.AddElsa(elsa =>
{
    elsa.UseEmail(email =>
    {
        email.ConfigureOptions = options =>
            builder.Configuration.GetSection("Smtp").Bind(options);
    });
});
```

`UseEmail` registers the `SendEmail` activity, the default MailKit-based SMTP
service, and the HTTP client used to download URL attachments. The release
also exposes `ISmtpService` as an extension point if the host needs a different
transport.

## Configure SMTP

The built-in service reads these options:

| Option | Purpose |
| --- | --- |
| `Host` | SMTP server hostname or IP address. |
| `Port` | SMTP port. The release default is `25`. |
| `SecureSocketOptions` | MailKit's TLS/STARTTLS connection mode. The release default is `Auto`. |
| `RequireCredentials` | Whether the service calls SMTP authentication. |
| `UserName` / `Password` | Credentials used when `RequireCredentials` is `true`. |
| `DefaultSender` | Sender used when the activity's `From` input is empty. |

For example:

```json
{
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "SecureSocketOptions": "StartTls",
    "RequireCredentials": true,
    "UserName": "workflow@example.com",
    "Password": "provided-by-the-deployment-secret-store",
    "DefaultSender": "workflow@example.com"
  }
}
```

The option binding is host configuration. Keep `Password` out of source
control and supply it through the deployment environment, a protected
configuration provider, or a platform secret. The workflow Secrets expression
does not automatically resolve into `SmtpOptions` during startup; use a custom
`ISmtpService` if SMTP credentials must be resolved dynamically at execution
time.

The default `MailKitSmtpService` connects with the configured host, port, and
secure-socket mode, authenticates only when `RequireCredentials` is enabled,
then sends and disconnects. It rejects invalid server certificates rather than
silently accepting them.

## Configure the Send Email activity

The activity exposes these inputs:

| Input | What it does |
| --- | --- |
| `From` | Sender address. Blank or whitespace uses `DefaultSender`. |
| `To` | Collection of recipient addresses. |
| `Cc` / `Bcc` | Optional collection of carbon-copy or blind-copy addresses. |
| `Subject` | Subject text. A missing value becomes an empty subject. |
| `Body` | Message body, assigned to the HTML body of the MIME message. |
| `Attachments` | One attachment or a collection of supported attachment values. |
| `Error` | Optional port to schedule when the SMTP send operation throws. |

In Studio, search for **Send Email** in the **Email** category. Use literals or
expressions for addresses, subject, body, and attachment values. Because the
body is assigned as HTML, encode or sanitize untrusted values before inserting
them into an HTML template.

The activity parses recipient strings as mailbox addresses. Invalid addresses
fail while the message is being prepared, before the SMTP send call begins.

## Add attachments

`Attachments` is intentionally flexible so a workflow can pass data produced
by another activity. The release handles these forms:

- a local file path, read by the Elsa Server process;
- an `http://` or `https://` URL, downloaded by the Elsa Server process;
- a `byte[]` or `Stream`;
- an `EmailAttachment` containing byte-array or stream `Content`, plus
  `FileName` and `ContentType`;
- an `ExpandoObject` with equivalent `Content`, `FileName`, and `ContentType`
  properties, where `Content` is a byte array or stream; or
- another object, which the activity serializes as a JSON attachment.

For more than one attachment, provide an enumerable collection. For raw bytes
or streams without a filename, the activity assigns names such as
`Attachment-1`. An `EmailAttachment` is the most predictable choice when the
workflow needs a stable filename and MIME type:

```csharp
using Elsa.Email.Models;

var attachment = new EmailAttachment(
    Content: pdfBytes,
    FileName: "invoice-1042.pdf",
    ContentType: "application/pdf");
```

Treat local paths and URLs as server-side capabilities. Validate or constrain
them when workflow authors are not fully trusted, and avoid allowing arbitrary
URL downloads to reach internal services. The default downloader performs an
HTTP GET from the Elsa Server; it does not provide an application-specific
allowlist.

## Handle send failures

Connect the `Error` port when the workflow should take a business or
operational path after an SMTP send failure, such as recording a retry request
or notifying an operator. The activity logs the exception and adds an
execution-log entry before scheduling the error branch.

The `Error` port covers exceptions raised by `ISmtpService.SendAsync`. It does
not cover every possible input problem: recipient parsing, attachment
preparation, local-file access, and URL download happen before the SMTP send
try/catch and can fault the activity directly. Validate inputs and test these
paths separately.

Sending email is an external side effect. If a workflow retries after a
transport timeout, the SMTP server may have accepted the message even when Elsa
did not receive a success response. Use an application-level idempotency
strategy or a dedicated delivery/outbox design when duplicate mail is
unacceptable; the activity itself does not deduplicate messages.

## Studio and deployment boundaries

- Register `Elsa.Email` on the server that executes workflows. Registering a
  type only in Studio does not make it executable.
- The 3.8.0 Studio source has no Email-specific SMTP administration page;
  configure the transport on the server.
- Network egress from the server must allow the configured SMTP host and, when
  used, attachment URLs.
- Prefer TLS/STARTTLS settings required by the provider. Do not weaken
  certificate validation to work around a certificate or trust-store problem.
- Review message content, recipients, attachments, and execution logs for
  personal or confidential data. The activity's error log includes the
  exception message and stack trace payload.

## Replace the SMTP service

The built-in MailKit implementation is registered as `ISmtpService`, but the
feature accepts a factory for a custom implementation:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseEmail(email =>
    {
        email.SmtpService = services =>
            services.GetRequiredService<MySmtpService>();
    });
});
```

Register `MySmtpService` in the host's dependency-injection container and
implement `ISmtpService.SendAsync`. This is useful for a provider API, a
centralized mail service, or runtime credential resolution. The activity
contract remains the same, including the `Error` port behavior.

## Troubleshooting checklist

1. Confirm `Elsa.Email` is installed in the server project and `.UseEmail(...)`
   runs during startup.
2. Check that `Host`, `Port`, `SecureSocketOptions`, and `DefaultSender` are
   bound to the expected configuration section.
3. If authentication is enabled, verify `RequireCredentials`, username, and
   password are present in the deployed configuration.
4. Check the server logs for TLS certificate errors or SMTP connection errors.
5. Test the activity with a literal recipient and no attachment, then add
   expressions and attachments one at a time.
6. If Studio cannot find the activity, verify it is connected to the server
   that registered the Email feature and that the server exposes activity
   descriptors.

## Release source

This page is validated against the `release/3.8.0` Extensions source:

- [`UseEmail` and feature registration](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/email/Elsa.Email/Extensions/ModuleExtensions.cs)
- [`EmailFeature` options and services](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/email/Elsa.Email/Features/EmailFeature.cs)
- [`SendEmail` inputs, attachments, and error path](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/email/Elsa.Email/Activities/SendEmail.cs)
- [`SmtpOptions`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/email/Elsa.Email/Options/SmtpOptions.cs)
- [`MailKitSmtpService`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/email/Elsa.Email/Services/MailKitSmtpService.cs)
- [`EmailAttachment`](https://github.com/elsa-workflows/elsa-extensions/blob/335a26495318f6ee1528bf2723b7333c753ce9a2/src/modules/email/Elsa.Email/Models/EmailAttachment.cs)
