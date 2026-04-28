# Sprint 33 — Official Microsoft Agent Framework Refactor

> Audience: Codex Agent / implementation agent  
> Language: English only  
> Objective: Replace all invented, regex-driven, or manually simulated agent/tool-calling logic with the **official Microsoft Agent Framework** as the tool-selection and argument-binding brain.

---

## 0. CTO Decision

The project must use the **official Microsoft Agent Framework** for natural-language-to-tool orchestration.

This sprint is not about building a better regex router. It is a corrective refactor to remove custom/fake agent implementations and make the system rely on official Microsoft Agent Framework agents and function tools.

The ERP safety boundary remains inside TILSOFTAI:

```text
Microsoft Agent Framework
  -> chooses candidate function tool
  -> binds function arguments from natural language

CapabilityExecutionFacade
  -> checks tenant/user/role/policy
  -> resolves entities
  -> validates argument contract
  -> enforces read/write rules
  -> routes to approved SQL stored procedures

AnswerComposer
  -> controls raw JSON / structured / summary output
```

The model/agent may choose tools and arguments, but it must never directly execute SQL or bypass ERP governance.

---

## 1. Non-Negotiable Rules

### 1.1 Must use official Microsoft Agent Framework

The implementation must use official Microsoft packages and types, such as:

```text
Microsoft.Agents.AI
Microsoft.Agents.AI.OpenAI
Microsoft.Extensions.AI
Microsoft.Extensions.AI.Ollama, only if the local provider path is explicitly selected
```

Use the official provider path that matches the runtime configuration:

```text
Preferred production path:
  Azure OpenAI or OpenAI provider via Microsoft Agent Framework

Allowed local/dev path:
  Official Ollama provider through Microsoft Agent Framework,
  only for models that pass function-calling evals
```

### 1.2 Forbidden fake/invented agent code

Remove or permanently disable any code that behaves like a hand-written agent brain.

Forbidden patterns:

```text
CandidateGatedToolCallingAgent
FakeMicrosoftAgentFramework
Custom IToolCallingAgent that chooses the tool itself
Custom AgentFunctionTool that is not an official AIFunction/function tool
selectedTool = tools.FirstOrDefault()
selectedTool = candidates[0]
Regex ExtractArgumentValue as the main argument extractor
Keyword/regex intent router as the final tool selector
Manual OpenAI-compatible tool-call loop as the primary brain
Manual parsing of model tool_calls as the primary brain
execute_proc(proc_name, args_json) exposed to the agent
execute_tool(capability_key, args_json) exposed to the agent
```

If any of these are kept for legacy comparison, they must be isolated under tests only and must not be wired into runtime DI.

### 1.3 Regex is allowed only as hard-signal support

Regex may only be used for deterministic, non-semantic hints:

```text
Allowed:
  - extract obvious document numbers
  - extract obvious numeric quantities
  - extract obvious ISO-like dates
  - normalize whitespace/punctuation
  - produce retrieval hints

Forbidden:
  - deciding the final ERP domain
  - deciding the final function/tool
  - deciding business intent
  - filling core business arguments as the main mechanism
  - choosing read vs write action
```

Regex output must be treated as `HardSignals`, not as final routing.

### 1.4 Candidate gating is mandatory

Never expose the full ERP tool catalog to the agent.

Limits:

```text
maxDomainsPerRequest: 2
maxToolsPerDomain: 6
maxTotalToolsPerRequest: 10
```

The agent must receive only candidate tools selected by semantic retrieval from SQL Server 2025 KB.

### 1.5 SQL Server 2025 KB is the metadata source of truth

Do not hardcode production function descriptions, parameter descriptions, aliases, examples, or result schema in C#.

SQL Server 2025 must store:

```text
capabilities
domains
tool/function names
stored procedure bindings
argument contracts
multilingual tool descriptions
multilingual parameter descriptions
aliases
examples
result schemas
answer policies
sensitivity policies
entity aliases
knowledge chunks / embeddings
execution traces
```

C# should load metadata dynamically and build official Agent Framework tools from it.

### 1.6 Agent tools must call CapabilityExecutionFacade

Agent function callbacks must never call SQL directly.

Correct path:

```text
Official AIFunction callback
  -> CapabilityExecutionFacade.ExecuteReadAsync / PreviewWriteAsync / ExecuteApprovedWriteAsync
  -> policy / validation / approval / adapter
  -> SQL proc
```

### 1.7 Write actions are preview-only in agent tools

