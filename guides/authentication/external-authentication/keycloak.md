---
description: >-
  A concrete end-to-end walkthrough for connecting Keycloak to Elsa External
  Authentication and signing in from Elsa Studio.
---

# Keycloak Walkthrough

{% hint style="info" %}
This walkthrough uses the stable Elsa 3.8.0 External Authentication packages.
Use sample credentials only in an isolated development environment and keep
provider secrets outside source control.
{% endhint %}

This walkthrough makes the provider-neutral configuration concrete with Keycloak. The same Elsa configuration model applies to Microsoft Entra ID, Auth0, Okta, OpenIddict, and other conforming OpenID Connect providers.

Before starting, complete [installation](installation.md) and choose a [Studio host model](studio-integration.md).

## Values Used in This Walkthrough

| Setting | Example |
| --- | --- |
| Elsa API base URL | `https://elsa.example/elsa/api/` |
| Studio URL | `https://studio.example` |
| Keycloak issuer | `https://login.example/realms/elsa` |
| Connection key | `keycloak` |
| Provider client ID | `elsa-server-broker` |
| Studio Server Authentication Client ID | `elsa-studio-server` |
| Studio WebAssembly Authentication Client ID | `elsa-studio-wasm` |

Replace every example hostname and secret before running the configuration.

## 1. Create the Keycloak Realm and User

In Keycloak:

1. Create or select a realm, such as `elsa`.
2. Create a test user and set a password.
3. Ensure the account may complete the standard authorization-code flow.
4. Add any provider claims you intend to project or use for authorization, such as `email`, `preferred_username`, or a realm role.

External Authentication validates the provider identity and then resolves it to an Elsa User. A Keycloak role does not automatically grant an Elsa permission.

## 2. Register Elsa Server at Keycloak

Create a confidential OpenID Connect client for Elsa Server:

- Enable the standard authorization-code flow.
- Disable implicit flow.
- Require client authentication.
- Use `client_secret_basic` when available; `client_secret_post` is also supported.
- Register the exact normal callback URI:

```text
https://elsa.example/elsa/api/external-authentication/callback/keycloak
```

If administrators will run interactive connection previews, also register the preview callback URI shown by the connection detail screen. It uses the connection record ID and is intentionally different from the normal callback.

Do not register a Studio callback at Keycloak. Keycloak redirects to Elsa Server; Elsa later redirects to the registered Studio Authentication Client.

Copy the generated provider client secret into a deployment secret store. Do not put it in `adapterSettings`, source control, a browser application, or a Studio connection document.

## 3. Configure the Provider Connection

The following example creates a deployment-owned connection. In a conventional host, place it under the root `ExternalAuthentication` section. In Modular Server, place the same object inside the `ExternalAuthentication` feature configuration.

```json
{
  "ExternalAuthentication": {
    "LocalLogin": {
      "IsEnabled": true
    },
    "Redirects": {
      "ExternalCallbackBaseUri": "https://elsa.example/elsa/api/"
    },
    "ProviderEgress": {
      "RequireHttps": true,
      "AllowPrivateNetworkDestinations": false,
      "AllowedHosts": ["login.example"]
    },
    "Connections": [
      {
        "Id": "keycloak-configuration",
        "Key": "keycloak",
        "AdapterType": "openid-connect",
        "AdapterSettingsVersion": 2,
        "AdapterSettings": {
          "mode": "discovery",
          "discoveryUrl": "https://login.example/realms/elsa/.well-known/openid-configuration",
          "clientId": "elsa-server-broker",
          "clientAuthenticationMethod": "client_secret_basic",
          "scopes": ["profile", "email"]
        },
        "SecretBindings": {
          "clientSecret": {
            "Ownership": "External",
            "ResolverType": "configuration",
            "Reference": "ExternalAuthentication:Secrets:KeycloakClientSecret",
            "ExpectedType": "text",
            "ExpectedScope": "external-authentication"
          }
        },
        "DisplayName": "Keycloak",
        "DisplayOrder": 10,
        "IsPreferred": true,
        "IsEnabled": true,
        "UnlinkedPolicy": {
          "Type": "create-user",
          "SettingsVersion": 1,
          "Settings": {
            "defaultRoleIds": ["admin"]
          }
        },
        "ClaimProjection": {
          "AllowedClaimTypes": [
            "preferred_username",
            "name",
            "given_name",
            "family_name",
            "email"
          ],
          "RedactedClaimTypes": [],
          "MaximumClaimCount": 64,
          "MaximumValueLength": 1024,
          "MaximumTotalBytes": 16384
        },
        "UpstreamLogoutMode": "UserChoice"
      }
    ]
  }
}
```

