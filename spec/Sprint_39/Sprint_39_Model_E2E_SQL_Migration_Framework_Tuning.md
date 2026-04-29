# Sprint 39 — Model E2E Runtime Proof, SQL Migration, and Framework Tuning Report

## Goal

Move the project from a cleaned **Agent Framework Core** into a verified, executable runtime using the read-only `model` domain as the acceptance domain.

This sprint focuses on:

1. Updating `README.md` to match the new architecture.
2. Splitting `AddTilsoftAiExtensions.cs` into maintainable API extension files.
3. Migrating the required SQL framework objects into a real local SQL Server database.
4. Testing the API against real data through the `model` domain.
5. Producing a test report that identifies concrete changes needed to optimize the Agent Framework Core before adding more domains.

Do not add new business domains. Do not enable real write execution. Do not add vector/embedding KB work in this sprint.

---

## Target Runtime

```text
API
  -> SupervisorRuntime
  -> OfficialAgentToolRouter
  -> OfficialMicrosoftAgentRuntime
  -> official Microsoft Agent Framework AIAgent
  -> model-only AIFunction tools
  -> CapabilityExecutionFacade
  -> SQL read-only ai_model_* stored procedures
  -> AnswerComposer
  -> API response
```

---

## Local SQL Server Target

Use this local development database connection for SQL migration and test execution:

```text
Server: localhost
Database: TILSOFTAI
User: sa
Password: 123
TrustServerCertificate: true
```

Connection string:

```text
Server=localhost;Database=TILSOFTAI;User Id=sa;Password=123;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True
```

This credential is for local development only. Do not hardcode it into production settings.

---

## Non-Negotiable Rules

1. Active runtime remains official Microsoft Agent Framework.
2. Active domain remains `model` only.
3. Real write execution remains disabled.
4. Do not reintroduce keyword intent classifiers or legacy domain agents.
5. Do not advertise non-model tools.
6. Do not ask the model to invent database IDs.
7. Model-facing arguments must remain `modelCode` and `modelCodes`.
8. `CapabilityExecutionFacade` remains the execution boundary.
9. `AnswerComposer` remains the response boundary.
10. SQL migration must not drop unrelated ERP source tables.

---

## Phase 0 — Preflight

Run:

```bash
dotnet build
dotnet test
```

Inventory current SQL and API state:

```bash
find sql -maxdepth 3 -type f | sort
rg "AddTilsoftAiExtensions" src/TILSOFTAI.Api
rg "IIntentClassifier|KeywordIntentClassifier|AccountingAgent|WarehouseAgent|StructuredCapabilityResolver" src tests
rg "modelId|model_id" src catalog sql tests
```

Expected:

```text
- Build and tests pass before changes, or failures are documented.
- No active legacy routing types are found.
- No active model-facing tool contract uses modelId/model_id.
```

---

## Phase 1 — Update README to Current Architecture

### Goal

Replace old runtime documentation with the current Agent Framework Core architecture.

### Required Changes

Update `README.md` so it no longer describes:

```text
IIntentClassifier
CapabilityRequestHint
IAgentRegistry
WarehouseAgent
AccountingAgent
GeneralChatAgent
StructuredCapabilityResolver
WarehouseCapabilities
AccountingCapabilities
```

Replace the runtime section with:

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

State clearly:

```text
- Active runtime is model-only.
- Active capabilities are read-only model capabilities.
- Write execution is disabled.
- PendingActionState is future infrastructure only.
- Legacy domain-agent routing was removed.
```

### README Must Include

```text
1. Current architecture diagram.
2. Local run settings summary.
3. Local SQL migration summary.
4. API smoke-test commands.
5. Model domain acceptance prompts.
6. Known limitations before adding more domains.
```

### Acceptance Criteria

```text
- README does not mention removed legacy runtime as active architecture.
- README tells a developer how to run the model-only runtime locally.
- README includes the local SQL connection shape without presenting it as production-safe.
```

---

## Phase 2 — Split `AddTilsoftAiExtensions.cs`

### Goal

Make API composition readable and maintainable.

### Required Refactor

Split:

```text
src/TILSOFTAI.Api/Extensions/AddTilsoftAiExtensions.cs
```

Into focused files:

