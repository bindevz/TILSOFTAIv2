namespace TILSOFTAI.Domain.Configuration;

public sealed class ModulesOptions
{
    public bool EnableLegacyAutoload { get; set; }

    public string[] Enabled { get; set; } = Array.Empty<string>();

    public Dictionary<string, string> Classifications { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// PATCH 37.01: Minimal bootstrap fallback when both DB and Enabled are empty.
    /// Should contain only critical modules like platform.
    /// </summary>
    public string[] FallbackEnabled { get; set; } = new[]
    {
        "TILSOFTAI.Modules.Platform"
    };
}
