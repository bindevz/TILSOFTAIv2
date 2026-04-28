# Sprint 32 — Corrective Refactor: Use Official Microsoft Agent Framework as the Tool/Parameter Brain

**Target repository:** `bindevz/TILSOFTAIv2`  
**Sprint type:** Corrective refactor after Sprint 31  
**Primary audience:** Codex Agent / coding agent  
**Language:** English-only for implementation clarity  
**Primary goal:** Remove all fake/custom “MicrosoftAgentFramework” behavior and make the runtime use the **official Microsoft Agent Framework** as the AI brain for tool selection and parameter extraction.

---

## 0. Executive Summary

Sprint 31 created useful foundations: semantic KB tables, dynamic tool metadata, routing feature flags, execution facade, raw JSON/structured answer composer, and write-preview scaffolding.

However, Sprint 31 must be corrected because the current implementation can appear to use Microsoft Agent Framework while still doing the core AI routing manually. This sprint must remove all fake/custom agent behavior and replace it with the official Microsoft Agent Framework runtime.

The target behavior is:

```text
User natural language
  -> SQL Server 2025 semantic/domain/capability retrieval
  -> candidate-gated official Agent Framework agent
  -> official function tools generated from SQL-backed capability metadata
  -> model selects tool and arguments through official tool/function calling
  -> CapabilityExecutionFacade validates, authorizes, resolves, and executes
  -> AnswerComposer returns RawJson or Structured answer
```

The target behavior is **not**:

```text
User natural language
  -> retrieve candidates
  -> pick first candidate manually
  -> regex-extract arguments manually
  -> call facade
```

---

## 1. Official References

The coding agent must use the official Microsoft Agent Framework / Microsoft.Extensions.AI APIs and must verify exact package/API names against the current official documentation before implementing.

Reference points:

- Microsoft Agent Framework overview:  
  https://learn.microsoft.com/en-us/agent-framework/overview/

- Microsoft Agent Framework get started:  
  https://learn.microsoft.com/en-us/agent-framework/get-started/

- Microsoft Agent Framework function tools:  
  https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools

- Microsoft Agent Framework tool approval / human-in-the-loop:  
  https://learn.microsoft.com/en-us/agent-framework/agents/tools/tool-approval

- `Microsoft.Extensions.AI.AIFunctionFactory`:  
  https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.aifunctionfactory

- `AIFunctionFactory.CreateDeclaration`:  
  https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.aifunctionfactory.createdeclaration

Important implementation rules:

1. Official docs show agents created with official `AIAgent` types, for example through `AsAIAgent(...)` or `CreateAIAgent(...)`, depending on provider and package version.
2. Official docs show function tools created with `AIFunctionFactory.Create(...)`.
3. Official docs show approval-capable tools through `ApprovalRequiredAIFunction`.
4. If the package API has changed, update the implementation to the current official API.
5. Do **not** invent local replacements when official API usage is unclear.

---

## 2. Non-Negotiable Rules

### 2.1. No fake Microsoft Agent Framework

Production code must not create an internal fake implementation that pretends to be Microsoft Agent Framework.

Forbidden examples:

```csharp
new CandidateGatedToolCallingAgent(...)
_tools.FirstOrDefault()
ExtractArgumentValue(message, argument)
Regex-based primary extraction for all capability arguments
```

Forbidden production classes/patterns:

```text
CandidateGatedToolCallingAgent
Custom agent that selects the first candidate tool
Custom tool-calling loop that never creates/runs official AIAgent
Custom AgentFunctionTool that is not converted to official AIFunction/tool type
Custom "MicrosoftAgentFramework" implementation that does not use Microsoft.Agents.AI runtime
```

Allowed:

```text
Small wrapper interfaces around official Microsoft Agent Framework types
Test doubles in test projects only
Fallback legacy route behind feature flag only when the official route declines or fails safely
```

Not allowed:

```text
Production runtime that claims Agent Framework is enabled but does not call official Agent Framework
Production runtime that uses regex as the main parameter extraction mechanism
Production runtime that chooses tools deterministically without giving official function tools to the model
```

