# Enterprise Readiness Gap Report - Sprint 7

Sprint 7 materially reduces fallback dependence and improves validation credibility, but the platform is not yet fully enterprise-grade. This report records what still blocks that status.

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

## Remaining Enterprise Blockers

| Blocker | Why it matters | Recommended next action |
|---------|----------------|-------------------------|
| Bridge fallback still exists | Some unmatched domain-capability requests can still execute through `LegacyChatPipelineBridge` and `ChatPipeline`. | Expand native capability coverage and remove bridge fallback request classes one by one. |
| Module loader still supports legacy path | Module loading/scope resolution remains for `ChatPipeline` and legacy diagnostics. | Introduce capability-pack loading for legacy tool catalog replacement, then remove module scope resolver from runtime registration. |
| REST secrets are metadata values | REST adapter supports auth/header policy, but secret sourcing is still caller/config responsibility. | Wire REST auth metadata to the platform secret provider or external connection catalog. |
| SQL remains dominant | Most production capabilities are still SQL-backed. | Add a second production-style REST or gRPC capability and document endpoint governance. |
| Argument contracts remain light | Native capability argument extraction is still dictionary/string based. | Add schema validation for at least one warehouse SQL, one accounting SQL, and the REST stock capability. |

## Verification Notes

Sprint 7 green baseline:
- `dotnet build src/TILSOFTAI.Api/TILSOFTAI.Api.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1`
- `dotnet test tests/TILSOFTAI.Tests/TILSOFTAI.Tests.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1 -v:minimal`
- `dotnet test tests/TILSOFTAI.IntegrationTests/TILSOFTAI.IntegrationTests.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1 -v:minimal`

Known bounded skip:
- `DeepAnalyticsE2ETests.AnalyticsWorkflow_VietnameseQuery_ShouldReturnValidInsight` remains skipped as an external/deep workflow test boundary.
