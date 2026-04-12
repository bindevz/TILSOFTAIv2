# TILSOFTAI V3

TILSOFTAI is an internal AI platform powered by a supervisor-driven orchestration runtime. Sprint 10 adds governed platform catalog mutation, catalog source-of-truth visibility, integrity validation, and explicit module package classifications.

## Current Runtime Shape (Sprint 10)

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
    -> IPlatformCatalogMutationStore
    -> PlatformCatalogChangeRequest
    -> PlatformCapabilityCatalog / PlatformExternalConnectionCatalog

Write requests:
  -> IApprovalEngine (create -> approve -> execute lifecycle)
     -> IActionRequestStore
     -> IWriteActionGuard
     -> SqlToolAdapter
```

## Sprint 10 Changes

### Governed platform catalog mutation
- Added SQL-backed platform catalog change requests.
- Added submit, approve, reject, and apply control-plane APIs.
- Catalog mutation requires configured submit/approve roles and blocks self-approval by default.
- Mutation emits governance audit, config-change audit, and catalog mutation metrics.

### Catalog source-of-truth visibility
- `/health/ready` includes `platform-catalog`.
- Startup reports `platform`, `mixed`, `bootstrap_only`, or `empty` source modes.
- Bootstrap fallback is visible as a degraded readiness state.

### Catalog integrity and contracts
- Platform catalog load and mutation validate duplicate keys, REST connection references, secret references, and required argument contracts.
- Production catalog records include explicit argument contracts.
- No-argument summary/list capabilities reject unexpected arguments.

### Module package classification
- Remaining module packages are classified as packaging-only or diagnostic-only.
- Module health reports classifications while staying outside ready checks.

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
- `docs/module_package_classification.md`
- `docs/deep_analytics_validation_boundary.md`
- `docs/cleanup_report.md`
- `docs/WRITE_PATH_AUDIT.md`

## License

Internal / proprietary.
