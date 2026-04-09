# TILSOFTAI V3 Architecture Baseline

## Sprint 1 boundary decisions

Sprint 1 introduces the V3 architecture boundaries inside the existing project layout instead of waiting for a full project split.

New runtime contracts:
- `TILSOFTAI.Supervisor.ISupervisorRuntime`
- `TILSOFTAI.Agents.Abstractions.IDomainAgent`
- `TILSOFTAI.Agents.Abstractions.IAgentRegistry`
- `TILSOFTAI.Tools.Abstractions.IToolAdapter`
- `TILSOFTAI.Tools.Abstractions.IToolAdapterRegistry`
- `TILSOFTAI.Approvals.IApprovalEngine`

New compatibility implementations:
- `TILSOFTAI.Supervisor.SupervisorRuntime`
- `TILSOFTAI.Agents.LegacyChatDomainAgent`
- `TILSOFTAI.Agents.DomainAgentRegistry`
- `TILSOFTAI.Tools.Abstractions.ToolAdapterRegistry`
- `TILSOFTAI.Infrastructure.Sql.SqlToolAdapter`
- `TILSOFTAI.Approvals.ApprovalEngine`

## Runtime layering after Sprint 1

```text
Compatibility entrypoints
  IOrchestrationEngine
  ActionApprovalService

V3 boundaries
  ISupervisorRuntime
  IDomainAgent / IAgentRegistry
  IToolAdapter / IToolAdapterRegistry
  IApprovalEngine

Current concrete bridge
  LegacyChatDomainAgent -> ChatPipeline
  SqlToolAdapter -> ISqlExecutor
  ApprovalEngine -> IActionRequestStore + SqlToolAdapter
```

## What changed materially

Supervisor:
- `OrchestrationEngine` is no longer the orchestration owner.
- `OrchestrationEngine` now maps chat requests into `SupervisorRequest` and delegates to `ISupervisorRuntime`.

Agents:
- `LegacyChatDomainAgent` wraps the existing `ChatPipeline`.
- This keeps today’s behavior alive while making agent routing a first-class concept.

Tool adapters:
- `SqlToolHandler` no longer reaches straight into `ISqlExecutor`.
- `SqlToolHandler` now goes through `IToolAdapterRegistry` and `SqlToolAdapter`.

Approval flow:
- `ActionsController` now depends on `IApprovalEngine`.
- `ActionRequestWriteToolHandler` now creates `ProposedAction` and calls `IApprovalEngine`.
- `ActionApprovalService` remains only as a compatibility facade for old callers.

## Still intentionally legacy

These pieces are still active, but no longer represent the target architecture:
- `IModuleLoader` / `ModuleLoader` / `ModuleLoaderHostedService`
- `IModuleScopeResolver` / `ModuleScopeResolver`
- `ITilsoftModule` module packages and named tool-handler registration
- `IOrchestrationEngine` compatibility entrypoint
- `ActionApprovalService` compatibility entrypoint

## Notes

- The AGENTS source-of-truth reference `spec\patch_18\18_00_overview.yaml` was not present in the repo at the requested path during implementation. Sprint 1 was anchored to the available v3 instruction pack plus the live codebase.
