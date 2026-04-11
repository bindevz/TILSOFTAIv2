# Compatibility Debt Report — Sprint 5

This document tracks all transitional components that exist for backward compatibility.
Each entry documents **why it exists**, **what depends on it**, and **what removes it**.

---

## 1. IOrchestrationEngine / OrchestrationEngine

| Field | Value |
|-------|-------|
| **Status** | `[Obsolete]` — marked in Sprint 5 |
| **Location** | `TILSOFTAI.Orchestration/IOrchestrationEngine.cs`, `OrchestrationEngine.cs` |
| **Why it still exists** | Controllers (`ChatController`, `OpenAiChatCompletionsController`) and hubs (`ChatHub`) reference `IOrchestrationEngine` instead of `ISupervisorRuntime` directly. The facade maps `ChatRequest` → `SupervisorRequest` and `SupervisorResult` → `ChatResult`, and also handles sensitivity classification and error logging. |
| **What depends on it** | `ChatController`, `OpenAiChatCompletionsController`, `ChatHub` |
| **What removes it** | Sprint 6+ — migrate all controllers/hubs to use `ISupervisorRuntime` directly. Move sensitivity classification and error-log persistence to middleware or a decorator. Then delete both files. |

---

## 2. LegacyChatPipelineBridge

| Field | Value |
|-------|-------|
| **Status** | Transitional — documented in Sprint 5 |
| **Location** | `TILSOFTAI.Orchestration/Agents/LegacyChatPipelineBridge.cs` |
| **Why it still exists** | Fallback execution path for domain agents when no native capability matches. `LegacyChatDomainAgent` (catch-all) relies on it entirely. Warehouse and Accounting agents fall back to it for unrecognized requests. |
| **What depends on it** | `DomainAgentBase.ExecuteAsync` (default implementation), `LegacyChatDomainAgent`, warehouse/accounting fallback paths |
| **What removes it** | Sprint 7+ — when all domain agents have full native capability coverage for their domains, and the catch-all `LegacyChatDomainAgent` is replaced with a proper "general" agent. |

---

## 3. LegacyChatDomainAgent

| Field | Value |
|-------|-------|
| **Status** | Transitional — documented in Sprint 5 |
| **Location** | `TILSOFTAI.Orchestration/Agents/LegacyChatDomainAgent.cs` |
| **Why it still exists** | Catch-all agent for requests where intent classification doesn't resolve to any known domain. Handles null/empty `DomainHint` or `"legacy-chat"`. |
| **What depends on it** | `DomainAgentRegistry` fallback logic — without this, requests with no domain classification would have no agent to handle them. |
| **What removes it** | Sprint 6+ — when intent classification reliably covers all production domains, or a purpose-built "general"/"chat" agent replaces this catch-all pattern. |

---

## 4. ChatPipeline

| Field | Value |
|-------|-------|
| **Status** | Active — core pipeline still in use |
| **Location** | `TILSOFTAI.Orchestration/Pipeline/ChatPipeline.cs` |
| **Why it still exists** | The full ChatPipeline with tool resolution, LLM interaction, and multi-step execution is still needed by the bridge fallback path. |
| **What depends on it** | `LegacyChatPipelineBridge`, and through it every agent's fallback path. |
| **What removes it** | Sprint 7+ — when native capability paths replace all pipeline-mediated execution. The pipeline's LLM interaction layer may be refactored into a separate `ILlmClient` for agent use. |

---

## 5. Module System (IModuleLoader, IModuleScopeResolver)

| Field | Value |
|-------|-------|
| **Status** | `[Obsolete]` — marked in Sprint 1 |
| **Location** | `TILSOFTAI.Domain/Modules/`, `TILSOFTAI.Infrastructure/Modules/` |
| **Why it still exists** | Module-based tool handler registration and scope resolution is still used by `ChatPipeline` to discover which tools are available. |
| **What depends on it** | `ChatPipeline`, `AddTilsoftAiExtensions`, `ModuleHealthCheck` |
| **What removes it** | Sprint 6+ — capability-pack migration replaces module-based tool discovery with `ICapabilityRegistry` + `IToolAdapterRegistry`. |

---

## 6. ActionApprovalService

| Field | Value |
|-------|-------|
| **Status** | `[Obsolete]` — marked in Sprint 3 |
| **Location** | `TILSOFTAI.Orchestration/Approvals/ActionApprovalService.cs` |
| **Why it still exists** | Sprint 1 compatibility facade that delegates to `IApprovalEngine`. May still be referenced by legacy API endpoints. |
| **What depends on it** | Legacy API endpoints (if any) |
| **What removes it** | Sprint 6 — audit all API references and replace with `IApprovalEngine`. |

---

## 7. InMemoryCapabilityRegistry

| Field | Value |
|-------|-------|
| **Status** | Active — demoted to test/fallback in Sprint 5 |
| **Location** | `TILSOFTAI.Orchestration/Capabilities/InMemoryCapabilityRegistry.cs` |
| **Why it still exists** | Used in unit tests as a simple, constructor-driven registry. Production now uses `CompositeCapabilityRegistry`. |
| **What depends on it** | Unit tests (`CapabilityRegistryTests`, `WarehouseAgentNativePathTests`, etc.) |
| **What removes it** | Not removed — remains as a testing fixture. May be marked `internal` in a future sprint. |

---

## Summary

| Component | Sprint Marked | Removal Target | Risk |
|-----------|--------------|----------------|------|
| IOrchestrationEngine | Sprint 5 | Sprint 6 | Controller migration scope |
| LegacyChatPipelineBridge | Sprint 5 docs | Sprint 7 | Full native coverage required |
| LegacyChatDomainAgent | Sprint 5 docs | Sprint 6 | Intent classification coverage |
| ChatPipeline | N/A | Sprint 7 | LLM interaction refactoring |
| Module System | Sprint 1 | Sprint 6 | Capability-pack migration |
| ActionApprovalService | Sprint 3 | Sprint 6 | API audit |
| InMemoryCapabilityRegistry | Sprint 5 demoted | Never (test fixture) | None |
