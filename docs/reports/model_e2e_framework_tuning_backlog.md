# Model E2E Framework Tuning Backlog

Generated: 2026-04-29

## P0

Sprint 46 summary quality evaluation:

- No open P0 summary safety or factuality blockers from deterministic evals.
- Maintain the gate that blocks the next domain if any masked field leak, stored procedure leak, fabricated numeric total, locale failure, no-data regression, or missing-argument follow-up regression appears.

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

Sprint 46 summary quality evaluation:

- Run `TILSOFTAI_RUN_AI_SUMMARY_EVALS=true dotnet test` against the selected local model before enabling the Purchasing read-only pilot outside deterministic readiness.
- Keep summary policy tuning catalog-driven; do not add domain-specific summary branches to `StructuredAnswerComposer`.

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

Sprint 46 summary quality evaluation:

- Improve Vietnamese style instructions in `summary.instructionsByLocale` after human review of AI-mode output.
- Add result schema `businessMeaning` or equivalent metadata before asking AI summaries to explain business context.
- Add a stronger numeric aggregation guard that distinguishes copied supplied numbers from inferred totals.
- Add table labels for any Purchasing pilot columns before turning on AI summaries.
- Expand redaction tests to cover masked values in arguments as well as rows.

1. Extend catalog-driven answer policy beyond model capabilities when new domains are approved.
   - Current state: SQL catalog policy controls summary mode, narration row limits, table display, no-data filters, follow-up missing fields, truncation notices, and locale instructions for the six model capabilities.
   - Target: when new read-only domains are introduced, seed `CapabilityAnswerPolicy` JSON before enabling capabilities.
   - Acceptance: answer behavior is changed through SQL policy, not capability-specific C# branches.

2. Make route evidence easier to query.
   - Current state: route evidence exists in JSON logs and `ai.ToolRoutingTrace`.
   - Target: add a local diagnostics query or report script that summarizes candidate count, advertised tools, selected function, row count, answer type, and fallback usage per correlation ID.

3. Clarify compatibility exceptions for archived SQL.
   - Current state: legacy SQL was archived, while `sql/ai` and `sql/97_legacy_diagnostics` remain in place for existing guard tests.
   - Target: either move tests to `sql/archive` fixtures or document the compatibility exception in the SQL README/report.

4. Address dependency vulnerability warnings.
   - Current state: build and test complete, but `OpenTelemetry.Api` and `OpenTelemetry.Exporter.OpenTelemetryProtocol` produce NU1902 warnings.
   - Target: upgrade packages or centrally suppress with an accepted risk note.

## P3

Sprint 46 summary quality evaluation:

- Publish answer summary eval report artifacts from CI once an AI-capable evaluation lane exists.
- Add a lightweight human-review score rubric for usefulness and executive-readiness beyond objective safety checks.
- Track summary fallback rate and guard failures as release evidence for each new read-only domain.

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

## Completed in Sprint 45

1. Configure answer behavior through SQL catalog.
   - Completed: `CapabilityAnswerPolicy` JSON includes `summary`, `table`, `noData`, `followUp`, `maxRowsForChat`, and `maxRowsForNarration`.
   - Completed: summary modes support `ai`, `fallback`, and `disabled`.
   - Completed: table visibility and max displayed rows are policy-driven.
   - Completed: no-data filter display, follow-up missing fields, truncation notices, and locale instructions are policy-driven.
   - Completed: catalog validation rejects missing sections, invalid summary modes, non-positive row limits, and malformed locale/forbidden-claim sections.

Configuration examples:

- Enable AI summary with `summary.mode = "ai"`.
- Disable AI summary with `summary.mode = "disabled"`.
- Use fallback-only summary with `summary.mode = "fallback"`.
- Hide the table block with `table.enabled = false`.
- Change max table rows with `table.maxDisplayedRows`.
- Change locale instructions with `summary.instructionsByLocale.vi-VN` and `summary.instructionsByLocale.en-US`.

## Evidence Links

- Runtime test report: `docs/reports/model_e2e_runtime_test_report.md`
- Smoke outputs: `artifacts/phase5`
- API route logs: `artifacts/api-phase5.out.log`
- Migration scripts: `sql/current`
- Migration runner: `tools/sql/migrate-local-tilsoftai.ps1`
- SQL-backed catalog architecture note: `docs/architecture/sql_backed_capability_catalog.md`
