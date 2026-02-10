# TILSOFTAIv2 — Agent Remediation Instructions

**Document ID:** TILSOFTAI-REMED-2026-02-10
**Purpose:** Step-by-step instructions for AI Agent to fix all 11 audit issues
**Language:** English
**Principle:** ReAct + LLM decides, C# orchestrates only, SQL Server is the execution engine

---

## IMPORTANT: RULES FOR AGENT

1. **DO NOT invent code.** Every file path, class name, method name, and SQL object referenced here exists in the codebase. Verify by reading the file BEFORE editing.
2. **DO NOT change working logic.** If the audit says "this works correctly," do not touch it.
3. **DO NOT upgrade packages or frameworks.** The project runs on .NET 10. Do not change target framework.
4. **Use DELETE/REPLACE strategy.** When fixing a method, replace the entire method body — do not patch line-by-line.
5. **Run `dotnet build` after each pack.** If build fails, fix compilation errors before proceeding.
6. **SQL scripts must be idempotent.** Always use `IF OBJECT_ID(...) IS NULL` or `CREATE OR ALTER`.
7. **Keep backward compatibility.** Existing APIs, DTOs, and SQL tables must not break.
8. **Follow the pack order.** Packs are numbered by dependency. Do not skip ahead.

---

## PROJECT STRUCTURE REFERENCE

```
src/
├── TILSOFTAI.Api/
│   ├── Controllers/
│   │   ├── OpenAiChatCompletionsController.cs    ← P0-01, P0-05
│   │   ├── ChatController.cs                      ← P0-05
│   │   └── ModelsController.cs                    ← P0-05
│   ├── Extensions/
│   │   └── AddTilsoftAiExtensions.cs              ← DI wiring (P0-02, P1-01)
│   ├── Middlewares/
│   │   └── ExecutionContextMiddleware.cs           ← Auth context
│   ├── appsettings.json                            ← P0-04, P1-01, P2-02
│   └── appsettings.Sample.json                     ← P0-04 (delete)
├── TILSOFTAI.Orchestration/
│   ├── Pipeline/
│   │   ├── ChatPipeline.cs                         ← P0-01, P0-02 integration
│   │   └── ChatRequest.cs                          ← P0-01
│   ├── Prompting/
│   │   ├── PromptBuilder.cs                        ← P1-02
│   │   ├── ContextPackBudgeter.cs                  ← P0-03
│   │   ├── IContextPackProvider.cs                 ← Interface (do not modify)
│   │   └── CompositeContextPackProvider.cs          ← DI assembly
│   ├── Tools/
│   │   ├── IToolCatalogResolver.cs                 ← Interface (extend for scoping)
│   │   └── ToolGovernance.cs                        ← Do not modify
│   └── Modules/
│       └── (NEW) ModuleScopeResolver.cs             ← P0-02
├── TILSOFTAI.Infrastructure/
│   ├── Tools/
│   │   └── ToolCatalogSyncService.cs               ← P0-02 (add scoped method)
│   ├── Metadata/
│   │   └── MetadataDictionaryContextPackProvider.cs ← P0-02 (add scope filter)
│   └── Prompting/
│       └── ToolCatalogContextPackProvider.cs         ← P1-01
├── TILSOFTAI.Domain/
│   └── Configuration/                               ← Options classes
└── TILSOFTAI.Modules.Model/
    └── ModelModule.cs                               ← Tool definitions (reference only)

sql/
├── 01_core/001_tables_core.sql                      ← MetadataDictionary, ToolCatalog tables
├── 02_atomic/001_tables_catalog.sql                 ← DatasetCatalog, FieldCatalog, EntityGraphCatalog
├── 02_modules/model/003_sps_model.sql               ← ai_model_* SPs (P1-02 SQL changes)
└── 99_seed/                                          ← Seed scripts
```

---

## EXECUTION ORDER

```
PHASE 1 — Immediate Stabilization (Packs 1-3)
├── Pack 1: P0-04 — Remove hardcoded credentials
├── Pack 2: P0-05 — Secure AllowAnonymous endpoints
└── Pack 3: P0-01 — Fix BuildUserInput prompt concatenation

PHASE 2 — Token Scalability (Packs 4-7)
├── Pack 4: P0-02a — Create SQL scope tables + stored procedures
├── Pack 5: P0-02b — Implement ModuleScopeResolver in C#
├── Pack 6: P0-02c — Integrate scoped loading into ChatPipeline + DI
├── Pack 7: P0-03 — Priority-based ContextPackBudgeter

PHASE 3 — ReAct Quality (Packs 8-11)
├── Pack 8:  P1-01 — Enable ToolCatalogContextPack (AFTER scoping)
├── Pack 9:  P1-02 — Hallucination guard (prompt + SQL)
├── Pack 10: P1-03 — Seed Atomic catalogs
└── Pack 11: P0-01b — Full message history refactor (enterprise)

PHASE 4 — Observability (Packs 12-14)
├── Pack 12: P2-01 — Module scope audit logging
├── Pack 13: P2-02 — ReAct follow-up policy configuration
└── Pack 14: P2-03 — Scope fallback mechanism
```

---

---

# PACK 1: P0-04 — Remove Hardcoded Credentials

**Priority:** P0 CRITICAL
**Risk:** Security violation — plaintext password in source control
**Estimated time:** 15 minutes
**Files to modify:** 2 files, 1 file to delete

## 1.1 Context

The file `src/TILSOFTAI.Api/appsettings.json` contains:
```json
"ConnectionString": "Server=.;Database=TILSOFTAI;User ID=sa;Password=123;TrustServerCertificate=True;"
```

The file `src/TILSOFTAI.Api/appsettings.Sample.json` also contains the same hardcoded password.

The project already has a `Secrets` section in appsettings with `"Provider": "Environment"`, and there is an `ISecretProvider` interface. The infrastructure for environment-based secrets EXISTS but is not being used for the connection string.

## 1.2 Steps

### Step 1: Modify `src/TILSOFTAI.Api/appsettings.json`

Replace the `Sql.ConnectionString` value. Find:
```json
"ConnectionString": "Server=.;Database=TILSOFTAI;User ID=sa;Password=123;TrustServerCertificate=True;"
```

Replace with:
```json
"ConnectionString": ""
```

**WHY empty string:** The actual connection string MUST come from environment variable `TILSOFTAI_SQL__ConnectionString` or `SQL_CONNECTION_STRING`. The empty string forces the developer to configure it externally. The .NET configuration system will overlay environment variables on top of appsettings.json automatically via `builder.Configuration.AddEnvironmentVariables()`.

### Step 2: Delete `src/TILSOFTAI.Api/appsettings.Sample.json`

This file duplicates the hardcoded credentials. Delete it entirely.

### Step 3: Create `src/TILSOFTAI.Api/appsettings.Sample.README.md`

Create a new file with instructions for developers:

```markdown
# Configuration Setup

## SQL Connection String

Set via environment variable (recommended):
```bash
export TILSOFTAI_SQL__ConnectionString="Server=YOUR_SERVER;Database=TILSOFTAI;User ID=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
```

Or via dotnet user-secrets (development):
```bash
dotnet user-secrets set "Sql:ConnectionString" "Server=.;Database=TILSOFTAI;User ID=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
```

NEVER commit passwords to appsettings.json or any file in source control.
```

### Step 4: Verify Program.cs has environment variable support

Open `src/TILSOFTAI.Api/Program.cs` and check that `builder.Configuration.AddEnvironmentVariables()` is called. If it exists, no change needed. If it does NOT exist, add it after `var builder = WebApplication.CreateBuilder(args);`:

```csharp
builder.Configuration.AddEnvironmentVariables("TILSOFTAI_");
```

## 1.3 Verification

```bash
# Must return 0 results
grep -r "Password=123" --include="*.json" --include="*.cs" src/
grep -r "Password=123" --include="*.json" --include="*.cs" tools/
```

## 1.4 Acceptance Criteria

- **AC-06:** `grep -r "Password=" --include="*.json" src/` returns 0 matches with actual passwords
- `appsettings.Sample.json` no longer exists
- Application starts with connection string from environment variable

---

---

# PACK 2: P0-05 — Secure AllowAnonymous Endpoints

**Priority:** P0 CRITICAL
**Risk:** Anyone can call chat API without authentication
**Estimated time:** 20 minutes
**Files to modify:** 3 controllers

## 2.1 Context

Three controllers use `[AllowAnonymous]` which overrides the `FallbackPolicy = RequireAuthenticatedUser` configured in Program.cs:

| File | Current | Line |
|------|---------|------|
| `OpenAiChatCompletionsController.cs` | `[AllowAnonymous]` on class | Line 17 |
| `ChatController.cs` | `[AllowAnonymous]` on class (with `//[Authorize]` commented out) | Line 18 |
| `ModelsController.cs` | `[AllowAnonymous]` on class | Line 10 |

The `ExecutionContextMiddleware` already handles both authenticated and anonymous cases. When `[AllowAnonymous]` is present, the middleware creates a guest context. When `[Authorize]` is present, it extracts JWT claims. Removing `[AllowAnonymous]` means the FallbackPolicy kicks in and requires a JWT token.

**IMPORTANT:** `ModelsController` (GET `/v1/models`) is an OpenAI-compatible endpoint that some clients call without auth to discover available models. This should remain publicly accessible. Only the chat endpoints need securing.

## 2.2 Steps

### Step 1: Fix `OpenAiChatCompletionsController.cs`

**File:** `src/TILSOFTAI.Api/Controllers/OpenAiChatCompletionsController.cs`

Find at the top of the class (around line 14-17):
```csharp
[ApiController]
[Route("v1/chat/completions")]
[AllowAnonymous]
public sealed class OpenAiChatCompletionsController : ControllerBase
```

Replace with:
```csharp
[ApiController]
[Route("v1/chat/completions")]
[Authorize]
public sealed class OpenAiChatCompletionsController : ControllerBase
```

