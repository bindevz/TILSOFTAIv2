# SQL-Backed Capability Catalog

Generated: 2026-04-29

## Runtime Source Of Truth

The active capability catalog is stored in SQL. Production runtime must not register static model capability descriptors from code.

Runtime ownership:

- Schema: `sql/current/002_core_tables.sql`
- Seed: `sql/current/008_seed_model_capability_catalog.sql`
- Validation: `sql/current/997_validate_capability_catalog.sql`
- Repository: `src/TILSOFTAI.Infrastructure/SemanticSql/SqlCapabilityCatalogRepository.cs`
- DI entry point: `src/TILSOFTAI.Api/Extensions/AddTilsoftAiSqlExtensions.cs`

`SqlCapabilityCatalogRepository` implements:

- `ICapabilityRegistry`
- `ICapabilityMetadataRepository`
- `ISqlBackedCapabilityCatalog`
- `ICapabilityCatalogReloader`

## Active Catalog

The active runtime catalog has exactly six enabled `model` capabilities:

- `model.count`
- `model.overview.by-code`
- `model.pieces.by-code`
- `model.materials.by-code`
- `model.compare`
- `model.packaging.by-code`

No enabled non-model capabilities are allowed.

Model-facing argument names are intentionally narrow:

- `modelCode`
- `modelCodes`
- optional `season`

`modelId` and `model_id` are forbidden model-facing arguments. Hidden database identifiers can still appear in result schemas or sensitivity policies when they are not exposed to the model.

## Model-Facing Metadata

Tool names, descriptions, aliases, examples, argument descriptions, clarification questions, result schemas, answer policies, and sensitivity policies are SQL-seeded.

The tool-description builder renders SQL metadata into official Agent Framework function descriptions without exposing stored procedure names to the model.

## Answer Composition

`CapabilityExecutionFacade` reads catalog metadata and passes result schema, answer policy, and sensitivity policy to `AnswerComposer`.

`AnswerComposer` is responsible for:

- RawJson envelopes.
- Structured answer blocks.
- SQL result schema labels and visibility.
- Row limits from answer policy.
- Hidden and masked columns from sensitivity policy.
- Follow-up and no-data behavior.

## Reload And Failure Behavior

Catalog data is cached in memory. Initial load failure fails closed by returning no active catalog. Reload failure preserves the last-known-good catalog.

Manual reload endpoint:

```text
POST /api/platform-catalog/capabilities/reload
```

The endpoint is available only when `CatalogReload:Enabled` is true and the host environment is Development or Local.

## Required Verification

Run before and after catalog changes:

```bash
dotnet build
dotnet test
```

Run against local SQL when SQL Server is available:

```powershell
./tools/sql/migrate-local-tilsoftai.ps1 -Server "localhost" -Database "TILSOFTAI" -User "sa" -Password "123"
```

The migration sequence should run `997_validate_capability_catalog.sql` and `999_validate_model_runtime.sql`.
