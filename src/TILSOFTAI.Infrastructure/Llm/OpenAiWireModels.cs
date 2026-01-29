namespace TILSOFTAI.Infrastructure.Llm;

internal sealed class OpenAiChatRequest
{
    public string Model { get; set; } = string.Empty;
    public List<OpenAiMessage> Messages { get; set; } = new();
    public List<OpenAiTool>? Tools { get; set; }
    public object? ToolChoice { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public bool Stream { get; set; }
}

internal sealed class OpenAiMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Name { get; set; }
}

internal sealed class OpenAiTool
{
    public string Type { get; set; } = "function";
    public OpenAiFunction Function { get; set; } = new();
}

internal sealed class OpenAiFunction
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? Parameters { get; set; }
}
