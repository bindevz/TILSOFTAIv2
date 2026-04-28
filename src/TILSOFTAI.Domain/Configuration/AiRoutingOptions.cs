namespace TILSOFTAI.Domain.Configuration;

public sealed class AiRoutingOptions
{
    public bool MicrosoftAgentFrameworkRoutingEnabled { get; set; }
    public bool FallbackToLegacyPipeline { get; set; } = true;
    public bool EnableDynamicToolDescriptions { get; set; } = true;
    public int MaxCandidateDomains { get; set; } = 2;
    public int MaxCandidateToolsPerDomain { get; set; } = 6;
    public int MaxTotalCandidateTools { get; set; } = 12;
    public int MaxToolCallsPerTurn { get; set; } = 3;
    public bool AllowParallelReadTools { get; set; } = true;
    public bool AllowParallelWriteTools { get; set; }
    public bool AllowModelSelectedMultiTool { get; set; }
    public bool PreferCompositeCapability { get; set; } = true;
    public bool EnableWritePreviewTools { get; set; }
    public string[] EnabledTenantIds { get; set; } = Array.Empty<string>();
    public string[] EnabledUserIds { get; set; } = Array.Empty<string>();
    public string[] PriorityReadOnlyCapabilityKeys { get; set; } = Array.Empty<string>();
}
