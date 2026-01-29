namespace TILSOFTAI.Domain.Configuration;

public sealed class ChatOptions
{
    public int MaxSteps { get; set; } = 12;
    public int MaxTokens { get; set; } = 4096;
    public int MaxToolCallsPerRequest { get; set; } = 8;
    public int MaxRecursiveDepth { get; set; } = 3;
    public Dictionary<string, int> CompactionLimits { get; set; } = new();
    public CompactionRules CompactionRules { get; set; } = new();
}
