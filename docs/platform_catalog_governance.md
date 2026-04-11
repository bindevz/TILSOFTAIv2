# Platform Catalog Governance - Sprint 9

Production capability and external connection records are now platform catalog records.

`appsettings.json` can still provide bootstrap records for local development or emergency startup, but it is no longer the primary production catalog.

## Source Precedence

Capability records load in this order:

1. Static fallback descriptors in code.
2. Bootstrap configuration records from `Capabilities`.
3. Durable platform catalog records from `PlatformCatalog:CatalogPath`.

Later sources override earlier records by `CapabilityKey`.

External connection records resolve in this order:

1. Durable platform catalog records.
2. Bootstrap configuration records from `ExternalConnections`, only when `PlatformCatalog:AllowBootstrapConfigurationFallback=true`.

## Change Control

Platform catalog changes must include:

- capability or connection owner,
- reviewed role and tenant policy,
- secret references instead of secret values,
- typed argument contract review,
- version/change note,
- deployment or migration audit trail.

## Durable SQL Shape

Sprint 9 adds SQL catalog tables and list procedures:

- `dbo.PlatformCapabilityCatalog`
- `dbo.PlatformExternalConnectionCatalog`
- `dbo.app_platform_capabilitycatalog_list`
- `dbo.app_platform_externalconnectioncatalog_list`

The file catalog at `catalog/platform-catalog.json` is the current bootstrapped durable platform record set. The SQL shape is the admin-managed persistence target for the next catalog write path.
