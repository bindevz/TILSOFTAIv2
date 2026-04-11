# Sprint Cleanup Report

## Deleted In Sprint 6

- `src/TILSOFTAI.Orchestration/IOrchestrationEngine.cs`
- `src/TILSOFTAI.Orchestration/OrchestrationEngine.cs`
- `src/TILSOFTAI.Orchestration/Actions/ActionApprovalService.cs`
- `src/TILSOFTAI.Orchestration/Capabilities/ICapabilityPackProvider.cs`
- `src/TILSOFTAI.Orchestration/Capabilities/ModuleBackedCapabilityPack.cs`
- `tests/TILSOFTAI.Tests/Approvals/ActionApprovalServiceFacadeTests.cs`

## Remaining Deprecated Runtime Paths

- `src/TILSOFTAI.Orchestration/Agents/LegacyChatPipelineBridge.cs`
- `src/TILSOFTAI.Orchestration/Agents/LegacyChatDomainAgent.cs`
- `src/TILSOFTAI.Orchestration/Pipeline/ChatPipeline.cs`
- `src/TILSOFTAI.Orchestration/Modules/IModuleScopeResolver.cs`
- `src/TILSOFTAI.Orchestration/Modules/ModuleScopeResolver.cs`
- `src/TILSOFTAI.Infrastructure/Modules/IModuleLoader.cs`
- `src/TILSOFTAI.Infrastructure/Modules/ModuleLoader.cs`
- `src/TILSOFTAI.Infrastructure/Modules/ModuleLoaderHostedService.cs`

## Why These Are Not Deleted Yet

- Bridge fallback still handles requests that no native capability resolves.
- `LegacyChatDomainAgent` is still the catch-all for unclassified requests.
- `ChatPipeline` still owns legacy LLM/tool behavior behind the fallback path.
- Module loader and module scope resolver still support the legacy pipeline and module health diagnostics.
- SQL-backed action request persistence remains the production approval persistence boundary.

## Sprint 6 Cleanup Outcome

Native runtime execution no longer depends on module scope resolution or the old orchestration facade. Module-era infrastructure is now explicitly bridge/legacy-only. Runtime telemetry makes native usage, bridge fallback, approval execution, capability invocation, adapter failures, and duration visible.

## Sprint 7 Cleanup Prerequisites

- Native general/chat agent or equivalent fallback replacement.
- Capability-pack loader for legacy tool catalog replacement.
- Expanded non-SQL capability configuration and production endpoint policy.
- Decision on whether module health remains a legacy diagnostic or is removed with module loading.
