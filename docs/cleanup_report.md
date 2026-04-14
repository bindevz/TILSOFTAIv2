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

## Deleted In Sprint 20

- `src/TILSOFTAI.Api/Health/ModuleHealthCheck.cs`
- `src/TILSOFTAI.Domain/Configuration/ModulesOptions.cs`
- `src/TILSOFTAI.Infrastructure/Modules/IModuleLoader.cs`
- `src/TILSOFTAI.Infrastructure/Modules/ModuleLoader.cs`
- `src/TILSOFTAI.Infrastructure/Modules/ModuleLoaderHostedService.cs`
- `src/TILSOFTAI.Infrastructure/Modules/SqlModuleActivationProvider.cs`
- `src/TILSOFTAI.Orchestration/Modules/IModuleActivationProvider.cs`
- `src/TILSOFTAI.Orchestration/Modules/IModuleScopeResolver.cs`
- `src/TILSOFTAI.Orchestration/Modules/ModuleScopeResolver.cs`
- `src/TILSOFTAI.Orchestration/Modules/ModuleScopeResult.cs`
- `tests/TILSOFTAI.Tests/Modules/ModuleActivationTests.cs`

## Deleted In Sprint 21

- `src/TILSOFTAI.Orchestration/Modules/ITilsoftModule.cs`
- `src/TILSOFTAI.Modules.Platform/`
- `src/TILSOFTAI.Modules.Analytics/`

## Remaining Deprecated Runtime Paths

- Legacy SQL objects with historical storage names are compatibility-only; runtime callers use capability-scope wrappers.

## Why These Are Not Deleted Yet

- Bridge fallback and `ChatPipeline` were deleted in Sprint 9.
- Module loader and module scope resolver were deleted in Sprint 20.
- SQL-backed action request persistence remains the production approval persistence boundary.

## Sprint 19 Cleanup Outcome

The obsolete Model module is no longer a project or supported structural concept. Runtime ownership is now documented as Supervisor + Domain Agents + Tool Adapters, with provider/model execution concerns kept out of technical module ownership.

## Sprint 20 Cleanup Outcome

API runtime no longer has a `Modules` configuration section, module autoload hosted service, module health check, module activation provider, or API references to Platform/Analytics package projects. SQL `ModuleKey` names remain only as compatibility names for capability-scope filtering.

## Sprint 21 Cleanup Outcome

The last Platform/Analytics package shells and `ITilsoftModule` contract were removed. Runtime SQL callers moved to capability-scope procedures while legacy SQL storage names remain behind compatibility views/procedures for safe rollout and rollback. SQL deployment scripts moved from `sql/02_modules` to `sql/02_capabilities`.

## Sprint 6 Cleanup Outcome

Native runtime execution no longer depends on module scope resolution or the old orchestration facade. Module-era infrastructure is now explicitly bridge/legacy-only. Runtime telemetry makes native usage, bridge fallback, approval execution, capability invocation, adapter failures, and duration visible.

## Sprint 7 Cleanup Prerequisites

- Native general/chat agent or equivalent fallback replacement.
- Capability-pack loader for legacy tool catalog replacement.
- Expanded non-SQL capability configuration and production endpoint policy.
- Future DB-major migration to rename physical legacy storage names after telemetry confirms no deployed callers depend on the legacy procedures.
