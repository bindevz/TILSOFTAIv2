# TILSOFTAI V3

TILSOFTAI is an internal AI platform powered by a supervisor-driven orchestration runtime. Sprint 5 transitions the architecture from a single-domain native demonstration to a repeatable multi-domain platform pattern with structured capability resolution and data-driven registry loading.

## Current runtime shape (Sprint 5)

```text
API / Hub / OpenAI Surface
  -> IOrchestrationEngine (compatibility facade — [Obsolete])
     -> ISupervisorRuntime
        -> IIntentClassifier (keyword-based)
        -> CapabilityRequestHint (structured resolution hints)
        -> IAgentRegistry
           -> WarehouseAgent          (native capability path)
           -> AccountingAgent         (native capability path)
           -> LegacyChatDomainAgent   (catch-all fallback)
              -> LegacyChatPipelineBridge
                 -> ChatPipeline -> IToolHandler -> SqlToolAdapter

Native capability path (Sprint 5):
  DomainAgent
    -> ICapabilityRegistry.GetByDomain(domain)
    -> ICapabilityResolver.Resolve(hint, candidates)
    -> IToolAdapterRegistry.Resolve(adapterType)
    -> IToolAdapter.ExecuteAsync(request)

Capability registry (Sprint 5):
  CompositeCapabilityRegistry
    -> StaticCapabilitySource (WarehouseCapabilities, AccountingCapabilities)
    -> ConfigurationCapabilitySource (appsettings.json)

Write requests:
  -> IApprovalEngine (create -> approve -> execute lifecycle)
     -> IActionRequestStore
     -> IWriteActionGuard (adapter-level enforcement)
     -> SqlToolAdapter
```

## Sprint 5 changes

### Structured capability resolution
- `CapabilityRequestHint` — structured hint with explicit capability key, domain, operation, and subject keywords
- `ICapabilityResolver` / `StructuredCapabilityResolver` — replaces naive string-matching
  - Priority: (1) exact capability key match, (2) keyword-to-capability matching
- `SupervisorRuntime` now builds and attaches hints to `AgentTask.CapabilityHint`
- Explicit `capabilityKey` can be provided via `request.Metadata["capabilityKey"]` for API-driven resolution

### Accounting native execution
- `AccountingAgent` upgraded from bridge-only to full native capability path
- Three read-only accounting capabilities:
  - `accounting.receivables.summary` → `dbo.ai_accounting_receivables_summary`
  - `accounting.payables.summary` → `dbo.ai_accounting_payables_summary`
  - `accounting.invoice.by-number` → `dbo.ai_accounting_invoice_by_number`
- Same pattern as `WarehouseAgent`: capability resolution → adapter execution → bridge fallback

### Data-driven capability registry
- `ICapabilitySource` — pluggable source interface for capability definitions
- `ConfigurationCapabilitySource` — loads capabilities from `appsettings.json` Capabilities section
- `CompositeCapabilityRegistry` — production registry that merges static + configuration sources
- `InMemoryCapabilityRegistry` retained as test fixture only

### Warehouse agent migration
- `WarehouseAgent` migrated to use `ICapabilityResolver` (same structured resolution as accounting)
- Legacy `ResolveCapability(string)` method marked `[Obsolete]`

### Compatibility debt reduction
- `IOrchestrationEngine` / `OrchestrationEngine` marked `[Obsolete]`
- `LegacyChatPipelineBridge` documented with explicit removal conditions
- `LegacyChatDomainAgent` documented with removal conditions
- Full compatibility debt report: `docs/compatibility_debt_report.md`

### Integration test suite
- `AccountingNativePathIntegrationTests` — full wiring: supervisor → accounting agent → adapter
- `WarehouseNativePathIntegrationTests` — non-regression of warehouse native path
- `ApprovalLifecycleIntegrationTests` — full create → approve → execute with in-memory persistence
- `AuthEnabledRequestPathIntegrationTests` — tenant/correlation context threading

## Sprint 4 changes (retained)

### Capability model
- `CapabilityDescriptor` — first-class runtime capability metadata
- `ICapabilityRegistry` — runtime capability resolution by domain or key
- `WarehouseCapabilities` — three read-only warehouse capabilities

### Auth hardening
- `[AllowAnonymous]` removed from `ChatController`
- When `Auth:Enabled=true`: JWT authentication required
- When `Auth:Enabled=false`: `NoAuthHandler` auto-succeeds

### Approval engine
- Full create → approve → execute → reject lifecycle
- `IWriteActionGuard` enforces approval at adapter level

## Transitional compatibility

These components remain on purpose:
- `IOrchestrationEngine` / `OrchestrationEngine` — [Obsolete], migrate controllers to `ISupervisorRuntime`
- `LegacyChatPipelineBridge` — fallback for non-native capability requests
- `LegacyChatDomainAgent` — catch-all for unclassified requests
- `ActionApprovalService` — Sprint 1 facade (delegates to `IApprovalEngine`)
- Module loading / scope resolution — until capability-pack migration

See `docs/compatibility_debt_report.md` for full tracking.

## What's next (Sprint 6 priorities)

- Remove `IOrchestrationEngine` compatibility facade — migrate controllers to `ISupervisorRuntime`
- Replace `LegacyChatDomainAgent` with purpose-built general/chat agent
- Begin capability-pack migration (replace module scope resolution)
- Non-SQL adapter implementations (REST, gRPC)
- Write-path capabilities for warehouse and accounting domains

## Documentation

- `docs/architecture_v3.md`
- `docs/migration_v2_to_v3.md`
- `docs/cleanup_report.md`
- `docs/compatibility_debt_report.md`
- `docs/WRITE_PATH_AUDIT.md`

## License

Internal / proprietary.
