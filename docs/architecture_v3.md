# Architecture V3 — Sprint 5

## Runtime Shape

```text
API / Hub / OpenAI Surface
  -> IOrchestrationEngine (compatibility facade — [Obsolete], Sprint 6 removal)
     -> ISupervisorRuntime
        -> IIntentClassifier (keyword-based)
        -> CapabilityRequestHint (Sprint 5 — structured hints for capability resolution)
        -> IAgentRegistry
           -> WarehouseAgent          (native capability path — Sprint 4+5)
           -> AccountingAgent         (native capability path — Sprint 5)
           -> LegacyChatDomainAgent   (catch-all fallback — transitional)
              -> LegacyChatPipelineBridge (transitional — Sprint 7 removal)
                 -> ChatPipeline -> IToolHandler -> SqlToolAdapter

Native capability path (Sprint 5):
  DomainAgent
    -> ICapabilityRegistry.GetByDomain(domain)        // candidates
    -> ICapabilityResolver.Resolve(hint, candidates)   // structured resolution
    -> IToolAdapterRegistry.Resolve(adapterType)       // adapter lookup
    -> IToolAdapter.ExecuteAsync(request)               // execution

Capability registry loading (Sprint 5):
  CompositeCapabilityRegistry
    -> StaticCapabilitySource("warehouse", WarehouseCapabilities.All)
    -> StaticCapabilitySource("accounting", AccountingCapabilities.All)
    -> ConfigurationCapabilitySource (loads from appsettings.json Capabilities section)

Write requests:
  -> IApprovalEngine (create -> approve -> execute lifecycle)
     -> IActionRequestStore
     -> IWriteActionGuard (adapter-level enforcement)
     -> SqlToolAdapter
```

## Key Components

### Capability Resolution (Sprint 5)

| Component | Purpose |
|-----------|---------|
| `CapabilityRequestHint` | Structured hint from supervisor (explicit key, domain, keywords) |
| `ICapabilityResolver` | Contract for structured capability resolution |
| `StructuredCapabilityResolver` | Default resolver: exact key > keyword matching |

### Capability Registry (Sprint 5)

| Component | Purpose |
|-----------|---------|
| `ICapabilitySource` | Pluggable capability source interface |
| `StaticCapabilitySource` | Wraps static capability definitions |
| `ConfigurationCapabilitySource` | Loads from `appsettings.json` |
| `CompositeCapabilityRegistry` | Production registry — loads from all sources |
| `InMemoryCapabilityRegistry` | Test/fallback only |

### Domain Agents (Sprint 5)

| Agent | Status | Capabilities |
|-------|--------|-------------|
| `WarehouseAgent` | Native (Sprint 4+5) | 3 read-only warehouse capabilities |
| `AccountingAgent` | Native (Sprint 5) | 3 read-only accounting capabilities |
| `LegacyChatDomainAgent` | Bridge (transitional) | Catch-all for unclassified requests |

### Registered Capabilities

| Key | Domain | Stored Procedure |
|-----|--------|-----------------|
| `warehouse.inventory.summary` | warehouse | `dbo.ai_warehouse_inventory_summary` |
| `warehouse.inventory.by-item` | warehouse | `dbo.ai_warehouse_inventory_by_item` |
| `warehouse.receipts.recent` | warehouse | `dbo.ai_warehouse_receipts_recent` |
| `accounting.receivables.summary` | accounting | `dbo.ai_accounting_receivables_summary` |
| `accounting.payables.summary` | accounting | `dbo.ai_accounting_payables_summary` |
| `accounting.invoice.by-number` | accounting | `dbo.ai_accounting_invoice_by_number` |

## Transition State

See [compatibility_debt_report.md](compatibility_debt_report.md) for full tracking of all transitional components.
