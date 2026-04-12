# TILSOFTAI V3

TILSOFTAI is an internal AI platform powered by a supervisor-driven orchestration runtime. Sprint 11 hardens the governed platform catalog control plane with preview, version safety, idempotent replay behavior, production-like approval policy, stricter fallback posture, and contract schema lifecycle metadata.

## Current Runtime Shape (Sprint 11)

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
    -> IPlatformCatalogMutationStore
    -> PlatformCatalogChangeRequest
    -> PlatformCapabilityCatalog / PlatformExternalConnectionCatalog

Write requests:
  -> IApprovalEngine (create -> approve -> execute lifecycle)
     -> IActionRequestStore
     -> IWriteActionGuard
     -> SqlToolAdapter
```

## Sprint 11 Changes

### Production-hard catalog mutation
- Added dry-run preview for mutation validation.
- Added expected-version checks for existing-record changes.
- Added duplicate pending-change detection through payload hash and idempotency key.
- Added idempotent apply replay for already applied changes.
- Added rollback metadata through `RollbackOfChangeId` for compensating changes.

### Policy-grade governance
- Split submit, approve, apply, high-risk approval, and break-glass roles.
- Production-like environments require expected versions and can require independent apply.
- Disables and external connection changes are high-risk.

### Source-of-truth tightening
- Production config disables bootstrap fallback by default.
- `mixed` and `bootstrap_only` source modes are unhealthy in production-like environments when strict posture is enabled.

### Contract and module end-state
- `ArgumentContract` includes `ContractVersion`, `SchemaDialect`, and `SchemaRef`.
- Module packages are formally retained only as non-runtime packaging or diagnostic artifacts.

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
- `docs/module_package_classification.md`
- `docs/deep_analytics_validation_boundary.md`
- `docs/cleanup_report.md`
- `docs/WRITE_PATH_AUDIT.md`

## License

Internal / proprietary.
