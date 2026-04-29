# Sprint 43 — SQL-Backed Catalog as Source of Truth

## Goal

Move capability metadata from code-first `ModelCapabilities` to a SQL-backed catalog source of truth.

This sprint prepares the framework for enterprise domain expansion. The active runtime remains model-only, but model capabilities must be loaded from SQL catalog metadata instead of hardcoded `ModelCapabilities`.

## CTO Decision

SQL-backed catalog becomes the source of truth for:

```text
capability key
domain
function name
adapter type
stored procedure
execution mode
argument contract
result schema
answer policy
sensitivity policy
multilingual descriptions
examples
```

After this sprint, `ModelCapabilities` must be removed from active runtime and then deleted or converted into test-only seed-generation code.

## Out of Scope

```text
- New business domains
- Real write execution
- Production auth
- Vector search as mandatory retrieval
- Multi-agent workflows
```

---

## Phase 0 — Preflight

Run:

```bash
dotnet build
dotnet test
```

Inventory model capability sources:

```bash
rg "ModelCapabilities|platform-catalog|CapabilityDescriptor|CapabilitySource|ICapabilityRegistry|InMemoryCapabilityRegistry|StaticCapabilitySource" src catalog sql tests
```

Document every current metadata source:

```text
1. ModelCapabilities.cs
2. catalog/platform-catalog.json
3. sql/current model proc metadata
4. docs/runbooks or tests
```

Acceptance:

```text
- All duplicated metadata sources are identified.
```

---

## Phase 1 — Design SQL Catalog Schema

Create or extend SQL schema under:

```text
sql/current/002_core_tables.sql
```

Required tables:

```sql
ai.Capability
ai.CapabilityText
ai.CapabilityArgument
ai.CapabilityArgumentText
ai.CapabilityResultSchema
ai.CapabilityAnswerPolicy
ai.CapabilitySensitivityPolicy
ai.CapabilityExample
```

Suggested columns:

```text
ai.Capability:
  CapabilityKey
  Domain
  FunctionName
  AdapterType
  Operation
  TargetSystemId
  StoredProcedureName
  ExecutionMode
  IsEnabled
  Version
  CreatedAtUtc
  UpdatedAtUtc

ai.CapabilityText:
  CapabilityKey
  Locale
  Name
  Description
  NegativeDescription
  AliasesJson
  IsDefault

ai.CapabilityArgument:
  CapabilityKey
  ArgumentName
  ModelFacingName
  SqlParameterName
  Type
  IsRequired
  EnumJson
  Format
  RegexPattern
  MinLength
  MaxLength
  MinValue
  MaxValue
  DefaultSource
  SortOrder

ai.CapabilityArgumentText:
  CapabilityKey
  ArgumentName
  Locale
  Description
  AliasesJson
  ClarificationQuestion

ai.CapabilityResultSchema:
  CapabilityKey
  SchemaJson

ai.CapabilityAnswerPolicy:
  CapabilityKey
  PolicyJson

ai.CapabilitySensitivityPolicy:
  CapabilityKey
  PolicyJson

ai.CapabilityExample:
  CapabilityKey
  Locale
  Utterance
  ArgumentsJson
  SortOrder
```

Use `NVARCHAR(MAX) CHECK (ISJSON(...)=1)` unless the project explicitly chooses SQL Server 2025 native `json` type. Keep provider compatibility in mind.

Acceptance:

```text
- SQL catalog schema exists.
- Schema is idempotent.
- JSON columns validate JSON.
```

---

## Phase 2 — Seed Model Capabilities into SQL Catalog

Create:

```text
sql/current/008_seed_model_capability_catalog.sql
```

Seed exactly these active model capabilities:

```text
model.count
model.overview.by-code
model.pieces.by-code
model.materials.by-code
model.compare
model.packaging.by-code
```

Seed:

```text
- function names
- stored procedure names
- model-facing argument names
- SQL parameter mapping
- vi-VN descriptions
- en-US descriptions
- examples
- result schemas
- answer policies
- sensitivity policies
```

Rules:

