# Compatibility Debt Report - Sprint 8

This document tracks transitional components that still exist after Sprint 8, plus the components removed or reduced during the sprint.

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

### 1. LegacyChatPipelineBridge

| Field | Value |
|-------|-------|
| Status | Explicit fallback-only, measured |
| Location | `src/TILSOFTAI.Orchestration/Agents/LegacyChatPipelineBridge.cs` |
| Why it exists | `GeneralChatAgent` uses it only for explicit `legacy-chat` / `legacyFallback=true` requests. |
| What depends on it | Explicit legacy fallback in `GeneralChatAgent` |
| Removal condition | Native capabilities or supervisor-native general workflows cover all remaining production request classes. |

### 2. ChatPipeline

| Field | Value |
|-------|-------|
| Status | Legacy fallback pipeline |
| Location | `src/TILSOFTAI.Orchestration/Pipeline/ChatPipeline.cs` |
| Why it exists | The bridge still delegates legacy multi-step chat/tool behavior to it. |
| What depends on it | `LegacyChatPipelineBridge` |
| Removal condition | Native agents own full execution, or remaining LLM/tool behavior is refactored behind supervisor-native services. |

### 3. Module Loader And Module Scope Resolver

| Field | Value |
|-------|-------|
| Status | Legacy diagnostic and ChatPipeline compatibility; autoload disabled by default |
| Location | `src/TILSOFTAI.Infrastructure/Modules/*`, `src/TILSOFTAI.Orchestration/Modules/*` |
| Why it exists | `ChatPipeline`, legacy tool catalog behavior, and diagnostic module health still use module loading/scope resolution when legacy autoload is enabled. |
| What depends on it | `ChatPipeline`, `ModuleHealthCheck`, `ModuleLoaderHostedService`, module packages |
| Removal condition | Capability-pack loading replaces module-first tool discovery for the legacy path. |

### 4. InMemoryCapabilityRegistry

| Field | Value |
|-------|-------|
| Status | Test fixture |
| Location | `src/TILSOFTAI.Orchestration/Capabilities/InMemoryCapabilityRegistry.cs` |
| Why it exists | Unit and integration tests use constructor-driven capability sets. Production uses `CompositeCapabilityRegistry`. |
| Removal condition | None required; may become internal test support in a later cleanup. |

## Sprint 9 Debt Priorities

1. Replace `ChatPipeline` legacy tool catalog behavior with capability-pack loading.
2. Delete `LegacyChatPipelineBridge` after explicit legacy fallback is no longer needed.
3. Move capability definitions from app configuration to a durable platform catalog.
4. Add schema-level typed value validation beyond required/allowed argument names.
