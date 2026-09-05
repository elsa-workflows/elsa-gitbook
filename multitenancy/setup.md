# Setup

Here are the general steps to enable a multi-tenant setup:

1. Add a reference to the `Elsa.Tenants` package.
2. Install and configure the `TenantsFeature`.
   1. Configure the tenant resolution pipeline
   2. Configure the tenant provider

## Configuration

The following is an example of setting up multitenancy using configuration as the tenants provider and a claims tenant resolver that determines the current tenant from the user's tenant ID claim:

{% code title="Program.cs" %}
```csharp
services.AddElsa(elsa =>
{
    elsa.UseTenants(tenants =>
    {
        tenants.ConfigureMultitenancy(options =>
        {
            // Configure the tenant resolution pipeline.
            options.TenantResolverPipelineBuilder.Append<ClaimsTenantResolver>();
        });

        // Install the configuration-based tenant provider.
        tenants.UseConfigurationBasedTenantsProvider(options => configuration.GetSection("Multitenancy").Bind(options));
});
```
{% endcode %}

The _appsettings.json_ file would look like this:

{% code title="appsettings.json" %}
```json
{
  "Multitenancy": {
      "Tenants": [
        {
          "Id": "tenant-1",
          "Name": "Tenant 1"
        },
        {
          "Id": "tenant-2",
          "Name": "Tenant 2"
        }
      ]
    }
}
```
{% endcode %}

When using the default Identity module (`Elsa.Identity`), and the signed in user is linked to a tenant, the ID of that tenant is added as a claim.

The **ClaimsTenantResolver** uses that claim to resolve the current tenant.

The following _appsettings.json_ section demonstrates an example of defining users, applications and roles that are linked to a given tenant:

{% code title="appsettings.json" %}
```json
{
  "Identity": {
    "Tokens": {
      "SigningKey": "set-outside-source-control",
      "AccessTokenLifetime": "1:00:00:00",
      "RefreshTokenLifetime": "7:00:00:00"
    },
    "Roles": [
      {
        "Id": "admin-1",
        "Name": "Administrator",
        "Permissions": [
          "*"
        ],
        "TenantId": "tenant-1"
      },
      {
        "Id": "admin-2",
        "Name": "Administrator",
        "Permissions": [
          "*"
        ],
        "TenantId": "tenant-2"
      }
    ],
    "Users": [
      {
        "Id": "a2323f46-42db-4e15-af8b-94238717d817",
        "Name": "admin",
        "HashedPassword": "<generated-password-hash>",
        "HashedPasswordSalt": "<generated-password-salt>",
        "Roles": [
          "admin-1"
        ],
        "TenantId": "tenant-1"
      },
      {
        "Id": "b0cd0e506e713a9d",
        "Name": "alice",
        "Roles": [
          "admin-2"
        ],
        "HashedPassword": "<generated-password-hash>",
        "HashedPasswordSalt": "<generated-password-salt>",
        "TenantId": "tenant-2"
      }
    ],
    "Applications": [
      {
        "Id": "d57030226341448daff5a2935aba2d3f",
        "Name": "Postman",
        "Roles": [
          "admin-1"
        ],
        "ClientId": "HXr0Vzdm9KCZbwsJ",
        "HashedApiKey": "<generated-api-key-hash>",
        "HashedApiKeySalt": "<generated-api-key-salt>",
        "HashedClientSecret": "<generated-client-secret-hash>",
        "HashedClientSecretSalt": "<generated-client-secret-salt>",
        "TenantId": "tenant-1"
      }
    ]
  }
}
```
{% endcode %}

The hash and salt placeholders above are intentionally not usable
credentials. Generate them with the identity tooling used by your deployment,
and keep the signing key and credential material outside source control. Elsa
3.8.0 rejects the known public signing-key samples outside `Development` and
`Demo`.

{% hint style="warning" %}
Primary keys (Id) must be unique across tenants since there's no constraint with tenant IDs. This might change in a future version.
{% endhint %}