---

## 3. Sprint 32 Objectives

### Objective A — Replace fake agent routing with official Microsoft Agent Framework

The tool/parameter brain must be an official Microsoft Agent Framework agent.

Expected runtime:

```text
OfficialMicrosoftAgentRuntime
  -> creates official AIAgent
  -> passes official AIFunction tools
  -> invokes agent.RunAsync / RunStreamingAsync
  -> parses official agent response
  -> returns selected tool result / clarification / answer envelope
```

### Objective B — Preserve candidate-gated domain routing

The system must not expose all ERP tools to the model.

Expected routing:

```text
User message
  -> detect hard signals
  -> retrieve candidate domains from SQL Server 2025 KB
  -> retrieve candidate capabilities within selected domains
  -> build only candidate official AIFunction tools
  -> create official AIAgent with only those tools
```

Hard limits:

```text
Max domains per request: 2
Max tools per domain: 6
Max total tools per request: 12
Max composite sub-capabilities: 5
```

### Objective C — Dynamic multilingual descriptions from SQL Server 2025 KB

Function and parameter descriptions must not be hardcoded in hand-written production code.

Expected source of truth:

```text
ai.Capability
ai.CapabilityText
ai.CapabilityArgument
ai.ArgumentText
ai.KnowledgeChunk
ai.EntityAlias
```

The runtime must dynamically build model-facing descriptions using:

```text
- user locale
- detected language
- domain
- capability descriptions
- argument descriptions
- aliases
- examples
- retrieved glossary chunks
- field/result schema metadata
```

### Objective D — Keep execution safety outside the model

The official agent selects a tool and proposes arguments. It must not execute SQL directly.

All tool callbacks must call:

```text
ICapabilityExecutionFacade.ExecuteReadAsync(...)
ICapabilityExecutionFacade.PreviewWriteAsync(...)
ICapabilityExecutionFacade.ExecuteApprovedWriteAsync(...) only after internal approval
```

The execution facade remains the mandatory boundary for:

```text
- tenant policy
- role policy
- capability whitelist
- argument validation
- entity resolution
- write-preview and approval enforcement
- adapter execution
- audit/telemetry
```

### Objective E — Keep AnswerComposer as the output brain

The Agent Framework should choose tools and arguments. It should not be the final uncontrolled answer formatter.

The final user response must go through:

```text
IAnswerComposer
```

Supported output modes:

```text
RawJson
Structured
```

Structured mode decides:

```text
text
summary
table
chart candidate
follow-up question
write confirmation card
empty-result explanation
sensitive-data masking
```

---

## 4. Target Architecture

```text
API / ChatHub / OpenAI-compatible endpoint
  -> SupervisorRuntime
  -> IAgentToolRouter
      -> HardSignalExtractor
      -> SQL Server 2025 Semantic KB Retriever
      -> DomainGate
      -> CandidateCapabilityRetriever
      -> OfficialMicrosoftAgentRuntime
          -> official AIAgent
          -> official AIFunction tools
          -> model chooses tool + arguments
  -> CapabilityExecutionFacade
      -> policy
      -> validation
      -> entity resolution
      -> approval/write guard
      -> SqlToolAdapter / RestToolAdapter / CompositeAdapter
  -> AnswerComposer
      -> RawJsonAnswerComposer
      -> StructuredAnswerComposer
```

---

## 5. Required Source Layout

Create or update these areas.

