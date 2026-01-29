namespace TILSOFTAI.Orchestration.Tools;

public sealed class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public string JsonSchema { get; set; } = string.Empty;
    public string? SpName { get; set; }
    public string[] RequiredRoles { get; set; } = Array.Empty<string>();
    public string Module { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
