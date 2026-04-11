# Enterprise Readiness Gap Report - Sprint 6

Sprint 6 materially reduces compatibility ownership, but the platform is not yet fully enterprise-grade. This report records what still blocks that status.

## Completed In Sprint 6

- Edge entrypoints now call `ISupervisorRuntime` directly.
- `IOrchestrationEngine`, `OrchestrationEngine`, and `ActionApprovalService` were deleted.
- Native runtime execution is measured separately from bridge fallback.
- Approval lifecycle operations are measured through `IApprovalEngine`.
- A REST/JSON adapter-backed warehouse capability proves the adapter model is not SQL-only.
- HTTP-level authenticated and authorization-failure integration tests exist.
- Dead module-backed capability-pack abstractions were removed.

## Remaining Enterprise Blockers

| Blocker | Why it matters | Recommended next action |
|---------|----------------|-------------------------|
| Bridge fallback still exists | Some requests still execute through `LegacyChatPipelineBridge` and `ChatPipeline`. | Replace catch-all fallback with a supervisor-native general/chat agent or expand native capability coverage. |
| Module loader still supports legacy path | Module loading/scope resolution remains in startup and health checks for `ChatPipeline`. | Introduce capability-pack loading for legacy tool catalog replacement, then remove module scope resolver from runtime registration. |
| REST capability has proof binding only | `warehouse.external-stock.lookup` proves the path, but production endpoint policy/configuration is still thin. | Move external endpoint binding to configuration and add auth/timeout/retry policy per external system. |
| Integration project has IdentityModel version warnings | API reference introduces 8.x identity assemblies while existing test dependencies resolve 7.5. | Align test dependency versions or isolate HTTP pipeline tests in a dedicated API integration project. |
| Existing analytics tests fail | Full test suite still has analytics detector and renderer failures unrelated to Sprint 6. | Fix analytics intent detection/localization and renderer newline expectations before calling the suite fully green. |
| SQL remains dominant | Most production capabilities are still SQL-backed. | Add a second production-style REST or gRPC capability and document endpoint governance. |
| Legacy module health is still exposed | Health checks still include module-era diagnostics. | Split native readiness from legacy fallback diagnostics. |

## Verification Notes

Passing Sprint 6-focused checks:
- API build succeeds.
- Focused capability and observability unit tests pass.
- Focused integration tests pass for HTTP auth, authorization failure, warehouse native, accounting native, approval lifecycle, auth context threading, and REST-backed warehouse capability.

Known non-Sprint-6 failures:
- Full unit suite currently fails older analytics/renderer tests.
- Full integration suite currently fails `DeepAnalyticsE2ETests.IntentDetector_AnalyticsQuery_ShouldDetectAsAnalytics`.