```text
src/TILSOFTAI.Api/Extensions/AddTilsoftAiExtensions.cs
src/TILSOFTAI.Api/Extensions/AddTilsoftAiAuthExtensions.cs
src/TILSOFTAI.Api/Extensions/AddTilsoftAiHealthExtensions.cs
src/TILSOFTAI.Api/Extensions/AddTilsoftAiLocalAiExtensions.cs
src/TILSOFTAI.Api/Extensions/AddTilsoftAiSqlExtensions.cs
src/TILSOFTAI.Api/Extensions/AddTilsoftAiTelemetryExtensions.cs
src/TILSOFTAI.Api/Extensions/AddTilsoftAiCorsExtensions.cs
src/TILSOFTAI.Api/Extensions/AddTilsoftAiCachingExtensions.cs
```

Keep `AddTilsoftAiExtensions.cs` as the high-level composition entry point only.

Target shape:

```csharp
public static IServiceCollection AddTilsoftAi(
    this IServiceCollection services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    services.AddTilsoftAiOptions(configuration);
    services.AddTilsoftAiAuth(configuration, environment);
    services.AddTilsoftAiCors(configuration, environment);
    services.AddTilsoftAiTelemetry(configuration);
    services.AddTilsoftAiSql(configuration);
    services.AddTilsoftAiCaching(configuration);
    services.AddTilsoftAiLocalAi(configuration);
    services.AddTilsoftAiHealthChecks(configuration);
    services.AddTilsoftAiOrchestration(configuration);

    return services;
}
```

### Rules

```text
- Do not change runtime behavior while splitting.
- Do not duplicate option binding.
- Do not create circular extension dependencies.
- Keep provider selection behavior unchanged unless required for local model E2E test.
```

### Acceptance Criteria

```text
- Main AddTilsoftAiExtensions file is short and readable.
- Each new extension file has one responsibility.
- dotnet build passes.
- Existing tests pass.
```

---

## Phase 3 — Clean and Rebuild SQL Migration Set

### Goal

Replace old/scattered SQL scripts with a clean local migration set for the current Agent Framework Core and model-only runtime.

### Required SQL Folder Structure

Create or normalize:

```text
sql/current/
  000_create_database.sql
  001_drop_old_tilsoftai_framework_objects.sql
  002_core_tables.sql
  003_action_request_tables.sql
  004_model_readonly_procs.sql
  005_seed_model_reference_data_optional.sql
  006_diagnostics.sql
  999_validate_model_runtime.sql

tools/sql/
  migrate-local-tilsoftai.ps1
  migrate-local-tilsoftai.sh
```

### What to Remove or Archive

Move old SQL scripts that are not part of current model-only Agent Framework Core to:

```text
sql/archive/
```

Archive or remove old folders if not needed by current runtime:

```text
sql/02_atomic
sql/02_capabilities
sql/04_diagnostics
sql/05_model
sql/06_semantic
sql/97_legacy_diagnostics
sql/98_dev_seeds
sql/99_seed
sql/ai
sql/patches
sql/sql-seeds
```

Do not delete permanently if the content is still useful; archive it under `sql/archive/YYYYMMDD_legacy_before_model_core/`.

### SQL Ownership Rule

The cleanup script may drop only TILSOFTAI-owned framework objects:

```text
dbo.app_actionrequest_*
dbo.ai_model_*
dbo.ai_* objects created by the project
app.* schema objects created by the project
ai.* schema objects created by the project
```

Do not drop unrelated ERP tables, ERP views, customer tables, production tables, or unknown objects.

### Required Objects

The local database must contain enough objects to run:

```text
model.count
model.overview.by-code
model.pieces.by-code
model.materials.by-code
model.compare
model.packaging.by-code
```

Required stored procedures:

```text
dbo.ai_model_count
dbo.ai_model_get_overview
dbo.ai_model_get_pieces
dbo.ai_model_get_materials
dbo.ai_model_compare
dbo.ai_model_get_packaging
```

Required pending action procedures/tables:

```text
dbo.app_actionrequest_create
dbo.app_actionrequest_get
dbo.app_actionrequest_get_active_for_conversation
dbo.app_actionrequest_confirm
dbo.app_actionrequest_reject
dbo.app_actionrequest_mark_executed
dbo.app_actionrequest_expire_old
```

