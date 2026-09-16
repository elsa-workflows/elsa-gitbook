---
description: >-
  Explain how Elsa 3.8.2 separates tenant resolution from storage isolation,
  including the default in-memory key-value store boundary.
---

# Storage boundaries

Tenant resolution and tenant isolation are related, but they are different
responsibilities:

- **Tenant resolution** selects the current tenant for a request or background
  operation.
- **Storage isolation** determines which records a provider reads, writes, and
  deletes for that tenant.

Enabling `UseTenants()` configures Elsa's tenant feature and resolver pipeline;
it does not make every storage implementation tenant-aware.

## Provider behavior in 3.8.2

| Boundary | Release behavior | Deployment guidance |
| --- | --- | --- |
| Shared EF Core database | When multitenancy is enabled, Elsa adds an EF Core query filter for `Entity` records. It matches the current tenant, the tenant-agnostic ID (`"*"`), and legacy records with no tenant ID when the current tenant is the default tenant (`""`). | Configure tenant resolution before using the context, and test both tenant-specific and tenant-agnostic records. |
| Separate database per tenant | A host or provider can choose a connection using the current tenant accessor. Core's tenant-aware EF factory carries the resolved tenant ID into the context; it does not choose a database for the host. | Configure the connection factory for every Elsa persistence module that should be isolated, and verify that a missing or incorrect tenant does not select the wrong database. |
| Default in-memory `IKeyValueStore` | Core 3.8.2 registers `MemoryKeyValueStore` over a process-local `MemoryStore`. It saves, queries, and deletes by key; it does not apply the ambient tenant to those operations. | Use it for local development or tests only. It is not a tenant-isolation or multi-node coordination boundary. |
| Custom or extension store | Tenant filtering is an implementation responsibility unless the provider explicitly supplies it. A `TenantId` field by itself does not enforce isolation. | Test reads, writes, updates, deletes, list operations, and background processing under more than one tenant. |

The EF Core behavior above is implemented by `SetTenantIdFilter` and is only
installed when `TenantsOptions.IsEnabled` is true. The default in-memory
key-value behavior is intentionally narrower: its `KeyValueFilter` filters
keys and limits results, not tenant IDs.

## The in-memory key-value warning

The default `IKeyValueStore` is infrastructure storage, not the workflow
definition or workflow instance store. Elsa uses it for small runtime records
in features such as the workflow dispatch outbox, instance heartbeats, and
persisted administrative pauses. In 3.8.2, the default implementation is
process-local and its records disappear when the process exits.

Do not put tenant-specific coordination data in the default store and assume
that `UseTenants()` will hide it from other tenants. For a shared or
multi-tenant deployment:

1. Configure a durable key-value provider supplied by your persistence setup.
2. Point every node at the same logical storage resource.
3. Confirm that the selected provider's reads, writes, and deletes enforce the
   tenant policy your deployment requires.
4. Exercise a cross-tenant test and a restart test before adding another node.

See [Runtime coordination storage](../guides/architecture/runtime-coordination-storage.md)
for provider registration and the list of runtime features that depend on
`IKeyValueStore`.

{% hint style="warning" %}
`TenantId = "*"` is Elsa's tenant-agnostic convention for stores that support
that concept. It is part of the EF Core query-filter contract; it does not
automatically change the behavior of an arbitrary custom or in-memory store.
{% endhint %}

## Release source

This page was checked against Core `release/3.8.2` at
[`33181ae3`](https://github.com/elsa-workflows/elsa-core/tree/33181ae3048f628f591a0155b5665a8e4d1bcea2),
Studio `release/3.8.2` at
[`1c72dc02`](https://github.com/elsa-workflows/elsa-studio/tree/1c72dc02c837919059b60efe5df2ed57ff2db2d9),
and Extensions `release/3.8.2` at
[`e7d05ef9`](https://github.com/elsa-workflows/elsa-extensions/tree/e7d05ef930dfde2aa85bf9dcaf841e5a6ede0a1e).
The relevant contracts are:

- [`TenantsFeature`](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Tenants/Features/TenantsFeature.cs)
  enables tenant services and sets `TenantsOptions.IsEnabled`.
- [`SetTenantIdFilter`](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Persistence.EFCore.Common/EntityHandlers/SetTenantIdFilter.cs)
  applies the EF Core tenant filter.
- [`TenantAwareDbContextFactory`](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.Persistence.EFCore.Common/TenantAwareDbContextFactory.cs)
  carries the current tenant ID into an Elsa EF context.
- [`MemoryKeyValueStore`](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.KeyValues/Stores/MemoryKeyValueStore.cs)
  delegates key-value operations to the in-memory store without tenant
  filtering.
- [`KeyValueFilter`](https://github.com/elsa-workflows/elsa-core/blob/33181ae3048f628f591a0155b5665a8e4d1bcea2/src/modules/Elsa.KeyValues/Models/KeyValueFilter.cs)
  defines key, prefix, ordering, and limit filters.

## Related guides

- [Multitenancy introduction](introduction.md)
- [Multitenancy setup](setup.md)
- [Persistence](../guides/persistence/README.md)
- [Runtime coordination storage](../guides/architecture/runtime-coordination-storage.md)