```text
src/TILSOFTAI.Orchestration/
  AiRouting/
    IAgentToolRouter.cs
    AgentToolRouterResult.cs
    AgentToolRouterOptions.cs

  AiRouting/OfficialMicrosoftAgentFramework/
    IOfficialAgentRuntime.cs
    OfficialMicrosoftAgentRuntime.cs
    OfficialAgentRuntimeOptions.cs
    OfficialAgentResponseMapper.cs
    OfficialAgentSessionStore.cs
    OfficialAgentInstructionsBuilder.cs

  AiRouting/Tools/
    IOfficialAgentToolFactory.cs
    OfficialAgentToolFactory.cs
    CapabilityToolDescriptor.cs
    CapabilityToolDescriptionBuilder.cs
    CapabilityParameterSchemaBuilder.cs
    CapabilityToolNameMapper.cs
    CapabilityToolCallbackDispatcher.cs

  Semantic/
    IDomainGate.cs
    DomainGate.cs
    ISemanticCapabilityRetriever.cs
    SqlSemanticCapabilityRetriever.cs
    HardSignalExtractor.cs
    EntityCandidateResolver.cs

  Execution/
    ICapabilityExecutionFacade.cs
    CapabilityExecutionFacade.cs

  Answering/
    IAnswerComposer.cs
    RawJsonAnswerComposer.cs
    StructuredAnswerComposer.cs
```

Remove or quarantine these production patterns:

```text
CandidateGatedToolCallingAgent
Custom MicrosoftAgentFramework models that never call official AIAgent
Regex-only argument extractor used as primary NLU
First-candidate selection logic
```

If a compatibility wrapper is needed, name it honestly, for example:

```text
OfficialMicrosoftAgentRuntime
AgentFrameworkRuntimeAdapter
```

Do not name a custom class as though it is the framework itself.

---

## 6. Official Agent Runtime Requirements

### 6.1. Runtime must create official agent

`OfficialMicrosoftAgentRuntime` must create and invoke an official Microsoft Agent Framework agent.

Pseudo-shape:

```csharp
public sealed class OfficialMicrosoftAgentRuntime : IOfficialAgentRuntime
{
    private readonly IChatClient _chatClient;
    private readonly OfficialAgentRuntimeOptions _options;

    public async Task<OfficialAgentRunResult> RunAsync(
        OfficialAgentRunRequest request,
        CancellationToken cancellationToken)
    {
        // Build official AIFunction tools.
        IReadOnlyList<AIFunction> tools = request.Tools;

        // Create official Microsoft Agent Framework agent.
        AIAgent agent = _chatClient.CreateAIAgent(
            name: request.AgentName,
            instructions: request.Instructions,
            tools: tools);

        // Use official session/thread support when multi-turn context exists.
        AgentRunResponse response = await agent.RunAsync(
            request.UserMessage,
            cancellationToken);

        return OfficialAgentResponseMapper.Map(response);
    }
}
```

The exact method names may differ depending on the official package version. The implementation must use the current official package APIs.

### 6.2. Runtime must not choose the tool itself

This is forbidden:

```csharp
var selected = tools.FirstOrDefault();
```

This is also forbidden:

```csharp
var selected = candidates.OrderByDescending(x => x.Score).First();
var args = RegexExtract(userText, selected);
```

The final tool selection and argument construction must be performed by the model through official function/tool calling.

Candidate retrieval can narrow the tool list. It cannot replace the model’s final function-call decision.

---

## 7. Official Function Tool Requirements

### 7.1. Function tools must be official

Every model-facing tool must be represented as an official function/tool type, not a custom fake.

Preferred:

```csharp
AIFunction tool = AIFunctionFactory.Create(...);
```

If fully dynamic JSON schema is required, check current official support. `AIFunctionFactory.CreateDeclaration(...)` creates a declaration but not an invocable function. If the current official API cannot create an invocable dynamic function with arbitrary parameter schema, use one of the approved approaches below.

### 7.2. Approved approaches for dynamic tools

#### Approach 1 — Official dynamic invocable functions, if supported

Use official APIs only.

Requirements:

```text
- dynamic name from SQL KB
- dynamic function description from SQL KB
- dynamic parameter schema from SQL KB
- callback dispatches to CapabilityExecutionFacade
- no fake local function-calling runtime
```

#### Approach 2 — Build-time/source-generated official tools

If official .NET API does not support fully dynamic invocable tools with arbitrary schema, generate typed tool wrappers from SQL-backed certified capability metadata.

Requirements:

```text
- generated source is derived from SQL-backed catalog/export, not hand-written hardcode
- generated methods are converted with AIFunctionFactory.Create(...)
- generated descriptions are injected from SQL-backed metadata during generation
- generated code is versioned and traceable to catalog version
- runtime still candidate-gates the generated official tools
```

Example generated tool shape:

```csharp
[Description("Generated from ai.CapabilityText for capability warehouse.inventory.by-item")]
public async Task<CapabilityExecutionEnvelope> warehouse_inventory_by_item(
    [Description("Generated from ai.ArgumentText for @ItemNo")] string item_no,
    [Description("Generated from ai.ArgumentText for @WarehouseCode")] string? warehouse_code,
    CancellationToken cancellationToken = default)
{
    return await _dispatcher.DispatchAsync(
        capabilityKey: "warehouse.inventory.by-item",
        modelArguments: new Dictionary<string, object?>
        {
            ["item_no"] = item_no,
            ["warehouse_code"] = warehouse_code
        },
        cancellationToken);
}
```

This is acceptable because the generated source is a build artifact of the catalog, not manually hardcoded business logic.

#### Approach 3 — Hybrid official wrapper with `AIFunctionArguments`

Use this only if supported by official APIs and if it still exposes enough schema for the model.

Requirements:

```text
- official AIFunction is created through AIFunctionFactory.Create(...)
- dynamic function name/description are passed through official options/overload
- parameter JSON schema must still be meaningful to the model
- callback validates model arguments through CapabilityExecutionFacade
```

### 7.3. Parameter descriptions must be multilingual/dynamic

Do not rely only on static `[Description]` attributes in hand-written production code.

Parameter descriptions must be built from:

```text
ai.ArgumentText
ai.CapabilityArgument.AliasesJson
ai.CapabilityExample
ai.KnowledgeChunk
ResultSchema/business field metadata
```

---

## 8. SQL Server 2025 Semantic KB Requirements

### 8.1. Required KB concepts

The system must support these KB records:

```text
Capability
CapabilityText
CapabilityArgument
ArgumentText
CapabilityExample
KnowledgeChunk
EntityAlias
ToolExecutionTrace
```

### 8.2. Retrieval stages

The retrieval pipeline must be staged:

```text
1. HardSignalExtractor
   - codes
   - dates
   - quantities
   - currencies
   - known ERP document patterns

2. DomainGate
   - retrieve top domains
   - avoid loading all domains/tools

3. CapabilityRetriever
   - retrieve top capabilities within selected domains
   - combine vector + lexical + hard-signal match

4. EntityResolver
   - resolve customer/supplier/item/warehouse aliases
   - if ambiguous, return clarification instead of guessing

5. ToolFactory
   - build official tools only for candidate capabilities
```

### 8.3. Vector/hybrid search

If SQL Server 2025 vector features are available in the environment, use vector search for `ai.KnowledgeChunk.Embedding`.

Fallback is allowed only when vector features are not enabled, but fallback must still be hybrid and metadata-driven.

Acceptable fallback:

```text
- lexical score over capability/argument/example text
- hard signal boosts
- domain boost
- entity alias confidence
```

Unacceptable fallback:

```text
- hardcoded keyword if/else by domain
- hardcoded language-specific tool mapping in C#
- selecting all tools from all domains
```

### 8.4. Dynamic description assembly

`CapabilityToolDescriptionBuilder` must assemble a per-request description like:

```text
Capability business purpose
Localized aliases
Positive examples
Negative examples / when-not-to-use
Relevant argument hints
Relevant glossary snippets
Known entity candidates
Safety/execution mode note
```

This description must be passed to the official function tool metadata where the official API supports it.

---

## 9. Domain-Gated Tool Routing Requirements

ERP systems have too many tables, views, and stored procedures. The model must never receive the full tool catalog.

### 9.1. Domain first

Before creating the official agent:

```text
- Determine candidate domains using SQL KB retrieval.
- Keep at most 2 domains.
- If domain ambiguity remains high, ask a follow-up question.
```

Example:

