namespace TILSOFTAI.Domain.Configuration;

public sealed class GovernanceOptions
{
    public string[] ToolAllowlist { get; set; } = Array.Empty<string>();
    public string ModelCallableSpPrefix { get; set; } = "ai_";
    public string InternalSpPrefix { get; set; } = "app_";
    public Dictionary<string, string[]> ToolRoleRequirements { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
