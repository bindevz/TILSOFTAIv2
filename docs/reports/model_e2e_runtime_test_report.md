# Model E2E Runtime Test Report

Generated: 2026-04-29

## Scope

This report records the Sprint 39 model-only end-to-end runtime smoke for the local SQL migration framework and Microsoft Agent Framework tool-routing path.

Runtime path verified:

`API / Hub / OpenAI-compatible surface -> ISupervisorRuntime -> OfficialAgentToolRouter -> OfficialMicrosoftAgentRuntime -> official Microsoft Agent Framework AIAgent -> model-only AIFunction tools -> CapabilityExecutionFacade -> SqlToolAdapter -> AnswerComposer`

## Environment

- API: `http://localhost:5218`
- Local AI endpoint: `http://192.168.8.247:6688/v1`
- Local AI model: `gemma4:e4b`
- SQL server: `localhost`
- SQL database: `TILSOFTAI`
- SQL tenant used by no-auth local smoke: `default`
- Auth: disabled for local smoke
- Routing: official Microsoft Agent Framework enabled
- Fallback to legacy pipeline: disabled
- Allowed domains: `model`
- Tool calling required: enabled
- Evidence folder: `artifacts/phase5`
- API log: `artifacts/api-phase5.out.log`

## SQL Migration Verification

The current migration runner completed successfully against local SQL Server.

- Runner: `tools/sql/migrate-local-tilsoftai.ps1`
- Scripts: `sql/current/*.sql`
- Runtime validation script: `sql/current/999_validate_model_runtime.sql`
- Catalog validation script: `sql/current/997_validate_capability_catalog.sql`
- Validation result: `passed`
- Model row count at validation time: `6`

The migration creates and validates the current model runtime objects, model read-only stored procedures, optional local seed data, diagnostics, and SQL-backed model capability catalog tables.

## Sprint 43 SQL-Backed Catalog Verification

The active runtime no longer registers `ModelCapabilities.All`. Production DI maps `ICapabilityRegistry`, `ICapabilityMetadataRepository`, `ISqlBackedCapabilityCatalog`, and `ICapabilityCatalogReloader` to `SqlCapabilityCatalogRepository`.

Catalog source-of-truth files:

- `sql/current/002_core_tables.sql`
- `sql/current/008_seed_model_capability_catalog.sql`
- `sql/current/997_validate_capability_catalog.sql`

Catalog guarantees:

- Exactly six enabled capabilities in the `model` domain.
- No enabled non-model capabilities.
- Function names are SQL-seeded and exposed to the official Agent Framework.
- Model-facing arguments use `modelCode`, `modelCodes`, and optional `season`; `modelId` and `model_id` are not exposed.
- Tool descriptions are assembled from SQL-localized text, aliases, examples, argument text, result schema, answer policy, and sensitivity policy.
- Structured and RawJson answers consume SQL result schema, answer policy, and sensitivity policy through the execution facade.
- Catalog reload is manual and opt-in through `POST /api/platform-catalog/capabilities/reload`, gated by `CatalogReload:Enabled` and local/development environment checks.

## Prompt Matrix

| Prompt | Mode | Status | Expected behavior | Result |
| --- | --- | ---: | --- | --- |
| `Có bao nhiêu model?` | Structured | 200 | Select `model.count` and execute `ai_model_count` | Passed |
| `Có bao nhiêu model?` | RawJson | 200 | Select `model.count` and execute `ai_model_count` | Passed |
| `Cho tôi xem thông tin model ABC` | Structured | 200 | Select `model.overview.by-code` and execute `ai_model_get_overview` | Passed |
| `Cho tôi xem thông tin model ABC` | RawJson | 200 | Select `model.overview.by-code` and execute `ai_model_get_overview` | Passed |
| `Model SET-DINING-001 gồm những piece nào?` | Structured | 200 | Select `model.pieces.by-code` and execute `ai_model_get_pieces` | Passed |
| `Model SET-DINING-001 gồm những piece nào?` | RawJson | 200 | Select `model.pieces.by-code` and execute `ai_model_get_pieces` | Passed |
| `Show materials for model ABC` | Structured | 200 | Select `model.materials.by-code` and execute `ai_model_get_materials` | Passed |
| `Show materials for model ABC` | RawJson | 200 | Select `model.materials.by-code` and execute `ai_model_get_materials` | Passed |
| `So sánh model ABC và XYZ` | Structured | 200 | Select `model.compare` and execute `ai_model_compare` | Passed |
| `So sánh model ABC và XYZ` | RawJson | 200 | Select `model.compare` and execute `ai_model_compare` | Passed |
| `Cho tôi xem thông tin model` | Structured | 200 | Ask a follow-up for missing model code, no guessing | Passed |
| `Cho tôi xem thông tin model` | RawJson | 200 | Do not guess or execute a read tool without code | Passed with note |

