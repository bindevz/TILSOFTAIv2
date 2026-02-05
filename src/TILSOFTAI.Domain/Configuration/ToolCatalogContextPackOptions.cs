namespace TILSOFTAI.Domain.Configuration;

public sealed class ToolCatalogContextPackOptions
{
    /// <summary>
    /// PATCH 29.04: When false (default), tool catalog is NOT included in system prompt.
    /// Tools are provided via the tools payload directly; no duplication needed.
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    public int MaxTools { get; set; } = 40;
    public int MaxTotalTokens { get; set; } = 900;
    public int MaxInstructionTokensPerTool { get; set; } = 60;
    public int MaxDescriptionTokensPerTool { get; set; } = 30;
    public string[] PreferTools { get; set; } = Array.Empty<string>();
}
