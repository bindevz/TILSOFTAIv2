# Sprint 34 — Model-Only Official Agent Runtime Hardening

**Audience:** Codex Agent / implementation agent  
**Repository:** `bindevz/TILSOFTAIv2`  
**Baseline commit:** `957738f415cecb61429fc3cd3205c1fc68c32006`  
**Primary goal:** make the current framework run for real through the API using an official Microsoft Agent Framework runtime and a local AI provider setting, focused only on the `model` read-only domain.

---

## 0. CTO Directive

Sprint 34 is not a feature-expansion sprint. It is a runtime-hardening sprint.

The project has already moved toward official Microsoft Agent Framework in Sprint 33. Sprint 34 must now remove noise, reduce scope, and prove the framework can run end-to-end against one real read-only ERP domain: `model`.

Do not expand to Purchasing, Sales, Warehouse, Accounting, or any write-heavy business domain in this sprint.

The required target is:

```text
API request
  -> official Microsoft Agent Framework route
  -> local AI provider selected by settings
  -> candidate-gated model-domain tools only
  -> official agent chooses tool + arguments
  -> CapabilityExecutionFacade validates and executes
  -> SQL ai_* stored procedure returns rows
  -> AnswerComposer returns RawJson or Structured output
```

The sprint must also standardize the write-preview conversation state infrastructure, but do not enable real write actions in production runtime yet.

---

## 1. External Technical References

Use official Microsoft Agent Framework concepts and APIs. Do not invent replacement abstractions that pretend to be Agent Framework.

References:

- Microsoft Agent Framework overview: https://learn.microsoft.com/en-us/agent-framework/overview/
- Microsoft Agent Framework function tools: https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools
- Microsoft Agent Framework OpenAI provider: https://learn.microsoft.com/en-us/agent-framework/agents/providers/openai
- `AIFunctionFactory.Create(...)`: https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.aifunctionfactory.create

Important principle:

```text
Microsoft Agent Framework is the agent/tool-calling brain.
SQL Server + CapabilityExecutionFacade are the ERP safety boundary.
AnswerComposer is the response/output policy layer.
```

---

## 2. Sprint 34 Goals

### Goal 1 — Focus the architecture

Remove or disable non-essential routing and domain complexity so the framework is easy to reason about.

For Sprint 34, only the `model` domain is active.

### Goal 2 — Run through API using local AI by settings

The system must be able to run through API using local AI settings, without changing source code.

The runtime must still use the official Microsoft Agent Framework path. Local AI is a provider configuration, not a custom tool-routing brain.

### Goal 3 — Run one real read-only domain

The active business domain is:

```text
model
```

The active tools should be limited to read-only `model` capabilities such as model overview, pieces, materials, compare, and count.

### Goal 4 — Standardize AnswerComposer

AnswerComposer must support exactly two production modes in Sprint 34:

```text
RawJson
Structured
```

RawJson must not call the LLM after tool execution.

Structured mode may use deterministic composition and, optionally, a summary LLM step only after masking sensitive data.

### Goal 5 — Standardize write-preview conversation state

Implement the conversation-state layer needed for future write preview confirmation:

```text
preview -> pending action -> user confirmation -> approval -> execute
```

For Sprint 34, implement the infrastructure and tests. Do not enable real business write tools in the default runtime.

---

## 3. Non-Goals

Do not do these in Sprint 34:

```text
- Do not add Purchasing tools.
- Do not add Sales tools.
- Do not add Warehouse tools.
- Do not add Accounting tools.
- Do not enable sales_order_create or any real write action in normal runtime.
- Do not optimize SQL Server vector search yet unless needed by existing code.
- Do not build a multi-agent graph.
- Do not create a new framework abstraction that replaces Microsoft Agent Framework.
- Do not use regex or keyword logic as the main tool/argument selector.
- Do not send all ERP tools to the model.
```

Regex may only remain as hard-signal extraction for deterministic codes, dates, numbers, or formatting hints. It must not select the final tool or business arguments.

