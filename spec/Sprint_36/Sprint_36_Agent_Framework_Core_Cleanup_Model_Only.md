# Sprint 36 — Agent Framework Core Cleanup and Model-Only Runtime Hardening

## Purpose

Clean up the Sprint 35 implementation so the project becomes a stable **Agent Framework Core** with one reference read-only domain: `model`.

This sprint is a corrective cleanup sprint. Do not add new business domains. Do not add real write execution. Do not introduce another framework. The goal is to remove invented/sprint-specific artifacts, reduce runtime noise, and make the current official Microsoft Agent Framework path easier to run, test, and extend later.

## CTO Decision

The active runtime must remain:

```text
API
  -> SupervisorRuntime
  -> Official Microsoft Agent Framework router
  -> local AI / configured provider
  -> model-only candidate functions
  -> CapabilityExecutionFacade
  -> SQL read-only ai_model_* stored procedures
  -> AnswerComposer
  -> API response
```

The framework core must be generic and stable. Names such as `Sprint35TraceEvents`, sprint-specific comments, temporary wrappers, fake agent patterns, and legacy fallback noise must be removed or isolated.

---

## Non-Negotiable Rules

1. Use **official Microsoft Agent Framework** as the tool-selection and argument-binding brain.
2. Runtime domain scope is **model only**.
3. Do not advertise non-model tools.
4. Do not use regex/keyword logic to choose final tools or bind business arguments.
5. Regex or phrase matching is allowed only for deterministic helpers such as confirmation intent or hard-signal hints.
6. `CapabilityExecutionFacade` remains the only boundary that can execute capabilities.
7. `AnswerComposer` remains the only final response boundary.
8. Raw JSON mode must not call the LLM after tool execution.
9. Structured mode must return stable text/blocks/provenance.
10. Real write execution remains disabled unless an approved action flow explicitly allows it.

---

## Phase 0 — Preflight

Before editing, run and record:

```bash
dotnet build
dotnet test
```

If the repo does not build, fix build errors caused by Sprint 35 first. Do not start feature work before the project builds.

Create a short cleanup note in the PR description:

```text
- Removed sprint-specific runtime names
- Removed or isolated legacy fallback code from active runtime
- Kept model-only Agent Framework path
- Kept CapabilityExecutionFacade and AnswerComposer boundaries
```

---

## Phase 1 — Remove Sprint-Specific Runtime Artifacts

### Goal

Remove names that make temporary sprint code look like permanent architecture.

### Required Changes

Replace:

```text
TILSOFTAI.Orchestration/Observability/Sprint35TraceEvents.cs
Sprint35TraceEvents
```

With a stable name:

```text
TILSOFTAI.Orchestration/Observability/AgentRoutingTraceEvents.cs
AgentRoutingTraceEvents
```

Use generic event names only:

```csharp
public static class AgentRoutingTraceEvents
{
    public const string RouteStarted = "agent_route_started";
    public const string CandidateSelectionCompleted = "candidate_selection_completed";
    public const string ToolsAdvertised = "tools_advertised";
    public const string AgentRunStarted = "agent_run_started";
    public const string AgentToolInvoked = "agent_tool_invoked";
    public const string AnswerComposerStarted = "answer_composer_started";
    public const string AnswerComposerCompleted = "answer_composer_completed";
    public const string RouteFailedClosed = "agent_route_failed_closed";
    public const string PendingActionCreated = "pending_action_created";
    public const string PendingActionConfirmed = "pending_action_confirmed";
    public const string PendingActionExpired = "pending_action_expired";
}
```

Update all references.

### Remove

Remove sprint-specific comments from production code, especially comments like:

```text
Sprint 35
Sprint 34
PATCH 31
PATCH 36
```

Keep historical sprint details only in `spec/` documents, not in runtime code.

### Acceptance Criteria

- No production runtime class named `Sprint35*`.
- No production observability class uses sprint numbers.
- `dotnet test` passes.

---

## Phase 2 — Clean Up Active Runtime Registration

### Goal

Make dependency injection reflect the active model-only Agent Framework runtime.

### Required Changes

Review:

```text
src/TILSOFTAI.Orchestration/OrchestrationServiceCollectionExtensions.cs
```

This file currently mixes active Agent Framework runtime, legacy supervisor agents, old intent classification, old domain-agent comments, and fallback infrastructure.

Refactor it into clear registration sections:

```csharp
services.AddModelOnlyCapabilities();
services.AddOfficialAgentFrameworkRouting();
services.AddAnswerComposition();
services.AddPendingActionState();
services.AddLegacySupervisorFallbackIfEnabled();
```

If adding extension methods is too much for one PR, at minimum add clear private methods inside the same file:

```csharp
private static IServiceCollection AddModelOnlyCapabilitySource(...)
private static IServiceCollection AddOfficialAgentRouting(...)
private static IServiceCollection AddAnswerComposer(...)
private static IServiceCollection AddPendingActions(...)
private static IServiceCollection AddLegacyFallbackOnlyServices(...)
```

