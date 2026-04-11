# Compatibility Debt Report - Sprint 9

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
| Status | Legacy diagnostic only; autoload disabled by default |
| Location | `src/TILSOFTAI.Infrastructure/Modules/*`, `src/TILSOFTAI.Orchestration/Modules/*` |
| Why it exists | Diagnostic module health and existing module packages still use module loading when legacy autoload is explicitly enabled. |
| What depends on it | `ModuleHealthCheck`, `ModuleLoaderHostedService`, module packages |
| Removal condition | Module packages are converted to platform catalog/tool records or explicitly retained as non-runtime packaging. |

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

## Sprint 9 Debt Priorities

Completed. See Sprint 10 priorities in the enterprise readiness report.
