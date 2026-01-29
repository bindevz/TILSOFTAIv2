namespace TILSOFTAI.Orchestration.Llm;

public sealed class LlmMessage
{
    public LlmMessage(string role, string content, string? name = null)
    {
        Role = role;
        Content = content;
        Name = name;
    }

    public string Role { get; }
    public string Content { get; }
    public string? Name { get; }
}
