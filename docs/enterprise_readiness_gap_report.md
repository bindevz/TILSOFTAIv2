# Enterprise Readiness Gap Report - Sprint 10

Sprint 10 adds the governed catalog control plane, source-of-truth visibility, catalog integrity validation, and explicit module package classifications. The platform is closer to enterprise-grade operational control; remaining gaps are now around production hardening and reducing fallback dependency.

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
- Platform catalog change requests are SQL-backed and exposed through submit, approve, reject, and apply control-plane APIs.
- Catalog mutation requires configured submit/approve roles and blocks self-approval by default.
- Catalog integrity validation reports duplicate keys, unresolved REST connection references, raw secret metadata, and missing contracts.
- `/health/ready` reports platform catalog source mode and degrades on bootstrap fallback.
- Startup emits catalog source mode metrics/logs; mutation emits catalog mutation metrics and governance/config-change audit events.
- Remaining module packages are classified as packaging-only or diagnostic-only.
- Production catalog records include explicit argument contracts, including no-argument contracts for summary/list capabilities.

## Remaining Enterprise Blockers

| Blocker | Why it matters | Recommended next action |
|---------|----------------|-------------------------|
| Catalog admin write path needs production exercising | SQL-backed submit/review/apply exists, but production operators still need migration/runbook adoption and failure drills. | Run the control plane against a real catalog database, document operational runbooks, and add environment-specific approval policies. |
| Bootstrap configuration still exists | Bootstrap fallback remains useful operationally but must not become the production catalog owner again. | Treat `mixed` and `bootstrap_only` readiness as deployment warnings and continue moving records to the platform catalog. |
| Module packages still exist | Module loading is no longer central, but module packages and diagnostic loading remain. | Keep classifications explicit, then convert remaining diagnostic metadata into platform catalog/tool records where useful. |
| SQL remains dominant | Most production capabilities are still SQL-backed even with two governed REST paths. | Continue adding governed non-SQL capabilities where production workflows require them. |
| Contract richness is uneven | Every production capability now has a contract, but summary/list capabilities only need no-argument contracts and future capabilities may need richer typed rules. | Continue adding typed constraints as new catalog records are introduced and consider JSON Schema interop. |

## Verification Notes

Sprint 9 green baseline:
- `dotnet build src/TILSOFTAI.Api/TILSOFTAI.Api.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1`
- `dotnet test tests/TILSOFTAI.Tests/TILSOFTAI.Tests.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1 -v:minimal`
- `dotnet test tests/TILSOFTAI.IntegrationTests/TILSOFTAI.IntegrationTests.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1 -v:minimal`

Known bounded skip:
- `DeepAnalyticsE2ETests.AnalyticsWorkflow_VietnameseQuery_ShouldReturnValidInsight` remains skipped as an external/deep workflow test boundary.