---

## 4. Non-Negotiable Rules

### 4.1 Official Agent Framework only

The official runtime path must use:

```text
Microsoft.Agents.AI
Microsoft.Extensions.AI
AIAgent / AsAIAgent(...)
AIFunction / AIFunctionFactory.Create(...) or an official AIFunction-compatible implementation
```

Forbidden:

```text
- CandidateGatedToolCallingAgent custom fake agent
- FirstOrDefault tool selector
- custom OpenAI tool-call loop as the production brain
- regex-based argument extraction as the production brain
- generic execute_tool(capability_key, args_json) as the only model-facing function
```

### 4.2 Candidate-gated tools only

The agent must never see the entire ERP tool catalog.

For Sprint 34:

```text
AllowedDomains = ["model"]
MaxCandidateTools = 6
```

### 4.3 Tool execution boundary

Agent tools must never call SQL directly.

Every tool callback must go through:

```text
CapabilityExecutionFacade
```

The facade must continue to enforce:

```text
- capability exists
- domain is allowed
- read-only execution mode for active model tools
- tenant/user/role policy
- argument contract validation
- SQL stored procedure whitelist
- audit/trace metadata
```

### 4.4 AnswerComposer owns final output

The agent may choose tools and provide arguments.

The agent must not be the only final-output policy layer after tool execution.

Final response must be produced by:

```text
IAnswerComposer
```

### 4.5 Write actions are preview-only infrastructure

No production write action is enabled by default in Sprint 34.

Write-preview conversation state must be implemented, but real execute-write requires explicit approval and must remain disabled unless a controlled test capability is used.

---

## 5. Target Runtime Architecture

```text
API
  -> SupervisorRuntime or Chat entrypoint
  -> OfficialAgentToolRouter
      -> load settings
      -> allowed domain gate: model only
      -> select model candidate tools
      -> build official AIFunction tools
      -> create official AIAgent with local AI provider
      -> agent chooses function + arguments
  -> CapabilityExecutionFacade
      -> validate/policy/execute
  -> SqlToolAdapter / model ai_* stored procedures
  -> AnswerComposer
      -> RawJson or Structured
  -> API response
```

---

## 6. Workstream A — Architecture Cleanup

### A.1 Remove fake or redundant agent code

Search for and remove or hard-disable any previous fake Agent Framework implementation.

Forbidden names/patterns:

```text
CandidateGatedToolCallingAgent
selected = tools.FirstOrDefault()
ExtractArgumentValue(...)
Regex-based tool selection
manual OpenAI tool-call parser as production brain
```

If any legacy file must remain for compatibility, it must satisfy all of the following:

```text
- not registered in DI for Sprint 34 runtime
- not reachable from API official path
- clearly marked as Legacy/Fallback
- covered by an architecture guard test that production config cannot use it
```

### A.2 Remove non-model domains from active runtime

For Sprint 34, non-model domains must not be active.

Allowed:

```text
model
```

Disallowed in active runtime:

```text
purchasing
sales
warehouse
accounting
product_model if it is used as a separate domain name
```

Implementation options:

```text
Option 1: Remove non-model seed rows from Sprint 34 active seed scripts.
Option 2: Keep the rows but mark IsEnabled = 0.
Option 3: Keep existing tables but enforce AllowedDomains = ["model"] in candidate selection and tests.
```

Preferred Sprint 34 choice:

```text
Keep schema extensibility, but disable non-model capabilities at runtime.
```

Do not delete reusable framework abstractions such as `CapabilityDescriptor`, `CapabilityExecutionFacade`, `AnswerComposer`, or adapter interfaces.

### A.3 Disable legacy fallback for integration config

For the Sprint 34 integration profile:

```json
{
  "AiRouting": {
    "MicrosoftAgentFrameworkRoutingEnabled": true,
    "UseOfficialMicrosoftAgentFramework": true,
    "FallbackToLegacyPipeline": false,
    "AllowedDomains": ["model"],
    "MaxCandidateTools": 6
  }
}
```