If the project uses direct tables instead of stored procedures for action requests, document that decision and update `SqlActionRequestStore` accordingly.

### Migration Command

PowerShell script must support:

```powershell
./tools/sql/migrate-local-tilsoftai.ps1 `
  -Server "localhost" `
  -Database "TILSOFTAI" `
  -User "sa" `
  -Password "123"
```

Shell script must support:

```bash
./tools/sql/migrate-local-tilsoftai.sh   --server localhost   --database TILSOFTAI   --user sa   --password 123
```

Both scripts must:

```text
1. Create database if missing.
2. Execute scripts in sql/current order.
3. Stop on first error.
4. Print executed script names.
5. Run 999_validate_model_runtime.sql at the end.
```

### Validation SQL

`999_validate_model_runtime.sql` must check:

```text
- Database exists.
- Required ai_model_* stored procedures exist.
- Required app_actionrequest_* objects exist.
- Model test data exists or real model source tables/views are accessible.
- ai_model_count returns a valid result.
```

### Acceptance Criteria

```text
- A fresh local database can be created and migrated.
- Running migration twice is safe or clearly documented as reset-based.
- Required model stored procedures exist.
- Validation script passes.
- No unrelated ERP data is dropped.
```

---

## Phase 4 — Real Data Model Domain Test Setup

### Goal

Prepare the local database and API settings to test against real model data.

### Required Config

Create or update:

```text
src/TILSOFTAI.Api/appsettings.Local.example.json
```

Must include:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=TILSOFTAI;User Id=sa;Password=123;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True"
  },
  "AiRouting": {
    "MicrosoftAgentFrameworkRoutingEnabled": true,
    "UseOfficialMicrosoftAgentFramework": true,
    "Provider": "OpenAiCompatibleLocal",
    "AllowedDomains": [ "model" ],
    "MaxCandidateTools": 6,
    "ToolCallingRequired": true,
    "FallbackToLegacyPipeline": false
  },
  "LocalAi": {
    "BaseUrl": "http://localhost:11434/v1",
    "Model": "CHANGE_ME_TOOL_CALLING_MODEL",
    "ApiKeyEnvironmentVariable": "TILSOFTAI_LOCAL_AI_API_KEY",
    "TimeoutSeconds": 120
  },
  "Answering": {
    "DefaultMode": "Structured"
  }
}
```

The real `appsettings.Local.json` must remain gitignored.

### Real Data Requirement

Use real model data if available in the local database. If real model source tables are missing, add a minimal seeded model dataset for test only and clearly mark it:

```text
test-only seeded model data
not ERP production data
```

Do not fake API success without data.

### Acceptance Criteria

```text
- Local config can connect to TILSOFTAI database.
- API can execute ai_model_* procedures.
- Real or clearly marked test model data exists.
- Test report identifies whether data is real ERP data or test seeded data.
```

---

## Phase 5 — API E2E Test with Model Domain

### Goal

Run API tests through the full runtime path.

### Required Test Prompts

Run these prompts through the API:

```text
1. Có bao nhiêu model?
2. Cho tôi xem thông tin model ABC
3. Model ABC gồm những piece nào?
4. Show materials for model ABC
5. So sánh model ABC và XYZ
6. Cho tôi xem thông tin model
```

Replace `ABC` and `XYZ` with real model codes from the local database if needed.

### Expected Behavior

```text
Prompt 1:
  function = model.count
  SQL = dbo.ai_model_count

Prompt 2:
  function = model.overview.by-code
  args.modelCode = real model code
  SQL = dbo.ai_model_get_overview

Prompt 3:
  function = model.pieces.by-code
  args.modelCode = real model code
  SQL = dbo.ai_model_get_pieces

Prompt 4:
  function = model.materials.by-code
  args.modelCode = real model code
  SQL = dbo.ai_model_get_materials

Prompt 5:
  function = model.compare
  args.modelCodes contains two real model codes
  SQL = dbo.ai_model_compare

Prompt 6:
  missing modelCode
  answerType = follow_up
  no SQL execution
