# TILSOFTAI V3

TILSOFTAI is an internal AI platform powered by a supervisor-driven orchestration runtime. Sprint 9 retires the legacy bridge/ChatPipeline runtime path, moves production metadata into a platform catalog, and hardens representative capability contracts with typed value validation.

## Current Runtime Shape (Sprint 9)

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

Write requests:
  -> IApprovalEngine (create -> approve -> execute lifecycle)
     -> IActionRequestStore
     -> IWriteActionGuard
     -> SqlToolAdapter
```

## Sprint 9 Changes

### Legacy runtime retirement
- Deleted `LegacyChatPipelineBridge`.
- Deleted `ChatPipeline`, `ChatRequest`, and `ChatResult`.
- Explicit legacy fallback now returns `LEGACY_RUNTIME_RETIRED`.
- Default runtime boot no longer registers module scope, ReAct follow-up policy, or legacy pipeline instrumentation services.

### Durable platform catalog
- Added `catalog/platform-catalog.json` as the platform-owned capability/connection record set.
- Added `PlatformCatalogCapabilitySource` and `CompositeExternalConnectionCatalog`.
- `appsettings.json` now points at the platform catalog and keeps bootstrap capability/connection sections empty.
- Added SQL catalog DDL/procedures for the admin-managed persistence target.

### Typed contract validation
- `ArgumentContract` supports typed argument rules.
- Representative capabilities validate string/integer/number/boolean type, enum values, formats, and length/range constraints before adapter execution.
- Invalid typed values return `CAPABILITY_ARGUMENT_VALIDATION_FAILED`.

### Validation boundary cleanup
- Deep analytics E2E is formally isolated as `Category=ExternalDeepWorkflow`, owned by Analytics, and gated by `TEST_SQL_CONNECTION`.

## Remaining Transitional Components

These components remain intentionally bounded:
- Module loader infrastructure: legacy diagnostic only and no longer autoloaded by default.
- Module packages: still present for tools/diagnostics, but default request routing is supervisor-native and capability-native.
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
- `docs/deep_analytics_validation_boundary.md`
- `docs/cleanup_report.md`
- `docs/WRITE_PATH_AUDIT.md`

## License

Internal / proprietary.
