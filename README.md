# TILSOFTAI V3

TILSOFTAI is an internal AI platform powered by a supervisor-driven orchestration runtime. Sprint 6 removes obsolete edge and approval facades, adds runtime execution telemetry, and proves the adapter model with a REST/JSON capability path in addition to SQL-backed capabilities.

## Current Runtime Shape (Sprint 6)

```text
API / Hub / OpenAI surface
  -> ISupervisorRuntime
     -> IIntentClassifier
     -> CapabilityRequestHint
     -> IAgentRegistry
        -> WarehouseAgent          (native capability path, SQL + REST/JSON)
        -> AccountingAgent         (native capability path, SQL)
        -> LegacyChatDomainAgent   (fallback only)
           -> LegacyChatPipelineBridge
              -> ChatPipeline -> legacy tool/module path

Native capability path:
  DomainAgent
    -> ICapabilityRegistry.GetByDomain(domain)
    -> ICapabilityResolver.Resolve(hint, candidates)
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

## Sprint 6 Changes

### Removed obsolete facades
- `ChatController`, `OpenAiChatCompletionsController`, and `ChatHub` now call `ISupervisorRuntime` directly.
- `IOrchestrationEngine` and `OrchestrationEngine` were deleted.
- `ActionApprovalService` was deleted; approval callers use `IApprovalEngine` directly.

### Native runtime observability
- Added `RuntimeExecutionInstrumentation` for supervisor, native, bridge, approval, capability, and adapter-failure signals.
- Added Prometheus metric names for native execution count, bridge fallback count, approval executions, capability invocation count, adapter failures, and runtime duration.
- Native and bridge paths now emit structured logs and counters with agent, capability, adapter, duration, and success labels.

### Non-SQL adapter proof
- Added `RestJsonToolAdapter` with adapter type `rest-json`.
- Added `warehouse.external-stock.lookup`, a warehouse native capability that executes through REST/JSON via the standard registry, resolver, adapter registry, and agent-owned native execution path.

### Integration validation
- Added HTTP-level ASP.NET pipeline tests for authenticated chat and authorization failure.
- Added REST-backed warehouse native integration coverage.
- Existing native warehouse, native accounting, approval lifecycle, and auth context threading integration tests remain in place.

## Remaining Transitional Components

These components remain intentionally bounded:
- `LegacyChatPipelineBridge`: fallback only when no native capability matches.
- `LegacyChatDomainAgent`: catch-all for unclassified or explicitly legacy requests.
- `ChatPipeline`: still required behind the bridge fallback path.
- Module loader and module scope resolver: still required by the legacy `ChatPipeline`, health checks, and module-backed tool catalog behavior. They do not own native domain capability execution.
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
