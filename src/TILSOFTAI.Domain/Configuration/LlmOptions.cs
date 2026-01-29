namespace TILSOFTAI.Domain.Configuration;

public sealed class LlmOptions
{
    public string Provider { get; set; } = "Null";
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
    public double Temperature { get; set; } = 0.2;
    public int MaxResponseTokens { get; set; } = 1024;
}
