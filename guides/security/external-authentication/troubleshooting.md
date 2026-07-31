---
description: >-
  Diagnose Elsa Studio External Authentication integration and administration
  issues in Elsa 3.8 preview.
---

# External Authentication troubleshooting

> **Preview feature — Elsa 3.8.** External Authentication currently ships from
> the Feedz.io preview feed and remains under development. Check that Elsa Core
> and Elsa Studio use compatible 3.8 preview builds before investigating a
> runtime symptom as a configuration problem.

Start with the boundary that failed: Studio host startup, login method discovery,
broker redirect/callback, token exchange/refresh, Elsa API authorization, or
management API authorization. Do not capture or share client secrets, access
tokens, refresh tokens, authorization codes, PKCE verifiers, or full callback
URLs in logs or support tickets.

## Fast triage

1. Record the Studio host model (Blazor Server or WebAssembly), public Studio
   origin, Elsa API `Backend:Url`, and the preview package versions—without
   secret values.
2. Confirm `Authentication:Provider` is exactly `ExternalAuthentication`.
3. Confirm Studio's `ClientId` identifies a dedicated Elsa Authentication
   Client, not an Elsa API Application.
4. Compare the absolute callback/logout URLs registered in Elsa Server with
   the public Studio URLs character-for-character.
5. Check the server-side External Authentication feature and the affected
   user's Elsa `permissions` claims.
6. Use a redacted **Test connection** or **Preview sign-in** observation where
   the failure is provider/policy related.

## Startup failures

### “External Authentication requires a configured broker client ID”

`Authentication:ExternalAuthentication:ClientId` is missing, empty, or bound
from the wrong configuration source.

Set a non-empty client ID for the dedicated Studio Authentication Client:

```json
{
  "Authentication": {
    "Provider": "ExternalAuthentication",
    "ExternalAuthentication": {
      "ClientId": "elsa-studio-server"
    }
  }
}
```

Use the corresponding public client ID in WASM. Do not reuse an Elsa API
application client just because it has an ID.

### “Studio Server must configure a confidential broker client secret”

The Blazor Server package requires `ClientSecret`. Put the value in deployment
secret configuration and ensure the process can read it. Do not add it to the
repository or a browser-delivered appsettings file.

### “Studio WebAssembly is a public client and must not contain a broker client secret”

Remove `ClientSecret` from all WASM configuration inputs. Any value published
to WASM is public, including a value injected at build time. Create/use a public
Elsa Authentication Client for that host instead.

### Fixed callback path validation fails

These settings cannot be customized:

```json
{
  "CallbackPath": "/authentication/external/callback",
  "LogoutCallbackPath": "/authentication/external/logout-callback"
}
```

Restore the fixed paths in configuration and change the client registration at
Elsa Server/upstream provider to match the absolute Studio URLs. Do not try to
solve a path-base or proxy issue by changing these values.

### Startup rejects authentication registrations

The host has mixed legacy login, Elsa Identity, direct OIDC, and/or brokered
registration. Select one `Authentication:Provider` and register only the
corresponding host authentication package. The shared External Authentication
management module is separate from selecting the broker login mode.

## Login methods missing or sign-in does not start

### `/login` shows no expected external provider

Check these in order:

1. The Studio host selects `ExternalAuthentication` and successfully binds its
   client ID.
2. `Backend:Url` reaches the Elsa API used by the configured Authentication
   Client.
3. The Elsa Server connection is enabled and valid, and its scope/policy makes
   it discoverable for the Studio client.
4. The Server External Authentication feature is enabled/advertised.
5. The login-method request for the Studio client completes successfully.

The preferred method is only a visual hint. It never navigates automatically;
the user must select it. If a preferred method fails, the chooser should still
allow another available method.

### WASM login discovery fails with a CORS error

Register the exact Studio origin twice: in the public Elsa Authentication
Client's `AllowedOrigins` collection and in the Elsa Server host's ASP.NET Core
CORS policy. These controls serve different purposes; `AllowedOrigins` does not
make Elsa API responses available cross-origin.

