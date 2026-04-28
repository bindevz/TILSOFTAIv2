using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Orchestration.AiRouting.Tools;

namespace TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

public sealed class AgentClientFactory : IAgentClientFactory
{
    private readonly AgentRunOptionsFactory _optionsFactory;
    private readonly IOfficialMicrosoftAgentRuntime _runtime;
    private readonly IChatClient? _chatClient;
    private readonly ILogger<AgentClientFactory> _logger;

    public AgentClientFactory(
        AgentRunOptionsFactory optionsFactory,
        IOfficialMicrosoftAgentRuntime runtime,
        IEnumerable<IChatClient> chatClients,
        ILogger<AgentClientFactory> logger)
    {
        _optionsFactory = optionsFactory ?? throw new ArgumentNullException(nameof(optionsFactory));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _chatClient = chatClients?.FirstOrDefault();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IToolCallingAgent CreateToolCallingAgent(
        IReadOnlyList<AgentFunctionTool> tools,
        string instructions)
    {
        var options = _optionsFactory.Create();
        _logger.LogDebug(
            "AgentFrameworkClientCreated | ToolCount: {ToolCount} | MaxToolCallsPerTurn: {MaxToolCallsPerTurn}",
            tools.Count,
            options.MaxToolCallsPerTurn);

        if (_chatClient is null)
        {
            throw new InvalidOperationException(
                "AgentFramework mode requires an official Microsoft.Extensions.AI IChatClient registration.");
        }

        return new OfficialAgentFrameworkToolCallingAgent(
            _runtime,
            _chatClient,
            tools,
            instructions,
            options);
    }

    private sealed class OfficialAgentFrameworkToolCallingAgent : IToolCallingAgent
    {
        private readonly IOfficialMicrosoftAgentRuntime _runtime;
        private readonly IChatClient _chatClient;
        private readonly IReadOnlyList<AgentFunctionTool> _tools;
        private readonly string _instructions;
        private readonly AgentRunOptions _options;

        public OfficialAgentFrameworkToolCallingAgent(
            IOfficialMicrosoftAgentRuntime runtime,
            IChatClient chatClient,
            IReadOnlyList<AgentFunctionTool> tools,
            string instructions,
            AgentRunOptions options)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _instructions = instructions;
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public Task<AgentRunResult> RunAsync(string message, CancellationToken cancellationToken) =>
            _runtime.RunAsync(
                new OfficialMicrosoftAgentRunRequest(
                    _chatClient,
                    _tools,
                    _instructions,
                    message,
                    _options),
                cancellationToken);
    }
}
