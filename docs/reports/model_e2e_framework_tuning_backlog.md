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
   - Current state: AnswerComposer now produces a complete RawJson enterprise envelope with mode, capability key, function name, procedure name, sanitized arguments, row count, rows, result schema, execution metadata, applied sensitivity policy, and provenance.
   - Target: expose raw JSON payload or a documented typed response field consistently from `/api/chats`.
   - Acceptance: RawJson smoke files should contain the composed capability payload without requiring log inspection.

2. Add migration idempotency and schema drift tests.
   - Current state: `tools/sql/migrate-local-tilsoftai.ps1` applies ordered scripts and validation passes locally.
   - Target: run migration twice in CI against disposable SQL Server and assert no failures, duplicate seed rows, or invalid schema drift.

3. Add SQL contract validation outside Development.
   - Current state: SQL contract validation is skipped in Development startup.
   - Target: provide an explicit local switch to run the same validation during smoke, without forcing production startup semantics.

4. Add disposable SQL catalog integration coverage in CI.
   - Current state: unit and architecture tests guard the SQL catalog files and runtime DI, while local SQL migration remains environment-dependent.
   - Target: run `tools/sql/migrate-local-tilsoftai.ps1` and `sql/current/997_validate_capability_catalog.sql` against disposable SQL Server in CI.
   - Acceptance: catalog drift fails CI when enabled model count, model-facing arguments, result schema JSON, answer policy JSON, or stored procedure references drift.

## P2

1. Extend deterministic summaries beyond model capabilities.
   - Current state: Sprint 42 added deterministic conservative summaries for the six model capabilities and guarded AI summary usage.
   - Target: when new read-only domains are introduced, add domain-specific deterministic summary rules at the AnswerComposer boundary.
   - Acceptance: summaries use only row count, arguments, result schema, and known returned columns; no LLM output controls tables, follow-ups, provenance, or safety.

2. Make route evidence easier to query.
   - Current state: route evidence exists in JSON logs and `ai.ToolRoutingTrace`.
   - Target: add a local diagnostics query or report script that summarizes candidate count, advertised tools, selected function, row count, answer type, and fallback usage per correlation ID.

3. Clarify compatibility exceptions for archived SQL.
   - Current state: legacy SQL was archived, while `sql/ai` and `sql/97_legacy_diagnostics` remain in place for existing guard tests.
   - Target: either move tests to `sql/archive` fixtures or document the compatibility exception in the SQL README/report.

4. Address dependency vulnerability warnings.
   - Current state: build and test complete, but `OpenTelemetry.Api` and `OpenTelemetry.Exporter.OpenTelemetryProtocol` produce NU1902 warnings.
   - Target: upgrade packages or centrally suppress with an accepted risk note.

## Completed in Sprint 42

1. Normalize AnswerComposer answer shape and answer types.
   - Completed: answers now carry stable `answerType`, `text`, `blocks`, `detail`, `followUpQuestions`, `provenance`, `correlationId`, and `locale`.
   - Completed: model write previews use `write_preview`; composite model output is emitted through the stable `structured` answer type.

2. Use result schema metadata for Structured tables.
   - Completed: table labels use `labelVi` for `vi-VN` and `label` for English.
   - Completed: invisible schema columns are omitted, sensitive hidden columns are removed, and masked columns display the mask value.
   - Completed: table blocks include title, `rowCount`, `displayedRows`, and `truncated`.

3. Improve no-data and follow-up quality.
   - Completed: missing model code produces a specific follow-up and does not execute SQL.
   - Completed: no-data responses include used filters and do not invent alternatives.

4. Guard optional AI summaries.
   - Completed: RawJson does not call AI summary.
   - Completed: deterministic model Structured responses run without `AiSummaryService`.
   - Completed: AI summary cannot override table, follow-up, safety, or provenance decisions.

## Completed in Sprint 43

1. Promote SQL to the model capability source of truth.
   - Completed: production `ModelCapabilities` was removed.
   - Completed: `SqlCapabilityCatalogRepository` supplies `ICapabilityRegistry`, `ICapabilityMetadataRepository`, and SQL-backed catalog reload services.
   - Completed: `sql/current/008_seed_model_capability_catalog.sql` seeds the six active model capabilities.

2. Validate catalog drift.
   - Completed: `sql/current/997_validate_capability_catalog.sql` rejects wrong enabled capability counts, enabled non-model capabilities, duplicate function names, forbidden `modelId`/`model_id` model-facing arguments, invalid JSON metadata, and missing stored procedures.
   - Completed: unit architecture tests guard the same contract when SQL Server is not available.

3. Drive model-facing tools and answers from SQL metadata.
   - Completed: tool descriptions include SQL text, aliases, examples, argument clarification text, result schema, answer policy, and sensitivity policy.
   - Completed: AnswerComposer uses SQL-shaped result schemas and policies for tables, labels, visibility, row limits, and sensitivity masking.

4. Add guarded reload path.
   - Completed: catalog reload is exposed only when explicitly enabled in local/development configuration.
   - Completed: repository reload fails closed on initial load and preserves the last-known-good catalog on reload failure.

## Evidence Links

- Runtime test report: `docs/reports/model_e2e_runtime_test_report.md`
- Smoke outputs: `artifacts/phase5`
- API route logs: `artifacts/api-phase5.out.log`
- Migration scripts: `sql/current`
- Migration runner: `tools/sql/migrate-local-tilsoftai.ps1`
- SQL-backed catalog architecture note: `docs/architecture/sql_backed_capability_catalog.md`
