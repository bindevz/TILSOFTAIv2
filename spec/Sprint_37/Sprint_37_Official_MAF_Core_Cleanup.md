# Sprint 37 — Official Microsoft Agent Framework Core Cleanup

## Goal

Refactor the project into a clean **Agent Framework Core** using the read-only `model` domain as the only reference runtime domain.

This sprint is not for adding features. It is for removing unused legacy code, deleting invented/fake framework leftovers, simplifying the runtime, and aligning the implementation with the official Microsoft Agent Framework design.

## Target Runtime

```text
API
  -> SupervisorRuntime
  -> Official Microsoft Agent Framework router
  -> Local AI / configured provider
  -> model-only AIFunction tools
  -> CapabilityExecutionFacade
  -> SQL read-only ai_model_* stored procedures
  -> AnswerComposer
  -> API response
```

## Non-Negotiable Rules

1. The active brain is the official Microsoft Agent Framework.
2. The active runtime domain is `model` only.
3. Do not use regex, keyword classifiers, or first-candidate selection as the active tool-routing brain.
4. Do not advertise non-model tools.
5. Do not keep fake/custom agent frameworks.
6. Do not keep unused domain agents or unused capability sources in the active runtime.
7. `CapabilityExecutionFacade` is the only execution boundary.
8. `AnswerComposer` is the only final response boundary.
9. Raw JSON mode must not call the LLM after tool execution.
10. Real write execution remains disabled.

---

## Phase 0 — Build, Test, and Inventory

Run first:

```bash
dotnet build
dotnet test
```

Then inventory active references:

```bash
rg "KeywordIntentClassifier|StructuredCapabilityResolver|AccountingAgent|WarehouseAgent|AccountingCapabilities|WarehouseCapabilities|DomainAgentBase|CandidateGatedToolCallingAgent|FirstOrDefault\("
rg "Sprint35|Sprint_35|sprint-35"
rg "modelId|model_id"
```

Create a short cleanup note in the PR:

```text
- Removed unused legacy routing code
- Kept official Microsoft Agent Framework path
- Kept model-only read runtime
- Kept CapabilityExecutionFacade and AnswerComposer boundaries
```

---

## Phase 1 — Restore Code Readability

Several runtime files were compressed into single-line C# files. Reformat them into normal readable C#.

Required files to review:

```text
src/TILSOFTAI.Orchestration/OrchestrationServiceCollectionExtensions.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialAgentToolRouter.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialMicrosoftAgentRuntime.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialAgentProviderFactory.cs
src/TILSOFTAI.Orchestration/AiRouting/Tools/DynamicFunctionToolFactory.cs
src/TILSOFTAI.Orchestration/Capabilities/ModelCapabilities.cs
src/TILSOFTAI.Orchestration/Actions/IActionRequestStore.cs
src/TILSOFTAI.Orchestration/Observability/AgentRoutingTraceEvents.cs
```

Acceptance:

```text
- No production C# file is intentionally minified.
- Files are readable and reviewable.
- dotnet format or equivalent formatting passes.
```

---

## Phase 2 — Delete or Isolate Legacy Routing

The active runtime must not depend on the old keyword/domain-agent route.

Remove from active DI and delete if no longer referenced:

```text
KeywordIntentClassifier
IIntentClassifier
StructuredCapabilityResolver
AccountingAgent
WarehouseAgent
DomainAgentBase
AccountingCapabilities
WarehouseCapabilities
ConfigurationCapabilitySource, if unused by active model runtime
ToolRegistry / ToolGovernance / ToolDefinition, if only used by obsolete OpenAI-compatible loop
```

If a type is still needed only by tests or historical compatibility, move it to a clearly named legacy area or keep it unregistered:

```text
TILSOFTAI.Orchestration.Legacy
```

Do not let legacy services appear in the active model runtime registration.

Acceptance:

```text
- Active DI does not register legacy keyword/domain-agent routing.
- No non-model domain agent is active.
- No old capability resolver is used by the model runtime.
- Tests prove official routing fails closed instead of falling back to legacy routing.
```

---

## Phase 3 — Simplify Dependency Injection

Refactor `OrchestrationServiceCollectionExtensions.cs` into clear sections.

Keep only these active sections:

```csharp
AddModelOnlyCapabilities();
AddOfficialAgentFrameworkCore();
AddCapabilityExecutionBoundary();
AddAnswerComposer();
AddPendingActionState();
```

Avoid a broad catch-all method such as:

```text
AddLegacyFallbackOnlyServices
```

unless legacy fallback is explicitly required by a separate test configuration. If kept, it must not run in model-only core settings.

Acceptance:

```text
- It is obvious which services are active.
- There is no accidental registration of accounting, warehouse, purchasing, or sales domain code.
- Model-only runtime still runs through official agent routing.
```

---

## Phase 4 — Align Function Tools with Official Agent Framework

Review:

```text
OfficialAgentProviderFactory.cs
OfficialMicrosoftAgentRuntime.cs
DynamicFunctionToolFactory.cs
CapabilityToolDescriptorFactory.cs
CapabilityParameterSchemaBuilder.cs
CapabilityFunctionNameMapper.cs
```

Rules:

```text
- Use official AIAgent.
- Use official AIFunction tools.
- Prefer AIFunctionFactory.Create(...) when practical.
- If DescriptorBackedAIFunction remains, it must be a thin adapter over official AIFunction semantics.
- Do not implement a fake agent runtime.
- Do not parse OpenAI tool calls manually as the active brain.
- Do not select the first candidate tool manually.
```

Acceptance:

```text
- Agent receives candidate AIFunction tools.
- Tool schema is visible to the agent.
- Tool invocation calls CapabilityExecutionFacade.
- Anti-regression test fails if the first candidate is always selected.
```

---

## Phase 5 — Keep Model-Only Capabilities Clean

Active capabilities must remain exactly:

```text
model.count
model.overview.by-code
model.pieces.by-code
model.materials.by-code
model.compare
model.packaging.by-code
```

Model-facing arguments:

```text
modelCode: string
modelCodes: string[]
season: string, optional for count only
```

Rules:

```text
- No active model-facing schema uses modelId or model_id.
- SQL wrappers may resolve modelCode internally.
- The agent must never be asked to invent database IDs.
- Non-model capabilities must not be in the active platform catalog.
```

Acceptance:

```text
- `rg "modelId|model_id"` finds no active model-facing tool contract.
- Mixed candidate tests advertise only model tools.
- Missing `modelCode` returns follow-up, not SQL execution.
```

---

## Phase 6 — Simplify OfficialAgentToolRouter

`OfficialAgentToolRouter` currently does too much. Split it only where it improves clarity.

Suggested extraction:

```text
AgentRouteLogger
AgentRouteMetrics
AgentRouteTraceWriter
AgentRouteFailureHandler
AgentConfirmationTurnHandler
```

Do not over-engineer. The router should read like:

```text
extract hard signals
resolve confirmation, if any
select candidates
build tools
run official agent
map result
compose answer
trace result
```

Rules:

```text
- Hard signals are hints only.
- Hard signals must not choose the final tool.
- Confirmation handling must not execute write actions without approved state.
```

Acceptance:

```text
- Router is readable.
- No function exceeds a reasonable size unless unavoidable.
- Core route is easy to trace in logs.
```

---

## Phase 7 — Standardize AnswerComposer

Keep only two public output modes:

```text
RawJson
Structured
```

RawJson:

```text
- no final LLM call
- deterministic envelope
- includes capability, procedure, arguments, rowCount, rows, resultSchema, executionMetadata, provenance
```

Structured:

```text
- deterministic answer blocks
- text + blocks + provenance
- follow-up for missing params
- no-data for empty rows
- table for small rowsets
- mask sensitive fields before rendering
```

Remove duplicate summary paths or obsolete response-formatting code.

Acceptance:

```text
- RawJson test proves no LLM summary call.
- Structured test proves blocks and provenance.
- No-data and follow-up are covered.
```

---

## Phase 8 — Keep Pending Action State, But Disable Real Write

Keep pending action state as future infrastructure. Do not enable real write tools.

Required store contract:

```csharp
CreateAsync
GetAsync
GetActiveForConversationAsync
ConfirmAsync
RejectAsync
MarkExecutedAsync
ExpireOldAsync
```

Rules:

```text
- Tenant/user/conversation isolation is mandatory.
- Expired actions cannot be confirmed.
- Confirmation does not execute write directly.
- The active model read-only runtime must not advertise write tools.
```

Acceptance:

```text
- Pending action tests cover user isolation.
- Expired pending action returns a safe response.
- No active write function is advertised.
```

---

## Phase 9 — Remove Unused Framework Pieces

After the active path is clean, remove dead code.

Delete or isolate if not referenced by active model runtime:

```text
Old domain agents
Old keyword classifier
Old structured resolver
Old non-model capability descriptors
Old OpenAI-compatible manual tool-calling loop
Old tool registry/governance classes if obsolete
Unused analytics/prompt/context-pack services not used by model runtime
Unused metrics that are never emitted
Empty architecture guard test files
```

Do not delete:

```text
Official Microsoft Agent Framework runtime
Model capabilities
CapabilityExecutionFacade
AnswerComposer
SqlToolAdapter
ApprovalEngine
PendingActionStore
Model local AI runbook
Agent routing trace infrastructure
```

Acceptance:

```text
- `dotnet build` passes after deletion.
- `dotnet test` passes after deletion.
- No active runtime references deleted components.
```

---

## Phase 10 — Final Tests

Required tests:

```text
- Official agent route is enabled and fail-closed.
- Model-only candidates are advertised.
- Non-model candidates are filtered out.
- Wrong candidate order does not force wrong tool selection.
- modelCode/modelCodes contracts are used.
- Missing modelCode returns follow-up.
- RawJson mode returns raw envelope.
- Structured mode returns blocks/provenance.
- Pending action confirmation is tenant/user/conversation isolated.
- No fake agent or sprint-specific runtime class exists.
```

Run:

```bash
dotnet build
dotnet test
```

---

## Definition of Done

This sprint is complete only when:

```text
- Active runtime uses official Microsoft Agent Framework.
- Active runtime is model-only.
- No fake/custom agent framework remains.
- No legacy keyword/domain-agent routing is registered in active runtime.
- No non-model capabilities are advertised.
- No active model-facing contract uses modelId.
- Code is readable and not minified.
- AnswerComposer owns final response output.
- PendingActionStore is clean and non-executing.
- Build and tests pass.
```

Do not merge if the project merely renames files while keeping legacy routing or unused framework code active.
