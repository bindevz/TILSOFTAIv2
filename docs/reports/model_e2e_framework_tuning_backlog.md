# Model E2E Framework Tuning Backlog

Generated: 2026-04-29

## P0

1. Normalize SQL result envelope contracts across adapters.
   - Current state: model stored procedures return a scalar JSON envelope with `{ meta, columns, rows }`.
   - Runtime fix: `CapabilityExecutionFacade` now reads `rows` from that envelope.
   - Next tuning: add focused unit tests for scalar-envelope parsing, list payload parsing, malformed JSON, and empty results.

2. Make no-tool/follow-up trace persistence non-blocking.
   - Current state: no-tool follow-up is valid runtime behavior, but trace persistence previously converted it into a failed request when nullable arguments serialized to SQL-invalid JSON.
   - Runtime fix: no-tool normalized arguments now persist as `{}`.
   - Next tuning: add tests around `ToolRoutingTraceFactory` for tool execution, validation failure, no-tool follow-up, and not-handled traces.

3. Formalize local no-auth tenant behavior.
   - Current state: local no-auth smoke uses tenant `default`.
   - Runtime fix: optional local seed data now includes `default`.
   - Next tuning: document tenant selection explicitly and add validation that local seed tenants match the local execution context.

## P1

1. Align RawJson HTTP response contracts.
   - Current state: routing and composer logs show RawJson answers succeed with row counts, but `/api/chats` returns an empty top-level `content` field.
   - Target: expose raw JSON payload or a documented typed response field consistently from `/api/chats`.
   - Acceptance: RawJson smoke files should contain the composed capability payload without requiring log inspection.

2. Add migration idempotency and schema drift tests.
   - Current state: `tools/sql/migrate-local-tilsoftai.ps1` applies ordered scripts and validation passes locally.
   - Target: run migration twice in CI against disposable SQL Server and assert no failures, duplicate seed rows, or invalid schema drift.

3. Add SQL contract validation outside Development.
   - Current state: SQL contract validation is skipped in Development startup.
   - Target: provide an explicit local switch to run the same validation during smoke, without forcing production startup semantics.

4. Reduce model-only semantic metadata duplication.
   - Current state: model capabilities are represented in code, catalog JSON, and SQL semantic tables.
   - Target: establish one generated/source-of-truth flow for active model capabilities, stored procedures, argument contracts, and result schemas.

## P2

1. Improve answer text richness for Structured mode.
   - Current state: Structured mode confirms row count and first rows, but the text remains generic.
   - Target: use result schema metadata for more helpful field labels and compact summaries while keeping no hallucination/no guessing guarantees.

2. Make route evidence easier to query.
   - Current state: route evidence exists in JSON logs and `ai.ToolRoutingTrace`.
   - Target: add a local diagnostics query or report script that summarizes candidate count, advertised tools, selected function, row count, answer type, and fallback usage per correlation ID.

3. Clarify compatibility exceptions for archived SQL.
   - Current state: legacy SQL was archived, while `sql/ai` and `sql/97_legacy_diagnostics` remain in place for existing guard tests.
   - Target: either move tests to `sql/archive` fixtures or document the compatibility exception in the SQL README/report.

4. Address dependency vulnerability warnings.
   - Current state: build and test complete, but `OpenTelemetry.Api` and `OpenTelemetry.Exporter.OpenTelemetryProtocol` produce NU1902 warnings.
   - Target: upgrade packages or centrally suppress with an accepted risk note.

## Evidence Links

- Runtime test report: `docs/reports/model_e2e_runtime_test_report.md`
- Smoke outputs: `artifacts/phase5`
- API route logs: `artifacts/api-phase5.out.log`
- Migration scripts: `sql/current`
- Migration runner: `tools/sql/migrate-local-tilsoftai.ps1`