If the official agent route fails, the API must fail with a clear diagnostic response. It must not silently fallback to keyword routing.

---

## 7. Workstream B — API Runtime with Local AI via Settings

### B.1 Add a dedicated local AI settings profile

Create or update configuration so local AI can be selected without source-code changes.

Required config shape:

```json
{
  "AiRouting": {
    "MicrosoftAgentFrameworkRoutingEnabled": true,
    "UseOfficialMicrosoftAgentFramework": true,
    "Provider": "OpenAiCompatibleLocal",
    "FallbackToLegacyPipeline": false,
    "AllowedDomains": ["model"],
    "MaxCandidateTools": 6,
    "ToolCallingRequired": true
  },
  "LocalAi": {
    "BaseUrl": "http://localhost:11434/v1",
    "ChatCompletionsPath": "/chat/completions",
    "Model": "CHANGE_ME_TOOL_CALLING_MODEL",
    "ApiKeyEnvironmentVariable": "TILSOFTAI_LOCAL_AI_API_KEY",
    "TimeoutSeconds": 120
  }
}
```

Do not hardcode:

```text
- internal IP addresses
- API keys
- SQL passwords
- model names that only exist on one machine
```

### B.2 Provider factory requirements

`OfficialAgentProviderFactory` must support the configured local provider through an official `IChatClient` -> `AsAIAgent(...)` path.

Allowed pattern:

```text
settings -> IChatClient -> AsAIAgent(... tools ...)
```

Forbidden pattern:

```text
settings -> custom HTTP client -> manual parse tool_calls -> execute tool
```

### B.3 Tool-calling capability check

Add a startup or health check that verifies the configured local model is suitable for tool calling.

At minimum, health check must report:

```text
- official agent routing enabled: true/false
- provider name
- base URL source: configured/env/local only, no secret printed
- model name
- allowed domains
- max candidate tools
- fallback enabled/disabled
- can create IChatClient
- can create AIAgent
- tool calling configured
```

Preferred:

```text
Add a dev-only smoke test that exposes one simple model-domain tool and verifies the agent invokes it.
```

### B.4 API endpoints to validate

Use existing API entrypoints. Do not create a new API surface unless required.

Expected smoke flow:

```text
POST /api/chat or existing chat endpoint
POST /v1/chat/completions if it already exists and is wired to SupervisorRuntime
```

Required test cases:

```text
1. Vietnamese: "Có bao nhiêu model?"
2. English: "How many models are active?"
3. Vietnamese: "Cho tôi xem thông tin model ABC"
4. English: "Show materials for model ABC"
5. Missing argument: "Cho tôi xem thông tin model"
```

---

## 8. Workstream C — Model Domain Read-Only Runtime

### C.1 Canonical domain name

Sprint 34 canonical domain is:

```text
model
```

Do not split into:

```text
product_model
model_product
```

If existing code uses `product_model`, create a temporary alias but normalize to `model` before candidate selection.

### C.2 Active read-only model capabilities

Use only read-only model capabilities.

Recommended active tools:

```text
model_count
model_get_overview
model_get_pieces
model_get_materials
model_compare_models
model_get_packaging
```

If the repository already has different exact names, normalize them to a single convention and update tests accordingly.

Preferred capability key convention:

```text
model.count
model.overview.by-code
model.pieces.by-code
model.materials.by-code
model.compare
model.packaging.by-code
```

Preferred model-facing function name convention:

```text
model_count
model_get_overview
model_get_pieces
model_get_materials
model_compare_models
model_get_packaging
```

### C.3 Active SQL procedure convention

All model tools must call `ai_*` stored procedures only.

Recommended stored procedure names:

```text
ai_model_count
ai_model_get_overview
ai_model_get_pieces
ai_model_get_materials
ai_model_compare_models
ai_model_get_packaging
```

Do not allow agent tools to call views or raw SQL directly.

Views may be used inside the stored procedures.

### C.4 Candidate selector behavior