Also REMOVE the guest context fallback inside the `Post` method. Find (around line 68-74):
```csharp
var context = _contextAccessor.Current ?? new TilsoftExecutionContext
{
    TenantId = "guest",
    UserId = "anonymous", 
    Roles = new[] { "guest" },
    CorrelationId = Guid.NewGuid().ToString("N")
};
```

Replace with:
```csharp
var context = _contextAccessor.Current 
    ?? throw new InvalidOperationException("ExecutionContext is required. Ensure authentication middleware is configured.");
```

**WHY:** With `[Authorize]`, the middleware will always create a context from JWT claims. The null fallback to "guest" was a workaround for anonymous access and is no longer needed. Keeping it would silently mask auth failures.

### Step 2: Fix `ChatController.cs`

**File:** `src/TILSOFTAI.Api/Controllers/ChatController.cs`

Find (around line 14-18):
```csharp
[ApiController]
[Route("api/chats")]
//[Authorize]
[AllowAnonymous]
public sealed class ChatController : ControllerBase
```

Replace with:
```csharp
[ApiController]
[Route("api/chats")]
[Authorize]
public sealed class ChatController : ControllerBase
```

### Step 3: Keep `ModelsController.cs` as-is (NO CHANGE)

**File:** `src/TILSOFTAI.Api/Controllers/ModelsController.cs`

**DO NOT MODIFY.** The `/v1/models` endpoint must remain `[AllowAnonymous]` because:
- OpenAI-compatible clients (Open WebUI, etc.) call this endpoint to discover models BEFORE authenticating
- This endpoint returns only public info (model name, provider)
- No sensitive data is exposed

### Step 4: Add `using` directive if missing

In `OpenAiChatCompletionsController.cs` and `ChatController.cs`, ensure this using exists:
```csharp
using Microsoft.AspNetCore.Authorization;
```
(It already exists in both files because `AllowAnonymous` is from the same namespace — just verify.)

## 2.3 Verification

```bash
# Build must pass
dotnet build src/TILSOFTAI.Api/

# Verify AllowAnonymous removed from chat controllers
grep -n "AllowAnonymous" src/TILSOFTAI.Api/Controllers/OpenAiChatCompletionsController.cs
# Expected: 0 results

grep -n "AllowAnonymous" src/TILSOFTAI.Api/Controllers/ChatController.cs
# Expected: 0 results

# Verify ModelsController still has AllowAnonymous
grep -n "AllowAnonymous" src/TILSOFTAI.Api/Controllers/ModelsController.cs
# Expected: 1 result (this is correct)
```

## 2.4 Acceptance Criteria

- **AC-07:** Calling POST `/v1/chat/completions` without JWT returns 401 Unauthorized
- **AC-07:** Calling POST `/api/chats` without JWT returns 401 Unauthorized
- GET `/v1/models` still works without auth (returns model list)
- Application compiles successfully

---

---

# PACK 3: P0-01 — Fix BuildUserInput Prompt Concatenation

**Priority:** P0 CRITICAL
**Risk:** LLM receives ALL user messages concatenated, ignores assistant turns → multi-turn broken
**Estimated time:** 15 minutes
**Files to modify:** 1 file

## 3.1 Context

**File:** `src/TILSOFTAI.Api/Controllers/OpenAiChatCompletionsController.cs`
**Method:** `BuildUserInput()` (line 157-174)

**Current behavior:** Filters ALL messages where `Role == "user"`, joins them with `\n`. If user sent 3 messages in a conversation, the LLM receives all 3 questions concatenated WITHOUT seeing the assistant's answers in between. This causes over-answering and breaks multi-turn reasoning.

**Target behavior:** Return ONLY the last user message. The full message history refactor (Pack 11) will handle multi-turn properly later. This pack is the fast-fix.

**IMPORTANT:** This is a fast fix. The enterprise-grade message history solution is in Pack 11. This pack only changes `BuildUserInput()` to return the last user message instead of concatenating all.

## 3.2 Steps

### Step 1: Replace `BuildUserInput` method

**File:** `src/TILSOFTAI.Api/Controllers/OpenAiChatCompletionsController.cs`

Find the entire `BuildUserInput` method (around line 157-174):
```csharp
private static string BuildUserInput(IReadOnlyList<OpenAiChatMessage> messages)
{
    if (messages is null || messages.Count == 0)
    {
        throw new ArgumentException("messages is required.");
    }

    var last = messages[^1];
    if (!string.Equals(last.Role, "user", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("The last message must be a user message.");
    }

    var userMessages = messages
        .Where(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
        .Select(message => message.Content ?? string.Empty)
        .ToList();

    if (userMessages.Count == 0)
    {
        throw new ArgumentException("At least one user message is required.");
    }

    return string.Join("\n", userMessages);
}
```

Replace with:
```csharp
private static string BuildUserInput(IReadOnlyList<OpenAiChatMessage> messages)
{
    if (messages is null || messages.Count == 0)
    {
        throw new ArgumentException("messages is required.");
    }

    var last = messages[^1];
    if (!string.Equals(last.Role, "user", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("The last message must be a user message.");
    }

    // Return ONLY the last user message.
    // Previously: all user messages were concatenated with \n, ignoring assistant turns.
    // This caused the LLM to receive multiple questions without seeing prior answers,
    // leading to over-answering and broken multi-turn context.
    return last.Content ?? string.Empty;
}
```

**DO NOT modify any other method in this controller in this pack.**

## 3.3 Verification

```bash
dotnet build src/TILSOFTAI.Api/
```

## 3.4 Acceptance Criteria

- When client sends `messages: [{role:"user", content:"Q1"}, {role:"assistant", content:"A1"}, {role:"user", content:"Q2"}]`, only `"Q2"` reaches the ChatPipeline
- Build passes
- Existing single-message requests work identically (no regression)

---

---

# PACK 4: P0-02a — SQL Scope Tables and Stored Procedures

**Priority:** P0 CRITICAL
**Risk:** Token explosion — all tools/metadata loaded globally
**Estimated time:** 30 minutes
**Files to create:** 1 new SQL file

## 4.1 Context

Currently, `ToolCatalogSyncService.GetResolvedToolsAsync()` loads ALL tools from SQL, and `MetadataDictionaryContextPackProvider` loads the ENTIRE metadata dictionary. When modules grow (model, analytics, platform, core...), the token count grows linearly and critical tools can be dropped by the budgeter.

This pack creates the SQL-side infrastructure: new tables for module scoping and stored procedures for scoped loading. The C# integration follows in Packs 5-6.

**IMPORTANT RULES:**
- All new tables are ADDITIVE — do not ALTER any existing table
- The existing `ToolCatalog` and `MetadataDictionary` tables are untouched
- New tables reference existing ones via foreign keys
- All scripts must be idempotent (re-runnable)

## 4.2 Existing Schema Reference

Before creating new tables, understand what exists:

**dbo.ToolCatalog** (existing, DO NOT modify):
```
ToolName        nvarchar(200)   PK
SpName          nvarchar(200)
IsEnabled       bit
RequiredRoles   nvarchar(1000)
JsonSchema      nvarchar(max)
Instruction     nvarchar(max)
Description     nvarchar(2000)
```

**dbo.MetadataDictionary** (existing, DO NOT modify):
```
Id              bigint IDENTITY PK
[Key]           nvarchar(200)
TenantId        nvarchar(50)
Language        nvarchar(10)
DisplayName     nvarchar(200)
Description     nvarchar(2000)
Unit            nvarchar(50)
Examples        nvarchar(2000)
```

**dbo.ToolCatalogTranslation** (existing, used by `app_toolcatalog_list`):
```
ToolName        nvarchar(200)
Language        nvarchar(10)
JsonSchema      nvarchar(max)
Instruction     nvarchar(max)
Description     nvarchar(2000)
```

## 4.3 Steps

### Step 1: Create new SQL migration file

**Create file:** `sql/01_core/070_tables_module_scope.sql`

```sql
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- PACK 4: Module Scope Infrastructure
-- Creates: ModuleCatalog, ToolCatalogScope, MetadataDictionaryScope
-- Purpose: Enable per-module tool and metadata scoping
-- Idempotent: Safe to re-run
-- =============================================

-- 1. ModuleCatalog: Registry of available modules with LLM-readable instructions
IF OBJECT_ID('dbo.ModuleCatalog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ModuleCatalog
    (
        ModuleKey       nvarchar(50)    NOT NULL,
        AppKey          nvarchar(50)    NOT NULL DEFAULT '',
        IsEnabled       bit             NOT NULL DEFAULT 1,
        Instruction     nvarchar(500)   NOT NULL,
        Priority        int             NOT NULL DEFAULT 100,
        TenantId        nvarchar(50)    NULL,
        Language        nvarchar(10)    NOT NULL DEFAULT 'en',
        CONSTRAINT PK_ModuleCatalog PRIMARY KEY (ModuleKey, AppKey, Language)
    );

    PRINT 'Created table: dbo.ModuleCatalog';
END;
GO

-- 2. ToolCatalogScope: Maps tools to modules (many-to-many)
IF OBJECT_ID('dbo.ToolCatalogScope', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ToolCatalogScope
    (
        ToolName    nvarchar(200)   NOT NULL,
        ModuleKey   nvarchar(50)    NOT NULL,
        AppKey      nvarchar(50)    NOT NULL DEFAULT '',
        TenantId    nvarchar(50)    NULL,
        IsEnabled   bit             NOT NULL DEFAULT 1,
        CONSTRAINT PK_ToolCatalogScope PRIMARY KEY (ToolName, ModuleKey, AppKey),
        CONSTRAINT FK_ToolCatalogScope_Tool FOREIGN KEY (ToolName)
            REFERENCES dbo.ToolCatalog (ToolName)
    );

    PRINT 'Created table: dbo.ToolCatalogScope';
END;
GO

-- 3. MetadataDictionaryScope: Maps metadata keys to modules
IF OBJECT_ID('dbo.MetadataDictionaryScope', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MetadataDictionaryScope
    (
        MetadataKey nvarchar(200)   NOT NULL,
        ModuleKey   nvarchar(50)    NOT NULL,
        AppKey      nvarchar(50)    NOT NULL DEFAULT '',
        TenantId    nvarchar(50)    NULL,
        IsEnabled   bit             NOT NULL DEFAULT 1,
        CONSTRAINT PK_MetadataDictionaryScope PRIMARY KEY (MetadataKey, ModuleKey, AppKey)
    );

    PRINT 'Created table: dbo.MetadataDictionaryScope';
END;
GO
```

