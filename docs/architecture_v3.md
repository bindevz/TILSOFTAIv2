# Architecture V3 - Sprint 8

## Runtime Shape

```text
API / Hub / OpenAI surface
  -> ISupervisorRuntime
     -> IIntentClassifier
     -> CapabilityRequestHint
     -> IAgentRegistry
        -> WarehouseAgent          (native capability path: sql, rest-json)
        -> AccountingAgent         (native capability path: sql)
        -> GeneralChatAgent        (native general response, explicit legacy fallback only)
           -> LegacyChatPipelineBridge
              -> ChatPipeline -> legacy module/tool pipeline

Native capability path:
  DomainAgent
    -> ICapabilityRegistry.GetByDomain(domain)
    -> ICapabilityResolver.Resolve(hint, candidates)
    -> CapabilityAccessPolicy.Evaluate(required roles, allowed tenants)
    -> CapabilityArgumentValidator.Validate(argument contract)
    -> IToolAdapterRegistry.Resolve(adapterType)
    -> IToolAdapter.ExecuteAsync(request)

Bridge fallback path:
  Explicit legacy request
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

## Sprint 7 Ownership Changes

| Area | Sprint 7 state |
|------|----------------|
| General chat | `GeneralChatAgent` owns unclassified/general requests. It returns native bounded guidance or a deterministic unsupported result. |
| Legacy fallback | `legacy-chat` and `legacyFallback=true` are explicit fallback requests; bridge usage is recorded with a reason. |
| Capability policy | `RequiredRoles` and `AllowedTenants` are evaluated before adapter resolution. Denials return `CAPABILITY_ACCESS_DENIED`. |
| REST capabilities | REST endpoint, method, timeout, retry, and auth metadata are configuration-driven through `IntegrationBinding`. |
| Readiness | `/health/ready` uses `NativeRuntimeHealthCheck`; module health is legacy diagnostic only. |

## Sprint 8 Ownership Changes

| Area | Sprint 8 state |
|------|----------------|
| Bridge fallback | Unmatched warehouse/accounting capability requests return `DOMAIN_CAPABILITY_NOT_FOUND` instead of reaching the bridge. |
| Capability sourcing | Static descriptors are fallback keys/contracts; configuration overrides production binding by capability key. |
| External connections | `ExternalConnections` catalog owns base URL, timeout, retry, and secret references by `connectionName`. |
| Secret governance | REST auth/API-key material is resolved through `ISecretProvider`; raw secret metadata is rejected. |
| Contracts | Representative SQL and REST capabilities enforce `ArgumentContract` before adapter resolution. |
| Readiness | `NativeRuntimeHealthCheck` uses all loaded capabilities and required adapter registrations, without domain hardcoding. |
| Modules | `ModuleLoaderHostedService` is disabled unless `Modules:EnableLegacyAutoload=true`; module health is diagnostic only. |

## Capabilities

| Key | Domain | Adapter | Binding |
|-----|--------|---------|---------|
| `warehouse.inventory.summary` | warehouse | sql | `dbo.ai_warehouse_inventory_summary` |
| `warehouse.inventory.by-item` | warehouse | sql | `dbo.ai_warehouse_inventory_by_item` |
| `warehouse.receipts.recent` | warehouse | sql | `dbo.ai_warehouse_receipts_recent` |
| `warehouse.external-stock.lookup` | warehouse | rest-json | Configured `connectionName`, `endpoint`, `method`, retry, timeout, secret-backed auth |
| `accounting.receivables.summary` | accounting | sql | `dbo.ai_accounting_receivables_summary` |
| `accounting.payables.summary` | accounting | sql | `dbo.ai_accounting_payables_summary` |
| `accounting.invoice.by-number` | accounting | sql | `dbo.ai_accounting_invoice_by_number` |
| `accounting.exchange-rate.lookup` | accounting | rest-json | Configured `connectionName`, `endpoint`, `method`, retry, timeout, secret-backed API key |

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

## Capability Policy

Capabilities may specify:
- `RequiredRoles`: at least one caller role must match.
- `AllowedTenants`: caller tenant must be in the list when the list is non-empty.

Policy denial happens before adapter resolution and is returned as `CAPABILITY_ACCESS_DENIED`.

## Capability Contracts

Capabilities may specify `ArgumentContract`:

- `RequiredArguments`: all must be present.
- `AllowedArguments`: permitted argument names when additional arguments are disabled.
- `AllowAdditionalArguments`: when false, unknown arguments fail validation.

Validation failure returns `CAPABILITY_ARGUMENT_VALIDATION_FAILED` before adapter resolution.

## Transition State

The native path is supervisor-driven, policy-gated, contract-validated, and adapter-backed. The bridge remains only for explicit legacy fallback. Module autoload is off by default and module health is diagnostic. Sprint 9 should focus on shrinking `ChatPipeline` dependency and replacing module-era tool catalog behavior with capability-pack loading.
