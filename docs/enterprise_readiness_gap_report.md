# Enterprise Readiness Gap Report - Sprint 8

Sprint 8 materially improves governance, contract validation, and readiness semantics, but the platform is not yet fully enterprise-grade. This report records what still blocks that status.

## Completed Through Sprint 7

- Edge entrypoints now call `ISupervisorRuntime` directly.
- `IOrchestrationEngine`, `OrchestrationEngine`, and `ActionApprovalService` were deleted.
- Native runtime execution is measured separately from bridge fallback.
- Approval lifecycle operations are measured through `IApprovalEngine`.
- `LegacyChatDomainAgent` was deleted and replaced by supervisor-native `GeneralChatAgent`.
- Bridge fallback reasons are explicit and measured.
- Capability execution can be denied by required role or allowed tenant before adapter resolution.
- REST/JSON adapter-backed capabilities support configuration-driven binding, timeout, retry, auth/header metadata, and classified failures.
- Native readiness is separated from module-era diagnostic health.
- HTTP-level authenticated and authorization-failure integration tests exist.
- Dead module-backed capability-pack abstractions were removed.
- Analytics detector and renderer regressions were fixed.
- Full unit and integration suites are green, with one intentionally skipped deep analytics E2E test.
- External REST auth is connection-catalog and secret-provider backed.
- Capability argument contracts are enforced before adapter execution.
- `NativeRuntimeHealthCheck` is domain-agnostic.
- A second governed REST-backed capability path exists: `accounting.exchange-rate.lookup`.
- Module legacy autoload is disabled by default.

## Remaining Enterprise Blockers

| Blocker | Why it matters | Recommended next action |
|---------|----------------|-------------------------|
| Bridge fallback still exists | Explicit legacy fallback still executes through `LegacyChatPipelineBridge` and `ChatPipeline`. | Replace explicit legacy fallback with supervisor-native general workflows, then delete the bridge. |
| Module loader still supports legacy path | Module loading/scope resolution remains for legacy `ChatPipeline` diagnostics when enabled. | Introduce capability-pack loading for legacy tool catalog replacement, then remove module scope resolver from runtime registration. |
| Capability catalog is still config-backed | Production capability shape is no longer only static code, but app configuration is not a durable platform catalog. | Move capability and connection records to SQL/admin-managed catalog with audit/change control. |
| SQL remains dominant | Most production capabilities are still SQL-backed even with two governed REST paths. | Continue adding governed non-SQL capabilities where production workflows require them. |
| Argument contracts remain shallow | Sprint 8 validates required/allowed names, but not types/ranges/formats. | Add JSON schema or typed contract validation with operator-safe error details. |

## Verification Notes

Sprint 8 green baseline:
- `dotnet build src/TILSOFTAI.Api/TILSOFTAI.Api.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1`
- `dotnet test tests/TILSOFTAI.Tests/TILSOFTAI.Tests.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1 -v:minimal`
- `dotnet test tests/TILSOFTAI.IntegrationTests/TILSOFTAI.IntegrationTests.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1 -v:minimal`

Known bounded skip:
- `DeepAnalyticsE2ETests.AnalyticsWorkflow_VietnameseQuery_ShouldReturnValidInsight` remains skipped as an external/deep workflow test boundary.