### Active Runtime Requirements

The active runtime must register:

```text
StaticCapabilitySource("static-model", ModelCapabilities.All)
OfficialAgentToolRouter
OfficialMicrosoftAgentRuntime
OfficialAgentProviderFactory
DynamicFunctionToolFactory or official function provider
CapabilityExecutionFacade
AnswerComposer
PendingActionConfirmationResolver
```

### Legacy Fallback Requirements

Legacy services may remain only if needed for compatibility, but they must be grouped under a clearly named registration section:

```text
AddLegacyFallbackOnlyServices
```

Do not let legacy services appear as the active model runtime.

### Acceptance Criteria

- DI file is readable.
- Active model-only runtime is obvious.
- Legacy fallback registrations are isolated.
- No accidental non-model capability source is registered.

---

## Phase 3 — Enforce Model-Only Runtime

### Goal

Ensure only the `model` domain can be used while building the framework core.

### Required Checks

Review and harden:

```text
DomainGate
SemanticCapabilityCandidateSelector
SemanticCapabilityRetriever
DynamicFunctionToolFactory
platform-catalog.json
ModelCapabilities.cs
```

### Rules

- `AllowedDomains` must normalize to exactly `model`.
- Candidate selector must not return non-model capabilities in active runtime.
- Function provider must not build non-model tools.
- Platform catalog must contain only model runtime capabilities.
- Tests must fail if warehouse/sales/purchasing/accounting tools are advertised.

### Acceptance Criteria

- A test with mixed model + non-model candidates advertises only model tools.
- Runtime cannot advertise more than 6 model tools.
- `model.count`, `model.overview.by-code`, `model.pieces.by-code`, `model.materials.by-code`, `model.compare`, `model.packaging.by-code` remain the only active model capabilities.

---

## Phase 4 — Clean Model Tool Contracts

### Goal

Keep model-facing tool arguments natural-language friendly.

### Required Contract

Single-model tools must use:

```text
modelCode: string
```

Compare tool must use:

```text
modelCodes: string[]
```

Do not expose internal database IDs to the model.

### Required Files

Review:

```text
catalog/platform-catalog.json
src/TILSOFTAI.Orchestration/Capabilities/ModelCapabilities.cs
sql/02_capabilities/model/003_sps_model.sql
sql/99_seed/003_seed_toolcatalog_model.sql
tests/TILSOFTAI.Tests/Capabilities/PlatformCatalogTests.cs
```

### Rules

- No user-facing model tool should require `modelId`.
- No function schema should expose `model_id` or `modelId`.
- SQL wrappers may resolve `modelCode` internally.
- The AI must never be asked to invent internal IDs.

### Acceptance Criteria

- Search for `modelId` in model-facing capability/tool definitions returns no active runtime usage.
- Smoke prompts with `ABC` pass through as `modelCode = "ABC"`.
- Missing model code returns follow-up, not SQL execution.

---

## Phase 5 — Clean Official Agent Function Provider

### Goal

Keep official tool usage real and testable.

### Required Review

Review:

```text
DynamicFunctionToolFactory.cs
OfficialAgentProviderFactory.cs
OfficialMicrosoftAgentRuntime.cs
OfficialAgentToolRouter.cs
```

### Required Rules

- No fake agent runtime.
- No first-candidate selection.
- No regex argument binder.
- No manual OpenAI-compatible tool-call loop as the active brain.
- Tool callbacks must call `CapabilityExecutionFacade`.
- Function descriptions and parameter schemas must come from capability metadata.

### Implementation Guidance

If `DescriptorBackedAIFunction : AIFunction` remains, it must be treated as an official `AIFunction` adapter, not as a fake framework. Keep it only if tests prove:

```text
- Agent receives its Name, Description, and JsonSchema.
- InvokeCoreAsync calls CapabilityExecutionFacade.
- LastInvocation is captured only for trace mapping.
```

If the adapter becomes unreliable, replace it with an `AIFunctionFactory.Create(...)` based provider.

### Acceptance Criteria

- Anti-regression tests prove wrong candidate order does not force wrong selection.
- Agent runtime sees candidate functions.
- Function invocation goes through the facade.
- No custom fake `CandidateGatedToolCallingAgent` or equivalent exists.

---

## Phase 6 — Standardize AnswerComposer

### Goal

Make final responses deterministic and framework-owned.

### Required Modes

```text
RawJson
Structured
```

### RawJson Rules

- Do not call LLM after tool execution.
- Return capability, procedure, arguments, row count, rows, result schema, execution metadata, sensitivity policy result, and provenance.
- Suitable for frontend/API consumers.

### Structured Rules

- Use `AnswerComposerRequest`.
- Return stable `AssistantAnswer`.
- Include text, blocks, provenance, and optional follow-up questions.
- Mask sensitive data before table/summary output.
- Return `follow_up` when required arguments are missing.
- Return `no_data` when row count is zero.
- Return `structured` for normal model data.

### Cleanup

