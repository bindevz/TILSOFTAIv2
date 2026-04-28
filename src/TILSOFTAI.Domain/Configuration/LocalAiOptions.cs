namespace TILSOFTAI.Domain.Configuration;

public sealed class LocalAiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ChatCompletionsPath { get; set; } = "/chat/completions";
    public string Model { get; set; } = string.Empty;
    public string ApiKeyEnvironmentVariable { get; set; } = "TILSOFTAI_LOCAL_AI_API_KEY";
    public int TimeoutSeconds { get; set; } = 120;
}
