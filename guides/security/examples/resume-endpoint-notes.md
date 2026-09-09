---
description: >-
  Operational security notes for Elsa bookmark resume callbacks and ingress.
---

# Bookmark resume endpoint security notes

Use this page as a short operational checklist for tokenized bookmark resume
callbacks. The [Bookmark Resume Tokens](../bookmark-resume-tokens.md) guide is
the canonical reference for token lifetime, single-use bookmarks, input
handling, and the release-backed request contract.

## Endpoint behavior in Core 3.8.0

The endpoint is available at `GET` or `POST /elsa/api/bookmarks/resume` and
allows anonymous access. The encrypted token is supplied in the `t` query
parameter. GET requests may carry a JSON object in the `in` query parameter;
POST requests use a body shaped as `{ "input": { ... } }`. Add
`async=true` to enqueue resumption instead of waiting for the runtime resume
call. A POST body is limited to 1 MiB and malformed token or input data is
rejected.

An accepted asynchronous request is not proof that the workflow completed. A
valid token also does not establish caller identity. Treat the URL as a
bearer capability and add authentication or an application-level approval
record when the business action requires an identified actor.

## Production checklist

- Generate the shortest practical token lifetime and use `AutoBurn = true` for
  one-time callbacks.
- Serve callback URLs only over HTTPS. Do not log the full URL, the `t` value,
  or a raw token-derived revocation key.
- Apply rate limits and, where appropriate, IP allowlists at the ingress or
  reverse proxy. Browser CORS is not a server-to-server webhook control.
- Validate the resume input's schema, size, and business constraints before an
  irreversible activity runs.
- Record safe operational context such as outcome, timestamp, workflow or
  bookmark identifiers after resolution, source metadata, and a correlation
  ID. Avoid sensitive request bodies and bearer tokens.

## Revocation

Cancel the workflow when all outstanding bookmarks for an instance must stop
being usable. For an individual bookmark, use an application-level revocation
or cancellation procedure supported by the host and storage provider. Do not
delete bookmark rows directly as an undocumented database operation: that can
bypass provider behavior, auditing, and related cleanup rules.

For especially sensitive integrations, keep a deny-list keyed by a safely
derived token identifier and reject it before the resume endpoint is reached.
Store neither the raw token nor the full callback URL in that list.

## Example ingress policy

This Nginx example limits the callback route to trusted internal networks;
adapt it to the actual reverse proxy and source ranges used by the deployment.

```nginx
location /elsa/api/bookmarks/resume {
    allow 10.0.0.0/8;
    allow 192.0.2.10;
    deny all;

    proxy_pass http://elsa-backend;
}
```

Keep the [ingress and CORS examples](ingress-cors-snippet.md) alongside this
checklist, and use [Production hardening](../production-hardening.md) for the
wider transport, browser, and deployment controls.

## Release source

The request behavior is implemented by Core's
[`Bookmarks.Resume` endpoint](https://github.com/elsa-workflows/elsa-core/blob/8191ae30554ea001b38bb44902dd90dc98c7a106/src/modules/Elsa.Workflows.Api/Endpoints/Bookmarks/Resume/Endpoint.cs)
at the tagged `release/3.8.0` ref.
