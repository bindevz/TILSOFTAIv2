# TILSOFTAI

Enterprise-grade **C# Orchestration Platform** for multi-tenant, multi-module data tooling where **SQL Server is the primary data/AI execution engine** and **.NET acts as an orchestrator** (not a business-logic brain).

> Core principle: **C# coordinates** (routing, safety, validation, observability, caching) while **LLM decides** when/how to use tools and **SQL stored procedures execute** the actual domain/data operations.

---

## What this project is

TILSOFTAI is a modular orchestration platform that exposes chat endpoints (WebAPI + streaming) and provides a tool-calling runtime for multiple business domains (modules).  
A “Model” domain exists as an example node, but the architecture is designed to scale across domains without hardcoding domain logic in the orchestrator.

**Key capabilities**
- Multi-tenant + multi-language execution context.
- WebAPI non-stream chat, SSE streaming, SignalR streaming.
- OpenAI-compatible endpoint: `/v1/chat/completions` (non-stream + SSE stream).
- Tool-calling runtime with SQL-backed tools (`ai_*` stored procedures).
- Deterministic **Normalization Rules Engine** (SQL rules + Redis caching).
- Safe, catalog-driven **Atomic Execute Plan** for dynamic analytics (`ai_atomic_execute_plan`).
- SQL-based **observability** (conversation persistence, tool executions, error logs).
- Prompt budgeting to prevent token overflow / schema truncation issues.

---

## Architecture (High-level)

```
Client (Web / App / SDK)
  -> API (Controllers/Hubs)
     -> ExecutionContextMiddleware (tenant/user/roles/lang/correlation ids)
     -> ChatPipeline (orchestration)
        -> PromptBuilder (budgeted system prompt + context packs)
        -> ILlmClient (OpenAI-compatible transport)
        -> ToolRegistry + ToolHandlers
            -> SQL Executor -> dbo.ai_* stored procedures
        -> Observability (optional): store conversation / tools / errors
     -> Return: JSON response / SSE / SignalR events
```

### Strict separation of responsibilities
- **C# MUST NOT infer or hardcode domain intelligence.**
  - No guessing business logic.
  - No “smart SQL generation” beyond validated catalog-driven templates.
- **LLM decides** (tool selection, filters, plan construction) via tools, with safety validation.
- **SQL executes** all tool work and returns **JSON envelope** (meta/columns/rows) so C# does not need hardcoded result classes.

---

## Authentication & Execution Context

### JWT-First Identity Resolution

All API endpoints require JWT Bearer authentication. Identity is resolved **claims-first**:

**JWT Claims** (primary source):
- **Tenant**: configured via `Auth:TenantClaimName` (e.g., `tid` or `http://schemas.company.com/tenant`)
- **User**: configured via `Auth:UserIdClaimName` (e.g., `sub` or `uid`)  
- **Roles**: configured via `Auth:RoleClaimName` (e.g., `roles`, supports CSV)

**Header Fallback** (Development only):
- `X-Tenant-Id` and `X-User-Id` headers are accepted ONLY in Development mode with `Auth:AllowHeaderFallback=true`
- OR when request has trusted gateway claim (`Auth:TrustedGatewayClaimName`)
- **Production**: Claims take precedence; mismatched headers trigger spoof detection

**Standard Headers** (always accepted):
- `X-Correlation-Id` (generated if missing, used for distributed tracing)
- `X-Conversation-Id` (generated if missing; stable per conversation)
- `X-Lang` (preferred language; overrides `Accept-Language`, defaults to `Localization:DefaultLanguage`)

**SignalR Context Isolation**:
- Hub invocations use per-invocation context from JWT claims via `ExecutionContextHubFilter`.
- **CRITICAL**: Tenant (`tid`) and User (`sub`) claims are REQUIRED. Missing claims result in immediate disconnection (401/403).
- Context is reset after every invocation to prevent AsyncLocal bleed.

**Context fields**:
- `TenantId`, `UserId`, `Roles[]`
- `CorrelationId`, `ConversationId`, `RequestId`, `TraceId`
- `Language` (stabilizes answer language)

---

## API Endpoints

### 1) Platform Chat (recommended)

#### Non-stream
- `POST /api/chat`
- JWT Bearer auth required

