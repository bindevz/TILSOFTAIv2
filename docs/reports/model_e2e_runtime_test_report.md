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
- Validation script: `sql/current/999_validate_model_runtime.sql`
- Validation result: `passed`
- Model row count at validation time: `6`

The migration creates and validates the current model runtime objects, model read-only stored procedures, optional local seed data, diagnostics, and model-only semantic capability/trace tables.

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
- RawJson data prompts routed and composed successfully according to logs, but `/api/chats` currently returns an empty top-level `content` field in its response envelope. The routing/composer evidence is present in logs and should be considered a response-contract tuning item.
- The missing-model Structured response asked for the model code: `Bạn muốn xem thông tin của model nào ạ? Vui lòng cung cấp mã model (model code) cụ thể để tôi có thể hỗ trợ bạn.`

## Issues Fixed During Runtime Verification

- SQL scalar result envelopes now parse `{ meta, columns, rows }` so rows reach the answer composer.
- No-tool follow-up traces now persist valid `{}` JSON for normalized arguments.
- SQL trace insert now escapes `[RowCount]`.
- Active model stored procedure bindings use bare `ai_model_*` names to satisfy the existing SQL adapter validation.
- Optional local seed now includes tenant `default`, matching local no-auth execution context.

