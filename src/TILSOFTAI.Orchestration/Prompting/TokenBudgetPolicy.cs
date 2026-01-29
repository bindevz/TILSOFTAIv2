using TILSOFTAI.Orchestration.Llm;

namespace TILSOFTAI.Orchestration.Prompting;

public sealed class TokenBudgetPolicy
{
    public IReadOnlyList<LlmMessage> Apply(
        string systemPrompt,
        IReadOnlyList<LlmMessage> messages,
        int maxTokens,
        int retainAssistantMessages,
        int contextPackChars)
    {
        if (messages.Count == 0)
        {
            return messages;
        }

        var maxChars = PromptBudget.GetMaxMessagesChars(maxTokens, systemPrompt?.Length ?? 0, contextPackChars);
        var trimmed = messages.ToList();

        var lastUser = trimmed.LastOrDefault(message => message.Role == "user");
        var keepAssistants = trimmed
            .Where(message => message.Role == "assistant")
            .Reverse()
            .Take(Math.Max(0, retainAssistantMessages))
            .ToHashSet();

        while (EstimateMessagesSize(trimmed) > maxChars)
        {
            if (RemoveFirst(trimmed, message => message.Role == "tool"))
            {
                continue;
            }

            if (RemoveFirst(trimmed, message => message.Role == "assistant" && !keepAssistants.Contains(message)))
            {
                continue;
            }

            if (RemoveFirst(trimmed, message => message.Role == "user" && message != lastUser))
            {
                continue;
            }

            break;
        }

        return trimmed;
    }

    private static bool RemoveFirst(List<LlmMessage> messages, Func<LlmMessage, bool> predicate)
    {
        var index = messages.FindIndex(predicate.Invoke);
        if (index < 0)
        {
            return false;
        }

        messages.RemoveAt(index);
        return true;
    }

    private static int EstimateMessagesSize(IReadOnlyList<LlmMessage> messages)
    {
        var size = 0;
        foreach (var message in messages)
        {
            size += message.Content.Length;
        }
        return size;
    }
}