```text
- Seed must be idempotent.
- Use MERGE or upsert pattern.
- No non-model capabilities.
- No modelId/model_id model-facing arguments.
```

Acceptance:

```text
- SQL validation confirms 6 model capabilities are enabled.
- All six have function names.
- All required argument contracts exist.
- All have result schemas and answer policies.
```

---

## Phase 3 — Implement SQL Capability Catalog Repository

Create:

```text
src/TILSOFTAI.Infrastructure/SemanticSql/SqlCapabilityCatalogRepository.cs
src/TILSOFTAI.Orchestration/Capabilities/ISqlBackedCapabilityCatalog.cs
```

or equivalent naming.

Required methods:

```csharp
Task<IReadOnlyList<CapabilityDescriptor>> GetEnabledCapabilitiesAsync(
    IReadOnlySet<string> allowedDomains,
    CancellationToken ct);

Task<CapabilityDescriptor?> GetByKeyAsync(
    string capabilityKey,
    CancellationToken ct);

Task<IReadOnlyList<CapabilityDescriptor>> GetByDomainAsync(
    string domain,
    CancellationToken ct);
```

Mapping requirements:

```text
SQL rows -> CapabilityDescriptor
SQL arguments -> CapabilityArgumentContract
SQL result schema -> ResultSchema
SQL answer policy -> AnswerPolicy
SQL sensitivity policy -> SensitivityPolicy
SQL texts/examples -> tool descriptions
```

Acceptance:

```text
- Repository maps all six model capabilities correctly.
- Mapping tests compare SQL-backed descriptors to expected model descriptors.
```

---

## Phase 4 — Replace ModelCapabilities in Runtime

Remove active registration of:

```text
ModelCapabilities.All
StaticCapabilitySource("static-model", ModelCapabilities.All)
```

Replace with:

```text
SqlBackedCapabilitySource
SqlBackedCapabilityRegistry
```

or:

```text
ICapabilityRegistry backed by SQL catalog repository
```

Runtime rules:

```text
- Capability registry must load enabled model capabilities from SQL.
- Candidate selector must use SQL-backed descriptors.
- DynamicFunctionToolFactory must build AIFunction tools from SQL-backed descriptors.
- CapabilityExecutionFacade must execute SQL-backed descriptors.
```

Acceptance:

```text
- Runtime uses SQL catalog, not ModelCapabilities.
- API E2E model prompts still pass.
- Logs show descriptors loaded from SQL catalog.
```

---

## Phase 5 — Delete or Demote ModelCapabilities

After runtime is SQL-backed:

```text
src/TILSOFTAI.Orchestration/Capabilities/ModelCapabilities.cs
```

must be deleted or moved to test-only fixture.

Preferred:

```text
Delete ModelCapabilities.cs.
```

Allowed only if needed for tests:

```text
tests/TILSOFTAI.Tests/Fixtures/ModelCapabilityFixtures.cs
```

Rules:

```text
- Production runtime must not reference ModelCapabilities.
- Production DI must not register static model capabilities.
- Platform catalog JSON must not be runtime source of truth.
```

Acceptance:

```bash
rg "ModelCapabilities" src
```

Expected:

```text
No production references.
```

---

## Phase 6 — Dynamic Function Descriptions from SQL

Function descriptions must come from SQL catalog.

Tool description builder must use:

```text
CapabilityText.Description
CapabilityText.NegativeDescription
CapabilityText.AliasesJson
CapabilityExample.Utterance
ArgumentText.Description
ArgumentText.ClarificationQuestion
```

Rules:

```text
- Use request locale when available.
- Fallback to en-US or default locale.
- Do not hardcode production function descriptions in C#.
- Do not include stored procedure names in model-facing descriptions unless debug mode is enabled.
```

Acceptance:

```text
- Changing SQL description changes advertised function description without code rebuild.
- Tests verify vi-VN and en-US descriptions.
```

---

## Phase 7 — SQL-Backed Result Schema and Answer Policy

AnswerComposer must receive result schema and answer policy from SQL-backed capability metadata.

Rules:

```text
- Result schema must drive labels.
- Answer policy must drive max rows/table behavior.
- Sensitivity policy must drive masking.
- RawJson and Structured modes must both use SQL-backed metadata.
```

Acceptance:

```text
- Model output labels come from SQL catalog.
- Changing SQL result schema labels changes output labels after reload.
```

---

## Phase 8 — Catalog Reload and Cache Strategy

Enterprise runtime needs safe catalog loading.

Implement:

```text
- in-memory cache with TTL
- manual reload endpoint or service method for test/dev
- fail-closed if catalog cannot load and no previous valid cache exists
- continue with last known good cache if reload fails after initial load
```

Skip auth hardening for the endpoint if test-only, but guard it by environment/config:

```text
CatalogReload:Enabled = true only in Local/Development
```

Acceptance:

```text
- Catalog loads on startup or first request.
- Catalog reload works in local/test.
- Broken SQL catalog does not silently produce empty tool set.
```

---

## Phase 9 — SQL Catalog Validation

Create:

```text
sql/current/997_validate_capability_catalog.sql
```

Validate:

```text
- exactly 6 enabled model capabilities
- all function names unique
- all capability keys unique
- all stored procedures exist
- all required argument contracts exist
- all JSON metadata is valid
- no enabled non-model capabilities
- no model-facing modelId/model_id arguments
```

Run this in migration before model runtime validation.

Acceptance:

```text
- Bad catalog seed fails validation clearly.
```

---

## Phase 10 — Tests

Add/update tests:

```text
SqlBackedCatalog_LoadsModelCapabilities
SqlBackedCatalog_MapsArguments
SqlBackedCatalog_MapsResultSchema
SqlBackedCatalog_RejectsNonModelEnabledCapabilities
SqlBackedCatalog_NoModelIdArguments
DynamicFunctionToolFactory_UsesSqlDescriptions
AnswerComposer_UsesSqlResultSchema
Runtime_ModelE2E_UsesSqlBackedCatalog
Architecture_NoModelCapabilitiesInProduction
```

Opt-in SQL tests may require:

```text
TILSOFTAI_RUN_SQL_INTEGRATION_TESTS=true
```

Acceptance:

```text
- Unit tests pass without SQL.
- SQL integration tests pass when enabled.
- Architecture guard fails if ModelCapabilities returns to production runtime.
```

---

## Phase 11 — Documentation and Reports

Update:

```text
README.md
docs/reports/model_e2e_runtime_test_report.md
docs/reports/model_e2e_framework_tuning_backlog.md
```

Add:

```text
docs/architecture/sql_backed_capability_catalog.md
```

Document:

```text
- SQL catalog is source of truth.
- ModelCapabilities was removed.
- How to add a new capability through SQL.
- How to reload catalog in local/test.
- How SQL metadata becomes AIFunction tools.
```

---

## Final Validation

Run:

```bash
dotnet build
dotnet test
```

Run SQL migration:

```powershell
./tools/sql/migrate-local-tilsoftai.ps1 -Server "localhost" -Database "TILSOFTAI" -User "sa" -Password "123"
```

Run model E2E smoke again:

```text
1. Có bao nhiêu model?
2. Cho tôi xem thông tin model ABC
3. Model ABC gồm những piece nào?
4. Show materials for model ABC
5. So sánh model ABC và XYZ
6. Cho tôi xem thông tin model
```

Expected:

```text
- Runtime loads SQL-backed catalog.
- Only six model tools are advertised.
- Agent calls SQL-backed model functions.
- RawJson and Structured still work.
- Missing model code returns follow-up.
```

## Definition of Done

```text
- SQL catalog is source of truth.
- ModelCapabilities is removed from production runtime.
- All model capability metadata is seeded in SQL.
- Dynamic AIFunction tools are built from SQL-backed descriptors.
- AnswerComposer uses SQL-backed result schema/answer policy.
- Catalog validation exists.
- Catalog reload/cache exists for local/test.
- Model E2E smoke passes.
- No new business domain is added.
- Auth hardening is skipped.
```
