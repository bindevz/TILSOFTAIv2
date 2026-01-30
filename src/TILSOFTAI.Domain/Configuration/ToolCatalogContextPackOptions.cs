namespace TILSOFTAI.Domain.Configuration;

public sealed class ToolCatalogContextPackOptions
{
    public int MaxTools { get; set; } = 40;
    public int MaxTotalTokens { get; set; } = 900;
    public int MaxInstructionTokensPerTool { get; set; } = 60;
    public int MaxDescriptionTokensPerTool { get; set; } = 30;
    public string[] PreferTools { get; set; } = Array.Empty<string>();
}
