# TILSOFTAI V3

TILSOFTAI is an internal AI platform powered by a supervisor-driven orchestration runtime. Sprint 14 adds provider-backed artifact verification, evidence trust tiers, freshness policy, retention snapshots, and hardened audit dossiers around the governed platform catalog.

## Current Runtime Shape (Sprint 14)

```text
API / Hub / OpenAI surface
  -> ISupervisorRuntime
     -> IIntentClassifier
     -> CapabilityRequestHint
     -> IAgentRegistry
        -> WarehouseAgent          (native capability path, SQL + REST/JSON)
        -> AccountingAgent         (native capability path, SQL + REST/JSON)
        -> GeneralChatAgent        (native general response, deterministic unsupported/retired legacy responses)

Native capability path:
  DomainAgent
    -> ICapabilityRegistry.GetByDomain(domain)
    -> ICapabilityResolver.Resolve(hint, candidates)
    -> CapabilityAccessPolicy.Evaluate(required roles, allowed tenants)
    -> CapabilityArgumentValidator.Validate(typed contract, arguments)
    -> IToolAdapterRegistry.Resolve(adapterType)
    -> IToolAdapter.ExecuteAsync(request)

Capability registry:
  CompositeCapabilityRegistry
    -> StaticCapabilitySource (WarehouseCapabilities, AccountingCapabilities)
    -> ConfigurationCapabilitySource (bootstrap only)
    -> PlatformCatalogCapabilitySource (durable platform records)

External connection catalog:
  CompositeExternalConnectionCatalog
    -> PlatformExternalConnectionCatalog (primary)
    -> ConfigurationExternalConnectionCatalog (bootstrap fallback when enabled)

Catalog control plane:
  PlatformCatalogController
    -> IPlatformCatalogControlPlane
    -> preview / submit / approve / reject / apply
    -> expected version + idempotency + risk policy
    -> IPlatformCatalogPromotionGate
    -> IPlatformCatalogCertificationStore
    -> IPlatformCatalogPromotionManifestService
    -> IPlatformCatalogPromotionManifestStore
    -> IPlatformCatalogMutationStore
    -> PlatformCatalogChangeRequest
    -> PlatformCapabilityCatalog / PlatformExternalConnectionCatalog

Write requests:
  -> IApprovalEngine (create -> approve -> execute lifecycle)
     -> IActionRequestStore
     -> IWriteActionGuard
     -> SqlToolAdapter
```

## Sprint 14 Changes

### Artifact trust
- Evidence now has trust tiers including metadata-verified and provider-verified.
- The controlled artifact provider verifies `artifact://catalog-evidence/` bytes under a configured trusted root and compares SHA-256 hashes.

### Freshness and policy
- Production-like environments require provider-verified evidence by default.
- Required certification evidence has per-kind freshness windows that block stale promotion.

### Audit dossiers
- Dossiers now include evidence trust evaluations, retention snapshots, and deterministic dossier hashes.

### Release discipline
- Release policy can require stronger trust tiers per environment.
- Retention policy is executable review data, not only documentation.

## Remaining Transitional Components

These components remain intentionally bounded:
- Module loader infrastructure: legacy diagnostic only and no longer autoloaded by default.
- Module packages: still present for packaging/diagnostics with explicit classifications, but default request routing is supervisor-native and capability-native.
- `InMemoryCapabilityRegistry`: test fixture only.

See `docs/compatibility_debt_report.md` and `docs/enterprise_readiness_gap_report.md` for removal conditions and blockers.

## Documentation

- `docs/architecture_v3.md`
- `docs/compatibility_debt_report.md`
- `docs/enterprise_readiness_gap_report.md`
- `docs/operational_runtime_observability.md`
- `docs/runtime_readiness.md`
- `docs/external_integration_governance.md`
- `docs/platform_catalog_governance.md`
- `docs/catalog_control_plane_runbook.md`
- `docs/catalog_failure_drills.md`
- `docs/catalog_contract_schema_lifecycle.md`
- `docs/catalog_live_certification_evidence.md`
- `docs/catalog_evidence_integrity.md`
- `docs/catalog_artifact_trust_and_retention.md`
- `docs/catalog_release_gates.md`
- `docs/catalog_promotion_manifest_provenance.md`
- `docs/catalog_promotion_audit_dossier.md`
- `docs/catalog_control_plane_slos_alerts.md`
- `docs/catalog_emergency_path_policy.md`
- `docs/module_package_classification.md`
- `docs/deep_analytics_validation_boundary.md`
- `docs/cleanup_report.md`
- `docs/WRITE_PATH_AUDIT.md`

## License

Internal / proprietary.
