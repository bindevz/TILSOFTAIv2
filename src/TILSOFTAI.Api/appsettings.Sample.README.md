# appsettings Configuration Guide

## Connection String

The `Sql:ConnectionString` is intentionally empty in `appsettings.json`.

### How to configure:

**Option 1: Environment Variable (recommended for production)**
```
Sql__ConnectionString=Server=.;Database=TILSOFTAI;User ID=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;
```

**Option 2: User Secrets (recommended for development)**
```bash
dotnet user-secrets set "Sql:ConnectionString" "Server=.;Database=TILSOFTAI;User ID=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
```

**Option 3: Local Override File (gitignored)**
Create `appsettings.Local.json`:
```json
{
  "Sql": {
    "ConnectionString": "Server=.;Database=TILSOFTAI;User ID=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  }
}
```

> **Note:** Never commit passwords to source control.

## Platform Catalog Configuration

Production capability and external connection records are loaded from the platform catalog, not primarily from `appsettings.json`.

```json
{
  "PlatformCatalog": {
    "Enabled": true,
    "CatalogPath": "catalog/platform-catalog.json",
    "AllowBootstrapConfigurationFallback": false,
    "EnvironmentName": "prod",
    "ProductionLikeEnvironments": [ "prod", "production", "staging" ],
    "TreatMixedAsUnhealthyInProductionLike": true,
    "TreatBootstrapOnlyAsUnhealthyInProductionLike": true
  },
  "Capabilities": [],
  "ExternalConnections": {
    "Connections": {}
  }
}
```

Source precedence is:

1. Static fallback capabilities.
2. Bootstrap app configuration, when present.
3. Durable platform catalog records from `PlatformCatalog:CatalogPath`.

Catalog capability records use the same shape as bootstrap capability records:

```json
{
  "CapabilityKey": "warehouse.external-stock.lookup",
  "Domain": "warehouse",
  "AdapterType": "rest-json",
  "Operation": "execute_http_json",
  "TargetSystemId": "external-stock-api",
  "ExecutionMode": "readonly",
  "RequiredRoles": [ "warehouse_external_read" ],
  "AllowedTenants": [],
  "IntegrationBinding": {
    "connectionName": "external-stock-api",
    "endpoint": "/warehouse/external-stock",
    "method": "GET"
  },
  "ArgumentContract": {
    "RequiredArguments": [ "@ItemNo" ],
    "AllowedArguments": [ "@ItemNo" ],
    "AllowAdditionalArguments": false,
    "Arguments": [
      {
        "Name": "@ItemNo",
        "Type": "string",
        "Format": "item-number",
        "MinLength": 1,
        "MaxLength": 50
      }
    ]
  }
}
```

`RequiredRoles`, `AllowedTenants`, and typed `ArgumentContract` rules are enforced before adapter resolution.

External auth belongs in the connection catalog, not raw capability metadata:

```json
{
  "ExternalConnections": {
    "Connections": {
      "external-stock-api": {
        "BaseUrl": "https://external-stock.example.com",
        "AuthScheme": "Bearer",
        "AuthTokenSecret": "tilsoft/external-stock-api/token",
        "TimeoutSeconds": 10,
        "RetryCount": 2,
        "RetryDelayMs": 100
      }
    }
  }
}
```

The REST adapter resolves secret references through `ISecretProvider` and rejects raw `authToken` or `apiKey` metadata.

## Platform Catalog Control Plane