Do not expose direct write-execute tools to the agent.

Allowed agent-facing write tools:

```text
sales_order_create_preview
purchasing_po_create_preview
warehouse_receipt_create_preview
model_note_create_preview
```

Forbidden agent-facing tools:

```text
sales_order_create_execute
purchasing_po_create_execute
warehouse_receipt_create_execute
direct_update_erp
```

Execution requires a user confirmation and an approved action id.

---

## 2. Official References

Use current Microsoft documentation as the implementation source, not invented APIs.

Official docs:

```text
Microsoft Agent Framework overview:
https://learn.microsoft.com/en-us/agent-framework/overview/

Function tools:
https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools

Tool approval / human-in-the-loop:
https://learn.microsoft.com/en-us/agent-framework/agents/tools/tool-approval

Providers:
https://learn.microsoft.com/en-us/agent-framework/agents/providers/

Azure OpenAI provider:
https://learn.microsoft.com/en-us/agent-framework/agents/providers/azure-openai

OpenAI provider:
https://learn.microsoft.com/en-us/agent-framework/agents/providers/openai

Ollama provider:
https://learn.microsoft.com/en-us/agent-framework/agents/providers/ollama

Microsoft.Extensions.AI AIFunctionFactory:
https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.aifunctionfactory.create
```

Important implementation facts:

```text
- Function tools are custom code the agent may call when needed.
- In .NET, create official function tools using AIFunctionFactory.Create(...).
- Agents are created from official provider clients with AsAIAgent(...).
- Human approval can be represented through official approval function tool mechanisms, but TILSOFTAI approval remains the ERP source of truth.
- Not every local model supports function calling. Local providers must pass evals before runtime use.
```

---

## 3. Target Architecture

```text
User Message
  -> ChatController / ChatHub / OpenAI-compatible API surface
  -> SupervisorRuntime
  -> OfficialAgentToolRouter
       -> Language detection / locale selection
       -> HardSignalExtractor, hints only
       -> SQL Server 2025 Semantic KB retrieval
       -> Domain-gated candidate capability selection
       -> Dynamic official AIFunction creation
       -> Official Microsoft Agent Framework AIAgent.RunAsync(...)
  -> CapabilityExecutionFacade
       -> tenant/user/role/access policy
       -> entity alias resolution
       -> argument contract validation
       -> read/write execution mode guard
       -> approval guard
       -> tool adapter / SQL adapter
  -> AnswerComposer
       -> RawJson
       -> Structured
       -> Summary
       -> Table
       -> Chart candidate
       -> Follow-up question
       -> Write confirmation card
```

---

## 4. Required Project Changes

### 4.1 Add official Agent Framework packages

Update the appropriate `.csproj` files.

At minimum, orchestration must reference official packages. Choose the provider package based on configuration.

Example package intent:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Agents.AI" Version="*" />
  <PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="*" />
  <PackageReference Include="Microsoft.Extensions.AI" Version="*" />
</ItemGroup>
```

Do not hardcode preview/stable versions blindly. Use the latest compatible version available to the project target framework. If a package is preview-only, document this in the PR.

### 4.2 Remove fake agent implementations

Search and remove or runtime-disable the following if present:

```text
src/**/MicrosoftAgentFramework/AgentClientFactory.cs
src/**/MicrosoftAgentFramework/AgentFrameworkModels.cs
src/**/CandidateGatedToolCallingAgent*.cs
src/**/AgentFunctionTool*.cs, if custom and not official
src/**/OpenAiCompatible manual tool-call loop used as the main brain
src/**/Regex ExtractArgumentValue methods used for business parameter extraction
```

Runtime DI must no longer resolve a custom/fake agent implementation.

### 4.3 Add official router interfaces

Create or replace with:

```csharp
public interface IOfficialAgentToolRouter
{
    Task<AgentToolRouterResult> RouteAsync(
        AgentToolRouterRequest request,
        CancellationToken cancellationToken);
}

public sealed record AgentToolRouterRequest
{
    public required string UserMessage { get; init; }
    public required TilsoftExecutionContext ExecutionContext { get; init; }
    public required string Locale { get; init; }
    public required AnswerMode AnswerMode { get; init; }
    public string? ConversationId { get; init; }
}

