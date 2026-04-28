# Phase 2 — Microsoft Agent Framework Tool Router

## Goal

Implement an `IAgentToolRouter` using Microsoft Agent Framework as the intelligent layer for selecting ERP function tools and extracting parameters from natural language.

The router must expose only a small candidate tool set per request.

## Runtime flow

```text
User message
  -> hard signal extraction
  -> SQL Server 2025 semantic KB retrieval
  -> candidate domains
  -> candidate capabilities
  -> dynamic function tools
  -> Microsoft Agent Framework agent
  -> tool call result
  -> AnswerComposer
```

## Package direction

Add the Microsoft Agent Framework package to the orchestration project:

```bash
dotnet add src/TILSOFTAI.Orchestration package Microsoft.Agents.AI
```

Add the provider package required by the deployment environment, but hide provider-specific code behind an internal factory.

## Add router implementation

Create:

```text
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/
  MicrosoftAgentToolRouter.cs
  AgentInstructionsBuilder.cs
  AgentRunOptionsFactory.cs
  AgentClientFactory.cs
  AgentToolCallResultMapper.cs
```

Skeleton:

```csharp
public sealed class MicrosoftAgentToolRouter : IAgentToolRouter
{
    private readonly IHardSignalExtractor _hardSignalExtractor;
    private readonly ISemanticCapabilityRetriever _retriever;
    private readonly IAgentFunctionToolFactory _toolFactory;
    private readonly IAgentClientFactory _agentClientFactory;
    private readonly IAnswerComposer _answerComposer;
    private readonly IToolRoutingTraceStore _traceStore;

    public async Task<AgentToolRoutingResult> TryRouteAsync(
        AgentToolRoutingRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var hardSignals = _hardSignalExtractor.Extract(
                request.Message,
                request.Locale,
                request.ExecutionContext);

            var retrieval = await _retriever.RetrieveAsync(
                request.Message,
                hardSignals,
                request.ExecutionContext,
                request.Locale,
                CapabilityRetrievalOptions.Default,
                cancellationToken);

            if (retrieval.Capabilities.Count == 0)
            {
                return new AgentToolRoutingResult
                {
                    Handled = false,
                    FailureReason = "No candidate capabilities found."
                };
            }

            var tools = await _toolFactory.BuildToolsAsync(
                retrieval.Capabilities,
                request.ExecutionContext,
                request.Locale,
                cancellationToken);

            if (tools.Count == 0)
            {
                return new AgentToolRoutingResult
                {
                    Handled = false,
                    FailureReason = "No tools created from candidate capabilities."
                };
            }

            var agent = _agentClientFactory.CreateToolCallingAgent(
                tools,
                AgentInstructionsBuilder.Build(request, hardSignals, retrieval));

            var agentResult = await agent.RunAsync(
                request.Message,
                cancellationToken);

            var answerRequest = AgentToolCallResultMapper.ToAnswerComposerRequest(
                agentResult,
                request);

            var answer = await _answerComposer.ComposeAsync(
                answerRequest,
                cancellationToken);

            await _traceStore.SaveAsync(
                ToolRoutingTrace.FromSuccess(request, retrieval, agentResult, answer, startedAt),
                cancellationToken);

            return new AgentToolRoutingResult
            {
                Handled = true,
                Answer = answer
            };
        }
        catch (Exception ex)
        {
            await _traceStore.SaveAsync(
                ToolRoutingTrace.FromFailure(request, ex, startedAt),
                cancellationToken);

            return new AgentToolRoutingResult
            {
                Handled = false,
                FailureReason = ex.Message
            };
        }
    }
}
```

Adapt method names to the exact Microsoft Agent Framework APIs used in the project.

## Hard signal extraction

Create:

```text
src/TILSOFTAI.Orchestration/Semantic/
```

```csharp
public interface IHardSignalExtractor
{
    HardSignalSet Extract(
        string message,
        string locale,
        TilsoftExecutionContext context);
}

public sealed record HardSignalSet
{
    public IReadOnlyList<CodeSignal> Codes { get; init; } = [];
    public IReadOnlyList<DateSignal> Dates { get; init; } = [];
    public IReadOnlyList<NumberSignal> Numbers { get; init; } = [];
    public IReadOnlyList<string> BusinessKeywords { get; init; } = [];
    public string? DetectedLanguage { get; init; }
}
```

Use hard signals for retrieval improvement only. Do not treat them as final authority.

Examples:

```text
"PO-123" -> possible purchase order number
"SO-9001" -> possible sales order number
"MADEIRA-BLK" -> possible item/product code
"BD" -> possible warehouse code
"last month" -> relative date range
"tháng trước" -> relative date range
"500 pcs" -> quantity signal
```

## Semantic capability retriever

Create:

```csharp
public interface ISemanticCapabilityRetriever
{
    Task<CapabilityRetrievalResult> RetrieveAsync(
        string userMessage,
        HardSignalSet hardSignals,
        TilsoftExecutionContext context,
        string locale,
        CapabilityRetrievalOptions options,
        CancellationToken cancellationToken);
}

public sealed record CapabilityRetrievalResult
{
    public required IReadOnlyList<DomainCandidate> Domains { get; init; }
    public required IReadOnlyList<CapabilityCandidate> Capabilities { get; init; }
    public required IReadOnlyList<EntityCandidate> EntityCandidates { get; init; }
    public required IReadOnlyList<KnowledgeChunk> ContextChunks { get; init; }
}
```

Routing levels:

```text
Level 0: hard signal extraction
Level 1: retrieve top 1-3 domains
Level 2: retrieve top 5-12 capabilities across selected domains
Level 3: build function tools only for those capabilities
Level 4: let the agent choose a tool and arguments
```

## Tool budget policy

Default policy:

```json
{
  "maxDomainsPerRequest": 2,
  "maxToolsPerDomain": 6,
  "maxTotalTools": 12,
  "maxToolCallsPerTurn": 3,
  "allowParallelReadTools": true,
  "allowParallelWriteTools": false,
  "allowModelSelectedMultiTool": false,
  "preferCompositeCapability": true
}
```

Implementation requirement:

- Never expose the full ERP tool catalog.
- Never expose write execution tools.
- Cap candidate tools before sending them to the model.
- Prefer one primary tool per request in the first release.

## Agent instructions

Build instructions dynamically but keep the safety rules stable:

```text
You are an internal ERP assistant.

Rules:
- Use only the provided ERP function tools.
- Never generate SQL.
- Never invent item codes, customer codes, supplier codes, warehouse IDs, invoice numbers, PO numbers, or SO numbers.
- If required parameters are missing, ask a concise clarification question.
- If multiple entity candidates are possible, ask the user to choose.
- For create/update/delete/post/approve/cancel actions, only use preview tools unless an approved action is explicitly provided by the system.
- Do not mention stored procedure names unless debug mode is enabled.
- Reply in the user's locale unless system configuration says otherwise.
```

Append request-local context:

```text
Locale: {locale}
Tenant: {tenant_id}
User roles: {role summary, no secrets}
Candidate domains: {top domains}
Candidate tool count: {n}
Current business date/time: {business timezone}
```

## Acceptance criteria

- User can ask a Vietnamese or English ERP question.
- Router retrieves a small set of candidate tools.
- Agent receives only candidate tools, not the full catalog.
- Agent can select the correct priority capability.
- Agent can extract basic parameters for priority capabilities.
- Router falls back safely if retrieval or agent execution fails.
- All tool calls go through the tool factory and then `CapabilityExecutionFacade`.
