# TILSOFTAI V3

TILSOFTAI is an internal AI platform powered by a supervisor-driven orchestration runtime. Sprint 12 adds promotion gates, certification evidence capture, control-plane SLO definitions, and emergency-path containment around the governed platform catalog.

## Current Runtime Shape (Sprint 12)

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
    -> IPlatformCatalogMutationStore
    -> PlatformCatalogChangeRequest
    -> PlatformCapabilityCatalog / PlatformExternalConnectionCatalog

Write requests:
  -> IApprovalEngine (create -> approve -> execute lifecycle)
     -> IActionRequestStore
     -> IWriteActionGuard
     -> SqlToolAdapter
```

## Sprint 12 Changes

### Promotion gates and evidence
- Added promotion gate evaluation for source mode, preview validity, approved-change safety, expected-version policy, and certification evidence.
- Added certification evidence capture for runbook execution, failure drills, and operator sign-off.
- Added SQL storage for certification evidence.

### SLOs and alerts
- Added control-plane SLO definitions for preview, submit, approve, apply, and rollback readiness.
- Added promotion gate and certification evidence metrics.

### Emergency containment
- Documented fallback re-enable and break-glass authorization policy.
- Promotion gates block unsafe fallback posture and break-glass changes without after-action evidence.

### Release discipline
- Catalog release gate docs define CI/CD blockers and deterministic operator-readable blocker codes.
- Live certification docs define accepted evidence kinds without fabricating environment execution.

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
- `docs/catalog_release_gates.md`
- `docs/catalog_control_plane_slos_alerts.md`
- `docs/catalog_emergency_path_policy.md`
- `docs/module_package_classification.md`
- `docs/deep_analytics_validation_boundary.md`
- `docs/cleanup_report.md`
- `docs/WRITE_PATH_AUDIT.md`

## License

Internal / proprietary.
