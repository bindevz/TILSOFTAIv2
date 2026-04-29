# Sprint 35 — Core Runtime Stabilization with Model Reference Domain

## Audience

This document is written for a Codex Agent or implementation agent working on the `TILSOFTAIv2` repository.

## CTO Decision

Sprint 35 continues the framework refactor, but it must not become an abstract architecture sprint. The goal is to stabilize the core runtime by using the **Model** read-only domain as the only reference runtime domain.

The target path is:

```text
API request
  -> SupervisorRuntime
  -> Official Microsoft Agent Framework route
  -> local AI provider selected from settings
  -> model-only candidate tools
  -> official AIFunction invocation
  -> CapabilityExecutionFacade
  -> read-only dbo.ai_model_* stored procedure
  -> AnswerComposer
  -> API response
```

## Primary Goal

Stabilize the AI framework core so that a real API request can run end-to-end through official Microsoft Agent Framework and the local AI configuration, using only the `model` read-only domain.

## Non-Goals

Do not work on these items in Sprint 35:

- Do not add Purchasing, Sales, Warehouse, Accounting, or any non-model domain.
- Do not add real write actions.
- Do not add multi-agent workflows.
- Do not add composite capabilities.
- Do not build a full SQL Server vector semantic KB.
- Do not switch frameworks.
- Do not reintroduce regex/keyword routing as the main brain.
- Do not reintroduce a custom fake Microsoft Agent Framework implementation.
- Do not expose all ERP tools to the model.

## Non-Negotiable Rules

1. **Official Microsoft Agent Framework is the brain.**
   - Use official `Microsoft.Agents.AI` / `Microsoft.Extensions.AI` abstractions already adopted in the project.
   - Use official `AIAgent` creation through the existing official provider path.
   - Use official `AIFunction` tools or `AIFunctionFactory.Create(...)` where appropriate.
   - Do not create fake classes that imitate Agent Framework behavior.

2. **No regex-based business routing.**
   - Regex may only be used as a hard-signal helper, for example to identify document-like strings or obvious numeric/date tokens.
   - Regex must not choose the final domain.
   - Regex must not choose the final tool.
   - Regex must not bind business arguments such as `modelCode`, `customerCode`, `supplierCode`, or action intent.

3. **Model domain only.**
   - Runtime candidate domains must be restricted to `model`.
   - Non-model capabilities must not be advertised to the agent.
   - Non-model capabilities must not be loaded into the runtime candidate set.

4. **CapabilityExecutionFacade is the ERP safety boundary.**
   - Function tools must not call SQL directly.
   - Function tools must call `CapabilityExecutionFacade`.
   - The facade must enforce access policy, tenant policy, argument validation, approval guard, and adapter routing.

5. **AnswerComposer owns the final response shape.**
   - Agent Framework selects tools and arguments.
   - AnswerComposer decides whether the response is raw JSON, text, table, chart candidate, summary, follow-up, no-data, or confirmation.

6. **Write preview state must be standardized, but real write execution stays disabled by default.**
   - Sprint 35 should build/clean the pending action state model.
   - Sprint 35 should not enable real write tools in the runtime candidate set.

## Official Reference Notes

Use Microsoft documentation as the behavioral reference for Agent Framework usage:

- Microsoft Agent Framework agents can use LLMs to process input, call tools, and generate responses.
- Function tools are custom code exposed to an agent and can be created from C# methods using `AIFunctionFactory.Create(...)`.
- Chat-client-based agents support function tools.
- Human-in-the-loop approval can exist in Agent Framework, but TILSOFTAI must still use its own ERP approval source of truth.

Reference links:

- https://learn.microsoft.com/en-us/agent-framework/overview/
- https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools
- https://learn.microsoft.com/en-us/agent-framework/agents/tools/tool-approval

---

# Phase 0 — Preflight and Architecture Freeze

## Objective

Confirm the current implementation state and freeze the Sprint 35 scope before changing code.

## Files to Inspect First

Inspect these files before editing:

```text
catalog/platform-catalog.json
src/TILSOFTAI.Api/appsettings.json
src/TILSOFTAI.Api/appsettings.Development.json
src/TILSOFTAI.Api/appsettings.Local.example.json
src/TILSOFTAI.Api/Extensions/AddTilsoftAiExtensions.cs
src/TILSOFTAI.Api/Health/OfficialAgentFrameworkHealthCheck.cs
src/TILSOFTAI.Domain/Configuration/AiRoutingOptions.cs
src/TILSOFTAI.Domain/Configuration/LocalAiOptions.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialAgentProviderFactory.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialMicrosoftAgentRuntime.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialAgentToolRouter.cs
src/TILSOFTAI.Orchestration/AiRouting/Tools/DynamicFunctionToolFactory.cs
src/TILSOFTAI.Orchestration/Capabilities/ModelCapabilities.cs
src/TILSOFTAI.Orchestration/Semantic/DomainGate.cs
src/TILSOFTAI.Orchestration/Semantic/SemanticCapabilityCandidateSelector.cs
src/TILSOFTAI.Orchestration/Semantic/SemanticCapabilityRetriever.cs
src/TILSOFTAI.Orchestration/Answering/AnswerComposerRequest.cs
src/TILSOFTAI.Orchestration/Answering/RawJsonAnswerComposer.cs
src/TILSOFTAI.Orchestration/Answering/StructuredAnswerComposer.cs
src/TILSOFTAI.Orchestration/Actions/ActionRequestRecord.cs
src/TILSOFTAI.Orchestration/Actions/IActionRequestStore.cs
src/TILSOFTAI.Orchestration/Actions/SqlActionRequestStore.cs
tests/TILSOFTAI.Tests/AiRouting/OfficialAgentProviderFactoryTests.cs
tests/TILSOFTAI.Tests/AiRouting/SemanticCapabilityRetrieverTests.cs
```

## Required Outcome

Add or update a short Sprint 35 note in `spec/Sprint_35/` stating:

```text
Sprint 35 is a core runtime stabilization sprint.
Only the Model read-only domain is allowed in runtime.
Official Microsoft Agent Framework remains the only AI routing brain.
No non-model domain, real write action, or framework swap is allowed.
```

## Acceptance Criteria

- A new Sprint 35 spec file exists.
- It clearly states model-only runtime scope.
- It clearly states no non-model domain is allowed.
- It clearly states no real write action is allowed.
- It clearly states official Microsoft Agent Framework is the only AI routing brain.

---

# Phase 1 — Enforce Model-Only Runtime and Remove Fallback Noise

## Objective

Make the runtime fail closed through the official agent route for the model-only domain. Legacy fallback can remain in source for development/debug, but it must not be used by Sprint 35 integration settings.

## Required Changes

### 1. Configuration

Ensure development/integration configuration has:

```json
{
  "AiRouting": {
    "MicrosoftAgentFrameworkRoutingEnabled": true,
    "UseOfficialMicrosoftAgentFramework": true,
    "Provider": "OpenAiCompatibleLocal",
    "FallbackToLegacyPipeline": false,
    "AllowedDomains": ["model"],
    "MaxCandidateTools": 6,
    "MaxTotalCandidateTools": 6,
    "MaxCandidateToolsPerDomain": 6,
    "MaxDomainsPerRequest": 1,
    "ToolCallingRequired": true
  }
}
```

If property names differ, use the existing option names in `AiRoutingOptions` and keep the same behavior.

### 2. Domain Gate

Ensure `DomainGate` allows only:

```text
model
product_model -> normalized to model, if the code already supports this alias
```

No other domain should pass runtime gating in Sprint 35.

### 3. Candidate Selector

Ensure `SemanticCapabilityCandidateSelector` and `SemanticCapabilityRetriever` never return non-model candidates when Sprint 35 config is active.

### 4. Legacy Fallback

Ensure that when Sprint 35 integration config is active:

```text
FallbackToLegacyPipeline = false
```

If official routing fails, the request must fail closed with a clear error and correlation id. It must not silently fall back to keyword routing.

## Forbidden Patterns

Do not add or keep code paths where:

```csharp
selectedTool = tools.FirstOrDefault();
```

or where regex/keyword logic chooses final tools or final arguments.

## Acceptance Criteria

- Model-only config is active in development/integration settings.
- Agent receives no more than 6 model tools.
- Non-model capabilities are not advertised to the agent.
- Legacy fallback is disabled for Sprint 35 integration config.
- If official agent routing fails, the response is a fail-closed framework error, not a legacy fallback result.

---

# Phase 2 — Fix Model Tool Contracts for Natural Language

## Objective

Make model-facing tools usable by real users. Users will normally provide model code/name, not database integer IDs. The model-facing schema must not force the agent to hallucinate `modelId`.

## Required Change

Replace user-facing `modelId:int` parameters with `modelCode:string` or `modelNo:string` for all single-model tools.

Preferred parameter name:

```text
model_code
```

Preferred C# or schema field name:

```text
modelCode
```

## Target Model Tools

The active model tools should be:

```text
model_count(filters?)
model_get_overview(model_code)
model_get_pieces(model_code)
model_get_materials(model_code)
model_get_packaging(model_code)
model_compare(model_codes)
```

Capability keys may remain in the existing style if already used by the catalog, for example:

```text
model.count
model.overview.by-code
model.pieces.by-code
model.materials.by-code
model.packaging.by-code
model.compare
```

But the model-facing argument names must be natural-language friendly.

## Stored Procedure Mapping

If current SQL procedures need integer IDs, do not ask the model to provide IDs.

Use one of these approaches:

### Option A — Preferred for Sprint 35

Stored procedures or SQL wrappers accept `modelCode` and resolve the database ID internally.

Example conceptual mapping:

```text
model_get_overview(model_code = "ABC")
  -> CapabilityExecutionFacade
  -> dbo.ai_model_get_overview
  -> SQL resolves model code safely
```

### Option B — Allowed if already implemented

`CapabilityExecutionFacade` or a dedicated entity resolver maps `modelCode` to `modelId` before adapter execution.

Rules:

- Do not let the agent invent `modelId`.
- Do not ask users for internal database IDs.
- If the model code is ambiguous, return a follow-up question.

## Function Description Requirements

Each model tool must have a concise but useful description.

Example:

```text
model_get_materials
Description:
Get the material list for a model by model code. Use this when the user asks about materials, BOM, components, fabric, hardware, or material consumption of a model. Do not use this for packaging or piece structure.

Parameter:
model_code: Model code, model number, product model code, or model identifier used by business users. Examples: ABC, MD-123, CHAIR-001.
```

Do not hardcode excessive descriptions in many places. Prefer the existing capability metadata path and keep descriptions centralized.

## Acceptance Criteria

- No model-facing single-model tool requires `modelId:int`.
- A user prompt with a business model code can be bound to `modelCode`.
- The agent is not expected to infer a database integer ID.
- Missing model code produces a follow-up question and no SQL call.
- Ambiguous model code produces a follow-up question and no SQL call.

---

# Phase 3 — Prove Official Agent Tool Calling with Local AI

## Objective

Prove that the configured local AI provider can run through official Microsoft Agent Framework and invoke model tools.

## Required Configuration

Use local AI settings from configuration. Do not hardcode model endpoint or model name in source code.

Example shape:

```json
{
  "LocalAi": {
    "BaseUrl": "http://localhost:11434/v1",
    "Model": "YOUR_TOOL_CALLING_MODEL",
    "ApiKeyEnvironmentVariable": "TILSOFTAI_LOCAL_AI_API_KEY",
    "TimeoutSeconds": 120
  }
}
```

If the local gateway needs a specific base path, ensure the SDK client is configured with the correct base endpoint. If `ChatCompletionsPath` exists in settings but is not used, either wire it correctly or document that `BaseUrl` must be the OpenAI-compatible base URL, usually ending in `/v1`.

## Health Check Requirements

Update `OfficialAgentFrameworkHealthCheck` so it is unhealthy when Sprint 35 official route is enabled and:

- `Provider` is empty.
- `Provider` is not allowed.
- `LocalAi.Model` is empty.
- `LocalAi.Model` is a placeholder such as `CHANGE_ME_TOOL_CALLING_MODEL`.
- No `IChatClient` is registered.
- Official route is enabled but allowed domains do not include only `model`.
- Official route is enabled but fallback is still enabled in integration config.

The health check does not need to call the LLM on every request, but it must make misconfiguration obvious.

## Smoke Test Runbook

Create or update a runbook file:

```text
spec/Sprint_35/RUNBOOK_Model_Local_AI_Smoke_Test.md
```

It must include:

1. How to set `appsettings.Local.json`.
2. How to set `TILSOFTAI_LOCAL_AI_API_KEY`.
3. How to start the API.
4. How to call health/readiness.
5. How to send a chat request.
6. How to request `raw_json` mode.
7. How to request `structured` mode.
8. What logs to inspect.

## Required Smoke Prompts

Use these prompts as the minimum manual/API smoke set:

```text
1. "Có bao nhiêu model?"
2. "How many models are active?"
3. "Cho tôi xem thông tin model ABC"
4. "Show materials for model ABC"
5. "Model ABC gồm những piece nào?"
6. "Cho tôi xem thông tin model"
```

Expected behavior:

```text
Prompt 1 -> model_count
Prompt 2 -> model_count
Prompt 3 -> model_get_overview with modelCode/model_code = ABC
Prompt 4 -> model_get_materials with modelCode/model_code = ABC
Prompt 5 -> model_get_pieces with modelCode/model_code = ABC
Prompt 6 -> follow-up question, no SQL execution
```

## Required Logs

For each API call, log at least:

```text
correlationId
provider
model
allowedDomains
candidateCapabilities
advertisedFunctionNames
selectedFunctionName
selectedArguments
capabilityKey
procedureName
rowCount
durationMs
answerMode
fallbackUsed
```

`fallbackUsed` must be `false` in Sprint 35 integration config.

## Acceptance Criteria

- One API request reaches `OfficialAgentToolRouter`.
- An official `AIAgent` is created.
- The agent receives model-only candidate tools.
- The agent invokes an `AIFunction` model tool.
- `CapabilityExecutionFacade` is called from the tool callback.
- No direct SQL call happens inside the tool callback.
- The response passes through `AnswerComposer`.
- No legacy fallback is used.

---

# Phase 4 — Standardize AnswerComposer as the Response Boundary

## Objective

Make `AnswerComposer` the deterministic response boundary for both machine-friendly and user-facing responses.

## Required Modes

Only two modes are required in Sprint 35:

```text
RawJson
Structured
```

### RawJson Mode

Rules:

- Do not call the LLM after tool execution.
- Return deterministic JSON envelope.
- Include capability/procedure/arguments/result metadata.
- Apply sensitivity masking before returning rows.

Minimum envelope shape:

```json
{
  "mode": "raw_json",
  "capabilityKey": "model.overview.by-code",
  "procedureName": "dbo.ai_model_get_overview",
  "arguments": {
    "modelCode": "ABC"
  },
  "rowCount": 1,
  "rows": [],
  "resultSchema": {},
  "executionMetadata": {
    "correlationId": "...",
    "durationMs": 123,
    "executedAtUtc": "..."
  },
  "provenance": {
    "source": "model.overview.by-code"
  }
}
```

### Structured Mode

Rules:

- Return text plus structured blocks.
- Use result schema and answer policy when available.
- Apply sensitivity masking before summary/table blocks.
- Produce no-data answer when `rowCount = 0`.
- Produce follow-up answer when required arguments are missing.
- Produce table block when rows are small enough.
- Produce summary text from deterministic rules first.
- Optional AI summary can be added later, but not required for Sprint 35.

Minimum structured answer shape:

```json
{
  "mode": "structured",
  "answerType": "summary_then_table",
  "text": "Found 1 model matching ABC.",
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
    "capabilityKey": "model.overview.by-code",
    "correlationId": "...",
    "rowCount": 1
  }
}
```

## Locale Requirements

Support at least:

```text
vi-VN
en-US
```

