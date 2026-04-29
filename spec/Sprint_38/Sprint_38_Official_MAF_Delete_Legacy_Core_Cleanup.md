# Sprint 38 — Delete Legacy Code and Align Core with Official Microsoft Agent Framework

## Goal

Clean the codebase so the active framework is a small, readable, model-only **Official Microsoft Agent Framework Core**.

This sprint is a deletion and cleanup sprint. Do not add new domains. Do not add real write actions. Do not introduce another routing framework. Remove unused legacy code instead of keeping it “just in case”.

## Target Runtime

```text
API
  -> SupervisorRuntime
  -> OfficialAgentToolRouter
  -> OfficialMicrosoftAgentRuntime
  -> official AIAgent
  -> model-only AIFunction tools
  -> CapabilityExecutionFacade
  -> SQL read-only ai_model_* stored procedures
  -> AnswerComposer
  -> API response
```

## Non-Negotiable CTO Rules

1. Official Microsoft Agent Framework is the only active tool-selection brain.
2. The only active business domain is `model`.
3. The active runtime must not register legacy domain agents.
4. The active runtime must not use keyword intent classification.
5. The active runtime must not use `StructuredCapabilityResolver`.
6. The active runtime must not use the old OpenAI-compatible manual tool-call loop.
7. The active runtime must not advertise non-model tools.
8. `CapabilityExecutionFacade` remains the only execution boundary.
9. `AnswerComposer` remains the only final response boundary.
10. Real write execution remains disabled.

---

## Phase 0 — Preflight

Run first:

```bash
dotnet build
dotnet test
```

Then inventory residue:

```bash
rg "KeywordIntentClassifier|IIntentClassifier|IntentClassification"
rg "AccountingAgent|WarehouseAgent|DomainAgentBase|DomainAgentRegistry|GeneralChatAgent|BridgeFallbackReasons"
rg "StructuredCapabilityResolver|ICapabilityResolver|CapabilityRequestHint"
rg "AccountingCapabilities|WarehouseCapabilities"
rg "ToolRegistry|ToolGovernance|ToolDefinition|IToolHandler|NamedToolHandlerRegistry"
rg "OpenAiCompatibleLlmClient|OpenAiResponseParser|ILlmClient"
rg "PlanOptimizer|AnalyticsOrchestrator|PromptBuilder|ContextPack"
rg "ExtractSubjectKeywords|BuildCapabilityHint|MapRequest"
```

Do not continue until you know which references are active and which are only legacy/test residue.

---

## Phase 1 — Reformat Runtime Files

Some production C# files are currently minified into one line. Reformat these first so the cleanup can be reviewed.

Required files:

```text
src/TILSOFTAI.Orchestration/OrchestrationServiceCollectionExtensions.cs
src/TILSOFTAI.Orchestration/Supervisor/SupervisorRuntime.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialAgentToolRouter.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialMicrosoftAgentRuntime.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialAgentProviderFactory.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/ToolRoutingTraceFactory.cs
src/TILSOFTAI.Orchestration/AiRouting/Tools/DynamicFunctionToolFactory.cs
src/TILSOFTAI.Orchestration/Capabilities/ModelCapabilities.cs
src/TILSOFTAI.Orchestration/Actions/IActionRequestStore.cs
src/TILSOFTAI.Api/Extensions/AddTilsoftAiExtensions.cs
```

Acceptance:

```text
- No production runtime file is intentionally minified.
- Code is readable.
- dotnet build still passes.
```

---

## Phase 2 — Delete Legacy Domain-Agent Routing

Delete these files if they are not required by the official model-only runtime:

```text
src/TILSOFTAI.Orchestration/Agents/Domain/AccountingAgent.cs
src/TILSOFTAI.Orchestration/Agents/Domain/WarehouseAgent.cs
src/TILSOFTAI.Orchestration/Agents/Domain/DomainAgentBase.cs
src/TILSOFTAI.Orchestration/Agents/DomainAgentRegistry.cs
src/TILSOFTAI.Orchestration/Agents/GeneralChatAgent.cs
src/TILSOFTAI.Orchestration/Agents/BridgeFallbackReasons.cs
```

Delete the legacy classification folder:

```text
src/TILSOFTAI.Orchestration/Supervisor/Classification/IIntentClassifier.cs
src/TILSOFTAI.Orchestration/Supervisor/Classification/IntentClassification.cs
src/TILSOFTAI.Orchestration/Supervisor/Classification/KeywordIntentClassifier.cs
```