### Step 2: Create stored procedures

**Create file:** `sql/01_core/071_sps_module_scope.sql`

```sql
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- SP: app_modulecatalog_list
-- Returns available modules for LLM routing
-- =============================================
CREATE OR ALTER PROCEDURE dbo.app_modulecatalog_list
    @TenantId       nvarchar(50) = NULL,
    @Language        nvarchar(10) = 'en'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ModuleKey, AppKey, Instruction, Priority
    FROM dbo.ModuleCatalog
    WHERE IsEnabled = 1
      AND Language = @Language
      AND (TenantId IS NULL OR TenantId = @TenantId)
    ORDER BY Priority ASC;
END;
GO

-- =============================================
-- SP: app_toolcatalog_list_scoped
-- Returns tools filtered by selected modules
-- Always includes 'platform' module tools (action_request_write, diagnostics_run, etc.)
-- @ModulesJson: JSON array, e.g., '["model","analytics"]'
-- =============================================
CREATE OR ALTER PROCEDURE dbo.app_toolcatalog_list_scoped
    @TenantId       nvarchar(50),
    @Language        nvarchar(10),
    @DefaultLanguage nvarchar(10) = 'en',
    @ModulesJson     nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    -- Validate JSON input
    IF ISJSON(@ModulesJson) <> 1
    BEGIN
        RAISERROR('@ModulesJson must be a valid JSON array.', 16, 1);
        RETURN;
    END

    -- Scoped tools: tools belonging to the selected modules
    SELECT DISTINCT 
        tc.ToolName, tc.SpName, tc.IsEnabled, tc.RequiredRoles,
        COALESCE(tt.JsonSchema, tc.JsonSchema) AS JsonSchema,
        COALESCE(tt.Instruction, tc.Instruction) AS Instruction,
        COALESCE(tt.Description, tc.Description) AS Description
    FROM dbo.ToolCatalog tc
    INNER JOIN dbo.ToolCatalogScope tcs 
        ON tc.ToolName = tcs.ToolName
        AND tcs.IsEnabled = 1
        AND (tcs.TenantId IS NULL OR tcs.TenantId = @TenantId)
    LEFT JOIN dbo.ToolCatalogTranslation tt 
        ON tc.ToolName = tt.ToolName AND tt.Language = @Language
    WHERE tc.IsEnabled = 1
      AND tcs.ModuleKey IN (
          SELECT [value] FROM OPENJSON(@ModulesJson)
      )

    UNION

    -- Platform tools: always available regardless of scope
    SELECT DISTINCT 
        tc.ToolName, tc.SpName, tc.IsEnabled, tc.RequiredRoles,
        COALESCE(tt.JsonSchema, tc.JsonSchema) AS JsonSchema,
        COALESCE(tt.Instruction, tc.Instruction) AS Instruction,
        COALESCE(tt.Description, tc.Description) AS Description
    FROM dbo.ToolCatalog tc
    INNER JOIN dbo.ToolCatalogScope tcs 
        ON tc.ToolName = tcs.ToolName
        AND tcs.ModuleKey = 'platform'
        AND tcs.IsEnabled = 1
        AND (tcs.TenantId IS NULL OR tcs.TenantId = @TenantId)
    LEFT JOIN dbo.ToolCatalogTranslation tt 
        ON tc.ToolName = tt.ToolName AND tt.Language = @Language
    WHERE tc.IsEnabled = 1;
END;
GO

-- =============================================
-- SP: app_metadatadictionary_list_scoped
-- Returns metadata keys filtered by selected modules
-- @ModulesJson: JSON array, e.g., '["model"]'
-- =============================================
CREATE OR ALTER PROCEDURE dbo.app_metadatadictionary_list_scoped
    @TenantId       nvarchar(50) = NULL,
    @Language        nvarchar(10) = 'en',
    @DefaultLanguage nvarchar(10) = 'en',
    @ModulesJson     nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    IF ISJSON(@ModulesJson) <> 1
    BEGIN
        RAISERROR('@ModulesJson must be a valid JSON array.', 16, 1);
        RETURN;
    END

    SELECT 
        md.[Key], md.DisplayName, md.Description, md.Unit, md.Examples
    FROM dbo.MetadataDictionary md
    INNER JOIN dbo.MetadataDictionaryScope mds 
        ON md.[Key] = mds.MetadataKey
        AND mds.IsEnabled = 1
        AND (mds.TenantId IS NULL OR mds.TenantId = @TenantId)
    WHERE (md.TenantId IS NULL OR md.TenantId = @TenantId)
      AND md.Language = @Language
      AND mds.ModuleKey IN (
          SELECT [value] FROM OPENJSON(@ModulesJson)
      )
    ORDER BY md.[Key];
END;
GO
```

### Step 3: Create seed data for module scope

**Create file:** `sql/99_seed/010_seed_module_scope.sql`

**CRITICAL:** You must read the EXISTING seed files to know the exact tool names that exist in ToolCatalog. Read these files first:
- `sql/99_seed/002_seed_toolcatalog_core.sql` — core tools
- `sql/99_seed/003_seed_toolcatalog_model.sql` — model tools
- `sql/99_seed/007_seed_toolcatalog_platform.sql` — platform tools
- `sql/99_seed/analytics/001_seed_toolcatalog_analytics.sql` — analytics tools

Then read `sql/99_seed/001_seed_metadata_dictionary.sql` and `sql/99_seed/004_seed_metadata_model.sql` for metadata keys.

Use ONLY tool names and metadata keys that actually exist in those seed files. DO NOT invent tool names.

The seed script structure:

```sql
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- Seed: Module Catalog
-- =============================================

-- Clear and re-seed (idempotent)
DELETE FROM dbo.ModuleCatalog;
GO

INSERT INTO dbo.ModuleCatalog (ModuleKey, AppKey, Instruction, Priority, Language)
VALUES
    ('model', '', 'Product models: dimensions, weight, CBM, pieces, materials, packaging, logistics metrics. Use for queries about specific model details, comparison, or configuration.', 10, 'en'),
    ('model', '', N'Sản phẩm model: kích thước, trọng lượng, CBM, pieces, vật liệu, đóng gói, chỉ số logistics. Dùng cho câu hỏi về chi tiết model, so sánh, cấu hình.', 10, 'vi'),
    ('analytics', '', 'Aggregate analytics: statistical queries across datasets like counts, sums, averages, grouping, filtering, time-series. Use for trend analysis, reporting, dashboards.', 20, 'en'),
    ('analytics', '', N'Phân tích tổng hợp: truy vấn thống kê trên datasets như đếm, tổng, trung bình, nhóm, lọc, thời gian. Dùng cho phân tích xu hướng, báo cáo, dashboard.', 20, 'vi'),
    ('platform', '', 'System operations: diagnostics, action requests, tool listing, write actions requiring human approval.', 0, 'en'),
    ('platform', '', N'Thao tác hệ thống: chẩn đoán, yêu cầu hành động, danh sách công cụ, ghi dữ liệu cần phê duyệt.', 0, 'vi');
GO

-- =============================================
-- Seed: ToolCatalogScope
-- Maps each tool to its owning module
-- IMPORTANT: Read existing tool names from ToolCatalog seed files
-- =============================================

DELETE FROM dbo.ToolCatalogScope;
GO

-- Model tools (read from sql/99_seed/003_seed_toolcatalog_model.sql)
-- Agent: INSERT the actual tool names found in that file. Example:
INSERT INTO dbo.ToolCatalogScope (ToolName, ModuleKey, AppKey)
VALUES
    ('model_get_overview', 'model', ''),
    ('model_get_pieces', 'model', ''),
    ('model_get_materials', 'model', ''),
    ('model_compare_models', 'model', ''),
    ('model_get_packaging', 'model', '');
    -- Agent: add ALL other model_* tools found in the seed file
GO

-- Platform tools (read from sql/99_seed/007_seed_toolcatalog_platform.sql)
-- These are ALWAYS included regardless of scope
INSERT INTO dbo.ToolCatalogScope (ToolName, ModuleKey, AppKey)
VALUES
    ('action_request_write', 'platform', ''),
    ('diagnostics_run', 'platform', '');
    -- Agent: add ALL other platform tools found in the seed file
GO

-- Analytics tools (read from sql/99_seed/analytics/001_seed_toolcatalog_analytics.sql)
INSERT INTO dbo.ToolCatalogScope (ToolName, ModuleKey, AppKey)
VALUES
    -- Agent: INSERT the actual analytics tool names found in that file
    -- Example: ('analytics_catalog_search', 'analytics', ''),
    ('PLACEHOLDER_READ_FROM_SEED', 'analytics', '');
    -- Agent: REPLACE placeholder with actual tool names
GO

-- =============================================
-- Seed: MetadataDictionaryScope
-- Maps metadata keys to modules
-- IMPORTANT: Read actual keys from seed files
-- =============================================

DELETE FROM dbo.MetadataDictionaryScope;
GO

-- Agent: Read sql/99_seed/004_seed_metadata_model.sql to find model metadata keys
-- Agent: Read sql/99_seed/analytics/002_seed_metadata_dictionary_analytics.sql for analytics keys
-- Then INSERT actual keys:

-- Model metadata keys (example — agent must verify from seed file):
-- INSERT INTO dbo.MetadataDictionaryScope (MetadataKey, ModuleKey) VALUES ('model.cbm', 'model'), ...

-- Analytics metadata keys:
-- INSERT INTO dbo.MetadataDictionaryScope (MetadataKey, ModuleKey) VALUES ('analytics.dataset', 'analytics'), ...
```