For Vietnamese output, use proper Vietnamese text with accents. Do not return unaccented Vietnamese such as `Xac nhan thao tac` unless the locale explicitly requires ASCII-only output.

## Required Cases

Implement or verify these cases:

1. `RawJson` with rows.
2. `RawJson` with no rows.
3. `Structured` with rows <= 20.
4. `Structured` with rows > 20.
5. `Structured` with rowCount = 0.
6. Missing required argument.
7. Validation failure.
8. Capability execution error.
9. Sensitivity masking.
10. Write preview confirmation block, even if write tools remain disabled by default.

## Acceptance Criteria

- RawJson mode never calls the LLM after tool execution.
- Structured mode always returns text, blocks, and provenance.
- No-data answer repeats the applied filters.
- Follow-up answer asks for the missing `modelCode` when needed.
- Table block uses result schema labels if available.
- Sensitive fields are masked before RawJson or Structured output.
- Vietnamese output uses proper accents in `vi-VN`.

---

# Phase 5 — Standardize Write Preview Conversation State

## Objective

Build a clean pending action state foundation for future write preview/confirmation, without enabling real write tools in Sprint 35.

## Required Domain Model

Update or replace `ActionRequestRecord` with fields equivalent to:

```text
actionId
tenantId
userId
conversationId
capabilityKey
functionName
proposedToolName
proposedProcedureName
proposedArgumentsJson
previewResultJson
status
createdAtUtc
expiresAtUtc
confirmedAtUtc
approvedAtUtc
executedAtUtc
cancelledAtUtc
requestedByUserId
approvedByUserId
executedByUserId
correlationId
metadataJson
```

If existing naming differs, keep compatible names but ensure the information exists.

## Required Status Values

Use a small, explicit lifecycle:

```text
Pending
Confirmed
Approved
Executed
Rejected
Cancelled
Expired
Failed
```

If the existing system uses different values, add mapping functions and keep behavior explicit.

## Required Store Methods

`IActionRequestStore` must support:

```csharp
Task<ActionRequestRecord> CreateAsync(
    ActionRequestCreateRequest request,
    CancellationToken cancellationToken);

Task<ActionRequestRecord?> GetAsync(
    string tenantId,
    string actionId,
    CancellationToken cancellationToken);

Task<ActionRequestRecord?> GetActiveForConversationAsync(
    string tenantId,
    string userId,
    string conversationId,
    CancellationToken cancellationToken);

Task<ActionRequestRecord> ConfirmAsync(
    string tenantId,
    string userId,
    string actionId,
    CancellationToken cancellationToken);

Task<ActionRequestRecord> RejectAsync(
    string tenantId,
    string userId,
    string actionId,
    string? reason,
    CancellationToken cancellationToken);

Task<ActionRequestRecord> MarkExecutedAsync(
    string tenantId,
    string actionId,
    string executedByUserId,
    CancellationToken cancellationToken);

Task<int> ExpireOldAsync(
    DateTimeOffset nowUtc,
    CancellationToken cancellationToken);
```

## Confirmation Intent Handling

Do not enable real write execution yet. However, add a framework-level resolver for future confirmation messages:

```text
User says: "xác nhận", "confirm", "yes", "đồng ý"
  -> lookup active pending action by tenant/user/conversation
  -> if exactly one active pending action exists, return confirmation-ready state
  -> if none exists, return follow-up/no-pending-action answer
  -> if multiple exist, ask the user to choose
```

This resolver must not execute real writes in Sprint 35.

## Isolation Rules

- A user cannot confirm another user's pending action.
- A tenant cannot access another tenant's pending action.
- An expired action cannot be confirmed or executed.
- A cancelled/rejected/executed action cannot be executed again.
- By default, only one active pending action is allowed per conversation. If multiple are allowed later, the response must ask the user to choose.

## Acceptance Criteria

- Pending action records include expiration.
- Pending action records include conversation ID and user ID.
- Store supports active conversation lookup.
- Store enforces tenant/user isolation.
- Expired pending action cannot be confirmed.
- Confirmation resolver can find a pending action from conversation context without the frontend resending all original arguments.
- Real write tools remain disabled by default.

