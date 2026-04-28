using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TILSOFTAI.Orchestration.AiRouting.Tools;

namespace TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

public sealed record OfficialMicrosoftAgentRunRequest(
    IReadOnlyList<AIFunction> Functions,
    string Instructions,
    string Message,
    TILSOFTAI.Domain.ExecutionContext.TilsoftExecutionContext ExecutionContext,
    AgentRunOptions Options);

public interface IOfficialMicrosoftAgentRuntime
{
    Task<AgentRunResult> RunAsync(
        OfficialMicrosoftAgentRunRequest request,
        CancellationToken cancellationToken);
}

public sealed class OfficialMicrosoftAgentRuntime : IOfficialMicrosoftAgentRuntime
{
    private readonly IOfficialAgentProviderFactory _agentProviderFactory;

    public OfficialMicrosoftAgentRuntime(IOfficialAgentProviderFactory agentProviderFactory)
    {
        _agentProviderFactory = agentProviderFactory ?? throw new ArgumentNullException(nameof(agentProviderFactory));
    }

    public async Task<AgentRunResult> RunAsync(
        OfficialMicrosoftAgentRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var agent = _agentProviderFactory.CreateAgent(
            request.Instructions,
            request.Functions,
            request.ExecutionContext);

        var response = await agent.RunAsync(
                request.Message,
                session: null,
                options: new ChatClientAgentRunOptions(new Microsoft.Extensions.AI.ChatOptions
                {
                    AllowMultipleToolCalls = request.Options.AllowModelSelectedMultiTool
                }),
                cancellationToken)
            .ConfigureAwait(false);

        return OfficialAgentResponseMapper.ToAgentRunResult(
            response.Text,
            FindInvocation(request.Functions));
    }

    private static OfficialAgentFunctionInvocation? FindInvocation(IEnumerable<AIFunction> functions)
    {
        foreach (var function in functions)
        {
            if (function is ICapabilityBackedAIFunction { LastInvocation: { } invocation })
            {
                return invocation;
            }
        }

        return null;
    }
}
