# Authentication & Authorization Source References

This maintainer reference records the Elsa 3.8 source areas that ground the
Authentication & Authorization section. Paths are relative to the named
repository.

## elsa-core

| Source | Documentation owner |
| --- | --- |
| `src/apps/Elsa.Server.Web/Program.cs` | `elsa-identity.md` |
| `src/modules/Elsa.Identity/Extensions/ModuleExtensions.cs` | `elsa-identity.md` |
| `src/modules/Elsa.Identity/Features/IdentityFeature.cs` | `elsa-identity.md` |
| `src/modules/Elsa.Identity/Endpoints/Applications/Create/Endpoint.cs` | `api-keys.md` |
| `src/common/Elsa.Api.Common/Abstractions/Endpoints.cs` | `permissions.md` |
| `src/modules/Elsa.Identity/` | `elsa-identity.md`, `api-keys.md`, and `permissions.md` |
| `src/modules/Elsa.ExternalAuthentication/` | `external-authentication/` |
| `src/modules/Elsa.ExternalAuthentication.OpenIdConnect/` | `external-authentication/` |

## elsa-studio

| Source | Documentation owner |
| --- | --- |
| `src/hosts/Elsa.Studio.Host.Server/Program.cs` | `elsa-identity.md`, `direct-openid-connect.md`, and `external-authentication/studio-integration.md` |
| `src/hosts/Elsa.Studio.Host.Wasm/Program.cs` | `elsa-identity.md`, `direct-openid-connect.md`, and `external-authentication/studio-integration.md` |
| `src/modules/Elsa.Studio.Authentication.OpenIdConnect.BlazorServer/` | `direct-openid-connect.md` |
| `src/modules/Elsa.Studio.Authentication.OpenIdConnect.BlazorWasm/` | `direct-openid-connect.md` |
| `src/modules/Elsa.Studio.ExternalAuthentication/` | `external-authentication/` |

## Maintenance Rules

- Validate examples against the stable `3.8.0` tags while the package family
  evolves.
- Keep Direct OIDC and External Authentication as separate, selectable
  topologies. Direct OIDC is not deprecated in Elsa 3.8.
- Keep External Authentication's stable `3.8.0` package alignment and migration
  notes prominent.
- Document API authorization through Elsa `permissions` claims; do not imply
  that ASP.NET Core roles alone authorize Elsa API endpoints.
- Keep workflow `HttpEndpoint` authorization under Security & Hardening.
