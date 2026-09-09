---
description: >-
  Secure Elsa bookmark resume URLs with expirations, single-use bookmarks,
  revocation procedures, logging, and ingress controls.
---

# Bookmark Resume Tokens

Elsa can generate a tokenized URL that resumes a workflow waiting at a
bookmark. These URLs are useful for approval links, webhook callbacks,
multi-step forms, and external event notifications.

Treat the token as a bearer capability: anyone who can use a valid URL can
attempt to resume the associated bookmark. Do not put the full URL or its
`t` query-string value in logs, analytics, support tickets, or referrer URLs.

With the default Elsa API prefix, the public resume endpoint is:

```text
GET or POST /elsa/api/bookmarks/resume?t=<encrypted-token>
```

The decrypted token payload identifies a bookmark and workflow instance. It
does not contain a caller identity, so it is not a substitute for endpoint
authentication where the callback needs an authenticated principal.

## Resume request contract

The Core 3.8.0 resume endpoint accepts both `GET` and `POST` requests and
allows anonymous access. The token is always supplied in the `t` query
parameter. The two methods differ only in how optional workflow input is
carried:

| Request | Optional input | Asynchronous mode |
| --- | --- | --- |
| `GET` | `in` query parameter containing a JSON object | `async=true` query parameter |
| `POST` | JSON body shaped as `{ "input": { ... } }` | `async=true` query parameter |

For a GET callback, `in` is a JSON string rather than an ordinary scalar
value. A POST body may be omitted when the workflow does not need resume
input. The endpoint rejects malformed input and limits the POST body to 1 MiB
(1,048,576 bytes), including requests whose declared `Content-Length` is
missing or inaccurate.

For example, a structured approval callback can enqueue resumption without
waiting for workflow execution to finish:

```bash
curl -X POST \
  'https://localhost:5001/elsa/api/bookmarks/resume?t=<encrypted-token>&async=true' \
  -H 'Content-Type: application/json' \
  --data '{"input":{"decision":"approve","comment":"Reviewed"}}'
```

An `async=true` response means the resume request was accepted for the
bookmark queue; it is not confirmation that the workflow has completed. Use a
normal synchronous request when the caller needs the server to attempt the
resume before receiving a response; that response is still not a business
outcome. In either mode, validate the input as untrusted data in the workflow
or host integration.

The endpoint's anonymous route and bearer-token semantics are separate from
the resume input contract. Apply HTTPS, ingress controls, rate limits, and
application authentication where the callback requires an authenticated
actor.

## Create a time-bounded URL

Generate a bookmark URL with the shortest lifetime that meets the business
need. Elsa provides overloads of `GenerateBookmarkTriggerUrl` that accept a
`TimeSpan` or an absolute `DateTimeOffset` expiration.

Typical starting points:

| Use case | Starting lifetime |
| --- | --- |
| Third-party webhook callback | 5–15 minutes |
| SMS or push approval | 1–4 hours |
| Email approval | 24–72 hours |
| Document-signing process | 7–30 days, subject to business policy |

Token lifetime is only one layer. Plan for the token to become unusable when
the bookmark is consumed or the workflow is cancelled as well.

## Make resumption single-use

For one-time callbacks, create the bookmark with `AutoBurn = true`:

```csharp
var bookmark = context.CreateBookmark(new CreateBookmarkArgs
{
    Name = "ApprovalBookmark",
    Payload = new { ApprovalId = approvalId },
    AutoBurn = true,
    Callback = OnResumeAsync
});
```

After a successful resume, an auto-burning bookmark is removed. A later use of
the same URL should not resume a workflow. Elsa's runtime also uses distributed
locking while resuming, which protects concurrent attempts; it is not a reason
to omit replay controls for a high-risk business operation.

## Revoke a compromised token

Choose a revocation procedure before issuing callback URLs.

- Cancel the workflow when every outstanding bookmark for that instance must
  stop being usable.
- For an individual bookmark, use the supported application or operational
  procedure for the storage provider rather than ad-hoc database changes.
- For especially sensitive integrations, keep an application-level deny-list
  keyed by a safely derived token identifier, and reject it before the resume
  endpoint is reached.

Do not store the raw token in a revocation list or in logs if a hashed or
otherwise derived identifier will meet the operational need.

## Validate input and observe attempts

The resume request's input is still untrusted input. Validate its schema,
size, and business constraints in the workflow or host integration before
performing an irreversible action.

Record enough information to investigate abuse without recording the token or
sensitive request body:

- timestamp and outcome;
- workflow instance and bookmark identifiers after successful, safe
  resolution;
- source IP and user agent where available; and
- a correlation identifier or safely derived token identifier.

Alert on unusual failure rates, repeated attempts against a single-use
bookmark, or sources that cause sustained rate-limit responses.

## Protect the route at ingress

Always expose resume URLs over HTTPS. Apply rate limits before traffic reaches
the host, and allowlist known sources when a callback is internal or comes from
a fixed third-party address range. Browser CORS does not protect server-to-
server webhooks; configure it only for browser clients that actually need it.

For ingress examples and the wider transport controls, see [Production
hardening](production-hardening.md) and [the ingress and CORS
examples](examples/ingress-cors-snippet.md).

## Related documentation

- [HTTP endpoint security](http-endpoint-security.md)
- [Production hardening](production-hardening.md)
- [Authentication & Authorization](../authentication/README.md)

## Release source

The request behavior described here is implemented by Core's
[`Bookmarks.Resume` endpoint](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Workflows.Api/Endpoints/Bookmarks/Resume/Endpoint.cs)
and the runtime's
[`IWorkflowResumer` contract](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Workflows.Runtime/Contracts/IWorkflowResumer.cs)
at the tagged `release/3.8.0` ref.