**AGENT INSTRUCTION:** You MUST read each seed file referenced above and extract the exact ToolName / Key values. Do not guess. If a tool name is wrong, the FK constraint will fail at runtime.

## 4.4 Verification

```sql
-- After running all 3 scripts:
SELECT COUNT(*) AS ModuleCount FROM dbo.ModuleCatalog;
-- Expected: 6 (3 modules × 2 languages)

SELECT ModuleKey, COUNT(*) AS ToolCount 
FROM dbo.ToolCatalogScope 
GROUP BY ModuleKey;
-- Expected: model=N, analytics=M, platform=P (actual counts depend on seed files)

SELECT ModuleKey, COUNT(*) AS MetadataCount 
FROM dbo.MetadataDictionaryScope 
GROUP BY ModuleKey;
-- Expected: model=X, analytics=Y
```

## 4.5 Acceptance Criteria

- All 3 SQL scripts run without errors
- FK constraints validate (all ToolNames exist in ToolCatalog)
- `app_toolcatalog_list_scoped` returns only model tools when called with `'["model"]'`
- `app_toolcatalog_list_scoped` always includes platform tools in result
- `app_metadatadictionary_list_scoped` returns only model metadata when called with `'["model"]'`

---

---

# PACK 5: P0-02b — Implement ModuleScopeResolver in C#

**Priority:** P0 CRITICAL
**Estimated time:** 45 minutes
**Files to create:** 4 new C# files

## 5.1 Context

The `ModuleScopeResolver` uses the LLM to decide which modules are relevant for a user query. It loads the `ModuleCatalog` from SQL, asks the LLM to pick modules, and returns the list. This adds ~1 LLM call per conversation turn (~200-500ms).

The resolver depends on:
- `ILlmClient` — already exists in `TILSOFTAI.Orchestration.Llm`
- `ISqlExecutor` — already exists in `TILSOFTAI.Orchestration.Sql`
- SQL SP `app_modulecatalog_list` — created in Pack 4

## 5.2 Steps

### Step 1: Create `ModuleScopeResult.cs`

**File:** `src/TILSOFTAI.Orchestration/Modules/ModuleScopeResult.cs`

```csharp
namespace TILSOFTAI.Orchestration.Modules;

/// <summary>
/// Result of module scope resolution. Contains which modules 
/// the LLM selected for the current user query.
/// </summary>
public sealed record ModuleScopeResult
{
    /// <summary>Selected module keys (e.g., ["model"], ["model","analytics"])</summary>
    public IReadOnlyList<string> Modules { get; init; } = Array.Empty<string>();

    /// <summary>LLM confidence in module selection (0.0 - 1.0)</summary>
    public decimal Confidence { get; init; }

    /// <summary>LLM reasoning for module selection</summary>
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();

    /// <summary>Whether this result came from cache</summary>
    public bool FromCache { get; init; }
}
```

### Step 2: Create `IModuleScopeResolver.cs`

**File:** `src/TILSOFTAI.Orchestration/Modules/IModuleScopeResolver.cs`

```csharp
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Modules;

public interface IModuleScopeResolver
{
    Task<ModuleScopeResult> ResolveAsync(
        string userQuery,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken);
}
```

### Step 3: Create `ModuleScopeResolver.cs`

**File:** `src/TILSOFTAI.Orchestration/Modules/ModuleScopeResolver.cs`

```csharp
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Sql;

namespace TILSOFTAI.Orchestration.Modules;

public sealed class ModuleScopeResolver : IModuleScopeResolver
{
    private readonly ILlmClient _llmClient;
    private readonly ISqlExecutor _sqlExecutor;
    private readonly ILogger<ModuleScopeResolver> _logger;

    // Fallback: if LLM fails or returns nothing, use all modules
    private static readonly ModuleScopeResult FallbackResult = new()
    {
        Modules = new[] { "model", "analytics", "platform" },
        Confidence = 0m,
        Reasons = new[] { "Fallback: LLM scope resolution failed, using all modules" }
    };

    public ModuleScopeResolver(
        ILlmClient llmClient,
        ISqlExecutor sqlExecutor,
        ILogger<ModuleScopeResolver> logger)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ModuleScopeResult> ResolveAsync(
        string userQuery,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Load module catalog from SQL
            var modules = await LoadModuleCatalogAsync(context, cancellationToken);
            if (modules.Count == 0)
            {
                _logger.LogWarning("ModuleCatalog is empty. Using fallback.");
                return FallbackResult;
            }

            // If only 1 non-platform module, skip LLM call
            var nonPlatform = modules.Where(m => 
                !string.Equals(m.ModuleKey, "platform", StringComparison.OrdinalIgnoreCase)).ToList();
            if (nonPlatform.Count <= 1)
            {
                return new ModuleScopeResult
                {
                    Modules = nonPlatform.Select(m => m.ModuleKey).ToList(),
                    Confidence = 1.0m,
                    Reasons = new[] { "Only one non-platform module available" }
                };
            }

            // 2. Build scope prompt
            var prompt = BuildScopePrompt(userQuery, modules);

            // 3. Ask LLM to select modules
            var llmRequest = new LlmRequest
            {
                SystemPrompt = "You are a module router. Given a user query and available modules, select which modules are needed. Respond ONLY in valid JSON with no markdown or extra text. Format: {\"modules\":[\"module1\"],\"confidence\":0.9,\"reasons\":[\"reason\"]}",
                Messages = new List<LlmMessage> { new(ChatRoles.User, prompt) },
                Tools = Array.Empty<ToolDefinition>(),
                MaxTokens = 200
            };

            var response = await _llmClient.CompleteAsync(llmRequest, cancellationToken);
            var result = ParseScopeResponse(response.Content, modules);

            _logger.LogInformation(
                "ModuleScopeResolved | Query: {QueryPreview} | Modules: [{Modules}] | Confidence: {Confidence}",
                userQuery.Length > 80 ? userQuery[..80] + "..." : userQuery,
                string.Join(", ", result.Modules),
                result.Confidence);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ModuleScopeResolver failed. Using fallback.");
            return FallbackResult;
        }
    }

    private async Task<List<ModuleCatalogEntry>> LoadModuleCatalogAsync(
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        var language = string.IsNullOrWhiteSpace(context.Language) ? "en" : context.Language;
        var parameters = new Dictionary<string, object?>
        {
            ["@TenantId"] = context.TenantId,
            ["@Language"] = language
        };

        var rows = await _sqlExecutor.ExecuteQueryAsync(
            "dbo.app_modulecatalog_list", parameters, cancellationToken);

        return rows.Select(row => new ModuleCatalogEntry
        {
            ModuleKey = row.TryGetValue("ModuleKey", out var mk) ? mk?.ToString() ?? "" : "",
            AppKey = row.TryGetValue("AppKey", out var ak) ? ak?.ToString() ?? "" : "",
            Instruction = row.TryGetValue("Instruction", out var inst) ? inst?.ToString() ?? "" : "",
            Priority = row.TryGetValue("Priority", out var pri) ? Convert.ToInt32(pri) : 100
        }).ToList();
    }

    private static string BuildScopePrompt(string query, List<ModuleCatalogEntry> modules)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Given this user query, select the relevant modules.");
        sb.Append("Query: \"").Append(query).AppendLine("\"");
        sb.AppendLine("Available modules:");
        foreach (var m in modules)
        {
            if (string.Equals(m.ModuleKey, "platform", StringComparison.OrdinalIgnoreCase))
                continue; // Platform is always included, don't confuse LLM
            sb.Append("- ").Append(m.ModuleKey).Append(": ").AppendLine(m.Instruction);
        }
        sb.AppendLine("Rules:");
        sb.AppendLine("- Select ONLY modules needed for the query");
        sb.AppendLine("- If unsure, include the module (better to include than miss)");
        sb.AppendLine("- 'platform' module is always auto-included, do NOT list it");
        sb.AppendLine("Respond JSON only: {\"modules\":[...],\"confidence\":0.0-1.0,\"reasons\":[...]}");
        return sb.ToString();
    }

    private static ModuleScopeResult ParseScopeResponse(
        string? content, List<ModuleCatalogEntry> availableModules)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return FallbackResult;
        }

        try
        {
            // Strip markdown code fences if present
            var json = content.Trim();
            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                var lastFence = json.LastIndexOf("```");
                if (firstNewline > 0 && lastFence > firstNewline)
                {
                    json = json[(firstNewline + 1)..lastFence].Trim();
                }
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var modules = new List<string>();
            if (root.TryGetProperty("modules", out var modulesEl) && modulesEl.ValueKind == JsonValueKind.Array)
            {
                var validKeys = new HashSet<string>(
                    availableModules.Select(m => m.ModuleKey), 
                    StringComparer.OrdinalIgnoreCase);

                foreach (var item in modulesEl.EnumerateArray())
                {
                    var key = item.GetString();
                    if (!string.IsNullOrEmpty(key) && validKeys.Contains(key))
                    {
                        modules.Add(key);
                    }
                }
            }

            var confidence = 0m;
            if (root.TryGetProperty("confidence", out var confEl))
            {
                confidence = confEl.TryGetDecimal(out var c) ? c : 0m;
            }

            var reasons = new List<string>();
            if (root.TryGetProperty("reasons", out var reasonsEl) && reasonsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in reasonsEl.EnumerateArray())
                {
                    var reason = item.GetString();
                    if (!string.IsNullOrEmpty(reason))
                    {
                        reasons.Add(reason);
                    }
                }
            }

            // Validate: if LLM returned empty modules, fallback
            if (modules.Count == 0)
            {
                return FallbackResult;
            }

            return new ModuleScopeResult
            {
                Modules = modules,
                Confidence = confidence,
                Reasons = reasons
            };
        }
        catch (JsonException)
        {
            return FallbackResult;
        }
    }

    private sealed class ModuleCatalogEntry
    {
        public string ModuleKey { get; init; } = "";
        public string AppKey { get; init; } = "";
        public string Instruction { get; init; } = "";
        public int Priority { get; init; }
    }
}
```

### Step 4: Create `IToolCatalogResolver` scoped method extension

**DO NOT modify the existing `IToolCatalogResolver` interface.** Instead, add a new interface:

**File:** `src/TILSOFTAI.Orchestration/Tools/IScopedToolCatalogResolver.cs`

```csharp
namespace TILSOFTAI.Orchestration.Tools;

