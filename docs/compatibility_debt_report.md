# Compatibility Debt Report - Sprint 11

This document tracks transitional components that still exist after Sprint 9, plus the components removed or reduced during the sprint.

## Removed In Sprint 9

| Component | Result | Notes |
|-----------|--------|-------|
| `LegacyChatPipelineBridge` | Deleted | Explicit legacy fallback no longer executes `ChatPipeline`; `GeneralChatAgent` returns `LEGACY_RUNTIME_RETIRED`. |
| `ChatPipeline` | Deleted | The legacy multi-step chat/tool owner is no longer registered or compiled. |
| `ChatRequest` / `ChatResult` | Deleted | These existed only for the retired pipeline path. |
| `ChatPipelineInstrumentation` | Deleted | Legacy pipeline span source removed from default runtime registration. |

## Changed In Sprint 9

| Component | Result | Notes |
|-----------|--------|-------|
| `GeneralChatAgent` | Supervisor-native only | Handles simple general requests and deterministic unsupported/retired legacy responses. |
| Capability catalog | Platform-owned | `catalog/platform-catalog.json` overrides static/bootstrap records. |
| External connection catalog | Platform-owned | Durable platform connection records override bootstrap configuration. |
| Capability contracts | Typed | Representative contracts now validate type, format, enum, and length/range rules. |
| Deep analytics E2E | External boundary | Marked `Category=ExternalDeepWorkflow`, owned by Analytics, and gated by `TEST_SQL_CONNECTION`. |

## Changed In Sprint 10

| Component | Result | Notes |
|-----------|--------|-------|
| Platform catalog mutation | Governed control plane | Catalog changes are submitted, independently reviewed, and applied through SQL-backed change requests. |
| Bootstrap fallback | Visible operational state | Startup logs, metrics, and `/health/ready` now report `platform`, `mixed`, `bootstrap_only`, or `empty`. |
| Catalog integrity | Enforced on load and mutation | Duplicate keys, unresolved REST connections, raw secrets, and missing contracts are reported as validation errors. |
| No-argument capabilities | Explicit contracts | Summary/list capabilities now reject unexpected arguments through no-argument contracts. |
| Module packages | Classified | Remaining module packages are marked `packaging-only` or `diagnostic-only` in `Modules:Classifications`. |

## Changed In Sprint 11

| Component | Result | Notes |
|-----------|--------|-------|
| Catalog mutation safety | Hardened | Expected-version checks, duplicate pending-change detection, preview, idempotent apply, and rollback reference metadata were added. |
| Production fallback | Stricter | Production config disables bootstrap fallback and marks mixed/bootstrap-only source modes unhealthy. |
| Approval policy | Environment/risk aware | Apply roles, high-risk approver roles, break-glass roles, and production-like independent apply are configured separately. |
| Contract lifecycle | Versioned baseline | `ArgumentContract` includes `ContractVersion`, `SchemaDialect`, and `SchemaRef` for future schema governance. |
| Module package end-state | Decided | Module packages are retained only as non-runtime packaging or diagnostic artifacts. |

## Removed In Sprint 7

| Component | Result | Notes |
|-----------|--------|-------|
| `LegacyChatDomainAgent` | Deleted | Replaced by supervisor-native `GeneralChatAgent`. Unknown requests no longer proxy silently to `ChatPipeline`. |

## Changed In Sprint 7

| Component | Result | Notes |
|-----------|--------|-------|
| `DomainAgentRegistry` | General fallback only | Unmatched agent resolution now returns `general-chat` instead of `legacy-chat`. |
| `ModuleHealthCheck` | Legacy diagnostic only | Removed from `/health/ready`; still available on detailed/default health endpoints for diagnostics. |
| `RestJsonToolAdapter` | Production adapter path | Binding validation, retry, timeout, auth metadata, and classified failures are implemented. |
| Capability descriptors | Policy-aware | `RequiredRoles` and `AllowedTenants` gate native execution before adapter resolution. |

## Changed In Sprint 8

| Component | Result | Notes |
|-----------|--------|-------|
| `LegacyChatPipelineBridge` | Explicit legacy only for normal production routing | Unmatched warehouse/accounting requests now return `DOMAIN_CAPABILITY_NOT_FOUND` instead of invoking the bridge. |
| `ModuleLoaderHostedService` | Disabled by default | Starts only when `Modules:EnableLegacyAutoload=true`. |
| `NativeRuntimeHealthCheck` | Domain-agnostic | Uses all loaded capabilities and registered adapters rather than hardcoded warehouse/accounting checks. |
| `RestJsonToolAdapter` | Secret-governed | Uses external connection catalog and `ISecretProvider`; rejects raw secret metadata. |
| Capability descriptors | Contract-aware | Representative capabilities validate arguments before adapter resolution. |

## Removed In Sprint 6

| Component | Result | Notes |
|-----------|--------|-------|
| `IOrchestrationEngine` | Deleted | Edge entrypoints now call `ISupervisorRuntime` directly. |
| `OrchestrationEngine` | Deleted | Mapping moved to API edge code; exception/error handling remains in API middleware. |
| `ActionApprovalService` | Deleted | Approval callers use `IApprovalEngine` directly. |
| `ICapabilityPackProvider` | Deleted | No runtime references remained. |
| `ModuleBackedCapabilityPack` | Deleted | Dead module-era capability wrapper. |

## Remaining Compatibility Components

### 1. Module Loader And Module Packages

| Field | Value |
|-------|-------|
| Status | Permanently non-runtime packaging/diagnostic residue; autoload disabled by default; package classifications are reported in health data |
| Location | `src/TILSOFTAI.Infrastructure/Modules/*`, `src/TILSOFTAI.Orchestration/Modules/*` |
| Why it exists | Diagnostic module health and existing module packages still use module loading when legacy autoload is explicitly enabled. |
| What depends on it | `ModuleHealthCheck`, `ModuleLoaderHostedService`, module packages |
| Removal condition | Optional future cleanup only; production capability ownership must remain in platform catalog/tool records. |

Current classifications:

| Package | Classification |
|---------|----------------|
| `TILSOFTAI.Modules.Platform` | packaging-only |
| `TILSOFTAI.Modules.Model` | packaging-only |
| `TILSOFTAI.Modules.Analytics` | diagnostic-only |

### 2. Bootstrap Configuration Sources

| Field | Value |
|-------|-------|
| Status | Bootstrap fallback |
| Location | `ConfigurationCapabilitySource`, `ConfigurationExternalConnectionCatalog` |
| Why it exists | Local bootstrap and emergency fallback when durable catalog records are unavailable. |
| What depends on it | `CompositeCapabilityRegistry`, `CompositeExternalConnectionCatalog` |
| Removal condition | SQL/admin platform catalog write path and operational bootstrap process are fully established. |

### 3. InMemoryCapabilityRegistry

| Field | Value |
|-------|-------|
| Status | Test fixture |
| Location | `src/TILSOFTAI.Orchestration/Capabilities/InMemoryCapabilityRegistry.cs` |
| Why it exists | Unit and integration tests use constructor-driven capability sets. Production uses `CompositeCapabilityRegistry`. |
| Removal condition | None required; may become internal test support in a later cleanup. |

## Sprint 11 Debt Priorities

Completed. Remaining priorities are live production exercising of SQL catalog workflows, operator training, and any future removal of non-runtime packages when packaging/diagnostic ownership no longer needs them.
