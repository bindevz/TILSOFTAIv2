# V2 to V3 Migration Notes

## Sprint 1 completed

This sprint focuses on boundary creation, not full behavior migration.

Completed:
- introduced supervisor contracts and runtime
- introduced domain agent contracts and registry
- introduced tool adapter contracts and SQL adapter
- introduced approval engine contracts and implementation
- re-routed current chat execution through supervisor and legacy agent compatibility
- re-routed current approval entrypoints through approval engine

## Mapping from current code to V3

Current orchestration:
- `src/TILSOFTAI.Orchestration/IOrchestrationEngine.cs`
- `src/TILSOFTAI.Orchestration/OrchestrationEngine.cs`

V3 mapping:
- `src/TILSOFTAI.Orchestration/Supervisor/ISupervisorRuntime.cs`
- `src/TILSOFTAI.Orchestration/Supervisor/SupervisorRuntime.cs`

Current SQL execution:
- `src/TILSOFTAI.Infrastructure/Sql/SqlExecutor.cs`
- `src/TILSOFTAI.Infrastructure/Tools/SqlToolHandler.cs`

V3 mapping:
- `src/TILSOFTAI.Orchestration/Tools/Adapters/IToolAdapter.cs`
- `src/TILSOFTAI.Infrastructure/Sql/SqlToolAdapter.cs`

Current approval flow:
- `src/TILSOFTAI.Orchestration/Actions/ActionApprovalService.cs`
- `src/TILSOFTAI.Infrastructure/Actions/SqlActionRequestStore.cs`

V3 mapping:
- `src/TILSOFTAI.Orchestration/Approvals/IApprovalEngine.cs`
- `src/TILSOFTAI.Orchestration/Approvals/ApprovalEngine.cs`

Retired module-first runtime:
- Module loader, module autoload, module activation provider, module health check, and module scope resolver were deleted by Sprint 20.
- SQL `ModuleKey` and `@ModuleKeysJson` names remain only as compatibility names for capability-scope filtering.

Planned V3 replacement:
- capability-pack loading
- supervisor-to-agent routing
- capability resolution by adapter/system target

## What Sprint 2 must do

Required next steps:
- add first real domain agents with owned capability sets instead of the single `LegacyChatDomainAgent`
- introduce capability-pack loading to replace reflection-heavy module bootstrap
- move module scope logic out of `ChatPipeline`
- define non-SQL adapter contracts concretely enough for REST, file import, queue, and webhook adapters
- split approval policy evaluation from SQL catalog lookup so the engine stops assuming SQL as the only approval metadata source