public sealed record AgentToolRouterResult
{
    public required bool Handled { get; init; }
    public required AnswerComposerRequest? AnswerRequest { get; init; }
    public string? ClarificationQuestion { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
```

### 4.4 Add official agent provider factory

Create:

```csharp
public interface IOfficialAgentProviderFactory
{
    AIAgent CreateAgent(
        string instructions,
        IReadOnlyList<AIFunction> tools,
        TilsoftExecutionContext executionContext);
}
```

Provider selection must be configuration-driven:

```json
{
  "AiRouting": {
    "Provider": "AzureOpenAI",
    "Model": "gpt-4o-mini",
    "MaxCandidateTools": 10,
    "UseOfficialMicrosoftAgentFramework": true
  }
}
```

Allowed providers:

```text
AzureOpenAI
OpenAI
OllamaOfficialProviderForDevOnly
```

Do not create a custom tool-calling brain to emulate Agent Framework.

### 4.5 Add official function tool provider

Create:

```csharp
public interface IOfficialAgentFunctionProvider
{
    Task<IReadOnlyList<AIFunction>> BuildFunctionsAsync(
        IReadOnlyList<CapabilityCandidate> candidates,
        TilsoftExecutionContext executionContext,
        string locale,
        CancellationToken cancellationToken);
}
```

Each returned tool must be an official `AIFunction`.

The function callback must call:

```csharp
CapabilityExecutionFacade.ExecuteReadAsync(...)
CapabilityExecutionFacade.PreviewWriteAsync(...)
CapabilityExecutionFacade.ExecuteApprovedWriteAsync(...)
```

The callback must not call SQL directly.

---

## 5. Dynamic Function Description Strategy

Because the system is multilingual and ERP-specific, function descriptions and parameter descriptions must be dynamic.

### 5.1 SQL Server tables

If not already present, add or complete these tables:

```sql
ai.Capability
ai.CapabilityArgument
ai.CapabilityText
ai.ArgumentText
ai.CapabilityExample
ai.KnowledgeChunk
ai.EntityAlias
ai.ToolExecutionTrace
```

Required data:

```text
ai.Capability:
  CapabilityKey
  Domain
  FunctionName
  StoredProcedure
  AdapterType
  ExecutionMode
  IsEnabled
  RequiredRoles
  ArgumentContractJson
  ResultSchemaJson
  AnswerPolicyJson
  SensitivityPolicyJson

ai.CapabilityText:
  CapabilityKey
  Locale
  Description
  PositiveExamplesJson
  NegativeGuidance
  AliasesJson

ai.ArgumentText:
  CapabilityKey
  ArgumentName
  Locale
  Description
  AliasesJson
  ExamplesJson
  ClarificationQuestion

ai.KnowledgeChunk:
  ChunkType
  ObjectKey
  Domain
  Locale
  ContentText
  MetadataJson
  Embedding
```

### 5.2 Dynamic description builder

Create:

```csharp
public interface ICapabilityToolDescriptionBuilder
{
    Task<CapabilityToolDescription> BuildAsync(
        CapabilityDescriptor capability,
        string locale,
        CancellationToken cancellationToken);
}
```

Output:

```csharp
public sealed record CapabilityToolDescription
{
    public required string FunctionName { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyDictionary<string, string> ParameterDescriptions { get; init; }
    public required IReadOnlyList<string> PositiveExamples { get; init; }
    public required IReadOnlyList<string> NegativeGuidance { get; init; }
}
```

Description format:

```text
Purpose:
  <localized description>

Use when:
  - <aliases/examples>

Do not use when:
  - <negative guidance>

Required business context:
  - <required params and clarification rules>
```

Do not write production descriptions in C# attributes unless used only as fallback.

### 5.3 AIFunction dynamic metadata

Prefer the official API path that allows providing name/description dynamically.

Use one of these acceptable strategies:

```text
Strategy A — Dynamic AIFunctionFactory.Create delegate with options:
  Create a delegate that receives AIFunctionArguments.
  Set function name and description from SQL metadata using official AIFunctionFactory options/overload.
  Map model-facing argument names to capability argument names inside the callback.
  Validate via CapabilityExecutionFacade.

Strategy B — Generated typed tool methods:
  Generate C# source from SQL-certified capability catalog.
  Use official AIFunctionFactory.Create on generated methods.
  Regenerate when catalog changes.
  Use generated XML/Description metadata only as an output of SQL catalog, not manually maintained hardcode.
```

Do not use `CreateDeclaration(...)` as an invocable tool unless the official API explicitly supports attaching invocation. A declaration-only object is not enough for ERP execution.

---

## 6. Semantic Candidate Gating

### 6.1 Required service

Create:

```csharp
public interface ICapabilityCandidateSelector
{
    Task<IReadOnlyList<CapabilityCandidate>> SelectAsync(
        string userMessage,
        HardSignalSet hardSignals,
        TilsoftExecutionContext context,
        string locale,
        CancellationToken cancellationToken);
}
```

### 6.2 Selection must be semantic-first

Use SQL Server 2025 KB retrieval:

```text
Primary:
  vector / embedding search over ai.KnowledgeChunk, CapabilityText, ArgumentText, examples

Secondary:
  entity alias matching
  multilingual aliases
  domain priors
  hard-signal boost

Never:
  regex-only intent selection
  keyword-only final selection
```

Suggested ranking:

```text
score =
  0.55 * vectorSimilarity
+ 0.20 * multilingualAliasScore
+ 0.10 * exampleSimilarity
+ 0.10 * entityAliasScore
+ 0.05 * hardSignalBoost
```

If embeddings are not available yet, implement a temporary hybrid selector but mark it as `SemanticMode = TextFallback`. The runtime must log that fallback mode is active. Do not claim it is semantic/vector if it is not.

### 6.3 Hard limits

```text
maxDomainsPerRequest = 2
maxToolsPerDomain = 6
maxTotalTools = 10
```

### 6.4 No full catalog prompt

The agent must never receive the entire ERP tool catalog.

Add a test that fails when more than `maxTotalTools` are provided to the agent.

---

## 7. CapabilityExecutionFacade Requirements

The existing facade should be retained or created if missing.

Required interface:

```csharp
public interface ICapabilityExecutionFacade
{
    Task<CapabilityExecutionEnvelope> ExecuteReadAsync(
        string capabilityKey,
        IReadOnlyDictionary<string, object?> modelFacingArguments,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken);

    Task<CapabilityExecutionEnvelope> PreviewWriteAsync(
        string capabilityKey,
        IReadOnlyDictionary<string, object?> modelFacingArguments,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken);

    Task<CapabilityExecutionEnvelope> ExecuteApprovedWriteAsync(
        string capabilityKey,
        string approvedActionId,
        IReadOnlyDictionary<string, object?> modelFacingArguments,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken);
}
```

Execution rules:

```text
1. Load capability by key.
2. Check IsEnabled.
3. Check tenant/user/roles.
4. Resolve entity aliases.
5. Map model-facing parameters to stored procedure parameters.
6. Validate against argument contract.
7. Reject missing/extra/type-invalid arguments.
8. Enforce stored procedure allowlist; SQL proc must be ai_* or configured safe.
9. Enforce execution mode.
10. For write: preview only unless approvedActionId is valid.
11. Call adapter.
12. Store trace.
13. Return envelope for AnswerComposer.
```

The facade must be the only path from agent tool callbacks into ERP execution.

---

## 8. AnswerComposer Requirements

Add or preserve a clear answer composer layer.

### 8.1 Interface

```csharp
public interface IAnswerComposer
{
    Task<AssistantAnswer> ComposeAsync(
        AnswerComposerRequest request,
        CancellationToken cancellationToken);
}

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

### 8.2 Required modes

```csharp
public enum AnswerMode
{
    RawJson,
    Structured
}
```

### 8.3 RawJson mode

RawJson mode must not call the LLM.

Return deterministic envelope:

```json
{
  "mode": "raw_json",
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

### 8.4 Structured mode

Structured mode decides output blocks:

```text
if clarification needed:
  follow_up_question

if rowCount = 0:
  no_data_answer + filters used

if rowCount <= table threshold:
  summary + table

if rowCount > threshold:
  summary + top rows + suggest filter/export

if result schema has time dimension + numeric measure:
  chart candidate

if write preview:
  confirmation card

if sensitive fields:
  mask before display or summary
```

Structured mode may optionally use an LLM to polish natural language, but only after sensitive fields are masked.

### 8.5 Agent final answer is not the system final answer

The Agent Framework may produce a response, but final user output must pass through `AnswerComposer` for ERP consistency, sensitivity handling, JSON mode, and UI blocks.

---

## 9. Write Preview / Approval Flow

### 9.1 Agent-facing tools

Only preview tools are visible to the agent.

Example:

```text
purchasing_po_create_preview(...)
sales_order_create_preview(...)
warehouse_receipt_create_preview(...)
```

Preview tool returns:

```json
{
  "type": "write_preview",
  "pendingActionId": "...",
  "capabilityKey": "...",
  "arguments": {},
  "summary": "...",
  "requiresUserConfirmation": true
}
```

### 9.2 Confirmation

User confirmation must be processed by application logic, not by letting the model call execute directly.

Required checks:

```text
same tenant
same user
same conversation/session
pending action not expired
action not already executed
arguments unchanged or approved diff exists
```

### 9.3 Execution

Only after confirmation:

```text
IApprovalEngine approves action
CapabilityExecutionFacade.ExecuteApprovedWriteAsync(...)
SqlToolAdapter receives approvedActionId
```

### 9.4 Optional official approval wrapper

Microsoft Agent Framework approval tools may be used as a UX/runtime convenience, but TILSOFTAI `IApprovalEngine` remains the ERP source of truth.

---

## 10. Implementation Phases

### Phase 0 — Inventory and removal

Goal: remove fake/invented agent brain.

Tasks:

```text
- Search for custom fake agent classes.
- Search for FirstOrDefault-based tool selection.
- Search for regex-based business argument extraction.
- Search for manual OpenAI-compatible tool-call loops used as the primary runtime.
- Remove these from DI/runtime.
- Add tests that fail if they are accidentally used again.
```

Deliverables:

```text
- Runtime no longer resolves fake agent.
- Build passes.
- Legacy code either deleted or isolated under tests only.
```

### Phase 1 — Official Microsoft Agent Framework provider

Goal: create a real `AIAgent` from an official provider.

Tasks:

```text
- Add official packages.
- Add provider config.
- Implement IOfficialAgentProviderFactory.
- Support AzureOpenAI/OpenAI first.
- Support official Ollama only as dev/local option if required.
- Add health check showing selected official provider and model.
```

Deliverables:

```text
- A real AIAgent is created.
- A simple no-tool prompt works.
- A simple official function tool can be called.
```

### Phase 2 — Official function tools from capabilities

Goal: build official AIFunction tools dynamically from candidate capabilities.

Tasks:

```text
- Implement IOfficialAgentFunctionProvider.
- Build official AIFunction per candidate capability.
- Load function name/description/parameter descriptions from SQL KB.
- Callback calls CapabilityExecutionFacade only.
- No SQL call in tool provider.
```

Deliverables:

```text
- Agent can call one read-only tool.
- Function metadata comes from SQL.
- Callback produces CapabilityExecutionEnvelope.
```

### Phase 3 — Semantic candidate gating

Goal: agent receives only relevant tools.

Tasks:

```text
- Implement ICapabilityCandidateSelector.
- Use SQL Server KB and embeddings/vector retrieval where available.
- Use hard signals only as boost.
- Enforce max domains/tools.
- Log selected domains/tools and scores.
```

Deliverables:

```text
- No request sends the full catalog.
- Candidate order can be intentionally wrong and agent still selects the correct tool.
```

### Phase 4 — Router integration

Goal: replace existing ChatPipeline brain.

Tasks:

```text
- Wire OfficialAgentToolRouter into SupervisorRuntime/ChatPipeline.
- Remove manual OpenAI-compatible tool-calling loop from runtime path.
- Preserve API contracts.
- Preserve streaming only if compatible with official agent runtime; otherwise return non-streaming for this sprint.
```

Deliverables:

```text
- Chat endpoint uses official Agent Framework path.
- RawJson and Structured output both go through AnswerComposer.
```

### Phase 5 — AnswerComposer hardening

Goal: deterministic ERP output layer.

Tasks:

```text
- Implement RawJson mode with no LLM final call.
- Implement Structured blocks.
- Apply sensitivity policy before output.
- Add locale-aware formatting.
- Add row count/table thresholds.
```

Deliverables:

```text
- RawJson mode returns stable envelope.
- Structured mode returns text/table/chart/follow-up blocks.
```

### Phase 6 — Write preview only

Goal: safe create/update path.

Tasks:

```text
- Add one preview write capability.
- Do not expose execute tool.
- Implement pending action confirmation.
- Enforce approval id before SQL write.
```

Deliverables:

```text
- Agent prepares write preview.
- User confirmation required.
- No direct write execute through agent.
```

---

## 11. Required Tests

### 11.1 Anti-fake tests

Must fail if:

```text
- runtime uses CandidateGatedToolCallingAgent or equivalent fake agent
- selected tool is always the first candidate
- regex extraction is used as the main argument binding path
- custom AgentFunctionTool is used instead of official AIFunction
- manual tool_calls parser is used as the main runtime
```

### 11.2 Candidate order test

Arrange candidate list intentionally wrong:

```text
1. sales_orders_by_customer
2. model_get_overview
3. purchasing_open_po_by_supplier
```

User:

```text
"Nhà cung cấp ABC còn đơn nào chưa nhập xong không?"
```

Expected:

```text
purchasing_open_po_by_supplier
```

Fail if first tool is selected.

### 11.3 Multilingual tests

Vietnamese:

```text
"Tồn mã A123 ở kho Bình Dương còn bao nhiêu?"
```

English:

```text
"Show open purchase orders for supplier ABC that are not fully received."
```

Mixed:

```text
"Check stock item A123 ở kho BD."
```

Expected:

```text
official agent selects correct function and binds arguments
```

### 11.4 Missing argument test

User:

```text
"Xem PO chưa nhận đủ."
```

Expected:

```text
No SQL execution.
AnswerComposer returns a follow-up question asking for supplier, PO number, or date filter depending on capability contract.
```

### 11.5 Write safety test

User:

```text
"Tạo PO cho supplier ABC, item A123 số lượng 500."
```

Expected:

```text
Agent calls preview tool only.
No write stored procedure is executed.
Pending action is created.
User confirmation is required.
```

### 11.6 RawJson test

Set:

```text
AnswerMode = RawJson
```

Expected:

```text
Tool executes.
LLM is not called after tool result.
Raw JSON envelope is returned.
```

### 11.7 Tool limit test

Expected:

```text
No agent run receives more than maxTotalTools.
```

---

## 12. Observability Requirements

Log and trace:

```text
request id
conversation id
tenant id
user id
locale
provider
model
selected candidate domains
candidate tools and scores
actual tool called by agent
model-facing arguments
mapped proc arguments, masked
capability key
stored procedure
execution duration
row count
answer mode
approval/pending action id if applicable
errors
```

Add metrics:

```text
agent_tool_selection_count
agent_tool_selection_failure_count
agent_argument_validation_failure_count
candidate_retrieval_latency_ms
agent_run_latency_ms
tool_execution_latency_ms
answer_composer_latency_ms
raw_json_response_count
structured_response_count
write_preview_count
write_execute_count
```

---

## 13. PR Acceptance Criteria

A PR for this sprint is accepted only if all are true:

```text
[ ] Official Microsoft Agent Framework packages are referenced.
[ ] Runtime creates a real AIAgent from an official provider.
[ ] Runtime uses official AIFunction/function tools.
[ ] Fake/custom agent brain is deleted or unreachable from runtime DI.
[ ] No FirstOrDefault/candidate[0] tool selection.
[ ] No regex/keyword final tool routing.
[ ] No regex-driven business argument extraction as primary mechanism.
[ ] Function descriptions and parameter descriptions come from SQL KB.
[ ] Candidate gating limits tools per request.
[ ] Tool callback calls CapabilityExecutionFacade only.
[ ] SQL adapter is never called directly from agent/tool provider.
[ ] RawJson mode bypasses final LLM summary.
[ ] Structured mode goes through AnswerComposer.
[ ] Write action is preview-only until approval.
[ ] Tests prove wrong candidate order still selects correct tool.
[ ] Tests prove missing params produce follow-up, not SQL execution.
[ ] Tests prove no direct write through agent.
```

---

## 14. Definition of Done

The sprint is done when a real user can ask a multilingual ERP question and the following happens:

```text
1. SQL Server 2025 KB retrieves a small set of relevant candidate capabilities.
2. Official Microsoft Agent Framework receives official function tools only.
3. The official agent selects the correct function and binds arguments.
4. The official function callback calls CapabilityExecutionFacade.
5. Facade validates policy/arguments and executes the safe ai_* proc.
6. AnswerComposer returns either RawJson or Structured output.
7. Trace shows the full path.
8. No fake agent, manual tool-call parser, first-candidate selector, or regex business router is used.
```

---

## 15. Implementation Notes for the Agent

Do not optimize for preserving old code. Optimize for a clean, correct runtime path.

Preferred final runtime path:

```text
SupervisorRuntime
  -> OfficialAgentToolRouter
  -> CapabilityCandidateSelector
  -> OfficialAgentFunctionProvider
  -> Official Microsoft AIAgent
  -> CapabilityExecutionFacade
  -> AnswerComposer
```

Do not create another abstraction that secretly re-implements function calling. The abstraction is allowed only to protect project code from provider details; it must delegate the brain behavior to official Microsoft Agent Framework.

