# Platform Catalog Governance - Sprint 11

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

## Governed Change Control

Sprint 10 adds the platform catalog control plane for admin-managed mutation.

Control-plane endpoints:

- `GET /api/platform-catalog/capabilities`
- `GET /api/platform-catalog/external-connections`
- `GET /api/platform-catalog/changes`
- `POST /api/platform-catalog/changes/preview`
- `POST /api/platform-catalog/changes`
- `POST /api/platform-catalog/changes/{changeId}/approve`
- `POST /api/platform-catalog/changes/{changeId}/reject`
- `POST /api/platform-catalog/changes/{changeId}/apply`

Configuration:

```json
{
  "CatalogControlPlane": {
    "EnvironmentName": "prod",
    "SubmitRoles": [ "platform_catalog_admin" ],
    "ApproveRoles": [ "platform_catalog_approver" ],
    "ApplyRoles": [ "platform_catalog_operator" ],
    "HighRiskApproveRoles": [ "platform_catalog_senior_approver" ],
    "BreakGlassRoles": [ "platform_catalog_break_glass" ],
    "ProductionLikeEnvironments": [ "prod", "production", "staging" ],
    "AllowSelfApproval": false,
    "RequireExpectedVersionForExistingRecordsInProductionLike": true,
    "RequireIndependentApplyInProductionLike": true,
    "AllowBreakGlass": false
  }
}
```

Platform catalog changes must include:

- capability or connection owner,
- submitter/reviewer role validation,
- secret references instead of secret values,
- typed argument contract review,
- version/change note,
- expected version for existing records in production-like environments,
- idempotency key for replay-safe submit,
- rollback reference when a change compensates for a previous applied change.

The control plane creates pending change requests first. A separate approver must approve the request before it can be applied when self-approval is disabled.

High-risk changes include disables and external connection mutations. They require senior approval unless explicit break-glass is enabled and audited.

Every preview, submit, duplicate-submit replay, approve, reject, apply, and apply replay operation emits governance/config-change audit events or mutation metrics and increments `tilsoftai_platform_catalog_mutations_total`.

## Change Safety

Sprint 11 adds:

- optimistic concurrency through `ExpectedVersionTag`,
- duplicate pending-change detection through payload hash and `IdempotencyKey`,
- dry-run preview through `POST /api/platform-catalog/changes/preview`,
- idempotent apply replay for already applied changes,
- production-like independent apply policy,
- rollback-by-compensating-change metadata through `RollbackOfChangeId`.

Operators should preview every production change before submit and include a ticket-derived idempotency key.

## Durable SQL Shape

Sprint 10 SQL catalog shape includes durable records, change requests, list procedures, and mutation procedures:

- `dbo.PlatformCapabilityCatalog`
- `dbo.PlatformExternalConnectionCatalog`
- `dbo.PlatformCatalogChangeRequest`
- `dbo.app_platform_capabilitycatalog_list`
- `dbo.app_platform_externalconnectioncatalog_list`
- `dbo.app_platform_catalogchange_create`
- `dbo.app_platform_catalogchange_get`
- `dbo.app_platform_catalogchange_list`
- `dbo.app_platform_catalogchange_approve`
- `dbo.app_platform_catalogchange_reject`
- `dbo.app_platform_catalogchange_mark_applied`
- `dbo.app_platform_capabilitycatalog_upsert`
- `dbo.app_platform_capabilitycatalog_disable`
- `dbo.app_platform_externalconnectioncatalog_upsert`
- `dbo.app_platform_externalconnectioncatalog_disable`
- `dbo.app_platform_catalogrecord_version`
- `dbo.app_platform_catalogchange_find_duplicate`

The file catalog at `catalog/platform-catalog.json` remains the bootstrapped durable platform record set for local/runtime startup. SQL is the admin-managed mutation target for catalog operations.

## Integrity Rules

Catalog integrity validation rejects:

- duplicate capability keys,
- capability records without key/domain/adapter/operation,
- REST capabilities without a resolvable `connectionName`,
- raw secret-bearing capability metadata or connection headers,
- auth/API-key connections without secret references,
- capabilities without an `ArgumentContract`,
- contract rules missing a name or type.

Startup and readiness expose whether the active runtime is using platform records, bootstrap fallback records, mixed records, or no catalog records.
