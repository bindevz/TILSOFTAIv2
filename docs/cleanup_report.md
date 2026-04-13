# Sprint Cleanup Report

## Deleted In Sprint 6

- `src/TILSOFTAI.Orchestration/IOrchestrationEngine.cs`
- `src/TILSOFTAI.Orchestration/OrchestrationEngine.cs`
- `src/TILSOFTAI.Orchestration/Actions/ActionApprovalService.cs`
- `src/TILSOFTAI.Orchestration/Capabilities/ICapabilityPackProvider.cs`
- `src/TILSOFTAI.Orchestration/Capabilities/ModuleBackedCapabilityPack.cs`
- `tests/TILSOFTAI.Tests/Approvals/ActionApprovalServiceFacadeTests.cs`

## Deleted In Sprint 7

- `src/TILSOFTAI.Orchestration/Agents/LegacyChatDomainAgent.cs`

## Deleted In Sprint 9

- `src/TILSOFTAI.Orchestration/Agents/LegacyChatPipelineBridge.cs`
- `src/TILSOFTAI.Orchestration/Pipeline/ChatPipeline.cs`
- `src/TILSOFTAI.Orchestration/Pipeline/ChatRequest.cs`
- `src/TILSOFTAI.Orchestration/Pipeline/ChatResult.cs`
- `src/TILSOFTAI.Orchestration/Observability/ChatPipelineInstrumentation.cs`

## Deleted In Sprint 19

- obsolete Model module project file
- obsolete Model module registration source
- obsolete Model module tool handlers

## Remaining Deprecated Runtime Paths

- `src/TILSOFTAI.Orchestration/Modules/IModuleScopeResolver.cs`
- `src/TILSOFTAI.Orchestration/Modules/ModuleScopeResolver.cs`
- `src/TILSOFTAI.Infrastructure/Modules/IModuleLoader.cs`
- `src/TILSOFTAI.Infrastructure/Modules/ModuleLoader.cs`
- `src/TILSOFTAI.Infrastructure/Modules/ModuleLoaderHostedService.cs`

## Why These Are Not Deleted Yet

- Bridge fallback and `ChatPipeline` were deleted in Sprint 9.
- Module loader and module scope resolver remain for opt-in diagnostics and bounded Platform/Analytics package support.
- SQL-backed action request persistence remains the production approval persistence boundary.

## Sprint 19 Cleanup Outcome

The obsolete Model module is no longer a project or supported structural concept. Runtime ownership is now documented as Supervisor + Domain Agents + Tool Adapters, with provider/model execution concerns kept out of technical module ownership.

## Sprint 6 Cleanup Outcome

Native runtime execution no longer depends on module scope resolution or the old orchestration facade. Module-era infrastructure is now explicitly bridge/legacy-only. Runtime telemetry makes native usage, bridge fallback, approval execution, capability invocation, adapter failures, and duration visible.

## Sprint 7 Cleanup Prerequisites

- Native general/chat agent or equivalent fallback replacement.
- Capability-pack loader for legacy tool catalog replacement.
- Expanded non-SQL capability configuration and production endpoint policy.
- Decision on whether module health remains a legacy diagnostic or is removed with module loading.
