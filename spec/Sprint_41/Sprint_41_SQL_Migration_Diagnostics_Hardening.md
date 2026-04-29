# Sprint 41 — SQL Migration and Diagnostics Hardening

## Goal

Make the local SQL migration and runtime diagnostics reliable enough to support future domain expansion.

This sprint hardens SQL migration, validation, idempotency, and diagnostics. Do not add new business domains. Do not convert to SQL-backed catalog yet; that happens in Sprint 43.

## Local SQL Target

Use the local development database:

```text
Server: localhost
Database: TILSOFTAI
User: sa
Password: 123
```

Connection string:

```text
Server=localhost;Database=TILSOFTAI;User Id=sa;Password=123;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True
```

This is local test configuration only. Do not make this production configuration.

## Out of Scope

```text
- Production auth
- New business domains
- Real write execution
- SQL-backed capability catalog source-of-truth
- Vector search
```

---

## Phase 0 — Preflight

Run:

```bash
dotnet build
dotnet test
```

Inventory SQL:

```bash
find sql/current -maxdepth 1 -type f | sort
find tools/sql -maxdepth 1 -type f | sort
```

Expected files:

```text
sql/current/000_create_database.sql
sql/current/001_drop_old_tilsoftai_framework_objects.sql
sql/current/002_core_tables.sql
sql/current/003_action_request_tables.sql
sql/current/004_model_readonly_procs.sql
sql/current/005_seed_model_reference_data_optional.sql
sql/current/006_diagnostics.sql
sql/current/007_agent_semantic_trace_tables.sql
sql/current/999_validate_model_runtime.sql
tools/sql/migrate-local-tilsoftai.ps1
tools/sql/migrate-local-tilsoftai.sh
```

---

## Phase 1 — Make Migration Idempotent

Migration must support repeated local execution.

Rules:

```text
- Running migration twice must not duplicate seed data.
- Running migration twice must not fail because objects already exist.
- Drop script may drop only TILSOFTAI-owned framework objects.
- Do not drop unrelated ERP/customer/source tables.
```

Required hardening:

```text
- Use CREATE OR ALTER for stored procedures.
- Use IF NOT EXISTS for tables.
- Use MERGE or guarded INSERT for seed data.
- Use explicit object ownership checks before DROP.
- Keep reset-based behavior documented if the migration intentionally resets project-owned objects.
```

Add a test or script:

```text
tools/sql/test-local-migration-idempotency.ps1
tools/sql/test-local-migration-idempotency.sh
```

The test should:

```text
1. Run migration.
2. Run validation.
3. Run migration again.
4. Run validation again.
5. Verify seed row counts did not duplicate.
```

Acceptance:

```text
- Migration can run twice on localhost/TILSOFTAI.
- Validation passes after both runs.
```

---

## Phase 2 — Schema Drift Validation

Add SQL validation that catches drift between expected framework schema and actual DB.

Create:

```text
sql/current/998_validate_schema_contract.sql
```

Validate:

```text
- dbo.ai_model_count exists
- dbo.ai_model_get_overview exists
- dbo.ai_model_get_pieces exists
- dbo.ai_model_get_materials exists
- dbo.ai_model_compare exists
- dbo.ai_model_get_packaging exists
- action request table/procs exist
- tool routing trace table/procs exist
- required columns exist with expected names/types
```

Add a script command that runs:

```text
998_validate_schema_contract.sql
999_validate_model_runtime.sql
```

Acceptance:

```text
- Schema drift causes a clear SQL error.
- Validation output is readable.
```

---

## Phase 3 — Strengthen Model SQL Procedure Contracts

Review:

```text
sql/current/004_model_readonly_procs.sql
```

Each model proc must:

```text
- accept @TenantId and @ArgsJson
- parse modelCode or modelCodes from @ArgsJson
- validate required args
- return deterministic JSON envelope
- avoid dynamic SQL
- return no-data envelope when model does not exist
- avoid throwing for normal no-data cases
```

Expected output shape:

```json
{
  "meta": {
    "capabilityKey": "model.overview.by-code",
    "rowCount": 1,
    "status": "ok"
  },
  "columns": [
    { "name": "ModelCode", "label": "Model Code", "type": "string" }
  ],
  "rows": []
}
```

Acceptance:

```text
- All model procs return a consistent JSON envelope.
- Missing args produce validation metadata or controlled error.
- No model proc returns undocumented JSON shape.
```

---

## Phase 4 — Diagnostics Query Pack

Create:

```text
sql/current/006_diagnostics.sql
```

or extend it with diagnostics procedures/views:

```text
dbo.app_agent_trace_recent
dbo.app_agent_trace_by_correlation
dbo.app_agent_trace_failures
dbo.app_model_runtime_health
```

Diagnostics must support:

```text
- last N route traces
- find by correlationId
- failed route traces
- tool selection evidence
- selected function
- procedure name
- row count
- duration
```

Acceptance:

```text
- Given a correlationId from API logs, a developer can inspect SQL trace rows.
- Diagnostics do not expose sensitive row payloads unless explicitly allowed.
```

---

## Phase 5 — Local SQL Test Data Policy

Clarify whether model data is real ERP data or seeded test data.

Create/update:

```text
docs/reports/model_sql_data_policy.md
```

Must state:

```text
- Is test using real ERP model data or seeded data?
- Which table/view/proc is the model source?
- Which model codes are safe for smoke tests?
- How to refresh model seed data?
- How to avoid confusing seeded test data with production ERP data?
```

Acceptance:

```text
- Test data origin is documented.
- Smoke test model codes are documented.
```

---

## Phase 6 — SQL Contract Test from .NET

Add tests that call SQL procs against local SQL only when explicitly enabled.

Use environment flag:

```text
TILSOFTAI_RUN_SQL_INTEGRATION_TESTS=true
```

Tests:

```text
Sql_ModelCount_ReturnsEnvelope
Sql_ModelOverview_ByCode_ReturnsEnvelope
Sql_ModelPieces_ByCode_ReturnsEnvelope
Sql_ModelMaterials_ByCode_ReturnsEnvelope
Sql_ModelCompare_ByCodes_ReturnsEnvelope
Sql_ModelPackaging_ByCode_ReturnsEnvelope
Sql_MissingModelCode_ReturnsControlledValidation
```

Acceptance:

```text
- SQL integration tests are opt-in.
- Tests do not run in normal unit test mode unless enabled.
```

---

## Phase 7 — Final Validation

Run:

```bash
dotnet build
dotnet test
```

Run:

```powershell
./tools/sql/migrate-local-tilsoftai.ps1 -Server "localhost" -Database "TILSOFTAI" -User "sa" -Password "123"
./tools/sql/test-local-migration-idempotency.ps1 -Server "localhost" -Database "TILSOFTAI" -User "sa" -Password "123"
```

or Linux shell equivalents.

## Definition of Done

```text
- Migration is repeatable or explicitly reset-based and safe.
- Schema contract validation exists.
- Model SQL procs return consistent envelopes.
- Diagnostics query pack exists.
- SQL integration tests are available behind env flag.
- Test data policy is documented.
- No new business domain is added.
- Auth hardening is skipped.
```
