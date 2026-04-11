# Enterprise Readiness Gap Report - Sprint 9

Sprint 9 retires the legacy bridge/ChatPipeline runtime path, introduces platform-owned catalogs, and deepens representative contract validation. The platform is closer to enterprise-grade, but the catalog admin/write path and broader contract coverage still need work.

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
- `LegacyChatPipelineBridge`, `ChatPipeline`, `ChatRequest`, and `ChatResult` were deleted.
- Production capability and external connection records now load from `catalog/platform-catalog.json` with static/bootstrap/platform precedence.
- Platform SQL catalog tables/procedures exist as the admin-managed persistence target.
- Representative contracts validate type, format, enum, and length/range constraints.
- Deep analytics E2E is isolated as `Category=ExternalDeepWorkflow`, owned by Analytics, and gated by `TEST_SQL_CONNECTION`.

## Remaining Enterprise Blockers

| Blocker | Why it matters | Recommended next action |
|---------|----------------|-------------------------|
| Catalog admin write path is not complete | Platform records exist, but admin-managed SQL mutation/audit workflows are not yet wired into runtime operations. | Implement catalog writer/reviewer APIs over `PlatformCapabilityCatalog` and `PlatformExternalConnectionCatalog`. |
| Bootstrap configuration still exists | Bootstrap fallback remains useful operationally but must not become the production catalog owner again. | Add startup/reporting checks that distinguish platform records from bootstrap fallbacks. |
| Module packages still exist | Module loading is no longer central, but module packages and diagnostic loading remain. | Convert remaining module package metadata into platform catalog/tool records or mark modules as packaging only. |
| SQL remains dominant | Most production capabilities are still SQL-backed even with two governed REST paths. | Continue adding governed non-SQL capabilities where production workflows require them. |
| Contract coverage is representative, not universal | Sprint 9 typed validation covers representative capabilities, but not every capability has rich typed constraints. | Extend typed contracts across all capability records and consider JSON Schema interop. |

## Verification Notes

Sprint 9 green baseline:
- `dotnet build src/TILSOFTAI.Api/TILSOFTAI.Api.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1`
- `dotnet test tests/TILSOFTAI.Tests/TILSOFTAI.Tests.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1 -v:minimal`
- `dotnet test tests/TILSOFTAI.IntegrationTests/TILSOFTAI.IntegrationTests.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1 -v:minimal`

Known bounded skip:
- `DeepAnalyticsE2ETests.AnalyticsWorkflow_VietnameseQuery_ShouldReturnValidInsight` remains skipped as an external/deep workflow test boundary.