Catalog writes are governed by submit/review/apply roles.

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
    "AllowBreakGlass": false,
    "MinBreakGlassJustificationLength": 20
  }
}
```

## Catalog Certification And Promotion Gates

Production-like promotion requires accepted certification evidence before catalog changes can pass the promotion gate.

```json
{
  "CatalogCertification": {
    "EnvironmentName": "prod",
    "ProductionLikeEnvironments": [ "prod", "production", "staging" ],
    "RequireCertificationEvidenceForProductionLikePromotion": true,
    "RequireTrustedEvidenceForProductionLikePromotion": true,
    "RequireArtifactHashForTrustedEvidence": true,
    "RequireEvidenceUriForTrustedEvidence": true,
    "RequireRolloutAttestationEvidenceForProductionLikeCompletion": true,
    "MaxTrustedEvidenceAgeDays": 90,
    "MinimumEvidenceTrustTierForProductionLikePromotion": "provider_verified",
    "EnvironmentMinimumEvidenceTrustTiers": {
      "prod": "provider_verified",
      "production": "provider_verified",
      "staging": "metadata_verified"
    },
    "EvidenceFreshnessDaysByKind": {
      "runbook_execution": 30,
      "preview_failure_drill": 30,
      "version_conflict_drill": 30,
      "duplicate_submit_drill": 30,
      "sql_apply_outage_drill": 30,
      "fallback_risk_drill": 30,
      "operator_signoff": 14
    },
    "TrustedArtifactRootPath": "evidence-artifacts",
    "ControlledArtifactUriPrefixes": [ "artifact://catalog-evidence/" ],
    "EvidenceRetentionDays": 2555,
    "ManifestRetentionDays": 2555,
    "AttestationRetentionDays": 2555,
    "DossierArchiveRetentionDays": 2555,
    "RequireArchiveForProductionLikeDossiers": true,
    "TrustedEvidenceStatuses": [ "accepted" ],
    "AllowedEvidenceUriPrefixes": [ "https://evidence.example/", "artifact://catalog-evidence/" ],
    "AllowedEvidenceContentTypes": [ "application/json", "application/pdf", "text/plain", "text/markdown" ],
    "AllowedEvidenceSourceSystems": [ "ci", "runbook", "incident", "release" ],
    "RequiredEvidenceKinds": [
      "runbook_execution",
      "preview_failure_drill",
      "version_conflict_drill",
      "duplicate_submit_drill",
      "sql_apply_outage_drill",
      "fallback_risk_drill",
      "operator_signoff"
    ],
    "PreviewSuccessSloPercent": 99,
    "SubmitSuccessSloPercent": 99,
    "ApproveSuccessSloPercent": 99,
    "ApplySuccessSloPercent": 99,
    "RollbackReadyMinutes": 30,
    "VersionConflictAlertThresholdPerHour": 3,
    "DuplicateSubmitAlertThresholdPerHour": 5,
    "ApplyFailureAlertThresholdPerHour": 1,
    "RollbackSurgeAlertThresholdPerHour": 2
  }
}
```

Promotion gate endpoints:

- `POST /api/platform-catalog/promotion-gate/evaluate`
- `GET /api/platform-catalog/slo-definitions`
- `GET /api/platform-catalog/certification-evidence?environmentName=prod`
- `POST /api/platform-catalog/certification-evidence`
- `POST /api/platform-catalog/certification-evidence/{evidenceId}/verify`
- `GET /api/platform-catalog/promotion-manifests?environmentName=prod`
- `GET /api/platform-catalog/promotion-manifests/{manifestId}`
- `POST /api/platform-catalog/promotion-manifests`
- `POST /api/platform-catalog/promotion-manifests/{manifestId}/attestations`
- `GET /api/platform-catalog/promotion-manifests/{manifestId}/dossier`

The gate blocks unsafe source modes, failed previews, missing expected versions, unapproved changes, break-glass changes that lack after-action evidence, missing certification evidence, untrusted certification evidence, insufficient trust tier, and stale live-certification evidence. Promotion manifests become the immutable release record after gate success and trusted evidence verification.

The control plane stores pending changes in SQL before applying them to `PlatformCapabilityCatalog` or `PlatformExternalConnectionCatalog`. Each change must include an owner, change note, record type, operation, and record payload. Capability records must include an `ArgumentContract`; REST records must reference a configured external connection and secret references instead of raw secret values.

For existing records in production-like environments, mutation requests must include `ExpectedVersionTag`. Use `POST /api/platform-catalog/changes/preview` before submit to verify the expected version, payload hash, risk level, and duplicate pending-change status.

Optional safety fields:

```json
{
  "ExpectedVersionTag": "catalog-v12",
  "IdempotencyKey": "change-ticket-12345",
  "RollbackOfChangeId": "previous-change-id-when-this-is-a-compensating-change"
}
```

`/health/ready` includes `platform-catalog` and reports the active source mode:

- `platform`: durable platform catalog records are active.
- `mixed`: platform records and bootstrap fallback records both exist.
- `bootstrap_only`: bootstrap fallback is serving records.
- `empty`: no catalog records are available.

## Legacy Package Classification

Default runtime configuration does not include a module activation section. Production API startup resolves tools from native registries and the platform catalog; the old Platform and Analytics package shells were retired in Sprint 21.