#### SSE Stream
- `POST /api/chat/stream`
- `Content-Type: text/event-stream`
- Events: `delta`, `tool_call`, `tool_result`, `final`, `error`

#### SignalR
- Hub: `/hubs/chat`
- Method: `StartChat({ input, allowCache, containsSensitive })`
- Server pushes: `chat_event`

### 2) OpenAI-compatible endpoint
- `POST /v1/chat/completions`
- Supports non-stream JSON and stream SSE (`data: ...`, ending with `data: [DONE]`)
- Internally calls **ChatPipeline** (same orchestration as platform chat)
- **Does not leak internal tool payloads** into OpenAI stream output by default.

### 3) Health Endpoints

**Liveness** (always returns 200 if process is running):
- `GET /health/live`
- No checks performed
- Anonymous access allowed

**Readiness** (checks dependencies):
- `GET /health/ready`
- Checks: SQL (always), Redis (only if `Redis:Enabled=true`)
- Anonymous access allowed
- Returns 200 if all checks pass, 503 otherwise

Both endpoints are exempt from rate limiting.

---

## Request Size Limits & DoS Protection

**Request body size enforcement**:
- Limit: `Chat:MaxRequestBytes` (default: 256KB)
- Enforced at:
  1. Kestrel server level (`MaxRequestBodySize`)
  2. Middleware level (Content-Length + chunked request support)
- Routes protected: `/api/chat`, `/api/chat/stream`, `/v1/chat/completions`
- Error response: `413 REQUEST_TOO_LARGE`

**Additional limits**:
- `Chat:MaxMessages` (default: 50)
- `Chat:MaxInputChars`
- `Chat:MaxToolCallsPerRequest`

---

## Streaming Design (Enterprise-hardening)

Streaming is implemented using a **bounded channel** pattern:
- Progress callback **only enqueues** events.
- A single async writer loop reads from the channel and writes SSE/SignalR.
- Backpressure policy:
  - If channel is full, drop older `delta` events (optional)
  - Never drop `final` or `error`

This avoids thread pool starvation and eliminates `GetAwaiter().GetResult()` in streaming paths.

**Streaming error envelope**:

When an error occurs during streaming, an `error` event is sent:

```json
{
  "type": "error",
  "correlationId": "...",
  "error": {
    "code": "ERROR_CODE",
    "message": "Localized error message",
    "detail": { ... }
  }
}
```

SignalR errors follow the same envelope shape in `chat_event` with `type: "error"`.

---

## Error System & Observability

### Unified error envelope
All API errors are returned in a consistent envelope:
- `success=false`
- `error: { code, message, detail }`
- Includes: `correlationId`, `conversationId`, `traceId`, `language`

### Multi-language error catalog
- EN + VI supported with fallback:
  - context.Language -> Localization.DefaultLanguage -> `en`

### SQL observability (optional)
When enabled, the system writes to SQL:
- `Conversation`
- `ConversationMessage`
- `ToolExecution`
- `ErrorLog`

Stored procedures (`app_*`) are used to write data:
- `dbo.app_conversation_upsert`
- `dbo.app_conversationmessage_insert`
- `dbo.app_toolexecution_insert`
- `dbo.app_errorlog_insert`

---

## SQL Tooling Conventions

### Naming rules
- **Model-callable tools:** stored procedures start with `dbo.ai_*`
- **Internal platform procedures:** start with `dbo.app_*`
- All in `dbo` schema.

### JSON envelope output (required)
`ai_*` procedures must return results as a **JSON envelope**:
- `meta` (tenantId, generatedAtUtc, rowCount, datasetKey/plan, warnings...)
- `columns` (name/type/descriptionKey)
- `rows` (array of objects keyed by columns[].name)

This enables schema-less tool consumption in C#.

---

## Atomic Engine (Catalog-driven analytics)

The `ai_atomic_execute_plan` procedure executes a validated JSON plan over whitelisted datasets:
- Uses DatasetCatalog + FieldCatalog + EntityGraphCatalog (one join supported)
- Enforces:
  - whitelist-only columns
  - parameterized predicates
  - tenant scoping (must be guaranteed, otherwise reject)
  - limit/offset caps

C# validates plan shape (no free-form SQL fragments) and sends plan JSON to SQL.

---

## Normalization Engine (Deterministic)

