using System.Text;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Prompting;

public sealed class PromptBuilder
{
    private readonly ChatOptions _chatOptions;
    private readonly IContextPackProvider _contextPackProvider;
    private readonly TokenBudgetPolicy _tokenBudgetPolicy;
    private readonly ContextPackBudgeter _contextPackBudgeter;

    public PromptBuilder(
        IOptions<ChatOptions> chatOptions,
        IContextPackProvider contextPackProvider,
        TokenBudgetPolicy tokenBudgetPolicy,
        ContextPackBudgeter contextPackBudgeter)
    {
        _chatOptions = chatOptions?.Value ?? throw new ArgumentNullException(nameof(chatOptions));
        _contextPackProvider = contextPackProvider ?? throw new ArgumentNullException(nameof(contextPackProvider));
        _tokenBudgetPolicy = tokenBudgetPolicy ?? throw new ArgumentNullException(nameof(tokenBudgetPolicy));
        _contextPackBudgeter = contextPackBudgeter ?? throw new ArgumentNullException(nameof(contextPackBudgeter));
    }

    public async Task<LlmRequest> BuildAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        var basePrompt = BuildBaseSystemPrompt(context);
        var contextPacks = await _contextPackProvider.GetContextPacksAsync(context, cancellationToken);
        var budgetedPacks = _contextPackBudgeter.Budget(contextPacks);
        var contextSection = BuildContextSection(budgetedPacks);
        var systemPrompt = basePrompt + contextSection;
        var retainAssistants = _chatOptions.CompactionLimits.TryGetValue("RetainAssistantMessages", out var retain)
            ? retain
            : 2;
        var trimmedMessages = _tokenBudgetPolicy.Apply(
            basePrompt,
            messages,
            _chatOptions.MaxTokens,
            retainAssistants,
            contextSection.Length);

        return new LlmRequest
        {
            SystemPrompt = systemPrompt,
            Messages = trimmedMessages.ToList(),
            Tools = tools,
            MaxTokens = _chatOptions.MaxTokens
        };
    }

    private static string BuildBaseSystemPrompt(TilsoftExecutionContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are TILSOFTAI, an assistant that follows tool instructions and does not guess domain rules.");
        builder.Append("Respond in language: ").Append(context.Language).AppendLine(".");
        builder.AppendLine("If tool calls are needed, call tools that match the user's request and follow tool instructions.");
        return builder.ToString();
    }

    private static string BuildContextSection(IReadOnlyList<KeyValuePair<string, string>> contextPacks)
    {
        if (contextPacks.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("Context Packs:");
        foreach (var pack in contextPacks)
        {
            builder.Append("## ").Append(pack.Key).AppendLine();
            builder.AppendLine(pack.Value);
        }

        return builder.ToString();
    }
}