```text
User: "Show me open orders for ABC"
Candidate domains:
  sales
  purchasing

If both customer ABC and supplier ABC match, ask:
"Do you mean sales orders for customer ABC or purchase orders from supplier ABC?"
```

### 9.2. Candidate tool limit

Before creating official tools:

```text
- max 6 tools per domain
- max 12 total tools
- prefer read-only tools unless user explicitly asks to create/update/delete
- expose write-preview tools only for mutation requests
```

### 9.3. No mega-agent

Do not create one permanent ERP agent with hundreds of tools.

Required pattern:

```text
Per request or per narrowed session:
  create official agent with only candidate tools
```

Longer-lived sessions may preserve conversation context, but the tool list must still be refreshed/narrowed per turn.

---

## 10. Capability Execution Facade Requirements

The official agent tool callback must call `ICapabilityExecutionFacade`.

### 10.1. Read execution

```csharp
Task<CapabilityExecutionEnvelope> ExecuteReadAsync(
    string capabilityKey,
    IReadOnlyDictionary<string, object?> modelArguments,
    CancellationToken cancellationToken);
```

Required steps:

```text
1. Load capability from SQL/catalog.
2. Ensure capability is active and certified.
3. Check tenant/user/roles.
4. Resolve model-facing argument names to proc argument names.
5. Resolve entity aliases.
6. Validate required/optional arguments.
7. Enforce execution mode = read_only.
8. Call adapter.
9. Attach execution metadata and provenance.
10. Return envelope to AnswerComposer.
```

### 10.2. Write preview

```csharp
Task<CapabilityExecutionEnvelope> PreviewWriteAsync(
    string capabilityKey,
    IReadOnlyDictionary<string, object?> modelArguments,
    CancellationToken cancellationToken);
```

Required steps:

```text
1. Validate capability is write_preview or mutation-capable.
2. Never execute final mutation.
3. Validate arguments and business rules.
4. Create proposed action / approval draft through IApprovalEngine.
5. Return confirmation envelope.
```

### 10.3. Write execute

```csharp
Task<CapabilityExecutionEnvelope> ExecuteApprovedWriteAsync(
    string capabilityKey,
    string approvedActionId,
    IReadOnlyDictionary<string, object?> modelArguments,
    CancellationToken cancellationToken);
```

Requirements:

```text
- Must only run after user confirmation.
- Must verify approvedActionId belongs to current user/session/tenant.
- Must route through IApprovalEngine.
- Must enforce SqlToolAdapter write guard.
- Must audit the final mutation.
```

The official model must not be able to directly call execute-write tools.

---

## 11. Write Tools and Approval Requirements

### 11.1. Expose preview tools only

Allowed model-facing write tools:

```text
sales_order_create_preview
purchasing_po_create_preview
warehouse_receipt_create_preview
model_product_create_preview
```

Forbidden model-facing tools:

```text
sales_order_create_execute
purchasing_po_create_execute
warehouse_receipt_create_execute
model_product_create_execute
```

### 11.2. Official approval support

If the official Agent Framework approval API is used, it is only a UX/runtime helper.

Internal source of truth remains:

```text
IApprovalEngine
```

Optional official wrapper:

```csharp
AIFunction approvalTool = new ApprovalRequiredAIFunction(previewFunction);
```

Do not replace internal ERP approval logic with provider-level approval alone.

### 11.3. Confirmation loop

When the user says:

```text
"Confirm"
"Yes, create it"
"Xác nhận"
"Đồng ý tạo"
```

The system must:

```text
1. Load pending action from ActionDraft/session store.
2. Verify user/tenant/session/correlation.
3. Create/mark approvedActionId through IApprovalEngine.
4. Execute approved write through facade.
5. Return final result through AnswerComposer.
```

---

## 12. Answer Composer Requirements

### 12.1. Input envelope

Structured mode must accept this full context:

```csharp
public sealed record AnswerComposerRequest
{
    public required AnswerMode Mode { get; init; }
    public required string? CapabilityKey { get; init; }
    public required string? ProcedureName { get; init; }
    public required IReadOnlyDictionary<string, object?> Arguments { get; init; }
    public required ResultSchema? ResultSchema { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public required int RowCount { get; init; }
    public required ExecutionMetadata ExecutionMetadata { get; init; }
    public required SensitivityPolicy SensitivityPolicy { get; init; }
    public required string Locale { get; init; }
}
```

