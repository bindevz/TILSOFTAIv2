using System.Collections.Concurrent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Orchestration.AiRouting.Tools;

namespace TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

public sealed record OfficialMicrosoftAgentRunRequest(
    IChatClient ChatClient,
    IReadOnlyList<AgentFunctionTool> Tools,
    string Instructions,
    string Message,
    AgentRunOptions Options);

public interface IOfficialMicrosoftAgentRuntime
{
    Task<AgentRunResult> RunAsync(
        OfficialMicrosoftAgentRunRequest request,
        CancellationToken cancellationToken);
}

public sealed class OfficialMicrosoftAgentRuntime : IOfficialMicrosoftAgentRuntime
{
    private readonly IOfficialAgentToolFactory _toolFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _services;

    public OfficialMicrosoftAgentRuntime(
        IOfficialAgentToolFactory toolFactory,
        ILoggerFactory loggerFactory,
        IServiceProvider services)
    {
        _toolFactory = toolFactory ?? throw new ArgumentNullException(nameof(toolFactory));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public async Task<AgentRunResult> RunAsync(
        OfficialMicrosoftAgentRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var invocations = new ConcurrentQueue<OfficialAgentToolInvocation>();
        var officialTools = _toolFactory.CreateTools(
            request.Tools,
            invocations.Enqueue);

        var agent = new ChatClientAgent(
            request.ChatClient,
            request.Instructions,
            name: "tilsoftai-tool-router",
            description: "Routes candidate ERP capabilities through the official Microsoft Agent Framework.",
            tools: officialTools.Select(tool => tool.Tool).ToArray(),
            loggerFactory: _loggerFactory,
            services: _services);

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
            invocations.TryPeek(out var selected) ? selected : null);
    }
}
