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
    /// <summary>
    /// DEPRECATED (Patch 35): Tool ordering is now driven by core_then_scope_order strategy.
    /// Retained for backward compatibility with existing config files.
    /// </summary>
    [Obsolete("Use RuntimePolicy 'tool_catalog_context_pack' with orderStrategy instead. PreferTools will be removed in a future patch.")]
    public string[] PreferTools { get; set; } = Array.Empty<string>();
}
