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

    /// <summary>
    /// PATCH 36.02: BuildAsync now accepts optional PromptBuildContext for scoped packs.
    /// </summary>
    public async Task<LlmRequest> BuildAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken,
        PromptBuildContext? buildContext = null)
    {
        var basePrompt = BuildBaseSystemPrompt(context);

        // Use scoped interface if build context is available
        IReadOnlyDictionary<string, string> contextPacks;
        if (buildContext is not null && _contextPackProvider is IScopedContextPackProvider scopedProvider)
        {
            contextPacks = await scopedProvider.GetContextPacksAsync(context, buildContext, cancellationToken);
        }
        else
        {
            contextPacks = await _contextPackProvider.GetContextPacksAsync(context, cancellationToken);
        }

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

    // PATCH 29.04: Compact system prompt - no guessing, user language, tool outputs only
    // PATCH 34.09: Hallucination guard rules
    private static string BuildBaseSystemPrompt(TilsoftExecutionContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are TILSOFTAI. Strict rules:");
        sb.AppendLine("1. Use tools for facts; never guess missing data.");
        sb.AppendLine("2. Follow tool outputs strictly.");
        sb.AppendLine("3. Reply in user's language unless asked otherwise.");
        sb.AppendLine("4. CRITICAL: If a field is missing, null, or absent from tool output, report it as 'không có dữ liệu' (no data available). NEVER invent, estimate, or fill in values.");
        sb.AppendLine("5. If key information is missing after a tool call and follow-up tools exist, call them before responding.");
        sb.AppendLine("6. Always cite which tool provided each piece of information.");
        return sb.ToString();
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