```

### Required Response Modes

Test both:

```text
RawJson
Structured
```

RawJson expectations:

```text
- no final LLM summary after SQL execution
- returns capability, procedure, arguments, rowCount, rows, executionMetadata, provenance
```

Structured expectations:

```text
- returns text + blocks + provenance
- returns table for row data when appropriate
- returns follow-up when required arguments are missing
```

### Required Logging

Each test request must log:

```text
correlationId
provider
model
allowedDomains
candidate capability keys
advertised tool names
selected function
arguments
capability key
stored procedure
rowCount
durationMs
answerMode
fallbackUsed=false
```

### Acceptance Criteria

```text
- At least 5 successful E2E model requests.
- Missing parameter prompt does not call SQL.
- No non-model tool is advertised.
- No legacy fallback is used.
- RawJson and Structured both work.
```

---

## Phase 6 — Write the Test Report

### Goal

Produce a concrete report that guides the next optimization sprint.

Create:

```text
docs/reports/model_e2e_runtime_test_report.md
```

### Required Report Sections

```markdown
# Model E2E Runtime Test Report

## Environment
- Commit:
- Date:
- API environment:
- SQL Server:
- Database:
- Local AI provider:
- Local AI model:
- Answer modes tested:

## Database Migration Result
- Migration script:
- Validation result:
- Required procedures present:
- Data source:
  - real ERP data / seeded test data
- Model codes used:

## API Test Matrix
| # | Prompt | Expected Tool | Actual Tool | Args | SQL Proc | Row Count | Answer Mode | Result |
|---|--------|---------------|-------------|------|----------|-----------|-------------|--------|

## Tool Calling Quality
- Correct tool selection count:
- Incorrect tool selection count:
- Argument binding issues:
- Missing parameter behavior:
- Non-model tool advertised? yes/no

## AnswerComposer Quality
- RawJson result quality:
- Structured result quality:
- Table quality:
- Follow-up quality:
- No-data behavior:
- Localization issues:

## Runtime Observability
- Correlation ID present:
- Candidate tools logged:
- Selected function logged:
- SQL procedure logged:
- Duration logged:
- Fallback used:

## Issues Found
| Severity | Area | Issue | Evidence | Proposed Fix |
|----------|------|-------|----------|--------------|

## CTO Recommendation
- Ready to add next domain? yes/no
- Required fixes before adding next domain:
- Suggested next domain:
- Suggested framework optimization:
```

### Acceptance Criteria

```text
- Report is based on real API calls.
- Report contains actual tool/function/proc evidence.
- Report identifies concrete tuning actions.
- Report clearly states whether framework is ready for next domain.
```

---

## Phase 7 — Framework Tuning Backlog

Based on the test report, create:

```text
docs/reports/model_e2e_framework_tuning_backlog.md
```

Classify findings:

```text
P0 - blocks model E2E runtime
P1 - required before next domain
P2 - quality improvement
P3 - future enhancement
```

Do not implement the full backlog in this sprint unless it is required to pass the model E2E test.

Potential tuning categories:

```text
- Tool descriptions too weak
- Argument schema mismatch
- Local AI tool-calling instability
- Candidate selection too broad
- AnswerComposer output unclear
- SQL result schema missing
- Logging insufficient
- Response mode ambiguity
```

---

## Phase 8 — Final Validation

Run:

```bash
dotnet build
dotnet test
```

Run SQL migration:

```powershell
./tools/sql/migrate-local-tilsoftai.ps1 -Server "localhost" -Database "TILSOFTAI" -User "sa" -Password "123"
```

or:

```bash
./tools/sql/migrate-local-tilsoftai.sh --server localhost --database TILSOFTAI --user sa --password 123
```

Run API smoke test and complete the report.

### Final Acceptance

This sprint is complete only when:

```text
- README reflects the current Agent Framework Core architecture.
- AddTilsoftAiExtensions is split into maintainable extension files.
- Local SQL migration creates/updates TILSOFTAI database.
- Required model ai_* procedures exist.
- API E2E test runs through the model domain.
- RawJson and Structured response modes are tested.
- Test report is written.
- Framework tuning backlog is written.
- No new domain is added.
- No real write execution is enabled.
```

---

## Do Not Do

```text
- Do not open Purchasing/Sales/Warehouse/Accounting.
- Do not add vector search.
- Do not implement broad semantic KB.
- Do not add real write tools.
- Do not reintroduce legacy routing.
- Do not treat seeded test data as real ERP data.
- Do not commit appsettings.Local.json.
```