Apply the CORS policy before authentication, authorization, and endpoint
mapping, and allow the headers and methods required by Studio and SignalR. Do
not use `AllowAnyOrigin` with credentialed requests. Confirm scheme, host, and
port exactly—changing only the Studio client registration cannot fix an API
CORS rejection.

### Test succeeds but sign-in returns `method_unavailable`

A successful metadata test proves that the adapter can resolve provider
metadata; it does not prove that the connection is currently eligible for the
requested broker flow. Check the effective connection selected for the current
tenant and client:

- it is enabled, valid, and not archived;
- a database override is not shadowing the configuration-owned record;
- the connection is allowed for this Authentication Client and tenant context;
- the adapter module is installed and its flow is available in this preview
  build.

Use the public error category and correlation ID to locate the matching
server-side security event. Do not expose provider exceptions or retry captured
callback URLs.

### Local broker credentials fail

Use the broker-local form only if Elsa Server advertises a local method. For
Blazor Server, local credentials post to
`/authentication/external/local-login` with antiforgery protection; they are
not placed in the browser address bar. Verify the browser is returning the
antiforgery cookie/form token and that any proxy does not strip the POST body.

For WASM, do not inspect browser history or logs for passwords. Confirm the
broker local-authorize endpoint is reachable and use a known test account.

### Callback returns to the chooser

Studio intentionally returns to `/login?choose=true` on an invalid callback,
provider error, missing code, expired state, or replayed state. Check for:

- The callback is exactly the registered scheme, host, port, and path.
- The callback belongs to the same Studio origin for WASM.
- The user completed the flow in the same tab that initiated it (especially
  with `BrowserStorage: Memory` or `Session`).
- The callback transaction did not exceed its short lifetime or get consumed
  by a previous attempt.
- No load balancer/proxy changed the Server host's public scheme or host.

Do not retry a captured callback URL: completion codes and state are one-time
values by design.

### Return path is unexpectedly `/`

Studio accepts only a local relative return path beginning with one `/`. It
normalizes an empty path, absolute URL, `//...`, backslash-containing path, or
invalid relative URI to `/` to prevent open redirects. Use a local Studio path
such as `/workflows?tab=active` and add the necessary allowed local return path
to the Authentication Client.

## Token, refresh, API, and SignalR failures

### Sign-in works but Studio API calls return `401`

Check that:

- `Backend:Url` is the Elsa API base URL expected by the host.
- The code exchange completed and the host could create an Elsa access token.
- The Studio Authentication Client and callback URI match the selected host.
- Server-side application instances share any required ticket/cache state when
  running more than one Server host node.
- The browser storage choice matches the symptom: `Memory` deliberately loses
  credentials after reload/new tab/tab close.

For Server hosts, cookies are secure and HTTP-only. Test over HTTPS; an HTTP
origin will not receive the secure session cookie. For WASM, verify the browser
loaded `external-authentication.js` before Blazor starts and that it is served
from the same deployed package version.

### API calls return `403`

This usually means authentication succeeded but authorization did not. Inspect
the current Elsa access token or server-side authorization diagnostics using
safe internal tooling and confirm `permissions` claims contain the required
Elsa endpoint permission. A Studio menu item being visible is not proof that
the API will authorize the action.

