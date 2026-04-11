# Architecture V3 - Sprint 6

## Runtime Shape

```text
API / Hub / OpenAI surface
  -> ISupervisorRuntime
     -> IIntentClassifier
     -> CapabilityRequestHint
     -> IAgentRegistry
        -> WarehouseAgent          (native capability path: sql, rest-json)
        -> AccountingAgent         (native capability path: sql)
        -> LegacyChatDomainAgent   (fallback only)
           -> LegacyChatPipelineBridge
              -> ChatPipeline -> legacy module/tool pipeline

Native capability path:
  DomainAgent
    -> ICapabilityRegistry.GetByDomain(domain)
    -> ICapabilityResolver.Resolve(hint, candidates)
    -> IToolAdapterRegistry.Resolve(adapterType)
    -> IToolAdapter.ExecuteAsync(request)

Bridge fallback path:
  LegacyChatDomainAgent or unmatched domain capability
    -> LegacyChatPipelineBridge
    -> ChatPipeline
    -> module-era tool catalog/scope resolution

Write path:
  IApprovalEngine
    -> IActionRequestStore
    -> IWriteActionGuard
    -> SqlToolAdapter
```

## Sprint 6 Ownership Changes

| Area | Sprint 6 state |
|------|----------------|
| Edge orchestration | Controllers and hubs inject `ISupervisorRuntime` directly. `IOrchestrationEngine` and `OrchestrationEngine` are deleted. |
| Approval governance | API and tool callers use `IApprovalEngine`. `ActionApprovalService` is deleted. |
| Native execution | Warehouse and accounting agents resolve capabilities and adapters without module scope resolution. |
| Bridge fallback | Still present, measured, and bounded to fallback/catch-all behavior. |
| Module system | Still used by `ChatPipeline`, module health, and legacy tool catalog behavior; no longer part of native capability ownership. |
| Observability | Runtime path, selected agent, capability, adapter, duration, and success/failure are emitted via structured logs and metrics. |

## Capabilities

| Key | Domain | Adapter | Binding |
|-----|--------|---------|---------|
| `warehouse.inventory.summary` | warehouse | sql | `dbo.ai_warehouse_inventory_summary` |
| `warehouse.inventory.by-item` | warehouse | sql | `dbo.ai_warehouse_inventory_by_item` |
| `warehouse.receipts.recent` | warehouse | sql | `dbo.ai_warehouse_receipts_recent` |
| `warehouse.external-stock.lookup` | warehouse | rest-json | `GET /warehouse/external-stock` |
| `accounting.receivables.summary` | accounting | sql | `dbo.ai_accounting_receivables_summary` |
| `accounting.payables.summary` | accounting | sql | `dbo.ai_accounting_payables_summary` |
| `accounting.invoice.by-number` | accounting | sql | `dbo.ai_accounting_invoice_by_number` |

## Observability Signals

Runtime instrumentation emits:
- `tilsoftai_runtime_supervisor_executions_total`
- `tilsoftai_runtime_native_executions_total`
- `tilsoftai_runtime_bridge_fallback_total`
- `tilsoftai_runtime_approval_executions_total`
- `tilsoftai_runtime_capability_invocations_total`
- `tilsoftai_runtime_adapter_failures_total`
- `tilsoftai_runtime_execution_duration_seconds`

See `operational_runtime_observability.md` for interpretation guidance.

## Transition State

The native path is supervisor-driven and adapter-backed. The bridge and module system remain only for legacy chat/tool fallback. Sprint 7 should focus on reducing `LegacyChatDomainAgent`, shrinking `ChatPipeline` dependency, and replacing module-era tool catalog behavior with capability-pack loading.
