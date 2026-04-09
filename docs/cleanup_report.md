# Sprint 1 Cleanup Report

## Deprecated runtime paths

Compatibility or deprecated paths kept after Sprint 1:
- `src/TILSOFTAI.Orchestration/IOrchestrationEngine.cs`
- `src/TILSOFTAI.Orchestration/OrchestrationEngine.cs`
- `src/TILSOFTAI.Orchestration/Actions/ActionApprovalService.cs`
- `src/TILSOFTAI.Orchestration/Modules/IModuleScopeResolver.cs`
- `src/TILSOFTAI.Orchestration/Modules/ModuleScopeResolver.cs`
- `src/TILSOFTAI.Infrastructure/Modules/IModuleLoader.cs`
- `src/TILSOFTAI.Infrastructure/Modules/ModuleLoader.cs`
- `src/TILSOFTAI.Infrastructure/Modules/ModuleLoaderHostedService.cs`

## Exact deletion candidates once Sprint 2 replacements exist

Delete after agent/capability migration is active:
- `src/TILSOFTAI.Orchestration/IOrchestrationEngine.cs`
- `src/TILSOFTAI.Orchestration/OrchestrationEngine.cs`

Delete after approval consumers stop using compatibility surface:
- `src/TILSOFTAI.Orchestration/Actions/ActionApprovalService.cs`

Delete after capability-pack loader replaces module-first bootstrap:
- `src/TILSOFTAI.Infrastructure/Modules/IModuleLoader.cs`
- `src/TILSOFTAI.Infrastructure/Modules/ModuleLoader.cs`
- `src/TILSOFTAI.Infrastructure/Modules/ModuleLoaderHostedService.cs`
- `src/TILSOFTAI.Orchestration/Modules/IModuleScopeResolver.cs`
- `src/TILSOFTAI.Orchestration/Modules/ModuleScopeResolver.cs`

Delete or rewrite after module packages stop owning runtime behavior:
- `src/TILSOFTAI.Modules.Core/*Module*.cs`
- `src/TILSOFTAI.Modules.Model/ModelModule.cs`
- `src/TILSOFTAI.Modules.Platform/PlatformModule.cs`
- `src/TILSOFTAI.Modules.Analytics/AnalyticsModule.cs`

## Why these are not deleted yet

- API controllers and hubs still use `IOrchestrationEngine`.
- current chat behavior still depends on `ChatPipeline`, module scope resolution, and module-driven tool catalogs.
- startup still depends on reflection-based module loading.
- write approval persistence still depends on the existing SQL-backed action request store.

## Sprint 2 prerequisites

- first real domain-agent implementations with owned capabilities
- capability-pack loader and registration model
- migration plan for module catalog and module scope SQL tables
- adapter selection rules beyond SQL
- approval policy source that is not hard-wired to SQL-only metadata
