# Architecture V3 - Sprint 12

## Runtime Shape

```text
API / Hub / OpenAI surface
  -> ISupervisorRuntime
     -> IIntentClassifier
     -> CapabilityRequestHint
     -> IAgentRegistry
        -> WarehouseAgent          (native capability path: sql, rest-json)
        -> AccountingAgent         (native capability path: sql, rest-json)
        -> GeneralChatAgent        (native general response, unsupported/retired legacy responses)

Native capability path:
  DomainAgent
    -> ICapabilityRegistry.GetByDomain(domain)
    -> ICapabilityResolver.Resolve(hint, candidates)
    -> CapabilityAccessPolicy.Evaluate(required roles, allowed tenants)
    -> CapabilityArgumentValidator.Validate(typed argument contract)
    -> IToolAdapterRegistry.Resolve(adapterType)
    -> IToolAdapter.ExecuteAsync(request)

Catalog control plane:
  PlatformCatalogController
    -> IPlatformCatalogControlPlane
    -> IPlatformCatalogPromotionGate
    -> IPlatformCatalogCertificationStore
    -> IPlatformCatalogMutationStore
    -> PlatformCatalogChangeRequest
    -> PlatformCapabilityCatalog / PlatformExternalConnectionCatalog

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

## Sprint 9 Ownership Changes

| Area | Sprint 9 state |
|------|----------------|
| Legacy runtime | `LegacyChatPipelineBridge`, `ChatPipeline`, `ChatRequest`, and `ChatResult` are deleted. Explicit legacy requests return `LEGACY_RUNTIME_RETIRED`. |
| Capability sourcing | Static fallback and bootstrap config records are overridden by durable platform catalog records. |
| External connections | Platform catalog records are primary; bootstrap `ExternalConnections` only apply when fallback is enabled. |
| Contract validation | Representative capabilities validate names, types, formats, enum values, and length/range constraints. |
| Module-era registration | Module scope/ReAct legacy pipeline services are no longer registered by default. Module loader remains diagnostic and opt-in. |
| Deep analytics | Deep SQL-backed analytics E2E is isolated as an external workflow boundary owned by Analytics. |

## Sprint 10 Ownership Changes

| Area | Sprint 10 state |
|------|-----------------|
| Catalog mutation | Platform catalog writes go through submit, independent review, and apply control-plane operations. |
| Catalog integrity | Platform catalog startup validates duplicate keys, REST connection references, secret references, and required argument contracts. |
| Catalog observability | Startup source mode and mutation operations emit metrics, health data, structured logs, and governance audit events. |
| Bootstrap fallback | Readiness distinguishes `platform`, `mixed`, `bootstrap_only`, and `empty` catalog source modes. |
| Module packages | Remaining module packages are classified as packaging-only or diagnostic-only in configuration and health output. |
| Contract coverage | Production catalog records and static no-argument capabilities now declare explicit `ArgumentContract` records. |

## Sprint 11 Ownership Changes

| Area | Sprint 11 state |
|------|-----------------|
| Mutation safety | Catalog changes support preview, expected-version concurrency checks, idempotency keys, duplicate pending-change detection, and idempotent apply replay. |
| Production governance | Production-like environments can require expected versions, independent apply, senior approval for high-risk changes, and strict fallback posture. |
| Recovery | Rollback is represented as a governed compensating change with `RollbackOfChangeId`, not as an unaudited metadata rewind. |
| Source of truth | Production config disables bootstrap fallback by default and marks mixed/bootstrap-only source modes unhealthy. |
| Contract lifecycle | `ArgumentContract` has `ContractVersion`, optional `SchemaDialect`, and optional `SchemaRef` for future JSON Schema interop. |
| Module packages | Module packages are formally retained only as packaging or diagnostic artifacts, not runtime ownership. |

## Sprint 12 Ownership Changes

| Area | Sprint 12 state |
|------|-----------------|
| Promotion control | `IPlatformCatalogPromotionGate` evaluates source mode, preview validity, approved-change state, expected-version coverage, break-glass containment, and certification evidence. |
| Certification evidence | `IPlatformCatalogCertificationStore` persists staging/prod-like evidence records with operator, approver, correlation, related change, related incident, evidence URI, kind, and status. |
| Release SLOs | Catalog control-plane SLO and escalation definitions are exposed through `GET /api/platform-catalog/slo-definitions`. |
| Emergency path | Production fallback and break-glass are blocked by gate policy until real evidence and after-action review exist. |
| Live certification | The code now stores and enforces evidence readiness; actual live drill execution remains an operational prerequisite before production promotion. |

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
- `tilsoftai_platform_catalog_source_mode_total`
- `tilsoftai_platform_catalog_mutations_total`
- `tilsoftai_platform_catalog_promotion_gate_total`
- `tilsoftai_platform_catalog_certification_evidence_total`

See `operational_runtime_observability.md` for interpretation guidance.

## Capability Policy

Capabilities may specify:
- `RequiredRoles`: at least one caller role must match.
- `AllowedTenants`: caller tenant must be in the list when the list is non-empty.

Policy denial happens before adapter resolution and is returned as `CAPABILITY_ACCESS_DENIED`.

## Capability Contracts

Capabilities may specify `ArgumentContract`:

- `ContractVersion`: lifecycle version for the contract shape.
- `SchemaDialect` and `SchemaRef`: optional schema interop metadata.
- `RequiredArguments`: all must be present.
- `AllowedArguments`: permitted argument names when additional arguments are disabled.
- `AllowAdditionalArguments`: when false, unknown arguments fail validation.
- `Arguments`: typed value rules for type, format, enum, pattern, length, and numeric range.

Validation failure returns `CAPABILITY_ARGUMENT_VALIDATION_FAILED` before adapter resolution.

## Transition State

The native path is supervisor-driven, policy-gated, contract-validated, and adapter-backed. The bridge and ChatPipeline are retired. Remaining compatibility debt is limited to opt-in module diagnostics, bootstrap catalog fallback, and continued hardening of admin catalog operations.