Update or remove tests that still depend on these classes.

Acceptance:

```text
rg "KeywordIntentClassifier|IIntentClassifier|IntentClassification" src tests
rg "AccountingAgent|WarehouseAgent|DomainAgentBase|DomainAgentRegistry|GeneralChatAgent|BridgeFallbackReasons" src tests
```

Both commands should return no active references.

---

## Phase 3 — Rewrite SupervisorRuntime as Official-Agent-Only

`SupervisorRuntime` must become a thin runtime coordinator.

Remove these fields:

```text
_intentClassifier
_agentRegistry
_approvalEngine, if used only by legacy routing
_toolAdapterRegistry, if used only by legacy routing
```

Remove these constructors:

```text
SupervisorRuntime(IIntentClassifier, IAgentRegistry, ...)
private SupervisorRuntime(bool useLegacyRouting, ...)
```

Remove these functions:

```text
MapRequest
BuildCapabilityHint
ExtractSubjectKeywords
```

Remove the entire legacy route block that:

```text
- classifies intent
- builds capability hints
- resolves candidate domain agents
- selects candidates[0]
- executes selected domain agent
```

Keep only:

```text
- pending action confirmation turn handling
- official Agent Framework routing
- fail-closed result when official routing cannot handle the request
- streaming wrapper that delegates to RunAsync
```

Target shape:

```csharp
public sealed class SupervisorRuntime : ISupervisorRuntime
{
    public Task<SupervisorResult> RunAsync(...)
    {
        // validate input
        // handle pending action confirmation
        // call IAgentToolRouter
        // return answer or fail closed
    }
}
```

Acceptance:

```text
- SupervisorRuntime has no keyword classification logic.
- SupervisorRuntime has no domain-agent fallback.
- SupervisorRuntime has no first-candidate selection.
- Official routing failure returns fail-closed response.
```

---

## Phase 4 — Delete Legacy Capability Routing

Delete non-model and legacy capability routing files:

```text
src/TILSOFTAI.Orchestration/Capabilities/AccountingCapabilities.cs
src/TILSOFTAI.Orchestration/Capabilities/WarehouseCapabilities.cs
src/TILSOFTAI.Orchestration/Capabilities/StructuredCapabilityResolver.cs
src/TILSOFTAI.Orchestration/Capabilities/ICapabilityResolver.cs
src/TILSOFTAI.Orchestration/Capabilities/CapabilityRequestHint.cs
```

Delete only if not needed by active model runtime:

```text
src/TILSOFTAI.Orchestration/Capabilities/ConfigurationCapabilitySource.cs
src/TILSOFTAI.Orchestration/Capabilities/ICapabilitySource.cs
src/TILSOFTAI.Orchestration/Capabilities/StaticCapabilitySource.cs
```

Keep:

```text
CapabilityDescriptor.cs
CapabilityArgumentContract.cs
CapabilityArgumentValidator.cs
CapabilityAccessPolicy.cs
ICapabilityRegistry.cs
InMemoryCapabilityRegistry.cs
ModelCapabilities.cs
CompositeCapabilityRegistry.cs only if it is actively used by model runtime or future pending tests
```

Acceptance:

```text
rg "AccountingCapabilities|WarehouseCapabilities|StructuredCapabilityResolver|ICapabilityResolver|CapabilityRequestHint" src tests
```

No active references should remain.

---

## Phase 5 — Delete Old Tool Registry / Manual Tool-Calling Runtime

The official runtime uses Microsoft Agent Framework `AIFunction` tools. Remove the old tool registry stack if it is not used by the active model runtime.

Delete candidates:

```text
src/TILSOFTAI.Orchestration/Tools/ToolDefinition.cs
src/TILSOFTAI.Orchestration/Tools/ToolRegistry.cs
src/TILSOFTAI.Orchestration/Tools/IToolRegistry.cs
src/TILSOFTAI.Orchestration/Tools/ToolGovernance.cs
src/TILSOFTAI.Orchestration/Tools/IToolHandler.cs
src/TILSOFTAI.Orchestration/Tools/INamedToolHandlerRegistry.cs
src/TILSOFTAI.Orchestration/Tools/NamedToolHandlerRegistry.cs
src/TILSOFTAI.Orchestration/Tools/IToolCatalogResolver.cs
src/TILSOFTAI.Orchestration/Tools/IScopedToolCatalogResolver.cs
src/TILSOFTAI.Orchestration/Tools/ToolExecutionRecord.cs
src/TILSOFTAI.Orchestration/Tools/ToolValidationLocalizer.cs
src/TILSOFTAI.Orchestration/Tools/BasicJsonSchemaValidator.cs
src/TILSOFTAI.Orchestration/Tools/RealJsonSchemaValidator.cs
src/TILSOFTAI.Orchestration/Tools/IJsonSchemaValidator.cs
```

