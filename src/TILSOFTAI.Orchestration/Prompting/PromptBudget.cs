namespace TILSOFTAI.Orchestration.Prompting;

public static class PromptBudget
{
    public const int CharsPerToken = 4;
    public const int MaxSystemPromptChars = 4000;
    public const int MaxContextPacksChars = 6000;

    public static int GetMaxMessagesChars(int maxTokens, int systemPromptChars, int contextPackChars)
    {
        var totalChars = Math.Max(0, maxTokens) * CharsPerToken;
        var budget = totalChars - systemPromptChars - contextPackChars;
        return Math.Max(256, budget);
    }
}