/// <summary>
/// Extended tool catalog resolver that supports module scoping.
/// Inherits from IToolCatalogResolver for backward compatibility.
/// </summary>
public interface IScopedToolCatalogResolver : IToolCatalogResolver
{
    /// <summary>
    /// Load tools scoped to the given modules.
    /// Always includes platform tools.
    /// </summary>
    Task<IReadOnlyList<ToolDefinition>> GetScopedToolsAsync(
        IReadOnlyList<string> moduleKeys,
        CancellationToken cancellationToken = default);
}
```

## 5.3 Verification

```bash
dotnet build src/TILSOFTAI.Orchestration/
```

## 5.4 Acceptance Criteria

- New files compile without errors
- `IModuleScopeResolver` and `IScopedToolCatalogResolver` interfaces defined
- `ModuleScopeResolver` handles: empty catalog (fallback), single module (skip LLM), LLM parse failure (fallback), malformed JSON (fallback)
- No existing files modified in this pack

---

---

# PACK 6: P0-02c — Integrate Scoped Loading into ToolCatalogSyncService, MetadataProvider, ChatPipeline, and DI

**Priority:** P0 CRITICAL
**Estimated time:** 60 minutes
**Files to modify:** 4 existing files

## 6.1 Context

This pack wires everything together:
1. `ToolCatalogSyncService` gets a new `GetScopedToolsAsync()` method
2. `MetadataDictionaryContextPackProvider` gets scoped loading
3. `ChatPipeline` calls `ModuleScopeResolver` before the ReAct loop
4. DI registration in `AddTilsoftAiExtensions.cs` is updated

## 6.2 Steps

### Step 1: Extend `ToolCatalogSyncService` to implement `IScopedToolCatalogResolver`

**File:** `src/TILSOFTAI.Infrastructure/Tools/ToolCatalogSyncService.cs`

Change the class declaration from:
```csharp
public sealed class ToolCatalogSyncService : IToolCatalogResolver
```
To:
```csharp
public sealed class ToolCatalogSyncService : IScopedToolCatalogResolver
```

Add the new method AFTER the existing `GetResolvedToolsAsync` method (do not modify the existing method):

```csharp
public async Task<IReadOnlyList<ToolDefinition>> GetScopedToolsAsync(
    IReadOnlyList<string> moduleKeys,
    CancellationToken cancellationToken = default)
{
    if (moduleKeys is null || moduleKeys.Count == 0)
    {
        _logger.LogWarning("GetScopedToolsAsync called with empty moduleKeys. Falling back to global.");
        return await GetResolvedToolsAsync(cancellationToken);
    }

    var tenantId = _contextAccessor.Current.TenantId;
    if (string.IsNullOrWhiteSpace(tenantId))
    {
        throw new InvalidOperationException("Execution context TenantId is required.");
    }

    var language = string.IsNullOrWhiteSpace(_contextAccessor.Current.Language)
        ? _localizationOptions.DefaultLanguage
        : _contextAccessor.Current.Language;

    var modulesJson = System.Text.Json.JsonSerializer.Serialize(moduleKeys);

    var sqlTools = await LoadScopedSqlToolsAsync(tenantId, language, _localizationOptions.DefaultLanguage, modulesJson, cancellationToken);
    var registryTools = _toolRegistry.ListEnabled();

    var resolved = new List<ToolDefinition>();
    foreach (var tool in registryTools)
    {
        if (!sqlTools.TryGetValue(tool.Name, out var sqlTool))
        {
            continue; // Tool not in scoped set — skip
        }

        var merged = Merge(tool, sqlTool);
        if (merged is not null)
        {
            resolved.Add(merged);
        }
    }

    _logger.LogInformation(
        "ScopedToolsResolved | Modules: [{Modules}] | ToolCount: {ToolCount}",
        string.Join(", ", moduleKeys), resolved.Count);

    return resolved;
}

private async Task<Dictionary<string, ToolCatalogEntry>> LoadScopedSqlToolsAsync(
    string tenantId,
    string language,
    string defaultLanguage,
    string modulesJson,
    CancellationToken cancellationToken)
{
    var results = new Dictionary<string, ToolCatalogEntry>(StringComparer.OrdinalIgnoreCase);

    await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
    await connection.OpenAsync(cancellationToken);

    await using var command = new SqlCommand("dbo.app_toolcatalog_list_scoped", connection)
    {
        CommandType = CommandType.StoredProcedure,
        CommandTimeout = _sqlOptions.CommandTimeoutSeconds
    };

    command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
    command.Parameters.Add(new SqlParameter("@Language", SqlDbType.NVarChar, 10) { Value = language });
    command.Parameters.Add(new SqlParameter("@DefaultLanguage", SqlDbType.NVarChar, 10) { Value = string.IsNullOrWhiteSpace(defaultLanguage) ? "en" : defaultLanguage });
    command.Parameters.Add(new SqlParameter("@ModulesJson", SqlDbType.NVarChar, -1) { Value = modulesJson });

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        var toolName = reader["ToolName"] as string;
        if (string.IsNullOrWhiteSpace(toolName)) continue;

        results[toolName] = new ToolCatalogEntry
        {
            ToolName = toolName,
            SpName = reader["SpName"] as string ?? string.Empty,
            IsEnabled = reader["IsEnabled"] != DBNull.Value && Convert.ToBoolean(reader["IsEnabled"]),
            RequiredRoles = reader["RequiredRoles"] as string,
            JsonSchema = reader["JsonSchema"] as string,
            Instruction = reader["Instruction"] as string,
            Description = reader["Description"] as string
        };
    }

    return results;
}
```

Add the using at the top if not present:
```csharp
using TILSOFTAI.Orchestration.Tools;
```

### Step 2: Add scope support to `MetadataDictionaryContextPackProvider`

**File:** `src/TILSOFTAI.Infrastructure/Metadata/MetadataDictionaryContextPackProvider.cs`

Add a public property to hold current scope (set by ChatPipeline before context packs are built):

Find the class fields:
```csharp
private const string ContextPackKey = "metadata_dictionary";
private readonly ISqlExecutor _sqlExecutor;
private readonly LocalizationOptions _localizationOptions;
```

Replace with:
```csharp
private const string ContextPackKey = "metadata_dictionary";
private readonly ISqlExecutor _sqlExecutor;
private readonly LocalizationOptions _localizationOptions;

/// <summary>
/// Current module scope. When set, metadata is filtered by module.
/// Set by ChatPipeline before calling GetContextPacksAsync.
/// Thread-safety: ChatPipeline creates new scope per request via DI scoping.
/// </summary>
public IReadOnlyList<string>? CurrentScope { get; set; }
```

Then modify `GetContextPacksAsync` to use scoped SP when scope is set. Find:
```csharp
var rows = await _sqlExecutor.ExecuteQueryAsync("dbo.app_metadatadictionary_list", parameters, cancellationToken);
```

Replace with:
```csharp
IReadOnlyList<IReadOnlyDictionary<string, object?>> rows;

if (CurrentScope is { Count: > 0 })
{
    // Scoped: use module-filtered SP
    var scopedParams = new Dictionary<string, object?>
    {
        ["@TenantId"] = string.IsNullOrWhiteSpace(context.TenantId) ? null : context.TenantId,
        ["@Language"] = resolvedLanguage,
        ["@DefaultLanguage"] = _localizationOptions.DefaultLanguage,
        ["@ModulesJson"] = System.Text.Json.JsonSerializer.Serialize(CurrentScope)
    };
    rows = await _sqlExecutor.ExecuteQueryAsync("dbo.app_metadatadictionary_list_scoped", scopedParams, cancellationToken);
}
else
{
    // Unscoped: backward compatible
    rows = await _sqlExecutor.ExecuteQueryAsync("dbo.app_metadatadictionary_list", parameters, cancellationToken);
}
```

### Step 3: Integrate into `ChatPipeline.RunAsync`

**File:** `src/TILSOFTAI.Orchestration/Pipeline/ChatPipeline.cs`

**3a.** Add new field for `IModuleScopeResolver`. Find the field declarations:
```csharp
private readonly AnalyticsOrchestrator? _analyticsOrchestrator;
private readonly ILogger<ChatPipeline> _logger;
```

Add after:
```csharp
private readonly AnalyticsOrchestrator? _analyticsOrchestrator;
private readonly Modules.IModuleScopeResolver? _moduleScopeResolver;
private readonly ILogger<ChatPipeline> _logger;
```

**3b.** Add constructor parameter. Find the constructor parameter list and add `Modules.IModuleScopeResolver? moduleScopeResolver` as an optional parameter (nullable, so it's backward compatible):

Find (in constructor parameters):
```csharp
AnalyticsOrchestrator? analyticsOrchestrator,
ILogger<ChatPipeline> logger,
```

Replace with:
```csharp
AnalyticsOrchestrator? analyticsOrchestrator,
Modules.IModuleScopeResolver? moduleScopeResolver,
ILogger<ChatPipeline> logger,
```

Find (in constructor body):
```csharp
_analyticsOrchestrator = analyticsOrchestrator; // Optional, null if not registered
```

Add after:
```csharp
_analyticsOrchestrator = analyticsOrchestrator; // Optional, null if not registered
_moduleScopeResolver = moduleScopeResolver; // Optional, null if module scoping not enabled
```

**3c.** Add scope resolution BEFORE tool loading. In `RunAsync`, find the line (around the tool loading section before the ReAct loop):
```csharp
var tools = await _toolCatalogResolver.GetResolvedToolsAsync(ct);
```

Replace with:
```csharp
// Module scope resolution (if enabled)
IReadOnlyList<string>? resolvedModules = null;
if (_moduleScopeResolver is not null)
{
    var scopeResult = await _moduleScopeResolver.ResolveAsync(promptInput, ctx, ct);
    resolvedModules = scopeResult.Modules;

    _logger.LogInformation(
        "ModuleScopeApplied | Modules: [{Modules}] | Confidence: {Confidence}",
        string.Join(", ", scopeResult.Modules), scopeResult.Confidence);
}

