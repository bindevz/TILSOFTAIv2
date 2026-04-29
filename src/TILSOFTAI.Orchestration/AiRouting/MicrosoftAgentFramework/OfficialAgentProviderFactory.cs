using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Observability;

namespace TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

public interface IOfficialAgentProviderFactory
{
    AIAgent CreateAgent(
        string instructions,
        IReadOnlyList<AIFunction> tools,
        TilsoftExecutionContext executionContext);
}

public sealed class OfficialAgentProviderFactory : IOfficialAgentProviderFactory
{
    public const string AzureOpenAiProvider = "AzureOpenAI";
    public const string OpenAiProvider = "OpenAI";
    public const string OpenAiCompatibleLocalProvider = "OpenAiCompatibleLocal";
    public const string OllamaDevProvider = "OllamaOfficialProviderForDevOnly";

    private readonly IChatClient? _chatClient;
    private readonly AiRoutingOptions _aiRoutingOptions;
    private readonly LlmOptions _llmOptions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _services;
    private readonly ILogger<OfficialAgentProviderFactory> _logger;

    public OfficialAgentProviderFactory(
        IEnumerable<IChatClient> chatClients,
        IOptions<AiRoutingOptions> aiRoutingOptions,
        IOptions<LlmOptions> llmOptions,
        ILoggerFactory loggerFactory,
        IServiceProvider services,
        ILogger<OfficialAgentProviderFactory> logger)
    {
        _chatClient = ResolveChatClient(chatClients);
        _aiRoutingOptions = aiRoutingOptions?.Value ?? throw new ArgumentNullException(nameof(aiRoutingOptions));
        _llmOptions = llmOptions?.Value ?? throw new ArgumentNullException(nameof(llmOptions));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public AIAgent CreateAgent(
        string instructions,
        IReadOnlyList<AIFunction> tools,
        TilsoftExecutionContext executionContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(executionContext);

        var provider = ResolveProviderName(_aiRoutingOptions, _llmOptions);
        var model = ResolveModelName(_aiRoutingOptions, _llmOptions);
        ValidateProvider(provider);

        if (_chatClient is null)
        {
            throw new InvalidOperationException(
                "Official Microsoft Agent Framework provider setup requires a registered Microsoft.Extensions.AI IChatClient.");
        }

        _logger.LogInformation(
            "{EventName} | correlationId: {CorrelationId} | provider: {Provider} | model: {Model} | advertised_tool_count: {ToolCount} | tenantId: {TenantId} | userId: {UserId} | conversationId: {ConversationId} | advertisedFunctionNames: {AdvertisedFunctionNames}",
            Sprint35TraceEvents.AgentCreated,
            executionContext.CorrelationId,
            provider,
            string.IsNullOrWhiteSpace(model) ? "unspecified" : model,
            tools.Count,
            string.IsNullOrWhiteSpace(executionContext.TenantId) ? "unknown" : executionContext.TenantId,
            string.IsNullOrWhiteSpace(executionContext.UserId) ? "unknown" : executionContext.UserId,
            string.IsNullOrWhiteSpace(executionContext.ConversationId) ? "unknown" : executionContext.ConversationId,
            string.Join(",", tools.Select(tool => tool.Name)));

        return _chatClient.AsAIAgent(
            instructions,
            name: "tilsoftai-tool-router",
            description: "Routes candidate ERP capabilities through the official Microsoft Agent Framework.",
            tools: tools.Cast<AITool>().ToArray(),
            loggerFactory: _loggerFactory,
            services: _services);
    }

    public static string ResolveProviderName(AiRoutingOptions aiRoutingOptions, LlmOptions llmOptions) =>
        string.IsNullOrWhiteSpace(aiRoutingOptions.Provider)
            ? llmOptions.Provider.Trim()
            : aiRoutingOptions.Provider.Trim();

    public static string ResolveModelName(AiRoutingOptions aiRoutingOptions, LlmOptions llmOptions) =>
        string.IsNullOrWhiteSpace(aiRoutingOptions.Model)
            ? llmOptions.Model.Trim()
            : aiRoutingOptions.Model.Trim();

    public static bool IsAllowedProvider(string provider) =>
        string.Equals(provider, AzureOpenAiProvider, StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, OpenAiProvider, StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, OpenAiCompatibleLocalProvider, StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, OllamaDevProvider, StringComparison.OrdinalIgnoreCase);

    private static void ValidateProvider(string provider)
    {
        if (!IsAllowedProvider(provider))
        {
            throw new InvalidOperationException(
                "AiRouting:Provider must be AzureOpenAI, OpenAI, OpenAiCompatibleLocal, or OllamaOfficialProviderForDevOnly when official Microsoft Agent Framework routing is enabled.");
        }
    }

    private static IChatClient? ResolveChatClient(IEnumerable<IChatClient>? chatClients)
    {
        if (chatClients is null)
        {
            return null;
        }

        foreach (var chatClient in chatClients)
        {
            return chatClient;
        }

        return null;
    }
}
