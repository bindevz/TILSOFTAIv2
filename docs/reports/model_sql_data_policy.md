# Model SQL Data Policy

## Scope

This policy applies to the local development database used by Sprint 41 SQL migration diagnostics:

```text
Server=localhost;Database=TILSOFTAI;User Id=sa;Password=123;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True
```

This is local test configuration only. It is not production configuration and must not be treated as production auth, production tenancy, or a production data policy.

## Data Origin

Local smoke tests use seeded test data, not production ERP model data. The optional seed is maintained in:

```text
sql/current/005_seed_model_reference_data_optional.sql
```

Seeded rows are clearly marked with this description:

```text
test-only seeded model data; not ERP production data
```

If real ERP model data is later imported into the same local database, developers must keep it distinct from the `TenantId = default` smoke-test seed rows and must not use this seed as evidence of production ERP correctness.

## Model Source

The local model runtime reads from these framework-owned tables and view:

```text
dbo.Model
dbo.ModelPiece
dbo.Material
dbo.ModelMaterial
dbo.ModelPackagingOption
dbo.vw_ModelSemantic
```

The read-only model procedures are:

```text
dbo.ai_model_count
dbo.ai_model_get_overview
dbo.ai_model_get_pieces
dbo.ai_model_get_materials
dbo.ai_model_compare
dbo.ai_model_get_packaging
```

The capability metadata and routing trace tables under the `ai` schema support local routing diagnostics. They are not the SQL-backed catalog source of truth for production; that conversion is out of scope until Sprint 43.

## Smoke-Test Model Codes

The following `TenantId = default` model codes are safe for local smoke tests:

```text
ABC
XYZ
SET-DINING-001
```

Recommended smoke-test calls:

```text
dbo.ai_model_count @TenantId = N'default', @ArgsJson = N'{}'
dbo.ai_model_get_overview @TenantId = N'default', @ArgsJson = N'{"modelCode":"ABC"}'
dbo.ai_model_get_pieces @TenantId = N'default', @ArgsJson = N'{"modelCode":"SET-DINING-001"}'
dbo.ai_model_get_materials @TenantId = N'default', @ArgsJson = N'{"modelCode":"ABC"}'
dbo.ai_model_compare @TenantId = N'default', @ArgsJson = N'{"modelCodes":["ABC","XYZ"]}'
dbo.ai_model_get_packaging @TenantId = N'default', @ArgsJson = N'{"modelCode":"ABC"}'
```

## Refresh Procedure

To refresh local model seed data, run the local migration:

```powershell
./tools/sql/migrate-local-tilsoftai.ps1 -Server "localhost" -Database "TILSOFTAI" -User "sa" -Password "123"
```

or:

```bash
./tools/sql/migrate-local-tilsoftai.sh --server localhost --database TILSOFTAI --user sa --password 123
```

The seed script uses guarded upserts for the known smoke-test rows. Re-running migration should not duplicate seed rows.

## Avoiding Production Confusion

Use these rules when debugging local SQL:

```text
- Treat TenantId = default and model codes ABC, XYZ, SET-DINING-001 as local seeded test data.
- Do not infer production ERP data quality from local seed rows.
- Do not add new business domains to the seed during Sprint 41.
- Do not use this local connection string in production configuration.
- Keep real ERP imports in explicit tenant scopes and document their source separately.
- Use sql/current/998_validate_schema_contract.sql for framework schema drift.
- Use sql/current/999_validate_model_runtime.sql for local model runtime smoke validation.
```
