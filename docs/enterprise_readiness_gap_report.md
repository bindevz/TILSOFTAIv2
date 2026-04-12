# Enterprise Readiness Gap Report - Sprint 12

Sprint 12 turns the governed catalog control plane into a certifiable production path. Promotion gates now block unsafe source modes, invalid previews, missing expected-version coverage, unsafe break-glass posture, and missing accepted evidence. The code can record and enforce certification readiness, but real staging/prod-like execution evidence is still required before claiming live certification.

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
- Catalog preview validates mutation payloads before submit.
- Existing-record mutation can require `ExpectedVersionTag` in production-like environments.
- Duplicate pending changes are detected by payload hash and idempotency key.
- Apply replay for already applied changes is idempotent.
- High-risk changes require senior approval.
- Production-like environments can require independent apply after approval.
- Bootstrap fallback is disabled by default in production config and strict source modes are unhealthy.
- Rollback is represented through governed compensating changes with `RollbackOfChangeId`.
- Contract metadata now includes `ContractVersion`, `SchemaDialect`, and `SchemaRef`.
- Module packages are formally retained only as non-runtime packaging or diagnostic artifacts.
- Catalog promotion gates evaluate source mode, preview validity, approved-change state, expected-version policy, break-glass containment, and certification evidence.
- Certification evidence has durable SQL storage and API capture/list endpoints.
- Catalog control-plane SLO and alert/escalation definitions are exposed through API and documentation.
- Release, live certification, fallback, and emergency-path runbooks now describe promotion-gate and evidence requirements.

## Remaining Enterprise Blockers

| Blocker | Why it matters | Recommended next action |
|---------|----------------|-------------------------|
| Catalog admin write path needs live certification | The platform now stores and enforces evidence, but the local implementation run did not execute real staging/prod-like drills. | Run the runbook and failure drills against staging/prod-like SQL with signed-off accepted evidence. |
| Bootstrap fallback still exists as an emergency mechanism | Production config is stricter, but fallback code remains available for lower environments and emergencies. | Keep production fallback disabled by default and alert on any fallback source mode. |
| Module packages still physically exist | Their end-state is non-runtime, but physical removal is optional future cleanup. | Remove packages only when packaging/diagnostic ownership no longer needs them. |
| SQL remains dominant | Most production capabilities are still SQL-backed even with two governed REST paths. | Continue adding governed non-SQL capabilities where production workflows require them. |
| Contract richness still depends on capability shape | Schema lifecycle exists, but future records must keep using it consistently. | Enforce preview and contract review in catalog operations and evaluate JSON Schema artifacts when schemas become shared. |
| Evidence acceptance still depends on operator discipline | The API records status and approver ids, but does not independently verify external evidence URLs. | Add evidence URI verification, artifact retention checks, or signed evidence bundles when compliance requires it. |

## Verification Notes

Sprint 12 local verification should include:
- `dotnet build src/TILSOFTAI.Api/TILSOFTAI.Api.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1`
- `dotnet test tests/TILSOFTAI.Tests/TILSOFTAI.Tests.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1 -v:minimal`
- `dotnet test tests/TILSOFTAI.IntegrationTests/TILSOFTAI.IntegrationTests.csproj -nologo --no-restore /nr:false /p:UseSharedCompilation=false -m:1 -v:minimal`

Known bounded skip:
- `DeepAnalyticsE2ETests.AnalyticsWorkflow_VietnameseQuery_ShouldReturnValidInsight` remains skipped as an external/deep workflow test boundary.
