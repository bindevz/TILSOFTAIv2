namespace TILSOFTAI.Domain.Configuration;

public sealed class ChatOptions
{
    public int MaxSteps { get; set; } = 12;
    public int MaxTokens { get; set; } = 4096;
    public int MaxToolCallsPerRequest { get; set; } = 8;
    public int MaxRecursiveDepth { get; set; } = 3;
    public int MaxInputChars { get; set; } = 8000;
    public int MaxRequestBytes { get; set; } = 262144; // 256KB
    public int MaxMessages { get; set; } = 50;
    public Dictionary<string, int> CompactionLimits { get; set; } = new();
    public CompactionRules CompactionRules { get; set; } = new();
}
