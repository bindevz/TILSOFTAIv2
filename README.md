# TILSOFTAI V3

TILSOFTAI is an internal AI platform powered by a supervisor-driven orchestration runtime. Sprint 8 adds governed external connection/secret sourcing, contract-first capability validation, generic native readiness, and a second governed REST-backed capability path.

## Current Runtime Shape (Sprint 8)

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
    -> CapabilityArgumentValidator.Validate(contract, arguments)
    -> IToolAdapterRegistry.Resolve(adapterType)
    -> IToolAdapter.ExecuteAsync(request)

Capability registry:
  CompositeCapabilityRegistry
    -> StaticCapabilitySource (WarehouseCapabilities, AccountingCapabilities)
    -> ConfigurationCapabilitySource (appsettings.json)
    -> ExternalConnections catalog for REST connection/auth/resilience metadata

Write requests:
  -> IApprovalEngine (create -> approve -> execute lifecycle)
     -> IActionRequestStore
     -> IWriteActionGuard
     -> SqlToolAdapter
```

## Sprint 8 Changes

### Governed external integrations
- Added `ExternalConnections` catalog support for REST connection metadata.
- `RestJsonToolAdapter` resolves `AuthTokenSecret`, `ApiKeySecret`, and `HeaderSecrets` through `ISecretProvider`.
- Raw `authToken` and `apiKey` metadata is rejected.

### Contract-first native execution
- `CapabilityDescriptor` supports `ArgumentContract`.
- Warehouse by-item, warehouse external stock, accounting invoice lookup, and accounting exchange-rate lookup validate required/allowed arguments before adapter execution.
- Invalid arguments return `CAPABILITY_ARGUMENT_VALIDATION_FAILED`.

### Legacy and readiness reduction
- Unmatched warehouse/accounting requests now fail deterministically with `DOMAIN_CAPABILITY_NOT_FOUND` instead of going to the bridge.
- `NativeRuntimeHealthCheck` is capability/adapter based rather than warehouse/accounting hardcoded.
- `ModuleLoaderHostedService` starts only when `Modules:EnableLegacyAutoload=true`.

### Second external path
- Added `accounting.exchange-rate.lookup`, a second governed REST-backed capability path using connection catalog and secret-backed API key auth.

## Remaining Transitional Components

These components remain intentionally bounded:
- `LegacyChatPipelineBridge`: explicit legacy fallback only.
- `ChatPipeline`: still required behind the bridge fallback path.
- Module loader and module scope resolver: legacy-only and no longer autoloaded by default. They do not own native readiness or native domain capability execution.
- `InMemoryCapabilityRegistry`: test fixture only.

See `docs/compatibility_debt_report.md` and `docs/enterprise_readiness_gap_report.md` for removal conditions and blockers.

## Documentation

- `docs/architecture_v3.md`
- `docs/compatibility_debt_report.md`
- `docs/enterprise_readiness_gap_report.md`
- `docs/operational_runtime_observability.md`
- `docs/runtime_readiness.md`
- `docs/external_integration_governance.md`
- `docs/cleanup_report.md`
- `docs/WRITE_PATH_AUDIT.md`

## License

Internal / proprietary.