### 12.2. RawJson mode

RawJson mode must not call the LLM.

It returns:

```json
{
  "type": "raw_json",
  "capabilityKey": "...",
  "procedureName": "...",
  "arguments": {},
  "rowCount": 0,
  "rows": [],
  "resultSchema": {},
  "executionMetadata": {},
  "provenance": {}
}
```

Sensitive fields must be masked before returning.

### 12.3. Structured mode

Structured mode chooses one or more blocks:

```text
summary
table
chart
follow_up_question
confirmation
error
empty_result
```

Decision rules:

```text
missing required args -> follow-up question
ambiguous entity -> follow-up question with options
rowCount = 0 -> empty-result explanation + filters used
rowCount <= configured max -> summary + table
rowCount > configured max -> summary + top rows + suggest filter/export
time-series schema -> chart candidate
category + numeric schema -> bar chart candidate
write preview -> confirmation card
sensitive data -> mask/hide before LLM and before output
execution error -> safe error + correlationId
```

LLM summary is allowed only after:

```text
- sensitive data masking
- row limit enforcement
- result schema normalization
- provenance captured
```

---

## 13. Tests and Evals

### 13.1. Forbidden pattern tests

Add tests or static checks that fail if production code contains:

```text
CandidateGatedToolCallingAgent
_tools.FirstOrDefault()
_tools[0]
Regex-only ExtractArgumentValue as primary model argument extraction
Custom production AgentFunctionTool not converted to official AIFunction
```

Allow exceptions only in test projects with clear naming:

```text
FakeAgentRuntimeForTests
StubChatClient
```

### 13.2. Official framework usage tests

Add tests that prove:

```text
OfficialMicrosoftAgentRuntime references Microsoft.Agents.AI types.
OfficialMicrosoftAgentRuntime creates an official AIAgent.
OfficialAgentToolFactory returns official AIFunction tools or official supported tool type.
The official tools are passed to the official agent.
The callback path invokes CapabilityExecutionFacade.
```

### 13.3. Candidate gating tests

Given many capabilities across domains:

```text
- request exposes <= 12 tools
- request exposes <= 2 domains
- unrelated domain tools are not present
- write execute tools are never present
```

### 13.4. Tool selection tests

Use controlled model/provider tests where possible.

Test cases must prove:

```text
- model can select second or third candidate, not always first
- model can decline tools and ask clarification
- model extracts parameters from multilingual text
- model handles Vietnamese and English utterances
```

### 13.5. Eval dataset

Fix or create these datasets:

```text
tests/TILSOFTAI.Evals/Sprint_32/tool-routing.vi.jsonl
tests/TILSOFTAI.Evals/Sprint_32/tool-routing.en.jsonl
tests/TILSOFTAI.Evals/Sprint_32/argument-extraction.vi.jsonl
tests/TILSOFTAI.Evals/Sprint_32/argument-extraction.en.jsonl
tests/TILSOFTAI.Evals/Sprint_32/write-preview.vi.jsonl
tests/TILSOFTAI.Evals/Sprint_32/answer-composer.jsonl
```

Each eval case must include:

```json
{
  "utterance": "Tồn mã MADEIRA-BLK ở kho BD còn bao nhiêu?",
  "expectedDomains": ["warehouse"],
  "expectedFunction": "warehouse_inventory_by_item",
  "expectedCapabilityKey": "warehouse.inventory.by-item",
  "expectedArguments": {
    "item_no": "MADEIRA-BLK",
    "warehouse_code": "BD"
  },
  "shouldExecute": true,
  "requiresApproval": false
}
```

### 13.6. Minimum quality gates

```text
Tool selection exact match >= 90% on certified eval subset
Required argument detection >= 95%
False write execution = 0%
No direct SQL execution from agent tool callback = 100%
RawJson mode LLM calls = 0
Candidate limit compliance = 100%
```