For External Authentication administration, verify the exact permission, such
as `external-authentication:connections:read`, rather than a similarly named
role. See [External Authentication administration](administration.md#routes-and-permissions).

### WASM user is signed out after refresh/reload

This is expected with `BrowserStorage: Memory`, the default. Select `Session`
only when retaining the session within a tab is worth the additional browser
token exposure; select `Durable` only when persistent local storage is an
explicit, reviewed requirement. Both alternatives emit a security warning and
increase exposure to script compromise.

### SignalR disconnects or fails after signing in

Confirm the host selected the External Authentication API handler and broker
registration, because the package also registers the SignalR HTTP connection
options configurator. Then check normal cross-origin/proxy/WebSocket settings,
the current access-token lifetime, and whether a WASM memory session was lost.

## Logout problems

### Local logout works but upstream logout does not

Local sign-out is intentionally independent of upstream availability. For
upstream logout, verify that the selected connection supports it and that the
registered `LogoutCallbackPath` is exactly
`/authentication/external/logout-callback` at the public Studio origin.

Studio accepts an upstream continuation only when Elsa Server returns a
same-origin route containing `/external-authentication/logout/continue/`.
Treat a different or external continuation URL as a configuration/security
problem, not a URL to follow manually.

### Server logout request returns antiforgery errors

The Server logout is an authenticated POST protected by antiforgery. Ensure the
page rendered the form token, the secure cookie is present on HTTPS, and any
reverse proxy preserves cookies and POST form values.

## Management UI and connection issues

### Identity & access menu is missing

Confirm that the shared `AddExternalAuthenticationModule(backendApiConfig)`
registration is present, Elsa Server advertises the External Authentication
feature, and the current user has the route's menu permission. For example,
connection management requires
`external-authentication:connections:read`; identity links require
`external-authentication:links:manage`.

### Connection cannot be edited

A configuration-owned connection is intentionally read-only. Change it in
deployment configuration, or create a complete database override only if Elsa
Server advertises `canCreateOverride` and the operator has create permission.
The override does not copy secret bindings; configure its required secrets
again.

### A secret field cannot be edited or removed

Studio exposes a write-only managed editor only if Elsa Server advertises an
installed managed-secret resolver. It does not show deployment-managed secret
references. A required managed secret cannot be removed while the connection
is enabled—disable it first.

If a secret was pasted into a log, configuration commit, or support artifact,
rotate it in the provider and managed secret store; deleting the text does not
make the exposure safe.

### Test connection or preview fails

Use the redacted test result's category, summary, warnings, and correlation ID
to investigate server/provider logs. Ensure the current connection revision is
saved first—test and preview operate on the current revision.

Preview opens in a separate tab and returns a one-time redacted result; it
does not create a user, link, credential, or session. A preview failure does
not alter normal accounts. Do not expect the page to reveal provider access
tokens or raw claims.

### Cannot disable a connection

When disabling would remove the final normal login method, Studio requires an
explicit recovery override confirmation. Verify an independent recovery path
first, including break-glass access where applicable. You may optionally revoke
active sessions if you have `external-authentication:sessions:revoke`; otherwise
existing sessions can remain active until expiry or separate revocation.

### Archived connection does not reappear as a login method

Restore returns it as **disabled**, by design. Test/validate it and explicitly
enable it. Also check whether another database override shadows a
configuration-owned connection.

### Identity link action is denied or has unexpected sign-in results

Confirm `external-authentication:links:manage`, tenant context, Elsa user ID,
connection key, issuer, and subject. Replacing a link resets its sign-in
history; unlinking prevents that external identity from signing in until an
appropriate new link or policy permits it.

### Session page is incomplete

The page deliberately contains only safe metadata and never returns tokens,
external subject values, or claim snapshots. Confirm
`external-authentication:sessions:read` for viewing and
`external-authentication:sessions:revoke` to revoke. This is a privacy/security
boundary, not a data-loading error.

## Verification commands

For Studio source builds, run the module test project:

```bash
dotnet test src/modules/Elsa.Studio.ExternalAuthentication.Tests/Elsa.Studio.ExternalAuthentication.Tests.csproj
```

The browser suite exercises browser-only PKCE, storage scope, reload/tab
behavior, refresh-token rotation/reuse, callback replay, logout, and
accessibility:

```bash
cd tests/browser/ExternalAuthentication
npm install
npx playwright install
npm test
```

Use a non-production test tenant/client for browser diagnostics. The suite is
designed to prove that tokens and secrets do not leak into URLs and that a
preferred method does not start without user input.

For setup and configuration details, see [Studio integration](studio-integration.md).
For safe management workflows, see
[External Authentication administration](administration.md).
