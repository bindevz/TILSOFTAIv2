# Sprint 40 — API Contract and Runtime Hardening

## Goal

Stabilize the API contract and runtime behavior after the first model-domain E2E proof.

This sprint is focused on making the current model-only Agent Framework runtime reliable for API consumers. Do not add new domains. Do not enable real write execution. Do not replace Microsoft Agent Framework.

## Current Problem

The model E2E report shows the runtime can route and execute model tools, but the API response contract is not yet enterprise-grade. In particular, RawJson mode can execute and compose successfully while `/api/chats` may still return an empty top-level `content` because the raw payload is stored in `SupervisorResult.Detail` and not exposed consistently by the API response.

## Target Runtime

```text
API
  -> ISupervisorRuntime
  -> OfficialAgentToolRouter
  -> OfficialMicrosoftAgentRuntime
  -> model-only AIFunction tools
  -> CapabilityExecutionFacade
  -> SqlToolAdapter
  -> AnswerComposer
  -> API response with content + detail + blocks + provenance
```

## Out of Scope

```text
- Production authentication hardening
- New business domains
- Real write execution
- Vector search
- Full SQL-backed catalog migration
```

---

## Phase 0 — Preflight

Run:

```bash
dotnet build
dotnet test
```

Inspect current response flow:

```bash
rg "ChatApiResponse|SupervisorResult|FromAssistantAnswer|RawJsonAnswerComposer|StructuredAnswerComposer" src tests
rg "Detail =|Blocks|AnswerType|Provenance" src/TILSOFTAI.Api src/TILSOFTAI.Orchestration tests
```

Acceptance:

```text
- Build state is known.
- Existing failing tests, if any, are documented before refactor.
```

---

## Phase 1 — Define Enterprise API Response Contract

Create or update a stable response DTO for chat responses.

Suggested DTO:

```csharp
public sealed record ChatApiResponse
{
    public required bool Success { get; init; }
    public string? Content { get; init; }
    public object? Detail { get; init; }
    public IReadOnlyList<object>? Blocks { get; init; }
    public string? AnswerType { get; init; }
    public IReadOnlyDictionary<string, object?>? Provenance { get; init; }
    public string? ConversationId { get; init; }
    public string? CorrelationId { get; init; }
    public string? TraceId { get; init; }
    public string? Language { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
```

Rules:

```text
- `Content` is human-readable text.
- `Detail` contains machine-readable payload such as RawJson envelope.
- `Blocks` contains structured UI blocks when available.
- `Provenance` contains capability/tool/proc/correlation evidence when available.
- Errors must be explicit, not hidden in plain text.
```

Acceptance:

```text
- RawJson API responses expose the raw payload in `Detail`.
- Structured API responses expose text, blocks, and provenance when available.
- Existing clients can still read `Content`.
```

---

## Phase 2 — Fix RawJson API Contract

Review:

```text
src/TILSOFTAI.Orchestration/Answering/RawJsonAnswerComposer.cs
src/TILSOFTAI.Orchestration/Supervisor/SupervisorResult.cs
src/TILSOFTAI.Api/Controllers/ChatController.cs
src/TILSOFTAI.Api/Controllers/OpenAiChatCompletionsController.cs
```

Rules:

```text
- RawJson mode must not call the LLM after tool execution.
- RawJson mode may have empty `Content`, but API must return the raw payload in `Detail`.
- RawJson response must include capability, procedure, arguments, rowCount, rows, resultSchema, executionMetadata, and provenance.
- OpenAI-compatible response may serialize RawJson into the `content` string only for compatibility, but `/api/chats` must expose a typed `Detail`.
```

Add tests:

```text
ChatController_RawJson_ReturnsDetailPayload
ChatController_RawJson_DoesNotDropRows
ChatController_RawJson_PreservesCorrelationId
OpenAiCompatible_RawJson_HasSerializableContent
```

Acceptance:

```text
- RawJson no longer returns an empty useful response to API callers.
- RawJson details are accessible without reading logs.
```

---

## Phase 3 — Harden Structured Response Contract

Review:

```text
StructuredAnswerComposer.cs
AnswerComposerRequest.cs
AssistantAnswer.cs
AnswerBlock.cs
```

Structured response must support:

```text
- answerType
- text
- blocks
- followUpQuestions
- provenance
- correlationId
```

Rules:

```text
- Missing parameter returns `answerType = follow_up`.
- Empty result returns `answerType = no_data`.
- Normal result returns `answerType = structured` or a more specific type.
- Structured mode must not expose sensitive fields.
```

Add tests:

```text
Structured_MissingModelCode_ReturnsFollowUp
Structured_EmptyRows_ReturnsNoData
Structured_ModelOverview_ReturnsBlocksAndProvenance
Structured_DoesNotExposeMaskedColumns
```

---

## Phase 4 — Runtime Result Parsing Tests

Review:

```text
CapabilityExecutionFacade.cs
SqlToolAdapter.cs
ToolResultMapper / equivalent result mapping files
```

Add tests for these result shapes:

```text
1. Envelope object:
   { "meta": {...}, "columns": [...], "rows": [...] }

2. Direct row array:
   [ { ... }, { ... } ]

3. Scalar result:
   { "count": 6 }

4. Empty result:
   { "rows": [] }

5. Malformed JSON:
   must return explicit failure, not crash

6. Unexpected SQL shape:
   must return explicit failure with correlation ID
```

Acceptance:

```text
- The runtime can parse model SQL envelopes predictably.
- Bad SQL JSON does not produce hallucinated answers.
- Errors include enough trace data for diagnostics.
```

---

## Phase 5 — Routing Trace and Diagnostics Tests

Review:

```text
ToolRoutingTraceFactory.cs
SqlToolRoutingTraceStore.cs
AgentRoutingTraceEvents.cs
RuntimeExecutionInstrumentation.cs
```

Add tests for:

```text
- successful tool route
- no tool called / follow-up required
- validation failure
- SQL execution failure
- AnswerComposer failure
```

Trace must include:

```text
correlationId
tenantId
userId
conversationId
provider
model
allowedDomains
candidate capability keys
advertised tool names
selected function
capability key
stored procedure
rowCount
answerMode
fallbackUsed=false
durationMs
```

Acceptance:

```text
- The team can debug one API request from logs/traces without stepping through code.
```

---

## Phase 6 — Format and Reviewability Cleanup

Format C# and SQL files.

Run:

```bash
dotnet format
```

Review SQL formatting manually for:

```text
sql/current/*.sql
```

Rules:

```text
- No production C# file should be intentionally minified.
- No SQL migration file should be a single unreadable line.
- Keep formatting changes separate from behavior changes where practical.
```

Acceptance:

```text
- Important runtime and SQL files are readable.
```

---

## Phase 7 — Final Validation

Run:

```bash
dotnet build
dotnet test
```

Run local model smoke test if SQL and local AI are available.

Required pass criteria:

```text
- RawJson returns useful API payload.
- Structured returns text/blocks/provenance.
- Missing modelCode returns follow-up and no SQL execution.
- Bad SQL JSON returns explicit error.
- Trace data is sufficient for debugging.
```

## Definition of Done

```text
- API response contract is stable.
- RawJson detail is exposed.
- Structured output is explicit.
- Runtime result parsing is tested.
- Trace factory is tested.
- Formatting is readable.
- No new domain is added.
- Auth hardening is not added in this test sprint.
```