For Sprint 34, candidate selector must behave simply and predictably:

```text
Input message
  -> AllowedDomains filter = model
  -> retrieve enabled model capabilities only
  -> rank by dynamic descriptions/examples/aliases if available
  -> return max 6 tools
```

If the selector cannot confidently identify a tool, it should still provide the small model tool set to the official agent and let the agent decide.

The final tool selection must be done by Microsoft Agent Framework, not by regex.

### C.5 Required model-domain descriptions

Descriptions must be loaded from metadata or centralized catalog. Avoid hardcoding descriptions inside C# attributes for production.

Each active model capability must have:

```text
- English description
- Vietnamese description
- positive usage guidance
- negative usage guidance
- example utterances
- argument descriptions
- result schema
- answer policy
```

Example:

```json
{
  "capabilityKey": "model.overview.by-code",
  "domain": "model",
  "functionName": "model_get_overview",
  "description": {
    "en-US": "Get overview information for a product model by model code.",
    "vi-VN": "Tra cứu thông tin tổng quan của model theo mã model."
  },
  "useWhen": {
    "en-US": "Use when the user asks for model information, model detail, product model overview.",
    "vi-VN": "Dùng khi người dùng hỏi thông tin model, chi tiết model, tổng quan sản phẩm."
  },
  "doNotUseWhen": {
    "en-US": "Do not use for sales orders, purchasing, inventory, or accounting.",
    "vi-VN": "Không dùng cho đơn bán, mua hàng, tồn kho hoặc kế toán."
  }
}
```

---

## 9. Workstream D — AnswerComposer Standardization

### D.1 Required interface

Ensure there is a single production-facing composer abstraction:

```csharp
public interface IAnswerComposer
{
    Task<AssistantAnswer> ComposeAsync(
        AnswerComposerRequest request,
        CancellationToken cancellationToken);
}
```

Required request shape:

```csharp
public sealed record AnswerComposerRequest
{
    public required AnswerMode Mode { get; init; }
    public required string CapabilityKey { get; init; }
    public required string? ProcedureName { get; init; }
    public required IReadOnlyDictionary<string, object?> Arguments { get; init; }
    public required ResultSchema? ResultSchema { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public required int RowCount { get; init; }
    public required ExecutionMetadata ExecutionMetadata { get; init; }
    public required SensitivityPolicy SensitivityPolicy { get; init; }
    public required string Locale { get; init; }
    public string? ClarificationQuestion { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
```

Required modes:

```csharp
public enum AnswerMode
{
    RawJson,
    Structured
}
```

Remove or disable extra modes until Sprint 34 is stable.

### D.2 RawJson mode

RawJson mode must not call the LLM after tool execution.

Output shape:

```json
{
  "mode": "raw_json",
  "capabilityKey": "model.overview.by-code",
  "procedureName": "ai_model_get_overview",
  "arguments": {
    "modelCode": "ABC"
  },
  "rowCount": 1,
  "rows": [],
  "resultSchema": {},
  "executionMetadata": {
    "durationMs": 123,
    "correlationId": "...",
    "executedAtUtc": "..."
  },
  "provenance": {
    "domain": "model",
    "tool": "model_get_overview"
  }
}
```

Rules:

```text
- apply sensitivity policy before output
- include execution metadata
- include capability/procedure provenance
- no LLM summary
- deterministic output
```

### D.3 Structured mode

Structured mode returns a stable block model suitable for frontend rendering.

Required output shape:

```json
{
  "mode": "structured",
  "answerType": "summary_then_table",
  "text": "...",
  "blocks": [
    {
      "type": "summary",
      "content": "..."
    },
    {
      "type": "table",
      "columns": [],
      "rows": []
    }
  ],
  "followUpQuestions": [],
  "provenance": {
    "capabilityKey": "...",
    "rowCount": 0,
    "correlationId": "..."
  }
}
```

Decision rules:

```text
if ClarificationQuestion exists:
  -> answerType = follow_up_question
  -> do not call SQL

if ErrorCode exists:
  -> answerType = error
  -> include correlationId

if RowCount = 0:
  -> answerType = no_data
  -> include filters used

if RowCount <= 20:
  -> answerType = summary_then_table
  -> include table block

if RowCount > 20:
  -> answerType = summary_with_top_rows
  -> include top rows and suggest filter/export

if ResultSchema has date/time + numeric measure:
  -> include chart candidate block

if SensitivityPolicy masks fields:
  -> mask before table, summary, and raw JSON
```

### D.4 Model-domain result schemas

Each active model tool must have a result schema.

Minimum result schema metadata:

```text
- column name
- label en-US
- label vi-VN
- type
- role: dimension | measure | identifier | date | text
- visibility
- sensitivity
```

Example:

```json
{
  "columns": [
    {
      "name": "ModelCode",
      "label": {
        "en-US": "Model Code",
        "vi-VN": "Mã model"
      },
      "type": "string",
      "role": "identifier",
      "visible": true
    },
    {
      "name": "ModelName",
      "label": {
        "en-US": "Model Name",
        "vi-VN": "Tên model"
      },
      "type": "string",
      "role": "text",
      "visible": true
    }
  ]
}
```

### D.5 Request-level response mode

API must allow response mode selection.

Preferred precedence:

```text
1. Explicit request responseMode
2. Capability answer policy
3. Tenant/user default
4. System default = Structured
```

Supported request values:

```text
raw_json
structured
```

---

## 10. Workstream E — Write Preview Conversation State

Sprint 34 must standardize the state layer. Do not enable real write business tools by default.

### E.1 Required state model

Create a durable pending action model.

Suggested table:

```sql
CREATE TABLE ai.PendingAction
(
    PendingActionId      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId             NVARCHAR(100) NOT NULL,
    UserId               NVARCHAR(200) NOT NULL,
    ConversationId       NVARCHAR(200) NOT NULL,
    CapabilityKey        NVARCHAR(200) NOT NULL,
    FunctionName         NVARCHAR(200) NULL,
    PreviewArgumentsJson NVARCHAR(MAX) NOT NULL,
    PreviewResultJson    NVARCHAR(MAX) NULL,
    Status               NVARCHAR(50) NOT NULL,
    ApprovalId           NVARCHAR(200) NULL,
    CreatedAtUtc         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ExpiresAtUtc         DATETIME2 NOT NULL,
    ConfirmedAtUtc       DATETIME2 NULL,
    ExecutedAtUtc        DATETIME2 NULL,
    CancelledAtUtc       DATETIME2 NULL,
    CorrelationId        NVARCHAR(100) NULL
);
GO

CREATE INDEX IX_PendingAction_Conversation
ON ai.PendingAction(TenantId, UserId, ConversationId, Status, ExpiresAtUtc);
GO
```

Optional event table:

```sql
CREATE TABLE ai.PendingActionEvent
(
    PendingActionEventId BIGINT IDENTITY PRIMARY KEY,
    PendingActionId      UNIQUEIDENTIFIER NOT NULL,
    EventType            NVARCHAR(100) NOT NULL,
    EventJson            NVARCHAR(MAX) NULL,
    CreatedAtUtc         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
```

### E.2 Required service contracts

```csharp
public interface IPendingActionStore
{
    Task<PendingActionRecord> CreateAsync(
        PendingActionCreateRequest request,
        CancellationToken cancellationToken);

    Task<PendingActionRecord?> GetActiveForConversationAsync(
        string tenantId,
        string userId,
        string conversationId,
        CancellationToken cancellationToken);

    Task<PendingActionRecord?> GetByIdAsync(
        string tenantId,
        string userId,
        Guid pendingActionId,
        CancellationToken cancellationToken);

    Task MarkConfirmedAsync(
        Guid pendingActionId,
        string approvalId,
        CancellationToken cancellationToken);

    Task MarkExecutedAsync(
        Guid pendingActionId,
        CancellationToken cancellationToken);

    Task MarkCancelledAsync(
        Guid pendingActionId,
        CancellationToken cancellationToken);

    Task ExpireOldAsync(CancellationToken cancellationToken);
}
```