// Load tools: scoped if resolver available, otherwise global
IReadOnlyList<ToolDefinition> tools;
if (resolvedModules is { Count: > 0 } && _toolCatalogResolver is IScopedToolCatalogResolver scopedResolver)
{
    tools = await scopedResolver.GetScopedToolsAsync(resolvedModules, ct);
}
else
{
    tools = await _toolCatalogResolver.GetResolvedToolsAsync(ct);
}

// Apply scope to metadata provider if available
if (resolvedModules is { Count: > 0 })
{
    // The metadata provider scope is set via the composite pattern.
    // This requires the MetadataDictionaryContextPackProvider to be accessible.
    // Scope is set through the DI-resolved instance.
    // (The actual scoping happens inside GetContextPacksAsync via CurrentScope property)
}
```

Add using at the top:
```csharp
using TILSOFTAI.Orchestration.Tools;
```

### Step 4: Update DI registration

**File:** `src/TILSOFTAI.Api/Extensions/AddTilsoftAiExtensions.cs`

Find the ToolCatalogSyncService registration:
```csharp
services.AddSingleton<ToolCatalogSyncService>();
services.AddSingleton<IToolCatalogResolver>(sp => sp.GetRequiredService<ToolCatalogSyncService>());
```

Replace with:
```csharp
services.AddSingleton<ToolCatalogSyncService>();
services.AddSingleton<IToolCatalogResolver>(sp => sp.GetRequiredService<ToolCatalogSyncService>());
services.AddSingleton<IScopedToolCatalogResolver>(sp => sp.GetRequiredService<ToolCatalogSyncService>());
```

Add after the ContextPackBudgeter registration (near the bottom of the method):
```csharp
// Module Scope Resolver
services.AddSingleton<IModuleScopeResolver, ModuleScopeResolver>();
```

Add required usings at the top of the file:
```csharp
using TILSOFTAI.Orchestration.Modules;
using TILSOFTAI.Orchestration.Tools;
```

## 6.3 Verification

```bash
dotnet build src/TILSOFTAI.Api/
```

## 6.4 Acceptance Criteria

- **AC-01:** Query "phân tích model ID 3" loads model + platform tools only (no analytics tools)
- **AC-02:** metadata_dictionary contains only model keys when scope = ["model"]
- **AC-08:** `action_request_write` is always present (platform tools always included)
- Build passes
- Existing unscoped flow still works when ModuleScopeResolver returns fallback

---

---

# PACK 7: P0-03 — Priority-Based ContextPackBudgeter

**Priority:** P0 CRITICAL
**Risk:** `tool_catalog` pack (letter "t") dropped before `metadata_dictionary` (letter "m") due to alphabetical sort
**Estimated time:** 20 minutes
**Files to modify:** 1 file

## 7.1 Context

**File:** `src/TILSOFTAI.Orchestration/Prompting/ContextPackBudgeter.cs`

Current `Budget()` method sorts packs alphabetically and removes the LAST one when over budget. Since "tool_catalog" sorts after "metadata_dictionary" alphabetically, it gets dropped first — making the LLM blind to available tools.

## 7.2 Steps

### Step 1: Replace the `Budget` method

Find the entire `Budget` method:
```csharp
public IReadOnlyList<KeyValuePair<string, string>> Budget(IReadOnlyDictionary<string, string> packs)
{
    if (packs is null || packs.Count == 0)
    {
        return Array.Empty<KeyValuePair<string, string>>();
    }

    var ordered = packs
        .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
        .ToList();

    var totalChars = ordered.Sum(pair => EstimatePackChars(pair.Key, pair.Value));

    while (totalChars > PromptBudget.MaxContextPacksChars && ordered.Count > 0)
    {
        var lastIndex = ordered.Count - 1;
        var last = ordered[lastIndex];
        totalChars -= EstimatePackChars(last.Key, last.Value);
        ordered.RemoveAt(lastIndex);
    }

    return ordered;
}
```

Replace with:
```csharp
// Priority map: lower number = higher priority = removed LAST
// Critical packs are trimmed (content shortened) rather than dropped entirely
private static readonly Dictionary<string, int> PackPriority = new(StringComparer.OrdinalIgnoreCase)
{
    ["tool_catalog"] = 0,        // Highest: LLM needs tool instructions
    ["atomic_catalog"] = 1,      // High: schema for analytics
    ["metadata_dictionary"] = 2  // Can be trimmed
};

private const int DefaultPriority = 50;

public IReadOnlyList<KeyValuePair<string, string>> Budget(IReadOnlyDictionary<string, string> packs)
{
    if (packs is null || packs.Count == 0)
    {
        return Array.Empty<KeyValuePair<string, string>>();
    }

    // Sort by priority (critical first), then alphabetically
    var ordered = packs
        .OrderBy(pair => PackPriority.GetValueOrDefault(pair.Key, DefaultPriority))
        .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
        .ToList();

    var totalChars = ordered.Sum(pair => EstimatePackChars(pair.Key, pair.Value));

    while (totalChars > PromptBudget.MaxContextPacksChars && ordered.Count > 0)
    {
        var lastIndex = ordered.Count - 1;
        var last = ordered[lastIndex];
        var priority = PackPriority.GetValueOrDefault(last.Key, DefaultPriority);

        if (priority <= 1)
        {
            // Critical pack: trim content instead of dropping
            var maxCharsPerPack = Math.Max(200, PromptBudget.MaxContextPacksChars / ordered.Count);
            if (last.Value.Length > maxCharsPerPack)
            {
                totalChars -= EstimatePackChars(last.Key, last.Value);
                var trimmed = last.Value[..maxCharsPerPack] + "\n[...truncated]";
                ordered[lastIndex] = new KeyValuePair<string, string>(last.Key, trimmed);
                totalChars += EstimatePackChars(last.Key, trimmed);
            }
            break; // Stop removing — all remaining packs are critical
        }

        totalChars -= EstimatePackChars(last.Key, last.Value);
        ordered.RemoveAt(lastIndex);
    }

    return ordered;
}
```

## 7.3 Verification

```bash
dotnet build src/TILSOFTAI.Orchestration/
```

## 7.4 Acceptance Criteria

- **AC-03:** `tool_catalog` pack is NEVER dropped by budgeter (always present in prompt)
- Priority order: tool_catalog → atomic_catalog → metadata_dictionary → everything else
- Critical packs (priority ≤ 1) are trimmed rather than dropped
- Build passes

---

---

# PACK 8: P1-01 — Enable ToolCatalogContextPack

**Priority:** P1 HARDENING
**Risk:** Without this, LLM has tool schemas but no instructions → shallow ReAct depth
**Prerequisite:** Pack 6 (module scoping) must be complete FIRST
**Estimated time:** 10 minutes
**Files to modify:** 1 file

## 8.1 Context

`ToolCatalogContextPack.Enabled` is currently `false` in appsettings.json. This means the LLM never receives tool instructions (e.g., "if PieceCount > 0 → call model_get_pieces"). This is the primary reason for shallow ReAct depth (LLM calls one tool and stops).

**WHY this was disabled:** Without module scoping, enabling it would inject instructions for ALL tools (40+), causing token explosion. Now that Pack 6 implements scoping, the tool count is reduced per request, making it safe to enable.

## 8.2 Steps

### Step 1: Modify `appsettings.json`

**File:** `src/TILSOFTAI.Api/appsettings.json`

Find:
```json
"ToolCatalogContextPack": {
    "Enabled": false,
    "MaxTools": 40,
    "MaxTotalTokens": 900,
    "MaxInstructionTokensPerTool": 60,
    "MaxDescriptionTokensPerTool": 30,
    "PreferTools": []
}
```

Replace with:
```json
"ToolCatalogContextPack": {
    "Enabled": true,
    "MaxTools": 20,
    "MaxTotalTokens": 1200,
    "MaxInstructionTokensPerTool": 80,
    "MaxDescriptionTokensPerTool": 30,
    "PreferTools": [
        "model_get_overview",
        "model_get_pieces",
        "model_get_materials",
        "action_request_write",
        "diagnostics_run"
    ]
}
```

**Changes explained:**
- `Enabled: true` — activate tool instructions in prompt
- `MaxTools: 20` — reduced from 40 because scoping reduces tool count
- `MaxTotalTokens: 1200` — increased from 900 because fewer tools = more budget per tool
- `MaxInstructionTokensPerTool: 80` — increased from 60 to include follow-up logic (e.g., "if PieceCount > 0 → call model_get_pieces")
- `PreferTools` — ensures critical tools get their instructions first

## 8.3 Acceptance Criteria

- **AC-04:** ReAct depth ≥ 2 for model query. Test: ask "cho tôi thông tin model ID 3" and verify that if PieceCount > 0, the LLM calls `model_get_pieces` as a follow-up
- Tool instructions appear in the LLM prompt (log the system prompt to verify)

---

---

# PACK 9: P1-02 — Hallucination Guard

**Priority:** P1 HARDENING
**Risk:** LLM invents data for NULL/missing fields
**Estimated time:** 25 minutes
**Files to modify:** 1 C# file + multiple SQL files

## 9.1 Context

SQL `FOR JSON PATH` automatically omits NULL fields. The LLM receives incomplete data and may "fill in" missing values from its training data or metadata examples. For instance, `LoadabilityIndex` might be NULL in the database but the LLM invents a value based on the metadata dictionary example.

Two fixes:
1. **Prompt-side:** Add explicit hallucination rules to the system prompt
2. **SQL-side:** Add `INCLUDE_NULL_VALUES` to `FOR JSON` in `ai_model_*` stored procedures

## 9.2 Steps

### Step 1: Update `PromptBuilder.BuildBaseSystemPrompt`

**File:** `src/TILSOFTAI.Orchestration/Prompting/PromptBuilder.cs`

Find the `BuildBaseSystemPrompt` method:
```csharp
private static string BuildBaseSystemPrompt(TilsoftExecutionContext context)
{
    // Short, deterministic prompt focused on tool behavior
    var sb = new StringBuilder();
    sb.AppendLine("You are TILSOFTAI. Strict rules:");
    sb.AppendLine("1. Use tools for facts; never guess missing data.");
    sb.AppendLine("2. Follow tool outputs strictly.");
    sb.AppendLine("3. Reply in user's language unless asked otherwise.");
    return sb.ToString();
}
```

Replace with:
```csharp
private static string BuildBaseSystemPrompt(TilsoftExecutionContext context)
{
    var sb = new StringBuilder();
    sb.AppendLine("You are TILSOFTAI. Strict rules:");
    sb.AppendLine("1. Use tools for facts; never guess missing data.");
    sb.AppendLine("2. Follow tool outputs strictly.");
    sb.AppendLine("3. Reply in user's language unless asked otherwise.");
    sb.AppendLine("4. CRITICAL: If a field is missing, null, or absent from tool output, report it as 'không có dữ liệu' (no data available). NEVER invent, estimate, or fill in values.");
    sb.AppendLine("5. If key information is missing after a tool call and follow-up tools exist, call them before responding.");
    sb.AppendLine("6. Always cite which tool provided each piece of information.");
    return sb.ToString();
}
```

### Step 2: Update SQL stored procedures to include NULL values

**IMPORTANT:** You must read each `ai_model_*` SP file to find the exact `FOR JSON PATH` statements. The SPs are in:
- `sql/02_modules/model/003_sps_model.sql`

For EVERY `FOR JSON PATH` statement in every `ai_model_*` procedure, add `INCLUDE_NULL_VALUES`.

Example — in `ai_model_get_overview`, find:
```sql
FOR JSON PATH
```

Replace with:
```sql
FOR JSON PATH, INCLUDE_NULL_VALUES
```

**BUT be careful:** Some `FOR JSON PATH` statements use `WITHOUT_ARRAY_WRAPPER` (for the meta wrapper). For those, the change is:
```sql
-- Before:
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER

-- After:  
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
```

**Agent: Read the ENTIRE `003_sps_model.sql` file and update ALL `FOR JSON PATH` occurrences in ALL `ai_model_*` procedures (get_overview, get_pieces, get_materials, compare_models, get_packaging, and any others).** Also check `sql/02_modules/model_enterprise/002_sps_model_enterprise.sql` if it exists.

## 9.3 Verification

```sql
-- Test: should show null fields explicitly
EXEC dbo.ai_model_get_overview @TenantId = 'test', @ArgsJson = '{"modelId": 1}';
-- Expected: JSON output includes "LoadabilityIndex": null instead of omitting the field
```

## 9.4 Acceptance Criteria

- **AC-05:** LLM responses show "không có dữ liệu" for missing values instead of invented data
- SQL output includes NULL fields explicitly (e.g., `"LoadabilityIndex": null`)
- System prompt contains all 6 rules

---

---

# PACK 10: P1-03 — Seed Atomic Catalogs

**Priority:** P1 HARDENING
**Risk:** Atomic subsystem non-operational — catalogs exist but empty
**Estimated time:** 30 minutes
**Files to create:** 1 SQL file

## 10.1 Context

The tables `DatasetCatalog`, `FieldCatalog`, and `EntityGraphCatalog` (in `sql/02_atomic/001_tables_catalog.sql`) exist but contain NO DATA. The `AtomicCatalogContextPackProvider` returns empty context packs.

**CRITICAL:** The seed data must match the ACTUAL view columns. You MUST read the model views before writing the seed script.

## 10.2 Steps

### Step 1: Read the actual view schemas

**Agent: You MUST read these files first and extract the exact column names:**

1. `sql/02_modules/model/004_views_model_semantic.sql` → `vw_ModelSemantic` columns
2. Find views for pieces and materials (search for `vw_Model` or `v_Model` in the sql/ directory)

### Step 2: Read the actual DatasetCatalog schema

**Agent:** Re-read `sql/02_atomic/001_tables_catalog.sql` carefully. Note:
- `DatasetCatalog` has column `BaseObject` (not `ViewName`) and has NO columns `SchemaName`, `DisplayName`, `Description`, `PrimaryKeyColumn`, or `TenantColumn`
- `FieldCatalog` has columns `FieldKey` and `PhysicalColumn` (not `FieldName`), has `IsDimension`, `AllowedAggregations`, `IsSortable`, but has NO columns `DisplayName`, `Description`, `SemanticType`, or `IsGroupable` (wait — re-check, it has `IsGroupable`)
- `EntityGraphCatalog` has columns `GraphKey`, `FromDatasetKey`, `ToDatasetKey`, `JoinConditionTemplate` (not `LeftDatasetKey`/`RightDatasetKey`/`LeftColumn`/`RightColumn`/`Description`)

**THE AUDIT REPORT'S SEED SCRIPT EXAMPLES ARE APPROXIMATIONS.** You must use the ACTUAL column names from the table definitions. Do NOT copy the audit report's seed verbatim — adapt it to match the real schema.

### Step 3: Create seed file

**File:** `sql/99_seed/011_seed_atomic_catalog_model.sql`

**Agent:** Write INSERT statements using the ACTUAL column names from Step 1-2. The structure will look like:

```sql
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Seed DatasetCatalog for Model module
-- Agent: use actual column names from 001_tables_catalog.sql

-- Example structure (adapt to actual schema):
-- INSERT INTO dbo.DatasetCatalog (DatasetKey, BaseObject, TimeColumn, IsEnabled, TenantId)
-- VALUES ('model_overview', 'dbo.vw_ModelSemantic', NULL, 1, NULL);

-- Seed FieldCatalog for model_overview
-- Agent: use actual column names from the view AND the FieldCatalog table

-- Seed EntityGraphCatalog
-- Agent: use actual column names from EntityGraphCatalog table
```

## 10.3 Acceptance Criteria

- **AC-09:** `SELECT * FROM dbo.DatasetCatalog WHERE DatasetKey LIKE 'model%'` returns rows
- `SELECT * FROM dbo.FieldCatalog WHERE DatasetKey = 'model_overview'` returns field definitions
- Column names in FieldCatalog match actual view columns
- SQL script is idempotent

---

---

# PACK 11: P0-01b — Full Message History Refactor (Enterprise)

**Priority:** P0 (deferred to Phase 3 for safety)
**Risk:** Multi-turn conversations lose context
**Estimated time:** 45 minutes
**Files to modify:** 3 files

## 11.1 Context

Pack 3 fixed the immediate problem (only send last user message). This pack completes the solution by sending full message history to the LLM so it understands conversation context.

## 11.2 Steps

### Step 1: Add `MessageHistory` to `ChatRequest`

**File:** `src/TILSOFTAI.Orchestration/Pipeline/ChatRequest.cs`

Add a new property:
```csharp
/// <summary>
/// Full conversation history (user + assistant turns).
/// When provided, ChatPipeline injects these into the LLM messages list.
/// The last message should be the current user query.
/// </summary>
public IReadOnlyList<ChatMessage>? MessageHistory { get; set; }
```

Ensure the `ChatMessage` type is importable. Check if `ChatMessage` is defined in the `Conversations` namespace — it's the same type used by `IConversationStore`. Add using if needed:
```csharp
using TILSOFTAI.Orchestration.Conversations;
```

### Step 2: Build message history in `OpenAiChatCompletionsController`

**File:** `src/TILSOFTAI.Api/Controllers/OpenAiChatCompletionsController.cs`

Add a new private method after `BuildUserInput`:

```csharp
/// <summary>
/// Builds conversation history from OpenAI-format messages.
/// Includes user and assistant turns, limited to maxTurns most recent pairs.
/// The last user message is excluded (it becomes Input).
/// </summary>
private static IReadOnlyList<ChatMessage> BuildMessageHistory(
    IReadOnlyList<OpenAiChatMessage> messages, int maxTurns = 10)
{
    if (messages is null || messages.Count <= 1)
    {
        return Array.Empty<ChatMessage>();
    }

    // Take all messages except the last one (which is the current user input)
    var history = new List<ChatMessage>();
    var relevantMessages = messages
        .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
        .ToList();

    // Remove the last message (current input) — it's handled by BuildUserInput
    if (relevantMessages.Count > 0 
        && string.Equals(relevantMessages[^1].Role, "user", StringComparison.OrdinalIgnoreCase))
    {
        relevantMessages.RemoveAt(relevantMessages.Count - 1);
    }

    // Take last N turns
    var startIndex = Math.Max(0, relevantMessages.Count - (maxTurns * 2));
    for (var i = startIndex; i < relevantMessages.Count; i++)
    {
        var msg = relevantMessages[i];
        history.Add(new ChatMessage(msg.Role!, msg.Content ?? string.Empty));
    }

    return history;
}
```

Add the using for ChatMessage:
```csharp
using TILSOFTAI.Orchestration.Conversations;
```

Then in the `Post` method, where `ChatRequest` is constructed (both stream and non-stream paths), add message history. Find (for streaming path):
```csharp
var chatRequest = new ChatRequest
{
    Input = joinedInput,
    AllowCache = true,
    ContainsSensitive = sensitivityResult.ContainsSensitive,
    SensitivityReasons = sensitivityResult.Reasons
};
```

Replace with:
```csharp
var chatRequest = new ChatRequest
{
    Input = joinedInput,
    MessageHistory = BuildMessageHistory(request.Messages),
    AllowCache = true,
    ContainsSensitive = sensitivityResult.ContainsSensitive,
    SensitivityReasons = sensitivityResult.Reasons
};
```

Do the same for the non-streaming `ChatRequest` construction (search for `chatRequestNonStream`).

### Step 3: Use `MessageHistory` in `ChatPipeline.RunAsync`

**File:** `src/TILSOFTAI.Orchestration/Pipeline/ChatPipeline.cs`

Find where the messages list is initialized:
```csharp
var messages = new List<LlmMessage>
{
    new(ChatRoles.User, promptInput)
};
```

Replace with:
```csharp
var messages = new List<LlmMessage>();

