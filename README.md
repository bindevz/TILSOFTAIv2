# TILSOFTAI V3

TILSOFTAI is an internal AI platform powered by a supervisor-driven orchestration runtime. Sprint 13 adds verified evidence, immutable promotion manifests, rollout attestations, and audit dossiers around the governed platform catalog.

## Current Runtime Shape (Sprint 13)

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

## Sprint 13 Changes

### Evidence trust
- Evidence lifecycle now distinguishes recorded, verified, accepted, expired, superseded, and rejected evidence.
- Trusted evidence requires verification status, allowed evidence references, artifact hashes, source metadata, and collection timestamps.

### Promotion provenance
- Added immutable promotion manifests with manifest hashes, change ids, evidence ids, gate summaries, actor identity, and environment.
- Added append-only rollout attestations for issued, started, completed, failed, aborted, and superseded rollout states.

### Audit dossiers
- Added deterministic dossier output for manifest, change, evidence, and attestation review.
- Added docs for evidence integrity, manifest provenance, and audit bundle review.

### Release discipline
- Production-like promotion now requires trusted evidence, not just accepted evidence metadata.
- Production-like rollout completion requires trusted attestation evidence by default.

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
