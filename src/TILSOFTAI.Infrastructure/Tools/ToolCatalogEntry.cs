namespace TILSOFTAI.Infrastructure.Tools;

public sealed class ToolCatalogEntry
{
    public string ToolName { get; set; } = string.Empty;
    public string SpName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? RequiredRoles { get; set; }
    public string? JsonSchema { get; set; }
    public string? Instruction { get; set; }
    public string? Description { get; set; }
}