Remove confusing or duplicate summary paths. Agent Framework may select tools and bind args, but it must not be the final response formatter.

### Acceptance Criteria

- RawJson mode test verifies no final LLM/agent summary.
- Structured mode test verifies blocks + provenance.
- No-data and missing-argument behavior are covered by tests.
- Vietnamese/English output should not use broken or placeholder text.

---

## Phase 7 — Clean Pending Action State

### Goal

Keep write-preview conversation state as a framework foundation, but do not enable real write actions.

### Required Review

```text
ActionRequestRecord.cs
IActionRequestStore.cs
SqlActionRequestStore.cs
IPendingActionConfirmationResolver.cs
PendingActionConfirmationResolver.cs
sql/03_actions
```

### Cleanup Rules

- Remove duplicate or ambiguous methods from `IActionRequestStore`.
- Keep one clear status transition set.
- Keep one clear `MarkExecutedAsync` signature unless there is a strong reason for overloads.
- Confirmation should use tenant + user + conversation isolation.
- Expired actions must not be confirmed.
- Confirmation should not execute writes by itself.
- Confirmation should only move state to confirmed/ready-for-approval.

### Suggested Interface

```csharp
public interface IActionRequestStore
{
    Task<ActionRequestRecord> CreateAsync(ActionRequestCreateRequest request, CancellationToken ct);
    Task<ActionRequestRecord?> GetAsync(string tenantId, string actionId, CancellationToken ct);
    Task<ActionRequestRecord?> GetActiveForConversationAsync(string tenantId, string userId, string conversationId, CancellationToken ct);
    Task<ActionRequestRecord> ConfirmAsync(string tenantId, string userId, string actionId, CancellationToken ct);
    Task<ActionRequestRecord> RejectAsync(string tenantId, string userId, string actionId, string? reason, CancellationToken ct);
    Task<ActionRequestRecord> MarkExecutedAsync(string tenantId, string actionId, string executedByUserId, string? resultCompactJson, bool success, CancellationToken ct);
    Task<int> ExpireOldAsync(DateTimeOffset nowUtc, CancellationToken ct);
}
```

### Acceptance Criteria

- No duplicate store APIs.
- `ConfirmAsync` cannot confirm another user's action.
- Expired pending action returns a follow-up/no-pending answer.
- No real write execution is enabled by this sprint.

---

## Phase 8 — Observability and Metrics Cleanup

### Goal

Keep observability generic and stable.

### Required Changes

- Rename sprint-specific metrics/comments to generic agent-routing names.
- Keep metric constants under `MetricNames`.
- Do not add evaluation metrics that are not emitted or tested.
- Remove unused metric names if they are dead code.
- Ensure logs include:
  - correlationId
  - tenantId
  - userId
  - conversationId
  - provider
  - model
  - candidate capability keys
  - advertised tool names
  - selected function
  - capability key
  - row count
  - answer mode
  - fallback used = false

### Acceptance Criteria

- No `Sprint35` observability names.
- Metrics are generic.
- Logs are useful for one API request through the model runtime.

---

## Phase 9 — API and Smoke Test Cleanup

### Goal

Make the model-only runtime testable through API.

### Required Tests

Keep or add tests for:

```text
- Provider config creates official agent path.
- Health check fails when LocalAi model is placeholder.
- Mixed candidate list advertises only model tools.
- Wrong candidate order still allows correct tool selection.
- RawJson mode returns raw envelope.
- Structured mode returns blocks/provenance.
- Missing modelCode returns follow-up.
- Pending action confirmation is isolated by tenant/user/conversation.
```

### Runbook

Keep the runbook short:

```text
spec/Sprint_35/RUNBOOK_Model_Local_AI_Smoke_Test.md
```

It should include only:

```text
1. local settings
2. environment variable
3. start API
4. health check
5. 5 smoke prompts
6. expected logs
```

Remove speculative or long architecture explanations from the runbook.

---

## Delete / Rename Checklist

Delete or rename:

```text
Sprint35TraceEvents.cs -> AgentRoutingTraceEvents.cs
Any production class starting with Sprint35*
Sprint-specific comments in runtime files
Dead non-model active capability sources
Fake/custom agent remnants
Duplicate IActionRequestStore methods
Unused metrics that are never emitted
Long speculative docs inside runtime folders
```

Do not delete:

```text
Official Microsoft Agent Framework runtime
ModelCapabilities
CapabilityExecutionFacade
AnswerComposer
Pending action state
Model smoke runbook
Anti-regression tests
```

---

## Final Definition of Done

This cleanup sprint is complete only when:

```text
dotnet build passes
dotnet test passes
No production runtime class is named Sprint35*
No fake agent framework code remains
Only model tools are active
Official Agent Framework route is the active brain
No legacy fallback is used in model runtime configuration
modelCode/modelCodes are the only model-facing identifiers
AnswerComposer owns final output
Pending action state is clean and non-executing
API smoke runbook is short and executable
```

Do not merge if the cleanup only renames files but leaves legacy/fake/sprint-specific behavior active.