Phase 5 summary file: `artifacts/phase5/summary.json`.

## Routing Evidence

The API log contains the expected official model-only trace points:

- `agent_route_started` with `fallbackUsed: false`
- `candidate_selection_completed` with `candidate_count: 6`
- `tools_advertised` with the six model tools
- `agent_tool_invoked` with selected model function names for data prompts
- `answer_composer_completed` with row counts for data prompts
- `answer_composer_completed` with `answerType: follow_up` for the missing-code structured prompt

Observed model tools advertised:

- `model_count`
- `model_compare`
- `model_materials_by_code`
- `model_overview_by_code`
- `model_packaging_by_code`
- `model_pieces_by_code`

Observed row-count evidence from logs:

- Count: `rowCount: 1`
- Overview: non-zero row count for `model.overview.by-code`
- Pieces: non-zero row count for `model.pieces.by-code`
- Materials: non-zero row count for `model.materials.by-code`
- Compare: `rowCount: 2`
- Missing model code: `rowCount: 0`, `agent.no-tool`, follow-up instead of guessing

## Notes

- The OpenAI-compatible `/v1/chat/completions` endpoint was used for Structured smoke because it returns assistant text directly.
- The `/api/chats` endpoint was used for RawJson smoke because it accepts `metadata.answerMode=rawJson`.
- RawJson data prompts route and compose through a deterministic enterprise envelope. The envelope includes mode, capability key, function name, procedure name, sanitized arguments, row count, sanitized rows, result schema, execution metadata, applied sensitivity policy, and provenance.
- Structured model responses now use deterministic conservative summaries for `model.count`, `model.overview.by-code`, `model.pieces.by-code`, `model.materials.by-code`, `model.compare`, and `model.packaging.by-code`.
- Structured table blocks are schema-driven. They use Vietnamese labels from `labelVi` for `vi-VN`, English labels from `label` for `en-US`, omit invisible columns, and include `rowCount`, `displayedRows`, and `truncated`.
- The missing-model Structured response is now specific and does not execute SQL when the required model code is missing: `Bạn muốn xem thông tin cho model nào? Vui lòng cung cấp mã model.`
- No-data responses include the filters used and do not invent alternative model codes or follow-up suggestions when the requested filter was already provided.
- AI summary service usage is guarded. RawJson does not call it, and deterministic model Structured responses can run without it.

## Sprint 42 AnswerComposer Quality Verification

Phase 8 unit coverage added or updated:

- `AnswerComposer_RawJson_AllModelCapabilities`
- `AnswerComposer_Structured_AllModelCapabilities`
- `AnswerComposer_Localization_ViAndEn`
- `AnswerComposer_NoData`
- `AnswerComposer_FollowUp`
- `AnswerComposer_Truncation`
- `AnswerComposer_SensitiveFieldMasking`

Focused AnswerComposer verification passed locally:

- `dotnet test tests/TILSOFTAI.Tests/TILSOFTAI.Tests.csproj --filter AnswerComposerTests`
- Result: 39 passed, 0 failed, 0 skipped

Full local verification after the Sprint 42 AnswerComposer upgrade:

- `dotnet build`
- Result: passed with existing NU1902 OpenTelemetry warnings
- `dotnet test`
- Result: 330 unit tests passed and 19 integration tests passed

Full local verification after the Sprint 43 SQL-backed catalog migration:

- Baseline `dotnet build`: passed with existing NU1902 OpenTelemetry warnings.
- Baseline `dotnet test`: 330 unit tests passed and 19 integration tests passed.
- Final `dotnet test` during Phase 10: 337 unit tests passed and 19 integration tests passed.

Residual notes:

- OpenTelemetry dependency vulnerability warnings remain outside the AnswerComposer scope.
- Local SQL migration and live model E2E smoke require a running SQL Server and local AI endpoint.

## Issues Fixed During Runtime Verification

- SQL scalar result envelopes now parse `{ meta, columns, rows }` so rows reach the answer composer.
- No-tool follow-up traces now persist valid `{}` JSON for normalized arguments.
- SQL trace insert now escapes `[RowCount]`.
- Active model stored procedure bindings use bare `ai_model_*` names to satisfy the existing SQL adapter validation.
- Optional local seed now includes tenant `default`, matching local no-auth execution context.
- Active model capability metadata now comes from SQL catalog tables instead of production code fixtures.
- AnswerComposer now emits stable answer types: `raw_json`, `structured`, `follow_up`, `no_data`, `error`, and `write_preview`.
- Result schema metadata now drives table labels, visibility, and frontend-friendly table metadata.
- Sensitive fields are masked or hidden before display, summary, and RawJson detail exposure.