// Inject conversation history if provided (user + assistant turns from previous exchanges)
if (request.MessageHistory is { Count: > 0 })
{
    foreach (var hist in request.MessageHistory)
    {
        messages.Add(new LlmMessage(hist.Role, hist.Content));
    }
}

// Current user message (sanitized + normalized)
messages.Add(new LlmMessage(ChatRoles.User, promptInput));
```

## 11.3 Verification

```bash
dotnet build src/TILSOFTAI.Api/
```

## 11.4 Acceptance Criteria

- Multi-turn conversation: LLM sees previous Q&A pairs and doesn't repeat answers
- Single-message requests work identically (MessageHistory is null/empty → no change)
- Build passes

---

---

# PACK 12: P2-01 — Module Scope Audit Logging

**Priority:** P2 POLISH
**Estimated time:** 20 minutes
**Files to create:** 1 SQL, modify 1 C# file

## 12.1 Steps

### Step 1: Create audit table

**File:** `sql/01_core/072_tables_module_scope_log.sql`

```sql
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.ModuleScopeLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ModuleScopeLog
    (
        Id              bigint IDENTITY(1,1) NOT NULL,
        ConversationId  nvarchar(100)   NOT NULL,
        TenantId        nvarchar(50)    NOT NULL,
        UserId          nvarchar(100)   NOT NULL,
        UserQuery       nvarchar(2000)  NOT NULL,
        ResolvedModules nvarchar(500)   NOT NULL,
        Confidence      decimal(5,4)    NOT NULL,
        Reasons         nvarchar(2000)  NULL,
        ToolCount       int             NOT NULL DEFAULT 0,
        CreatedAtUtc    datetime2(3)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ModuleScopeLog PRIMARY KEY (Id)
    );

    CREATE NONCLUSTERED INDEX IX_ModuleScopeLog_Tenant_Date
        ON dbo.ModuleScopeLog (TenantId, CreatedAtUtc DESC);

    PRINT 'Created table: dbo.ModuleScopeLog';
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_modulescopelog_insert
    @ConversationId  nvarchar(100),
    @TenantId        nvarchar(50),
    @UserId          nvarchar(100),
    @UserQuery       nvarchar(2000),
    @ResolvedModules nvarchar(500),
    @Confidence      decimal(5,4),
    @Reasons         nvarchar(2000) = NULL,
    @ToolCount       int = 0
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ModuleScopeLog 
        (ConversationId, TenantId, UserId, UserQuery, ResolvedModules, Confidence, Reasons, ToolCount)
    VALUES 
        (@ConversationId, @TenantId, @UserId, @UserQuery, @ResolvedModules, @Confidence, @Reasons, @ToolCount);
END;
GO
```

### Step 2: Add logging to ChatPipeline

In `ChatPipeline.RunAsync`, after the scope resolution block (added in Pack 6), add audit logging. Find the block you added:
```csharp
_logger.LogInformation(
    "ModuleScopeApplied | Modules: [{Modules}] | Confidence: {Confidence}",
    string.Join(", ", scopeResult.Modules), scopeResult.Confidence);
```

Add after it (fire-and-forget, do not await to avoid latency):
```csharp
// Fire-and-forget audit log (do not block pipeline)
_ = Task.Run(async () =>
{
    try
    {
        var auditParams = new Dictionary<string, object?>
        {
            ["@ConversationId"] = ctx.ConversationId,
            ["@TenantId"] = ctx.TenantId,
            ["@UserId"] = ctx.UserId,
            ["@UserQuery"] = promptInput.Length > 2000 ? promptInput[..2000] : promptInput,
            ["@ResolvedModules"] = System.Text.Json.JsonSerializer.Serialize(scopeResult.Modules),
            ["@Confidence"] = scopeResult.Confidence,
            ["@Reasons"] = scopeResult.Reasons.Count > 0 ? string.Join("; ", scopeResult.Reasons) : null,
            ["@ToolCount"] = tools.Count
        };
        // Agent: use the ISqlExecutor instance if available in ChatPipeline,
        // or create a direct SQL call similar to existing observability patterns.
        // Check how _conversationStore saves data for the pattern to follow.
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to log module scope audit.");
    }
}, CancellationToken.None);
```

**Agent NOTE:** Check how `IConversationStore` or `ISqlExecutor` is used in ChatPipeline for the exact SQL execution pattern to reuse. Do not introduce a new dependency if possible.

## 12.2 Acceptance Criteria

- **AC-10:** `SELECT TOP 10 * FROM dbo.ModuleScopeLog ORDER BY CreatedAtUtc DESC` returns audit records after chat requests

---

---

# PACK 13: P2-02 — ReAct Follow-Up Policy Configuration

**Priority:** P2 POLISH
**Estimated time:** 15 minutes
**Files to modify:** 1 file

## 13.1 Steps

### Step 1: Add ReAct policy to `appsettings.json`

**File:** `src/TILSOFTAI.Api/appsettings.json`

Add a new section after the `Chat` section:

```json
"ReActPolicy": {
    "EnforceFollowUp": true,
    "MaxIdleStepsBeforeFollowUp": 1,
    "FollowUpRules": {
        "model_get_overview": {
            "condition": "PieceCount > 0",
            "followUpTool": "model_get_pieces"
        }
    }
}
```

**NOTE:** This is configuration-only in this pack. The enforcement logic in ChatPipeline is a future enhancement. The configuration allows the team to define rules that can be read by the LLM through a context pack or by C# logic in the future.

## 13.2 Acceptance Criteria

- Configuration parses without errors at startup
- No runtime behavior change (configuration-only for now)

---

---

# PACK 14: P2-03 — Scope Fallback Mechanism

**Priority:** P2 POLISH
**Estimated time:** 20 minutes
**Files to modify:** 1 file

## 14.1 Context

When the LLM requests a tool that is NOT in the scoped tool set, `ToolGovernance.Validate()` returns `IsValid = false`. The pipeline returns an error. Instead, the system should:
1. Log a warning
2. Re-run `ModuleScopeResolver` with a hint about the requested tool
3. Widen the scope and retry

## 14.2 Steps

### Step 1: Add fallback logic in ChatPipeline

In the ReAct loop inside `ChatPipeline.RunAsync`, find the tool validation section:

```csharp
var validation = _toolGovernance.Validate(call, toolLookup, ctx);
if (!validation.IsValid || validation.Tool is null)
{
```

Add scope fallback BEFORE the error return. Replace the entire validation block with:

```csharp
var validation = _toolGovernance.Validate(call, toolLookup, ctx);
if (!validation.IsValid || validation.Tool is null)
{
    // Scope fallback: if tool not in scoped set, try widening scope
    if (resolvedModules is { Count: > 0 } 
        && _moduleScopeResolver is not null
        && string.Equals(validation.Code, ErrorCode.ToolNotFound, StringComparison.OrdinalIgnoreCase))
    {
        _logger.LogWarning(
            "ScopeFallback | ToolName: {ToolName} not in scoped set [{Modules}]. Re-resolving scope.",
            call.Name, string.Join(", ", resolvedModules));

        // Re-resolve with hint
        var fallbackScope = await _moduleScopeResolver.ResolveAsync(
            $"{promptInput} [tool_requested: {call.Name}]", ctx, ct);

        if (fallbackScope.Modules.Count > resolvedModules.Count)
        {
            // Widen scope: reload tools with expanded modules
            if (_toolCatalogResolver is IScopedToolCatalogResolver scopedFallback)
            {
                tools = await scopedFallback.GetScopedToolsAsync(fallbackScope.Modules, ct);
                toolLookup = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
                resolvedModules = fallbackScope.Modules;

                // Re-validate with widened scope
                validation = _toolGovernance.Validate(call, toolLookup, ctx);
            }
        }
    }

    // If still invalid after fallback, proceed with original error handling
    if (!validation.IsValid || validation.Tool is null)
    {
        if (string.Equals(validation.Code, ErrorCode.ToolArgsInvalid, StringComparison.OrdinalIgnoreCase))
        {
            throw new TilsoftApiException(
                ErrorCode.ToolArgsInvalid,
                400,
                detail: validation.Detail);
        }

        return Fail(
            request,
            validation.Error ?? "Tool validation failed.",
            validation.Code ?? ErrorCode.ToolValidationFailed,
            validation.Detail);
    }
}
```

**IMPORTANT:** The variable `resolvedModules` was declared in Pack 6 at the top of `RunAsync`. Verify it is still in scope here (it should be — it's a local variable in the same method).

## 14.3 Acceptance Criteria

- When LLM requests a tool outside the scoped set, the system widens scope instead of failing
- Warning log includes the tool name and original scope
- If widening still doesn't find the tool, original error handling applies

---

---

# VERIFICATION CHECKLIST (Run after all packs)

| # | Test | Command / Check | Expected |
|---|------|-----------------|----------|
| AC-01 | Model query loads only model tools | Log `ScopedToolsResolved` for "phân tích model ID 3" | `Modules: [model]`, no analytics_* tools |
| AC-02 | Metadata scoped | Log context pack size | Smaller than before scoping |
| AC-03 | tool_catalog never dropped | Assert in ContextPackBudgeter | Always present |
| AC-04 | ReAct depth ≥ 2 | Query model with PieceCount > 0 | `model_get_pieces` called |
| AC-05 | No hallucination | Query model with NULL fields | "không có dữ liệu" shown |
| AC-06 | No hardcoded passwords | `grep -r "Password=123" src/` | 0 results |
| AC-07 | Auth enforced | POST `/v1/chat/completions` without JWT | 401 |
| AC-08 | action_request_write always available | Assert in scoped tools | Present for any scope |
| AC-09 | Atomic catalogs seeded | `SELECT * FROM DatasetCatalog` | Has model rows |
| AC-10 | Scope decisions logged | `SELECT * FROM ModuleScopeLog` | Has records |

---

*End of Agent Remediation Instructions*