### E.3 Confirmation behavior

When a future preview tool is called:

```text
PreviewWriteAsync
  -> validates capability/arguments
  -> creates pending action
  -> returns pendingActionId and confirmation block
```

When user says:

```text
"xác nhận"
"confirm"
"ok tạo"
"đồng ý"
```

The system should:

```text
1. Look up active pending action by tenant/user/conversation.
2. If none exists, ask what the user wants to confirm.
3. If expired, ask user to create preview again.
4. If exactly one active pending action exists, confirm it.
5. Create or link approval through IApprovalEngine.
6. Execute only via ExecuteApprovedWriteAsync.
```

Regex may be used only as a simple confirmation phrase detector here, not as a business tool selector.

### E.4 Sprint 34 write scope

Do not enable real business write actions in normal runtime.

Allowed implementation:

```text
- pending action table/store
- confirmation resolver service
- unit/integration tests with a fake/test write capability
- AnswerComposer confirmation block
```

Disallowed in default runtime:

```text
- sales_order_create active tool
- purchasing_po_create active tool
- model create/update active tool unless it is a disabled test fixture
```

---

## 11. Workstream F — API Response Contract

The API must produce stable output.

### F.1 RawJson response

When `responseMode = raw_json`:

```text
- execute selected model tool
- do not request a final natural-language answer from local AI
- return raw JSON envelope
```

### F.2 Structured response

When `responseMode = structured`:

```text
- execute selected model tool
- pass execution result to AnswerComposer
- return blocks and text
```

### F.3 Clarification response

If required arguments are missing:

```text
- do not execute SQL
- return structured follow-up question
- include missing argument metadata when useful
```

Example:

```json
{
  "mode": "structured",
  "answerType": "follow_up_question",
  "text": "Bạn muốn xem thông tin cho mã model nào?",
  "blocks": [
    {
      "type": "follow_up_question",
      "missingArguments": ["modelCode"]
    }
  ]
}
```

---

## 12. Tests Required

### 12.1 Architecture guard tests

Add or update tests that fail if forbidden patterns return.

Required checks:

```text
- no CandidateGatedToolCallingAgent class is registered
- no FirstOrDefault tool selection in official route
- no regex-based business argument extraction in official route
- no direct SQL call from dynamic tool callback
- official agent route uses AIAgent/AsAIAgent path
- production/integration config has fallback disabled
```

### 12.2 Model-only domain tests

Required tests:

```text
- candidate selector returns only model tools
- non-model capabilities are ignored or disabled
- model tools count <= MaxCandidateTools
- missing modelCode leads to clarification, not SQL execution
- model_count can run without modelCode
```

### 12.3 Local AI provider tests

Use fake/stub provider for unit tests, but integration config must support real local AI.

Required tests:

```text
- provider factory builds an official agent for configured local provider
- health check reports provider/model/fallback/domain settings
- local provider path does not call manual OpenAI tool-loop code
```

### 12.4 AnswerComposer tests

Required tests:

```text
- RawJson mode does not call LLM summarizer
- RawJson includes capability/procedure/arguments/rows/metadata
- Structured no-data response includes filters
- Structured <=20 rows returns table block
- Structured >20 rows returns top rows or suggests filter/export
- sensitivity masking happens before output
- clarification request returns follow-up block
```

### 12.5 Pending action tests

Required tests:

```text
- create pending action
- get active pending action by tenant/user/conversation
- cannot confirm another user's pending action
- expired pending action cannot execute
- confirmation creates or links approval
- execute requires approvedActionId
- mark executed after successful write
```

### 12.6 API smoke tests

Add scripts or tests for:

```text
POST chat: "Có bao nhiêu model?"
POST chat: "How many models are active?"
POST chat: "Cho tôi xem thông tin model ABC"
POST chat: "Show materials for model ABC"
POST chat: "Cho tôi xem thông tin model" -> clarification
POST chat with responseMode=raw_json
POST chat with responseMode=structured
```

---

## 13. Suggested File/Folder Changes

Codex Agent should adapt names to the current repository structure, but keep the following intent.

### 13.1 API/config

```text
src/TILSOFTAI.Api/appsettings.json
src/TILSOFTAI.Api/appsettings.Development.json
src/TILSOFTAI.Api/appsettings.Local.example.json
src/TILSOFTAI.Api/Extensions/AddTilsoftAiExtensions.cs
src/TILSOFTAI.Api/Health/OfficialAgentFrameworkHealthCheck.cs
```

Required changes:

```text
- remove committed secrets/internal endpoints from appsettings.json
- add local AI settings profile
- configure official agent route
- disable legacy fallback in integration profile
- expose health diagnostics without leaking secrets
```

### 13.2 Agent routing

```text
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialAgentToolRouter.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialAgentProviderFactory.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialMicrosoftAgentRuntime.cs
src/TILSOFTAI.Orchestration/AiRouting/Tools/DynamicFunctionToolFactory.cs
src/TILSOFTAI.Orchestration/Semantic/SemanticCapabilityCandidateSelector.cs
```

Required changes:

```text
- official agent path only
- local provider supported via settings
- model-only candidate gate
- no fake framework
- no direct SQL in tools
```

### 13.3 Model domain

```text
src/TILSOFTAI.Modules.Model/
sql/ai/model/*.sql or existing sql seed path
```

Required changes:

```text
- model-only active capability seed
- read-only ai_model_* procedures
- model result schemas
- model multilingual descriptions/examples
```

### 13.4 AnswerComposer

```text
src/TILSOFTAI.Orchestration/Answering/IAnswerComposer.cs
src/TILSOFTAI.Orchestration/Answering/RawJsonAnswerComposer.cs
src/TILSOFTAI.Orchestration/Answering/StructuredAnswerComposer.cs
src/TILSOFTAI.Orchestration/Answering/AnswerComposerRequest.cs
src/TILSOFTAI.Orchestration/Answering/AssistantAnswer.cs
```

Required changes:

```text
- exactly RawJson and Structured production modes
- deterministic no-data/follow-up/error behavior
- sensitivity masking before output
- model result schema support
```

### 13.5 Pending write preview state

```text
src/TILSOFTAI.Orchestration/Approvals/IPendingActionStore.cs
src/TILSOFTAI.Orchestration/Approvals/PendingActionModels.cs
src/TILSOFTAI.Infrastructure/Approvals/SqlPendingActionStore.cs
sql/ai/0xx_ai_pending_action_tables.sql
```

Required changes:

```text
- durable pending action storage
- active pending action lookup by tenant/user/conversation
- confirm/cancel/expire/execute state transitions
- tests for tenant/user isolation
```

---

## 14. Definition of Done

Sprint 34 is complete only when all items below are true.

### Runtime

```text
[ ] API request can run through official Microsoft Agent Framework path.
[ ] Local AI provider is selected by settings.
[ ] Official agent route uses AIAgent/AsAIAgent and official function tools.
[ ] No fake custom agent is used as the tool/param brain.
[ ] Legacy fallback is disabled for Sprint 34 integration config.
```

### Domain focus

```text
[ ] Only model domain is active.
[ ] Non-model capabilities are disabled or ignored by candidate selector.
[ ] Agent receives <= 6 model tools per request.
[ ] At least 3 model read-only tools run end-to-end.
```

### Tool execution

```text
[ ] Agent chooses tool and arguments.
[ ] Tool callback goes through CapabilityExecutionFacade.
[ ] SQL calls only ai_* stored procedures.
[ ] Missing required argument does not call SQL.
```

### AnswerComposer