Supply the referenced value through deployment configuration, for example an
environment variable named
`ExternalAuthentication__Secrets__KeycloakClientSecret`. Do not add the value
to the JSON file.

{% hint style="danger" %}
Assigning the `admin` role on just-in-time creation is convenient for an isolated walkthrough, but it grants full Elsa access when that role contains `*`. Use [least-privilege roles and explicit permission grants](production.md#authorization-and-administrative-safety) in a real environment.
{% endhint %}

`BindExternalAuthenticationOptions` preserves the adapter's versioned JSON settings. Do not replace it with a plain options bind in a conventional host.

## 4. Register Studio as an Elsa Authentication Client

For Studio Server, add a confidential Authentication Client:

```json
{
  "ClientId": "elsa-studio-server",
  "DisplayName": "Elsa Studio Server",
  "ClientType": "Confidential",
  "CallbackUris": [
    "https://studio.example/authentication/external/callback"
  ],
  "LogoutCallbackUris": [
    "https://studio.example/authentication/external/logout-callback"
  ],
  "AllowedReturnPathPrefixes": ["/"],
  "SecretBinding": {
    "Ownership": "External",
    "ResolverType": "configuration",
    "Reference": "ExternalAuthentication:Secrets:StudioServerClientSecret"
  },
  "IsEnabled": true
}
```

For Studio WebAssembly, use a public client, omit the secret, and register the exact browser origin:

```json
{
  "ClientId": "elsa-studio-wasm",
  "DisplayName": "Elsa Studio WebAssembly",
  "ClientType": "Public",
  "CallbackUris": [
    "https://studio.example/authentication/external/callback"
  ],
  "LogoutCallbackUris": [
    "https://studio.example/authentication/external/logout-callback"
  ],
  "AllowedOrigins": ["https://studio.example"],
  "AllowedReturnPathPrefixes": ["/"],
  "IsEnabled": true
}
```

If both Studio hosts are deployed, give each its own Authentication Client ID and exact URLs.

## 5. Configure Studio

Set `Authentication:Provider` to `ExternalAuthentication` and choose the matching host configuration from [Studio Integration](studio-integration.md). Do not register direct OIDC and brokered External Authentication in the same Studio host.

Restart Elsa Server and Studio after changing deployment-owned configuration.

## 6. Validate the Connection

In Studio:

1. Open **Administration → Identity & access → Identity provider connections**.
2. Open the `Keycloak` configuration-owned connection.
3. Confirm the displayed callback URI exactly matches the Keycloak client registration.
4. Run **Validate** to check local structure and secret-binding state.
5. Run **Test connection** to fetch discovery metadata and signing keys.
6. Run **Preview sign-in** to verify the provider identity without creating a user, link, credential, or normal session.

Configuration-owned connections are read-only in Studio. If deployment policy permits overrides, Studio can create a complete database-owned override; it never partially merges settings or copies secret values.

## 7. Sign In

1. Sign out of Studio and open `/login`.
2. Select **Keycloak**. A preferred connection may be emphasized or sorted first, but Studio never auto-starts it.
3. Authenticate at Keycloak.
4. Confirm Elsa returns to Studio and creates or resolves the expected Elsa User.
5. Verify a normal session appears under **Administration → Identity & access → Authentication sessions** when session administration is enabled.
6. Verify the user can perform only the Elsa operations allowed by their Elsa roles and permissions.

Keep local login enabled until this complete path and a recovery account have been tested.

## Provider Variations

For Microsoft Entra ID, Auth0, Okta, and other providers, change the discovery URL, provider client registration, allowed egress host, scopes, and claim selection. The Elsa callback shape, Authentication Client distinction, PKCE completion flow, identity linking, Elsa-issued credentials, and explicit authorization model remain the same.

## Next Steps

- [Configuration Reference](configuration.md)
- [Studio Integration](studio-integration.md)
- [Administration in Studio](administration.md)
- [Production and Security](production.md)
- [Troubleshooting](troubleshooting.md)