Purpose: normalize user input deterministically (no inference):
- Rules stored in SQL: `NormalizationRule`
- Loaded via `dbo.app_normalizationrule_list`
- Cached in Redis (>= 30 minutes) per tenant
- Includes season/year expansions:
  - `24/25 -> 2024/2025`
  - `99/00 -> 1999/2000`
  - `25/26 -> 2025/2026`

---

## Redis Caching

Redis is used for:
- Baseline fast lookup caches (e.g., normalization rules)
- Temporary computation caches (minimum TTL 30 minutes)

If Redis is disabled, the system falls back to in-memory caching with TTL semantics.

---

## Tenant Override Schema Hardening (Patch 12–14)

Many tables use `TenantId NULL` as “global row” semantics.  
SQL Server uniqueness with NULL requires special handling.

**Final approach**
- Use `Id BIGINT IDENTITY` as PK
- Use **filtered unique indexes** to enforce deterministic uniqueness:
  - `TenantId IS NULL` unique on global keys
  - `TenantId IS NOT NULL` unique on tenant keys

Tables hardened:
- `MetadataDictionary`
- `NormalizationRule`
- `DatasetCatalog`
- `DiagnosticsRule` (business uniqueness includes `Module`)

Contract tests enforce:
- No PK includes nullable columns
- Required filtered unique indexes exist

---

## Configuration & Secrets

### Overview

The project uses ASP.NET Core's layered configuration system. **Never commit real secrets to the repository.**

**Configuration sources (in order of precedence)**:
1. `appsettings.json` - **Non-secret defaults only** (committed to repo)
2. `appsettings.Local.json` - **Local override file** (gitignored, optional)
3. Environment variables - **Production secret injection** (recommended for deployments)
4. Command-line arguments

### Local Development Secrets

For local development, you have two options:

#### Option 1: Local Override File (Recommended for Development)

Create `src/TILSOFTAI.Api/appsettings.Local.json` and add your secrets:

```json
{
  "Sql": {
    "ConnectionString": "Server=localhost;Database=TILSOFTAI;Integrated Security=true;TrustServerCertificate=True;"
  },
  "Auth": {
    "JwksUrl": "https://your-auth-server/.well-known/jwks.json"
  },
  "Llm": {
    "Provider": "OpenAiCompatible",
    "Endpoint": "https://api.openai.com/v1/chat/completions",
    "ApiKey": "sk-your-actual-api-key-here"
  },
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379"
  }
}
```

This file is **automatically ignored by git** and will override values from `appsettings.json`.

#### Option 2: Environment Variables (Recommended for Production)

Use ASP.NET Core's environment variable mapping with double-underscore (`__`) as the hierarchy separator:

**Examples**:
```bash
# Windows (PowerShell)
$env:Sql__ConnectionString = "Server=prod-sql;Database=TILSOFTAI;..."
$env:Auth__JwksUrl = "https://auth.company.com/.well-known/jwks.json"
$env:Llm__ApiKey = "sk-prod-key"
$env:Redis__ConnectionString = "redis-prod:6379"

# Linux/macOS (Bash)
export Sql__ConnectionString="Server=prod-sql;Database=TILSOFTAI;..."
export Auth__JwksUrl="https://auth.company.com/.well-known/jwks.json"
export Llm__ApiKey="sk-prod-key"
export Redis__ConnectionString="redis-prod:6379"
```

**Docker/Kubernetes**: Pass environment variables via container orchestration (`-e` flags, ConfigMaps, Secrets).

### Sample Configuration

A sample configuration file with placeholders is available at `src/TILSOFTAI.Api/appsettings.Sample.json`. Use this as a reference when creating your local override file.

---

## Configuration

All settings are centralized in `appsettings*.json` and bound to Options classes.
**Configuration is the Single Source of Truth**:
- Code consumes strictly `IOptions<T>` / `IOptionsMonitor<T>`.
- Manual `configuration.GetSection()` is prohibited in application code (allowed only in `Program.cs` startup).
- All Options are validated at startup (`ValidateOnStart`).

Common sections:
- `Chat` (limits, compaction)
- `Llm` (Provider, Endpoint, ApiKey, Model, Temperature, MaxResponseTokens)
- `Redis` (enabled, connection, DefaultTtlMinutes >= 30)
- `Observability` (EnableConversationPersistence, EnableSqlToolLog, EnableSqlErrorLog)
- `Atomic` (MaxLimit, rules)
- `Localization` (DefaultLanguage, SupportedLanguages)
- `Streaming` (ChannelCapacity, DropDeltaWhenFull)