---

# Phase 6 — Anti-Regression Tests

## Objective

Add tests that prevent the project from regressing back to fake agent behavior, first-candidate selection, regex-based routing, or non-model tool bloat.

## Required Tests

### Test 1 — Official Agent Runtime Is Used

Assert that the provider factory creates an official Agent Framework agent path, not a custom fake agent.

Expected:

```text
Official provider factory returns/uses a Microsoft Agent Framework agent type.
No fake CandidateGatedToolCallingAgent is used.
```

### Test 2 — Candidate Order Does Not Determine Selected Tool

Create a test candidate list where the wrong tool is first.

Prompt:

```text
"Show materials for model ABC"
```

Expected:

```text
The selected/invoked function is model_get_materials.
```

Fail if:

```text
The first candidate is always selected.
```

If a real LLM is not available in unit tests, simulate `IChatClient` behavior while still going through official agent runtime abstractions where possible.

### Test 3 — Missing Model Code Does Not Execute SQL

Prompt:

```text
"Cho tôi xem thông tin model"
```

Expected:

```text
Answer type = follow_up
No CapabilityExecutionFacade execution
No SQL adapter execution
```

### Test 4 — RawJson Does Not Call Final LLM Summary

Prompt:

```text
"Cho tôi xem thông tin model ABC"
```

Mode:

```text
RawJson
```

Expected:

```text
Tool execution happens.
AnswerComposer returns raw JSON.
No second/final LLM call occurs after tool result.
```

### Test 5 — Structured Output Has Blocks and Provenance

Expected:

```text
Structured response includes text, blocks, answerType, and provenance.
```

### Test 6 — Non-Model Tools Are Not Advertised

Given Sprint 35 config, candidate selection must only return model tools.

Expected:

```text
No purchasing/sales/warehouse/accounting tools are passed to the agent.
```

### Test 7 — Pending Action User Isolation

User A creates pending action.
User B attempts to confirm it.

Expected:

```text
Confirmation fails.
```

### Test 8 — Expired Pending Action Cannot Be Confirmed

Expected:

```text
Expired action cannot move to Confirmed or Executed.
```

## Acceptance Criteria

- Tests fail if the implementation chooses the first candidate automatically.
- Tests fail if regex/keyword logic is used as the final tool selector.
- Tests fail if non-model tools are returned in Sprint 35 config.
- Tests fail if RawJson performs final LLM summarization.
- Tests fail if pending action can be confirmed by another user or after expiration.

---

# Phase 7 — Observability and Diagnostics

## Objective

Make the end-to-end runtime observable enough to debug real API calls.

## Required Trace Events

Add or verify structured logs/trace records for:

```text
agent_route_started
candidate_selection_completed
agent_created
tools_advertised
agent_run_started
agent_tool_invoked
capability_facade_started
capability_validation_failed
capability_execution_completed
answer_composer_started
answer_composer_completed
agent_route_failed_closed
pending_action_created
pending_action_confirmed
pending_action_expired
```

## Required Fields

Each event should include where applicable:

```text
correlationId
tenantId
userId
conversationId
provider
model
allowedDomains
candidateCapabilityKeys
advertisedFunctionNames
selectedFunctionName
capabilityKey
procedureName
argumentsMasked
rowCount
durationMs
answerMode
fallbackUsed
errorCode
```

## Metrics

Expose or log at least:

```text
candidate_count
advertised_tool_count
agent_duration_ms
facade_duration_ms
sql_duration_ms
answer_composer_duration_ms
row_count
fallback_count
follow_up_count
validation_failure_count
```

## Acceptance Criteria

- A single API call can be traced end-to-end by `correlationId`.
- Logs show candidate tools and selected function.
- Logs show whether fallback was used.
- Logs show AnswerComposer mode.
- Errors include correlation id and stable error code.

---

# Phase 8 — API-Level Smoke Tests and Runbook

## Objective

Create a repeatable process to prove the framework works through API, not only through unit tests.

## Required API Smoke Cases

Add a smoke test script or documented curl/http file under one of:

```text
spec/Sprint_35/smoke/
tools/smoke/
docs/runbooks/
```

Minimum cases:

### Case 1 — Model Count, Structured

Request:

```json
{
  "message": "Có bao nhiêu model?",
  "responseMode": "structured",
  "locale": "vi-VN"
}
```

Expected:

```text
model.count capability
structured answer
no legacy fallback
```

### Case 2 — Model Overview, RawJson

Request:

```json
{
  "message": "Cho tôi xem thông tin model ABC",
  "responseMode": "raw_json",
  "locale": "vi-VN"
}
```

Expected:

```text
model.overview.by-code capability
modelCode/model_code = ABC
raw_json response
no final LLM summary
```

### Case 3 — Missing Parameter

Request:

```json
{
  "message": "Cho tôi xem thông tin model",
  "responseMode": "structured",
  "locale": "vi-VN"
}
```

Expected:

```text
follow-up question
no SQL execution
```

### Case 4 — English Prompt

Request:

```json
{
  "message": "Show materials for model ABC",
  "responseMode": "structured",
  "locale": "en-US"
}
```

Expected:

```text
model.materials.by-code capability
modelCode/model_code = ABC
structured answer
```

## Acceptance Criteria

- Smoke runbook exists.
- API requests are documented.
- Expected logs are documented.
- Expected response shapes are documented.
- Fail-closed behavior is documented.

---

# Final Definition of Done

Sprint 35 is complete only when all statements below are true:

1. Official Microsoft Agent Framework remains the only AI routing brain.
2. No fake custom agent is used to choose tools or bind arguments.
3. No regex/keyword logic chooses final tool or final business arguments.
4. Runtime is model-only.
5. Non-model tools are not advertised to the agent.
6. Local AI provider is selected from settings.
7. Health check detects missing/placeholder local model config.
8. Agent receives at most 6 model tools.
9. Agent invokes model tools through official function tool path.
10. Function tool callback calls `CapabilityExecutionFacade`.
11. Model-facing tools use `modelCode`/`model_code`, not hallucinated `modelId`.
12. Missing model code returns a follow-up question and does not call SQL.
13. RawJson mode returns deterministic envelope and does not call final LLM summary.
14. Structured mode returns text, blocks, and provenance.
15. Pending action state has tenant/user/conversation isolation and expiration.
16. Real write tools remain disabled by default.
17. API smoke runbook exists and covers model count, overview, materials, missing parameter, and multilingual prompt.
18. Logs allow tracing an API request end-to-end by correlation id.

---

# Suggested Implementation Order for Codex Agent

Work in this order. Do not skip phases.

```text
1. Phase 0 — Add Sprint 35 spec and freeze scope.
2. Phase 1 — Enforce model-only official route and fail-closed config.
3. Phase 2 — Fix model tool contracts from modelId to modelCode.
4. Phase 3 — Prove official local AI tool calling path.
5. Phase 4 — Stabilize AnswerComposer RawJson/Structured modes.
6. Phase 5 — Standardize pending action conversation state.
7. Phase 6 — Add anti-regression tests.
8. Phase 7 — Add observability and diagnostics.
9. Phase 8 — Add API smoke runbook.
```

If context becomes too long, stop after the current phase, commit, and continue in the next session with the next phase. Do not attempt to implement all future domains or real write actions in this sprint.

---

# PR Checklist

Before opening the PR, verify:

```text
[ ] dotnet build passes.
[ ] dotnet test passes or failing tests are explicitly documented.
[ ] Sprint 35 spec exists.
[ ] Only model domain is active in runtime config.
[ ] Official agent route is enabled in integration/dev config.
[ ] Legacy fallback is disabled for Sprint 35 config.
[ ] Model tools use modelCode/model_code, not modelId.
[ ] RawJson mode does not call final LLM summary.
[ ] Structured mode returns blocks and provenance.
[ ] Pending action state includes expiration and conversation lookup.
[ ] API smoke runbook exists.
[ ] No non-model tools are advertised in Sprint 35 tests.
[ ] No fake Microsoft Agent Framework implementation remains in the routing brain.
```

