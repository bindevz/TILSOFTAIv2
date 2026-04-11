# TILSOFTAI V3

TILSOFTAI is an internal AI platform powered by a supervisor-driven orchestration runtime. Sprint 7 replaces the legacy catch-all agent with a supervisor-native general agent, policy-gates native capability execution, productionizes the REST/JSON adapter path, and separates native readiness from legacy diagnostics.

## Current Runtime Shape (Sprint 7)

```text
API / Hub / OpenAI surface
  -> ISupervisorRuntime
     -> IIntentClassifier
     -> CapabilityRequestHint
     -> IAgentRegistry
        -> WarehouseAgent          (native capability path, SQL + REST/JSON)
        -> AccountingAgent         (native capability path, SQL)
        -> GeneralChatAgent        (native general response, explicit legacy fallback only)
           -> LegacyChatPipelineBridge
              -> ChatPipeline -> legacy tool/module path

Native capability path:
  DomainAgent
    -> ICapabilityRegistry.GetByDomain(domain)
    -> ICapabilityResolver.Resolve(hint, candidates)
    -> CapabilityAccessPolicy.Evaluate(required roles, allowed tenants)
    -> IToolAdapterRegistry.Resolve(adapterType)
    -> IToolAdapter.ExecuteAsync(request)

Capability registry:
  CompositeCapabilityRegistry
    -> StaticCapabilitySource (WarehouseCapabilities, AccountingCapabilities)
    -> ConfigurationCapabilitySource (appsettings.json)

Write requests:
  -> IApprovalEngine (create -> approve -> execute lifecycle)
     -> IActionRequestStore
     -> IWriteActionGuard
     -> SqlToolAdapter
```

## Sprint 7 Changes

### General fallback modernization
- `LegacyChatDomainAgent` was deleted.
- `GeneralChatAgent` owns unclassified/general requests without blindly proxying to the legacy bridge.
- Explicit legacy fallback remains available through `legacy-chat` or `legacyFallback=true` and is measured separately.

### Governed capability execution
- `CapabilityDescriptor` supports `RequiredRoles` and `AllowedTenants`.
- `WarehouseAgent` and `AccountingAgent` deny unauthorized native execution before adapter resolution.
- Policy denials return `CAPABILITY_ACCESS_DENIED`.

### REST/JSON productionization
- REST endpoint binding is supplied through configuration/capability metadata.
- `RestJsonToolAdapter` supports timeout, retry, auth token, API key/header metadata, tenant/correlation propagation, and classified failures.

### Readiness and validation
- `/health/ready` now uses `NativeRuntimeHealthCheck`; module health is legacy diagnostic only.
- Full unit and integration suites are green, with one intentionally skipped deep analytics E2E test.
- Integration dependency prune warnings were removed by dropping unnecessary direct package references.

## Remaining Transitional Components

These components remain intentionally bounded:
- `LegacyChatPipelineBridge`: fallback only when no native capability matches or a request explicitly asks for legacy fallback.
- `ChatPipeline`: still required behind the bridge fallback path.
- Module loader and module scope resolver: still required by the legacy `ChatPipeline`, diagnostic module health, and module-backed tool catalog behavior. They do not own native readiness or native domain capability execution.
- `InMemoryCapabilityRegistry`: test fixture only.

See `docs/compatibility_debt_report.md` and `docs/enterprise_readiness_gap_report.md` for removal conditions and blockers.

## Documentation

- `docs/architecture_v3.md`
- `docs/compatibility_debt_report.md`
- `docs/enterprise_readiness_gap_report.md`
- `docs/operational_runtime_observability.md`
- `docs/cleanup_report.md`
- `docs/WRITE_PATH_AUDIT.md`

## License

Internal / proprietary.