Also delete old manual LLM tool-call classes if not used by official Agent Framework health/config:

```text
src/TILSOFTAI.Orchestration/Llm/ILlmClient.cs
src/TILSOFTAI.Orchestration/Llm/LlmMessage.cs
src/TILSOFTAI.Orchestration/Llm/LlmRequest.cs
src/TILSOFTAI.Infrastructure/Llm/OpenAiCompatibleLlmClient.cs
src/TILSOFTAI.Infrastructure/Llm/OpenAiResponseParser.cs
```

Keep embedding clients only if used by semantic retrieval or future SQL Server 2025 KB work.

Acceptance:

```text
rg "ToolRegistry|ToolGovernance|ToolDefinition|IToolHandler|NamedToolHandlerRegistry|OpenAiCompatibleLlmClient|OpenAiResponseParser|ILlmClient" src tests
```

No active references should remain.

---

## Phase 6 — Simplify DI to Active Core Only

Refactor:

```text
src/TILSOFTAI.Orchestration/OrchestrationServiceCollectionExtensions.cs
src/TILSOFTAI.Api/Extensions/AddTilsoftAiExtensions.cs
```

The orchestration extension should register only:

```text
ModelCapabilities
InMemoryCapabilityRegistry
OfficialAgentToolRouter
OfficialMicrosoftAgentRuntime
OfficialAgentProviderFactory
DynamicFunctionToolFactory
CapabilityExecutionFacade
AnswerComposer
PendingActionConfirmationResolver
ActionRequestStore
RuntimeExecutionInstrumentation / tracing if used
```

Remove registrations for:

```text
legacy agents
intent classifiers
old tool registry
old prompt pipeline
old analytics planner
old manual LLM client
non-model capabilities
```

Acceptance:

```text
- DI registration is short and readable.
- No old domain-agent or keyword classifier is registered.
- No non-model capability source is registered.
- dotnet build passes.
```

---

## Phase 7 — Align Function Tools with Microsoft Agent Framework

Review:

```text
src/TILSOFTAI.Orchestration/AiRouting/Tools/DynamicFunctionToolFactory.cs
src/TILSOFTAI.Orchestration/AiRouting/Tools/CapabilityToolDescriptorFactory.cs
src/TILSOFTAI.Orchestration/AiRouting/Tools/CapabilityParameterSchemaBuilder.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialAgentProviderFactory.cs
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/OfficialMicrosoftAgentRuntime.cs
```

Rules:

```text
- Use official AIAgent.
- Use official AIFunction tools.
- Prefer AIFunctionFactory.Create(...) where practical.
- If DescriptorBackedAIFunction remains, it must be a thin official AIFunction adapter.
- Tool callbacks must call CapabilityExecutionFacade.
- Do not parse OpenAI tool calls manually.
- Do not choose first candidate manually.
```

Acceptance:

```text
- Tests prove candidate order does not determine selected tool.
- Tests prove AIFunction callback reaches CapabilityExecutionFacade.
- Tests prove non-model tools are not advertised.
```

---

## Phase 8 — Remove Unused Planning, Prompting, Analytics Modules

Delete these modules if they are not used by the official model-only runtime:

```text
src/TILSOFTAI.Orchestration/Planning/PlanOptimizer.cs
src/TILSOFTAI.Orchestration/Planning/PlanValidationResult.cs
src/TILSOFTAI.Orchestration/Analytics/AnalyticsCache.cs
src/TILSOFTAI.Orchestration/Analytics/AnalyticsIntentDetector.cs
src/TILSOFTAI.Orchestration/Analytics/AnalyticsOrchestrator.cs
src/TILSOFTAI.Orchestration/Analytics/AnalyticsPersistence.cs
src/TILSOFTAI.Orchestration/Analytics/IInsightAssemblyService.cs
src/TILSOFTAI.Orchestration/Analytics/InsightAssemblyService.cs
src/TILSOFTAI.Orchestration/Analytics/InsightRenderer.cs
src/TILSOFTAI.Orchestration/Prompting/CompositeContextPackProvider.cs
src/TILSOFTAI.Orchestration/Prompting/ContextPackBudgeter.cs
src/TILSOFTAI.Orchestration/Prompting/ContextPackKeys.cs
src/TILSOFTAI.Orchestration/Prompting/IContextPackProvider.cs
src/TILSOFTAI.Orchestration/Prompting/IScopedContextPackProvider.cs
src/TILSOFTAI.Orchestration/Prompting/NullContextPackProvider.cs
src/TILSOFTAI.Orchestration/Prompting/PromptBudget.cs
src/TILSOFTAI.Orchestration/Prompting/PromptBuildContext.cs
src/TILSOFTAI.Orchestration/Prompting/PromptBuilder.cs
src/TILSOFTAI.Orchestration/Prompting/TokenBudgetPolicy.cs
```

