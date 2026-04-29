# TILSOFTAI V3

TILSOFTAI is an internal AI runtime for executing governed, SQL-backed capabilities through the official Microsoft Agent Framework. The active development runtime is intentionally narrow: the `model` domain is the only enabled domain, all active capabilities are read-only, and write execution remains disabled.

## Current Architecture

```text
API / Hub / OpenAI-compatible surface
  -> ISupervisorRuntime
  -> OfficialAgentToolRouter
  -> OfficialMicrosoftAgentRuntime
  -> official Microsoft Agent Framework AIAgent
  -> model-only AIFunction tools
  -> CapabilityExecutionFacade
  -> SqlToolAdapter
  -> AnswerComposer
```

Runtime boundaries:

- `CapabilityExecutionFacade` is the execution boundary for capability calls.
- `AnswerComposer` is the response boundary for RawJson and Structured answers.
- Active capabilities are loaded from SQL catalog tables in `sql/current/002_core_tables.sql` and seeded by `sql/current/008_seed_model_capability_catalog.sql`.
- The SQL catalog is the source of truth for active function names, argument contracts, localized descriptions, examples, result schemas, answer policies, and sensitivity policies.
- Active model-facing arguments are `modelCode` and `modelCodes`.
- Model-facing `modelId` and `model_id` arguments are forbidden.
- PendingActionState is future infrastructure only.
- Legacy domain-agent routing was removed.
- Real write execution is disabled.

## Local Run Settings

Use `src/TILSOFTAI.Api/appsettings.Local.example.json` as the local template and create an untracked `src/TILSOFTAI.Api/appsettings.Local.json` for machine-specific values.

Local development SQL shape:

```text
Server=localhost;Database=TILSOFTAI;User Id=sa;Password=123;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True
```

This credential is for local development only. Do not commit real local secrets and do not use this connection string as a production-safe setting.

Required local routing posture:

```json
{
  "AiRouting": {
    "MicrosoftAgentFrameworkRoutingEnabled": true,
    "UseOfficialMicrosoftAgentFramework": true,
    "Provider": "OpenAiCompatibleLocal",
    "AllowedDomains": [ "model" ],
    "MaxCandidateTools": 6,
    "ToolCallingRequired": true,
    "FallbackToLegacyPipeline": false
  },
  "Answering": {
    "DefaultMode": "Structured"
  }
}
```

## Local SQL Migration

The current local migration set lives in `sql/current/` and is executed in filename order by:

```powershell
./tools/sql/migrate-local-tilsoftai.ps1 -Server "localhost" -Database "TILSOFTAI" -User "sa" -Password "123"
```

or:

```bash
./tools/sql/migrate-local-tilsoftai.sh --server localhost --database TILSOFTAI --user sa --password 123
```

The migration creates the local database if it is missing, installs TILSOFTAI-owned framework objects, installs the read-only `ai_model_*` stored procedures, and runs `999_validate_model_runtime.sql`. Cleanup scripts are scoped to project-owned framework objects and must not drop unrelated ERP source tables.

The SQL-backed catalog validator is:

```text
sql/current/997_validate_capability_catalog.sql
```

It verifies the active runtime catalog has exactly six enabled `model` capabilities, no enabled non-model capabilities, unique function names, no model-facing `modelId` or `model_id` arguments, valid JSON metadata, result schemas, answer policies, and resolvable stored procedures.

Required model procedures:

- `dbo.ai_model_count`
- `dbo.ai_model_get_overview`
- `dbo.ai_model_get_pieces`
- `dbo.ai_model_get_materials`
- `dbo.ai_model_compare`
- `dbo.ai_model_get_packaging`

## Build And Test

```bash
dotnet build
dotnet test
```

## API Smoke Tests

Start the API with the local settings profile, then call the OpenAI-compatible chat surface. Use the actual route configured by the API host.

Structured mode example:

```bash
curl -s http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d "{\"model\":\"local-tool-model\",\"messages\":[{\"role\":\"user\",\"content\":\"Co bao nhieu model?\"}],\"metadata\":{\"answerMode\":\"Structured\"}}"
```

RawJson mode example:

```bash
curl -s http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d "{\"model\":\"local-tool-model\",\"messages\":[{\"role\":\"user\",\"content\":\"Show materials for model ABC\"}],\"metadata\":{\"answerMode\":\"RawJson\"}}"
```

## Model Domain Acceptance Prompts

Replace `ABC` and `XYZ` with model codes present in the local database:

1. `Co bao nhieu model?`
2. `Cho toi xem thong tin model ABC`
3. `Model ABC gom nhung piece nao?`
4. `Show materials for model ABC`
5. `So sanh model ABC va XYZ`
6. `Cho toi xem thong tin model`

Expected behavior:

- The first five prompts execute model-only tools through SQL stored procedures.
- The missing-code prompt returns a follow-up answer and does not call SQL.
- No non-model tools are advertised.
- Legacy fallback is not used.
- RawJson returns the tool envelope without a final LLM summary.
- Structured returns text, blocks, tables when appropriate, and provenance.

## Known Limitations Before More Domains

- Only the `model` domain is active.
- Catalog reload is opt-in and local/development-only by default. `CatalogReload:Enabled` is `false` in base settings and `true` in Development.
- Tool-calling quality depends on the selected local model supporting reliable function calls.
- Real ERP model data should be used when available; seeded model data must be labeled as test-only data.
- Write approval state exists for future confirmation flows, but real write execution remains disabled.
- Vector search and embedding knowledge-base work are outside the active runtime scope.
- Additional domains should wait until the model E2E report and tuning backlog show stable routing, argument binding, SQL execution, observability, and answer composition.

## Documentation

- `docs/reports/model_e2e_runtime_test_report.md`
- `docs/reports/model_e2e_framework_tuning_backlog.md`
- `docs/architecture/sql_backed_capability_catalog.md`
- `docs/architecture_v3.md`
- `docs/operational_runtime_observability.md`
- `docs/runtime_readiness.md`
- `docs/sql_compatibility_observability_runbook.md`
- `docs/platform_catalog_governance.md`
- `docs/catalog_control_plane_runbook.md`

## License

Internal / proprietary.