> **Security**: Never commit API keys, connection strings with passwords, or other secrets to the repository. Use `appsettings.Local.json` for local development or environment variables for production deployments.

---

## Getting Started (Local)

### Prerequisites
- .NET 10 SDK
- SQL Server 2025 (or compatible SQL Server for development)
- Redis (optional, recommended)

### 1) Configure
- Set `Llm.Provider=Null` for offline/dev, or `OpenAiCompatible` to use a real endpoint.
- Configure `Llm.Endpoint`, `Llm.ApiKey`, `Llm.Model`.
- Configure `Localization.SupportedLanguages` as needed (e.g., `["en","vi"]`).

### 2) Database setup
Run SQL scripts in your deployment order (core -> modules -> migrations -> seeds).  
For Patch 14 (one-file-per-run), recommended DB execution order:
1. `sql/01_core/008_migration_metadata_dictionary_to_v4.sql`
2. `sql/01_core/011_migration_normalization_rule_to_v4.sql`
3. `sql/02_atomic/004_migration_datasetcatalog_to_v2.sql`
4. `sql/04_diagnostics/003_migration_diagnosticsrule_to_v2.sql`
5. `sql/04_diagnostics/002_sps_diagnostics.sql`

### Dev seeds (optional)
For local testing only (NOT production), run the example Model seeds:
- `sql/98_dev_seeds/model/001_seed_model_demo.sql`
- `sql/98_dev_seeds/model/002_seed_metadata_dictionary_model.sql`

The Model module is an example domain; these seeds are demo data only.

### 3) Run API
Run `src/TILSOFTAI.Api`.

---

## Example Requests

### /api/chat
```bash
curl -X POST "https://localhost:5001/api/chat"   -H "Authorization: Bearer <token>"   -H "Content-Type: application/json"   -H "X-Tenant-Id: T1"   -H "X-User-Id: U1"   -H "X-Lang: vi"   -d '{ "input": "How many models are in the system?", "allowCache": true }'
```

### /v1/chat/completions (OpenAI compatible)
```bash
curl -X POST "https://localhost:5001/v1/chat/completions"   -H "Authorization: Bearer <token>"   -H "Content-Type: application/json"   -H "X-Tenant-Id: T1"   -H "X-User-Id: U1"   -d '{
    "model": "ignored-by-platform",
    "stream": false,
    "messages": [
      { "role": "user", "content": "hello" }
    ]
  }'
```

---

---

## Testing & Headers

### Contract Tests
Set environment variable:
- `TILSOFTAI_TEST_CONNECTION` (SQL connection string)

Contract tests include:
- Required SQL objects exist (ai_/app_ procedures, tables)
- Schema lint (PKs, indexes)

### Test-Only Headers
The test suite (in `TILSOFTAI.Tests.Contract`) uses a custom authentication handler (`TestAuthHandler`).
- **Authorization: Test <token>**
- Injected via `TestWebApplicationFactory`.
- These headers and schemes are **NOT available in Production** (removed by compilation directives or separate startup config).
- Production MUST use standard JWT Bearer tokens.

---

## Contribution Guidelines (non-negotiable)

1) **No hardcoded domain intelligence** in C#.
2) Tools must be **catalog-driven** and executed via **SQL ai_* procedures**.
3) All tool outputs must return the **JSON envelope** format.
4) Migrations must be **idempotent** and **upgrade-safe**.
5) Keep system prompt small; tool instructions travel via tool descriptions and tool schemas, not via huge system prompt blocks.
6) Preserve multi-tenant isolation:
   - If tenant scoping cannot be guaranteed, the system must reject execution (fail safe).

---

## Roadmap (typical next steps)
- Expand additional modules (domain packs) using the same patterns.
- Add SQL Server 2025 AI-native features where applicable (vector search/semantic indexing) as optional tools, still behind `ai_*` procedures.
- Add more diagnostics and schema linters to prevent drift.

---

## Module Template
See `docs/module_template.md` and the starter SQL template in `sql/90_template_module/`.

--- 

## License
Internal / proprietary (define your company policy here).