```text
[ ] RawJson mode returns deterministic JSON and does not call LLM after tool execution.
[ ] Structured mode returns text + blocks.
[ ] No-data, error, follow-up, and table cases are tested.
[ ] Sensitivity masking is applied before output.
```

### Write preview state

```text
[ ] PendingAction table/store exists.
[ ] Pending action is isolated by tenant/user/conversation.
[ ] Expired pending action cannot execute.
[ ] Execute path requires approval/approvedActionId.
[ ] No real write tool is active by default.
```

### Safety/config

```text
[ ] appsettings.json contains no real SQL passwords/API keys/internal endpoints.
[ ] appsettings.Local.example.json documents local AI setup without secrets.
[ ] Health check reports official agent readiness without leaking secrets.
```

### Tests

```text
[ ] Architecture guard tests pass.
[ ] Model-only routing tests pass.
[ ] AnswerComposer tests pass.
[ ] PendingActionStore tests pass.
[ ] API smoke tests are documented or automated.
```

---

## 15. Implementation Order

Implement in this order. Do not start later items until earlier items are stable.

### Step 1 — Clean runtime entrypoint

```text
- wire API -> SupervisorRuntime -> OfficialAgentToolRouter
- disable legacy fallback in integration profile
- ensure official route can fail closed
```

### Step 2 — Local AI settings

```text
- add local AI settings profile
- create official IChatClient/AIAgent from settings
- add health check
```

### Step 3 — Model-only candidate gate

```text
- enforce AllowedDomains = ["model"]
- disable non-model capabilities
- limit candidate tools to <= 6
```

### Step 4 — Model read-only tools

```text
- register/seed model read-only capabilities
- ensure all callbacks use CapabilityExecutionFacade
- run API smoke tests
```

### Step 5 — AnswerComposer

```text
- standardize RawJson and Structured modes
- integrate responseMode selection
- add tests
```

### Step 6 — Pending write preview state

```text
- add PendingAction table/store
- add confirmation resolver service
- add tests
- keep real writes disabled by default
```

### Step 7 — Cleanup and residue tests

```text
- remove dead/fake code
- add guard tests
- document how to run local AI model domain smoke test
```

---

## 16. Example Local Development Runbook

### 16.1 Configure local AI

Create `appsettings.Local.json` or environment variables. Do not commit real values.

```json
{
  "AiRouting": {
    "MicrosoftAgentFrameworkRoutingEnabled": true,
    "UseOfficialMicrosoftAgentFramework": true,
    "Provider": "OpenAiCompatibleLocal",
    "FallbackToLegacyPipeline": false,
    "AllowedDomains": ["model"],
    "MaxCandidateTools": 6,
    "ToolCallingRequired": true
  },
  "LocalAi": {
    "BaseUrl": "http://localhost:11434/v1",
    "Model": "your-tool-calling-model",
    "ApiKeyEnvironmentVariable": "TILSOFTAI_LOCAL_AI_API_KEY",
    "TimeoutSeconds": 120
  }
}
```

### 16.2 Run tests

```bash
dotnet test
```

Preferred filtered test groups:

```bash
dotnet test --filter "Category=Architecture"
dotnet test --filter "Category=AiRouting"
dotnet test --filter "Category=AnswerComposer"
dotnet test --filter "Category=PendingAction"
```

### 16.3 Manual smoke prompts

```text
Có bao nhiêu model?
How many models are active?
Cho tôi xem thông tin model ABC
Show materials for model ABC
Cho tôi xem thông tin model
```

Expected:

```text
- model_count runs for count prompts
- model overview/materials tools run only when model code is present
- missing model code returns a follow-up question
- no non-model tool is advertised to the agent
```

---

## 17. Final CTO Notes

The sprint succeeds only if the framework becomes smaller, clearer, and runnable.

A good Sprint 34 PR should feel boring:

```text
one domain
few tools
official agent runtime
local AI settings
clear AnswerComposer
safe pending action state
no fake framework
no regex brain
no domain sprawl
```

Do not optimize for theoretical extensibility during Sprint 34. Optimize for one real working path.

