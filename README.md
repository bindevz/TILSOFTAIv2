# TILSOFTAI V3

TILSOFTAI is evolving from a module-centric, SQL-first orchestration runtime into a supervisor-driven internal AI platform. Sprint 4 introduces the first domain-native execution path, a concrete capability model, and production auth hardening.

## Current runtime shape (Sprint 4)

```text
API / Hub / OpenAI Surface
  -> IOrchestrationEngine (compatibility facade)
     -> ISupervisorRuntime
        -> IIntentClassifier (keyword-based)
        -> IAgentRegistry
           -> WarehouseAgent          (native capability path via IToolAdapterRegistry)
           -> AccountingAgent         (bridge delegation — not yet native)
           -> LegacyChatDomainAgent   (catch-all fallback)
              -> LegacyChatPipelineBridge
                 -> ChatPipeline -> IToolHandler -> SqlToolAdapter

Native capability path (Sprint 4):
  WarehouseAgent
    -> ICapabilityRegistry.Resolve(capabilityKey)
    -> IToolAdapterRegistry.Resolve(adapterType)
    -> IToolAdapter.ExecuteAsync(request)

Write requests:
  -> IApprovalEngine (create -> approve -> execute lifecycle)
     -> IActionRequestStore
     -> IWriteActionGuard (adapter-level enforcement)
     -> SqlToolAdapter
```

## Sprint 4 changes

### Capability model
- `CapabilityDescriptor` — first-class runtime capability metadata (key, domain, adapter type, operation, integration binding, execution mode)
- `ICapabilityRegistry` / `InMemoryCapabilityRegistry` — runtime capability resolution by domain or key
- `WarehouseCapabilities` — three read-only warehouse capabilities seeded at startup:
  - `warehouse.inventory.summary` → `dbo.ai_warehouse_inventory_summary`
  - `warehouse.inventory.by-item` → `dbo.ai_warehouse_inventory_by_item`
  - `warehouse.receipts.recent` → `dbo.ai_warehouse_receipts_recent`

### Warehouse native execution
- `WarehouseAgent` now resolves capabilities from `ICapabilityRegistry` and executes directly via `IToolAdapterRegistry` → `SqlToolAdapter`
- Falls back to `LegacyChatPipelineBridge` only when no native capability matches
- `DomainAgentBase.ExecuteAsync` is now `virtual` — domain agents can override for native execution

### Auth hardening
- `[AllowAnonymous]` removed from `ChatController` (class-level and both action methods)
- When `Auth:Enabled=true` (production): endpoints require JWT authentication
- When `Auth:Enabled=false` (dev): `NoAuthHandler` auto-succeeds — no workflow change

### Approval engine
- Full end-to-end test suite: create → approve → execute → reject → re-execute failure
- `IWriteActionGuard` (`ApprovalBackedWriteActionGuard`) enforces approval at adapter level

### SupervisorRuntime
- Now accepts `IToolAdapterRegistry` and passes it into `AgentExecutionContext`
- Domain agents with native paths can access adapters directly through context

## Transitional compatibility

These paths still exist on purpose:
- `IOrchestrationEngine` and `OrchestrationEngine` keep current controllers and hubs stable while routing into `ISupervisorRuntime`
- `LegacyChatPipelineBridge` remains for agents without native capability paths (AccountingAgent, LegacyChatDomainAgent)
- `ActionApprovalService` remains as a Sprint 1 compatibility facade (delegates to `IApprovalEngine`)
- Module loading, module scope resolution, and named tool-handler registration remain until capability-pack migration is ready

## What's next (Sprint 5 priorities)

- AccountingAgent native capability path
- Data-driven capability registry (replace `InMemoryCapabilityRegistry` with SQL/config-backed)
- Remove `IOrchestrationEngine` compatibility facade
- Begin capability-pack migration (replace module scope resolution)
- Non-SQL adapter implementations

## Documentation

- `docs/architecture_v3.md`
- `docs/migration_v2_to_v3.md`
- `docs/cleanup_report.md`

## License

Internal / proprietary.