If any file is still required for API startup, remove its registration first and verify whether it is truly needed.

Acceptance:

```text
rg "AnalyticsOrchestrator|PlanOptimizer|PromptBuilder|ContextPack" src tests
```

No active references should remain unless explicitly documented as needed.

---

## Phase 9 — Keep AnswerComposer and PendingActionState

Do not delete these. They are part of the core.

Keep:

```text
src/TILSOFTAI.Orchestration/Answering/*
src/TILSOFTAI.Orchestration/Actions/ActionRequestRecord.cs
src/TILSOFTAI.Orchestration/Actions/ActionRequestCreateRequest.cs
src/TILSOFTAI.Orchestration/Actions/IActionRequestStore.cs
src/TILSOFTAI.Orchestration/Actions/IPendingActionConfirmationResolver.cs
src/TILSOFTAI.Orchestration/Actions/PendingActionConfirmationResolver.cs
```

Rules:

```text
- RawJson does not call LLM after tool execution.
- Structured output is composed by AnswerComposer.
- Pending actions are infrastructure only.
- No active write tool is advertised.
- Confirmation state is tenant/user/conversation isolated.
```

Acceptance:

```text
- RawJson tests pass.
- Structured tests pass.
- Pending action isolation tests pass.
```

---

## Phase 10 — Architecture Guard Tests

Add or update tests that fail if deleted legacy pieces return.

Required guard tests:

```text
Architecture_NoLegacyDomainAgents
Architecture_NoKeywordIntentClassifier
Architecture_NoStructuredCapabilityResolver
Architecture_NoOldToolRegistry
Architecture_NoManualOpenAiToolLoop
Architecture_ModelOnlyCapabilities
Architecture_OfficialAgentFrameworkOnly
```

Suggested assertions:

```text
- No production type name contains AccountingAgent or WarehouseAgent.
- No production type name contains KeywordIntentClassifier.
- No production type name contains StructuredCapabilityResolver.
- No production type name contains ToolRegistry or ToolGovernance.
- No active DI registration resolves IIntentClassifier or IAgentRegistry.
- Active capability registry contains only model.* capabilities.
```

---

## Phase 11 — Final Validation

Run:

```bash
dotnet build
dotnet test
```

Then run:

```bash
rg "KeywordIntentClassifier|IIntentClassifier|IntentClassification" src tests
rg "AccountingAgent|WarehouseAgent|DomainAgentBase|DomainAgentRegistry|GeneralChatAgent" src tests
rg "StructuredCapabilityResolver|ICapabilityResolver|CapabilityRequestHint" src tests
rg "AccountingCapabilities|WarehouseCapabilities" src tests
rg "ToolRegistry|ToolGovernance|ToolDefinition|IToolHandler|NamedToolHandlerRegistry" src tests
rg "OpenAiCompatibleLlmClient|OpenAiResponseParser|ILlmClient" src tests
rg "PlanOptimizer|AnalyticsOrchestrator|PromptBuilder|ContextPack" src tests
```

Only documented exceptions are allowed. If an exception remains, explain it in the PR.

---

## Definition of Done

This sprint is complete only when:

```text
- Active runtime uses official Microsoft Agent Framework.
- Active runtime is model-only.
- Legacy domain agents are deleted.
- Keyword intent classifier is deleted.
- Structured capability resolver is deleted.
- Old tool registry/governance stack is deleted or fully isolated outside active runtime.
- Manual OpenAI-compatible tool-call loop is deleted or fully isolated outside active runtime.
- DI is short, readable, and core-only.
- SupervisorRuntime is official-agent-only.
- AnswerComposer remains the response boundary.
- PendingActionState remains infrastructure-only.
- Build and tests pass.
```

Do not merge if old code remains merely unregistered but still presented as part of the core framework.
