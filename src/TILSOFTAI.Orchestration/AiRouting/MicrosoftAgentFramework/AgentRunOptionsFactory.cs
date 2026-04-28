using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

public sealed record AgentRunOptions
{
    public int MaxToolCallsPerTurn { get; init; }
    public bool AllowParallelReadTools { get; init; }
    public bool AllowParallelWriteTools { get; init; }
    public bool AllowModelSelectedMultiTool { get; init; }
}

public sealed class AgentRunOptionsFactory
{
    private readonly AiRoutingOptions _options;

    public AgentRunOptionsFactory(IOptions<AiRoutingOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public AgentRunOptions Create() => new()
    {
        MaxToolCallsPerTurn = _options.MaxToolCallsPerTurn,
        AllowParallelReadTools = _options.AllowParallelReadTools,
        AllowParallelWriteTools = _options.AllowParallelWriteTools,
        AllowModelSelectedMultiTool = _options.AllowModelSelectedMultiTool
    };
}
