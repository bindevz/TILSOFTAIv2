# Compatibility Debt Report - Sprint 7

This document tracks transitional components that still exist after Sprint 7, plus the components removed during the sprint.

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
| Why it exists | Domain agents can still use it when no native capability resolves, and `GeneralChatAgent` uses it only for explicit `legacy-chat` / `legacyFallback=true` requests. |
| What depends on it | `WarehouseAgent` fallback, `AccountingAgent` fallback, explicit legacy fallback in `GeneralChatAgent` |
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
| Status | Legacy diagnostic and ChatPipeline compatibility |
| Location | `src/TILSOFTAI.Infrastructure/Modules/*`, `src/TILSOFTAI.Orchestration/Modules/*` |
| Why it exists | `ChatPipeline`, legacy tool catalog behavior, and diagnostic module health still use module loading/scope resolution. |
| What depends on it | `ChatPipeline`, `ModuleHealthCheck`, `ModuleLoaderHostedService`, module packages |
| Removal condition | Capability-pack loading replaces module-first tool discovery for the legacy path. |

### 4. InMemoryCapabilityRegistry

| Field | Value |
|-------|-------|
| Status | Test fixture |
| Location | `src/TILSOFTAI.Orchestration/Capabilities/InMemoryCapabilityRegistry.cs` |
| Why it exists | Unit and integration tests use constructor-driven capability sets. Production uses `CompositeCapabilityRegistry`. |
| Removal condition | None required; may become internal test support in a later cleanup. |

## Sprint 8 Debt Priorities

1. Move legacy tool catalog behavior away from module scope resolution.
2. Remove or replace `LegacyChatPipelineBridge` fallback coverage with native capabilities.
3. Move capability definitions from static fallback to durable data/config sources.
4. Add production secret sourcing for REST auth metadata.
