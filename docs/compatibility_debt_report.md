# Compatibility Debt Report - Sprint 6

This document tracks transitional components that still exist after Sprint 6, plus the components removed during the sprint.

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
| Status | Fallback-only, measured in Sprint 6 |
| Location | `src/TILSOFTAI.Orchestration/Agents/LegacyChatPipelineBridge.cs` |
| Why it exists | Domain agents still need a fallback when no native capability resolves. `LegacyChatDomainAgent` depends on it entirely. |
| What depends on it | `WarehouseAgent` fallback, `AccountingAgent` fallback, `LegacyChatDomainAgent` |
| Removal condition | Replace fallback coverage with native capabilities or a purpose-built general/chat agent. |

### 2. LegacyChatDomainAgent

| Field | Value |
|-------|-------|
| Status | Transitional catch-all, measured through bridge fallback instrumentation |
| Location | `src/TILSOFTAI.Orchestration/Agents/LegacyChatDomainAgent.cs` |
| Why it exists | Requests with no known domain hint still need a bounded fallback response path. |
| What depends on it | `DomainAgentRegistry` fallback behavior |
| Removal condition | Intent classification plus native/general agent coverage handles all production request classes. |

### 3. ChatPipeline

| Field | Value |
|-------|-------|
| Status | Legacy fallback pipeline |
| Location | `src/TILSOFTAI.Orchestration/Pipeline/ChatPipeline.cs` |
| Why it exists | The bridge still delegates legacy multi-step chat/tool behavior to it. |
| What depends on it | `LegacyChatPipelineBridge` |
| Removal condition | Native agents own full execution, or remaining LLM/tool behavior is refactored behind supervisor-native services. |

### 4. Module Loader And Module Scope Resolver

| Field | Value |
|-------|-------|
| Status | Bridge/legacy pipeline only |
| Location | `src/TILSOFTAI.Infrastructure/Modules/*`, `src/TILSOFTAI.Orchestration/Modules/*` |
| Why it exists | `ChatPipeline`, module health checks, and legacy tool catalog behavior still use module loading/scope resolution. |
| What depends on it | `ChatPipeline`, `ModuleHealthCheck`, `ModuleLoaderHostedService`, module packages |
| Removal condition | Capability-pack loading replaces module-first tool discovery for the legacy path. |

### 5. InMemoryCapabilityRegistry

| Field | Value |
|-------|-------|
| Status | Test fixture |
| Location | `src/TILSOFTAI.Orchestration/Capabilities/InMemoryCapabilityRegistry.cs` |
| Why it exists | Unit and integration tests use constructor-driven capability sets. Production uses `CompositeCapabilityRegistry`. |
| Removal condition | None required; may become internal test support in a later cleanup. |

## Sprint 7 Debt Priorities

1. Replace `LegacyChatDomainAgent` with a supervisor-native general/chat agent.
2. Move legacy tool catalog behavior away from module scope resolution.
3. Convert module loader health into a legacy-only diagnostic or remove it after capability-pack loading lands.
4. Expand REST/JSON capability configuration beyond the static proof binding.
5. Resolve analytics test regressions and IdentityModel version conflicts in the integration test project.
