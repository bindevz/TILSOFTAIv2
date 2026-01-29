namespace TILSOFTAI.Orchestration.Conversations;

public sealed class ChatMessage
{
    public ChatMessage(string role, string content, string? name = null)
    {
        Role = role;
        Content = content;
        Name = name;
    }

    public string Role { get; }
    public string Content { get; }
    public string? Name { get; }
}
