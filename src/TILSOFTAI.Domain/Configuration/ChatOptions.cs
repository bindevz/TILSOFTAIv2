namespace TILSOFTAI.Domain.Configuration;

public sealed class ChatOptions
{
    public int MaxSteps { get; set; } = ConfigurationDefaults.Chat.MaxSteps;
    public int MaxTokens { get; set; } = 4096;
    public int MaxToolCallsPerRequest { get; set; } = ConfigurationDefaults.Chat.MaxToolCallsPerRequest;
    public int MaxRecursiveDepth { get; set; } = 3;
    public int MaxInputChars { get; set; } = ConfigurationDefaults.Chat.MaxInputChars;
    public int MaxRequestBytes { get; set; } = (int)ConfigurationDefaults.Chat.MaxRequestBytes;
    public int MaxMessages { get; set; } = ConfigurationDefaults.Chat.MaxMessages;
    public Dictionary<string, int> CompactionLimits { get; set; } = new();
    public CompactionRules CompactionRules { get; set; } = new();
}