---

## 14. Telemetry Requirements

Record these fields for each routed request:

```text
correlationId
tenantId
userId
locale
detectedLanguage
candidateDomains
candidateCapabilities
toolsAdvertisedToAgent
officialAgentProvider
officialAgentModel
agentSelectedFunction
agentFunctionArgumentsMasked
capabilityKey
procedureName
executionMode
rowCount
answerMode
latencyMs
validationFailures
clarificationReason
approvalDraftId
approvedActionId
errorCode
```

Do not log raw secrets, credentials, or sensitive field values.

---

## 15. Security Requirements

### 15.1. Production config

Production must not run with:

```json
"Auth": { "Enabled": false }
```

Production must not use:

```text
CORS "*"
AllowedHosts "*"
sa user
plain SQL password in committed appsettings.json
anonymous OpenAI-compatible ERP data access
```

### 15.2. Agent safety

The agent instructions must include:

```text
- Never generate SQL.
- Never invent ERP IDs.
- Use only provided tools.
- Ask clarification when required arguments are missing.
- Ask clarification when entity resolution is ambiguous.
- For create/update/delete/cancel/post operations, call preview tools only.
- Do not reveal hidden/sensitive fields.
```

These instructions are not a substitute for validation and policy checks.

---

## 16. Implementation Phases

### Phase 32.0 — Audit and remove fake agent code

Deliverables:

```text
- Identify all fake/custom Agent Framework classes.
- Remove CandidateGatedToolCallingAgent from production runtime.
- Remove first-candidate selection logic.
- Remove regex-only primary argument extraction from agent runtime.
- Keep hard signal extractor only as retrieval/context input, not final model argument binding.
```

Acceptance:

```text
- Forbidden pattern tests fail before fix and pass after fix.
- Production routing cannot select a tool without official agent invocation when AgentFramework mode is enabled.
```

---

### Phase 32.1 — Official dependencies and provider setup

Deliverables:

```text
- Add/verify official Microsoft.Agents.AI package.
- Add/verify Microsoft.Extensions.AI package.
- Add/verify provider package for the configured model provider.
- Register IChatClient through DI.
- Add OfficialMicrosoftAgentRuntime.
```

Provider must be configurable:

```text
Azure OpenAI
OpenAI-compatible endpoint
Other Microsoft.Extensions.AI-compatible provider if explicitly configured
```

Acceptance:

```text
- Build compiles with official packages.
- Runtime creates official AIAgent.
- No local class pretends to be AIAgent/AIFunction/AgentResponse.
```

---

### Phase 32.2 — SQL-backed dynamic tool descriptors

Deliverables:

```text
- Load candidate capability descriptors from SQL KB.
- Load localized capability and argument text.
- Load examples and aliases.
- Assemble CapabilityToolDescriptor per candidate capability.
```

Acceptance:

```text
- No manually hardcoded multilingual descriptions in production code.
- Changing SQL KB text changes the model-facing tool description without changing hand-written C#.
```

---

### Phase 32.3 — Official function tool factory

Deliverables:

```text
- Implement IOfficialAgentToolFactory.
- Convert CapabilityToolDescriptor to official AIFunction/tool.
- Map model-facing names to capability/proc argument names.
- Dispatch callbacks to CapabilityExecutionFacade.
```

Acceptance:

```text
- Tool objects passed to official agent are official tool/function types.
- Callback does not call SQL directly.
- Tool descriptions and schemas come from SQL-backed metadata.
```

---

### Phase 32.4 — Domain-gated official agent run

Deliverables:

```text
- Implement DomainGate.
- Implement candidate capability retrieval.
- Create per-request official agent with candidate tools only.
- Use official agent session/thread support where required for multi-turn.
```

Acceptance:

```text
- Never advertise full ERP tool catalog.
- Tool count limits enforced.
- Agent can select non-first candidate.
```

---

### Phase 32.5 — Official response mapping to AnswerComposer

Deliverables:

```text
- Map official agent response to internal ToolCallingResult.
- Detect tool execution envelope.
- Detect clarification/no-tool response.
- Send tool result to AnswerComposer.
```

Acceptance:

```text
- RawJson mode returns raw JSON envelope without LLM summary.
- Structured mode returns blocks through AnswerComposer.
- Agent final text is not blindly returned when structured/raw mode is configured.
```

---

### Phase 32.6 — Write preview and confirmation

Deliverables:

```text
- Only expose preview tools for mutations.
- Store pending action draft/session state.
- Implement confirmation turn mapping.
- Execute approved write through IApprovalEngine + facade only.
```

Acceptance:

```text
- Model cannot directly execute mutation.
- User confirmation is required.
- approvedActionId is required for final write.
```

---

### Phase 32.7 — Evals and observability

Deliverables:

```text
- Fix Sprint 31 eval naming mismatch.
- Add Sprint 32 eval datasets.
- Add quality gates.
- Add telemetry fields.
```

Acceptance:

```text
- Eval can catch fake first-tool selection.
- Eval can catch regex-only extraction.
- Eval can catch over-exposed tools.
```

---

## 17. Definition of Done

Sprint 32 is complete only when all are true:

```text
1. Official Microsoft Agent Framework runtime is used for tool/argument selection.
2. Fake/custom CandidateGatedToolCallingAgent is removed from production runtime.
3. Tool selection is not first-candidate selection.
4. Argument extraction is not regex-only/manual in the agent runtime.
5. Function tools are official AIFunction/tool types.
6. Function descriptions and parameter descriptions are built from SQL Server KB or generated from SQL-backed catalog.
7. Candidate/domain gating limits are enforced.
8. All tool callbacks go through CapabilityExecutionFacade.
9. No direct SQL execution from agent tool callbacks.
10. Write execution is impossible without user confirmation and approvedActionId.
11. RawJson and Structured answer modes both work.
12. RawJson mode does not call LLM.
13. Structured mode masks sensitive data before summary/output.
14. Eval dataset names match catalog/function names.
15. Forbidden-pattern tests pass.
16. Security config is not production-unsafe by default.
```

---

## 18. PR Checklist

Before opening the PR, verify:

```text
[ ] Official Agent Framework docs were checked for the current package/API.
[ ] Microsoft.Agents.AI is used in production runtime.
[ ] Microsoft.Extensions.AI is used for official function tools/provider integration.
[ ] Official AIAgent is created and invoked.
[ ] Official AIFunction/tool objects are passed to the agent.
[ ] No fake production tool-calling agent remains.
[ ] No first-tool selection logic remains.
[ ] No regex-only primary extraction remains.
[ ] Candidate gating is enforced.
[ ] Tool descriptions are SQL-backed/dynamic/generated, not hand-hardcoded.
[ ] Execution facade is mandatory for every tool callback.
[ ] AnswerComposer is mandatory for final output.
[ ] Write flow is preview -> user confirmation -> approval -> execute.
[ ] Tests and evals updated.
[ ] Telemetry records advertised tools and selected function.
[ ] Production config does not disable auth or expose wildcard CORS.
```

---

## 19. Out of Scope for Sprint 32

Do not implement these unless already trivial:

```text
- Full multi-agent workflow orchestration for every ERP domain.
- Full MCP server integration.
- Full UI redesign.
- Massive catalog migration for all ERP procedures.
- Fine-tuning.
- Autonomous write execution without confirmation.
```

Sprint 32 is a corrective sprint. Its job is to make the existing Sprint 31 architecture honest and production-aligned by using the official Microsoft Agent Framework as the real AI tool/parameter brain.

---

## 20. Final Instruction to Codex Agent

Do not “simulate” Microsoft Agent Framework.

If official API usage is unclear:

```text
1. Inspect current official Microsoft Agent Framework docs.
2. Inspect installed package public APIs.
3. Implement against official APIs.
4. If impossible, stop with a clear build-time note and propose the smallest official-compatible alternative.
```

Never replace the official framework with a fake local implementation just to make tests pass.
