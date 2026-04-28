# Phase 0 — Architecture, Safety Rules, and Feature-Flagged Entry Point

## Goal

Introduce the new Microsoft Agent Framework routing layer without breaking the existing runtime.

This phase should not replace the legacy path yet. It should add a new optional path controlled by feature flags.

## Current runtime to preserve

```text
API / Hub / OpenAI surface
  -> ISupervisorRuntime
  -> IIntentClassifier
  -> CapabilityRequestHint
  -> IAgentRegistry
  -> DomainAgent
  -> CapabilityRegistry
  -> CapabilityResolver
  -> Policy
  -> Validator
  -> ToolAdapter
```

## Target runtime

```text
API / Hub / OpenAI surface
  -> ISupervisorRuntime
  -> IAgentToolRouter.TryRouteAsync(...)
      -> Microsoft Agent Framework tool routing
  -> CapabilityExecutionFacade
  -> IToolAdapter
  -> IAnswerComposer
```

## Keep these existing components

- `ISupervisorRuntime`
- `CapabilityDescriptor`
- `CapabilityArgumentContract`
- `CapabilityArgumentValidator`
- `CapabilityAccessPolicy`
- `IToolAdapterRegistry`
- `SqlToolAdapter`
- `IApprovalEngine`
- `IActionRequestStore`
- `IWriteActionGuard`
- Existing audit/telemetry/readiness infrastructure

## Reduce or retire later

Do not delete these in this phase. They remain fallback components.

- `KeywordIntentClassifier`
- `StructuredCapabilityResolver`
- Manual argument extraction inside domain agents
- Static hardcoded capability descriptions

## Add these abstractions

Create:

```text
src/TILSOFTAI.Orchestration/AiRouting/
```

```csharp
public interface IAgentToolRouter
{
    Task<AgentToolRoutingResult> TryRouteAsync(
        AgentToolRoutingRequest request,
        CancellationToken cancellationToken);
}

public sealed record AgentToolRoutingRequest
{
    public required string Message { get; init; }
    public required TilsoftExecutionContext ExecutionContext { get; init; }
    public required string Locale { get; init; }
    public required AnswerMode RequestedAnswerMode { get; init; }
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } =
        new Dictionary<string, object?>();
}

public sealed record AgentToolRoutingResult
{
    public required bool Handled { get; init; }
    public AssistantAnswer? Answer { get; init; }
    public string? FailureReason { get; init; }
}
```

If `TilsoftExecutionContext`, `AnswerMode`, or `AssistantAnswer` names differ in the current repository, adapt the names while preserving the design.

## Feature flags

Add configuration:

```json
{
  "AiRouting": {
    "MicrosoftAgentFrameworkRoutingEnabled": false,
    "FallbackToLegacyPipeline": true,
    "EnableDynamicToolDescriptions": true,
    "MaxCandidateDomains": 2,
    "MaxCandidateToolsPerDomain": 6,
    "MaxTotalCandidateTools": 12,
    "MaxToolCallsPerTurn": 3,
    "EnableWritePreviewTools": false
  }
}
```

## SupervisorRuntime integration

Add a non-breaking branch before the legacy classifier/resolver path:

```csharp
public async Task<SupervisorResult> RunAsync(
    SupervisorRequest request,
    TilsoftExecutionContext context,
    CancellationToken cancellationToken)
{
    if (_featureFlags.MicrosoftAgentFrameworkRoutingEnabled)
    {
        var routed = await _agentToolRouter.TryRouteAsync(
            new AgentToolRoutingRequest
            {
                Message = request.Input,
                ExecutionContext = context,
                Locale = request.Locale ?? "vi-VN",
                RequestedAnswerMode = ResolveAnswerMode(request)
            },
            cancellationToken);

        if (routed.Handled && routed.Answer is not null)
        {
            return SupervisorResult.FromAssistantAnswer(routed.Answer);
        }

        if (!_featureFlags.FallbackToLegacyPipeline)
        {
            return SupervisorResult.FromFailure(routed.FailureReason ?? "Agent routing failed.");
        }
    }

    return await RunLegacyPipelineAsync(request, context, cancellationToken);
}
```

## Safety rules

The new route must obey these rules from day one:

1. The model must only call provided function tools.
2. Function tools must not call SQL directly.
3. Function tools must call `CapabilityExecutionFacade`.
4. All model-generated arguments must be validated.
5. Write operations must use preview first.
6. Tool count must be capped.
7. The system must be able to fall back to the legacy path.
8. Sensitive fields must not be passed to AI summary without masking.

## Microsoft Agent Framework scope

Use Microsoft Agent Framework for:

```text
natural language -> selected tool -> extracted arguments
```

Do not use it to replace:

```text
policy, validation, approval, SQL execution, audit, answer policy
```

## Phase tasks

- Add routing interfaces.
- Add feature flag options.
- Register an initial no-op/stub `IAgentToolRouter` implementation.
- Wire the router into `SupervisorRuntime` behind the feature flag.
- Add telemetry events for routing attempt, handled, not handled, failure.
- Keep legacy behavior unchanged when the flag is off.

## Acceptance criteria

- The application builds.
- Existing tests pass.
- Feature flag off means old behavior is unchanged.
- Feature flag on can call the stub router and safely fall back.
- No SQL execution behavior changes in this phase.
